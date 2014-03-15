using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SpaceClaim.Api.V10.Geometry;


// totally incomplete right now
namespace SpaceClaim.AddInLibrary {
	public static class Interpolation {
		public static double Interpolate(double a, double b, double t) {
			return a + (b - a) * t;
		}

		public static Vector Interpolate(Vector a, Vector b, double t) {
			return Vector.Create(
				Interpolate(a.X, b.X, t),
				Interpolate(a.Y, b.Y, t),
				Interpolate(a.Z, b.Z, t)
				);
		}

		public static Point Interpolate(Point a, Point b, double t) {
			return Point.Create(
				Interpolate(a.X, b.X, t),
				Interpolate(a.Y, b.Y, t),
				Interpolate(a.Z, b.Z, t)
				);
		}

		public static Matrix Interpolate(Matrix a, Matrix b, double t) {
			Vector translation = b.Translation - a.Translation;
			double scale = (b.Scale - a.Scale) / a.Scale;
			Rotation rotation = Rotation.CreateFromMatrix(a.Inverse * b);

			return
                Matrix.CreateTranslation(a.Translation + translation * t) *
				Matrix.CreateScale(a.Scale + scale * t) *
				Matrix.CreateRotation(Line.Create(Point.Origin, rotation.Direction), rotation.Angle * t)
			;
		}

		public static double Bilinear(BoxUV bounds, double[,] values, PointUV t) {
			double divisor = (double) 1 / bounds.RangeU.Span / bounds.RangeV.Span;
			double dx1 = t.U - bounds.RangeU.Start;
			double dx2 = bounds.RangeU.End - t.U;
			double dy1 = t.V - bounds.RangeV.Start;
			double dy2 = bounds.RangeV.End - t.V;

			return (
				values[0, 0] * dx2 * dy2 +
				values[1, 0] * dx1 * dy2 +
				values[0, 1] * dx2 * dy1 +
				values[1, 1] * dx1 * dy1
			) * divisor;
		}

		public static Point Bilinear(BoxUV bounds, Point[,] values, PointUV t) {
			var xs = new double[2, 2];
			var ys = new double[2, 2];
			var zs = new double[2, 2];

			for (int i = 0; i < 2; i++) {
				for (int j = 0; j < 2; j++) {
					xs[i, j] = values[i, j].X;
					ys[i, j] = values[i, j].Y;
					zs[i, j] = values[i, j].Z;
				}
			}

			return Point.Create(
				Bilinear(bounds, xs, t),
				Bilinear(bounds, ys, t),
				Bilinear(bounds, zs, t)
			);
		}

		public static double Clamp(double minInput, double maxInput, double value, double minOutput, double maxOutput) {
			double ratio = (value - minInput) / (maxInput - minInput);
			ratio = Math.Max(0, ratio);
			ratio = Math.Min(1, ratio);
			return minOutput + (maxOutput - minOutput) * ratio;
		}
	}
}
