using System;
using System.Collections.Generic;
using System.Text;
using SpaceClaim.Api.V10.Geometry;

namespace SpaceClaim.AddInLibrary {
	class Quaternion {
		double w, x, y, z;

		Quaternion(double w, double x, double y, double z) {
			this.w = w;
			this.x = x;
			this.y = y;
			this.z = z;
		}

		Quaternion(double s, Vector v)
			: this(s, v.X, v.Y, v.Z) {
		}

		public static Quaternion Create(double t, double x, double y, double z) {
			return new Quaternion(t, x, y, z);
		}

		public static Quaternion Create(double s, Vector v) {
			return new Quaternion(s, v);
		}

		public double W {
			get { return w; }
			set { w = value; }
		}

		public double X {
			get { return x; }
			set { x = value; }
		}

		public double Y {
			get { return y; }
			set { y = value; }
		}

		public double Z {
			get { return z; }
			set { z = value; }
		}

		public double Scalar {
			get { return w; }
			set { w = value; }
		}

		public Vector Vector {
			get { return Vector.Create(x, y, z); }
			set {
				x = value.X;
				y = value.Y;
				z = value.Z;
			}
		}

		public static Quaternion operator -(Quaternion a) {
			return new Quaternion(-a.W, -a.X, -a.Y, -a.Z);
		}

		public static Quaternion operator +(Quaternion a, Quaternion b) {
			return new Quaternion(a.W + b.W, a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		public static Quaternion operator *(Quaternion a, Quaternion b) {
			return new Quaternion(
				a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
				a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
				a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
				a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W
				);
		}

		public static Quaternion operator *(double s, Quaternion a) {
			return new Quaternion(a.W * s, a.X * s, a.Y * s, a.Z * s);
		}

		public static Quaternion operator *(Quaternion a, double s) {
			return new Quaternion(a.W * s, a.X * s, a.Y * s, a.Z * s);
		}

		public static Quaternion operator /(Quaternion a, double divisor) {
			return a * (double) 1 / divisor;
		}

		public Quaternion Conjugate {
			get { return new Quaternion(w, -x, -y, -z); }
		}

		public Quaternion Inverse {
			get { return Conjugate / (this * Conjugate).Scalar; }
		}

		public double Magnitude {
			get { return Math.Sqrt(w * w + x * x + y * y + z * z); }
		}

		public override string ToString() {
			return W.ToString() + " + " + X.ToString() + "i + " + Y.ToString() + "j + " + Z.ToString() + "k";
		}

	}
}
