using System.Collections.Generic;

namespace Horcrux.Runtime.Implementations.Utilities.Common
{
    /// <summary>
    /// Safe list that allows adding and removing items while iterating over it.
    /// Compact before traversing this.
    /// </summary>
    public class DeferredList<T> where T : class
    {
        static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;
        
        private readonly List<T> items;
        public T this[int index] => items[index];
        public int Count => items.Count;
        public int TombstoneCount { get; private set; }
        
        public DeferredList(int capacity = 4)
        {
            items = new List<T>(capacity);
        }

        public bool Add(T item)
        {
            if (item == null || IndexOf(item) >= 0)
                return false;
            
            items.Add(item);
            return true;
        }

        public bool Remove(T item)
        {
            if (item == null)
                return false;

            int idx = IndexOf(item);
            if (idx < 0) 
                return false;

            RemoveAt(idx);
            return true;
        }

        public bool RemoveAt(int index)
        {
            if (items[index] == null)
                return false;

            items[index] = null;
            TombstoneCount++;
            return true;
        }

        public void Compact()
        {
            if (TombstoneCount <= 0)
                return;
        
            int cnt = items.Count;
            int write = 0;
            // push all null elements into the bottom of the list.
            for (int read = 0; read < cnt; read++)
            {
                if (items[read] == null)
                    continue;
        
                if (write != read)
                    items[write] = items[read];
                write++;
            }
        
            items.RemoveRange(write, cnt - write);
            TombstoneCount = 0;
        }

        private int IndexOf(T item)
        {
            int cnt = items.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (items[i] != null && Comparer.Equals(items[i], item))
                    return i;
            }
            return -1;
        }

        public void Clear()
        {
            items.Clear();
            TombstoneCount = 0;
        }
    }
}