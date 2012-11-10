using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using Point = SpaceClaim.Api.V10.Geometry.Point;

/// <summary>
/// AddIn helper functions and other utilities
/// </summary>

namespace SpaceClaim.AddInLibrary {
	public static class AddInHelper {
		public static ICollection<IDesignCurve> CreateLines(IList<Point> points, IPart iPart) {
			if (iPart == null)
				iPart = Window.ActiveWindow.ActiveContext.ActivePart;

			List<IDesignCurve> iDesignCurves = new List<IDesignCurve>();
			for (int i = 0; i < points.Count - 1; i++) {
				CurveSegment curveSegment = CurveSegment.Create(points[i], points[i + 1]);
				if (curveSegment != null)
					iDesignCurves.Add(DesignCurve.Create(iPart, curveSegment));
			}

			return iDesignCurves;
		}

		// Creates a plane from the most total normal between vectors created between consecutive points.  
		// TBD The algorithm for multiple points needs revision.  Won't consistently set normal on star shapes with equal convex and concave vertices.  
		public static bool TryCreatePlaneFromPoints(IList<Point> points, out Plane plane) {
			plane = null;
			Vector u, v;
			Direction dir = Direction.Zero;
			int count = points.Count;

			if (count < 3)
				return false;

			if (count == 3) { // Optimization
				u = points[1] - points[0];
				v = points[2] - points[1];
				try {
					plane = Plane.Create(Frame.Create(points[0], u.Direction, Direction.Cross(u.Direction, Direction.Cross(v.Direction, u.Direction))));
					return true;
				}
				catch { // In case two points were the same, we get an exception that a cross is zero
					return false;
				}

			}

			// When we have more than four points, we try to figure out the handed normal assuming that the points lie in plane.
			// To do so, we'll see if the angles of the consecutive points add up to a postive or negative number.
			Dictionary<Direction, int> normalCandidates = new Dictionary<Direction, int>(new DirectionComparer());
			//double angle = 0;
			for (int i = 0; i < count; i++) {
				u = points[(i + 1) % count] - points[i % count];
				v = points[(i + 2) % count] - points[(i + 1) % count];

				//angle += AngleBetween(u, v);

				dir = Vector.Cross(u, v).Direction;
				if (normalCandidates.ContainsKey(dir))
					normalCandidates[dir]++;
				else
					normalCandidates.Add(dir, 0);
			}

			int max = 0;
			foreach (Direction key in normalCandidates.Keys) {
				if (normalCandidates[key] > max && !key.IsZero) {
					dir = key;
					max = normalCandidates[key];
				}
			}

			if (dir.IsZero)
				return false;

		//	if (angle < 0)
		//		dir = -dir;

			plane = Plane.Create(Frame.Create(points[0], dir.ArbitraryPerpendicular));
			return true;
		}

		public static ICollection<Point> IntersectSpheres(ICollection<Sphere> sphereCollection) {
			Debug.Assert(sphereCollection.Count > 2, "Not enough spheres to get vertex.");

			List<Sphere> spheres = new List<Sphere>(sphereCollection);

			Body body = ShapeHelper.CreateSphere(spheres[0].Frame.Origin, spheres[0].Radius * 2);
			spheres.RemoveAt(0);

			while (spheres.Count > 0) {
				Body tool = ShapeHelper.CreateSphere(spheres[0].Frame.Origin, spheres[0].Radius * 2);
				spheres.RemoveAt(0);
				body.Intersect(new Body[] { tool });
			}

			List<Point> points= new List<Point>();
			foreach (Vertex vertex in body.Vertices)
				points.Add(vertex.Position);

			return points;
		}

		public static List<Point> GetPointsForLoop(Loop loop) { 
            throw new NotSupportedException("Not used or tested.  Beware.");
			List<Point> points = new List<Point>();

			foreach (Fin fin in loop.Fins)
					points.Add(fin.TrueStartPoint());

			if (loop.Face.IsReversed)
				points.Reverse();

			return points;
		}

		public static bool isCooincident(Plane plane1, Plane plane2) {
			if (!plane1.ContainsPoint(plane2.Frame.Origin))
				return false;

			if (!plane1.Frame.DirZ.IsParallel(plane2.Frame.DirZ))
				return false;

			return true;
		}

		public static bool isCooincident(Cylinder cylinder1, Cylinder cylinder2) {
			if (!isCooincident(cylinder1.Axis, cylinder2.Axis))
				return false;

			if (cylinder1.Radius != cylinder2.Radius)
				return false;

			return true;
		}

		public static bool isCooincident(Line line1, Line line2) {
			if (!line1.ContainsPoint(line2.Origin))
				return false;

			if (!line1.Direction.IsParallel(line2.Direction))
				return false;

			return true;
		}

		// Remove requirement that directions must be perpendicular
		public static Frame CreateFrame(Point point, Direction DirX, Direction DirY) {
			return Frame.Create(point, DirX, Direction.Cross(DirX, Direction.Cross(DirY, DirX)));
		}

		// Create a Transformation mapping a line to a line at their origins
		public static Matrix CreateLineToLineTransform(Line source, Line dest) {
			return Matrix.CreateMapping(Frame.Create(source.Origin, source.Direction, source.Direction.ArbitraryPerpendicular)).Inverse *
							Matrix.CreateMapping(Frame.Create(dest.Origin, dest.Direction, dest.Direction.ArbitraryPerpendicular));
		}

		public static double AngleBetween(Vector a, Vector b) {
			return Math.Acos(Vector.Dot(a, b) / (a.Magnitude * b.Magnitude));
		}
		public static double AngleBetween(Direction a, Direction b) {
			return AngleBetween(a.UnitVector, b.UnitVector);
		}

		public static Component CreateNewComponent(string name) {
            throw new NotSupportedException("Not used or tested.  Beware.");

			Part parent = Window.ActiveWindow.ActiveContext.ActivePart as Part;
			Debug.Assert(parent != null, "No parent part.  Are we not in a design window?");

            Part part = Part.Create(parent.Document, name);
			Debug.Assert(part != null, "part != null");

			Component component = Component.Create(parent, part);
			Debug.Assert(component != null, "Null component!");

			return component;
		}

		// Traversal Helpers

		public static bool TryGetAdjacentDesignFace(DesignFace sourceFace, DesignEdge sourceEdge, out DesignFace otherFace) {
			Debug.Assert(sourceEdge.Faces.Count <= 2, "Yikes, non-manifold edge!");

			otherFace = null;
			foreach (DesignFace designFace in sourceEdge.Faces) {
				if (designFace != sourceFace) {
					otherFace = designFace;
					return true;
				}
			}

			return false;
		}

		// Command helpers 

		public static void RefreshMainform() {
			try {
				MainForm.Refresh();
			}
			finally {
			}
		}

		public static System.Windows.Forms.Form MainForm {
			get { return System.Windows.Forms.Application.OpenForms["MainForm"]; }
		}

		public static void EnabledCommand_Updating(object sender, EventArgs e) {
			(sender as Command).IsEnabled = true;
		}

		public static void BooleanCommand_Updating(object sender, EventArgs e) {
			Command command = (sender as Command);
			command.IsEnabled = true;
	// TBD update		command.IsChecked = ((bool?)command.Tag).GetValueOrDefault();
		}

		public static bool IsEscDown {
			get { return (IsKeyDown(System.Windows.Forms.Keys.Escape)); }
		}

		[System.Runtime.InteropServices.DllImport("User32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public static extern short GetAsyncKeyState(uint key);

		/// <summary>Is this key down right now</summary>
		public static bool IsKeyDown(System.Windows.Forms.Keys key) {
			return (GetAsyncKeyState((uint) key) & 0x8000) != 0;
		}					

		public static double ParseAffixedCommand(string commandName, string commandPrefix) {
			string fraction = commandName.Substring(commandPrefix.Length, commandName.Length - commandPrefix.Length);
			string[] divideMe = fraction.Split('_');
			if (divideMe.Length == 1)
				return double.Parse(fraction);
			else
				return double.Parse(divideMe[0]) / double.Parse(divideMe[1]);
		}
	}

	class DirectionComparer : IEqualityComparer<Direction> {
		public bool Equals(Direction x, Direction y) {
			return x.Equals(y);
		}

		public int GetHashCode(Direction dir) {
			return
				(int)(dir.X * int.MaxValue) ^
				(int)(dir.Y * int.MaxValue) ^
				(int)(dir.Z * int.MaxValue)
			;
		}
	}

}
