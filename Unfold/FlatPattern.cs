using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Unfold {
	public class FlatPattern {
		Body sourceBody;
		Part flatPart;

		List<FlatBody> flatBodies = new List<FlatBody>();
		Plane paperPlane = Plane.PlaneXY;

		bool isDetectingCollisions;
		bool isCreatingBreaks;
		double breakAngle;
		double dashSize;

		Dictionary<Face, FlatFace> flatFaceMapping = new Dictionary<Face, FlatFace>();

		public FlatPattern(Body sourceBody, Face startFace, bool isDetectingCollisions, bool isCreatingBreaks, double breakAngle, double dashSize, string name) {
			Debug.Assert(sourceBody != null);
			this.sourceBody = sourceBody;
			this.isDetectingCollisions = isDetectingCollisions;
			this.isCreatingBreaks = isCreatingBreaks;
			this.breakAngle = breakAngle;
			this.dashSize = dashSize;

			Part mainPart = Window.ActiveWindow.ActiveContext.Context as Part;
			Debug.Assert(mainPart != null, "mainPart != null");

            this.flatPart = Part.Create(mainPart.Document, name);
			Debug.Assert(flatPart != null, "flatPart != null");

			Component flatComponent = Component.Create(mainPart, flatPart);
			Debug.Assert(flatComponent != null, "flatComponent != null");

			if (startFace == null) {
				// Find the longest edge on the model
				double bestLength = 0f;
				foreach (Edge edge in sourceBody.Edges)
					if (edge.Length > bestLength)
						bestLength = edge.Length;

				// Find a face that conatins the longest edge and start unfolding using it
				foreach (Face sourceFace in sourceBody.Faces) {
					foreach (Edge edge in sourceFace.Edges) {
						if (edge.Length == bestLength) {
							startFace = sourceFace;
							break;
						}
					}
					if (startFace != null)
						break;
				}
			}

			LoopFlatBodies(startFace);
		}

		public List<FlatBody> FlatBodies {
			get { return flatBodies; }
		}

		public Plane PaperPlane {
			get { return paperPlane; }
		}

		public bool IsDetectingIntersections {
			get { return isDetectingCollisions; }
		}

		public bool IsCreatingDashes {
			get { return isCreatingBreaks; }
		}

		public double BreakAngle {
			get { return breakAngle; }
		}

		public double DashSize {
			get { return dashSize; }
		}

		public Part FlatPart {
			get { return flatPart; }
		}

		public Dictionary<Face, FlatFace> FlatFaceMapping {
			get { return flatFaceMapping; }
			set { flatFaceMapping = value; }
		}

		private void LoopFlatBodies(Face startFace) {
			List<FlatFin> remainingFins = new List<FlatFin>();
			FlatFin nextFin = null;
			while (true) {
				FlatBody flatBody = new FlatBody(this);
				flatBodies.Add(flatBody);
				
				FlatFace baseFace = new FlatFace(startFace, flatBody);

				FlatLoop flatLoop = null;
				foreach (FlatLoop testLoop in baseFace.Loops) {
					if (testLoop.SourceLoop.IsOuter) {
						flatLoop = testLoop;
						break;
					}
				}
				Debug.Assert(flatLoop != null);

				Point startPoint = flatLoop.SourcePoints[0];
				Point endPoint = flatLoop.SourcePoints[1];
				Frame startFrame = Frame.Create(startPoint, (endPoint - startPoint).Direction, baseFace.SourcePlane.Frame.DirZ);
				
				Frame endFrame;
				if (nextFin == null) // Pick the origin on the first pass, otherwise place it where it overlaps
					endFrame = Frame.Create(Point.Origin, Direction.DirX, -Direction.DirZ);
				else // use the location of the open fin on the FlatBody where it couldn't be placed
					endFrame = Frame.Create(nextFin.End, (nextFin.Start - nextFin.End).Direction, -Direction.DirZ);

				baseFace.Transform = Matrix.CreateMapping(endFrame) * Matrix.CreateMapping(startFrame).Inverse;
				flatBody.AddFace(baseFace);

				remainingFins.AddRange(LoopUnfold(flatBody));  // Most of the unfolding work is here

				List<FlatFin> cleanRemainingFins = new List<FlatFin>();
				foreach (FlatFin flatFin in remainingFins) {
					if (!FlatFaceExists(flatFin.AdjacentFin.FlatFace.SourceFace))
						cleanRemainingFins.Add(flatFin);
				}
				remainingFins = cleanRemainingFins;

				if (remainingFins.Count == 0)
					return;
				else {
					nextFin = remainingFins[0];
					remainingFins.Remove(nextFin);
					startFace = nextFin.AdjacentFin.FlatFace.SourceFace;
				}
			}
		}

		/*
		 * LoopUnfold attempts to add as many FlatFaces to a FlatBody as it can.  If interference checking is off, it will add all of the
		 * faces of the body. (Actually, the shell, as we do not handle voids).  We expect for the seed face of the FlatBody to already
		 * be created, so we have some FlatFins from which to propogate.
		 *  
		 * If interference checking is on, multiple flat bodies are required to avoid collisions.  Therefore
		 * we return the list of FlatFins on the completed FlatBody that need to start as the seeds for additional FlatBodies.  By returning 
		 * the FlatFins on the base, rather than just the faces, we can easily transfrom the adjacent FlatFace to line up with that FlatFin
		 * by making it the seed face for the next FlatBody.
		 */

		// TBD add tab code would go here, created extra flatfaces, but we need to consider the tabs as part of the collision detection???

		private List<FlatFin> LoopUnfold(FlatBody flatBody) {
			Debug.Assert(flatBody.FlatFaces.Count > 0);  // should only be one, but more is harmless if we optimize with seeds of several flatfaces

			List<FlatFin> remainingFins = new List<FlatFin>();

			while (flatBody.OpenFins.Count > 0) {
				if (AddInHelper.IsEscDown)
					break;

				FlatFace testFace = null; 
				List<FlatFin> interferingFins = new List<FlatFin>();

				foreach (FlatFin baseFin in flatBody.OpenFins) {  // OpenFins sorts itself to present the most desirable edge (the longest)  
					testFace = new FlatFace(baseFin.FlatFace.SourceFace.GetAdjacentFace(baseFin.SourceFin.Edge), flatBody);
					baseFin.AdjacentFin = baseFin.OtherFlatFin(testFace);  // make symmetric? Careful: FlatFace.Render() relies on the assumption it is not

					testFace.Transform = Matrix.CreateMapping(AddInHelper.CreateFrame(baseFin.Start, (baseFin.End - baseFin.Start).Direction, -Direction.DirZ)) *
						Matrix.CreateMapping(AddInHelper.CreateFrame(baseFin.AdjacentFin.SourceEnd, (baseFin.AdjacentFin.SourceStart - baseFin.AdjacentFin.SourceEnd).Direction, baseFin.AdjacentFin.FlatFace.SourcePlane.Frame.DirZ)).Inverse;

					if (isDetectingCollisions && flatBody.FaceInterferes(testFace))  // FaceInterferes only gets called if isDetectingCollisions == True
						interferingFins.Add(baseFin);
					else {
						flatBody.AddFace(testFace);
						baseFin.IsInternal = true;
						baseFin.AdjacentFin.IsInternal = true;
						break;
					}
				}

				foreach (FlatFin baseFin in interferingFins) {
					flatBody.OpenFins.Remove(baseFin);
					remainingFins.Add(baseFin);
				}
			}

			return remainingFins;
		}

		public bool FlatFaceExists(Face sourceFace) {
			foreach (FlatBody flatBody in FlatBodies)
				if (flatBody.FlatFaceExists(sourceFace))
					return true;
			return false;
		}

		public void Render() {
			foreach (FlatBody flatBody in flatBodies)
				flatBody.Render();
		}

		public Box GetBoundingBox(Matrix trans) {
			Box box = Box.Empty;
			foreach (FlatBody flatBody in flatBodies)
				box |= flatBody.GetBoundingBox(trans);

			return box;
		}



	}
}
