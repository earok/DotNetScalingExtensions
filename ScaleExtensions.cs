using System;
using System.Drawing;

namespace ScaleExtensions
{
	public static class ScaleExtensions
	{
		/// <summary>
		/// Compares the distance between two colors
		/// </summary>
		/// <param name="e1">Color 1</param>
		/// <param name="e2">Color 2</param>
		/// <returns></returns>
		//https://stackoverflow.com/a/9085524
		static double Dist(Color e1, Color e2)
		{
			long rmean = ((long)e1.R + (long)e2.R) / 2;
			long r = (long)e1.R - (long)e2.R;
			long g = (long)e1.G - (long)e2.G;
			long b = (long)e1.B - (long)e2.B;
			return Math.Sqrt((((512 + rmean) * r * r) >> 8) + 4 * g * g + (((767 - rmean) * b * b) >> 8));
		}

		/// <summary>
		/// Are these colors more similar to each other than a third color?
		/// </summary>
		/// <param name="a">Color1</param>
		/// <param name="b">Color2</param>
		/// <param name="input">Compare Color</param>
		/// <returns></returns>
		static bool Similar(Color a, Color b, Color input)
		{
			if (a == b)
			{
				return true;
			}

			var dist = Dist(a, b);
			return dist < Dist(a, input) && dist < Dist(b, input);
		}

		/// <summary>
		/// Are these colors more different to each other than a third color?
		/// </summary>
		/// <param name="a">Color1</param>
		/// <param name="b">Color2</param>
		/// <param name="input">Compare Color</param>
		/// <returns></returns>
		static bool Different(Color a, Color b, Color input)
		{
			return !Similar(a, b, input);
		}

		/// <summary>
		/// Set the color of a pixel on a bitmap without exceeding boundaries
		/// </summary>
		/// <param name="bitmap">The bitmap</param>
		/// <param name="x">The X coordinate</param>
		/// <param name="y">The Y coordinate</param>
		/// <param name="color">The color</param>
		public static void SetPixelSafe(this Bitmap bitmap, int x, int y, Color color)
		{
			if (x < 0)
			{
				x = 0;
			}
			else if (x >= bitmap.Width)
			{
				x = bitmap.Width - 1;
			}

			if (y < 0)
			{
				y = 0;
			}
			else if (y >= bitmap.Height)
			{
				y = bitmap.Height - 1;
			}
			bitmap.SetPixel(x, y, color);
		}

		/// <summary>
		/// Get a scaled pixel from a bitmap without exceeding boundaries
		/// </summary>
		/// <param name="bitmap">The bitmap</param>
		/// <param name="x">The X pixel position</param>
		/// <param name="y">The Y pixel position</param>
		/// <param name="xScale">The x pixel scale</param>
		/// <param name="yScale">The y pixel scale</param>
		/// <returns></returns>
		public static Color GetPixelSafeScaled(this Bitmap bitmap, int x, int y, double xScale, double yScale)
		{
			return bitmap.GetPixelSafe((int)Math.Round(x * xScale), (int)Math.Round(y * yScale));
		}

		/// <summary>
		/// Get a pixel from a bitmap without exceeding boundaries
		/// </summary>
		/// <param name="bitmap">The bitmap</param>
		/// <param name="x">The X pixel position</param>
		/// <param name="y">The Y pixel position</param>
		/// <returns></returns>
		public static Color GetPixelSafe(this Bitmap bitmap, int x, int y)
		{
			if (x < 0)
			{
				x = 0;
			}
			else if (x >= bitmap.Width)
			{
				x = bitmap.Width - 1;
			}

			if (y < 0)
			{
				y = 0;
			}
			else if (y >= bitmap.Height)
			{
				y = bitmap.Height - 1;
			}

			return bitmap.GetPixel(x, y);
		}

		/// <summary>
		/// Scale bitmap using 2X algoritm
		/// </summary>
		/// <param name="bitmap">The source bitmap</param>
		/// <param name="useSimilar">If we should use similar or exact pixels for matching</param>
		/// <param name="disposeOfOriginal">If we want to dispose of the original image</param>
		/// <returns></returns>
		public static Bitmap Scale2X(this Bitmap bitmap, bool useSimilar = false, bool disposeOfOriginal = false)
		{
			var result = new Bitmap(bitmap.Width * 2, bitmap.Height * 2);

			for (var x = 0; x < bitmap.Width; x++)
			{
				for (var y = 0; y < bitmap.Height; y++)
				{
					var P = bitmap.GetPixel(x, y);
					var A = bitmap.GetPixelSafe(x + 0, y - 1);
					var B = bitmap.GetPixelSafe(x + 1, y + 0);
					var C = bitmap.GetPixelSafe(x - 1, y + 0);
					var D = bitmap.GetPixelSafe(x + 0, y + 1);

					if (useSimilar)
					{
						result.SetPixel(x * 2 + 0, y * 2 + 0, (Similar(C, A, P) && Different(C, D, P) && Different(A, B, P)) ? A : P);
						result.SetPixel(x * 2 + 1, y * 2 + 0, (Similar(A, B, P) && Different(A, C, P) && Different(B, D, P)) ? B : P);
						result.SetPixel(x * 2 + 0, y * 2 + 1, (Similar(D, C, P) && Different(D, B, P) && Different(C, A, P)) ? C : P);
						result.SetPixel(x * 2 + 1, y * 2 + 1, (Similar(B, D, P) && Different(B, A, P) && Different(D, C, P)) ? D : P);
					}
					else
					{
						result.SetPixel(x * 2 + 0, y * 2 + 0, (C == A && C != D && A != B) ? A : P);
						result.SetPixel(x * 2 + 1, y * 2 + 0, (A == B && A != C && B != D) ? B : P);
						result.SetPixel(x * 2 + 0, y * 2 + 1, (D == C && D != B && C != A) ? C : P);
						result.SetPixel(x * 2 + 1, y * 2 + 1, (B == D && B != A && D != C) ? D : P);
					}
				}
			}

			if (disposeOfOriginal)
			{
				bitmap.Dispose();
			}
			return result;
		}

		/// <summary>
		/// Scale bitmap using the nearest neighbour algorithm
		/// </summary>
		/// <param name="bitmap">The Bitmap</param>
		/// <param name="scaleX">The XScale</param>
		/// <param name="scaleY">The YScale</param>
		/// <param name="disposeOfOriginal">If we should dispose of the original bitmap</param>
		/// <returns></returns>
		public static Bitmap ScaleNearestNeighbor(this Bitmap bitmap, double scaleX, double scaleY, bool disposeOfOriginal = false)
		{
			if (scaleX <= 0 || scaleY <= 0)
			{
				throw new Exception("Scale must be greater than 0");
			}
			var result = new Bitmap((int)Math.Round(bitmap.Width * scaleX), (int)Math.Round(bitmap.Height * scaleY));

			for (var x = 0; x < result.Width; x++)
			{
				for (var y = 0; y < result.Height; y++)
				{
					result.SetPixel(x, y, bitmap.GetPixel((int)Math.Round(x / scaleX), (int)Math.Round(y / scaleY)));
				}
			}

			if (disposeOfOriginal)
			{
				bitmap.Dispose();
			}
			return result;
		}

		/// <summary>
		/// Scale a bitmap using Rotsprite
		/// </summary>
		/// <param name="bitmap">The Bitmap</param>
		/// <param name="scaleX">The XScale</param>
		/// <param name="scaleY">The YScale</param>
		/// <param name="disposeOfOriginal">If we should dispose of the original bitmap</param>
		/// <returns></returns>
		public static Bitmap ScaleRotSprite(this Bitmap bitmap, double scaleX, double scaleY, bool disposeOfOriginal = false)
		{
			var rotSprite = bitmap.Scale2X(true, false).Scale2X(true, true).Scale2X(true, true).ScaleNearestNeighbor(0.125 * scaleX, 0.125 * scaleY, true);
			var result = new Bitmap(rotSprite);

			//Apply restore single pixel details - ONLY if the scaleX or scaleY is less than 1
			if (scaleX < 1 || scaleY < 1)
			{
				for (var x = 0; x < rotSprite.Width; x++)
				{
					for (var y = 0; y < rotSprite.Height; y++)
					{
						//Need FOUR side to fix single pixel details?
						var matches = 0;

						if (GetPixelSafe(rotSprite, x + 1, y) == bitmap.GetPixelSafeScaled(x + 1, y, 1 / scaleX, 1 / scaleY))
						{
							matches++;
						}
						if (GetPixelSafe(rotSprite, x - 1, y) == bitmap.GetPixelSafeScaled(x - 1, y, 1 / scaleX, 1 / scaleY))
						{
							matches++;
						}
						if (GetPixelSafe(rotSprite, x, y - 1) == bitmap.GetPixelSafeScaled(x, y - 1, 1 / scaleX, 1 / scaleY))
						{
							matches++;
						}
						if (GetPixelSafe(rotSprite, x, y + 1) == bitmap.GetPixelSafeScaled(x, y + 1, 1 / scaleX, 1 / scaleY))
						{
							matches++;
						}

						if (matches >= 4) //If we're matching on all sides, use this pixel
						{
							result.SetPixel(x, y, bitmap.GetPixelSafeScaled(x, y, 1 / scaleX, 1 / scaleY));
							continue;
						}

						//Further check for single pixel detail
						if (matches == 3)
						{
							var targetPixel = bitmap.GetPixelSafeScaled(x, y, 1 / scaleX, 1 / scaleY);
							if (rotSprite.GetPixelSafe(x, y) != targetPixel)
							{
								var anyMatchingSidePixels = false;
								for (var x1 = -3; x1 < 4; x1++)
								{
									if (anyMatchingSidePixels)
									{
										break;
									}

									for (var y1 = -3; y1 < 4; y1++)
									{
										var dist = Math.Abs(x1) + Math.Abs(y1);
										if (dist == 0 || dist > 3)
										{
											continue;
										}

										if (rotSprite.GetPixelSafe(x + x1, y + y1) == targetPixel)
										{
											anyMatchingSidePixels = true;
											break;
										}
									}
								}

								if (anyMatchingSidePixels == false) //This is a single pixel in isolation "like an island", add it to the final result
								{
									result.SetPixel(x, y, targetPixel);
									continue;
								}

							}
						}
					}
				}
			}

			rotSprite.Dispose();
			if (disposeOfOriginal)
			{
				bitmap.Dispose();
			}
			return result;
		}

	}
}
