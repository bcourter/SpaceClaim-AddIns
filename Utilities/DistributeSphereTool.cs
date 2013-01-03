using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
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
    static class ColorComboBox {
        const string commandName = "DistributeSpheresToolColorList";

        static readonly Color[] colorList = {
			Color.Gray,
			Color.Red,
			Color.Yellow,
			Color.Green,
			Color.Cyan,
			Color.Blue,
			Color.Magenta
		};

        public static void Initialize() {
            Command command = Command.Create(commandName);

            string[] items = Array.ConvertAll(colorList, color => color.Name);
            command.ControlState = ComboBoxState.CreateFixed(items, 0);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static Color Value {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return colorList[state.SelectedIndex];
            }
            set {
                var state = (ComboBoxState)Command.ControlState;
                for (int i = 0; i < state.Items.Count; i++) {
                    if (colorList[i] == value) {
                        Command.ControlState = ComboBoxState.CreateFixed(state.Items, i);
                        break;
                    }
                }
            }
        }
    }

    static class CountSlider {
        const string commandName = "DistributeSpheresToolCountSlider";
        const int sliderScale = 512;
        const int startCount = 64;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = SliderState.Create(startCount, 3, sliderScale);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static int Value {
            get {
                var state = (SliderState)Command.ControlState;
                return state.Value;
            }
            set {
                var state = (SliderState)Command.ControlState;
                Command.ControlState = SliderState.Create(value, state.MinimumValue, state.MaximumValue);
            }
        }
    }

    static class StrengthSlider {
        const string commandName = "DistributeSpheresToolStrengthSlider";
        const int scale = 100;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = SliderState.Create(0, -scale, scale);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Value {
            get {
                var state = (SliderState)Command.ControlState;
                return Math.Pow(2, (double)state.Value / scale);
            }
            set {
                var state = (SliderState)Command.ControlState;
                Command.ControlState = SliderState.Create((int)(Math.Log(value, 2) * scale), state.MinimumValue, state.MaximumValue);
            }
        }
    }

    static class AnimateStepButton {
        const string commandName = "DistributeSpheresToolAnimateStepButton";
        const string commandText = "Step";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.IsWriteBlock = false;
            command.Text = commandText;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }
    }

    static class AnimatePlayButton {
        const string commandName = "DistributeSpheresToolAnimatePlayButton";
        const string commandText = "Play";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.IsWriteBlock = false;
            command.Text = commandText;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }
    }

    static class RadiusSlider {
        const string commandName = "DistributeSpheresToolSlider";
        const double sliderScale = 0.008;
        const int sliderTicks = 100;
        const double startRadius = 0.0005;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = SliderState.Create(1, 1, sliderTicks);
            Value = startRadius;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Value {
            get {
                var state = (SliderState)Command.ControlState;
                return (double)state.Value / sliderTicks * sliderScale;
            }
            set {
                var state = (SliderState)Command.ControlState;
                Command.ControlState = SliderState.Create((int)(value / sliderScale * sliderTicks), state.MinimumValue, state.MaximumValue);
            }
        }
    }

    static class CreateSpheresButton {
        const string commandName = "DistributeSpheresToolCreateSpheres";
        const string commandText = "Create Spheres";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.Text = commandText;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }
    }

    class DistributeSpheresToolCapsule : RibbonButtonCapsule {

        public DistributeSpheresToolCapsule(string commandName, RibbonCollectionCapsule parent, ButtonSize size)
            : base(commandName, Resources.DistributeSpheresToolText, Resources.DistributeSpheres32, Resources.DistributeSpheresToolHint, parent, size) {
        }

        protected override void OnInitialize(Command command) {
            SphereSet.InitializeGraphics();

            ColorComboBox.Initialize();
            CountSlider.Initialize();
            StrengthSlider.Initialize();
            AnimateStepButton.Initialize();
            AnimatePlayButton.Initialize();
            RadiusSlider.Initialize();
            CreateSpheresButton.Initialize();
        }

        protected override void OnUpdate(Command command) {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is DistributeSpheresTool;
        }

        protected override void OnExecute(Command command, SpaceClaim.Api.V10.ExecutionContext context, Rectangle buttonRect) {
            Window window = Window.ActiveWindow;
            window.SetTool(new DistributeSpheresTool());
        }
    }

    class DistributeSpheresTool : Tool {
        int count;
        double strength;
        double radius;
        Color color;
        SphereSetAnimator animator;

        public DistributeSpheresTool()
            : base(InteractionMode.Solid) {
        }

#if false
		protected override bool OnEscape() {
			Window.SetTool(new SketchTool());
			return true;
		}
#endif

        public override string OptionsXml {
            get { return Resources.DistributeSpheresToolOptions; }
        }

        protected override void OnInitialize() {
            Reset();
            animator = new SphereSetAnimator();
        }

        void Reset() {
            Rendering = null;
            SelectionTypes = new[] { typeof(DesignFace), typeof(CustomObject) };
            StatusText = "Click on a face to create a new sphere set.";

            count = CountSlider.Value;
            strength = StrengthSlider.Value;
            radius = RadiusSlider.Value;
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            var desFace = docObject as DesignFace;
            if (desFace != null)
                return desFace;

            var custom = docObject as CustomObject;
            if (custom != null)
                return custom.Type == SphereSet.Type ? custom : null;

            Debug.Fail("Unexpected case");
            return null;
        }

        protected override void OnEnable(bool enable) {
            if (enable)
                Window.PreselectionChanged += Window_PreselectionChanged;
            else
                Window.PreselectionChanged -= Window_PreselectionChanged;

            if (enable) {
                count = CountSlider.Value;
                CountSlider.Command.TextChanged += countSliderCommand_TextChanged;
            }
            else
                CountSlider.Command.TextChanged -= countSliderCommand_TextChanged;

            if (enable) {
                strength = StrengthSlider.Value;
                StrengthSlider.Command.TextChanged += strengthSliderCommand_TextChanged;
            }
            else
                StrengthSlider.Command.TextChanged -= strengthSliderCommand_TextChanged;

            if (enable) {
                AnimateStepButton.Command.Executing += animateStepCommand_Execute;
            }
            else
                AnimateStepButton.Command.Executing -= animateStepCommand_Execute;

            if (enable) {
                AnimatePlayButton.Command.Executing += animatePlayCommand_Execute;
            }
            else
                AnimatePlayButton.Command.Executing -= animatePlayCommand_Execute;


            if (enable) {
                color = ColorComboBox.Value;
                ColorComboBox.Command.TextChanged += colorCommand_TextChanged;
            }
            else
                ColorComboBox.Command.TextChanged -= colorCommand_TextChanged;

            if (enable) {
                radius = RadiusSlider.Value;
                RadiusSlider.Command.TextChanged += radiusSliderCommand_TextChanged;
            }
            else
                RadiusSlider.Command.TextChanged -= radiusSliderCommand_TextChanged;

            if (enable) {
                CreateSpheresButton.Command.Executing += createSphereCommand_Execute;
            }
            else
                CreateSpheresButton.Command.Executing -= createSphereCommand_Execute;
        }

        void countSliderCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            count = CountSlider.Value;

            SphereSet sphereSet = SelectedSphereSet;
            if (sphereSet != null)
                WriteBlock.ExecuteTask("Adjust radius", () => sphereSet.Count = count);
        }

        void strengthSliderCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            strength = StrengthSlider.Value;

            SphereSet sphereSet = SelectedSphereSet;
            if (sphereSet != null)
                WriteBlock.ExecuteTask("Adjust radius", () => sphereSet.Strength = strength);
        }

        void colorCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            color = ColorComboBox.Value;

            SphereSet sphereSet = SelectedSphereSet;
            if (sphereSet != null)
                WriteBlock.ExecuteTask("Adjust color", () => sphereSet.Color = color);
        }

        void animateStepCommand_Execute(object sender, CommandExecutingEventArgs e) {
            animator.Advance(1);
        }

        void animatePlayCommand_Execute(object sender, CommandExecutingEventArgs e) {
            Command command = (Command)sender;

            //for (int i = 0; i < 33; i++)
            //    animator.Advance(1);

            if (Animation.IsAnimating)
                Animation.IsPaused = !Animation.IsPaused; // toggle Play/Pause
            else
                Animation.Start(Resources.DistributeSpheresToolAnimationPlay, animator);

            command.IsChecked = Animation.IsAnimating;
            command.Text = Animation.IsAnimating ? Resources.DistributeSpheresToolAnimationPause : Resources.DistributeSpheresToolAnimationPlay;
        }

        void radiusSliderCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            radius = RadiusSlider.Value;

            SphereSet sphereSet = SelectedSphereSet;
            if (sphereSet != null)
                WriteBlock.ExecuteTask("Adjust radius", () => sphereSet.Radius = radius);
        }

        void createSphereCommand_Execute(object sender, CommandExecutingEventArgs e) {
            SphereSet sphereSet = SelectedSphereSet;
            if (sphereSet == null)
                return;

            Part sphereRootPart = Part.Create(Window.Document, "Spheres");
            Component.Create(Window.ActiveWindow.Scene as Part, sphereRootPart);
            Part innerSpherePart = Part.Create(Window.Document, "Inner Spheres");
            Part outerSpherePart = Part.Create(Window.Document, "Outer Spheres");
            Component.Create(sphereRootPart, innerSpherePart);
            Component.Create(sphereRootPart, outerSpherePart);

            Part spherePart = Part.Create(Window.Document, "Sphere");
            ShapeHelper.CreateSphere(Point.Origin, sphereSet.Radius * 2, spherePart);

            Body body = sphereSet.DesFace.Shape.Body;
            foreach (Point point in sphereSet.Positions) {
                bool isEdge = false;
                foreach (Edge edge in body.Edges.Where(edge => edge.Faces.Count == 1)) {
                    if ((edge.ProjectPoint(point).Point - point).MagnitudeSquared() < (sphereSet.Radius * sphereSet.Radius)) {
                        isEdge = true;
                        break;
                    }
                }

                Component component = Component.Create(isEdge ? outerSpherePart : innerSpherePart, spherePart);
                component.Placement = Matrix.CreateTranslation(point.Vector);
            }
        }

        void Window_PreselectionChanged(object sender, EventArgs e) {
            if (IsDragging)
                return;

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
            DesignFace desFace = null;

            SphereSet existingSphereSet = SphereSet.GetWrapper(preselection as CustomObject);
            if (existingSphereSet != null)
                desFace = existingSphereSet.DesFace;
            if (desFace == null)
                desFace = preselection as DesignFace;
            if (desFace == null) // selection filtering is not applied if you (pre)select in the tree
                return false;

            if (SphereSet.AllSphereSets.Where(s => s.DesFace == desFace).Count() > 0)
                return false;

            Body body = desFace.Shape.Body;

            GraphicStyle style = null;
            if (existingSphereSet == null) {
                style = new GraphicStyle {
                    EnableDepthBuffer = true,
                    FillColor = color
                };

                Rendering = Graphic.Create(style, null, SphereSet.GetGraphics(SphereSet.GetInitialPositions(body, count, true), radius));
            }

            return false; // if we return true, the preselection won't update
        }

        #region Click-Click Notifications

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            IDocObject selection = null;
            IDocObject preselection = InteractionContext.Preselection;
            var desFace = preselection as DesignFace;
            if (desFace != null)
                WriteBlock.ExecuteTask("Create Sphere Set", () => selection = SphereSet.Create(desFace, count, strength, radius, color).Subject);
            else {
                SphereSet sphereSet = SphereSet.GetWrapper(preselection as CustomObject);
                if (sphereSet != null) {
                    selection = sphereSet.Subject;

                    CountSlider.Value = sphereSet.Count;
                    StrengthSlider.Value = sphereSet.Strength;
                    ColorComboBox.Value = sphereSet.Color;
                    RadiusSlider.Value = sphereSet.Radius;
                }
            }

            Window.ActiveContext.Selection = new[] { selection };
            return false;
        }

        #endregion

        public static SphereSet SelectedSphereSet {
            get {
                IDocObject docObject = Window.ActiveWindow.ActiveContext.SingleSelection;
                if (docObject == null)
                    return null;

                return SphereSet.GetWrapper(docObject as CustomObject);
            }
        }

    }

    class SphereSetAnimator : Animator {
        public SphereSetAnimator() {
        }

        public override int Advance(int frame) {
            Debug.Assert(frame >= 1);

            SphereSet sphereSet = DistributeSpheresTool.SelectedSphereSet;
            if (sphereSet == null)
                return frame;

            WriteBlock.ExecuteTask("Animate Spheres", () => CalculateFrame(sphereSet));

            return frame + 2;
        }

        private static void CalculateFrame(SphereSet sphereSet) {
            Point[] positions = sphereSet.Positions;
            Point[] newPositions = new Point[positions.Length];
            Vector[] forces = new Vector[positions.Length];
            Body body = sphereSet.DesFace.Shape.Body;
            Random random = new Random();
            double idealRadius = sphereSet.IdealRadius;
            double idealRadiusComped = idealRadius * Math.Sqrt(sphereSet.Count);
            double minRadiusFactor = 0.05;

            double scale = 1E-3 * idealRadius;
            double maxCalc = 16 * idealRadius * idealRadius;

            for (int i = 0; i < positions.Length; i++) {
                for (int j = 0; j < i; j++) {
                    Vector separation = positions[i] - positions[j];
                    double distanceSquared = separation.MagnitudeSquared();
                    if (distanceSquared > maxCalc)
                        continue;

                    double x = distanceSquared / (idealRadiusComped * idealRadius);

                    Vector force;
                    if (x > minRadiusFactor)
                        force = separation.Direction * ((double)1 / (x * x) - (double)1 / x);
                    else
                        force = Math.Sqrt(sphereSet.Count) / minRadiusFactor * 4 * Vector.Create(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);

                    forces[i] += force;
                    forces[j] -= force;
                }
            }

            Face face;
            for (int i = 0; i < positions.Length; i++)
                newPositions[i] = body.ProjectPointToShell(positions[i] + scale * forces[i], out face).Point;

            sphereSet.Positions = newPositions;
        }

        protected override void OnCompleted(AnimationCompletedEventArgs args) {
            if (args.Result == AnimationResult.Canceled) {
                UndoStepAdded = false;
            }

            base.OnCompleted(args);
        }

        public bool UndoStepAdded { get; private set; }
    }

    public class SphereSet : CustomWrapper<SphereSet> {
        readonly DesignFace desFace;
        int count;
        Color color;
        double strength;
        double radius;
        Point[] positions;

        const double tau = Math.PI * 2;
        static List<SphereSet> allSphereSets = new List<SphereSet>();
        static List<Facet> facets = new List<Facet>();
        const int sphereFacets = 24;
        static FacetVertex[] facetVertices = new FacetVertex[sphereFacets * (sphereFacets / 2 + 1)];

        // creates a wrapper for an existing custom object
        protected SphereSet(CustomObject subject)
            : base(subject) {
            allSphereSets.Add(this);
        }

        // creates a new custom object and a wrapper for it
        protected SphereSet(DesignFace desFace, int count, double strength, double radius, Color color)
            : base(desFace.GetAncestor<Part>()) {
            this.desFace = desFace;
            this.count = count;
            this.strength = strength;
            this.radius = radius;
            this.color = color;
            this.positions = GetInitialPositions(desFace.Shape.Body, count, true);

            desFace.KeepAlive(true);
            allSphereSets.Add(this);
        }

        ~SphereSet() {
            allSphereSets.Remove(this);
        }

        // static Create method follows the API convention and parent should be first argument
        public static SphereSet Create(DesignFace desFace, int count, double strength, double radius, Color color) {
            Debug.Assert(desFace != null);

            var sphereSet = new SphereSet(desFace, count, strength, radius, color);
            sphereSet.Initialize();
            return sphereSet;
        }

        public static void InitializeGraphics() {
            Point unit = Point.Origin + Direction.DirZ.UnitVector;
            int jMax = sphereFacets / 2 + 1;

            for (int i = 0; i < sphereFacets; i++) {
                for (int j = 0; j < jMax; j++) {
                    Point point =
                        Matrix.CreateRotation(Frame.World.AxisZ, (double)i / sphereFacets * tau) *
                        Matrix.CreateRotation(Frame.World.AxisY, (double)j / sphereFacets * tau) *
                        unit;

                    facetVertices[i * jMax + j] = new FacetVertex(point, point.Vector.Direction);
                }
            }

            for (int i = 0; i < sphereFacets; i++) {
                for (int j = 0; j < jMax - 1; j++) {
                    int ii = (i + 1) % sphereFacets;
                    int jj = j + 1;

                    facets.Add(new Facet(
                        i * jMax + j,
                        ii * jMax + j,
                        ii * jMax + jj
                    ));
                    facets.Add(new Facet(
                        i * jMax + j,
                        i * jMax + jj,
                        ii * jMax + jj
                    ));

                }
            }
        }

        public static Point[] GetInitialPositions(Body body, int count, bool useSeed) {
            Point[] positions = new Point[count];

            Box box = body.GetBoundingBox(Matrix.Identity);
            Point start = box.MinCorner;
            Vector span = box.MaxCorner - start;
            start = start - span / 2;
            span *= 2;

            Random random = useSeed ? new Random(body.GetHashCode()) : new Random();
            int i = 0;

            var faceAreas = new Dictionary<double, Face>();
            double totalArea = body.SurfaceArea;
            double runningArea = 0;
            foreach (Face face in body.Faces) {
                runningArea += face.Area;
                faceAreas[runningArea / totalArea] = face;
            }

            while (i < positions.Length) {
               // positions[i++] = body.ProjectPointToShell(start + Vector.Create(span.X * random.NextDouble(), span.Y * random.NextDouble(), span.Z * random.NextDouble()), out face).Point;

                Face face = GetRandomFaceByArea(faceAreas, body, random);
                PointUV param = PointUV.Create(face.BoxUV.RangeU.Start + random.NextDouble() * face.BoxUV.RangeU.Span, face.BoxUV.RangeV.Start + random.NextDouble() * face.BoxUV.RangeV.Span);

                if (!face.ContainsParam(param))
                    continue;

                positions[i] = face.Geometry.Evaluate(param).Point;
                i++;
            }


            return positions;
        }
        
        private static Face GetRandomFaceByArea(Dictionary<double, Face> faceAreas, Body body, Random random) {
            double faceChoice = random.NextDouble();
            double[] keys = faceAreas.Keys.ToArray();

            for (int i = 0; i < keys.Length; i++) {
                if (keys[i] >= faceChoice)
                    return faceAreas[keys[i]];
            }

            Debug.Fail("Could not find area for face");
            return null;
        }

#if false
        // if this returns true, the custom object can be used with the Move tool
        public override bool TryGetTransformFrame(out Frame frame, out Transformations transformations) {
            Face face = desFace.Shape;
            var plane = face.GetGeometry<Plane>();
            Point center = plane.ProjectPoint(face.GetBoundingBox(Matrix.Identity).Center).Point;
            Direction dirZ = plane.Frame.DirZ;
            if (face.IsReversed)
                dirZ = -dirZ;
            frame = placement * Frame.Create(center, dirZ);

            // only offer transformations in the plane
            transformations = Transformations.TranslateX | Transformations.TranslateY | Transformations.RotateZ;
            return true;
        }

        // the Move tool uses this
        public override void Transform(Matrix trans) {
            Face face = desFace.Shape;

            // only accept transformations in the plane
            var plane = face.GetGeometry<Plane>();
            if (plane.IsCoincident(trans * plane)) {
                placement = trans * placement;
                Commit();
            }
        }

        // the Move tool uses this if the Ctrl key is down
        public override SphereSet Copy() {
            SphereSet copy = Create(desFace, offset, color, count);
            copy.Placement = placement;
            return copy;
        }
#endif

        /*
         * Automatic update of a custom object happens in two stages.
         * 
         *  (1)	IsAlive is called to see if the custom object should continue to exist.
         *  
         *		If a custom object requires references to other doc objects for its definition,
         *		it can return false if any of these objects no longer exists.
         *		
         *		The data for a custom wrapper is stored in the custom object, and references to
         *		doc objects are stored as monikers.  When a custom wrapper is obtained, a moniker
         *		to a deleted object will resolve as a null reference.  If the custom wrapper already
         *		held the reference, and the doc object has since been deleted, then the reference
         *		will not be null, but IsDeleted will be true.  Therefore both cases must be checked.
         *		
         *		In our example, if the design face no longer exists, or the design face is no longer
         *		planar, then the custom object should be deleted.
         *		
         *		If the custom object does not depend on other objects for its continued existence,
         *		then there is no need to override IsAlive.  The default implementation returns true.
         *		
         *		In some cases, you might decide that rather than being deleted, the custom object
         *		should become 'invalid' and render itself so as to indicate this to the user.  You
         *		might provide a command to repair the custom object.  In this case, there is no need
         *		to override IsAlive, but you would override Update so that the Rendering indicates
         *		the invalid state, perhaps using a special color.
         *		
         *  (2)	Update is called to potentially update the custom object.
         *  
         *		A custom object needs updating if it has any information which is evaluated from
         *		its 'determinants', i.e. those objects on which its evaluated state depends.
         *		
         *		The Rendering is an example of evaluated data.
         *		
         *		You should call IsChanged with the determinants.  Determinants are often references
         *		to other doc objects, but they do not have to be.  Determinants can be obtained by
         *		traversals, e.g. the Parent might be a determinant.
         *		
         *		If IsChanged returns true, you should update evaluated data, e.g. the Rendering.
         *		You must not change the definition of the custom object during update; you can only
         *		changed evaluated data.  For example, you cannot call Commit during Update.
         *		
         *		The custom object itself is implicitly a determinant.  You do not need to supply
         *		'this' as one of the determinants with IsChanged.  This is useful, since if the rendering
         *		depends on data in the custom object itself (which is likely), the fact that the custom
         *		object was changed when Commit was called after the data was changed means that IsChanged
         *		will return true and you will then proceed to update the Rendering.
         *		
         *		Internally, the state of update of all the determinants, including the custom object itself,
         *		is recorded each time IsChanged is called.  Each time IsChanged is called, if this combined
         *		update state has changed, IsChanged returns true.  This can happen because objects have
         *		been modified, or an undo/redo has occurred, or a referenced document has been replaced.
         */

        protected override bool IsAlive {
            get {
                if (desFace == null || desFace.IsDeleted)
                    return false;

                return true;
            }
        }

        public new static string DefaultDisplayName {
            get { return "Profile"; }
        }

        public new static System.Drawing.Image[] ImageList {
            get { return new[] { Resources.DistributeSpheres32, Resources.DistributeSpheres32 }; }
        }

        protected override ICollection<IDocObject> Determinants {
            get { return new IDocObject[] { desFace }; }
        }

        protected override bool Update() {
#if false
			DisplayImage = new DisplayImage(0, 1);
			UpdateRendering(CancellationToken.None);
			return true; // update was done
#else
            return false; // update was not done - use async update
#endif
        }

        protected override void UpdateAsync(CancellationToken token) {
            DisplayImage = new DisplayImage(0, 1);
            UpdateRendering(token);
        }

        void UpdateRendering(CancellationToken token) {
            Body body = desFace.Shape.Body;
            Graphic nucleus = GetGraphics(positions, radius);
            Graphic shell = GetGraphics(positions, IdealRadius);
            GraphicStyle style;
            int shellAlpha = 22;

            Color selectedColor = Color.FromArgb(color.R / 2, color.G / 2, color.B / 2);
            Color prehighlightColor = Color.FromArgb(255 - (255 - color.R) / 2, 255 - (255 - color.G) / 2, 255 - (255 - color.B) / 2);
            Color shellColor = Color.FromArgb(shellAlpha, selectedColor);
            Color shellPrehighlightColor = Color.FromArgb(shellAlpha, prehighlightColor);

            // nucleus
            style = new GraphicStyle {
                IsPrimarySelection = false,
                IsPreselection = false,
                FillColor = color
            };
            Graphic visible = Graphic.Create(style, null, nucleus);

            style = new GraphicStyle {
                IsPrimarySelection = true,
                FillColor = selectedColor
            };
            Graphic selected = Graphic.Create(style, null, visible);

            style = new GraphicStyle {
                IsPreselection = true,
                IsPrimarySelection = false,
                FillColor = prehighlightColor
            };
            Graphic prehighlighted = Graphic.Create(style, null, selected);

            // shell
            style = new GraphicStyle {
                IsPrimarySelection = true,
                FillColor = shellColor
            };
            Graphic selectedShell = Graphic.Create(style, null, shell);

            style = new GraphicStyle {
                IsPreselection = true,
                IsPrimarySelection = false,
                FillColor = shellPrehighlightColor
            };
            Graphic prehighlightedShell = Graphic.Create(style, null, selectedShell);

            style = new GraphicStyle {
                EnableDepthBuffer = true
            };

            Rendering = Graphic.Create(style, null, new[] { prehighlighted, prehighlightedShell });

        }

        private static Graphic BlendGraphics(Color color, Graphic nucleus, Graphic shell, double shellAlpha) {
            GraphicStyle style;
            style = new GraphicStyle {
                FillColor = color
            };
            Graphic selectedNucleus = Graphic.Create(style, null, nucleus);

            style = new GraphicStyle {
                FillColor = Color.FromArgb(22, color)
            };
            Graphic selectedShell = Graphic.Create(style, null, shell);
            return Graphic.Create(null, null, new[] { selectedNucleus, selectedShell });
        }

        public static Graphic GetGraphics(Point[] positions, double radius) {
            var meshPrimitive = MeshPrimitive.Create(facetVertices, facets);

            var graphics = new Graphic[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                graphics[i] = Graphic.Create(null, new[] { meshPrimitive }, null,
                    Matrix.CreateTranslation(positions[i].Vector) *
                    Matrix.CreateScale(radius)
               );

            return Graphic.Create(null, null, graphics);
        }


        public static IList<SphereSet> AllSphereSets {
            get { return allSphereSets; }
        }

        public DesignFace DesFace {
            get { return desFace; }
        }

        public int Count {
            get { return count; }
            set {
                if (value == count)
                    return;

                if (value > count)
                    positions = positions.Concat(GetInitialPositions(desFace.Shape.Body, value - count, false)).ToArray();
                else
                    positions = positions.Take(value).ToArray();

                count = value;
                Commit();
            }
        }

        public double Strength {
            get { return strength; }
            set {
                if (value == strength)
                    return;

                strength = value;
                Commit();
            }
        }

        public double Radius {
            get { return radius; }
            set {
                if (Accuracy.EqualLengths(value, radius))
                    return;

                radius = value;
                Commit();
            }
        }

        public Color Color {
            get { return color; }
            set {
                if (value == color)
                    return;

                color = value;
                Commit();
            }
        }

        public double IdealRadius {
            //  get { return Math.Sqrt(desFace.Shape.Area / count) * Math.PI * 2; }
            //   get { return Math.Sqrt(desFace.Shape.Area / count / Math.PI) * 8; }

            get {
                double area = DesFace.Shape.Body.Faces.Sum(f => f.Area);
                double perimeter = DesFace.Shape.Body.Edges.Where(e => e.Faces.Count == 1).Sum(e => e.Length);
                double a = count - 1;
                double b = -perimeter / 4;
                double c = -area * 2 / 3 / Math.Sqrt(3);

                double rA = (-b + Math.Sqrt(b * b - 4 * a * c)) / 2 / a;
                double rB = (-b - Math.Sqrt(b * b - 4 * a * c)) / 2 / a;
                return Math.Max(rA, rB) * strength;
            }
        }

        public Point[] Positions {
            get { return positions; }
            set {
                positions = value;
                Commit();
            }
        }

    }

}
