using System.Collections.Concurrent;

namespace OneIdentity.DevOps.Data
{
    class FixedSizeQueue<T> : ConcurrentQueue<T>
    {
        private readonly object _syncObject = new object();

        public int Size { get; }

        public FixedSizeQueue(int size)
        {
            Size = size;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (_syncObject)
            {
                while (base.Count > Size)
                {
                    base.TryDequeue(out _);
                }
            }
        }
    }
}
