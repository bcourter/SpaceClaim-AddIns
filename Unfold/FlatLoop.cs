using System;
using System.Collections.Generic;
using System.Diagnostics;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Unfold {
	public class FlatLoop {
		FlatFace flatFace;
		Loop sourceLoop;
		List<FlatFin> fins;

		public FlatLoop(Loop sourceLoop, FlatFace flatFace) {
			this.sourceLoop = sourceLoop;
			this.flatFace = flatFace;

			fins = new List<FlatFin>();
			foreach (Fin fin in sourceLoop.Fins)
				fins.Add(new FlatFin(this, fin));
		}

		public Loop SourceLoop {
			get { return sourceLoop; }
		}

		public FlatFace FlatFace {
			get { return flatFace; }
		}

		public IList<FlatFin> Fins {
			get { return fins; }
		}

		public IList<Point> SourcePoints {
			get {
				List<Point> sourcePoints = new List<Point>();
				foreach (FlatFin fin in Fins) {
					sourcePoints.Add(fin.SourceStart);
				}
				return sourcePoints;
			}
		}

		public IList<Point> Points {
			get {
				List<Point> destPoints = new List<Point>();
				foreach (Point point in SourcePoints)
					destPoints.Add(flatFace.Transform * point);
				return destPoints;
			}
		}
	}
}
