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
	class GearGroupCapsule : RibbonGroupCapsule {
		public GearGroupCapsule(string name, RibbonTabCapsule parent)
			: base(name, Resources.CreateGroupText, parent, LayoutOrientation.horizontal) {

			Values[Resources.NumberOfTeethLText] = new RibbonCommandValue(Settings.Default.NumberOfTeethL);
			Values[Resources.NumberOfTeethRText] = new RibbonCommandValue(Settings.Default.NumberOfTeethR);
			Values[Resources.PressureAngleText] = new RibbonCommandValue(Settings.Default.PressureAngleDegrees);
			Values[Resources.ModuleText] = new RibbonCommandValue(Settings.Default.Module * Window.ActiveWindow.Units.Length.ConversionFactor);
			Values[Resources.DedendumClearanceText] = new RibbonCommandValue(Settings.Default.DedendumClearance);
			Values[Resources.DepthText] = new RibbonCommandValue(Settings.Default.Depth * Window.ActiveWindow.Units.Length.ConversionFactor);

			Booleans[Resources.UseTrochoidalText] = new RibbonCommandBoolean(Settings.Default.UseTrochoidalInterferenceRemoval);
			Booleans[Resources.AddDedendumClearance] = new RibbonCommandBoolean(Settings.Default.AddDedendumClearace);
		}
	}

	class GearButtonCapsule : RibbonButtonCapsule {
		public GearButtonCapsule(string name, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, Resources.CreateGearCommandText, Resources.Gear, Resources.CreateGearCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			double lengthConversion = ActiveWindow.Units.Length.ConversionFactor;

			int numberOfTeethL = (int) Values[Resources.NumberOfTeethLText].Value;
			int numberOfTeethR = (int) Values[Resources.NumberOfTeethRText].Value;
			bool isInternalL = numberOfTeethL < 0;
			bool isInternalR = numberOfTeethR < 0;
			numberOfTeethL = Math.Abs(numberOfTeethL);
			numberOfTeethR = Math.Abs(numberOfTeethR);

			double pressureAngle = Values[Resources.PressureAngleText].Value * Math.PI / 180;
			double module = Values[Resources.ModuleText].Value / lengthConversion;
			double dedendumClearance = Values[Resources.DedendumClearanceText].Value;
			double depth = Values[Resources.DepthText].Value / lengthConversion;

			bool useTrochoidalInterferenceRemoval = Booleans[Resources.UseTrochoidalText].Value;
			bool addDedendumClearance = Booleans[Resources.AddDedendumClearance].Value;
			if (!addDedendumClearance)
				dedendumClearance = 0;

			bool isBevel = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsBevelText].IsEnabledCommandBoolean.Value;
			double bevelAngle = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsBevelText].Values[Resources.BevelAngleText].Value * Math.PI / 180;
			double bevelKneeRatio = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsBevelText].Values[Resources.BevelKneeRatioText].Value;

			bool isHelical = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHelicalText].IsEnabledCommandBoolean.Value;
			double helicalAngle = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHelicalText].Values[Resources.HelicalAngleText].Value * Math.PI / 180;
			if (!isHelical)
				helicalAngle = 0;

			bool isScrew = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsScrewText].IsEnabledCommandBoolean.Value;
			double screwAngle = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsScrewText].Values[Resources.ScrewAngleText].Value * Math.PI / 180;
			double screwAngleOffset = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsScrewText].Values[Resources.ScrewAngleBiasText].Value * Math.PI / 180;
			if (!isScrew) {
				screwAngle = 0;
				screwAngleOffset = 0;
			}

			double screwAngleAverage = screwAngle / 2;
			double screwAngleL = screwAngleAverage + screwAngleOffset;
			double screwAngleR = screwAngleAverage - screwAngleOffset;

			bool isHypoid = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHypoidText].IsEnabledCommandBoolean.Value;
			double hypoidAngle = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHypoidText].Values[Resources.HypoidAngleText].Value * Math.PI / 180;
			double hypoidOffset = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHypoidText].Values[Resources.HypoidOffsetText].Value / lengthConversion;
			if (!isHypoid) {
				hypoidAngle = 0;
				hypoidOffset = 0;
			}

			Frame frame = Frame.World;
			//Circle circle = SelectedCircle(ActiveWindow);
			//if (circle != null)
			//    frame = circle.Frame;

			List<ITrimmedCurve> selectedCurves = ActiveWindow.GetAllSelectedITrimmedCurves().ToList();
			if (selectedCurves.Count == 2 && selectedCurves[0].Geometry is Circle && selectedCurves[0].Geometry is Circle) {
				Circle circle0 = (Circle) selectedCurves[0].Geometry;
				Circle circle1 = (Circle) selectedCurves[1].Geometry;
				Separation separation = circle0.Axis.GetClosestSeparation(circle1.Axis);

				if (Accuracy.LengthIsZero(separation.Distance))
					throw new NotImplementedException("Distance between axes is zero; only hypoid implemented.");

				isHypoid = true;
				hypoidAngle = AddInHelper.AngleBetween(circle0.Axis.Direction, circle1.Axis.Direction);
				hypoidOffset = ((circle0.Frame.Origin - separation.PointA).Magnitude - depth / 2) / Math.Cos(hypoidAngle / 2);

				double radiusAApprox = separation.Distance * circle0.Radius / (circle0.Radius + circle1.Radius);
				double radiusBApprox = separation.Distance - radiusAApprox;
				numberOfTeethR = (int) Math.Round((double) numberOfTeethL / radiusAApprox * radiusBApprox);
				module = radiusAApprox * 2 / numberOfTeethL;

				Point midpoint = separation.PointA + (separation.PointA - separation.PointB) * numberOfTeethL / numberOfTeethR;
				Direction sideSide = (circle0.Frame.Origin - circle1.Frame.Origin).Direction;
				frame = Frame.Create(midpoint, Direction.Cross(sideSide, -(midpoint - circle0.GetClosestSeparation(circle1).PointA).Direction), sideSide);
			}

			double hypoidAngleL = Math.Atan(Math.Sin(hypoidAngle) / (Math.Cos(hypoidAngle) + (double) numberOfTeethR / numberOfTeethL));
			double hypoidAngleR = Math.Atan(Math.Sin(hypoidAngle) / (Math.Cos(hypoidAngle) + (double) numberOfTeethL / numberOfTeethR));

			Gear gearL = null;
			Gear gearR = null;

			var gearDataL = new GearData(numberOfTeethL, pressureAngle, module, dedendumClearance, isInternalL, screwAngleL);
			var gearDataR = new GearData(numberOfTeethR, pressureAngle, module, dedendumClearance, isInternalR, screwAngleR);
			ToothProfile toothProfileL = GetGearProfileFromOptions(gearDataL, gearDataR, useTrochoidalInterferenceRemoval, addDedendumClearance);
			ToothProfile toothProfileR = GetGearProfileFromOptions(gearDataR, gearDataL, useTrochoidalInterferenceRemoval, addDedendumClearance);

			if (isBevel) {
				gearL = BevelGear.Create(ActiveIPart, gearDataL, gearDataR, toothProfileL, helicalAngle, bevelAngle, bevelKneeRatio, depth);
				gearR = BevelGear.Create(ActiveIPart, gearDataR, gearDataL, toothProfileR, -helicalAngle, bevelAngle, bevelKneeRatio, depth);
			}
			else if (isHypoid) {
				gearL = HypoidGear.Create(ActiveIPart, gearDataL, gearDataR, toothProfileL, helicalAngle, hypoidAngleL, hypoidOffset, bevelKneeRatio, depth);
				gearR = HypoidGear.Create(ActiveIPart, gearDataR, gearDataL, toothProfileR, -helicalAngle, hypoidAngleR, hypoidOffset, bevelKneeRatio, depth);
			}
			else {
				gearL = StraightGear.Create(ActiveIPart, gearDataL, gearDataR, toothProfileL, helicalAngle, screwAngleL, depth);
				gearR = StraightGear.Create(ActiveIPart, gearDataR, gearDataL, toothProfileR, -helicalAngle, screwAngleR, depth);
			}

			Line zAxis = Line.Create(Point.Origin, Direction.DirZ);
			gearL.Component.Transform(
				Matrix.CreateMapping(frame) *
				Matrix.CreateRotation(zAxis, Math.PI) *
				gearL.TransformToTangent *
				Matrix.CreateRotation(zAxis, gearDataL.PitchAngle * ((double) 1 / 2 + (gearDataL.NumberOfTeeth % 2 == 0 && !isHypoid ? -1 : 0)))
			);

			gearR.Component.Transform(
				Matrix.CreateMapping(frame) *
				gearR.TransformToTangent *
				Matrix.CreateRotation(zAxis, gearDataR.PitchAngle * ((double) 1 / 2 + (gearDataR.NumberOfTeeth % 2 == 0 && !isHypoid ? -1 : 0)))
			);


			//		if (gearDataR.NumberOfTeeth % 2 == 0)
			//			gearR.Component.Transform(Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), gearDataR.PitchAngle));

			//gearR.Component.Transform(gearR.TransformToTangent);

			Part parent = ActiveIPart.Master;
			IDesignFace pitchCircleL = gearL.Component.Content.Bodies.Where(b => b.Master == gearL.PitchCircleDesBody).First().Faces.First();
			IDesignFace pitchCircleR = gearR.Component.Content.Bodies.Where(b => b.Master == gearR.PitchCircleDesBody).First().Faces.First();

            Part gearMountPart = Part.Create(parent.Document, String.Format(Resources.GearMountPartName, gearDataL.NumberOfTeeth, gearDataR.NumberOfTeeth));
			Component gearMountComponent = Component.Create(parent, gearMountPart);
			DesignBody mountBodyL = DesignBody.Create(gearMountPart, string.Format(Resources.MountBodyName, gearDataL.NumberOfTeeth), pitchCircleL.Master.Shape.Body.CreateTransformedCopy(pitchCircleL.TransformToMaster.Inverse));
			DesignBody mountBodyR = DesignBody.Create(gearMountPart, string.Format(Resources.MountBodyName, gearDataR.NumberOfTeeth), pitchCircleR.Master.Shape.Body.CreateTransformedCopy(pitchCircleR.TransformToMaster.Inverse));
			IDesignFace mountCircleL = gearMountComponent.Content.Bodies.Where(b => b.Master == mountBodyL).First().Faces.First();
			IDesignFace mountCircleR = gearMountComponent.Content.Bodies.Where(b => b.Master == mountBodyR).First().Faces.First();

			Layer mountLayer = NoteHelper.CreateOrGetLayer(ActiveDocument, Resources.GearMountAlignmentCircleLayer, System.Drawing.Color.LightGray);
			mountLayer.SetVisible(null, false);
			mountBodyL.Layer = mountLayer;
			mountBodyR.Layer = mountLayer;

			MatingCondition matingCondition;
			matingCondition = AnchorCondition.Create(parent, gearMountComponent);
			matingCondition = AlignCondition.Create(parent, mountCircleL, pitchCircleL);
			matingCondition = AlignCondition.Create(parent, mountCircleR, pitchCircleR);
			//		matingCondition = TangentCondition.Create(parent, pitchCircleL, pitchCircleR);
			GearCondition gearCondition = GearCondition.Create(parent, pitchCircleL, pitchCircleR);
			if (gearDataL.IsInternal ^ gearDataR.IsInternal)
				gearCondition.IsBelt = true;

			ActiveWindow.InteractionMode = InteractionMode.Solid;

			Settings.Default.NumberOfTeethL = numberOfTeethL;
			Settings.Default.NumberOfTeethR = numberOfTeethR;
			Settings.Default.PressureAngleDegrees = pressureAngle * 180 / Math.PI;
			Settings.Default.Module = module;
			Settings.Default.Depth = depth;
			Settings.Default.DedendumClearance = dedendumClearance;

			Settings.Default.UseTrochoidalInterferenceRemoval = useTrochoidalInterferenceRemoval;
			Settings.Default.AddDedendumClearace = addDedendumClearance;

			Settings.Default.IsBevel = isBevel;
			if (isBevel) {
				Settings.Default.BevelAngle = bevelAngle * 180 / Math.PI;
				Settings.Default.BevelKneeRatio = bevelKneeRatio;
			}

			Settings.Default.IsHelical = isHelical;
			Settings.Default.HelicalAngle = helicalAngle * 180 / Math.PI;

			Settings.Default.IsScrew = isScrew;
			Settings.Default.ScrewAngle = screwAngle * 180 / Math.PI;
			Settings.Default.ScrewAngleOffset = screwAngleOffset * 180 / Math.PI;

			Settings.Default.IsHypoid = isHypoid;
			Settings.Default.HypoidAngle = hypoidAngle * 180 / Math.PI;
			Settings.Default.HypoidOffset = hypoidOffset * lengthConversion;

			Settings.Default.Save();
		}

		protected static ToothProfile GetGearProfileFromOptions(GearData gearData, GearData conjugateGearData, bool useTrochoidalInterferenceRemoval, bool addDedendumClearance, double screwAngle = 0) {
			ToothProfile toothProfile = null;
			if (addDedendumClearance) {
				if (useTrochoidalInterferenceRemoval)
					toothProfile = new ExtendedTrochoidalToothProfile(gearData, conjugateGearData);
				else {
					if (gearData.IsSmallDedendum)
						toothProfile = new ExtendedToothProfile(gearData);
					else
						toothProfile = new BasicToothProfile(gearData);
				}
			}
			else {
				if (useTrochoidalInterferenceRemoval)
					toothProfile = new BasicTrochoidalToothProfile(gearData, conjugateGearData);
				else {
					if (gearData.IsSmallDedendum)
						toothProfile = new ExtendedToothProfile(gearData);
					else
						toothProfile = new BasicToothProfile(gearData);
				}

			}

			return toothProfile;
		}

		private Circle SelectedCircle(Window window) {
			ICollection<ITrimmedCurve> curves = window.GetAllSelectedITrimmedCurves();
			if (curves.Count != 1)
				return null;

			return curves.First().Geometry as Circle; ;
		}

	}

	class GearIsBevelCapsule : RibbonBooleanGroupCapsule {
		public GearIsBevelCapsule(string name, RibbonTabCapsule parent)
			: base(name, Resources.IsBevelText, Resources.IsBevelText, parent) {

			RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsBevelText].IsEnabledCommandBoolean.Value = Settings.Default.IsBevel;
			Values[Resources.BevelAngleText] = new RibbonCommandValue(Settings.Default.BevelAngle);
			Values[Resources.BevelKneeRatioText] = new RibbonCommandValue(Settings.Default.BevelKneeRatio);
		}

	}

	class GearIsHelicalCapsule : RibbonBooleanGroupCapsule {
		public GearIsHelicalCapsule(string name, RibbonTabCapsule parent)
			: base(name, Resources.IsHelicalText, Resources.IsHelicalText, parent) {

			RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHelicalText].IsEnabledCommandBoolean.Value = Settings.Default.IsHelical;
			Values[Resources.HelicalAngleText] = new RibbonCommandValue(Settings.Default.HelicalAngle);
		}

	}

	class GearIsScrewCapsule : RibbonBooleanGroupCapsule {
		public GearIsScrewCapsule(string name, RibbonTabCapsule parent)
			: base(name, Resources.IsScrewText, Resources.IsScrewText, parent) {

			RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsScrewText].IsEnabledCommandBoolean.Value = Settings.Default.IsScrew;
			Values[Resources.ScrewAngleText] = new RibbonCommandValue(Settings.Default.ScrewAngle);
			Values[Resources.ScrewAngleBiasText] = new RibbonCommandValue(Settings.Default.ScrewAngleOffset);
		}

	}

	class GearIsHypoidCapsule : RibbonBooleanGroupCapsule {
		public GearIsHypoidCapsule(string name, RibbonTabCapsule parent)
			: base(name, Resources.IsHypoidText, Resources.IsHypoidText, parent) {

			RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsHypoidText].IsEnabledCommandBoolean.Value = Settings.Default.IsHypoid;
			Values[Resources.HypoidAngleText] = new RibbonCommandValue(Settings.Default.HypoidAngle);
			Values[Resources.HypoidOffsetText] = new RibbonCommandValue(Settings.Default.HypoidOffset);
		}

	}


	class GearCalculationsContainerCapsule : RibbonGroupCapsule {
		public GearCalculationsContainerCapsule(string name, RibbonTabCapsule parent)
			: base(name, "Calculations", parent, LayoutOrientation.vertical) {

			this.Spacing = 6;
		}
	}

	abstract class GearCalculationsCapsule : RibbonLabelCapsule {
		static protected RibbonGroupCapsule group;
		static protected double lengthConversion;
		static protected int numberOfTeethL, numberOfTeethR;
		static protected bool isInternalL, isInternalR;
		static protected double pressureAngle;
		static protected double module;
		static protected double dedendumClearance;
		static protected double depth;

		static protected bool isBevel;
		static protected double bevelAngle;

		static protected GearData gearDataL;
		static protected GearData gearDataR;

		public GearCalculationsCapsule(string name, RibbonGroupCapsule group, RibbonCommandCapsule parent)
			: base(name, string.Empty, string.Empty, parent, 400) {

			GearCalculationsCapsule.group = group;
			this.Justification = LabelJustification.near;
		}

		protected override void OnUpdate(Command command) {
			base.OnUpdate(command);

			lengthConversion = Window.ActiveWindow.Units.Length.ConversionFactor;

			numberOfTeethL = (int) group.Values[Resources.NumberOfTeethLText].Value;
			numberOfTeethR = (int) group.Values[Resources.NumberOfTeethRText].Value;
			isInternalL = numberOfTeethL < 0;
			isInternalR = numberOfTeethR < 0;
			numberOfTeethL = Math.Abs(numberOfTeethL);
			numberOfTeethR = Math.Abs(numberOfTeethR);

			pressureAngle = group.Values[Resources.PressureAngleText].Value * Math.PI / 180;
			module = group.Values[Resources.ModuleText].Value / lengthConversion;
			dedendumClearance = group.Values[Resources.DedendumClearanceText].Value;
			depth = group.Values[Resources.DepthText].Value / lengthConversion;

			isBevel = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsBevelText].IsEnabledCommandBoolean.Value;
			bevelAngle = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsBevelText].Values[Resources.BevelAngleText].Value;


			gearDataL = new GearData(numberOfTeethL, pressureAngle, module, dedendumClearance, isInternalL);
			gearDataR = new GearData(numberOfTeethR, pressureAngle, module, dedendumClearance, isInternalR);
			command.Text = String.Format("Module: {0:0.000}, Center Distance: {1:0.000}", gearDataL.Module * lengthConversion, (gearDataL.PitchRadius + gearDataR.PitchRadius) * lengthConversion);
		}
	}

	class GearCalculationsLine1Capsule : GearCalculationsCapsule {
		public GearCalculationsLine1Capsule(string name, RibbonGroupCapsule group, RibbonCommandCapsule parent)
			: base(name, group, parent) { }

		protected override void OnUpdate(Command command) {
			base.OnUpdate(command);

			command.Text = String.Format(Resources.CalculationsLine1Text,
				gearDataL.Pitch * lengthConversion,
				(gearDataL.PitchRadius + gearDataR.PitchRadius) * lengthConversion,
				(gearDataL.AddendumDiameter + gearDataR.AddendumDiameter) * lengthConversion
			);
		}
	}

	class GearCalculationsLine2Capsule : GearCalculationsCapsule {
		public GearCalculationsLine2Capsule(string name, RibbonGroupCapsule group, RibbonCommandCapsule parent)
			: base(name, group, parent) { }

		protected override void OnUpdate(Command command) {
			base.OnUpdate(command);

			command.Text = String.Format(Resources.CalculationsLine2Text,
				gearDataL.PitchDiameter * lengthConversion,
				gearDataL.AddendumDiameter * lengthConversion,
				gearDataL.DedendumDiameter * lengthConversion
			);
		}
	}
	class GearCalculationsLine3Capsule : GearCalculationsCapsule {
		public GearCalculationsLine3Capsule(string name, RibbonGroupCapsule group, RibbonCommandCapsule parent)
			: base(name, group, parent) { }

		protected override void OnUpdate(Command command) {
			base.OnUpdate(command);

			command.Text = String.Format(Resources.CalculationsLine3Text,
				gearDataR.PitchDiameter * lengthConversion,
				gearDataR.AddendumDiameter * lengthConversion,
				gearDataR.DedendumDiameter * lengthConversion
			);
		}
	}

	class MobiusButtonCapsule : RibbonButtonCapsule {
		public MobiusButtonCapsule(string name, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("MobiusGearRing", "Mobius", null, "Mobius", parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			int numGears = 11;
			Debug.Assert(MainPart.Components.Count == numGears);
			var chain = new List<Component>();

			List<AlignCondition> aligns = MainPart.MatingConditions.Where(c => c is AlignCondition).Cast<AlignCondition>().ToList();
			chain.Add(aligns.First().GeometricA.GetAncestor<Component>());
			while (chain.Count != numGears) {
				foreach (AlignCondition align in aligns) {
					Component component = align.GeometricA.GetAncestor<Component>();
					if (!chain.Contains(component)) {
						chain.Add(component);
						break;
					}

					component = align.GeometricB.GetAncestor<Component>();
					if (!chain.Contains(component)) {
						chain.Add(component);
						break;
					}
				}
			}

			for (int iter = 0; iter < 100; iter++) {

				//		foreach (AlignCondition align in aligns)
				//			align.IsEnabled = false;

				Random random = new Random();
				var angles = new List<double>();
				for (int i = 0; i < numGears; i++)
					angles.Add(AngleBetween(chain, i));

				double averageAngle = angles.Average();

				//		double scale = 0.002;
				for (int i = 0; i < numGears; i++) {
					//Vector noise = Vector.Create(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5) * scale;
					Vector spring = -SpringDistance(chain, i) * (angles[i] - averageAngle) / 10;
					Matrix trans = Matrix.CreateTranslation(spring);
					Matrix rot = Matrix.CreateRotation(Line.Create(chain[i].Placement * Point.Origin, Vector.Create(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Direction), (random.NextDouble() - 0.5) * 0.001);
					chain[i].Transform(trans * rot);
				}

				//		int offset = random.Next(numGears);
				//		for (int i = 0; i < numGears; i++) 
				//			aligns[(i+offset)%numGears].IsEnabled = true;

			}
#if false
			Frame frame = Frame.World;
			Circle baseCircle = Circle.Create(frame, 0.05);
			baseCircle.Print();

			int gearCount = 5; // odd
			var controlCircles = new Circle[gearCount];
			var controlAxes = new Line[gearCount];

			double spacing = Math.PI * 2 / gearCount;
			double spacingOffset = -spacing / 2;
			double angleIncrement = spacing / 2;

			for (int i = 0; i < gearCount; i++) {
				Point p0 = baseCircle.Evaluate(spacingOffset + spacing * i).Point;
				Point p1 = baseCircle.Evaluate(spacingOffset + spacing * (i + 1)).Point;
				Point midPoint = new Point[] { p0, p1 }.Average();
				controlAxes[i] = Line.Create(midPoint, (p1 - p0).Direction);

				controlCircles[i] = Circle.Create(Frame.Create(midPoint, (baseCircle.Frame.Origin - midPoint).Direction), (p1 - p0).Magnitude / 2).
					CreateTransformedCopy(Matrix.CreateRotation(controlAxes[i], angleIncrement * i));
			}

			controlCircles.Print();

			int counter = 10000;
			while (counter-- >= 0) {
				var angles = ComputeAngles(controlCircles);
				double averageAngle = angles.Average();

				for (int i = 1; i < gearCount; i++) {
					controlCircles[i] = controlCircles[i].CreateTransformedCopy(Matrix.CreateRotation(controlAxes[i], (averageAngle - angles[i - 1])));
				}

				string op = string.Empty;
				for (int i = 0; i < gearCount; i++) {
					op += angles[i].ToString() + "  ";
				}

				if (counter % 1000 == 0)
					Application.ReportStatus(op, StatusMessageType.Information, null);
			}

			controlCircles.Print();

			ActiveWindow.InteractionMode = InteractionMode.Solid;
			ActiveWindow.ZoomExtents();
#endif
		}

		private static double AngleBetween(List<Component> chain, int i) {
			int numGears = chain.Count;
			return AddInHelper.AngleBetween(chain[i].Placement.Translation - chain[(i + 1) % numGears].Placement.Translation, chain[i].Placement.Translation - chain[(i + numGears - 1) % numGears].Placement.Translation);
		}

		private static Vector SpringDistance(List<Component> chain, int i) {
			int numGears = chain.Count;
			return (chain[i].Placement.Translation - chain[(i + 1) % numGears].Placement.Translation) + (chain[i].Placement.Translation - chain[(i + numGears - 1) % numGears].Placement.Translation);
		}

		double[] ComputeAngles(Circle[] circles) {
			int count = circles.Length;

			var angles = new double[count];
			for (int i = 0; i < count; i++) {
				int otherIndex = i == count - 1 ? 0 : i + 1;
				angles[i] = AddInHelper.AngleBetween(circles[i].Axis.Direction, circles[otherIndex].Axis.Direction) / 2;
			}

			angles[count - 1] = Math.PI / 2 - angles[count - 1];
			return angles;
		}
	}
}
