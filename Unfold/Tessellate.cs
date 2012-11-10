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
using SpaceClaim.Svg;
using Unfold.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Unfold {
    class TessellateButtonCapsule : RibbonButtonCapsule {
        public TessellateButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base("Tessellate", Resources.TessellateCommandText, Resources.Tessellate, Resources.TessellateCommandHint, parent, buttonSize) {

            Values[Resources.TesselateSurfaceDeviationText] = new RibbonCommandValue(Settings.Default.TessellateLinearDeviation);
            Values[Resources.TesselateAngleDeviationText] = new RibbonCommandValue(Settings.Default.TessellateAngularDeviation);
        }

        protected override void OnUpdate(Command command) {
            base.OnUpdate(command);

            command.IsEnabled = false;

            if (Window.ActiveWindow.ActiveContext.SingleSelection as IDesignBody != null) {
                command.IsEnabled = true;
                return;
            }

            if (Window.ActiveWindow.ActiveContext.GetSelection<IDesignFace>().Count > 0)
                command.IsEnabled = true;
        }

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
            double surfaceDeviation = Values[Resources.TesselateSurfaceDeviationText].Value / ActiveWindow.Units.Length.ConversionFactor;
            double angleDeviation = Values[Resources.TesselateAngleDeviationText].Value / ActiveWindow.Units.Angle.ConversionFactor;

            ICollection<Face> faces = null;
            IDesignBody iDesignBody = Window.ActiveWindow.ActiveContext.SingleSelection as IDesignBody;
            if (iDesignBody != null)
                faces = (iDesignBody.Master.Shape as Body).Faces;
            else {
                ICollection<IDesignFace> iDesignFaces = Window.ActiveWindow.ActiveContext.GetSelection<IDesignFace>();
                foreach (IDesignFace iDesignFace in iDesignFaces) {
                    iDesignBody = iDesignFace.GetAncestor<DesignBody>();
                    continue; // TBD: handle multiple selection of faces across different DesignBodies
                }

                if (iDesignBody != null) {
                    List<Face> modelerFaces = new List<Face>();
                    foreach (DesignFace designFace in iDesignFaces) {
                        if (designFace.GetAncestor<DesignBody>() == iDesignBody)
                            modelerFaces.Add(designFace.Shape as Face);
                    }
                    if (modelerFaces.Count > 0)
                        faces = modelerFaces;
                }
                else
                    return;
            }

            iDesignBody.SetVisibility(null, false);
            Body body = iDesignBody.Shape as Body;

            IDictionary<Face, FaceTessellation> tessellationMap = body.GetTessellation(faces, FacetSense.RightHanded, new TessellationOptions(surfaceDeviation, angleDeviation));

            Body testfacetBody = null;
            List<Body> facetBodies = new List<Body>();
            Point[] points = new Point[3];

            foreach (FaceTessellation faceTessellation in tessellationMap.Values) {
                IList<FacetVertex> vertices = faceTessellation.Vertices;
                foreach (FacetStrip facetStrip in faceTessellation.FacetStrips) {
                    foreach (Facet facet in facetStrip.Facets) {
                        points[0] = vertices[facet.Vertex0].Position;
                        points[1] = vertices[facet.Vertex1].Position;
                        points[2] = vertices[facet.Vertex2].Position;

                        testfacetBody = ShapeHelper.CreatePolygon(points, null, 0);
                        if (testfacetBody != null)
                            facetBodies.Add(testfacetBody);
                    }
                }
            }

            // when the topology has holes (e.g. an annulus made with one surface), non-manifold vertices can occur when merging triangles around the hole.  The workaround is to merge the fragment lumps. 
            facetBodies = new List<Body>(facetBodies.TryUnionBodies());
            facetBodies = new List<Body>(facetBodies.TryUnionBodies());
            foreach (Body facetBody in facetBodies)
                DesignBody.Create(iDesignBody.GetAncestor<Part>(), "Tesselation", facetBody);

        }
    }

    class TessellateLoftButtonCapsule : RibbonButtonCapsule {
        public TessellateLoftButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base("Loft", Resources.TessellateLoftCommandText, Resources.TessellateLoft, Resources.TessellateLoftCommandHint, parent, buttonSize) {

            Values[Resources.TessellateLoftStepSize] = new RibbonCommandValue(Settings.Default.TessellateLoftStepSize);
        }

        protected override void OnUpdate(Command command) {
            base.OnUpdate(command);

            command.IsEnabled = Window.ActiveWindow.GetAllSelectedITrimmedCurves().Count == 2;
        }

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
            if (ScenePart == null)
                return;

            double stepSize = Values[Resources.TessellateLoftStepSize].Value / ActiveWindow.Units.Length.ConversionFactor;


            ITrimmedCurve[] curves = Window.ActiveWindow.GetAllSelectedITrimmedCurves().ToArray();

            if (curves.Length != 2)
                Debug.Fail("Need exactly two curves");

            ITrimmedCurve curve0 = curves[0], curve1 = curves[1];

            //List<Point> points0 = curve0.Shape.GetPolyline() as List<Point>;
            //List<Point> points1 = curve1.Shape.GetPolyline() as List<Point>;
            //Debug.Assert (points0 != null && points1 != null);
            //if ((points1[0] - points0[0]).Magnitude > (points1[points1.Count - 1] - points0[0]).Magnitude)
            //    points1.Reverse();

            if ((curve1.StartPoint - curve0.StartPoint).Magnitude > (curve1.EndPoint - curve0.StartPoint).Magnitude)
                curve1 = CurveSegment.Create(curve1.GetGeometry<Curve>(), Interval.Create(curve1.Bounds.End, curve1.Bounds.Start));

            List<Point> points0 = curve0.TessellateCurve(stepSize) as List<Point>;
            List<Point> points1 = curve1.TessellateCurve(stepSize) as List<Point>;

            if ((points1[0] - points0[0]).Magnitude > (points1[points1.Count - 1] - points0[0]).Magnitude)
                points1.Reverse();

            //int steps = Math.Min(points0.Count, points1.Count);
            //for (int i = 0; i < steps; i++) {
            //    AddInHelper.CreateLines(new List<Point> { points0[i], points1[i] }, part);

            int basePoint0 = points0.Count - 1;
            int basePoint1 = points1.Count - 1;

            List<Body> bodies = new List<Body>();
            while (basePoint0 > 0 || basePoint1 > 0) {
                double base0Diagonal = double.MaxValue;
                double base1Diagonal = double.MaxValue;

                if (basePoint1 > 0)
                    base0Diagonal = (points1[basePoint1 - 1] - points0[basePoint0]).Magnitude;

                if (basePoint0 > 0)
                    base1Diagonal = (points0[basePoint0 - 1] - points1[basePoint1]).Magnitude;

                List<Point> facetPoints = new List<Point>();
                facetPoints.Add(points0[basePoint0]);
                facetPoints.Add(points1[basePoint1]);

                if (base0Diagonal < base1Diagonal)
                    facetPoints.Add(points1[--basePoint1]);
                else
                    facetPoints.Add(points0[--basePoint0]);

                Plane plane = null;
                if (AddInHelper.TryCreatePlaneFromPoints(facetPoints, out plane))
                    bodies.Add(ShapeHelper.CreatePolygon(facetPoints, plane, 0));
            }

            bodies = bodies.TryUnionBodies().ToList();
            Debug.Assert(bodies.Count == 1);

            DesignBody.Create(ScenePart, Resources.TessellateLoftBodyName, bodies[0]);
        }
    }

#if false // experimental
    class TessellateFoldCornerButtonCapsule : RibbonButtonCapsule {
        public TessellateFoldCornerButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base("FoldCorner", Resources.TessellateFoldCornerCommandText, null, Resources.TessellateFoldCornerCommandHint, parent, buttonSize) {
        }

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
            Part part = Window.ActiveWindow.Scene as Part;
            Debug.Assert(part != null);

            Double stepSize = 0.001;
            string tessellateLoftResolutionPropertyName = "Loft Tessellation Resolution";
            if (!part.Document.CustomProperties.ContainsKey(tessellateLoftResolutionPropertyName))
                CustomProperty.Create(part.Document, tessellateLoftResolutionPropertyName, stepSize);

            CustomProperty property;
            if (part.Document.CustomProperties.TryGetValue(tessellateLoftResolutionPropertyName, out property))
                stepSize = (double)property.Value;

            List<ITrimmedCurve> curves = new List<ITrimmedCurve>(Window.ActiveWindow.GetAllSelectedITrimmedCurves());
            if (curves.Count < 3)
                return;

            Point startPoint = curves[0].StartPoint;
            if (curves[1].StartPoint != startPoint) {
                if (curves[1].StartPoint != startPoint)
                    curves[0] = CurveSegment.Create(curves[0].GetGeometry<Curve>(), Interval.Create(curves[0].Bounds.End, curves[0].Bounds.Start)); // TBD Figure out why we can't call ReverseCurves and debug
                else
                    curves[1] = CurveSegment.Create(curves[1].GetGeometry<Curve>(), Interval.Create(curves[1].Bounds.End, curves[1].Bounds.Start));
            }

            for (int i = 2; i < curves.Count; i++) {
                if (curves[i].StartPoint != startPoint)
                    curves[i] = CurveSegment.Create(curves[i].GetGeometry<Curve>(), Interval.Create(curves[i].Bounds.End, curves[i].Bounds.Start));

                if (curves[i].StartPoint != startPoint)
                    return;
            }

            double endZ = double.NegativeInfinity;
            foreach (ITrimmedCurve curve in curves) {
                if (curve.EndPoint.Z > endZ)
                    endZ = curve.EndPoint.Z;
            }

            Plane startPlane = Plane.Create(Frame.Create(startPoint, Direction.DirX, Direction.DirY));
            double cuttingZ = startPoint.Z;
            List<List<Point>> curveSteps = new List<List<Point>>();
            List<List<Point>> insetSteps = new List<List<Point>>();
            while (true) {
                cuttingZ -= stepSize;
                if (cuttingZ < endZ)
                    break;

                Plane cuttingPlane = Plane.Create(Frame.Create(Point.Create(startPoint.X, startPoint.Y, cuttingZ), Direction.DirX, Direction.DirY));
                List<Point> curvePoints = new List<Point>();
                List<Point> planarPoints = new List<Point>();
                foreach (ITrimmedCurve curve in curves) {
                    ICollection<IntPoint<SurfaceEvaluation, CurveEvaluation>> surfaceIntersections = cuttingPlane.IntersectCurve(curve.GetGeometry<Curve>());
                    foreach (IntPoint<SurfaceEvaluation, CurveEvaluation> surfaceIntersection in surfaceIntersections) {
                        Point point = surfaceIntersection.Point;
                        curvePoints.Add(point);

                        Point projectedPoint = startPlane.ProjectPoint(point).Point;
                        Direction direction = (projectedPoint - startPoint).Direction;
                        double length = CurveSegment.Create(curve.GetGeometry<Curve>(), Interval.Create(curve.Bounds.Start, surfaceIntersection.EvaluationB.Param)).Length;
                        planarPoints.Add(startPoint + direction * length);

                        break; // assume one intersection
                    }
                }

                List<Point> insetPoints = new List<Point>();
                for (int i = 0; i < planarPoints.Count; i++) {
                    int ii = i == planarPoints.Count - 1 ? 0 : i + 1;

                    ICollection<Point> pointCandidates = AddInHelper.IntersectSpheres(new Sphere[]{
			                        Sphere.Create(Frame.Create(startPoint, Direction.DirX, Direction.DirY), ((startPoint - curvePoints[i]).Magnitude + (startPoint - curvePoints[ii]).Magnitude) / 2),
			                        Sphere.Create(Frame.Create(curvePoints[i], Direction.DirX, Direction.DirY), (startPoint - curvePoints[i]).Magnitude),
			                        Sphere.Create(Frame.Create(curvePoints[ii], Direction.DirX, Direction.DirY), (startPoint - curvePoints[ii]).Magnitude)
			                    });

                    Point planarMidPoint = Point.Origin + (planarPoints[i] + planarPoints[ii].Vector).Vector / 2;
                    Point insetPoint;
                    foreach (Point point in pointCandidates) {
                        Point testPoint = startPlane.ProjectPoint(point).Point;

                        if ((testPoint - planarMidPoint).Magnitude < (insetPoint - planarMidPoint).Magnitude)
                            insetPoint = point;
                    }

                    insetPoints.Add(insetPoint);
                }

                curveSteps.Add(curvePoints);
                insetSteps.Add(insetPoints);
            }

            for (int i = 0; i < curveSteps.Count - 1; i++) {
                for (int j = 0; j < curveSteps[i].Count; j++) {
                    int jj = j == curveSteps[i].Count - 1 ? 0 : j + 1;

                    ShapeHelper.CreatePolygon(new Point[] { curveSteps[i][j], curveSteps[i + 1][j], insetSteps[i][j] }, 0, null);
                    ShapeHelper.CreatePolygon(new Point[] { curveSteps[i + 1][j], insetSteps[i][j], insetSteps[i + 1][j] }, 0, null);
                    ShapeHelper.CreatePolygon(new Point[] { insetSteps[i][j], insetSteps[i + 1][j], curveSteps[i][jj] }, 0, null);
                    ShapeHelper.CreatePolygon(new Point[] { insetSteps[i + 1][j], curveSteps[i][jj], curveSteps[i + 1][jj] }, 0, null);
                }
            }

            return;
        }
    }
#endif
}
