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
    public static class GeometryExtensions {
        public static Point GetPoint(this Vector vector) {
            return Point.Origin + vector;
        }

        public static Point Average(this IEnumerable<Point> points) {
            Vector averageVector = Vector.Zero;
            foreach (Point point in points)
                averageVector += point.Vector;

            return Point.Origin + averageVector / points.Count();
        }

        public static double Dot(this Direction a, Direction b) {
            if (a.IsZero || b.IsZero)
                return 0;

            return Vector.Dot(a.UnitVector, b.UnitVector);
        }

        public static double Middle(this Interval interval) {
            return interval.Start + interval.Span / 2;
        }

        public static double AngleInXY(this Vector vector) {
            return Math.Atan2(vector.Y, vector.X);
        }

        public static double AngleInFrame(this Vector vector, Frame frame) {
            double x = Vector.Dot(vector, frame.DirX.UnitVector);
            double y = Vector.Dot(vector, frame.DirY.UnitVector);
            return Math.Atan2(y, x);
        }

        public static Vertex TrueStartVertex(this Fin fin) {
            if (!(fin.IsReversed ^ fin.Edge.IsReversed))
                return fin.Edge.StartVertex;
            else
                return fin.Edge.EndVertex;
        }

        public static Vertex TrueEndVertex(this Fin fin) {
            if (!(fin.IsReversed ^ fin.Edge.IsReversed))
                return fin.Edge.EndVertex;
            else
                return fin.Edge.StartVertex;
        }

        public static Point TrueStartPoint(this Fin fin) {
            return fin.TrueStartVertex().Position;
        }

        public static Point TrueEndPoint(this Fin fin) {
            return fin.TrueEndVertex().Position;
        }

        public static FacetVertex Transform(this FacetVertex facetVertex, Matrix matrix) {
            return new FacetVertex(
                matrix * facetVertex.Position,
                matrix.Rotation * facetVertex.Normal
            );
        }

        public static double TrueStartParam(this Fin fin) {
            if (fin.IsReversed ^ fin.Edge.IsReversed)
                return fin.Edge.Bounds.Start;
            else
                return fin.Edge.Bounds.End;
        }

        public static double TrueEndParam(this Fin fin) {
            if (!(fin.IsReversed ^ fin.Edge.IsReversed))
                return fin.Edge.Bounds.Start;
            else
                return fin.Edge.Bounds.End;
        }

        public static CurveSegment AsCurveSegment(this Point point) {
            return CurveSegment.Create(PointCurve.Create(point));
        }

        public static double Volume(this Box box) {
            Vector size = box.Size;
            return size.X * size.Y * size.Z;
        }

#if false
        public static Point ProjectToPlane(this Point point, Plane plane) {
            Direction normal = plane.Frame.DirZ;
            Vector pointVector = point.Vector;
            Vector vector = Vector.Dot(plane.Frame.Origin.Vector - pointVector, normal.UnitVector) * normal;
            pointVector += vector;

            return Point.Origin + pointVector;
        }
#else
        public static Point ProjectToPlane(this Point point, Plane plane) {
            return plane.ProjectPoint(point).Point;
        }
#endif

        public static SurfaceEvaluation ProjectPointToShell(this Body body, Point point, out Face face) {
            Debug.Assert(body != null);
            face = null;

            double leastDistSquared = double.MaxValue;
            SurfaceEvaluation leastEval = null;
            SurfaceEvaluation eval = null;
            foreach (Face testFace in body.Faces) {
                eval = testFace.ProjectPoint(point);
                double distSquared = (eval.Point - point).MagnitudeSquared();
                if (distSquared == Math.Min(leastDistSquared, distSquared)) {
                    face = testFace;
                    leastEval = eval;
                    leastDistSquared = distSquared;
                }
            }

            Debug.Assert(face != null);
            Debug.Assert(leastEval != null);
            return leastEval;
        }

        public static IList<Point> GetRectanglePointsAround(this Point point, Vector v1, Vector v2) {
            v1 /= 2;
            v2 /= 2;
            return new Point[] {
				point + v1 + v2,
				point - v1 + v2,
				point - v1 - v2,
				point + v1 - v2
			};
        }

        public static Line IntersectPlanes(this Plane plane0, Plane plane1) {
            Direction dir = Direction.Cross(plane0.Frame.DirZ, plane1.Frame.DirZ);
            ICollection<IntPoint<SurfaceEvaluation, CurveEvaluation>> intersections = plane1.IntersectCurve(Line.Create(plane0.Frame.Origin, Direction.Cross(dir, plane0.Frame.DirZ)));

            if (intersections.Count == 0)
                return null;

            return Line.Create(intersections.ToArray()[0].Point, dir);
        }

        public static IList<ITrimmedCurve> CreatePolyline(this IList<Point> points) {
            Debug.Assert(points != null, "points != null");
            Debug.Assert(points.Count >= 2, "points.Count >= 2");

            var lineSegments = new List<ITrimmedCurve>();
            for (int i = 1; i < points.Count; i++) {
                if (points[i - 1] == points[i])
                    continue;

                lineSegments.Add(CurveSegment.Create(points[i - 1], points[i]));
            }
            return lineSegments;
        }

        public static IList<Point> RemoveAdjacentDuplicates(this IList<Point> points) {
            Debug.Assert(points != null, "points != null");
            Debug.Assert(points.Count > 1, "points.Count > 1");

            var newPoints = new List<Point>();
            newPoints.Add(points[0]);
            for (int i = 1; i < points.Count; i++) {
                if (newPoints.Last() != points[i])
                    newPoints.Add(points[i]);
            }

            return newPoints;
        }

        public static double MagnitudeSquared(this Vector vector) {
            return vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z;
        }

        public static ICollection<ITrimmedCurve> GetProfile(this IList<Point> points) {
            var iTrimmedCurves = new List<ITrimmedCurve>();
            for (int i = 0; i < points.Count; i++)
                iTrimmedCurves.Add(CurveSegment.Create(points[i], points[i == points.Count - 1 ? 0 : i + 1]));

            return iTrimmedCurves;
        }

        public static ICollection<ITrimmedCurve> OffsetTowards(this ICollection<ITrimmedCurve> curves, Point point, Plane plane, double distance) {
            ITrimmedCurve firstCurve = curves.First();
            ITrimmedCurve[] otherCurves = curves.Skip(1).ToArray();

            bool isLeft = Vector.Dot(Vector.Cross(firstCurve.Geometry.Evaluate(0).Tangent.UnitVector, point - firstCurve.StartPoint), plane.Frame.DirZ.UnitVector) >= 0;
            return firstCurve.OffsetChain(plane, (isLeft ? -1 : 1) * distance, otherCurves, OffsetCornerType.NaturalExtension);
        }

        public static List<Point> CleanProfile(this List<Point> profile, double tolerance) {  //TBD this may not work correctly
            tolerance *= tolerance;
            var cleanProfile = new List<Point>();

            double segmentLength;
            for (int i = 0; i < profile.Count - 1; i++) {
                segmentLength = (profile[i] - profile[i + 1]).MagnitudeSquared();
                if (segmentLength > tolerance)
                    cleanProfile.Add(profile[i]);
                else {
                    cleanProfile.Add(new Point[] {
						profile[i], 
						profile[i + 1]
					}.Average());

                    i++;
                }
            }

            segmentLength = (cleanProfile[0] - profile.Last()).MagnitudeSquared();
            if (segmentLength > tolerance)
                cleanProfile.Add(profile.Last());
            else {
                cleanProfile.Add(new Point[] {
					profile.Last(), 
					cleanProfile[0]
				}.Average());

                cleanProfile.RemoveAt(0);
            }

            return cleanProfile;
        }

        public static ICollection<IList<ITrimmedCurve>> ExtractChains(this ICollection<ITrimmedCurve> curveSegments) {
            var profiles = new List<List<ITrimmedCurve>>();
            var unsortedCurves = new Queue<ITrimmedCurve>(curveSegments);

            while (unsortedCurves.Count > 0) {
                var profile = new List<ITrimmedCurve>();
                profile.Add(unsortedCurves.Dequeue());
                Point chainStart = profile[0].StartPoint;
                Point chainEnd = profile[0].EndPoint;

                int counter = unsortedCurves.Count;
                while (chainStart != chainEnd && counter-- > 0) {
                    ITrimmedCurve candidate = unsortedCurves.Dequeue();

                    if (candidate.StartPoint == chainEnd) {
                        profile.Add(candidate);
                        chainEnd = candidate.EndPoint;
                        counter = unsortedCurves.Count;
                        continue;
                    }

                    if (candidate.EndPoint == chainEnd) {
                        profile.Add(candidate.GetReverse());
                        chainEnd = candidate.StartPoint;
                        counter = unsortedCurves.Count;
                        continue;
                    }

                    if (candidate.EndPoint == chainStart) {
                        profile.Insert(0, candidate);
                        chainStart = candidate.StartPoint;
                        counter = unsortedCurves.Count;
                        continue;
                    }

                    if (candidate.StartPoint == chainStart) {
                        profile.Insert(0, candidate.GetReverse());
                        chainStart = candidate.EndPoint;
                        counter = unsortedCurves.Count;
                        continue;
                    }

                    unsortedCurves.Enqueue(candidate);
                }

                if (profile.Count > 0) {
                    profiles.Add(profile);
                    profile = new List<ITrimmedCurve>();
                }
            }

            return profiles.Cast<IList<ITrimmedCurve>>().ToArray();
        }

        public static List<List<Point>> CloseProfiles(this List<List<Point>> profiles) {
            var closedProfiles = new List<List<Point>>();

            while (profiles.Count > 0) {
                double minSeparation = double.MaxValue;
                int startProfile = -1, endProfile = -1;
                bool isStartProfileReversed = false, isEndProfileReversed = false;
                for (int i = 0; i < profiles.Count; i++) {
                    double separation = (profiles[i][0] - profiles[i].Last()).MagnitudeSquared();
                    if (separation < minSeparation)
                        startProfile = endProfile = i;

                    for (int j = 0; j < i; j++) {
                        //if (i == j)
                        //    continue;

                        //double separation = (profiles[j][0] - profiles[j].Last()).MagnitudeSquared();
                        //if (separation < minSeparation)
                        //    startProfile = endProfile = j;

                        separation = (profiles[i].Last() - profiles[j][0]).MagnitudeSquared();
                        if (separation < minSeparation) {
                            minSeparation = separation;
                            startProfile = i;
                            endProfile = j;
                            isStartProfileReversed = false;
                            isEndProfileReversed = false;
                        }

                        separation = (profiles[i][0] - profiles[j].Last()).MagnitudeSquared();
                        if (separation < minSeparation) {
                            minSeparation = separation;
                            startProfile = i;
                            endProfile = j;
                            isStartProfileReversed = true;
                            isEndProfileReversed = true;
                        }

                        separation = (profiles[i][0] - profiles[j][0]).MagnitudeSquared();
                        if (separation < minSeparation) {
                            minSeparation = separation;
                            startProfile = i;
                            endProfile = j;
                            isStartProfileReversed = true;
                            isEndProfileReversed = false;
                        }

                        separation = (profiles[i].Last() - profiles[j].Last()).MagnitudeSquared();
                        if (separation < minSeparation) {
                            minSeparation = separation;
                            startProfile = i;
                            endProfile = j;
                            isStartProfileReversed = false;
                            isEndProfileReversed = true;
                        }
                    }
                }

                if (startProfile == endProfile) {
                    //	profiles[startProfile].Add(profiles[startProfile][0]);
                    closedProfiles.Add(profiles[startProfile]);
                    profiles.RemoveAt(startProfile);
                    continue;
                }

                if (isStartProfileReversed)
                    profiles[startProfile].Reverse();

                if (isEndProfileReversed)
                    profiles[endProfile].Reverse();

                profiles[startProfile].AddRange(profiles[endProfile]);
                profiles.RemoveAt(endProfile);
            }

            return closedProfiles;
        }

        public static ICollection<ITrimmedCurve> OffsetAllLoops(this ICollection<ITrimmedCurve> curves, Plane plane, double distance, OffsetCornerType offsetCornerType) {
            Body body = Body.CreatePlanarBody(plane, curves);
            Debug.Assert(body.Faces.Count == 1);
            Face face = body.Faces.First();

            return face.OffsetAllLoops(distance, offsetCornerType);
        }

        public static ICollection<ITrimmedCurve> OffsetAllLoops(this Face face, double distance, OffsetCornerType offsetCornerType) {
            Plane plane;

            ITrimmedCurve[] outerLoop = null;
            var innerLoops = new List<ITrimmedCurve[]>();

            plane = face.Geometry as Plane;
            if (plane == null)
                throw new NotImplementedException();

            Debug.Assert(face.Loops.Where(l => l.IsOuter).Count() == 1, "Multiple outer loops not implemented");

            foreach (Loop loop in face.Loops) {
                if (loop.IsOuter)
                    outerLoop = loop.Edges.ToArray();
                else
                    innerLoops.Add(loop.Edges.ToArray());
            }

            if (innerLoops.Count == 0)
                return outerLoop.OffsetChainInward(plane, distance, offsetCornerType);

            throw new NotImplementedException();

            // DTR throws exception in SpaceClaim
            return DoInWriteBlock<ICollection<ITrimmedCurve>>(() => OffsetChainInwardWithInnerLoops(face, distance, offsetCornerType, outerLoop, innerLoops));
        }

        private static T DoInWriteBlock<T>(Func<T> func) {
            if (WriteBlock.IsActive)
                return func();
            else {
                T result = default(T);
                WriteBlock.ExecuteTask("API busywork", () => { result = func(); });
                return result;
            }
        }

        private static ICollection<ITrimmedCurve> OffsetChainInwardWithInnerLoops(Face face, double distance, OffsetCornerType offsetCornerType, ITrimmedCurve[] outerLoop, List<ITrimmedCurve[]> innerLoops) {
            if (!(face.Geometry is Plane))
                throw new ArgumentException("Face must be planar");

            Plane plane = (Plane)face.Geometry; 
            
            Body outerBody = null;
            try {
                outerBody = Body.CreatePlanarBody(plane, outerLoop.OffsetChainInward(plane, distance, offsetCornerType));
            }
            catch {
                return null;
            }

            Body[] innerBodies = innerLoops.Select(l => Body.CreatePlanarBody(plane, outerLoop.OffsetChainInward(plane, -distance, offsetCornerType))).ToArray();
            try {
                outerBody.Subtract(innerBodies);
            }
            catch {
                return null;
            }

            if (outerBody == null)
                return null;

            return outerBody.Edges.Cast<ITrimmedCurve>().ToArray();
        }

#if true
        public static ICollection<ITrimmedCurve> OffsetChainInward(this ICollection<ITrimmedCurve> curves, Plane plane, double distance, OffsetCornerType offsetCornerType) {
            ITrimmedCurve firstEdge = curves.First();
            ITrimmedCurve[] otherEdges = curves.Skip(1).ToArray();

            ICollection<ITrimmedCurve> offsetCurvesA = firstEdge.OffsetChain(plane, distance, otherEdges, offsetCornerType);
            ICollection<ITrimmedCurve> offsetCurvesB = firstEdge.OffsetChain(plane, -distance, otherEdges, offsetCornerType);
            if (offsetCurvesA.Count == 0 || offsetCurvesB.Count == 0)
                return new ITrimmedCurve[0];

            if (offsetCurvesA.Select(c => c.Length).Sum() < offsetCurvesB.Select(c => c.Length).Sum())
                return offsetCurvesA;
            else
                return offsetCurvesB;
        }
#else
        public static ICollection<ITrimmedCurve> OffsetChainInward(this ICollection<ITrimmedCurve> curves, Face face, double distance, OffsetCornerType offsetCornerType) {
            if (!(face.Geometry is Plane))
                throw new ArgumentException("Face must be planar");

            Plane plane = (Plane)face.Geometry;

            ITrimmedCurve firstEdge = curves.First();
            ITrimmedCurve[] otherEdges = curves.Skip(1).ToArray();

            ICollection<ITrimmedCurve> offsetCurves = firstEdge.OffsetChain(plane, distance, otherEdges, offsetCornerType);
            if (offsetCurves.Count == 0)
                return new ITrimmedCurve[0];

            if (face.ContainsPoint(offsetCurves.First().StartPoint) ^ distance > 0)
                return offsetCurves;

            offsetCurves = firstEdge.OffsetChain(plane, -distance, otherEdges, offsetCornerType);
            if (offsetCurves.Count == 0)
                return new ITrimmedCurve[0];

            return offsetCurves;
        }
#endif
        public static ICollection<Face> OffsetFaceEdgesInward(this Face face, double distance, OffsetCornerType offsetCornerType) {
            if (!(face.Geometry is Plane))
                throw new ArgumentException("Face must be planar");

            Plane plane = (Plane)face.Geometry;

            ITrimmedCurve[] curves = face.Edges.ToArray();
            ITrimmedCurve firstEdge = curves.First();
            ITrimmedCurve[] otherEdges = curves.Skip(1).ToArray();

            ICollection<ITrimmedCurve> offsetCurves = firstEdge.OffsetChain(plane, distance, otherEdges, offsetCornerType);
            if (offsetCurves.Count == 0)
                return null;

            if (face.ContainsPoint(offsetCurves.First().StartPoint) ^ distance > 0)
                return DoInWriteBlock<ICollection<Face>>(() => Body.CreatePlanarBody(plane, offsetCurves).SeparatePieces().Select(b => b.Faces.First()).ToList());

            offsetCurves = firstEdge.OffsetChain(plane, -distance, otherEdges, offsetCornerType);
            if (offsetCurves.Count == 0)
                return null;

            return Body.CreatePlanarBody(plane, offsetCurves).SeparatePieces().Select(b => b.Faces.First()).ToList();
        }

        public static ITrimmedCurve GetReverse(this ITrimmedCurve curve) {
            return CurveSegment.Create(curve.Geometry, curve.Bounds).CreateReversedCopy();
        }

        public static Ellipse AsEllipse(this Circle circle) {
            return Ellipse.Create(circle.Frame, circle.Radius, circle.Radius);
        }

        public static Point WrapPoint(this Cone cone, double angle, double distance) {
            return cone.Evaluate(PointUV.Create(angle, distance * Math.Cos(cone.HalfAngle))).Point;
        }

        public static Interval Union(this Interval a, Interval b) {
            return Interval.Create(
                Math.Min(Math.Min(a.Start, a.End), Math.Min(b.Start, b.End)),
                Math.Max(Math.Max(a.Start, a.End), Math.Max(b.Start, b.End))
            );
        }

        public static BoxUV Union(this BoxUV a, BoxUV b) {
            return BoxUV.Create(
                a.RangeU.Union(b.RangeU),
                a.RangeV.Union(b.RangeV)
            );
        }

        public static Point GetInBetweenPoint(this ITrimmedGeometry a, ITrimmedGeometry b) {
            Separation separation = a.GetClosestSeparation(b);
            return new Point[] { separation.PointA, separation.PointB }.Average();
        }

        public static double Magnitude(this PointUV pointUV) {
            return Math.Sqrt(pointUV.MagnitudeSquared());
        }

        public static double MagnitudeSquared(this PointUV pointUV) {
            return pointUV.U * pointUV.U + pointUV.V * pointUV.V;
        }

        public static double MagnitudeSquared(this VectorUV vectorUV) {
            return vectorUV.U * vectorUV.U + vectorUV.V * vectorUV.V;
        }

        public static Separation GetClosestSeparation(this Curve curveA, Curve curveB) {
            return CurveSegment.Create(curveA, curveA.Parameterization.GetReasonableInterval()).GetClosestSeparation(CurveSegment.Create(curveB, curveB.Parameterization.GetReasonableInterval()));
        }

        public static Interval GetReasonableInterval(this Parameterization parameterization) {
            if (parameterization.Bounds.Start == null)
                return Interval.Create(-1000, 1000);

            return Interval.Create(parameterization.Bounds.Start.Value, parameterization.Bounds.End.Value);

        }

        #region ModelerExtensions

        public static ICollection<Face> GetSurvivors(this Tracker tracker, Face face) {
            ICollection<Face> survivorList;
            bool success = tracker.TryGetSurvivors(face, out survivorList);
            return success ? survivorList : new[] { face };
        }

        /// <summary>
        /// Attempt to find convexity-sensitive angle between the normals of the adjacent faces of an edge using its midpoint. Returns a negavive angle for concave edges, 0 for tangent, or postitive for convex.
        /// </summary>
        /// <param name="edge">The Edge who's convexity is to be determined.</param>
        /// <returns></returns>
        public static double GetAngle(this Edge edge) {
            if (edge.Fins.Count != 2)
                throw new ArgumentException("Edge must have two fins in order to have angle.");

            CurveEvaluation curveEval = edge.Geometry.Evaluate((edge.Bounds.Start + edge.Bounds.End) / 2);
            Point edgePoint = curveEval.Point;
            Direction tangent = curveEval.Tangent;

            Fin finA = edge.Fins.ToArray()[0];
            if (finA.IsReversed ^ finA.Edge.IsReversed)
                tangent = -tangent;

            Direction dirA = finA.Loop.Face.ProjectPoint(edgePoint).Normal;
            if (finA.Loop.Face.IsReversed)
                dirA = -dirA;

            Fin finB = edge.Fins.ToArray()[1];
            Direction dirB = finB.Loop.Face.ProjectPoint(edgePoint).Normal;
            if (finB.Loop.Face.IsReversed)
                dirB = -dirB;

            double sense = Math.Asin(Math.Min(Math.Max(Vector.Dot(Direction.Cross(tangent, dirA).UnitVector, dirB.UnitVector), -1), 1)); // can be slightly out of range of [-1 ,1]
            if (Accuracy.AngleIsZero(sense))
                return 0;

            return Math.Abs(AddInHelper.AngleBetween(dirA, dirB)) * (sense > 0 ? 1 : -1);
        }

        public static Body TryUnionAndStitchOrFailBodies(this IEnumerable<Body> bodyList) {
            Body[] testBodies = bodyList.TryUnionBodies().ToArray();
            if (testBodies.Length == 1)
                return testBodies[0];

            testBodies = bodyList.TryStitchBodies().ToArray();
            if (testBodies.Length == 1)
                return testBodies[0];

            bodyList.ToArray().Print();

            Debug.Fail("TryUnionAndStitchOrFailBodies: didn't merge.");
            return null;
        }

        public static Body TryUnionOrFailBodies(this IEnumerable<Body> bodyList) {
            Body[] testBodies = bodyList.TryUnionBodies().ToArray();
            if (testBodies.Length == 1)
                return testBodies[0];

            bodyList.ToArray().Print();

            Debug.Fail("TryUnionOrFailBodies: didn't merge.");
            return null;
        }

        public static ICollection<Body> TryStitchBodies(this IEnumerable<Body> bodyList) {
            if (bodyList.Count() < 2)
                return bodyList.ToArray();

            var bodies = new List<Body>(bodyList.Select(b => b.Copy()));
            Body target = bodies[0];
            bodies.RemoveAt(0);

            target.Stitch(bodies, 0.0001, null);
            return target.SeparatePieces();
        }


#if false
		public static ICollection<Body> TryUnionBodies(this IEnumerable<Body> bodyList) {
			Queue<Body> bodies = new Queue<Body>(bodyList.Select(b => b.Copy()));
			Body targetBody = bodies.Dequeue();

			while (bodies.Count > 0) {
				targetBody.Unite(new Body[] { bodies.Dequeue() });
			}

			return targetBody.SeparatePieces();
		}

#else
        public static ICollection<Body> TryUnionBodies2(this IEnumerable<Body> bodyList) {
            if (bodyList.Count() < 2)
                return bodyList.ToArray();

            //		var bodies = new List<Body>(bodyList);
            var bodies = new List<Body>(bodyList.Select(b => b.Copy()));
            Body target = bodies[0];
            bodies.RemoveAt(0);

            target.Unite(bodies);
            return target.SeparatePieces();
        }

        public static ICollection<Body> TryUnionBodies(this IEnumerable<Body> bodyList) {
            if (AddInHelper.IsEscDown)
                return null;

            //	Queue<Body> bodies = new Queue<Body>(bodyList);
            Queue<Body> bodies = new Queue<Body>(bodyList.Select(b => b.Copy()));

            int failures = 0;
            List<Body> mergedBodies = new List<Body>();
            mergedBodies.Add(bodies.Dequeue());

            //PrintBodies(bodies, mergedBodies);

            Body toolBody = null;
            while (bodies.Count > 0) {
                //	if (mergedBodies.Count > 1)
                //		PrintBodies(bodies, mergedBodies);

                toolBody = bodies.Dequeue();
                try {
                    mergedBodies[mergedBodies.Count - 1].Unite(new Body[] { toolBody });
                    failures = 0;
                }
                catch {
                    bodies.Enqueue(toolBody);
                    failures++;
                }

                if (failures >= bodies.Count && bodies.Count > 0) {
                    mergedBodies.Add(bodies.Dequeue());
                    failures = 0;
                }
            }

            var separatedBodies = new List<Body>();
            foreach (Body body in mergedBodies) {
                List<Body> separatePieces = body.SeparatePieces().ToList();
                if (separatePieces.Count > 1) {
                    Body targetBody = separatePieces[0];
                    separatePieces.RemoveAt(0);
                    targetBody.Unite(separatePieces);
                    separatePieces = targetBody.SeparatePieces().ToList();
                }

                separatedBodies.AddRange(separatePieces);
            }

            if (separatedBodies.Count > 0) {
                Body[] backupBodies = separatedBodies.Select(b => b.Copy()).ToArray();
                Body target = separatedBodies[0];
                separatedBodies.RemoveAt(0);
                try {
                    target.Unite(separatedBodies);
                }
                catch {
                    return backupBodies;
                }

                return target.SeparatePieces();
            }

            return separatedBodies;
        }

        static int printBodiesCount = 0;
        static double printBodiesOffset = .05;
        static void PrintBodies(Queue<Body> bodyQueue, List<Body> bodyList) {
            printBodiesCount++;
            foreach (Body body in bodyList) {
                Body bodyTrans = body.Copy();
                bodyTrans.Transform(Matrix.CreateTranslation(Vector.Create(printBodiesOffset * printBodiesCount, 0, 0)));
                DesignBody.Create(Window.ActiveWindow.Scene as Part, "Merged", bodyTrans);
            }

            List<Body> bodies = new List<Body>(bodyQueue);
            foreach (Body body in bodies) {
                Body bodyTrans = body.Copy();
                bodyTrans.Transform(Matrix.CreateTranslation(Vector.Create(printBodiesOffset * printBodiesCount, 0, 0)));
                DesignBody.Create(Window.ActiveWindow.Scene as Part, "TBD", bodyTrans);
            }
        }
#endif

        public static Face OtherFace(this Face face, Edge edge) {
            foreach (Face otherFace in edge.Faces) {
                if (otherFace != face)
                    return otherFace;
            }

            return null;
        }

        public static ITrimmedCurve AsITrimmedCurve(this Point point) {
            return CurveSegment.Create(PointCurve.Create(point));
        }


        public static IList<ITrimmedCurve> AsPolygon(this IList<Point> points) {
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

        public static IList<Point> TessellateCurve(this ITrimmedCurve curve, double spacing) {
            double length = curve.Length;

            int count = (int)Math.Ceiling(length / spacing);

            double param = Math.Min(curve.Bounds.Start, curve.Bounds.End);
            double endParam = Math.Max(curve.Bounds.Start, curve.Bounds.End);
            length = endParam - param;

            List<Point> points = new List<Point>();
            for (int i = 0; i < count; i++)
                points.Add(curve.Geometry.Evaluate(param + (double)i * length / count).Point);

            points.Add(curve.Geometry.Evaluate(endParam).Point);
            return points;
        }

        public static Direction GetNormal(this Curve curve, double param) {
            double delta = Accuracy.LinearResolution * 10;
            return (
                curve.Evaluate(param + delta).Tangent.UnitVector -
                curve.Evaluate(param - delta).Tangent.UnitVector
                ).Direction;
        }

        public static SurfaceEvaluation GetSingleRayIntersection(this ITrimmedSurface iTrimmedSurface, Line line) {
            double min = double.MaxValue;
            double max = double.MinValue;

            Point origin = line.Origin;
            Vector direction = line.Direction.UnitVector;
            double position;
            foreach (Point corner in iTrimmedSurface.GetBoundingBox(Matrix.Identity).Corners) {
                position = Vector.Dot(corner - origin, direction);

                if (position < min)
                    min = position;

                if (position > max)
                    max = position;
            }

            foreach (IntPoint<SurfaceEvaluation, CurveEvaluation> intersectionTest in iTrimmedSurface.IntersectCurve(CurveSegment.Create(line, Interval.Create(min, max))))
                return intersectionTest.EvaluationA;

            return null;
        }

        public static bool AreEndPointsOnFace(this ITrimmedCurve iTrimmedCurve, Face face) {
            if ((iTrimmedCurve.GetBoundingBox(Matrix.Identity) & face.GetBoundingBox(Matrix.Identity)).IsEmpty)
                return false;

            if (iTrimmedCurve.StartPoint != face.ProjectPoint(iTrimmedCurve.StartPoint).Point)
                return false;

            if (iTrimmedCurve.EndPoint != face.ProjectPoint(iTrimmedCurve.EndPoint).Point)
                return false;

            return true;
        }

        public static ICollection<Primitive> GetMesh(this Body body, double surfaceDeviation, double angleDeviation) {
            IDictionary<Face, FaceTessellation> tessellationMap = body.GetTessellation(null, FacetSense.RightHanded, new TessellationOptions(surfaceDeviation, angleDeviation));

            var primitives = new List<Primitive>();
            foreach (FaceTessellation faceTessellation in tessellationMap.Values) {
                var facets = new List<Facet>();
                foreach (FacetStrip facetStrip in faceTessellation.FacetStrips)
                    facets.AddRange(facetStrip.Facets);

                primitives.Add(MeshPrimitive.Create(faceTessellation.Vertices, facets));
            }

            return primitives;
        }

        public static Body OffsetPlanarBody(this Body body, double thickness) {
            foreach (Face face in body.Faces) {
                if (face.Geometry as Plane == null)
                    throw new NotImplementedException();
            }

            var loftedBodies = new List<Body>();
            foreach (Face face in body.Faces) {
                var pointSharedEdgeMap = new Dictionary<Vertex, Edge>();
                foreach (Loop loop in face.Loops) {
                    var basePoints = new List<Point>();
                    var offsetPoints = new List<Point>();
                    foreach (Fin fin in loop.Fins) {
                        Vertex basePoint = fin.TrueStartVertex();
                        basePoints.Add(basePoint.Position);

                        Point? offsetPoint = null;
                        switch (fin.TrueStartVertex().Faces.Count) {
                            case 1:
                                offsetPoint = basePoint.Position + face.ProjectPoint(basePoint.Position).Normal * thickness;
                                break;

                            case 2: {
                                    List<Face> vertexFaces = fin.TrueStartVertex().Faces.ToList();
                                    Surface surface0 = GetPlanarFaceOffsetGeometry(thickness, basePoint, vertexFaces[0]);
                                    Surface surface1 = GetPlanarFaceOffsetGeometry(thickness, basePoint, vertexFaces[1]);

                                    Curve newEdge = (surface0 as Plane).IntersectPlanes(surface1 as Plane);
                                    offsetPoint = newEdge.ProjectPoint(basePoint.Position).Point;
                                    break;
                                }

                            case 3: {
                                    List<Face> vertexFaces = fin.TrueStartVertex().Faces.ToList();
                                    Surface surface0 = GetPlanarFaceOffsetGeometry(thickness, basePoint, vertexFaces[0]);
                                    Surface surface1 = GetPlanarFaceOffsetGeometry(thickness, basePoint, vertexFaces[1]);

                                    Curve newEdge = (surface0 as Plane).IntersectPlanes(surface1 as Plane);

                                    Surface surface2 = GetPlanarFaceOffsetGeometry(thickness, basePoint, vertexFaces[2]);
                                    offsetPoint = surface2.IntersectCurve(newEdge).ToArray()[0].Point;
                                    break;
                                }

                            default:
                                throw new NotImplementedException();
                        }

                        offsetPoints.Add(offsetPoint.Value);
                    }

                    basePoints.Add(basePoints[0]);
                    offsetPoints.Add(offsetPoints[0]);

                    IList<ITrimmedCurve> baseProfile = CreatePolyline(basePoints);
                    IList<ITrimmedCurve> offsetProfile = CreatePolyline(offsetPoints);

                    loftedBodies.Add(Body.LoftProfiles(new IList<ITrimmedCurve>[] { baseProfile, offsetProfile }, false, false));
                }
            }

            Body targetBody = loftedBodies[0];
            loftedBodies.RemoveAt(0);
            targetBody.Unite(loftedBodies);
            return targetBody;
        }

        public static DesignBody CreateTransformedCopy(this DesignBody desBody, Matrix trans) {
            Body body = desBody.Shape.Copy();
            body.Transform(trans);
            return DesignBody.Create(desBody.Parent, desBody.Name, body);
        }

        public static CurveSegment CreateTransformedCopy(this ITrimmedCurve curve, Matrix trans) {
            return CurveSegment.Create(curve.Geometry, curve.Bounds).CreateTransformedCopy(trans);
        }

        public static Body CreateTransformedCopy(this Body body, Matrix trans) {
            body = body.Copy();
            body.Transform(trans);
            return body;
        }

        public static Body OffsetPlanarBodySymmetric(this Body body, double thickness) { //TBD do this properly
            return new Body[] { OffsetPlanarBody(body, -thickness / 2), OffsetPlanarBody(body, thickness / 2) }.TryUnionBodies().First();
        }

        private static Surface GetPlanarFaceOffsetGeometry(double thickness, Vertex basePoint, Face face) {
            return face.Geometry.CreateTransformedCopy(Matrix.CreateTranslation(face.ProjectPoint(basePoint.Position).Normal * thickness));
        }

        public static Box GetBoundingBox(this IDesignBody iDesBody, Matrix trans) {
            Debug.Assert(trans == Matrix.Identity, "Warning: GetBoundingBox(this IDesignBody iDesBody, Matrix trans) not tested with trans != Matrix.Identity");
            return iDesBody.Master.GetBoundingBox(trans * iDesBody.TransformToMaster.Inverse * trans.Inverse);
        }

        #endregion
    }
}
