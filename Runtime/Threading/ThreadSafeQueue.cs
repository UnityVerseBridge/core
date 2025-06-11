using System;
using System.Collections.Generic;

namespace UnityVerseBridge.Core.Threading
{
    /// <summary>
    /// Thread-safe queue implementation for WebRTC message handling
    /// </summary>
    public class ThreadSafeQueue<T>
    {
        private readonly Queue<T> queue = new Queue<T>();
        private readonly object lockObject = new object();
        private readonly int maxSize;

        public ThreadSafeQueue(int maxSize = 1000)
        {
            this.maxSize = maxSize;
        }

        public int Count
        {
            get
            {
                lock (lockObject)
                {
                    return queue.Count;
                }
            }
        }

        public bool Enqueue(T item)
        {
            if (item == null) return false;

            lock (lockObject)
            {
                if (queue.Count >= maxSize)
                {
                    // Drop oldest message if queue is full
                    queue.Dequeue();
                }
                queue.Enqueue(item);
                return true;
            }
        }

        public bool TryDequeue(out T item)
        {
            lock (lockObject)
            {
                if (queue.Count > 0)
                {
                    item = queue.Dequeue();
                    return true;
                }
                item = default(T);
                return false;
            }
        }

        public void Clear()
        {
            lock (lockObject)
            {
                queue.Clear();
            }
        }

        public T[] ToArray()
        {
            lock (lockObject)
            {
                return queue.ToArray();
            }
        }
    }
}