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
using Utilities.Properties;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Utilities {
    class ThreadsGroupCapsule : RibbonGroupCapsule {
        public ThreadsGroupCapsule(string name, RibbonTabCapsule parent)
            : base(name, Resources.ThreadsGroupText, parent, LayoutOrientation.horizontal) {
            Values[Resources.Pitch] = new RibbonCommandValue(Settings.Default.Pitch);
            Values[Resources.Angle] = new RibbonCommandValue(Settings.Default.Angle);
            Booleans[Resources.IsImperial] = new RibbonCommandBoolean(Settings.Default.IsImperial);
            Booleans[Resources.IsInternal] = new RibbonCommandBoolean(Settings.Default.IsInternal);
        }
    }

    class ThreadButtonCapsule : RibbonButtonCapsule {
        public ThreadButtonCapsule(string name, RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base(name, Resources.CreateThreadsCommandText, Resources.Threads32, Resources.CreateThreadsCommandHint, parent, buttonSize) {
        }

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
            double lengthConversion = ActiveWindow.Units.Length.ConversionFactor;

            bool isImperial = Booleans[Resources.IsImperial].Value;
            bool isInternal = Booleans[Resources.IsInternal].Value;

            double pitch = isImperial ? (Const.inches / Values[Resources.Pitch].Value) : (1E-3 * Values[Resources.Pitch].Value);
            double angle = Values[Resources.Angle].Value * Math.PI / 180;


            Body[] bodies = SpaceClaimExtensions.GetAllSelectedIDesignFaces(Window.ActiveWindow)
                .Where(f => f.Shape.Geometry is Cylinder)
                .Select(f => f.Master.Parent.Shape.CopyFaces(new[] { f.Master.Shape }))
                .Select(b => b.Faces.First())
                .Select(f => CreateThreads(f, pitch, angle, isInternal))
                .ToArray();

            Part part = Window.ActiveWindow.Scene as Part;
            foreach (Body body in bodies)
                DesignBody.Create(part, Resources.ThreadStructureText, body);

            Settings.Default.IsImperial = isImperial;
            Settings.Default.IsInternal = isInternal;
            Settings.Default.Pitch = isImperial ? 0.0254 / pitch : pitch / 1E3;
            Settings.Default.Angle = angle * 180 / Math.PI;

            Settings.Default.Save();
        }

        private Body CreateThreads(Face cylinderFace, double pitch, double angle, bool isInternal) {
            double stitchTolerance = Accuracy.LinearResolution * 1E4;

            Cylinder cylinder = cylinderFace.Geometry as Cylinder;
            Debug.Assert(cylinder != null);

            double radius = cylinder.Radius;
            Line axis = cylinder.Axis;

            double[] points = cylinderFace.Loops
                .Where(l => l.IsOuter)
                .SelectMany(l => l
                    .Vertices.Select(v => v.Position)
                    .Select(p => axis.ProjectPoint(p).Param)
                ).ToArray();

            double start = points.Min();
            double end = points.Max();
            double length = end - start + pitch * 2;
            double threadDepth = pitch / (2 * Math.Tan(angle / 2));
            double innerRadius = radius - threadDepth;
            if (!isInternal) {
                innerRadius = radius;
                radius += threadDepth;
            }

            int pointsPerTurn = 360;
            int pointCount = (int)(length / pitch * pointsPerTurn);
            var outerPoints = new Point[pointCount];
            var innerPoints = new Point[pointCount];
            for (int i = 0; i < pointCount; i++) {
                double t = (double)i / pointCount;
                double rotation = Const.Tau * t * length / pitch;
                double depth = length * t;
                outerPoints[i] = Point.Create(radius * Math.Cos(rotation), radius * Math.Sin(rotation), depth);
                innerPoints[i] = Point.Create(innerRadius * Math.Cos(rotation), innerRadius * Math.Sin(rotation), depth);
            }

            var outerCurve = CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, outerPoints, Accuracy.LinearResolution * 1E1));
            var innerCurve = CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, innerPoints, Accuracy.LinearResolution * 1E1));

            var translation = Matrix.CreateTranslation(Direction.DirZ * pitch / 2);
            CurveSegment outerCurveA = outerCurve.CreateTransformedCopy(translation);
            CurveSegment outerCurveB = outerCurve.CreateTransformedCopy(translation.Inverse);
            Body loftBodyA = Body.LoftProfiles(new[] { new[] { outerCurveA }, new[] { innerCurve } }, false, false);
            Body loftBodyB = Body.LoftProfiles(new[] { new[] { innerCurve }, new[] { outerCurveB } }, false, false);
            loftBodyA.Stitch(new[] { loftBodyB }, stitchTolerance, null);
            loftBodyA.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, -pitch)));

            var capA = Body.ExtrudeProfile(Plane.PlaneXY, new[] { CurveSegment.Create(Circle.Create(Frame.World, radius * 2)) }, end - start);
            loftBodyA.Imprint(capA);

            loftBodyA.DeleteFaces(loftBodyA.Faces.Where(f => f.Edges.Where(e => e.Faces.Count == 1).Count() > 0).ToArray(), RepairAction.None);
            capA.DeleteFaces(capA.Faces.Where(f => f.GetBoundingBox(Matrix.Identity).Size.Magnitude > 4 * radius).ToArray(), RepairAction.None);

            loftBodyA.Stitch(new[] { capA }, stitchTolerance, null);

            var trans = Matrix.CreateMapping(Frame.Create(axis.Evaluate(start).Point, axis.Direction));
            Matrix.CreateTranslation(Vector.Create(0, 0, -pitch));
            loftBodyA.Transform(trans);

            return loftBodyA;
        }

    }


}
