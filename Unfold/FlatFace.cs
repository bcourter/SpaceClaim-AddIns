using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using Unfold.Properties;

namespace SpaceClaim.AddIn.Unfold {
	public class FlatFace {
		static int rankIndex = 0;
		FlatBody flatBody;
		Face sourceFace;
		Matrix transform;
		Body renderedBody;
		int rank;

		List<FlatLoop> loops;

		public FlatFace(Face sourceFace, FlatBody flatBody) {
			this.sourceFace = sourceFace;
			this.flatBody = flatBody;
			rank = rankIndex++;

			loops = new List<FlatLoop>();
			foreach (Loop loop in sourceFace.Loops)
				loops.Add(new FlatLoop(loop, this));
		}

		public Face SourceFace {
			get { return sourceFace; }
		}

		public FlatBody FlatBody {
			get { return flatBody; }
		}

		public Matrix Transform {
			get { return transform; }
			set { transform = value; }
		}

		public Body RenderedBody {
			get { return renderedBody; }
		}

		public Plane SourcePlane {
			get {
				SurfaceEvaluation eval0 = SourceFace.Geometry.Evaluate(PointUV.Create(0, 0));
				SurfaceEvaluation eval1 = SourceFace.Geometry.Evaluate(PointUV.Create(1, 1));
				Point p0 = eval0.Point;
				Point p1 = eval1.Point;
				return Plane.Create(Frame.Create(p0, (p1 - p0).Direction, Direction.Cross((p1 - p0).Direction, (eval0.Normal.UnitVector * (SourceFace.IsReversed ? -1 : 1)).Direction)));
			}
		}

		public IList<FlatLoop> Loops {
			get { return loops; }
		}

		public int Rank {
			get { return rank; }
		}

		public void Render() {
			renderedBody = CreateUnfoldedFaceBody();
			DesignBody designBody = DesignBody.Create(flatBody.FlatPart, Resources.FlatFaceName, renderedBody);
			designBody.Layer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, Resources.FlatFaceLayerName, System.Drawing.Color.Beige);

			foreach (FlatLoop flatLoop in Loops) {
				foreach (FlatFin flatFin in flatLoop.Fins.Where(f => !f.IsInternal)) {
					DesignCurve designCurve = DesignCurve.Create(flatBody.FlatPart, flatFin.SourceFin.Edge);
					designCurve.Transform(transform);
					designCurve.Layer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, Resources.FlatCuttingLinesLayerName, System.Drawing.Color.Blue);
				}

				foreach (FlatFin flatFin in flatLoop.Fins.Where(f => f.AdjacentFin != null)) {
					if (
						flatBody.FlatPattern.IsCreatingDashes &&
						Accuracy.CompareAngles(AddInHelper.AngleBetween(flatFin.FlatFace.SourceFace.Geometry.Evaluate(PointUV.Origin).Normal, flatFin.AdjacentFin.FlatFace.SourceFace.Geometry.Evaluate(PointUV.Origin).Normal), flatBody.FlatPattern.BreakAngle) <= 0) {

						Layer breakLayer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, Resources.BreakLinesLayerName, System.Drawing.Color.DarkBlue);
						if (flatBody.FlatPattern.DashSize == 0) {
							DesignCurve desCurve = DesignCurve.Create(FlatBody.FlatPart, flatFin.AsCurveSegment());
							desCurve.Layer = breakLayer;
						}
						else
							DashesButtonCapsule.CreateDashes(flatFin.AsCurveSegment(), FlatBody.FlatPart, flatBody.FlatPattern.DashSize, breakLayer);
					}
				}
			}
		}

		public Body CreateUnfoldedFaceBody() {
			List<ITrimmedCurve> profile = new List<ITrimmedCurve>();

			foreach (FlatLoop flatLoop in Loops) {
				foreach (Fin fin in flatLoop.SourceLoop.Fins)
					profile.Add(fin.Edge);
			}

			Body body = null;
			try {
				body = Body.CreatePlanarBody(sourceFace.Geometry as Plane, profile);
			}
			catch {
				Debug.Assert(false, "Could not create facet.");
				return null;
			}

			body.Transform(transform);

			return body;
		}

		public bool IsAdjacentTo(FlatFace otherFace) {
			if (this == otherFace)
				throw new InvalidOperationException();

			foreach (FlatLoop thisLoop in loops) {
				foreach (FlatFin thisFin in thisLoop.Fins) {
					foreach (FlatLoop otherLoop in otherFace.Loops) {
						foreach (FlatFin otherFin in otherLoop.Fins) {
							if (otherFin.AdjacentFin == thisFin)
								return true;

							if (thisFin.AdjacentFin == otherFin)
								return true;
						}
					}
				}
			}

			return false;
		}

		public Box GetBoundingBox(Matrix trans) {
			return CreateUnfoldedFaceBody().GetBoundingBox(trans);
		}

	}
}
