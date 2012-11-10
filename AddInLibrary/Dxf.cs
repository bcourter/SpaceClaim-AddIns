// references: http://www.codeproject.com/KB/files/dxffiles.aspx
// http://www.autodesk.com/techpubs/autocad/acad2000/dxf/general_dxf_file_structure_dxf_aa.htm

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using System.IO;
using SpaceClaim.AddInLibrary;
using Color = System.Drawing.Color;

/// <summary>
/// Simple Dxf Writer
/// </summary>

namespace SpaceClaim.Dxf {
	public class Document {
		const double degrees = 180 / Math.PI;

		string path;
		StreamWriter textWriter;

		List<DxfCurve> dxfCurves = new List<DxfCurve>();

		public Document(string path) {
			this.path = path;
			textWriter = new StreamWriter(path);
		}

		public void AddCurve(ITrimmedCurve iTrimmedCurve) {
			if (iTrimmedCurve.Geometry is Line) {
				dxfCurves.Add(new DxfLine(iTrimmedCurve, textWriter));
				return;
			}

			if (iTrimmedCurve.Geometry is Circle) {
				if (iTrimmedCurve.Bounds.Span == 2 * Math.PI)
					dxfCurves.Add(new DxfCircle(iTrimmedCurve, textWriter));
				else
					dxfCurves.Add(new DxfArc(iTrimmedCurve, textWriter));

				return;
			}

			IList<Point> points = iTrimmedCurve.Geometry.GetPolyline(iTrimmedCurve.Bounds);
			for (int i = 0; i < points.Count - 1; i++)
				dxfCurves.Add(new DxfLine(CurveSegment.Create(points[i], points[i + 1]), textWriter));
		}

		public void SaveDxf() {
			WriteStartDocument();
			foreach (DxfCurve dxfCurve in dxfCurves)
				dxfCurve.Write();

			WriteEndDocument();
			textWriter.Close();
		}

		private abstract class DxfCurve {
			protected ITrimmedCurve iTrimmedCurve;
			protected TextWriter textWriter;

			public DxfCurve(ITrimmedCurve iTrimmedCurve, TextWriter textWriter) {
				this.iTrimmedCurve = iTrimmedCurve;
				this.textWriter = textWriter;
			}

			public abstract void Write();
		}

		private class DxfLine : DxfCurve {
			public DxfLine(ITrimmedCurve iTrimmedCurve, TextWriter textWriter)
				: base(iTrimmedCurve, textWriter) {
			}

			public override void Write() {
				Debug.Assert(iTrimmedCurve.Geometry as Line != null);

				textWriter.WriteLine("0");
				textWriter.WriteLine("LINE");
				textWriter.WriteLine("8");	// Group code for layer name
				textWriter.WriteLine("0");	// Layer number

				textWriter.WriteLine("10");	// Start point of line
				textWriter.WriteLine(iTrimmedCurve.StartPoint.X);	// X in WCS coordinates
				textWriter.WriteLine("20");
				textWriter.WriteLine(iTrimmedCurve.StartPoint.Y);	// Y in WCS coordinates
				textWriter.WriteLine("30");
				textWriter.WriteLine(0);	// Z in WCS coordinates

				textWriter.WriteLine("11");	// End point of line
				textWriter.WriteLine(iTrimmedCurve.EndPoint.X);	// X in WCS coordinates
				textWriter.WriteLine("21");
				textWriter.WriteLine(iTrimmedCurve.EndPoint.Y);	// X in WCS coordinates
				textWriter.WriteLine("31");
				textWriter.WriteLine(0);	// X in WCS coordinates
			}
		}

		private class DxfCircle : DxfCurve {
			protected Circle circle;
			public DxfCircle(ITrimmedCurve iTrimmedCurve, TextWriter textWriter)
				: base(iTrimmedCurve, textWriter) {
				circle = iTrimmedCurve.Geometry as Circle;
				Debug.Assert(circle != null);
			}

			public override void Write() {
				textWriter.WriteLine("0");
				textWriter.WriteLine("CIRCLE");
				textWriter.WriteLine("8");	// Group code for layer name
				textWriter.WriteLine("0");	// Layer number

				textWriter.WriteLine("10");	// Center point of circle
				textWriter.WriteLine(circle.Frame.Origin.X);	// X in OCS coordinates
				textWriter.WriteLine("20");
				textWriter.WriteLine(circle.Frame.Origin.Y);	// Y in OCS coordinates
				textWriter.WriteLine("30");
				textWriter.WriteLine(0);	// Z in OCS coordinates
				textWriter.WriteLine("40");	// radius of circle
				textWriter.WriteLine(circle.Radius);
			}
		}

		private class DxfArc : DxfCircle {
			public DxfArc(ITrimmedCurve iTrimmedCurve, TextWriter textWriter)
				: base(iTrimmedCurve, textWriter) {
			}

			public override void Write() {
				textWriter.WriteLine("0");
				textWriter.WriteLine("ARC");
				textWriter.WriteLine("8");	// Group code for layer name
				textWriter.WriteLine("0");	// Layer number

				textWriter.WriteLine("10");	// Center point of circle
				textWriter.WriteLine(circle.Frame.Origin.X);	// X in OCS coordinates
				textWriter.WriteLine("20");
				textWriter.WriteLine(circle.Frame.Origin.Y);	// Y in OCS coordinates
				textWriter.WriteLine("30");
				textWriter.WriteLine(0);	// Z in OCS coordinates
				textWriter.WriteLine("40");	// radius of circle
				textWriter.WriteLine(circle.Radius);

				Point startPoint, endPoint;
				if (circle.Frame.DirZ.Z > 0) {  //TBD this only works in 2D
					startPoint = iTrimmedCurve.StartPoint;
					endPoint = iTrimmedCurve.EndPoint;
				}
				else {
					startPoint = iTrimmedCurve.EndPoint;
					endPoint = iTrimmedCurve.StartPoint;
				}

				textWriter.WriteLine("50");	// radius of circle
				textWriter.WriteLine((startPoint - circle.Frame.Origin).Direction.UnitVector.AngleInFrame(Frame.World) * degrees);
				textWriter.WriteLine("51");	// radius of circle
				textWriter.WriteLine((endPoint - circle.Frame.Origin).Direction.UnitVector.AngleInFrame(Frame.World) * degrees);
			}
		}

		private void WriteStartDocument() {
			textWriter.WriteLine("0");
			textWriter.WriteLine("SECTION");
			textWriter.WriteLine("2");
			textWriter.WriteLine("ENTITIES");
		}

		private void WriteEndDocument() {
			textWriter.WriteLine("0");
			textWriter.WriteLine("ENDSEC");
			textWriter.WriteLine("0");
			textWriter.WriteLine("EOF");
		}
	}
}
