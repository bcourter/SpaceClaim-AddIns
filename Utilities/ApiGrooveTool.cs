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
    struct ApiGroove {
        public double InnerDiameter { get; private set; }
        public double OuterDiameter { get; private set; }
        public double Depth { get; private set; }

        public ApiGroove(double innerDiameter, double outerDiameter, double depth)
            : this() {
            InnerDiameter = innerDiameter;
            OuterDiameter = outerDiameter;
            Depth = depth;
        }

        public double Angle { get { return Const.Tau * 23 / 360; } }
        public string Name { get { return string.Format("ID: {0}", InnerDiameter); } }
    }

    static class SizeComboBox {
        const string commandName = "ApiGrooveToolSizeList";

        static readonly ApiGroove[] sizeList = {
			new ApiGroove(1 * Const.inches, 1.25 * Const.inches, 0.0625*Const.inches),
			new ApiGroove(2 * Const.inches, 2.25 * Const.inches, 0.0625*Const.inches)
		};

        public static void Initialize() {
            Command command = Command.Create(commandName);

            string[] items = Array.ConvertAll(sizeList, s => s.Name);
            command.ControlState = ComboBoxState.CreateFixed(items, 0);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static ApiGroove SelectedSize {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return sizeList[state.SelectedIndex];
            }
        }
    }

    class ApiGrooveToolCapsule : RibbonButtonCapsule {

        public ApiGrooveToolCapsule(string commandName, RibbonCollectionCapsule parent, ButtonSize size)
            : base(commandName, Resources.ApiGrooveToolText, null, Resources.ApiGrooveToolHint, parent, size) {
        }

        protected override void OnInitialize(Command command) {
            SizeComboBox.Initialize();
        }

        protected override void OnUpdate(Command command) {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is ApiGrooveTool;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect) {
            Window window = Window.ActiveWindow;
            window.SetTool(new ApiGrooveTool());
        }
    }

    class ApiGrooveTool : Tool {
        Plane apiGroovePlane;
        Fin apiGrooveFin;
        ApiGroove apiGroove;

        public ApiGrooveTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.ApiGrooveToolOptions; }
        }

        protected override void OnInitialize() {
            Reset();
        }

        void Reset() {
            apiGroovePlane = null;
            apiGrooveFin = null;

            Rendering = null;
            SelectionTypes = new[] { typeof(DesignEdge) };
            StatusText = Resources.ApiGrooveStatusText;
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            var designEdge = docObject as DesignEdge;
            if (designEdge != null)
                return designEdge.Shape.GetGeometry<Circle>() != null ? designEdge : null;

            Debug.Fail("Unexpected case");
            return null;
        }

        protected override void OnEnable(bool enable) {
            if (enable)
                Window.PreselectionChanged += Window_PreselectionChanged;
            else
                Window.PreselectionChanged -= Window_PreselectionChanged;

            if (enable) {
                apiGroove = SizeComboBox.SelectedSize;
                SizeComboBox.Command.TextChanged += apiSizeCommand_TextChanged;
            }
            else
                SizeComboBox.Command.TextChanged -= apiSizeCommand_TextChanged;
        }

        void apiSizeCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            apiGroove = SizeComboBox.SelectedSize;
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
            DesignEdge designEdge = preselection as DesignEdge;
            if (designEdge == null) // selection filtering is not applied if you (pre)select in the tree
                return false;

            Circle edgeCircle = (Circle)designEdge.Shape.Geometry;

            CurveSegment innerCurve = CurveSegment.Create(Circle.Create(edgeCircle.Frame, apiGroove.InnerDiameter / 2));
            CurveSegment outerCurve = CurveSegment.Create(Circle.Create(edgeCircle.Frame, apiGroove.OuterDiameter / 2));

            var style = new GraphicStyle {
                LineColor = Color.DarkGray,
                LineWidth = 2
            };
            Graphic centerLine = Graphic.Create(style, new[] { CurvePrimitive.Create(innerCurve), CurvePrimitive.Create(outerCurve) });

            style = new GraphicStyle {
                LineColor = Color.White,
                LineWidth = 4
            };
            Graphic highlightLine = Graphic.Create(style, new[] { CurvePrimitive.Create(innerCurve), CurvePrimitive.Create(outerCurve) });

            Rendering = Graphic.Create(style, null, new[] { highlightLine, centerLine });
            return false; // if we return true, the preselection won't update
        }

        #region Click-Click Notifications

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            DesignEdge designEdge = InteractionContext.Preselection as DesignEdge;
            if (designEdge == null)
                return false;

            Circle edgeCircle = (Circle)designEdge.Shape.Geometry;
            Frame frame = edgeCircle.Frame;
            Face face = designEdge.Faces.Where(f => f.Shape.Geometry is Plane).First().Shape;
            Plane plane = (Plane)face.Geometry;
            if (frame.DirZ == plane.Frame.DirZ ^ !face.IsReversed)
                frame = Frame.Create(frame.Origin, -frame.DirZ);

            double angle = apiGroove.Angle;
            double depth = apiGroove.Depth;
            var points = new[] {
                Point.Create(apiGroove.InnerDiameter/2, 0, 0),
                Point.Create(apiGroove.InnerDiameter/2 + Math.Sin(angle) * depth, 0, -Math.Cos(angle) * depth),
                Point.Create(apiGroove.OuterDiameter/2 - Math.Sin(angle) * depth, 0, -Math.Cos(angle) * depth),
                Point.Create(apiGroove.OuterDiameter/2, 0, 0)
            };

            var profile = points.AsPolygon();
            var path = new[] { CurveSegment.Create(Circle.Create(Frame.World, 1)) };

            WriteBlock.ExecuteTask("Create Profile", () => {
                var body = Body.SweepProfile(Plane.PlaneZX, profile, path);
                body.Transform(Matrix.CreateMapping(frame));
                var cutter = DesignBody.Create(designEdge.Parent.Parent, "temp", body);
                designEdge.Parent.Shape.Subtract(new[] { cutter.Shape });
            });

            return false;
        }

        #endregion

    }

}
