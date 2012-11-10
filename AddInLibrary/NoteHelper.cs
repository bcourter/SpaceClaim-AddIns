using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary.Properties;

/// <summary>
/// AddIn helper functions and other utilities
/// </summary>
 
namespace SpaceClaim.AddInLibrary {
	public static class NoteHelper {
		public static PointUV GetNoteLocation(INote note, HorizontalType horizontalType, VerticalType verticalType) {
			double U = 0, V = 0;

			if (horizontalType == HorizontalType.Left)
				U = note.GetLocation(TextPoint.LeftSide).U;
			else if (horizontalType == HorizontalType.Right)
				Debug.Fail("Note width not implemented");
			else if (horizontalType == HorizontalType.Center)
				Debug.Fail("Note width not implemented");
			else
				Debug.Fail("Case not handled");

			if (verticalType == VerticalType.Bottom)
                V = note.GetLocation(TextPoint.BottomSide).V;
			else if (verticalType == VerticalType.Top)
				Debug.Fail("Note height not implemented");
			else if (verticalType == VerticalType.Middle)
				Debug.Fail("Note height not implemented");
			else
				Debug.Fail("Case not handled");

			return PointUV.Create(U, V);
		}

		public static void AnnotateFace(Part targetPart, DesignFace face, string comment, double textHeight, Direction? direction) {
			string annotationPlanePartName = "Annotation Planes";
			Part part = null;
			foreach (Part testPart in targetPart.Document.Parts) {
				if (testPart.Name == annotationPlanePartName)
					part = testPart;
			}

			if (part == null) {
				part = Part.Create(targetPart.Document, annotationPlanePartName);
				Component component = Component.Create(targetPart, part);
			}

			Plane plane = GetPlaneCenteredOnFace(face);
			if (direction != null) {
				Direction dirX = Direction.DirX;
				if (direction.Value != Direction.DirZ)
					dirX = direction.Value.ArbitraryPerpendicular;

				plane = Plane.Create(Frame.Create(plane.Frame.Origin, dirX, Direction.Cross(direction.Value, dirX)));
			}

			Layer planeLayer = CreateOrGetLayer(targetPart.Document, Resources.AnnotationPlaneLayerNameText, System.Drawing.Color.Black);
			planeLayer.SetVisible(null, false);
			Layer noteLayer = CreateOrGetLayer(targetPart.Document, Resources.AnnotationLayerNameText, System.Drawing.Color.DarkRed);

			DatumPlane datum = DatumPlane.Create(part, Resources.AnnotationPlaneNameText, plane);
			datum.Layer = planeLayer;

			PointUV location = PointUV.Origin;
			Note note = Note.Create(datum, location, TextPoint.Center, textHeight, comment);

			note.SetFontName(DraftingFont.Asme);
			note.Layer = noteLayer;
			note.SetColor(null, null);
		}

		public static void AnnotateFace(Part targetPart, DesignFace face, string comment) {
			AnnotateFace(targetPart, face, comment, 0.001, null);
		}

		public static Plane GetPlaneCenteredOnFace(DesignFace face) {
			PointUV centerUV = face.Shape.BoxUV.Center;
			SurfaceEvaluation evaluation = face.Shape.Geometry.Evaluate(centerUV);
			Point point = evaluation.Point;
			Direction dirU = (face.Shape.Geometry.Evaluate(PointUV.Create(centerUV.U + 0.001, centerUV.V)).Point.Vector
				- face.Shape.Geometry.Evaluate(PointUV.Create(centerUV.U - 0.001, centerUV.V)).Point.Vector).Direction;
			Direction dirVTemp = Direction.Cross(evaluation.Normal, dirU);

			if (face.Shape.Geometry as Plane != null) {
				if (!Accuracy.Equals(Vector.Dot(dirU.UnitVector, Direction.DirZ.UnitVector), 0.0)) { // if our U direction has a Z component, the text might be slanted
					if (Accuracy.Equals(Vector.Dot(dirVTemp.UnitVector, Direction.DirZ.UnitVector), 0.0))  // if our V direction has no Z component, use it
						dirU = dirVTemp;
				}
			}

			if (face.Shape.Geometry as Cylinder != null)
				dirU = dirVTemp;

			dirVTemp = Direction.Cross(evaluation.Normal, dirU);
			if (Vector.Dot(dirVTemp.UnitVector, Direction.DirZ.UnitVector) < 0.0)  // Prevent upside-down notes
				dirU = -dirU;

			return Plane.Create(Frame.Create(point, dirU, Direction.Cross(evaluation.Normal, dirU)));
		}

		static public Layer CreateOrGetLayer(Document doc, string name, System.Drawing.Color color) {
			return doc.GetLayer(name) ?? Layer.Create(doc, name, color);
		}

		public enum VerticalType {
			Bottom,
			Top,
			Middle
		}

		public enum HorizontalType {
			Left,
			Right,
			Center
		}
	}

}
