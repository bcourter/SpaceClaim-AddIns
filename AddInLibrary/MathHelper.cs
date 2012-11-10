using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpaceClaim.AddInLibrary {
	public static class MathHelper {

		public static double Asinh(double z) {
			return Math.Log(z + Math.Sqrt(z * z + 1));
		}

		public static double Acosh(double z) {
			return Math.Log(z + Math.Sqrt(z + 1) * Math.Sqrt(z - 1));
		}

		public static double Atanh(double z) {
			return 0.5 * (Math.Log(1 + z) - Math.Log(1 - z));
		}

		public static double Acsch(double z) {
			return Math.Log(1 / z + Math.Sqrt(1 / (z * z) + 1));
		}

		public static double Asech(double z) {
			return Math.Log(1 / z + Math.Sqrt(1 / z + 1) * Math.Sqrt(1 / z - 1));
		}

		public static double Acoth(double z) {
			return 0.5 * (Math.Log(1 + 1 / z) - Math.Log(1 - 1 / z));
		}

		public static double IntegrateSimple(double x1, double x2, int steps, Func<double, double> func) {
		    double stepSize = (x2 - x1) / steps;
			double x = x1;
			double yLast = func(x);
			double y;

		    double sum = 0;
		    for (int i = 0; i < steps; i++) {
				x += stepSize;
				y = func(x);
				sum += (y + yLast) * stepSize / 2;
				yLast = y;
		    }

			return sum;
		}

	}
}
