using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using System.Xml;
using SpaceClaim.AddInLibrary;
using Color = System.Drawing.Color;

/// <summary>
/// AddIn helper functions and other utilities
/// </summary>

namespace SpaceClaim.Svg {
	public class Document {
		const double mm = 1000;
		const double pt = (double) 127 / 360 / mm;

		string path;
		XmlWriter xmlWriter;

		List<Path> paths = new List<Path>();

		public Document(string path) {
			this.path = path;

			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Indent = true;

			xmlWriter = XmlWriter.Create(path, settings);
		}

		public void AddPath(IList<ITrimmedCurve> iTrimmedCurves, bool isClosed, double strokeWidth, Color? strokeColor, Color? fillColor) {
			Path path;
			if (iTrimmedCurves.Count == 1 && (iTrimmedCurves[0].Geometry is Circle || iTrimmedCurves[0].Geometry is Ellipse) && iTrimmedCurves[0].Bounds.Span == Math.PI * 2) {
				Ellipse ellipse = iTrimmedCurves[0].Geometry as Ellipse;

				if (ellipse == null)
					ellipse = ((Circle) iTrimmedCurves[0].Geometry).AsEllipse();

				Debug.Assert(ellipse != null);

				path = new EllipsePath(xmlWriter, ellipse);
			}
			else {
				path = new BasicPath(xmlWriter, iTrimmedCurves);
				path.IsClosed = isClosed;
			}

			path.StrokeWidth = strokeWidth;
			path.StrokeColor = strokeColor;
			path.FillColor = fillColor;
			paths.Add(path);
		}

		public void AddPath(IList<Point> points, bool isClosed, double strokeWidth, Color? strokeColor, Color? fillColor) {
			List<ITrimmedCurve> iTrimmedCurves = new List<ITrimmedCurve>();
			for (int i = 0; i < points.Count - 1; i++)
				iTrimmedCurves.Add(CurveSegment.Create(points[i], points[i + 1]));

			AddPath(iTrimmedCurves, isClosed, strokeWidth, strokeColor, fillColor);
		}

		public void SaveXml() {
			Box boundingBox = Box.Create(Point.Origin);
			if (paths.Count > 0)
				boundingBox = paths[0].GetBoundingBox();

			foreach (Path path in paths)
				boundingBox |= path.GetBoundingBox();

			Matrix documentTrans = Matrix.CreateScale(1 / pt) * Matrix.CreateTranslation(Vector.Create(-boundingBox.MinCorner.X, -boundingBox.MaxCorner.Y, 0));

			xmlWriter.WriteStartDocument();

			xmlWriter.WriteComment("SVG written from SpaceClaim using AECollection Unfold Add-In");
			xmlWriter.WriteDocType("svg", "-//W3C//DTD SVG 1.1//EN http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd", null, null);

			xmlWriter.WriteStartElement("svg", "http://www.w3.org/2000/svg");


			// SVG doesn't support meters as a unit, so we'll convert everything to mm
			xmlWriter.WriteAttributeString("width", string.Format("{0}mm", boundingBox.Size.X * mm));
			xmlWriter.WriteAttributeString("height", string.Format("{0}mm", boundingBox.Size.Y * mm));
			xmlWriter.WriteAttributeString("version", "1.1");
			xmlWriter.WriteStartElement("g");
			// Set the transform on the group to transform to SVG's left-handed coordinate system. Would be nice if we could set the transform here instead of passing documentTrans around, but we can't because Illustrator doesn't scale line thickness, but Firefox, Chrome, and Inkscape do.  At the same time, doing so messed up inkscape's arcs.  This was the best workaround I could come up with.
			xmlWriter.WriteAttributeString("transform", string.Format("scale({0},{1})", 1, -1));
			xmlWriter.WriteAttributeString("style", string.Format("fill-rule:nonzero; stroke-linecap:round; stroke-linejoin:round;"));

			foreach (Path path in paths)
				path.WriteXml(documentTrans);

			xmlWriter.WriteEndElement(); // <g>
			xmlWriter.WriteEndElement(); // <svg>
			xmlWriter.WriteEndDocument();
			xmlWriter.Close();
		}

		private abstract class Path {
			protected XmlWriter xmlWriter;

			protected double strokeWidth = 1;
			protected Color? strokeColor = Color.Black;
			protected Color? fillColor = null;
			protected bool isClosed = true;

			public Path(XmlWriter xmlWriter) {
				this.xmlWriter = xmlWriter;
			}

			public abstract void WriteXml(Matrix documentTrans);

			public abstract Box GetBoundingBox();

			protected void StyleAttributes(Color? strokeColor, Color? fillColor, double strokeWidth) {
				xmlWriter.WriteAttributeString("style", string.Format("stroke:{0}; stroke-opacity:{1}; stroke-width:{2}mm; fill:{3}; fill-opacity:{4}",
					strokeColor == null ? "none" : RgbColor(strokeColor.Value),
					strokeColor == null ? 255 : (double) strokeColor.Value.A / 255,
					strokeWidth * mm,
					fillColor == null ? "none" : RgbColor(fillColor.Value),
					fillColor == null ? 255 : (double) fillColor.Value.A / 255
				));
			}

			protected string RgbColor(Color color) {
				return string.Format("rgb({0},{1},{2})", color.R, color.G, color.B);
			}

			public double StrokeWidth {
				get { return strokeWidth; }
				set { strokeWidth = value; }
			}

			public Color? StrokeColor {
				get { return strokeColor; }
				set { strokeColor = value; }
			}

			public Color? FillColor {
				get { return fillColor; }
				set { fillColor = value; }
			}

			public bool IsClosed {
				get { return isClosed; }
				set { isClosed = value; }
			}
		}

		private class BasicPath : Path {
			IList<ITrimmedCurve> iTrimmedCurves;

			public BasicPath(XmlWriter xmlWriter, IList<ITrimmedCurve> iTrimmedCurves)
				: base(xmlWriter) {
				this.iTrimmedCurves = iTrimmedCurves;
			}

			public override void WriteXml(Matrix documentTrans) {
				Debug.Assert(iTrimmedCurves != null);
				Debug.Assert(iTrimmedCurves.Count > 0);

				string pathData = MoveTo(documentTrans * iTrimmedCurves[0].StartPoint);

				foreach (ITrimmedCurve curve in iTrimmedCurves) {
					ITrimmedCurve iTrimmedCurve = curve;
					Line line = iTrimmedCurve.Geometry as Line;
					if (line != null) {
						pathData += LineTo(documentTrans * iTrimmedCurve.EndPoint);
						continue;
					}

					if (iTrimmedCurve.Geometry is Circle || iTrimmedCurve.Geometry is Ellipse) {
						ITrimmedCurve iTrimmedEllipse = iTrimmedCurve.ProjectToPlane(Plane.PlaneXY);

						Ellipse ellipse = iTrimmedEllipse.Geometry as Ellipse;
						if (ellipse == null)
							ellipse = ((Circle) iTrimmedEllipse.Geometry).AsEllipse();

						Debug.Assert(ellipse != null);
						Debug.Assert(iTrimmedEllipse.Bounds.Span > 0);  // interval should always be positive for SC ellipses
						Debug.Assert(!iTrimmedEllipse.IsReversed);

						double majorRadius = documentTrans.Scale * Math.Max(ellipse.MajorRadius, ellipse.MinorRadius);
						double minorRadius = documentTrans.Scale * Math.Min(ellipse.MajorRadius, ellipse.MinorRadius);

						double x = Vector.Dot(ellipse.Frame.DirX.UnitVector, Direction.DirX.UnitVector);
						double y = Vector.Dot(ellipse.Frame.DirX.UnitVector, Direction.DirY.UnitVector);

						pathData += ArcTo(
							majorRadius, minorRadius,
							Math.Atan2(y, x) * 180 / Math.PI,
							iTrimmedEllipse.Bounds.Span > Math.PI,
							Direction.Equals(Direction.DirZ, ellipse.Frame.DirZ),
							documentTrans * iTrimmedEllipse.EndPoint
						);

						continue;
					}

					NurbsCurve nurbsCurve = iTrimmedCurve.Geometry as NurbsCurve;
					if (nurbsCurve == null) {
						ProceduralCurve proceduralCurve = iTrimmedCurve.Geometry as ProceduralCurve;
						if (proceduralCurve != null)
							nurbsCurve = proceduralCurve.AsSpline(iTrimmedCurve.Bounds);
					}

					if (nurbsCurve != null) {
						Debug.Assert(nurbsCurve.Data.Degree == 3);

						SelectFragmentResult result = null;
						CurveSegment fullNurbsCurve = CurveSegment.Create(nurbsCurve);

						List<ITrimmedCurve> pointCurves = nurbsCurve.Data.Knots
							.Where(k => iTrimmedCurve.Bounds.Contains(k.Parameter))
							.Select(k => ((ITrimmedCurve) (nurbsCurve.Evaluate(k.Parameter).Point.AsCurveSegment())))
							.ToList<ITrimmedCurve>();

						pointCurves.Insert(0, nurbsCurve.Evaluate(iTrimmedCurve.Bounds.Start).Point.AsCurveSegment());
						pointCurves.Add(nurbsCurve.Evaluate(iTrimmedCurve.Bounds.End).Point.AsCurveSegment());

						for (int i = 0; i < pointCurves.Count - 1; i++) {
							//try {
							result = fullNurbsCurve.SelectFragment((
								nurbsCurve.ProjectPoint(pointCurves[i].StartPoint).Param +
								nurbsCurve.ProjectPoint(pointCurves[i + 1].StartPoint).Param
							) / 2, pointCurves);
							//}
							//catch {
							//    Debug.Assert(false, "Attempted to trim curve out of bounds.");
							//    WriteBlock.ExecuteTask("SVG Exception Curves", () => DesignCurve.Create(Window.ActiveWindow.Scene as Part, iTrimmedCurve));
							//    break;
							//}

							if (result != null) {
								NurbsCurve fragment = result.SelectedFragment.Geometry as NurbsCurve;
								WriteBlock.ExecuteTask("curve", delegate { fragment.Print(); });
								pathData += CubicBézierTo(documentTrans * fragment.ControlPoints[1].Position, documentTrans * fragment.ControlPoints[2].Position, documentTrans * fragment.ControlPoints[3].Position);
							}
						}

						continue;
					}

					PointCurve pointCurve = iTrimmedCurve.Geometry as PointCurve;
					if (pointCurve != null)
						continue;

					// Handle Polygons, which are not in the API
					foreach (Point point in iTrimmedCurve.Geometry.GetPolyline(iTrimmedCurve.Bounds).Skip(1))
						pathData += LineTo(documentTrans * point);

					continue;

					//		throw new NotSupportedException("Unhandled iTrimmedCurve");
				}

				if (isClosed)
					pathData += ClosePath();

				xmlWriter.WriteStartElement("path");
				xmlWriter.WriteAttributeString("d", pathData);
				StyleAttributes(strokeColor, fillColor, strokeWidth);
				xmlWriter.WriteEndElement();
			}

			public override Box GetBoundingBox() {
				Box box = iTrimmedCurves[0].GetBoundingBox(Matrix.Identity);
				foreach (ITrimmedCurve iTrimmedCurve in iTrimmedCurves)
					box |= iTrimmedCurve.GetBoundingBox(Matrix.Identity);

				return box;
			}

			private string MoveTo(Point point) {
				return string.Format("M{0},{1} ", point.X, point.Y);
			}

			private string LineTo(Point point) {
				return string.Format("L{0},{1} ", point.X, point.Y);
			}

			private string CubicBézierTo(Point controlPoint1, Point controlPoint2, Point endPoint) {
				return string.Format("C{0},{1} {2},{3} {4},{5} ", controlPoint1.X, controlPoint1.Y, controlPoint2.X, controlPoint2.Y, endPoint.X, endPoint.Y);
			}

			private string ArcTo(double rX, double rY, double xAxisRotation, bool isLargeArc, bool isSweep, Point point) {
				return string.Format("A{0},{1} {2} {3},{4} {5},{6} ", rX, rY, xAxisRotation, isLargeArc ? 1 : 0, isSweep ? 1 : 0, point.X, point.Y);
			}

			private string ClosePath() {
				return "Z ";
			}
		}

		private class EllipsePath : Path {
			Ellipse ellipse;

			public EllipsePath(XmlWriter xmlWriter, Ellipse ellipse)
				: base(xmlWriter) {
				this.ellipse = ellipse;
			}

			public override void WriteXml(Matrix documentTrans) {
				Debug.Assert(this.ellipse != null);
				ITrimmedCurve iTrimmedEllipse = CurveSegment.Create(this.ellipse).ProjectToPlane(Plane.PlaneXY);
				Ellipse ellipse = iTrimmedEllipse.Geometry as Ellipse;

				Debug.Assert(ellipse != null);

				double x = Vector.Dot(ellipse.Frame.DirX.UnitVector, Direction.DirX.UnitVector);
				double y = Vector.Dot(ellipse.Frame.DirX.UnitVector, Direction.DirY.UnitVector);

				xmlWriter.WriteStartElement("ellipse");
				xmlWriter.WriteAttributeString("transform", string.Format("translate({0} {1}) rotate({2})",
					documentTrans.Scale * ellipse.Frame.Origin.X + documentTrans.Translation.X,
					documentTrans.Scale * ellipse.Frame.Origin.Y + documentTrans.Translation.Y,
					Math.Atan2(y, x) * 180 / Math.PI
				));

				xmlWriter.WriteAttributeString("rx", (documentTrans.Scale * ellipse.MajorRadius).ToString());
				xmlWriter.WriteAttributeString("ry", (documentTrans.Scale * ellipse.MinorRadius).ToString());
				StyleAttributes(strokeColor, fillColor, strokeWidth);
				xmlWriter.WriteEndElement();
			}

			public override Box GetBoundingBox() {
				return CurveSegment.Create(ellipse).GetBoundingBox(Matrix.Identity);
			}

		}

	}
}
