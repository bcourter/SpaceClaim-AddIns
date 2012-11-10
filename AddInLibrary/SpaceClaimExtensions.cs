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
	public static class SpaceClaimExtensions {
		public static IEnumerable<IPart> WalkParts(this Part part) {  // Copied from SpaceClaim.Api.V10.Examples class ShowBomCapsule
			Debug.Assert(part != null);

			// GetDescendants goes not include the object itself
			yield return part;

			foreach (IPart descendant in part.GetDescendants<IPart>())
				yield return descendant;
		}

		public static Box GetBoundingBox(this IDocObject iDocObject, Matrix trans) {
			Box box = Box.Empty;
			foreach (Box shapeBox in iDocObject.GetDescendants<IHasTrimmedGeometry>().Select(t => t.Shape.GetBoundingBox(Matrix.Identity)))
				box |= shapeBox;

			return box;
		}

		public static ICollection<IDesignBody> GetAllSelectedIDesignBodies(this Window window) {
			var selectedBodies = new Dictionary<IDesignBody, byte>();  // dictionary used this way becomes a set of unique DesignBodies

			foreach (IDesignBody iDesignBody in window.ActiveContext.GetSelection<IDesignBody>())
				selectedBodies[iDesignBody] = 0;

			foreach (IDesignFace iDesignFace in window.ActiveContext.GetSelection<IDesignFace>())
				selectedBodies[iDesignFace.Parent] = 0;

			foreach (IDesignEdge iDesignEdge in window.ActiveContext.GetSelection<IDesignEdge>())
				selectedBodies[iDesignEdge.Parent] = 0;

			// TDB IDesignVertices not implemented in API

			return selectedBodies.Keys;
		}

		public static ICollection<DesignBody> GetAllSelectedDesignBodies(this Window window) {
			var selectedBodies = new Dictionary<DesignBody, byte>();  // dictionary used this way becomes a set of unique DesignBodies
			foreach (IDesignBody iDesBody in window.GetAllSelectedIDesignBodies())
				selectedBodies[iDesBody.Master] = 0;

			return selectedBodies.Keys;
		}

		public static ICollection<ITrimmedCurve> GetAllSelectedITrimmedCurves(this Window window) {
			Dictionary<ITrimmedCurve, byte> trimmedCurves = new Dictionary<ITrimmedCurve, byte>();  // dictionary used this way becomes a unique set

			foreach (IDesignBody iDesignBody in window.ActiveContext.GetSelection<IDesignBody>()) {
				foreach (IDesignEdge iDesignEdge in iDesignBody.Edges)
					trimmedCurves[iDesignEdge.Shape] = 0;
			}

			foreach (IDesignFace iDesignFace in window.ActiveContext.GetSelection<IDesignFace>()) {
				foreach (IDesignEdge iDesignEdge in iDesignFace.Edges)
					trimmedCurves[iDesignEdge.Shape] = 0;
			}

			foreach (IDesignEdge iDesignEdge in window.ActiveContext.GetSelection<IDesignEdge>())
				trimmedCurves[iDesignEdge.Shape] = 0;

			// TDB Vertices not implemented in API

			foreach (IDesignCurve iDesignCurve in window.ActiveContext.GetSelection<IDesignCurve>())
				trimmedCurves[iDesignCurve.Shape] = 0;

			return trimmedCurves.Keys;
		}

		public static ICollection<IDesignFace> GetAllSelectedIDesignFaces(this Window window) {
			Dictionary<IDesignFace, byte> iDesignFaces = new Dictionary<IDesignFace, byte>();  // dictionary used this way becomes a unique set

			foreach (IDesignBody iDesignBody in window.ActiveContext.GetSelection<IDesignBody>()) {
				foreach (IDesignFace iDesignFace in iDesignBody.Master.Faces)
					iDesignFaces[iDesignFace] = 0;
			}

			foreach (IDesignFace iDesignFace in window.ActiveContext.GetSelection<IDesignFace>()) {
				iDesignFaces[iDesignFace] = 0;
			}

			return iDesignFaces.Keys;
		}

		public static Color GetVisibleColor(this IDesignBody iDesignBody) {
			return iDesignBody.Master.GetColor(null) ?? iDesignBody.Master.Layer.GetColor(null);
		}

		public static Color GetVisibleColor(this IDesignCurve iDesignCurve) {
			return iDesignCurve.Master.GetColor(null) ?? iDesignCurve.Master.Layer.GetColor(null);
		}

		public static Dictionary<Layer, List<CurveSegment>> GetCurvesByLayer(this IComponent iComponent) {
			var objectsOnLayer = new Dictionary<Layer, List<CurveSegment>>();
			Matrix trans = iComponent.TransformToMaster.Inverse;
			foreach (IDesignCurve iDesCurve in iComponent.Content.GetDescendants<IDesignCurve>()) {
				Layer layer = iDesCurve.Master.Layer;
				if (!objectsOnLayer.ContainsKey(layer))
					objectsOnLayer[layer] = new List<CurveSegment>();

				objectsOnLayer[layer].Add(((CurveSegment) iDesCurve.Shape).CreateTransformedCopy(trans));
			}

			return objectsOnLayer;
		}

		public static Dictionary<Layer, List<CurveSegment>> GetCurvesByLayer(this Part part) {
			var objectsOnLayer = new Dictionary<Layer, List<CurveSegment>>();
			foreach (IDesignCurve iDesCurve in part.GetDescendants<IDesignCurve>()) {
				Layer layer = iDesCurve.Master.Layer;
				if (!objectsOnLayer.ContainsKey(layer))
					objectsOnLayer[layer] = new List<CurveSegment>();

				objectsOnLayer[layer].Add(((CurveSegment) iDesCurve.Shape).CreateTransformedCopy(Matrix.Identity));
			}

			return objectsOnLayer;
		}

		public static ICollection<CurveSegment> Transform(this ICollection<CurveSegment> curves, Matrix trans) {
			var resultCurves = new List<CurveSegment>();
			foreach (CurveSegment curve in curves)
				resultCurves.Add(curve.CreateTransformedCopy(trans));
		
			return resultCurves;
		}

		//public static Dictionary<Layer, List<T>> GetSortedByLayer<T>(this IPart part) where T : class, IHasLayer, IDocObject, ITransformable {
		//    var objectsOnLayer = new Dictionary<Layer, List<T>>();
		//    foreach (T t in part.GetDescendants<T>()) {
		//        if (!objectsOnLayer.ContainsKey(t.Layer))
		//            objectsOnLayer[t.Layer] = new List<T>();

		//        objectsOnLayer[t.Layer].Add(t.co);
		//    }

		//    return objectsOnLayer;
		//}
	}
}
