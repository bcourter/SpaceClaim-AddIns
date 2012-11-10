using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Display;
using Utilities.Properties;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using Point = SpaceClaim.Api.V10.Geometry.Point;
using ScreenPoint = System.Drawing.Point;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Utilities {
    enum ThreadSpecificationType {
        ThreadsPerInch = 0,
        MillimetersPerThread = 1
    }

    static class TypeComboBox {
        const string commandName = "ThreadSpecificationTypeList";
        static readonly Dictionary<ThreadSpecificationType, string> names =
            new Dictionary<ThreadSpecificationType, string>() {
                {ThreadSpecificationType.ThreadsPerInch, Resources.ThreadSpecificationThreadsPerInch},
                {ThreadSpecificationType.MillimetersPerThread, Resources.ThreadSpecificationMillimetersPerThread}
            };

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = ComboBoxState.CreateFixed(names.Values.ToArray(), 0);
        }

        public static string TypeText {
            get { return names[Type]; }
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static ThreadSpecificationType Type {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return names.Keys.ToArray()[state.SelectedIndex];
            }
        }
    }

    static class PitchTextBox {
        const string commandName = "ThreadPitchValue";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.Text = "20";
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Pitch {
            get {
                // var state = (ControlState)Command.ControlState;
                double value;
                if (!Double.TryParse(Command.Text, out value))
                    return 0;

                if (TypeComboBox.Type == ThreadSpecificationType.ThreadsPerInch)
                    return Const.inches / value;

                if (TypeComboBox.Type == ThreadSpecificationType.MillimetersPerThread)
                    return value * 1E-3;

                throw new NotImplementedException();
                // return 0;
            }
        }
    }

    static class AngleTextBox {
        const string commandName = "ThreadAngleValue";
        const string labelCommandName = "ThreadAngleLabel";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.Text = "60";
            command = Command.Create(labelCommandName);
            command.Text = Resources.ThreadAngle + ":";
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Value {
            get {
                double value;
                if (!Double.TryParse(Command.Text, out value))
                    return 0;

                return value / 360 * Const.Tau;
            }
        }
    }

    static class PositionComboBox {
        const string commandName = "ThreadPositionList";
        const string labelCommandName = "ThreadPositionLabel";
        static readonly Dictionary<double, string> names =
            new Dictionary<double, string>() {
                {-0.5, Resources.ThreadPositionInside},
                {0, Resources.ThreadPositionCenter},
                {0.5, Resources.ThreadPositionOutside}
            };

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = ComboBoxState.CreateFixed(names.Values.ToArray(), 0);
            command = Command.Create(labelCommandName);
            command.Text = Resources.ThreadPosition + ":";
        }

        public static string TypeText {
            get { return names[Offset]; }
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Offset {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return names.Keys.ToArray()[state.SelectedIndex];
            }
        }
    }

    class ThreadToolCapsule : RibbonButtonCapsule {
        public ThreadToolCapsule(string commandName, RibbonCollectionCapsule parent, ButtonSize size)
            : base(commandName, Resources.ThreadToolCommandText, Resources.Threads32, Resources.ThreadToolCommandHint, parent, size) {
        }

        protected override void OnInitialize(Command command) {
            TypeComboBox.Initialize();
            PitchTextBox.Initialize();
            AngleTextBox.Initialize();
            PositionComboBox.Initialize();
        }

        protected override void OnUpdate(Command command) {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is ThreadTool;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect) {
            Window window = Window.ActiveWindow;
            window.SetTool(new ThreadTool());
        }
    }

    class ThreadTool : Tool {
        ThreadSpecificationType type;
        double pitch;
        double angle;
        double positionOffset;

        public ThreadTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.ThreadToolOptions; }
        }

        protected override void OnInitialize() {
            Reset();
            type = TypeComboBox.Type;
            pitch = PitchTextBox.Pitch;
        }

        void Reset() {
            Rendering = null;
            SelectionTypes = new[] { typeof(DesignFace) };
            StatusText = Resources.ThreadToolStatusText;
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            var designFace = docObject as DesignFace;
            if (designFace != null)
                return designFace.Shape.GetGeometry<Cylinder>() != null ? designFace : null;

            Debug.Fail("Unexpected case");
            return null;
        }

        protected override void OnEnable(bool enable) {
            if (enable)
                Window.PreselectionChanged += Window_PreselectionChanged;
            else
                Window.PreselectionChanged -= Window_PreselectionChanged;

            if (enable) {
                type = TypeComboBox.Type;
                pitch = PitchTextBox.Pitch;
                angle = AngleTextBox.Value;
                positionOffset = PositionComboBox.Offset;
                TypeComboBox.Command.TextChanged += TypeCommand_TextChanged;
                PitchTextBox.Command.TextChanged += PitchCommand_TextChanged;
                AngleTextBox.Command.TextChanged += AngleCommand_TextChanged;
                PositionComboBox.Command.TextChanged += PositionCommand_TextChanged;
            }
            else {
                TypeComboBox.Command.TextChanged -= TypeCommand_TextChanged;
                PitchTextBox.Command.TextChanged -= PitchCommand_TextChanged;
                AngleTextBox.Command.TextChanged -= AngleCommand_TextChanged;
                PositionComboBox.Command.TextChanged -= PositionCommand_TextChanged;
            }
        }

        void TypeCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            type = TypeComboBox.Type;
            pitch = PitchTextBox.Pitch;
        }

        void PitchCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            pitch = PitchTextBox.Pitch;
        }

        void AngleCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            angle = AngleTextBox.Value;
        }

        void PositionCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            positionOffset = PositionComboBox.Offset;
        }

        void Window_PreselectionChanged(object sender, EventArgs e) {
            // preselection can change without the mouse moving (e.g. just created a profile)
            Rendering = null;

            InteractionContext context = InteractionContext;
            Line cursorRay = context.CursorRay;
            if (cursorRay != null)
                OnMouseMove(context.Window.CursorPosition, cursorRay, Control.MouseButtons);
        }

        protected override bool OnMouseMove(ScreenPoint cursorPos, Line cursorRay, MouseButtons button) {
            if (button != MouseButtons.None)
                return false;

            IDocObject preselection = InteractionContext.Preselection;
            DesignFace designFace = preselection as DesignFace;
            if (designFace == null) // selection filtering is not applied if you (pre)select in the tree
                return false;

            CurveSegment innerCurve, outerCurveA, outerCurveB;
            CreateThreadCurves(designFace.Shape, pitch, angle, positionOffset, out innerCurve, out outerCurveA, out outerCurveB);
            var primitives = new[] { CurvePrimitive.Create(innerCurve), CurvePrimitive.Create(outerCurveA), CurvePrimitive.Create(outerCurveB) };

            var style = new GraphicStyle {
                LineColor = Color.DarkGray,
                LineWidth = 2
            };
            Graphic centerLine = Graphic.Create(style, primitives);

            style = new GraphicStyle {
                LineColor = Color.White,
                LineWidth = 4
            };
            Graphic highlightLine = Graphic.Create(style, primitives);

            Rendering = Graphic.Create(style, null, new[] { highlightLine, centerLine });
            return false; // if we return true, the preselection won't update
        }

        #region Click-Click Notifications

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            DesignFace designFace = InteractionContext.Preselection as DesignFace;
            if (designFace == null)
                return false;

            CurveSegment innerCurve, outerCurveA, outerCurveB;
            CreateThreadCurves(designFace.Shape, pitch, angle, positionOffset, out innerCurve, out outerCurveA, out outerCurveB);

            WriteBlock.ExecuteTask(Resources.ThreadStructureText, () => {
                DesignBody.Create(designFace.Parent.Parent, Resources.ThreadStructureText, CreateThreadBody(designFace.Shape, pitch, angle, positionOffset));
            });

            return false;
        }

        #endregion


        private static Body CreateThreadBody(Face cylinderFace, double pitch, double angle, double positionOffset) {
            double stitchTolerance = Accuracy.LinearResolution * 1E4;

            double radius, innerRadius, outerRadius;
            double start, end;
            Line axis;
            CurveSegment innerCurve, outerCurveA, outerCurveB;
            Matrix trans;
            CreateUntransformedThreadCurves(cylinderFace, pitch, angle, positionOffset, out radius, out axis, out start, out end, out innerCurve, out outerCurveA, out outerCurveB, out trans, out innerRadius, out outerRadius);

            Body loftBodyA = Body.LoftProfiles(new[] { new[] { outerCurveA }, new[] { innerCurve } }, false, false);
            Body loftBodyB = Body.LoftProfiles(new[] { new[] { innerCurve }, new[] { outerCurveB } }, false, false);
            loftBodyA.Stitch(new[] { loftBodyB }, stitchTolerance, null);
            loftBodyA.Print();

            double threadDepth = outerRadius - innerRadius;
            double length = end - start;
            double padding = 1.1;
            double paddedOuterRadius = innerRadius + threadDepth * padding;
            //var capA = Body.ExtrudeProfile(Plane.PlaneXY, new[] { CurveSegment.Create(Circle.Create(Frame.World, radius * 2)) }, end - start);
            var capA = Body.SweepProfile(Plane.PlaneZX, new[] {
                Point.Origin,
                Point.Create(innerRadius, 0, 0),
                Point.Create(paddedOuterRadius, 0, threadDepth * Math.Tan(angle/2) * padding),
                Point.Create(paddedOuterRadius, 0, length - threadDepth * Math.Tan(angle/2) * padding),
                Point.Create(innerRadius, 0, length),
                Point.Create(0, 0, length)
            }.AsPolygon(), new[] { CurveSegment.Create(Circle.Create(Frame.World, 1)) });
            loftBodyA.Imprint(capA);

            loftBodyA.DeleteFaces(loftBodyA.Faces.Where(f => f.Edges.Where(e => e.Faces.Count == 1).Count() > 0).ToArray(), RepairAction.None);
            capA.DeleteFaces(capA.Faces
                .Where(f => f.Edges
                    .Where(e => (e.Geometry is Circle && Accuracy.EqualLengths(((Circle)e.Geometry).Radius, paddedOuterRadius))
                ).Count() > 0).ToArray(),
                RepairAction.None
            );

            loftBodyA.Stitch(new[] { capA }, stitchTolerance, null);
            loftBodyA.Transform(trans);

            return loftBodyA;
        }

        private static void CreateThreadCurves(Face cylinderFace, double pitch, double angle, double positionOffset, out CurveSegment innerCurve, out CurveSegment outerCurveA, out CurveSegment outerCurveB) {
            double radius, innerRadius, outerRadius;
            double start, end;
            Line axis;
            Matrix trans;
            CreateUntransformedThreadCurves(cylinderFace, pitch, angle, positionOffset, out radius, out axis, out start, out end, out innerCurve, out outerCurveA, out outerCurveB, out trans, out innerRadius, out outerRadius);

            var planeA = Plane.PlaneXY;
            var planeB = Plane.Create(Frame.Create(Point.Create(0, 0, end - start), Direction.DirZ));

            innerCurve = TrimAndTransform(ref trans, planeA, planeB, innerCurve);
            outerCurveA = TrimAndTransform(ref trans, planeA, planeB, outerCurveA);
            outerCurveB = TrimAndTransform(ref trans, planeA, planeB, outerCurveB);
        }

        private static CurveSegment TrimAndTransform(ref Matrix trans, Plane planeA, Plane planeB, CurveSegment curve) {
            double startParam = planeA.IntersectCurve(curve.Geometry).First().EvaluationB.Param;
            double endParam = planeB.IntersectCurve(curve.Geometry).First().EvaluationB.Param;
            curve = CurveSegment.Create(curve.Geometry, Interval.Create(startParam, endParam));
            return curve.CreateTransformedCopy(trans);
        }

        private static void CreateUntransformedThreadCurves(Face cylinderFace, double pitch, double angle, double positionOffset, out double radius, out Line axis, out double start, out double end, out CurveSegment innerCurve, out CurveSegment outerCurveA, out CurveSegment outerCurveB, out Matrix trans, out double innerRadius, out double outerRadius) {
            Cylinder cylinder = cylinderFace.Geometry as Cylinder;
            Debug.Assert(cylinder != null);

            radius = cylinder.Radius;
            axis = cylinder.Axis;
            Line axisCopy = axis; //needed for out property in lambda expression

            double[] points = cylinderFace.Loops
                .Where(l => l.IsOuter)
                .SelectMany(l => l
                    .Vertices.Select(v => v.Position)
                    .Select(p => axisCopy.ProjectPoint(p).Param)
                ).ToArray();

            start = points.Min();
            end = points.Max();
            double length = end - start + pitch * 2;
            double threadDepth = pitch / (2 * Math.Tan(angle / 2));
            innerRadius = radius + threadDepth * (positionOffset - 0.5);
            outerRadius = radius + threadDepth * (positionOffset + 0.5);

            int pointsPerTurn = 360;
            int pointCount = (int)(length / pitch * pointsPerTurn);
            var outerPoints = new Point[pointCount];
            var innerPoints = new Point[pointCount];
            for (int i = 0; i < pointCount; i++) {
                double t = (double)i / pointCount;
                double rotation = Const.Tau * t * length / pitch;
                double depth = length * t;
                outerPoints[i] = Point.Create(outerRadius * Math.Cos(rotation), outerRadius * Math.Sin(rotation), depth);
                innerPoints[i] = Point.Create(innerRadius * Math.Cos(rotation), innerRadius * Math.Sin(rotation), depth);
            }

            var outerCurve = CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, outerPoints, Accuracy.LinearResolution * 1E1));
            innerCurve = CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, innerPoints, Accuracy.LinearResolution * 1E1));

            var translation = Matrix.CreateTranslation(Vector.Create(0, 0, -pitch));
            var offset = Matrix.CreateTranslation(Direction.DirZ * pitch / 2);
            outerCurveA = outerCurve.CreateTransformedCopy(translation * offset);
            outerCurveB = outerCurve.CreateTransformedCopy(translation * offset.Inverse);
            innerCurve = innerCurve.CreateTransformedCopy(translation);

            trans = Matrix.CreateMapping(Frame.Create(axis.Evaluate(start).Point, axis.Direction));
        }

    }

}
