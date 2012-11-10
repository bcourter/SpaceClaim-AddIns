using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.AddInLibrary;
using Gear.Properties;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Gear {
	public abstract class ToothProfile {
		public GearData Data { get; set; }
		protected Matrix mirror = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirX), Math.PI);

		protected double baseRadius;
		protected double pitchRadius;
		protected double addendumRadius;
		protected double dedendumRadius;

		CurveSegment basicInvolute;
		protected double offsetAngle;
		protected Matrix involutePitchTrans;

		// Build the profile starting with one section
		protected CurveSegment curveA, curveB;
		protected CurveSegment topCurve;

		protected List<ITrimmedCurve> period = new List<ITrimmedCurve>();

		public ToothProfile(GearData gearData) {
			Data = gearData;
		}

		public virtual ICollection<ITrimmedCurve> GetProfile() {
			baseRadius = Data.BaseRadius;
			pitchRadius = Data.PitchRadius;
			addendumRadius = Data.AddendumRadius;
			dedendumRadius = Data.DedendumRadius;

			basicInvolute = CurveSegment.Create(Data.CreateInvolute());
			offsetAngle = Data.OffsetAngle;
			involutePitchTrans = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), offsetAngle);

			curveA = basicInvolute.CreateTransformedCopy(involutePitchTrans);
			curveB = curveA.CreateTransformedCopy(mirror);

			if (Data.TopStartAngle < Data.TopEndAngle) {
				topCurve = CurveSegment.Create(Data.AddendumCircle, Interval.Create(Data.TopStartAngle, Data.TopEndAngle));
				period.Add(topCurve);
			}
			else { // involutes intersect at top land
				ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersections = curveA.IntersectCurve(curveB.CreateTransformedCopy(Data.ToothTrans));
				Debug.Assert(intersections.Count == 1);
				if (intersections.Count != 1) {
					curveA.Print();
					curveB.CreateTransformedCopy(Data.ToothTrans).Print();
				}

				curveA = CurveSegment.Create(curveA.Geometry, Interval.Create(curveA.Bounds.Start, intersections.First().EvaluationA.Param));
				curveB = curveA.CreateTransformedCopy(mirror);
			}

			ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> bottomIntersections = curveA.IntersectCurve(curveB);
			if (bottomIntersections.Count > 0 && bottomIntersections.First().Point.Vector.Magnitude > dedendumRadius) { // the involutes intersect at bottom land
				curveA = CurveSegment.Create(curveA.Geometry, Interval.Create(curveA.IntersectCurve(curveB).ToArray()[0].EvaluationA.Param, curveA.Bounds.End));
				period.Add(curveA);
				period.Add(curveA.CreateTransformedCopy(mirror));

				return period;
			}

			return null;
		}
	}

	public class BasicToothProfile : ToothProfile {
		public BasicToothProfile(GearData gearData) : base(gearData) { }

		public override ICollection<ITrimmedCurve> GetProfile() {
			if (base.GetProfile() != null)
				return period;

			CurveSegment bottomCurve = CurveSegment.Create(Data.DedendumCircle);
			IntPoint<CurveEvaluation, CurveEvaluation> intersectionA = bottomCurve.IntersectCurve(curveA).ToArray()[0];
			IntPoint<CurveEvaluation, CurveEvaluation> intersectionB = bottomCurve.IntersectCurve(curveB).ToArray()[0];

			if (intersectionB.EvaluationA.Param > intersectionA.EvaluationA.Param) {
				period.Add(CurveSegment.Create(Data.DedendumCircle, Interval.Create(intersectionB.EvaluationA.Param, intersectionA.EvaluationA.Param)));

				curveA = CurveSegment.Create(curveA.Geometry, Interval.Create(intersectionA.EvaluationB.Param, curveA.Bounds.End));
				period.Add(curveA);
				period.Add(curveA.CreateTransformedCopy(mirror));
			}

			return period;
		}
	}

	public class ExtendedToothProfile : ToothProfile {
		public ExtendedToothProfile(GearData gearData) : base(gearData) { }

		public override ICollection<ITrimmedCurve> GetProfile() {
			if (base.GetProfile() != null)
				return period;

			period.Add(curveA);
			period.Add(curveB);

			CurveSegment bottomCurve = CurveSegment.Create(Data.DedendumCircle, Interval.Create(2 * Math.PI - offsetAngle, offsetAngle));
			period.Add(bottomCurve);
			period.Add(CurveSegment.Create(bottomCurve.StartPoint, curveB.StartPoint));
			period.Add(CurveSegment.Create(bottomCurve.EndPoint, curveA.StartPoint));

			return period;
		}
	}

	public class BasicTrochoidalToothProfile : ToothProfile {
		GearData ConjugateGearData { get; set; }
		public BasicTrochoidalToothProfile(GearData gearData, GearData conjugateGearData)
			: base(gearData) {
			ConjugateGearData = conjugateGearData;
		}

		public override ICollection<ITrimmedCurve> GetProfile() {
			IPart iPart = Window.ActiveWindow.Scene as Part;

			if (base.GetProfile() != null)
				return period;

			CurveSegment trochoidA = CurveSegment.Create(Data.CreateConjugateTrochoid(ConjugateGearData));
			ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersectionsInvolute = trochoidA.IntersectCurve(curveA);

			CurveSegment addendumCircle = null;
			Matrix conjugateTrans = Matrix.CreateTranslation(Vector.Create(Data.PitchRadius + ConjugateGearData.PitchRadius, 0, 0));

			// involute traced by only one corner
			//	addendumCircle = CurveSegment.Create(ConjugateGearData.AddendumCircle).CreateTransformedCopy(conjugateTrans);

			// ClosestSeparation extension technique, with scaling
			Circle largeCircle = Circle.Create(Frame.Create(conjugateTrans * Point.Origin, Direction.DirX, Direction.DirY), ConjugateGearData.PitchRadius + pitchRadius);
			Separation separation = CurveSegment.Create(largeCircle).GetClosestSeparation(trochoidA);
			Circle circle = Circle.Create(largeCircle.Frame, (separation.PointB - largeCircle.Frame.Origin).Magnitude - Accuracy.LinearResolution * 100);
			addendumCircle = CurveSegment.Create(circle);

			//// Three point circle techique
			//Matrix rotation = Matrix.CreateRotation(Line.Create(conjugateTrans * Point.Origin, Direction.DirZ), 2 * Math.PI / 3);
			//double param = trochoidA.Geometry.Parameterization.Range.Value.GetParameter(0.5);

			//CurveSegment curve0 = trochoidA.CreateTransformedCopy(Matrix.Identity);
			//CurveSegment curve1 = curve0.CreateTransformedCopy(rotation);
			//CurveSegment curve2 = curve1.CreateTransformedCopy(rotation);
			//Circle circle = Circle.CreateTangentToThree(
			//    Plane.PlaneXY,
			//    new CurveParam(curve0.Geometry, param),
			//    new CurveParam(curve1.Geometry, param),
			//    new CurveParam(curve2.Geometry, param)
			//);
			//				addendumCircle = CurveSegment.Create(Circle.Create(Frame.Create(circle.Frame.Origin, Direction.DirX, Direction.DirY), circle.Radius - Accuracy.LinearResolution * 10));

			if (intersectionsInvolute.Count == 0) { // error
				DesignCurve.Create(iPart, CurveSegment.Create(addendumCircle.Geometry));

				DesignCurve.Create(iPart, trochoidA);
				DesignCurve.Create(iPart, trochoidA.CreateTransformedCopy(mirror));

				DesignCurve.Create(iPart, curveA);
				DesignCurve.Create(iPart, curveA.CreateTransformedCopy(mirror));
				return null;
			}
			///		else {
			IntPoint<CurveEvaluation, CurveEvaluation> intersectionInvolute = intersectionsInvolute.ToArray()[0];

			addendumCircle = addendumCircle.CreateTransformedCopy(Matrix.CreateScale(1 / 1.00, (addendumCircle.Geometry as Circle).Frame.Origin));
			double maxDist = 0;

			ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersections = trochoidA.IntersectCurve(addendumCircle);
			IntPoint<CurveEvaluation, CurveEvaluation> intersectionOther = intersections.First();
			foreach (IntPoint<CurveEvaluation, CurveEvaluation> intersection in intersections) {
				double dist = Math.Abs(intersection.EvaluationA.Param - intersectionInvolute.EvaluationA.Param);
				if (dist > maxDist) {
					intersectionOther = intersection;
					maxDist = dist;
				}
			}

			//	period.Add(addendumCircle);
			if (maxDist == 0) { //error
				DesignCurve.Create(iPart, CurveSegment.Create(addendumCircle.Geometry));

				DesignCurve.Create(iPart, trochoidA);
				DesignCurve.Create(iPart, trochoidA.CreateTransformedCopy(mirror));

				DesignCurve.Create(iPart, curveA);
				DesignCurve.Create(iPart, curveA.CreateTransformedCopy(mirror));
				return null;
			}
			

				period.Add(CurveSegment.Create(addendumCircle.Geometry, Interval.Create(intersectionOther.EvaluationB.Param, 2 * Math.PI - intersectionOther.EvaluationB.Param)));
			//		}
			//CurveSegment trochoidB = trochoidA.CreateTransformedCopy(mirror);
			//intersectionOther = trochoidA.IntersectCurve(trochoidB).ToArray()[0];

			trochoidA = CurveSegment.Create(trochoidA.Geometry, Interval.Create(intersectionOther.EvaluationA.Param, intersectionInvolute.EvaluationA.Param));
			period.Add(trochoidA);
			period.Add(trochoidA.CreateTransformedCopy(mirror));

			curveA = CurveSegment.Create(curveA.Geometry, Interval.Create(intersectionInvolute.EvaluationB.Param, curveA.Bounds.End));
			period.Add(curveA);
			period.Add(curveA.CreateTransformedCopy(mirror));

			return period;
		}
	}

	public class ExtendedTrochoidalToothProfile : ToothProfile {
		GearData ConjugateGearData { get; set; }
		public ExtendedTrochoidalToothProfile(GearData gearData, GearData conjugateGearData)
			: base(gearData) {
			ConjugateGearData = conjugateGearData;
		}

		public override ICollection<ITrimmedCurve> GetProfile() {
			IPart iPart = Window.ActiveWindow.Scene as Part;

			if (base.GetProfile() != null)
				return period;

			CurveSegment trochoidA = CurveSegment.Create(Data.CreateConjugateTrochoid(ConjugateGearData));

			Line tangentLine = Line.CreateTangentToOne(Plane.PlaneXY, new CurveParam(trochoidA.Geometry, (trochoidA.Bounds.Start + trochoidA.Bounds.End) * 3 / 4), Point.Origin);
			ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersectionsInvoluteTangent = tangentLine.IntersectCurve(trochoidA.Geometry);
			double paramA, paramB;

			if (intersectionsInvoluteTangent.Count != 1) {  // Apparently, an ACIS bug can cause the intersection to miss even though the tangent line is perfect, so we work around it.
				Separation sep = tangentLine.GetClosestSeparation(trochoidA.Geometry);
				paramA = tangentLine.ProjectPoint(sep.PointA).Param;
				paramB = trochoidA.Geometry.ProjectPoint(sep.PointB).Param;
			}
			else {
				IntPoint<CurveEvaluation, CurveEvaluation> intersectionInvoluteTangent = intersectionsInvoluteTangent.ToArray()[0];
				paramA = intersectionInvoluteTangent.EvaluationA.Param;
				paramB = intersectionInvoluteTangent.EvaluationB.Param;
			}

			CurveSegment tangentLineSegment = CurveSegment.Create(tangentLine, Interval.Create(
				Data.DedendumRadius, // line origin is world origin in direction of tangency
				paramA
			));

			double tangentAngle = Math.Atan(tangentLine.Direction.Y / tangentLine.Direction.X);
			CurveSegment baseCircleSegment = CurveSegment.Create(Data.DedendumCircle, Interval.Create(2 * Math.PI - tangentAngle, tangentAngle));

			ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersectionsInvoluteTrochoid = trochoidA.IntersectCurve(curveA);
			if (intersectionsInvoluteTrochoid.Count == 0) { // error
				DesignCurve.Create(iPart, baseCircleSegment);
				DesignCurve.Create(iPart, tangentLineSegment);

				DesignCurve.Create(iPart, trochoidA);
				DesignCurve.Create(iPart, trochoidA.CreateTransformedCopy(mirror));

				DesignCurve.Create(iPart, curveA);
				DesignCurve.Create(iPart, curveA.CreateTransformedCopy(mirror));
				return null;
			}

			IntPoint<CurveEvaluation, CurveEvaluation> intersectionInvoluteTrochoid = intersectionsInvoluteTrochoid.ToArray()[0];


			// if (tangentLineSegment.IntersectCurve(curveA).Count > 0) an ACIS intersection bug prevents this from working.  e.g. 13 tooth gear with 20 degree pressure angle
			if (paramB > intersectionInvoluteTrochoid.EvaluationA.Param)
				return new ExtendedToothProfile(Data).GetProfile();

			trochoidA = CurveSegment.Create(trochoidA.Geometry, Interval.Create(paramB, intersectionInvoluteTrochoid.EvaluationA.Param));

			period.Add(trochoidA);
			period.Add(trochoidA.CreateTransformedCopy(mirror));

			curveA = CurveSegment.Create(curveA.Geometry, Interval.Create(intersectionInvoluteTrochoid.EvaluationB.Param, curveA.Bounds.End));
			period.Add(curveA);
			period.Add(curveA.CreateTransformedCopy(mirror));

			period.Add(tangentLineSegment);
			period.Add(tangentLineSegment.CreateTransformedCopy(mirror));

			period.Add(baseCircleSegment);

			return period;
		}
	}
}
