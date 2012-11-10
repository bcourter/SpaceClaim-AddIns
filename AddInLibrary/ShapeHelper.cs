using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using Point = SpaceClaim.Api.V10.Geometry.Point;

/// <summary>
/// AddIn helper functions and other utilities
/// </summary>

namespace SpaceClaim.AddInLibrary {
	public static class ShapeHelper {
		// Creates a prism or planar surface from a list of points. 
		// The first three non-collinear points define the plane and direction; the remaining points are projected to the plane.  
		// If part is null, use the active part.

		public static IList<ITrimmedCurve> CreatePolygon(IList<Point> points) {
			var profile = new List<ITrimmedCurve>();
			for (int i = 0; i < points.Count; i++) {
				ITrimmedCurve iTrimmedCurve = null;
				if (i < points.Count - 1)
					iTrimmedCurve = CurveSegment.Create(points[i], points[i + 1]);
				else
					iTrimmedCurve = CurveSegment.Create(points[i], points[0]);

				if (iTrimmedCurve == null) // if points are the same, the curve is null
					continue;


				profile.Add(iTrimmedCurve);
			}

			if (profile.Count == 0)
				return null;	
	
			return profile;
		}

		public static IList<IDesignCurve> CreatePolygon(IList<Point> inputPoints, IPart part) {
			IList<ITrimmedCurve> iTrimmedCurves = CreatePolygon(inputPoints);
			if (iTrimmedCurves == null)
				return null;

			var designCurves = new List<IDesignCurve>();
			foreach (ITrimmedCurve iTrimmedCurve in iTrimmedCurves)
				designCurves.Add(DesignCurve.Create(part, iTrimmedCurve));

			return designCurves;
		}

		public static Body CreatePolygon(IList<Point> inputPoints, Plane plane, double thickness) {
			List<ITrimmedCurve> profile = new List<ITrimmedCurve>();
			if (plane == null)
				if (!AddInHelper.TryCreatePlaneFromPoints(inputPoints, out plane))
					return null;

			Point newPoint;
			Point lastPoint = inputPoints[inputPoints.Count - 1].ProjectToPlane(plane);
			List<Point> points = new List<Point>();

			foreach (Point point in inputPoints) {
				newPoint = point.ProjectToPlane(plane);
				if (!Accuracy.Equals(newPoint, lastPoint)) {
					points.Add(newPoint);
					lastPoint = newPoint;
				}
			}

			for (int i = 0; i < points.Count; i++) {
				if (i < points.Count - 1)
					profile.Add(CurveSegment.Create(points[i], points[i + 1]));
				else
					profile.Add(CurveSegment.Create(points[i], points[0]));
			}

			Body body = null;
			try {
				if (thickness == 0)
					body = Body.CreatePlanarBody(plane, profile);
				else
					body = Body.ExtrudeProfile(plane, profile, thickness);
			}
			catch {
				string error = "Exception thrown creating planar body:\n";
				foreach (Point point in inputPoints)
					error += string.Format("{0}, {1}, {2}\n", point.X, point.Y, point.Z);

				Debug.Assert(false, error);
			}

			if (body == null) {
				Debug.Fail("Profile was not connected, not closed, or not in order.");
				return null;
			}

			return body;
		}

		public static DesignBody CreatePolygon(IList<Point> inputPoints, Plane plane, double thickness, IPart part) {
			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Polygon", CreatePolygon(inputPoints, plane, thickness));
			desBodyMaster.Transform(part.TransformToMaster);  // TBD I should be doing this before we make it ??
			return desBodyMaster;
		}

		public static Body CreatePolygon(IList<Point> inputPoints, double thickness) {
			Plane plane;
			if (!AddInHelper.TryCreatePlaneFromPoints(inputPoints, out plane))
				//throw new ArgumentException("Points don't form plane");
				return null;

			return CreatePolygon(inputPoints, plane, thickness);
		}


		public static DesignBody CreatePolygon(IList<Point> inputPoints, double thickness, IPart part) {
			Plane plane;
			if (!AddInHelper.TryCreatePlaneFromPoints(inputPoints, out plane))
				throw new ArgumentException("Points don't form plane");

			return CreatePolygon(inputPoints, plane, thickness, part);
		}

		public static DesignBody CreateCircle(Frame frame, double diameter, IPart part) {
			Plane plane = Plane.Create(frame);

			List<ITrimmedCurve> profile = new List<ITrimmedCurve>();
			Circle circle = Circle.Create(frame, diameter / 2);
			profile.Add(CurveSegment.Create(circle));

			Body body = null;
			try {
				body = Body.CreatePlanarBody(plane, profile);
			}
			catch {
				Debug.Assert(false, "Exception thrown creating body");
			}

			if (body == null) {
				Debug.Fail("Profile was not connected, not closed, or not in order.");
				return null;
			}

			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Circle", body);
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static DesignBody CreateBlock(Point point1, Point point2, IPart part) {
			List<Point> points = new List<Point>();

			points.Add(point1);
			points.Add(Point.Create(point2.X, point1.Y, point1.Z));
			points.Add(Point.Create(point2.X, point2.Y, point1.Z));
			points.Add(Point.Create(point1.X, point2.Y, point1.Z));

			return CreatePolygon(
				points,
				Plane.Create(Frame.Create(point1, Direction.DirX, Direction.DirY)),
				point2.Z - point1.Z,
				part);
		}

		public static DesignBody CreateBlock(Box box, IPart part) {
			return CreateBlock(box.MinCorner, box.MaxCorner, part);
		}

		public static Body CreateCylinder(Point point1, Point point2, double diameter) {  // TBD Merge you lazy bum
			Vector heightVector = point2 - point1;
			Frame frame = Frame.Create(point1, heightVector.Direction);
			Plane plane = Plane.Create(frame);

			List<ITrimmedCurve> profile = new List<ITrimmedCurve>();
			profile.Add(CurveSegment.Create(Circle.Create(frame, diameter / 2)));

			Body body = null;
			try {
				body = Body.ExtrudeProfile(plane, profile, heightVector.Magnitude);
			}
			catch (Exception e) {
				Debug.WriteLine(e.Message);
			}

			if (body == null) {
				Debug.Fail("Profile was not connected, not closed, or not in order.");
				return null;
			}

			return body;
		}

		public static DesignBody CreateCylinder(Point point1, Point point2, double diameter, IPart part) {
			Body body = CreateCylinder(point1, point2, diameter);

			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Cylinder", body);
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static Body CreateCylindricalSurface(Plane startPlane, double radius, double height) {
			Body pitchCircleBody = Body.ExtrudeProfile(startPlane, new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(startPlane.Frame, radius)) }, height);
			foreach (Face face in pitchCircleBody.Faces) {
				if (face.Geometry is Plane)
					pitchCircleBody.DeleteFaces(new Face[] { face }, RepairAction.None);
			}
			return pitchCircleBody;
		}

		public static DesignBody CreateTorus(Point center, Direction axis, double minorDiameter, double majorDiameter, IPart part) {
			double radius = minorDiameter / 2;
			Direction dirX = axis.ArbitraryPerpendicular;
			Direction dirY = Direction.Cross(axis, dirX);

			Frame profileFrame = Frame.Create(center + dirX * majorDiameter / 2, dirX, axis);
			Circle sphereCircle = Circle.Create(profileFrame, radius);

			IList<ITrimmedCurve> profile = new List<ITrimmedCurve>();
			profile.Add(CurveSegment.Create(sphereCircle));

			IList<ITrimmedCurve> path = new List<ITrimmedCurve>();
			Circle sweepCircle = Circle.Create(Frame.Create(center, dirX, dirY), radius);
			path.Add(CurveSegment.Create(sweepCircle));

			Body body = Body.SweepProfile(Plane.Create(profileFrame), profile, path);
			if (body == null) {
				Debug.Fail("Sweep failed.");
				return null;
			}

			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Torus", body);
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static DesignBody CreateSausage(Point point1, Point point2, double diameter, IPart part) {
			double radius = diameter / 2;
			Vector lengthVector = point2.Vector - point1.Vector;
			Direction dirX = lengthVector.Direction;
			Direction dirY = dirX.ArbitraryPerpendicular;
			Direction dirZ = Direction.Cross(dirX, dirY);

			Frame profileFrame = Frame.Create(point1, dirX, dirY);
			Plane profilePlane = Plane.Create(profileFrame);

			IList<ITrimmedCurve> profile = new List<ITrimmedCurve>();

			Line axisLine = Line.Create(point1, dirX);
			profile.Add(CurveSegment.Create(axisLine, Interval.Create(-radius, lengthVector.Magnitude + radius)));

			Circle circle1 = Circle.Create(profileFrame, radius);
			profile.Add(CurveSegment.Create(circle1, Interval.Create(Math.PI / 2, Math.PI)));

			Line tangentLine = Line.Create(Matrix.CreateTranslation(dirY * radius) * point1, dirX);
			profile.Add(CurveSegment.Create(tangentLine, Interval.Create(0, lengthVector.Magnitude)));

			Circle circle2 = Circle.Create(Frame.Create(point2, dirX, dirY), radius);
			profile.Add(CurveSegment.Create(circle2, Interval.Create(0, Math.PI / 2)));

			IList<ITrimmedCurve> path = new List<ITrimmedCurve>();
			Circle sweepCircle = Circle.Create(Frame.Create(point1, dirY, dirZ), radius);
			path.Add(CurveSegment.Create(sweepCircle));

			Body body = Body.SweepProfile(Plane.Create(profileFrame), profile, path);
			if (body == null) {
				Debug.Fail("Profile was not connected, not closed, or swept along an inappropriate path.");
				return null;
			}

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Sausage", body);
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static Body CreateSphere(Point center, double diameter) {
			double radius = diameter / 2;
			Frame profileFrame = Frame.Create(center, Direction.DirX, Direction.DirY);
			Circle sphereCircle = Circle.Create(profileFrame, radius);
			Line sphereRevolveLine = Line.Create(center, Direction.DirX);
			IList<ITrimmedCurve> profile = new List<ITrimmedCurve>();
			profile.Add(CurveSegment.Create(sphereCircle, Interval.Create(0, Math.PI)));
			profile.Add(CurveSegment.Create(sphereRevolveLine, Interval.Create(-radius, radius)));

			IList<ITrimmedCurve> path = new List<ITrimmedCurve>();
			Circle sweepCircle = Circle.Create(Frame.Create(center, Direction.DirY, Direction.DirZ), radius);
			path.Add(CurveSegment.Create(sweepCircle));

			Body body = Body.SweepProfile(Plane.Create(profileFrame), profile, path);
			if (body == null) {
				Debug.Fail("Sweep failed.");
				return null;
			}

			return body;
		}

		public static DesignBody CreateSphere(Point center, double diameter, IPart part) {
			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Sphere", CreateSphere(center, diameter));
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static Body CreateCone(Point point1, Point point2, double diameter1, double diameter2, bool isSurface) {
			Vector heightVector = point2 - point1;
			Frame frame1 = Frame.Create(point1, heightVector.Direction);
			Frame frame2 = Frame.Create(point2, heightVector.Direction);

			var profiles = new List<ICollection<ITrimmedCurve>>();
			profiles.Add(new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(frame1, diameter1 / 2)) });
			profiles.Add(new ITrimmedCurve[] { CurveSegment.Create(Circle.Create(frame2, diameter2 / 2)) });

			Body body = null;
			try {
				body = Body.LoftProfiles(profiles, false, true);
			}
			catch (Exception e) {
				Debug.WriteLine(e.Message);
			}

			if (body == null) {
				Debug.Fail("Error creating loft for cone");
				return null;
			}

			if (isSurface)
				body.DeleteFaces(body.Faces.Where(f => f.Geometry is Plane).ToArray(), RepairAction.None);

			return body;
		}

		public static DesignBody CreateCone(IPart part, Point point1, Point point2, double diameter1, double diameter2, bool isSurface) {
			return CreateDesignBody(CreateCone(point1, point2, diameter1, diameter2, isSurface), "Cone", part);
		}


		// TBD move all this designbody nonsense to an abstract class for shape creation and get rid of all the copied code
		public static DesignBody CreateDesignBody(Body body, string name, IPart part) {
			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Sphere", body);
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static DesignBody CreateDesignBody(Body body, string name) {
			return CreateDesignBody(body, name, Window.ActiveWindow.ActiveContext.ActivePart);
		}

		public static DesignBody CreateCable(ITrimmedCurve iTrimmedCurve, double diameter, IPart part) {
			double radius = diameter / 2;
			CurveEvaluation curveEvaluation = iTrimmedCurve.Geometry.Evaluate(iTrimmedCurve.Bounds.Start);
			Point startPoint = curveEvaluation.Point;
			Direction dirZ = curveEvaluation.Tangent;
			Direction dirX = dirZ.ArbitraryPerpendicular;
			Direction dirY = Direction.Cross(dirZ, dirX);

			Frame profileFrame = Frame.Create(startPoint, dirX, dirY);
			Circle profileCircle = Circle.Create(profileFrame, radius);

			IList<ITrimmedCurve> profile = new List<ITrimmedCurve>();
			profile.Add(CurveSegment.Create(profileCircle));

			IList<ITrimmedCurve> path = new List<ITrimmedCurve>();
			path.Add(iTrimmedCurve);

			Body body = Body.SweepProfile(Plane.Create(profileFrame), profile, path);
			if (body == null) {
				Debug.Fail("Sweep failed.");
				return null;
			}

			if (part == null)
				part = Window.ActiveWindow.ActiveContext.ActivePart;

			DesignBody desBodyMaster = DesignBody.Create(part.Master, "Sweep", body);
			desBodyMaster.Transform(part.TransformToMaster);
			return desBodyMaster;
		}

		public static CurveSegment CreateHelixAroundCurve(this ITrimmedCurve curveSegment, double turns, double radius, double pointCount) {
			var points = new List<Point>();

			Direction lastNormal = curveSegment.Geometry.Evaluate(curveSegment.Bounds.Start).Tangent.ArbitraryPerpendicular;
			for (int i = 0; i < pointCount; i++) {
				double ratio = (double) i / pointCount;
				double param = curveSegment.Bounds.Start + ratio * curveSegment.Bounds.Span;

				CurveEvaluation curveEval = curveSegment.Geometry.Evaluate(param);
				Direction normal = Direction.Cross(Direction.Cross(curveEval.Tangent, lastNormal), curveEval.Tangent);
				if (normal.IsZero)
					normal = lastNormal;

				Point point = curveEval.Point;
				Matrix rotation = Matrix.CreateRotation(Line.Create(point, curveEval.Tangent), 2 * Math.PI * turns * ratio);
				point += radius * normal;
				point = rotation * point;

				points.Add(point);
				lastNormal = normal;
			}

			return CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, points, 0.000001));
		}

		public static ICollection<Primitive> CreateCylinderMesh(Point point1, Point point2, double diameter, int sides) {
			Vector heightVector = point2 - point1;

			Circle circle1 = Circle.Create(Frame.Create(point1, heightVector.Direction), diameter / 2);
			Circle circle2 = Circle.Create(Frame.Create(point2, heightVector.Direction), diameter / 2);

			var cylinderVertices = new List<FacetVertex>();
			var end1Vertices = new List<FacetVertex>();
			var end2Vertices = new List<FacetVertex>();

			var tempVertices = new List<Point>();  //TBD remove when we can do seletction on meshes

			double angle = 2 * Math.PI / (double)sides;
			for (int i = 0; i < sides; i++) {
				CurveEvaluation eval1 = circle1.Evaluate((double)i * angle);
				CurveEvaluation eval2 = circle2.Evaluate(((double)i + 0.5) * angle);

				cylinderVertices.Add(new FacetVertex(eval1.Point, (eval1.Point - circle1.Frame.Origin).Direction));
				cylinderVertices.Add(new FacetVertex(eval2.Point, (eval2.Point - circle2.Frame.Origin).Direction));

				end1Vertices.Add(new FacetVertex(eval1.Point, heightVector.Direction));
				end2Vertices.Add(new FacetVertex(eval2.Point, -heightVector.Direction));

				tempVertices.Add(eval1.Point);
				tempVertices.Add(eval2.Point);
			}

			var cylinderFacets = new List<Facet>();
			for (int i = 0; i < sides; i++) {
				int f00 = 2 * i;
				int f01 = f00 + 1;
				int f10 = f00 + 2 >= sides * 2 ? 0 : f00 + 2;
				int f11 = f10 + 1;

				cylinderFacets.Add(new Facet(f00, f10, f01));
				cylinderFacets.Add(new Facet(f10, f11, f01));
			}

			var end1Facets = new List<Facet>();
			var end2Facets = new List<Facet>();
			for (int i = 1; i < sides - 1; i++) {
				end1Facets.Add(new Facet(0, i + 1, i));
				end2Facets.Add(new Facet(0, i, i + 1));
			}

			var primitives = new List<Primitive>();
			primitives.Add(MeshPrimitive.Create(cylinderVertices, cylinderFacets));
			primitives.Add(MeshPrimitive.Create(end1Vertices, end1Facets));
			primitives.Add(MeshPrimitive.Create(end2Vertices, end2Facets));

			primitives.Add(PolylinePrimitive.Create(tempVertices)); //TBD remove

			return primitives;
		}

		public static Circle CreateCircleThroughPoints(Point p0, Point p1, Point p2) {
			Direction normal = Vector.Cross(p1 - p0, p2 - p0).Direction;
			return Circle.CreateThroughPoints(Plane.Create(Frame.Create(p0, normal)), p0, p1, p2);
		}


		public static Body CreateRevolvedCurve(Line axis, ITrimmedCurve curve) {
			Point point = curve.Geometry.Evaluate(curve.Bounds.Middle()).Point;
			Debug.Assert(Accuracy.LengthIsPositive((axis.ProjectPoint(point).Point - point).Magnitude));

			Plane plane = null;
			bool success = AddInHelper.TryCreatePlaneFromPoints(new Point[]{
				axis.Origin,
				axis.Evaluate(1).Point,
				point
			}, out plane);

			Debug.Assert(success, "Could not create plane through points.");

			Point axisStart = axis.ProjectPoint(curve.StartPoint).Point;
			Point axisEnd = axis.ProjectPoint(curve.EndPoint).Point;

			var profile = new List<ITrimmedCurve>();
			profile.Add(curve);

			if (axisStart != curve.StartPoint)
				profile.Add(CurveSegment.Create(axisStart, curve.StartPoint));

			if (axisEnd != curve.EndPoint)
				profile.Add(CurveSegment.Create(axisEnd, curve.EndPoint));

			profile.Add(CurveSegment.Create(axisStart, axisEnd));

			try {
				Body body = Body.SweepProfile(Plane.PlaneZX, profile, new ITrimmedCurve[] {
					CurveSegment.Create(Circle.Create(Frame.World, 1))
				});

				body.DeleteFaces(body.Faces.Where(f => f.Geometry is Plane).ToArray(), RepairAction.None);
				return body;
			}
			catch {
				return null;
			}
		}

     //   Graphics for a sphere

        //static List<Facet> facets = new List<Facet>();
        //const int sphereFacets = 24;
        //static FacetVertex[] facetVertices = new FacetVertex[sphereFacets * (sphereFacets / 2 + 1)];

        //public static void InitializeGraphics() {
        //    Point unit = Point.Origin + Direction.DirZ.UnitVector;
        //    int jMax = sphereFacets / 2 + 1;

        //    for (int i = 0; i < sphereFacets; i++) {
        //        for (int j = 0; j < jMax; j++) {
        //            Point point =
        //                Matrix.CreateRotation(Frame.World.AxisZ, (double)i / sphereFacets * tau) *
        //                Matrix.CreateRotation(Frame.World.AxisY, (double)j / sphereFacets * tau) *
        //                unit;

        //            facetVertices[i * jMax + j] = new FacetVertex(point, point.Vector.Direction);
        //        }
        //    }

        //    for (int i = 0; i < sphereFacets; i++) {
        //        for (int j = 0; j < jMax - 1; j++) {
        //            int ii = (i + 1) % sphereFacets;
        //            int jj = j + 1;

        //            facets.Add(new Facet(
        //                i * jMax + j,
        //                ii * jMax + j,
        //                ii * jMax + jj
        //            ));
        //            facets.Add(new Facet(
        //                i * jMax + j,
        //                i * jMax + jj,
        //                ii * jMax + jj
        //            ));

        //        }
        //    }
        //}

	}
}
