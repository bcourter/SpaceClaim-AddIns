using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpaceClaim.AddInLibrary {
	public struct Complex : IEquatable<Complex> {
		double re, im;

		public Complex(double re, double im) {
			this.re = re;
			this.im = im;
		}

		public static Complex CreatePolar(double modulus, double arg) {
			return new Complex(modulus * Math.Cos(arg), modulus * Math.Sin(arg));
		}

		public static readonly Complex Zero = new Complex(0, 0);

		public static readonly Complex I = new Complex(0, 1); 

		public static bool operator ==(Complex a, Complex b) {
			return a.re == b.re && a.im == b.im;
		}

		public static bool operator !=(Complex a, Complex b) {
			return !(a == b);
		}

		public static Complex operator +(Complex a, Complex b) {
			return new Complex(a.re + b.re, a.im + b.im);
		}

		public static Complex operator +(double s, Complex a) {
			return new Complex(a.re + s, a.im);
		}

		public static Complex operator +(Complex a, double s) {
			return new Complex(a.re + s, a.im);
		}

		public static Complex operator -(Complex a) {
			return new Complex(-a.re, -a.im);
		}

		public static Complex operator -(Complex a, Complex b) {
			return new Complex(a.re - b.re, a.im - b.im);
		}

		public static Complex operator -(double s, Complex a) {
			return new Complex(s - a.re, -a.im);
		}

		public static Complex operator -(Complex a, double s) {
			return new Complex(a.re - s, a.im);
		}

		public static Complex operator *(Complex a, Complex b) {
			return new Complex(a.re * b.re - a.im * b.im, a.re * b.im + a.im * b.re);
		}

		public static Complex operator *(double s, Complex a) {
			return new Complex(s * a.re, s * a.im);
		}

		public static Complex operator *(Complex a, double s) {
			return new Complex(s * a.re, s * a.im);
		}

		public static Complex operator /(Complex a, Complex b) {
			double divisor = b.re*b.re + b.im*b.im;
			return new Complex((a.re * b.re + a.im * b.im) / divisor, (-a.re * b.im + a.im * b.re) / divisor);
		}

		public static Complex operator /(double s, Complex a) {
			return new Complex(s, 0) / a;
		}

		public static Complex operator /(Complex a, double s) {
			return new Complex(a.re / s, a.im / s);
		}

		public Complex Pow(double n) {
			if (this == Zero)
				return Zero;

			double nArg = n * this.Arg;
			return Math.Pow(this.Modulus, n) * new Complex(Math.Cos(nArg), Math.Sin(nArg));
		}

		//public static Complex Exp(Complex z) {
		//    return XXXXX
		//}

		public double Re {
			get { return re; }
			set { re = value; }
		}

		public double Im {
			get { return im; }
			set { im = value; }
		}

		public Complex Conjugate {
			get { return new Complex(re, -im); }
		}

		public double Arg {
			get { return Math.Atan2(im, re); }
		}

		public double Modulus {
			get { return Math.Sqrt(re * re + im * im); }
		}

		#region IEquatable<Complex> Members

		public bool Equals(Complex other) {
			return this == other;
		}

		#endregion

		#region Object Members

		public override bool Equals(object obj) {
			if (obj is Complex)
				return (Complex) obj == this;

			return false;
		}

		public override int GetHashCode() {
			return re.GetHashCode() ^ im.GetHashCode();
		}

		#endregion

	}
}
