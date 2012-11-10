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
using Unfold.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Unfold {
	class UnfoldButtonCapsule : RibbonButtonCapsule {
		bool isDetectingCollisions = false;
		bool isCreatingBreaks;
		double breakAngle;
		double dashSize;

		public UnfoldButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Unfold", Resources.UnfoldCommandText, Resources.Unfold, Resources.UnfoldCommandHint, parent, buttonSize) {
		}

		protected override void OnUpdate(Command command) {
			base.OnUpdate(command);

			command.IsEnabled = false;

			ICollection<IDocObject> selection = Window.ActiveWindow.ActiveContext.Selection;
			if (selection.Count != 1)
				return;

			if (selection.First() is IDesignBody || selection.First() is IDesignFace)
				command.IsEnabled = true;
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			//isDetectingCollisions = Booleans[Resources.DetectCollisionsText].Value;
			isCreatingBreaks = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsCreatingBreakLines].IsEnabledCommandBoolean.Value;
			breakAngle = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsCreatingBreakLines].Values[Resources.BreakLinesMinimumAngle].Value * Math.PI / 180;
			dashSize = RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsCreatingBreakLines].Values[Resources.BreakLinesDashSize].Value / ActiveWindow.Units.Length.ConversionFactor;
			if (!isCreatingBreaks) {
				breakAngle = 0;
				dashSize = 0;
			}

			IDesignBody designBody = Window.ActiveWindow.ActiveContext.SingleSelection as IDesignBody;
			IDesignFace designFace = null;
			if (designBody == null) {
				designFace = Window.ActiveWindow.ActiveContext.SingleSelection as IDesignFace;
				if (designFace != null)
					designBody = designFace.GetAncestor<IDesignBody>();
				else
					return;
			}

			bool isPlanarBody = true;
			foreach (DesignFace face in designBody.Master.Faces) {
				if (face.Shape.Geometry is Plane)
					continue;

				isPlanarBody = false;
				face.SetColor(null, Color.Red);
			}

			if (!isPlanarBody) {
				Application.ReportStatus(Resources.UnfoldNotPlanarError, StatusMessageType.Error, null);
				return;
			}

			Face startFace = null;
			if (designFace != null)
				startFace = designFace.Master.Shape;

			Unfold(designBody.Master.Shape.Body, startFace, isDetectingCollisions, isCreatingBreaks, breakAngle, dashSize);

			Settings.Default.IsUnfoldingWithBreaks = isCreatingBreaks;
			Settings.Default.UnfoldingWithBreaksAngle = breakAngle * 180 / Math.PI;
			Settings.Default.UnfoldingWithBreaksDashSize = dashSize / ActiveWindow.Units.Length.ConversionFactor;
		}

		void Unfold(Body body, Face startFace, bool isDetectingCollisions, bool isCreatingDashes, double breakAngle, double dashSize) {
			DateTime startTime = DateTime.Now;

			FlatPattern flatPattern = new FlatPattern(body, startFace, isDetectingCollisions, isCreatingDashes, breakAngle, dashSize, Resources.FlatPatternText);

			DateTime calcTime = DateTime.Now;
			double calcDuration = (calcTime - startTime).TotalSeconds;

			flatPattern.Render();
			double drawDuration = (DateTime.Now - calcTime).TotalSeconds;

			int n = 0;
			foreach (FlatBody flatBody in flatPattern.FlatBodies)
				foreach (FlatFace flatFace in flatBody.FlatFaces)
					foreach (FlatLoop flatLoop in flatFace.Loops)
						n++;

			string output =
				String.Format("Unfolded {0:D} faces in {1:F} s. ({2:F} fps.) \n", n, Math.Round(calcDuration, 2), Math.Round((double) n / calcDuration, 2)) +
				String.Format("Modeled {0:F} seconds. ({1:F} fps.)", Math.Round(drawDuration, 2), Math.Round((double) n / drawDuration, 2))
			;

			Application.ReportStatus(output, StatusMessageType.Information, null);
		}
	}

	class UnfoldBreakOptionsCapsule : RibbonBooleanGroupCapsule {
		public UnfoldBreakOptionsCapsule(string name, RibbonTabCapsule parent)
			: base(name, Resources.IsCreatingBreakLines, Resources.IsCreatingBreakLines, parent) {

			RibbonBooleanGroupCapsule.BooleanGroupCapsules[Resources.IsCreatingBreakLines].IsEnabledCommandBoolean.Value = Settings.Default.IsUnfoldingWithBreaks;
			Values[Resources.BreakLinesMinimumAngle] = new RibbonCommandValue(Settings.Default.UnfoldingWithBreaksAngle);
			Values[Resources.BreakLinesDashSize] = new RibbonCommandValue(Settings.Default.UnfoldingWithBreaksDashSize);
		}
	}

	class UnfoldWithCurvesButtonCapsule : RibbonButtonCapsule {
		bool isDetectingCollisions = false;

		public UnfoldWithCurvesButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Unfold", Resources.UnfoldWithCurvesCommanText, null, Resources.UnfoldWithCurvesCommandHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			//isDetectingCollisions = Booleans[Resources.DetectCollisionsText].Value;
			//isCreatingDashes = Booleans[Resources.CreateDashesText].Value;

			Part part = Window.ActiveWindow.Scene as Part;
			if (part == null)
				return;

			Layer curveLayer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, Resources.FlatMediumEngravingLayerName, System.Drawing.Color.Green);
			Layer planeLayer = NoteHelper.CreateOrGetLayer(part.Document, Resources.AnnotationPlanesLayerName, Color.Gray);
			planeLayer.SetVisible(null, false);
			foreach (Component component in part.Components) {
				Body body = component.Content.Bodies.First().Master.Shape.Copy();
				body.Transform(component.Content.TransformToMaster.Inverse);
				Face startFace = body.Faces.First();

				string name = component.Content.Master.Name;
				FlatPattern flatPattern = new FlatPattern(body, startFace, isDetectingCollisions, false, 0, 0, name);
				flatPattern.Render();

				DatumPlane datumPlane = DatumPlane.Create(flatPattern.FlatPart, Resources.AnnotationPlaneName, flatPattern.PaperPlane);
				datumPlane.Layer = planeLayer;
				PointUV center = flatPattern.PaperPlane.ProjectPoint(flatPattern.GetBoundingBox(Matrix.Identity).Center).Param;
				Note note = Note.Create(datumPlane, center, TextPoint.Center, 0.01, name);
				note.Layer = NoteHelper.CreateOrGetLayer(part.Document, Resources.AnnotationLayerName, System.Drawing.Color.DarkViolet);

				foreach (FlatBody flatBody in flatPattern.FlatBodies) {
					foreach (FlatFace flatFace in flatBody.FlatFaces) {
						foreach (ITrimmedCurve iTrimmedCurve in part.Curves.Select(c => c.Shape).Where(c => c.AreEndPointsOnFace(flatFace.SourceFace))) {
							var designCurve = DesignCurve.Create(flatBody.FlatPart, iTrimmedCurve);
							designCurve.Transform(flatFace.Transform);
							designCurve.Layer = curveLayer;
						}
					}
				}
			}
		}
	}

	class UnfoldVerifyPlanarButtonCapsule : RibbonButtonCapsule {
		public UnfoldVerifyPlanarButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("VerifyPlanar", Resources.UnfoldVerifyPlanarCommandText, null, Resources.UnfoldVerifyPlanarCommandHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Part part = Window.ActiveWindow.ActiveContext.Context as Part;
			ICollection<DesignBody> designBodies = part.GetDescendants<DesignBody>();
			Dictionary<string, int> tally = new Dictionary<string, int>();

			const string typePrefix = "SpaceClaim.Api.V10.Geometry.";

			foreach (DesignBody designBody in designBodies) {
				foreach (DesignFace face in designBody.Faces) {
					string typeName = face.Shape.Geometry.GetType().ToString();
					typeName = typeName.Substring(typePrefix.Length);

					if (tally.ContainsKey(typeName))
						tally[typeName]++;
					else
						tally[typeName] = 1;

					if (typeName != "Plane")
						NoteHelper.AnnotateFace(part, face, typeName);
				}
			}

			string output = string.Empty;
			foreach (string key in tally.Keys)
				output += string.Format("{0}: {1} | ", key, tally[key]);

			Application.ReportStatus(output, StatusMessageType.Information, null);
		}
	}

	class MergeComponentBodiesButtonCapsule : RibbonButtonCapsule {
		public MergeComponentBodiesButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("MergeComponents", "Merge Components", null, Resources.UnfoldVerifyPlanarCommandHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			InteractionContext activeContext = Window.ActiveWindow.ActiveContext;
			Part mainPart = Window.ActiveWindow.Scene as Part;

#if false
			foreach (Part part in mainPart.Components.Select(c => c.Content.Master).Where(p => p.Bodies.Count > 1)) {
				ICollection<Body> bodies = part.Bodies.Select(b => b.Shape).TryUnionBodies2();
				foreach (DesignBody desBody in part.Bodies)
					desBody.Delete();

				foreach (Body body in bodies)
					DesignBody.Create(part, "merged", body);
			}
#else
			foreach (IPart iPart in mainPart.Components.Select(c => c.Content).Where(p => p.Bodies.Count > 1)) {
                activeContext.Selection = iPart.Bodies.Cast<IDocObject>().ToArray();
				Command.Execute("IntersectTool"); //Combine
				//	System.Threading.Thread.Sleep(4000);
				Command.Execute("Select");
			}
#endif
		}
	}

	class ConvexHullButtonCapsule : RibbonButtonCapsule {
		public ConvexHullButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("ConvexHull", "Convex Hull", null, Resources.UnfoldVerifyPlanarCommandHint, parent, buttonSize) {
		}

        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Part mainPart = Window.ActiveWindow.Scene as Part;
			Layer layer = NoteHelper.CreateOrGetLayer(mainPart.Document, "Convex Hull", Color.DarkTurquoise);

			foreach (Part part in mainPart.Components.Select(c => c.Content.Master).Where(p => p.Bodies.Count > 0)) {
				foreach (Body body in part.Bodies.Select(b => b.Shape)) {
					foreach (Face face in body.Faces) {
						foreach (Loop loop in face.Loops.Where(l => l.IsOuter)) {
							List<Point> points = new List<Point>();
							foreach (Fin fin in loop.Fins) {
								bool wasPreviousLine = false;
								Line line = fin.Edge.Geometry as Line;
								if (line != null) {
									if (!wasPreviousLine)
										points.Add(fin.TrueStartPoint());

									points.Add(fin.TrueEndPoint());
									wasPreviousLine = true;
								}

								Circle circle = fin.Edge.Geometry as Circle;
								if (circle != null && fin.Edge.Bounds.Span < Math.PI)
									continue;

								ITrimmedCurve iTrimmedCurve = fin.Edge;
								double mid = (iTrimmedCurve.Bounds.Start + iTrimmedCurve.Bounds.End) / 2;
								points.Add(iTrimmedCurve.Geometry.Evaluate(mid).Point);
							}

							points.Add(points[0]);
							foreach (ITrimmedCurve iTrimmedCurve in points.CreatePolyline()) {
								DesignCurve desCurve = DesignCurve.Create(part, iTrimmedCurve);
								desCurve.Layer = layer;
							}
						}
					}
				}
			}

		}
	}


	//    class ConvexHullButtonCapsule : RibbonButtonCapsule {
	//        public ConvexHullButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
	//            : base("ConvexHull", "Convex Hull", null, Resources.UnfoldVerifyPlanarCommandHint, parent, buttonSize) {
	//        }

	//        protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
	//            InteractionContext context = Window.ActiveWindow.ActiveContext;
	//            Part mainPart = Window.ActiveWindow.Scene as Part;
	//            Layer layer = NoteHelper.CreateOrGetLayer(mainPart.Document, "Convex Hull", Color.DarkTurquoise);

	//            foreach (Part part in mainPart.Components.Select(c => c.Content.Master).Where(p => p.Bodies.Count > 0)) {
	//                foreach (Body body in part.Bodies.Select(b => b.Shape)) {
	//                    foreach (Face face in body.Faces) {
	//                        foreach (Loop loop in face.Loops.Where(l => l.IsOuter)) {
	//                            List<Point> points = new List<Point>();
	//                            foreach (Point[] finPoints in loop.Fins.Select(f => f.Edge.GetPolyline()).ToArray()) {
	//                                if (points.Count == 0) {
	//                                    points.AddRange(finPoints);
	//                                    continue;
	//                                }

	//                                if (points.Count == 0 || points[points.Count - 1] == finPoints[0]) {
	//                                    points.RemoveAt(points.Count - 1);
	//                                    points.AddRange(finPoints);
	//                                    continue;
	//                                }

	//                                if (points[points.Count - 1] == finPoints[finPoints.Length - 1]) {
	//                                    points.RemoveAt(points.Count - 1);
	//                                    points.AddRange(finPoints.Reverse());
	//                                    continue;
	//                                }
	//                            }

	//                            //List<Point> tempPoints = new List<Point>();
	//                            //tempPoints.Add(points[0]);
	//                            //for (int k = 1; k < points.Count; k++) {
	//                            //    if ((points[k] - tempPoints.Last()).Magnitude > Accuracy.LinearResolution * 1000)
	//                            //        tempPoints.Add(points[k]);
	//                            //}

	//                            //points = tempPoints;

	//#if true
	//                            points.Sort(new PointVerticalHorizontalComparer());
	//                            Point p0 = points[0];
	//                            points.RemoveAt(0);

	//                    //		for(int i = 0; i < points.Count - 1; i++)
	//                    //			deb;

	//                    //		Point 

	//                            int counter = points.Count + 1;
	//                            var hull = new List<Point>();
	//                            while (counter > 0 && points.Count > 2) {
	//                                Vector vec1 = points[1] - points[0];
	//                                Vector vec2 = points[2] - points[1];

	//                                if (vec1.Magnitude < Accuracy.LinearResolution) {
	//                                    DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(points[0])));
	//                                    DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(points[1])));
	//                                    points.RemoveAt(1);
	//                                    counter = points.Count + 1;
	//                                    continue;
	//                                }

	//                                if (vec2.Magnitude < Accuracy.LinearResolution) {
	//                                    DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(points[1])));
	//                                    DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(points[2])));
	//                                    points.RemoveAt(1);
	//                                    counter = points.Count + 1;
	//                                    continue;
	//                                }

	//                                double angle = Math.Atan2(v2.y, v2.x) - Math.Atan2(baseVec.y, v1.x);


	//                                Vector normal = Vector.Cross(vec1.Direction.UnitVector, vec2.Direction.UnitVector);

	//                                bool selfIntersects = false;

	//                                if (normal.Direction == Direction.DirZ) {
	//                                    points.Add(points[0]);
	//                                    points.RemoveAt(0);
	//                                    counter--;
	//                                }
	//                                else {
	//                                    tempPoints = points.ToList();
	//                                    tempPoints.RemoveAt(1);

	//                                    selfIntersects = false;
	//                                    try {
	//                                        Body.CreatePlanarBody(Plane.PlaneXY, tempPoints.CreatePolyline());
	//                                    }
	//                                    catch {
	//                                        selfIntersects = false;
	//                                    }

	//                                    if (selfIntersects) {
	//                                        points.Add(points[0]);
	//                                        points.RemoveAt(0);
	//                                    } 
	//                                    else
	//                                        points.RemoveAt(1);

	//                                    counter = points.Count + 2;
	//                                }

	//                            }

	//                            Debug.Assert(points.Count > 0);
	//                            IEnumerable<DesignCurve> desCurves = points.CreatePolyline().Select(c => DesignCurve.Create(part, c));
	//                            foreach (DesignCurve desCurve in desCurves)
	//                                desCurve.Layer = layer;
	//#else
	//                            List<Point> hull = new List<Point>();
	//                            int i = 0;
	//                            while (i < points.Count - 1) {
	//                                double maxAngle = 0;
	//                                Point maxPoint = points[i + 1];
	//                                Vector baseVector = points[i + 1] - points[i];
	//                                if (baseVector.Magnitude < Accuracy.LinearResolution * 100)
	//                                    continue;

	//                                int iOld = i;
	//                                for (int j = i + 2; j < points.Count; j++) {
	//                                    Vector vector = points[j] - points[i];
	//                                    if (vector.Magnitude < Accuracy.LinearResolution * 100)
	//                                        continue;

	//                                    Vector normal = Vector.Cross(baseVector.Direction.UnitVector, vector.Direction.UnitVector);
	//                                    double angle = Math.Atan2(v2.y, v2.x) - Math.Atan2(baseVec.y, v1.x);
	//                                    if (j == 2)
	//                                        maxAngle = angle;

	//                                    if (angle <= maxAngle && normal.Direction == Direction.DirZ) {
	//                                        maxAngle = angle;
	//                                        maxPoint = points[j];
	//                                        i = j;
	//                                    }
	//                                }

	//                                if (i == iOld) {
	//                                    hull.Add(points[0]);
	//                                    break;
	//                                }

	//                                hull.Add(maxPoint);
	//                            }
	//                            Debug.Assert(hull.Count > 0);
	//                            IEnumerable<DesignCurve> desCurves = hull.CreatePolyline().Select(c => DesignCurve.Create(part, c));
	//                            foreach (DesignCurve desCurve in desCurves)
	//                                desCurve.Layer = layer;
	//#endif

	//                        }
	//                    }
	//                }
	//            }

	//        }
	//    }

	public class PointVerticalHorizontalComparer : IComparer<Point> {
		public int Compare(Point a, Point b) {
			int comp = 0;
			if ((comp = Accuracy.CompareLengths(a.Y, b.Y)) != 0)
				return comp;

			return Accuracy.CompareLengths(a.X, b.X);
		}
	}
}

public class PointAngleComparer : IComparer<Point> {
	Point point;
	public PointAngleComparer(Point point) {
		this.point = point;
	}

	public int Compare(Point a, Point b) {
		//	aAngle
		int comp = 0;
		if ((comp = Accuracy.CompareLengths(a.Y, b.Y)) != 0)
			return comp;

		return Accuracy.CompareLengths(a.X, b.X);
	}

	public double AngleOf(Point p) {
		p = Point.Origin + (p - point);
		return Math.Atan2(p.Y, p.X);

	}


}
