using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;

namespace SpaceClaim.AddInLibrary {
	public class TrimmedCurveChain {
		List<OrientedTrimmedCurve> orientedCurves = new List<OrientedTrimmedCurve>();

		public TrimmedCurveChain(ICollection<ITrimmedCurve> curveCollection) {
            if (curveCollection.Count == 0)
                return;

			Queue<ITrimmedCurve> curveQueue = new Queue<ITrimmedCurve>(curveCollection);

			orientedCurves.Add(new OrientedTrimmedCurve(curveQueue.Dequeue(), false));
			ITrimmedCurve trimmedCurve = null;
			int failCount = curveQueue.Count;
			while (curveQueue.Count > 0) {
				trimmedCurve = curveQueue.Dequeue();

				if (trimmedCurve.StartPoint == orientedCurves[orientedCurves.Count - 1].EndPoint) {
					orientedCurves.Add(new OrientedTrimmedCurve(trimmedCurve, false));
					failCount = curveQueue.Count;
					continue;
				}

				if (trimmedCurve.EndPoint == orientedCurves[orientedCurves.Count - 1].EndPoint) {
					orientedCurves.Add(new OrientedTrimmedCurve(trimmedCurve, true));
					failCount = curveQueue.Count;
					continue;
				}

				if (trimmedCurve.StartPoint == orientedCurves[0].StartPoint) {
					orientedCurves.Insert(0, new OrientedTrimmedCurve(trimmedCurve, true));
					failCount = curveQueue.Count;
					continue;
				}

				if (trimmedCurve.EndPoint == orientedCurves[0].StartPoint) {
					orientedCurves.Insert(0, new OrientedTrimmedCurve(trimmedCurve, false));
					failCount = curveQueue.Count;
					continue;
				}

				curveQueue.Enqueue(trimmedCurve);
				if (failCount-- < 0) {
					Application.ReportStatus("Can't seem to sort curves.", StatusMessageType.Warning, null);
					break;
				}
			}
		}

        public static ICollection<TrimmedCurveChain> GatherLoops(ICollection<ITrimmedCurve> curveCollection) {
            Queue<ITrimmedCurve> curveQueue = new Queue<ITrimmedCurve>(curveCollection);
            var loops = new List<TrimmedCurveChain>();
            var chain = new TrimmedCurveChain(new List<ITrimmedCurve>());
            loops.Add(chain);
            chain.Curves.Add(new OrientedTrimmedCurve(curveQueue.Dequeue(), false));

            ITrimmedCurve trimmedCurve = null;
            int failCount = curveQueue.Count;
            while (curveQueue.Count > 0) {
                trimmedCurve = curveQueue.Dequeue();

                if (trimmedCurve.StartPoint == chain.Curves[chain.Curves.Count - 1].EndPoint) {
                    chain.Curves.Add(new OrientedTrimmedCurve(trimmedCurve, false));
                    failCount = curveQueue.Count;
                    continue;
                }

                if (trimmedCurve.EndPoint == chain.Curves[chain.Curves.Count - 1].EndPoint) {
                    chain.Curves.Add(new OrientedTrimmedCurve(trimmedCurve, true));
                    failCount = curveQueue.Count;
                    continue;
                }

                if (trimmedCurve.StartPoint == chain.Curves[0].StartPoint) {
                    chain.Curves.Insert(0, new OrientedTrimmedCurve(trimmedCurve, true));
                    failCount = curveQueue.Count;
                    continue;
                }

                if (trimmedCurve.EndPoint == chain.Curves[0].StartPoint) {
                    chain.Curves.Insert(0, new OrientedTrimmedCurve(trimmedCurve, false));
                    failCount = curveQueue.Count;
                    continue;
                }

                curveQueue.Enqueue(trimmedCurve);
                if (failCount-- < 0) {
                    chain = new TrimmedCurveChain(new List<ITrimmedCurve>());
                    loops.Add(chain);
                    chain.Curves.Add(new OrientedTrimmedCurve(curveQueue.Dequeue(), false));
                }
            }

            return loops;
        }

		public double Length {
			get {
				double length = 0;
				foreach (OrientedTrimmedCurve orientedCurve in orientedCurves)
					length += orientedCurve.Length;

				return length;
			}
		}

		public Interval Bounds {
			get { return Interval.Create(0, Length); }
		}

		public Point StartPoint {
			get { return orientedCurves[0].StartPoint; }
		}

		public Point EndPoint {
			get { return orientedCurves[orientedCurves.Count - 1].EndPoint; }
		}

		public IList<OrientedTrimmedCurve> Curves {
			get { return orientedCurves; }
		}

		public IList<ITrimmedCurve> SortedCurves {
			get { return Curves.Select(c => c.TrimmedCurve).ToArray(); }
		}

		public void Reverse() {
			orientedCurves.Reverse();
			foreach (OrientedTrimmedCurve curve in orientedCurves)
				curve.IsReversed = !curve.IsReversed;
		}

		public bool TryGetPointAlongCurve(double param, out Point point) {
			point = Point.Origin;
			double travelled = 0;

			int i;
			for (i = 0; i < orientedCurves.Count; i++) {
				double length = orientedCurves[i].Length;
				if (param - travelled < length)
					break;

				travelled += length;
			}

			double offsetParam;

			if (i == orientedCurves.Count) { // handles the endpoint case
				CurveEvaluation curveEval = orientedCurves[i - 1].TrimmedCurve.Geometry.Evaluate(orientedCurves[i - 1].Bounds.End);
				point = curveEval.Point;
				return true;
			}

			if (orientedCurves[i].TryOffsetAlongCurve(orientedCurves[i].Bounds.Start, param - travelled, out offsetParam)) {
				CurveEvaluation curveEval = orientedCurves[i].TrimmedCurve.Geometry.Evaluate(offsetParam);
				point = curveEval.Point;
				return true;
			}

			return false;
		}
	}

	public class OrientedTrimmedCurve { //: ITrimmedCurve{
		ITrimmedCurve trimmedCurve;

		public OrientedTrimmedCurve(ITrimmedCurve trimmedCurve, bool isReversed) {
			this.trimmedCurve = trimmedCurve;
			IsReversed = isReversed;
		}

		public ITrimmedCurve OriginalTrimmedCurve {
			get { return trimmedCurve; }
		}

		public ITrimmedCurve TrimmedCurve {
			get {
				if (IsReversed)
					return trimmedCurve.GetReverse();

				return trimmedCurve;
			}
		}

		public bool IsReversed { get; set; }

		#region ITrimmedCurve Members

		public double Length {
			get { return trimmedCurve.Length; }
		}

		public Point StartPoint {
			get { return IsReversed ? trimmedCurve.EndPoint : trimmedCurve.StartPoint; }
		}

		public Point EndPoint {
			get { return IsReversed ? trimmedCurve.StartPoint : trimmedCurve.EndPoint; }
		}

		public Interval Bounds {
			get {
				return IsReversed ?
					Interval.Create(trimmedCurve.Bounds.End, trimmedCurve.Bounds.Start) :
					trimmedCurve.Bounds
				;
			}
		}

		public CurveEvaluation ProjectPoint(Point point) {
			return trimmedCurve.ProjectPoint(point);
		}

		public Curve Geometry {
			get { return trimmedCurve.Geometry; }
		}

		public TFilter GetGeometry<TFilter>() where TFilter : Curve {
			return trimmedCurve.GetGeometry<TFilter>() as TFilter;
		}

		#endregion

		public bool TryOffsetAlongCurve(double start, double distance, out double result) {
			Interval bounds = TrimmedCurve.Bounds;
			if (IsReversed) {
				//		start = bounds.End - (start - bounds.Start);
				distance *= -1;
			}

			return TrimmedCurve.Geometry.TryOffsetParam(start, distance, out result);
		}
	}

}
