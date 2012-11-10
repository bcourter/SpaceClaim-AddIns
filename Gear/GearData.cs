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
	public class GearData {
		public int NumberOfTeeth { get; set; }
		public double PressureAngle { get; set; }
		public double Module { get; set; }
		public double DedendumClearance { get; set; }
		public double ScrewAngle { get; set; }
		public bool IsInternal { get; set; }

		static int pointCount = 10000;
		static double accuracy = Accuracy.LinearResolution; 

		double? offsetAngle = null;

		static Line axis = Line.Create(Point.Origin, Direction.DirZ);

		public GearData(int numberOfTeeth, double pressureAngle, double module, double dedendumClearance, bool isInternal, double screwAngle = 0) {
			NumberOfTeeth = numberOfTeeth;
			PressureAngle = pressureAngle;
			Module = module;
			DedendumClearance = dedendumClearance;
			ScrewAngle = screwAngle;
			IsInternal = isInternal;
		}

		public NurbsCurve CreateInvolute() {
			var points = new List<Point>();

			double gammaMax = GammaMax;
			double baseRadius = BaseRadius;
			for (int i = 0; i <= pointCount; i++) {
				double t = (double) i / pointCount;
				double gamma = t * gammaMax;
				double r = Math.Sqrt(gamma * gamma + baseRadius * baseRadius);
				double theta = (gamma / baseRadius - Math.Atan(gamma / baseRadius)) * CircumferentialScale;
				double x = r * Math.Cos(theta);
				double y = r * Math.Sin(theta);

				points.Add(Point.Create(x, y, 0));
			}

			return NurbsCurve.CreateThroughPoints(false, points, accuracy);
		}

		public NurbsCurve CreateConjugateTrochoid(GearData otherGearData) {
			var points = new List<Point>();

			double separation = PitchRadius + otherGearData.PitchRadius;
			double phiMax = 1.5 * Math.Acos((otherGearData.AddendumRadius * otherGearData.AddendumRadius + separation * separation - PitchRadius * PitchRadius) / (2 * otherGearData.AddendumRadius * separation));

			double phiOffset = otherGearData.PitchAngle - otherGearData.TopStartAngle;

			for (int i = -pointCount; i <= pointCount; i++) {
				double t = (double) i / pointCount;
				double phi = t * phiMax;
				Point point = Point.Create(separation - otherGearData.AddendumRadius * Math.Cos(phi), otherGearData.AddendumRadius * Math.Sin(phi), 0);

				point = Matrix.CreateRotation(axis, (-phi + phiOffset) * otherGearData.ApparentNumberOfTeeth / ApparentNumberOfTeeth) * point;
				double angle = Math.Atan2(point.Y, point.X);
				points.Add(Matrix.CreateRotation(axis, angle * (CircumferentialScale - 1)) * point);
			}

			return NurbsCurve.CreateThroughPoints(false, points, accuracy);
		}

		// Source: Shigley and Mischke, Mechanical Engineering Design, 5th Edition, pp 529-526
		public double CircumferentialScale { get { return 1 / Math.Cos(ScrewAngle); } }
		public double ApparentNumberOfTeeth { get { return (double) NumberOfTeeth * CircumferentialScale; } }

		public double Pitch { get { return Math.PI * Module; } }
		public double PitchAngle { get { return Math.PI / NumberOfTeeth; } }

		public double PitchDiameter { get { return Module * NumberOfTeeth * CircumferentialScale; } }
		public double BaseDiameter { get { return PitchDiameter * Math.Cos(PressureAngle); } }
		public double AddendumDiameter { get { return PitchDiameter + 2 * Module; } }
		public double DedendumDiameter { get { return PitchDiameter - 2 * (1 + DedendumClearance) * Module; } }

		public double PitchRadius { get { return PitchDiameter / 2; } }
		public double BaseRadius { get { return BaseDiameter / 2; } }
		public double AddendumRadius { get { return AddendumDiameter / 2; } }
		public double DedendumRadius { get { return DedendumDiameter / 2; } }

		public Circle PitchCircle { get { return Circle.Create(Frame.World, PitchRadius); } }
		public Circle BaseCircle { get { return Circle.Create(Frame.World, BaseRadius); } }
		public Circle AddendumCircle { get { return Circle.Create(Frame.World, AddendumRadius); } }
		public Circle DedendumCircle { get { return Circle.Create(Frame.World, DedendumRadius); } }

		public bool IsSmallDedendum { get { return DedendumDiameter < BaseDiameter; } } // See Shigley on drawing gears.  Basically in this case we're going to add some extra faces inside the start of the involute.

		public double GammaMax { get { return Math.Sqrt(AddendumRadius * AddendumRadius - BaseRadius * BaseRadius); } }
		public double Theta { get { return (GammaMax / BaseRadius - Math.Atan(GammaMax / BaseRadius)) * CircumferentialScale; } }

		public Matrix ToothTrans { get { return Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), PitchAngle * 2); } }
		public Matrix HalfToothTrans { get { return Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), PitchAngle); } }

		public double OffsetAngle {  // Figure out how to cache this properly
			get {
				if (!offsetAngle.HasValue) {
					// Calculate where the involute intersects the pitch circle and rotate the involute so that the tooth thickness and the width thickness are the same
					CurveSegment basicInvolute = CurveSegment.Create(CreateInvolute());
					CurveSegment pitchCircleSegment = CurveSegment.Create(PitchCircle);

					ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersections = pitchCircleSegment.IntersectCurve(basicInvolute);
					Debug.Assert(intersections.Count == 1);
					offsetAngle = PitchAngle / 2 - intersections.ToArray()[0].EvaluationA.Param;
				}

				return offsetAngle.Value;
			}
		}

		public double TopStartAngle { get { return (Theta + OffsetAngle); } }
		public double TopEndAngle { get { return PitchAngle * 2 - TopStartAngle; } }

		public Line Axis { get { return axis; } }
	}
}
