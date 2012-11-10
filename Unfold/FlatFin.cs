using System;
using System.Collections.Generic;
using System.Diagnostics;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Unfold {
	public class FlatFin {
		FlatLoop flatLoop;
		Fin sourceFin;
		FlatFin adjacentFin;
		bool isInternal = false;

		public FlatFin(FlatLoop flatLoop, Fin sourceFin) {
			this.flatLoop = flatLoop;
			this.sourceFin = sourceFin;
		}

		public CurveSegment AsCurveSegment() { // TBD using AsSpline here is a disgusting hack. Fix it.
		//	return CurveSegment.Create(flatLoop.FlatFace.Transform * sourceFin.Edge.Geometry.AsSpline(sourceFin.Edge.Bounds), sourceFin.Edge.Bounds);
          return  sourceFin.Edge.CreateTransformedCopy(flatLoop.FlatFace.Transform);
		}

		public FlatLoop FlatLoop {
			get { return flatLoop; }
		}

		public FlatFace FlatFace {
			get { return flatLoop.FlatFace; }
		}

		public Fin SourceFin {
			get { return sourceFin; }
		}

		public Point SourceStart {
			get { return SourceFin.TrueStartPoint(); }
		}

		public Point SourceEnd {
			get { return SourceFin.TrueEndPoint(); }
		}

		public Point Start {
			get { return FlatFace.Transform * SourceStart; }
		}

		public Point End {
			get { return FlatFace.Transform * SourceEnd; }
		}

		public Vector Vector {
			get { return End - Start; }
		}

		public FlatFin AdjacentFin {
			get { return adjacentFin; }
			set { adjacentFin = value; }
		}

		public bool IsInternal {
			get { return isInternal; }
			set { isInternal = value; }
		}

		public bool Intersects(FlatFin fin) {
			return !Vector.Cross(this.Vector, fin.Start.Vector).Direction.Equals(Vector.Cross(this.Vector, fin.End.Vector).Direction)
				&& !Vector.Cross(fin.Vector, this.Start.Vector).Direction.Equals(Vector.Cross(fin.Vector, this.End.Vector).Direction);
		}

		public class FlatFinComparer : IComparer<FlatFin> {  // structure borrowed from online help
			public int Compare(FlatFin x, FlatFin y) {
				if (x == null)
					if (y == null)	// If x is null and y is null, they're equal.
						return 0;
					else // If x is null and y is not null, y is greater. 
						return -1;
				else {  // If x is not null and y is null, x is greater.
					if (y == null) 
						return 1;
					else {  // and y is not null, compare the lengths of the two strings.
						double xl = x.SourceFin.Edge.Length;
						double yl = y.SourceFin.Edge.Length;
						if (Accuracy.EqualLengths(xl, yl))  // if lengths are close
							return -x.FlatFace.Rank.CompareTo(y.FlatFace.Rank);  // then use proximity to seed face
						else if (xl > yl)
							return 1;
						else
							return -1;
					}
				}
			}
		}

		public FlatFin OtherFlatFin(FlatFace flatFace) {
			foreach (FlatLoop flatLoop in flatFace.Loops)
				foreach (FlatFin flatFin in flatLoop.Fins)
					if (flatFin.SourceFin.Edge.Equals(this.SourceFin.Edge))
					return flatFin;

			Debug.Fail("Could not find other FlatFin!");
			return null;
		}

	}
}
