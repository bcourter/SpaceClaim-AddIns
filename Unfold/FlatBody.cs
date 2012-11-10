using System;
using System.Collections.Generic;
using System.Diagnostics;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Unfold {
	public class FlatBody {
		List<FlatFace> flatFaces = new List<FlatFace>();
		FlatPattern flatPattern;
		Part flatPart;
		List<FlatFin> openFins = new List<FlatFin>();
		Body flatBodyShape = null;

		public FlatBody(FlatPattern flatPattern) {
			this.flatPattern = flatPattern;

			if (flatPattern.IsDetectingIntersections) {
                this.flatPart = Part.Create(flatPattern.FlatPart.Document, "Flat Part");
				Debug.Assert(flatPart != null, "flatPart != null");

				Component flatComponent = Component.Create(flatPattern.FlatPart, flatPart);
				Debug.Assert(flatComponent != null, "flatComponent != null");
			}
			else {
				Part mainPart = Window.ActiveWindow.ActiveContext.Context as Part;
				this.flatPart = flatPattern.FlatPart;
			}
		}

		public List<FlatFace> FlatFaces {
			get { return flatFaces; }
		}

		public FlatPattern FlatPattern {
			get { return flatPattern; }
		}

		public Part FlatPart {
			get { return flatPart; }
		}

		public List<FlatFin> OpenFins {
			get { return openFins; }
		}

		public void Render() {
			foreach (FlatFace flatFace in flatFaces)
				flatFace.Render();
		}

		public void AddFace(FlatFace flatFace) {
			flatFaces.Add(flatFace);
			flatPattern.FlatFaceMapping[flatFace.SourceFace] = flatFace;

			foreach (FlatLoop flatLoop in flatFace.Loops) {
				foreach (FlatFin flatFin in flatLoop.Fins) {
					Face testFace = flatFin.FlatFace.SourceFace.GetAdjacentFace(flatFin.SourceFin.Edge);
					if (testFace == null)  // one-sided (laminar) edge
						continue;
					if (!FlatPattern.FlatFaceExists(testFace)) {
						openFins.Add(flatFin);
					}

					List<FlatFin> removeFins = new List<FlatFin>();
					foreach (FlatFin baseFin in openFins)
						if (baseFin.SourceFin.Edge.Equals(flatFin.SourceFin.Edge) && !baseFin.FlatFace.Equals(flatFace))
							removeFins.Add(baseFin);
					foreach (FlatFin removeFin in removeFins)
						openFins.Remove(removeFin);
				}
			}

			openFins.Sort(new FlatFin.FlatFinComparer());  // get longest fin --TBD use sorted list structure?
			openFins.Reverse();

			if (flatPattern.IsDetectingIntersections) {
				Body body = flatFace.CreateUnfoldedFaceBody();
				if (flatBodyShape == null)
					flatBodyShape = body;
				else {
					try {
						flatBodyShape.Unite(new Body[] { body });
					}
					catch {
						DesignBody.Create(Window.ActiveWindow.Scene as Part, "flatBodyShape", flatBodyShape);
						DesignBody.Create(Window.ActiveWindow.Scene as Part, "tool", body);
						Debug.Fail("Boolean failed when merging flatBodyShape.");
					}
				}
			}
		}

		public bool FlatFaceExists(Face sourceFace) {
			foreach (FlatFace face in FlatFaces)
				if (sourceFace.Equals(face.SourceFace))
					return true;
			return false;
		}

		public bool FaceInterferes(FlatFace candidateFace) {
			Body tool = candidateFace.CreateUnfoldedFaceBody();
			Body target = flatBodyShape.Copy();

			return target.GetCollision(tool) == Collision.Intersect;
		}

		public Box GetBoundingBox(Matrix trans) {
			Box box = Box.Empty;
			foreach (FlatFace flatFace in flatFaces)
				box |= flatFace.GetBoundingBox(trans);

			return box;
		}

	}
}
