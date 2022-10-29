using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace ScaleExtensions
{
	public static class MegaDriveExtensions
	{
		/// <summary>
		/// Array that represents the Mega Drive VDP Color ramp. Source - https://plutiedev.com/vdp-color-ramp
		/// </summary>
		public static List<int> VDP_Ramp = new List<int>() { 0, 52, 87, 116, 144, 172, 206, 255 };

		/// <summary>
		/// Convert a color to the VDP Ramp table equivilent (todo - use something like CIE2000 rather than just closest average?)
		/// </summary>
		/// <param name="color">The color to convert</param>
		/// <returns>The color in MD palette format</returns>
		public static int ConvertToMDColor(this Color color) => (VDP_Ramp_Match(color.B) << 9) | (VDP_Ramp_Match(color.G) << 5) | (VDP_Ramp_Match(color.R) << 1);

		/// <summary>
		/// Finds the closest match on the VDP ramp table, using averages between two points
		/// </summary>
		/// <param name="channel">The channel intensity 0-255</param>
		/// <returns>The matching VDP color intensity index</returns>
		private static int VDP_Ramp_Match(byte channel)
		{
			for (var i = 0; i < VDP_Ramp.Count - 1; i++)
			{
				if (
					//The color is equal to or less than the current color
					channel <= VDP_Ramp[i]

					//The color is less than the average between this and the next color
					|| channel < (VDP_Ramp[i] + VDP_Ramp[i + 1]) / 2)
				{
					return i;
				}
			}
			return VDP_Ramp.Count - 1; //Must be 255 or higher
		}


		/// <summary>
		/// Converts an image into raw palette, pattern and nametable bytes
		/// </summary>
		/// <param name="image">The image to convert</param>
		/// <param name="outputPalettePath">Where to save the palette bytes</param>
		/// <param name="outputPatternPath">Where to save the pattern bytes</param>
		/// <param name="outputNametablePath">Where to save the nametable bytes</param>
		public static void ConvertToMegaDriveRaw(this Bitmap image, string outputPalettePath, string outputPatternPath, string outputNametablePath)
		{
			if (image.Width % 8 != 0 || image.Height % 8 != 0)
			{
				throw new Exception("Image dimensions are not a multiple of 8");
			}

			//Add up to four palettes
			var palettes = new List<List<int>>();

			//Add as many patterns as needed
			var patterns = new List<List<int>>();

			var nameTable = new List<int>();

			for (var tileY = 0; tileY < image.Height / 8; tileY++)
			{
				for (var tileX = 0; tileX < image.Width / 8; tileX++)
				{
					//First pass - we get all of the colors
					var allColors = new List<int>();
					var neededColors = new HashSet<int>();

					for (var y = 0; y < 8; y++)
					{
						for (var x = 0; x < 8; x++)
						{
							var color = image.GetPixel(tileX * 8 + x, tileY * 8 + y).ConvertToMDColor();
							allColors.Add(color);
							neededColors.Add(color);
						}
					}

					//Too many colors in tile
					if (neededColors.Count > 16)
					{
						throw new Exception(string.Format("Too many colors in tile at {0}:{1}", tileX * 8, tileY * 8));
					}

					//Find the best palette for this block
					var paletteIndex = FindPalette(palettes, neededColors);
					var palette = palettes[paletteIndex];
					if (palettes.Count > 4)
					{
						throw new Exception(string.Format("Too many palettes needed from tile  at {0}:{1}", tileX * 8, tileY * 8));
					}

					//Now we generate a pattern
					var pattern = new List<int>();
					foreach (var color in allColors)
					{
						pattern.Add(palette.IndexOf(color));
					}
					nameTable.Add(FindPattern(pattern, patterns) | (paletteIndex << 13));
				}
			}

			//Save the palette
			var bytes = new List<byte>();
			for (var i = 0; i < 4; i++)
			{
				if (i >= palettes.Count)
				{
					//Create an empty palette
					for (var j = 0; j < 32; j++)
					{
						bytes.Add(0);
					}
				}
				else
				{
					for (var j = 0; j < 16; j++)
					{
						if (j >= palettes[i].Count)
						{
							//Missing palette index
							bytes.Add(0);
							bytes.Add(0);
						}
						else
						{
							bytes.Add((byte)(palettes[i][j] >> 8));
							bytes.Add((byte)(palettes[i][j] & 0xff));
						}
					}
				}
			}
			File.WriteAllBytes(outputPalettePath, bytes.ToArray());

			//the patterns
			bytes.Clear();
			foreach (var pattern in patterns)
			{
				for (var i = 0; i < pattern.Count; i += 2)
				{
					bytes.Add((byte)((pattern[i] << 4) | (pattern[i + 1] & 0xf)));
				}
			}
			File.WriteAllBytes(outputPatternPath, bytes.ToArray());

			//The nametable
			bytes.Clear();
			foreach (var tile in nameTable)
			{
				bytes.Add((byte)(tile >> 8));
				bytes.Add((byte)(tile & 0xff));
			}
			File.WriteAllBytes(outputNametablePath, bytes.ToArray());


		}

		/// <summary>
		/// Find a matching pattern - otherwise add it to the list of patterns
		/// </summary>
		/// <param name="pattern">The pattern to find</param>
		/// <param name="patterns">The full list of patterns. Will add this pattern if it's not present</param>
		/// <returns>The offset of the matching pattern</returns>
		private static int FindPattern(List<int> pattern, List<List<int>> patterns)
		{
			for (var patternId = 0; patternId < patterns.Count; patternId++)
			{
				var result = PatternMatches(pattern, patterns[patternId]);
				if (result > -1)
				{
					//Result contains if H and V is flipped
					return result | patternId;
				}
			}

			//This pattern doesn't exist at all
			patterns.Add(pattern);
			return patterns.Count - 1;
		}

		/// <summary>
		/// Do these patterns match?
		/// </summary>
		/// <param name="pattern">The first pattern</param>
		/// <param name="pattern2">The second pattern</param>
		/// <returns>-1 if no match, otherwise 0 and the horizontal and vertical flip bits</returns>
		private static int PatternMatches(List<int> pattern, List<int> pattern2)
		{
			//Test the V and H flips
			for (var v = 0; v < 2; v++)
			{
				for (var h = 0; h < 2; h++)
				{
					//Check each pixel in the pattern
					var allMatched = true;
					for (var i = 0; i < pattern.Count; i++)
					{
						var xPixel = i % 8;
						var yPixel = i / 8;

						//Flip?
						if (v == 1)
						{
							yPixel = 7 - yPixel;
						}
						if (h == 1)
						{
							xPixel = 7 - xPixel;
						}
						var otherCoordinate = yPixel * 8 + xPixel;

						if (pattern[i] != pattern2[otherCoordinate])
						{
							allMatched = false;
							break;
						}
					}
					if (allMatched)
					{
						//We have a matching tile on at least one flip setting!
						return (v << 12) | (h << 11);
					}
				}
			}

			return -1; //No matches
		}

		/// <summary>
		/// From a hashset of colors needed, find a palette that matches all of them. Otherwise add a new palette
		/// </summary>
		/// <param name="palettes">The palettes to search</param>
		/// <param name="neededColors">A hashset of all colors needed</param>
		/// <returns>The palette line index</returns>
		private static int FindPalette(List<List<int>> palettes, HashSet<int> neededColors)
		{
			//Does a matching palette exist already?
			for (var i = 0; i < palettes.Count; i++)
			{
				var palette = palettes[i];
				if (neededColors.All(p => palette.Contains(p))) //ALL needed colors are in this palette
				{
					return i;
				}
			}

			//No existing palette - we need to try and add to a new one
			for (var i = 0; i < palettes.Count; i++)
			{
				//Attempt to generate a new one from each existing palette
				var newPalette = new List<int>(palettes[i]);

				foreach (var color in neededColors)
				{
					if (newPalette.Contains(color) == false)
					{
						newPalette.Add(color);
					}
				}

				//This palette is good enough to use
				if (newPalette.Count <= 16)
				{
					palettes[i] = newPalette;
					return i;
				}

			}

			//If we get to here, we need to add a whole new palette
			var newestPalette = new List<int>
			{
				0 //Always add black
			};
			newestPalette.AddRange(neededColors.Where(p => p != 0)); //Add all colors from needed colors (except for black)

			palettes.Add(newestPalette);
			return palettes.Count - 1;
		}

	}
}
