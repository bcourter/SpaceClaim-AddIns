using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using Point = SpaceClaim.Api.V10.Geometry.Point;

/// <summary>
/// AddIn helper functions and other utilities
/// </summary>

namespace SpaceClaim.AddInLibrary {
	public static class DebugExtensions {
		public static void Print(this ITrimmedCurve curve, Part part = null) {
			DesignCurve.Create(part ?? MainPart, curve);
		}

		public static void Print(this Curve curve, Part part = null) {
			CurveSegment.Create(curve, curve.Parameterization.GetReasonableInterval()).Print();
		}

		public static void Print(this Point point, Part part = null) {
			point.AsITrimmedCurve().Print(part);
		}

		public static void Print(this Body body, Part part = null) {
			body = body.Copy();

			if (body.PieceCount > 1) {
				ICollection<Body> bodies = body.SeparatePieces();
				Debug.Assert(bodies.Count == body.PieceCount);
				bodies.Print();
				return;
			}

			try {
				DesignBody.Create(part ?? MainPart, GetDebugString(body), body);
			}
			catch { }
		}

		public static void Print(this ICollection<Body> bodies, Part part = null) {
			foreach (Body body in bodies)
				body.Print(part);
		}

		public static void Print(this ICollection<ITrimmedCurve> curves, Part part = null) {
			foreach (ITrimmedCurve curve in curves)
				curve.Print(part);
		}

		public static void Print(this ICollection<Curve> curves, Part part = null) {
			foreach (Curve curve in curves)
				curve.Print(part);
		}

        public static void Print(this ICollection<Point> points, Part part = null) {
            foreach (Point point in points)
                point.Print(part);
        }

        public static void Print(this Plane plane, Part part = null) {
            DatumPlane.Create(part ?? MainPart, GetDebugString(plane), plane);
        }

        public static void Print(this Cone cone, Part part = null) {
			double startV = -Math.Cos(cone.HalfAngle) * cone.Radius;
			double endV = 0;

			ShapeHelper.CreateRevolvedCurve(cone.Axis, CurveSegment.Create(
				cone.Evaluate(PointUV.Create(0, startV)).Point,
				cone.Evaluate(PointUV.Create(0, endV)).Point
			)).Print(part);

			//	Circle.Create(cone.Frame, cone.Radius).Print(part);
			//	CurveSegment.Create(cone.Evaluate(PointUV.Create(0, 0)).Point, cone.Evaluate(PointUV.Create(Math.PI / 2, 0)).Point).Print(part);
		}

		public static string GetDebugString(Object obj) {
			return "Debug " + obj.GetType().ToString();
		}

		public static Window ActiveWindow {
			get { return Window.ActiveWindow; }
		}

		public static Part MainPart {
			get { return ActiveWindow.Scene as Part; }
		}

	}
}
