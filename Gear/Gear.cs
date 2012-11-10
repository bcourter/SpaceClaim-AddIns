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
	public abstract class Gear {
		protected double internalGearODScale = 1.25;

		protected Gear(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double depth) {
			Parent = parent;
			GearData = gearData;
			ConjugateGearData = conjugateGearData;
			ToothProfile = toothProfile;
			HelicalAngle = helicalAngle;
			Depth = depth;

			Period = ToothProfile.GetProfile();
			Window activeWindow = Window.ActiveWindow;
			string name = String.Format(Resources.GearPartNameFormat, GearData.Pitch * activeWindow.Units.Length.ConversionFactor, activeWindow.Units.Length.Symbol, GearData.NumberOfTeeth, GearData.PressureAngle * 180 / Math.PI);
			Part = Part.Create(Parent.Master.Document, name);
			Component = Component.Create(parent.Master, Part);
			Component.Transform(parent.TransformToMaster);

			GearLayer = NoteHelper.CreateOrGetLayer(Part.Document, Resources.GearLayerName, System.Drawing.Color.LightSteelBlue);
			PitchCircleLayer = NoteHelper.CreateOrGetLayer(Part.Document, Resources.PitchCircleLayerName, System.Drawing.Color.SteelBlue);
			PitchCircleLayer.SetVisible(null, false);
			VisualizationLayer = NoteHelper.CreateOrGetLayer(Part.Document, Resources.VisualizationBodyLayerName, System.Drawing.Color.SteelBlue);

            AlignmentPart = Part.Create(Part.Document, Resources.AlignmentPlanePartName);
			AlignmentComponent = Component.Create(Part, AlignmentPart);
			AlignmentDesBodies = new List<DesignBody>();
			AlignmentLayer = NoteHelper.CreateOrGetLayer(Part.Document, Resources.AlignmentPlaneLayerName, System.Drawing.Color.AliceBlue);
			AlignmentLayer.SetVisible(null, false);
		}

		protected static void BuildGear(Gear gear) {
			Body gearBody = gear.CreateGearBody();
			Body pitchCircleBody = gear.CreatePitchCircleBody();
			Body addendumBody, dedendumBody;
			gear.CreateAlignmentBodies(out addendumBody, out dedendumBody);

			gear.CreateDesignBodies(gearBody, pitchCircleBody, addendumBody, dedendumBody);
		}

		private Body CreateGearBody() {
			int numSteps = (int) Math.Max(Math.Ceiling(Math.Abs(64 * HelixRotations / 2 / Math.PI) + 1), 2);

			if (this is BevelGear/*|| this is HypoidGear*/)
				numSteps *= 4;

			int extraSteps = 2;

			if (Accuracy.AngleIsZero(TotalTwistAngle)) {
				numSteps = 2;
				extraSteps = 0;//0
			}

			double accuracy = Accuracy.LinearResolution;
			//	Period.Print();
			var orderedCurveChain = new TrimmedCurveChain(Period);
			orderedCurveChain.Reverse();
			IList<ITrimmedCurve> orderedCurves = orderedCurveChain.SortedCurves;

			var periodBodies = new List<Body>();
			foreach (ITrimmedCurve iTrimmedCurve in orderedCurves) {
				var profiles = new List<ICollection<ITrimmedCurve>>();
				for (int i = -extraSteps; i < numSteps + extraSteps; i++) {
					ITrimmedCurve transformedCurve = GetTransformedProfileCurve(iTrimmedCurve, (double) i / (numSteps - 1));
					var profile = new List<ITrimmedCurve>();
					profile.Add(transformedCurve);
					profiles.Add(profile);
				}

				try {
					periodBodies.Add(Body.LoftProfiles(profiles, false, false));
				}
				catch {
					foreach (ICollection<ITrimmedCurve> curves in profiles) {
						foreach (ITrimmedCurve curve in curves)
							DesignCurve.Create(Part, curve);
					}
				}
			}

			Body periodBody = periodBodies.TryUnionOrFailBodies();

			var profileBodies = new List<Body>();
			var startProfile = new List<ITrimmedCurve>();
			var endProfile = new List<ITrimmedCurve>();
			Matrix trans;
			for (int i = 0; i < GearData.NumberOfTeeth; i++) {
				trans = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), GearData.PitchAngle * 2 * i);
				profileBodies.Add(periodBody.CreateTransformedCopy(trans));
			}

			Body gearBody = profileBodies.TryUnionAndStitchOrFailBodies();
			Body cappingBody = GetCappingBody();
			try {
				//	throw new NotImplementedException();
				if (GearData.IsInternal) {
					//	gearBody.Subtract(new Body[] { cappingBody });
					cappingBody.Subtract(new Body[] { gearBody });
					gearBody = cappingBody;
				}
				else
					gearBody.Intersect(new Body[] { cappingBody });
			}
			catch {
				DesignBody.Create(Part, "capping", cappingBody.Copy());
				DesignBody.Create(Part, "gear", gearBody.Copy());
			}

			return gearBody;
		}

		protected abstract ITrimmedCurve GetTransformedProfileCurve(ITrimmedCurve iTrimmedCurve, double param);
		protected abstract Body GetCappingBody();

		protected abstract Body CreatePitchCircleBody();
		protected abstract void CreateAlignmentBodies(out Body addendumBody, out Body dedendumBody);
		protected virtual ICollection<Body> CreateVisualizationBodies() {
			return new Body[] { CreatePitchCircleBody() };
		}

		protected void CreateDesignBodies(Body gearBody, Body pitchCircleBody, Body addendumBody, Body dedendumBody) {
			Debug.Assert(gearBody != null);
			Debug.Assert(pitchCircleBody != null);
			Debug.Assert(addendumBody != null);
			Debug.Assert(dedendumBody != null);

			// Fillets
			var roundEdges = new List<KeyValuePair<Edge, EdgeRound>>();
			FixedRadiusRound round = new FixedRadiusRound(GearData.Module * GearData.DedendumClearance);
			foreach (Edge edge in gearBody.Edges) {
				if (edge.Faces.Count == 2 && Accuracy.AngleIsNegative(edge.GetAngle()))
					roundEdges.Add(new KeyValuePair<Edge, EdgeRound>(edge, round));
			}
			//		if (!GearData.IsSmallDedendum)////////tdb
			gearBody.RoundEdges(roundEdges);

			GearDesBody = DesignBody.Create(Part, Resources.GearNameSimple, gearBody);
			GearDesBody.Layer = GearLayer;

			PitchCircleDesBody = DesignBody.Create(Part, Resources.PitchCircleSurfaceName, pitchCircleBody);
			PitchCircleDesBody.Layer = PitchCircleLayer;

			var alignmentPlanes = new List<Body>();
			Matrix trans;
			for (int i = 0; i < GearData.NumberOfTeeth; i++) {
				trans = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), GearData.PitchAngle * 2 * i);
				alignmentPlanes.Add(addendumBody.CreateTransformedCopy(trans));
				alignmentPlanes.Add(dedendumBody.CreateTransformedCopy(trans));
			}

			// Alignment planes
			foreach (Body alignmentBody in alignmentPlanes) {
				DesignBody desBody = DesignBody.Create(AlignmentPart, Resources.AlignmentPlaneBodyName, alignmentBody);
				desBody.Layer = AlignmentLayer;
				AlignmentDesBodies.Add(desBody);
			}

			foreach (Body visualizationBody in CreateVisualizationBodies()) {
				DesignBody desBody = DesignBody.Create(Part, Resources.VisualizationBodyName, visualizationBody);
				desBody.Layer = VisualizationLayer;
			}
		}

		public IPart Parent { get; set; }
		public GearData GearData { get; set; }
		public GearData ConjugateGearData { get; set; }
		public ToothProfile ToothProfile { get; set; }
		public double HelicalAngle { get; set; }
		public double Depth { get; set; }

		public ICollection<ITrimmedCurve> Period { get; set; }
		public Part Part { get; set; }
		public Component Component { get; set; }

		public virtual double TotalTwistAngle { get { return HelicalAngle; } }
		public double HelixRotations { get { return Math.Tan(TotalTwistAngle) * Depth / GearData.PitchRadius; } }

		public DesignBody GearDesBody { get; set; }
		public Layer GearLayer { get; set; }
		public DesignBody PitchCircleDesBody { get; set; }
		public IDesignFace PitchSurfaceFace { get; set; }

		public Layer PitchCircleLayer { get; set; }

		public Part AlignmentPart { get; set; }
		public List<DesignBody> AlignmentDesBodies { get; set; }
		public Component AlignmentComponent { get; set; }
		public Layer AlignmentLayer { get; set; }
		public Layer VisualizationLayer { get; set; }

		public Line Axis { get { return Line.Create(Point.Origin, Direction.DirZ); } }

		protected abstract Matrix TransformToTangentExternal { get; }
		//	protected abstract Matrix TransformToTangentInternal { get; }

		private Matrix TransformToTangentInternal {
			get {
				return
					Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), Math.PI) *
					TransformToTangentExternal
				;
			}
		}

		public Matrix TransformToTangent {
			get {
				if (GearData.IsInternal)
					return TransformToTangentInternal;

				return TransformToTangentExternal;
			}
		}
	}

	public class StraightGear : Gear {
		protected StraightGear(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double screwAngle, double depth)
			: base(parent, gearData, conjugateGearData, toothProfile, helicalAngle, depth) {

			StartPoint = Point.Origin + Vector.Create(0, 0, Depth / 2); ;
			EndPoint = StartPoint + Vector.Create(0, 0, -Depth);
			StartFrame = Frame.Create(StartPoint, Direction.DirX, Direction.DirY);
			EndFrame = Frame.Create(EndPoint, StartFrame.DirX, -StartFrame.DirY);
			StartPlane = Plane.Create(StartFrame);
			EndPlane = Plane.Create(EndFrame);

			ScrewAngle = screwAngle;
		}

		public static Gear Create(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double screwAngle, double depth) {
			Gear gear = new StraightGear(parent, gearData, conjugateGearData, toothProfile, helicalAngle, screwAngle, depth);
			BuildGear(gear);
			return gear;
		}

		protected override Body GetCappingBody() {
			return Body.ExtrudeProfile(StartPlane, new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(StartPlane.Frame, GearData.AddendumRadius * internalGearODScale)) }, -Depth);
		}

		protected override ITrimmedCurve GetTransformedProfileCurve(ITrimmedCurve iTrimmedCurve, double param) {
			Matrix trans =
						Matrix.CreateTranslation(Vector.Create(0, 0, -Depth * param + Depth / 2)) *
						Matrix.CreateRotation(Axis, HelixRotations * (param - 0.5));

			return iTrimmedCurve.CreateTransformedCopy(trans);
		}

		protected override Body CreatePitchCircleBody() {
			return ShapeHelper.CreateCylindricalSurface(StartPlane, GearData.PitchRadius / GearData.CircumferentialScale, -Depth);
		}

		protected override ICollection<Body> CreateVisualizationBodies() {
			return new Body[] { ShapeHelper.CreateCylindricalSurface(StartPlane, GearData.PitchRadius, -Depth) };
		}

		protected override void CreateAlignmentBodies(out Body addendumBody, out Body dedendumBody) {
			addendumBody = ShapeHelper.CreatePolygon(new Point[] {
				Point.Create(GearData.AddendumRadius, 0, 0), 
				Point.Create(GearData.PitchRadius, 0, 0),
				Point.Create(GearData.PitchRadius, 0, -Depth),
				Point.Create(GearData.AddendumRadius, 0, -Depth)
			}, 0);

			addendumBody.Transform(GearData.HalfToothTrans);

			dedendumBody = ShapeHelper.CreatePolygon(new Point[] {
				Point.Create(GearData.BaseRadius, 0, 0), 
				Point.Create(GearData.PitchRadius, 0, 0),
				Point.Create(GearData.PitchRadius, 0, -Depth),
				Point.Create(GearData.BaseRadius, 0, -Depth)
			}, 0);
		}

		protected override Matrix TransformToTangentExternal {
			get {
				return
					Matrix.CreateTranslation(Vector.Create(GearData.PitchRadius, 0, 0)) *
					Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirX), ScrewAngle * (GearData.IsInternal ? -1 : 1))
				;
			}
		}

		public double ScrewAngle { get; set; }
		public override double TotalTwistAngle { get { return (HelicalAngle + ScrewAngle) * (GearData.IsInternal ? -1 : 1); } }

		Point StartPoint { get; set; }
		Point EndPoint { get; set; }
		Frame StartFrame { get; set; }
		Frame EndFrame { get; set; }
		public Plane StartPlane { get; set; }
		public Plane EndPlane { get; set; }
	}

	public class BevelGear : Gear {
		protected BevelGear(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double bevelAngle, double bevelKneeRatio, double depth)
			: base(parent, gearData, conjugateGearData, toothProfile, helicalAngle, depth) {

			BevelAngle = bevelAngle;
			BevelKneeRatio = bevelKneeRatio;

			StartPoint = Point.Create(0, 0, XOffset);
			EndPoint = StartPoint - Vector.Create(0, 0, Depth * Math.Cos(Alpha));
			StartFrame = Frame.Create(StartPoint, Direction.DirX, -Direction.DirY);
			EndFrame = Frame.Create(EndPoint, Direction.DirX, -Direction.DirY);
			StartDiameter = GearData.PitchDiameter;
			EndDiameter = StartDiameter - 2 * Depth * Math.Sin(Alpha);
			StartCone = Cone.Create(StartFrame, StartDiameter / 2, Math.PI / 2 - Alpha);
			EndCone = Cone.Create(EndFrame, EndDiameter / 2, Math.PI / 2 - Alpha);
		}

		public static Gear Create(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double bevelAngle, double bevelKneeRatio, double depth) {
			Gear gear = new BevelGear(parent, gearData, conjugateGearData, toothProfile, helicalAngle, bevelAngle, bevelKneeRatio, depth);
			BuildGear(gear);
			return gear;
		}

		protected override ITrimmedCurve GetTransformedProfileCurve(ITrimmedCurve iTrimmedCurve, double param) {
			int numPoints = 10000;

			Matrix trans = Matrix.CreateRotation(Axis, HelixRotations * param);

			Point[] points = new Point[numPoints + 1];
			Frame profileFrame = Frame.Create(StartPoint + (EndPoint - StartPoint) * param, StartFrame.DirX, StartFrame.DirY);
			Cone profileCone = Cone.Create(profileFrame, (StartDiameter + (EndDiameter - StartDiameter) * param) / 2, Math.PI / 2 - Alpha);

			for (int j = 0; j <= numPoints; j++) {
				double t = iTrimmedCurve.Bounds.Start + iTrimmedCurve.Bounds.Span * (double) j / numPoints;
				Vector pointVector = iTrimmedCurve.Geometry.Evaluate(t).Point.Vector;
				double angle = Circle.Create(Frame.Create(Point.Origin, Direction.DirX, -Direction.DirY), GearData.PitchRadius).ProjectPoint(pointVector.GetPoint()).Param;
				double startDistance = pointVector.Magnitude - StartDiameter / 2;
				double endDistance = pointVector.Magnitude * EndScale - EndDiameter / 2;
				double distance = startDistance + (endDistance - startDistance) * param;
				points[j] = profileCone.WrapPoint(angle, distance);
			}

			return CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, points, Accuracy.LinearResolution)).CreateTransformedCopy(trans);
		}

		protected override Body GetCappingBody() {
			Point startOuter = StartCone.WrapPoint(0, GearData.AddendumRadius * internalGearODScale);
			Point endOuter = EndCone.WrapPoint(0, GearData.AddendumRadius * internalGearODScale);
			Point startKnee = StartCone.WrapPoint(0, -(1 + BevelKneeRatio) * GearData.Module);
			Point endKnee = EndCone.WrapPoint(0, -(1 + BevelKneeRatio) * GearData.Module);
			Point startCenter = Axis.ProjectPoint(startKnee).Point;
			Point endCenter = Axis.ProjectPoint(endKnee).Point;

			IList<ITrimmedCurve> cappingProfile = new Point[] { startCenter, startKnee, startOuter, endOuter, endKnee, endCenter }.AsPolygon();
			IList<ITrimmedCurve> revolvePath = new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(StartFrame, 1)) };

			Body cappingBody = null;
			try {
				cappingBody = Body.SweepProfile(Plane.PlaneZX, cappingProfile, revolvePath);
			}
			catch {
				foreach (ITrimmedCurve curve in cappingProfile)
					DesignCurve.Create(Part, curve);
			}

			return cappingBody;
		}

		protected override Body CreatePitchCircleBody() {
			return ShapeHelper.CreateCone(StartCone.Frame.Origin, EndCone.Frame.Origin, StartCone.Radius * 2, EndCone.Radius * 2, true);
		}

		protected override void CreateAlignmentBodies(out Body addendumBody, out Body dedendumBody) {
			addendumBody = ShapeHelper.CreatePolygon(new Point[] {
			    StartCone.WrapPoint(GearData.PitchAngle, GearData.AddendumRadius - GearData.PitchRadius), 
			    StartCone.WrapPoint(GearData.PitchAngle, 0), 
			    EndCone.WrapPoint(GearData.PitchAngle, 0), 
			    EndCone.WrapPoint(GearData.PitchAngle, EndScale * (GearData.AddendumRadius - GearData.PitchRadius))
			}, 0);

			dedendumBody = ShapeHelper.CreatePolygon(new Point[] {
			    StartCone.WrapPoint(0, GearData.BaseRadius - GearData.PitchRadius), 
			    StartCone.WrapPoint(0, 0), 
			    EndCone.WrapPoint(0, 0), 
			    EndCone.WrapPoint(0, EndScale * (GearData.BaseRadius - GearData.PitchRadius))
			}, 0);
		}

		double BevelAngle { get; set; }
		double BevelKneeRatio { get; set; }

		Point StartPoint { get; set; }
		Point EndPoint { get; set; }
		Frame StartFrame { get; set; }
		Frame EndFrame { get; set; }
		double StartDiameter { get; set; }
		double EndDiameter { get; set; }
		Cone StartCone { get; set; }
		Cone EndCone { get; set; }

		double Chord {
			get { return Math.Sqrt(GearData.PitchRadius * GearData.PitchRadius + ConjugateGearData.PitchRadius * ConjugateGearData.PitchRadius - 2 * GearData.PitchRadius * ConjugateGearData.PitchRadius * Math.Cos(Math.PI - BevelAngle)); }
		}

		double Gamma {
			get { return Math.Acos((-GearData.PitchRadius * GearData.PitchRadius + Chord * Chord + ConjugateGearData.PitchRadius * ConjugateGearData.PitchRadius) / (2 * Chord * ConjugateGearData.PitchRadius)); }
		}

		public double XOffset {
			get { return Math.Sin(Math.PI / 2 - Gamma) * Chord / Math.Sin(BevelAngle); }
		}

		public double Alpha {
			get { return Math.Atan(GearData.PitchRadius / XOffset); }
		}

		public double EndScale {
			get { return EndCone.Radius / StartCone.Radius; }
		}

		protected override Matrix TransformToTangentExternal {
			get {
				double angle = Math.Atan(GearData.PitchRadius / XOffset);
				return
					Matrix.CreateTranslation(Vector.Create(0, 0, -XOffset / Math.Cos(Alpha))) *
					Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), angle)
				;
			}
		}

		//protected override Matrix TransformToTangentInternal {
		//    get {
		//        double angle = Math.Atan(GearData.PitchRadius / XOffset);
		//        return
		//            Matrix.CreateTranslation(-Vector.Create(0, 0, -XOffset / Math.Cos(Alpha))) *
		//            Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), angle)
		//        ;
		//    }
		//}
	}

	public class HypoidGear : Gear {
		protected HypoidGear(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double hypoidAngle, double hypoidOffset, double hypoidKneeRatio, double depth)
			: base(parent, gearData, conjugateGearData, toothProfile, helicalAngle, depth) {

			HypoidAngle = hypoidAngle;
			HypoidOffset = hypoidOffset;
			HypoidKneeRatio = hypoidKneeRatio;

			TangentLine = Line.Create(Point.Origin, Direction.DirZ).CreateTransformedCopy(TransformToTangent.Inverse);

			Debug.Assert(Accuracy.LengthIsZero(TangentLine.Origin.Y));
			A = TangentLine.Origin.Vector.Magnitude;
			Point pointA = Point.Create(A, 0, 0);

			Point p0 = TangentLine.Origin;
			Point p1 = TangentLine.Evaluate(1).Point;
			p0 = Plane.PlaneYZ.ProjectPoint(p0).Point;
			p1 = Plane.PlaneYZ.ProjectPoint(p1).Point;
			B = A * (p1.Z - p0.Z) / (p1.Y - p0.Y);

			Depth = TangentLine.Evaluate(Depth).Point.Z;
			HypoidOffset = TangentLine.Evaluate(HypoidOffset).Point.Z;

			StartZ = HypoidOffset;
			EndZ = HypoidOffset + Depth;
			StartPoint = Point.Create(0, 0, StartZ);
			EndPoint = Point.Create(0, 0, EndZ);
			StartFrame = Frame.Create(StartPoint, Direction.DirX, -Direction.DirY);
			EndFrame = Frame.Create(EndPoint, Direction.DirX, -Direction.DirY);
			StartDiameter = GetHyperbolicRadius(StartZ) * 2;
			EndDiameter = GetHyperbolicRadius(EndZ) * 2;
			StartCone = GetConeAtParameter(StartZ);
			EndCone = GetConeAtParameter(EndZ);

			StartPlane = Plane.Create(StartFrame);
			EndPlane = Plane.Create(EndFrame);


		}

		public static Gear Create(IPart parent, GearData gearData, GearData conjugateGearData, ToothProfile toothProfile, double helicalAngle, double hypoidAngle, double hypoidOffset, double hypoidKneeRatio, double depth) {
			Gear gear = new HypoidGear(parent, gearData, conjugateGearData, toothProfile, helicalAngle, hypoidAngle, hypoidOffset, hypoidKneeRatio, depth);
			BuildGear(gear);
			return gear;
		}

		protected override ITrimmedCurve GetTransformedProfileCurve(ITrimmedCurve iTrimmedCurve, double param) {
			int numPoints = 10000;
			Point[] points = new Point[numPoints + 1];

			double paramZ = StartZ + (EndZ - StartZ) * param;
			Frame profileFrame = Frame.Create(StartPoint + (EndPoint - StartPoint) * param, StartFrame.DirX, StartFrame.DirY);
			Cone profileCone = GetConeAtParameter(paramZ);
			double radius = profileCone.Radius;
			ICollection<IntPoint<SurfaceEvaluation, CurveEvaluation>> intersections = profileCone.IntersectCurve(TangentLine);
			Matrix trans = Matrix.CreateRotation(Axis, intersections.OrderBy(i => Math.Abs(i.Point.Z)).First().EvaluationA.Param.U);
			//		profileCone.Print(Part);

			//		double scale = (radius / A) / Math.Sin(profileCone.HalfAngle);

			for (int j = 0; j <= numPoints; j++) {
				double t = iTrimmedCurve.Bounds.Start + iTrimmedCurve.Bounds.Span * (double) j / numPoints;
				Vector pointVector = iTrimmedCurve.Geometry.Evaluate(t).Point.Vector;
				double angle = -Circle.Create(Frame.Create(Point.Origin, Direction.DirX, Direction.DirY), GearData.PitchRadius).ProjectPoint(pointVector.GetPoint()).Param;
				double distance = (pointVector.Magnitude - A) * radius / A;// *Math.Sin(profileCone.HalfAngle);

				//		points[j] = profileCone.WrapPoint(angle, (distance - A) / Math.Sin(profileCone.HalfAngle));
				points[j] = profileCone.WrapPoint(angle, distance);
			}
			try {
				return CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, points, Accuracy.LinearResolution)).CreateTransformedCopy(trans);
			}
			catch {
				points.Print();
			}

			return null;
		}

		private static double GetHyperbolicRadius(double t, double a, double b) {
			return a * Math.Sqrt(1 + Math.Pow(t / b, 2));
		}

		private double GetHyperbolicRadius(double t) {
			return GetHyperbolicRadius(t, A, B);
		}

		private Cone GetConeAtParameter(double t) {
			Point point = Point.Create(GetHyperbolicRadius(t), 0, t);
			double slope = A * t / B / B / Math.Sqrt(1 + Math.Pow(t / B, 2));
			double angle;
			if (Accuracy.LengthIsZero(slope))
				angle = Math.PI / 2;
			else
				angle = Math.Atan(-1 / slope);

			if (Accuracy.AngleIsNegative(angle))
				angle += Math.PI;

			Debug.Assert(angle < Math.PI && angle > 0, "Cone angle improper.");

			Frame frame = Frame.Create(Point.Create(0, 0, t), Direction.DirX, Direction.DirY);
			return Cone.Create(frame, GetHyperbolicRadius(t), angle);
		}

		protected override Body GetCappingBody() {
			Point startOuter = StartCone.WrapPoint(0, GearData.AddendumRadius * internalGearODScale);
			Point endOuter = EndCone.WrapPoint(0, GearData.AddendumRadius * internalGearODScale);
			Point startKnee = StartCone.WrapPoint(0, -(1 + HypoidKneeRatio) * GearData.Module * StartCone.Radius / A);
			Point endKnee = EndCone.WrapPoint(0, -(1 + HypoidKneeRatio) * GearData.Module * EndCone.Radius / A);
			Point startCenter = Axis.ProjectPoint(startKnee).Point;
			Point endCenter = Axis.ProjectPoint(endKnee).Point;

			CurveSegment startLine = CurveSegment.Create(startKnee, startOuter);
			CurveSegment endLine = CurveSegment.Create(endKnee, endOuter);
			ICollection<IntPoint<CurveEvaluation, CurveEvaluation>> intersections = startLine.IntersectCurve(endLine);

			IList<ITrimmedCurve> cappingProfile;
			if (intersections.Count > 0) {
				Point middlePoint = intersections.First().Point;
				cappingProfile = new Point[] { startCenter, startKnee, middlePoint, endKnee, endCenter }.AsPolygon();
			}
			else
				cappingProfile = new Point[] { startCenter, startKnee, startOuter, endOuter, endKnee, endCenter }.AsPolygon();

			IList<ITrimmedCurve> revolvePath = new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(StartFrame, 1)) };

			Body cappingBody = null;
			try {
				cappingBody = Body.SweepProfile(Plane.PlaneZX, cappingProfile, revolvePath);
			}
			catch {
				foreach (ITrimmedCurve curve in cappingProfile)
					DesignCurve.Create(Part, curve);
			}

			return cappingBody;
		}

		protected override Body CreatePitchCircleBody() {
			Body pitchCircleBody = Body.ExtrudeProfile(StartPlane, new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(StartPlane.Frame, GearData.PitchDiameter / 2)) }, -Depth);
			foreach (Face face in pitchCircleBody.Faces) {
				if (face.Geometry is Plane)
					pitchCircleBody.DeleteFaces(new Face[] { face }, RepairAction.None);
			}

			return pitchCircleBody;
		}

		protected override ICollection<Body> CreateVisualizationBodies() {
			var points = new List<Point>();
			for (double z = StartZ; z <= EndZ; z += (EndZ - StartZ) / 10000)
				//			for (double z =0; z <= 0.05; z += 0.001)
				points.Add(Point.Create(GetHyperbolicRadius(z), 0, z));

			CurveSegment curve = CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, points, Accuracy.LinearResolution));
			return new Body[] { ShapeHelper.CreateRevolvedCurve(Axis, curve) };
		}

		protected override void CreateAlignmentBodies(out Body addendumBody, out Body dedendumBody) {
			addendumBody = ShapeHelper.CreatePolygon(new Point[] {
			    StartCone.WrapPoint(GearData.PitchAngle, GearData.AddendumRadius - GearData.PitchRadius), 
			    StartCone.WrapPoint(GearData.PitchAngle, 0), 
			    EndCone.WrapPoint(GearData.PitchAngle, 0), 
			    EndCone.WrapPoint(GearData.PitchAngle, EndScale * (GearData.AddendumRadius - GearData.PitchRadius))
			}, 0);

			dedendumBody = ShapeHelper.CreatePolygon(new Point[] {
			    StartCone.WrapPoint(0, GearData.BaseRadius - GearData.PitchRadius), 
			    StartCone.WrapPoint(0, 0), 
			    EndCone.WrapPoint(0, 0), 
			    EndCone.WrapPoint(0, EndScale * (GearData.BaseRadius - GearData.PitchRadius))
			}, 0);
		}

		double HypoidAngle { get; set; }
		double HypoidOffset { get; set; }
		double HypoidKneeRatio { get; set; }

		Line TangentLine { get; set; }

		double StartZ { get; set; }
		double EndZ { get; set; }
		Point StartPoint { get; set; }
		Point EndPoint { get; set; }
		Frame StartFrame { get; set; }
		Frame EndFrame { get; set; }
		double StartDiameter { get; set; }
		double EndDiameter { get; set; }
		Cone StartCone { get; set; }
		Cone EndCone { get; set; }
		public Plane StartPlane { get; set; }
		public Plane EndPlane { get; set; }

		double A { get; set; }
		double B { get; set; }

		double Chord {
			get { return Math.Sqrt(GearData.PitchRadius * GearData.PitchRadius + ConjugateGearData.PitchRadius * ConjugateGearData.PitchRadius - 2 * GearData.PitchRadius * ConjugateGearData.PitchRadius * Math.Cos(Math.PI - HypoidAngle)); }
		}

		double Gamma {
			get { return Math.Acos((-GearData.PitchRadius * GearData.PitchRadius + Chord * Chord + ConjugateGearData.PitchRadius * ConjugateGearData.PitchRadius) / (2 * Chord * ConjugateGearData.PitchRadius)); }
		}

		public double Alpha {
			get { return Math.Atan(GearData.PitchRadius / HypoidAngle); }
		}

		public double EndScale {
			get { return EndCone.Radius / StartCone.Radius; }
		}

		public override double TotalTwistAngle { get { return HelicalAngle + HypoidAngle; } }

		protected override Matrix TransformToTangentExternal {
			get {
				return
					Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirX), HypoidAngle) *
					Matrix.CreateTranslation(Vector.Create(GearData.PitchRadius, 0, 0))
				;
			}
		}


	}
}

//namespace SpaceClaim.Api.V10.Geometry {
//    /// <summary>
//    /// x^2/a^2 + y^2/a^2 - z^2/c^2 = 1
//    /// </summary>
//    public class HyperboloidOneSurface {// : Surface, IHasFrame, IHasAxis {
//        Frame Frame { get; set; }
//        double A { get; set; }
//        double C { get; set; }
//        public static HyperboloidOneSurface Create(Frame frame, double radius, double halfAngle);

//        HyperboloidOneSurface(Frame frame, double a, double c) {
//            Frame = frame;
//            A = a;
//            C = c;
//        }

//        public static HyperboloidOneSurface operator *(Matrix trans, Cone cone) {
//            return this.CreateTransformedCopy(trans);
//        }

//        public Line Axis { get; }
//        //
//        public Frame Frame { get; }
//        //
//        // Summary:
//        //     The half angle of the cone.
//        //
//        // Remarks:
//        //     The cone increases in radius in the direction of SpaceClaim.Api.V10.Geometry.Frame.DirZ.
//        public double HalfAngle { get; }
//        //
//        // Summary:
//        //     The radius of the cone in the XY plane of its frame.
//        public double Radius { get; }

//        public HyperboloidOneSurface CreateTransformedCopy(Matrix trans);
//        public override double GetLength(PointUV paramA, PointUV paramB) {
//            throw new NotImplementedException();
//        }

//        public override bool TryOffsetParam(PointUV start, DirectionUV dir, double distance, out PointUV result);
//    }
//}