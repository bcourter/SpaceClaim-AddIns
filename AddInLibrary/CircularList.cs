using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace SpaceClaim.AddInLibrary {
    public class CircularList<T> : IEnumerable<T> {
        List<T> List { get; set; }
        int count;

        public CircularList(int count) {
            this.count = count;
            List = new List<T>(count);
        }

        public CircularList(ICollection<T> collection)
        {
            List = collection.ToList();
        }

        public T this[int index] {
            get { return List[ClampIndex(index)]; }
            set { List[ClampIndex(index)] = value; }
        }

        public void Add(T item) {
            List.Add(item);
        }

        int ClampIndex(int index) {
            return ((index % Count) + Count ) % Count; // Cyclic group, not computer modulo
        }

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return List.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator() {
            return List.GetEnumerator();
        }

        public int Count {
            get { return count; }
        }


        #endregion
    }

    public class CircularListEnum<T> : IEnumerator<T>, IDisposable {
        List<T> List { get; set; }

        // Enumerators are positioned before the first element
        // until the first MoveNext() call.
        int position = -1;

        public CircularListEnum(List<T> List) {
            this.List = List;
        }

        #region IEnumerator Members

        object IEnumerator.Current {
            get { return List[position]; }
        }

        bool IEnumerator.MoveNext() {
            position++;
            return (position < List.Count);
        }

        void IEnumerator.Reset() {
            position = -1;
        }

        T IEnumerator<T>.Current {
            get { return List[position]; }
        }

        #endregion

        #region IDisposable Members

        void IDisposable.Dispose() {
            List = null;
        }

        #endregion
    }
}