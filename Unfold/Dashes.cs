using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using SpaceClaim.AddIn.Unfold;
using Unfold.Properties;
using ExecutionContext = SpaceClaim.Api.V10.ExecutionContext;

namespace SpaceClaim.AddIn.Unfold {
	abstract class DashesPropertiesButtonCapsule : RibbonButtonCapsule {
		protected const double inches = 25.4 / 1000;
		protected double dashMinSize = 0.004;
		protected Window activeWindow;
		protected Layer dashLayer;
		protected Part part;

		public DashesPropertiesButtonCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, text, image, hint, parent, buttonSize) {

			Values[Resources.DashSizeText] = new RibbonCommandValue(dashMinSize);
		}
      
		protected override void OnExecute(Command command, Api.V10.ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			dashMinSize = Values[Resources.DashSizeText].Value;
			activeWindow = Window.ActiveWindow;
			part = activeWindow.ActiveContext.Context as Part;
			dashLayer = NoteHelper.CreateOrGetLayer(activeWindow.ActiveContext.Context.Document, "Dashes", System.Drawing.Color.AliceBlue);
		}
	}

	class DashesButtonCapsule : DashesPropertiesButtonCapsule {
		public DashesButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Dashes", Resources.DashesText, null, Resources.DashesHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);

			foreach (ITrimmedCurve iTrimmedCurve in activeWindow.GetAllSelectedITrimmedCurves())
				CreateDashes(iTrimmedCurve, part, dashMinSize, dashLayer);
		}

		public static void CreateDashes(ITrimmedCurve curve, Part part, double dashMinSize, Layer dashLayer) {
			double length = curve.Length;
			int count = (int) Math.Floor(length / dashMinSize / 2) * 2;
			if (curve.StartPoint != curve.EndPoint) // odd number when not closed curve
				count++;

			double lastParam = curve.Bounds.Start;
			double param;
			for (int i = 0; i < count; i++) {
				if (curve.Geometry.TryOffsetParam(lastParam, length / count, out param)) {
					if (i % 2 == 1) {
						DesignCurve dash = DesignCurve.Create(part, CurveSegment.Create(curve.Geometry, Interval.Create(lastParam, param)));
						dash.Layer = dashLayer;
					}

					lastParam = param;
				}
			}
		}
	}

	class DashChainButtonCapsule : DashesPropertiesButtonCapsule {
		public DashChainButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("DashChain", Resources.DashChainText, null, Resources.DashChainHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);

			List<ITrimmedCurve> trimmedCurves = new List<ITrimmedCurve>();
			foreach (ITrimmedCurve trimmedCurve in activeWindow.GetAllSelectedITrimmedCurves())
				trimmedCurves.Add(trimmedCurve);

			TrimmedCurveChain curveChain = new TrimmedCurveChain(trimmedCurves);
			double length = curveChain.Length;

			int count = (int) Math.Floor(length / dashMinSize / 2) * 2;
			if (curveChain.StartPoint != curveChain.EndPoint) // odd number when not closed curve
				count++;

			List<DesignCurve> dashes = new List<DesignCurve>();
			double lastParam = curveChain.Bounds.Start;
			Point lastPoint;
			Debug.Assert(curveChain.TryGetPointAlongCurve(lastParam, out lastPoint));
			for (int i = 0; i < count; i++) {
				Point point;
				if (curveChain.TryGetPointAlongCurve(lastParam -= length / count, out point)) {
					if (i % 2 == 1) {
						DesignCurve dash = DesignCurve.Create(part, CurveSegment.Create(lastPoint, point));
						dash.Layer = dashLayer;
						dashes.Add(dash);
					}
#if false // tori
					ShapeHelper.CreateTorus(
						new Point[] { point, lastPoint }.Average(),
						(point - lastPoint).Direction,
						0.188 * inches,
						0.75 * inches,
						part
					);
#endif
					lastPoint = point;
				}
			}
#if false // cylinders
			for (int i = 1; i < count; i++) {
				CurveEvaluation eval = dashes[i].Shape.Geometry.Evaluate(dashes[i].Shape.Bounds.Start);
				Direction dir1 = eval.Tangent;

				eval = dashes[i - 1].Shape.Geometry.Evaluate(dashes[i - 1].Shape.Bounds.End);
				Direction dir2 = eval.Tangent;

				if (dir1 == dir2) {
					DatumPlane.Create(part, "miter parallel", Plane.Create(Frame.Create(eval.Point, eval.Tangent.ArbitraryPerpendicular, Direction.Cross(eval.Tangent.ArbitraryPerpendicular, eval.Tangent))));
					continue;
				}

				Direction averageDir = (dir1.UnitVector + dir2.UnitVector).Direction;
				Direction xDir = Direction.Cross(averageDir, dir1);
				//	DatumPlane.Create(part, "miter", Plane.Create(Frame.Create(eval.Point, xDir, Direction.Cross(xDir, averageDir))));
				double offset = 0.0001 / 2;
				ShapeHelper.CreateCylinder(eval.Point + averageDir * offset, eval.Point - averageDir * offset, 7 * inches, part);
			}
#endif
		}
	}

	class CopyCurvesButtonCapsule : DashesPropertiesButtonCapsule {
		public CopyCurvesButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("CopyCurves", Resources.CopyCurvesText, null, Resources.CopyCurvesHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);

			List<ITrimmedCurve> iTrimmedCurves = new List<ITrimmedCurve>(activeWindow.GetAllSelectedITrimmedCurves());
			List<ITrimmedCurve> uniqueCurves = new List<ITrimmedCurve>();
			uniqueCurves.Add(iTrimmedCurves[0]);
			iTrimmedCurves.RemoveAt(0);

			foreach (ITrimmedCurve candidate in iTrimmedCurves) {
				ITrimmedCurve notUnique = null;
				foreach (ITrimmedCurve uniqueCurve in uniqueCurves) {
					if (candidate.IsCoincident(uniqueCurve))
						notUnique = uniqueCurve;
				}

				if (notUnique != null) {
					uniqueCurves.Remove(notUnique);
					continue;
				}

				uniqueCurves.Add(candidate);
			}

			// Old crappy way
			//Dictionary<ITrimmedCurve, bool> uniqueCurves = new Dictionary<ITrimmedCurve, bool>(new ITrimmedCurveComparer());

			//foreach (ITrimmedCurve iTrimmedCurve in iTrimmedCurves) {
			//    if (!uniqueCurves.ContainsKey(iTrimmedCurve))
			//        uniqueCurves.Add(iTrimmedCurve, false);
			//    else
			//        uniqueCurves[iTrimmedCurve] = !uniqueCurves[iTrimmedCurve];
			//}

			//foreach (ITrimmedCurve iTrimmedCurve in uniqueCurves.Keys) {

			foreach (ITrimmedCurve iTrimmedCurve in uniqueCurves)
				DesignCurve.Create(part, iTrimmedCurve);


		}
	}

}
