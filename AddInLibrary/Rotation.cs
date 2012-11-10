using System;
using System.Collections.Generic;
using System.Text;
using SpaceClaim.Api.V10.Geometry;

namespace SpaceClaim.AddInLibrary {
	class Rotation {
		Quaternion h;

		Rotation(Quaternion h) {
			this.h = h / h.Magnitude;
		}

		Rotation(double angle, Direction direction) {
			if (Accuracy.LengthIsZero(angle)) {
				angle = 0;
				direction = Direction.Zero;
			}

			h = Quaternion.Create(Math.Cos(angle / 2), direction.UnitVector * Math.Sin(angle / 2));
		}

		public static Rotation Create(double angle, Direction axis) {
			return new Rotation(angle, axis);
		}

		public static Rotation CreateFromMatrix(Matrix matrix) {
			double angle = 0;
			Line axis = null;

			if (matrix.TryGetRotation(out axis, out angle))
				return new Rotation(angle, axis.Direction);

			return Zero;
		}

		public static Rotation Zero {
			get { return new Rotation(Quaternion.Create(1, 0, 0, 0)); }
		}

		public Direction Direction {
			get { return h.Vector.Direction; }
		}

		public double Angle {
			get { return Math.Acos(h.W) * 2; }
		}

		public override string ToString() {
			return h.ToString();
		}

	}
}
