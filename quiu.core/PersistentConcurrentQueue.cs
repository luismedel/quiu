using System;
using System.Collections.Concurrent;
using System.Threading;

namespace quiu.core
{
    public abstract class PersistentConcurrentQueue<T>
        : IDisposable
    {
        struct Item
        {
            public T Value;
            public Task? WhenPersisted;

            public Item(T value, Task? whenPersisted)
            {
                this.Value = value;
                this.WhenPersisted = whenPersisted;
            }
        }

        public bool CanAppend => _running && !_stopping;

        public PersistentConcurrentQueue ()
        {
            _queue = new ConcurrentQueue<Item> ();
        }

        public void Start ()
        {
            if (_running)
                throw new InvalidOperationException ("Already running");

            var ts = new ThreadStart (ConsumerLoop);

            _thread = new Thread (ts);
            _stopping = false;
            _running = true;
            _thread.Start ();
        }

        public void Stop (bool wait)
        {
            if (!_running)
                return;

            _stopping = true;
            if (!wait)
                StopNoWait ();
        }

        void StopNoWait ()
        {
            _stopping = true;
            _running = false;
            _thread = null;
        }

        void ConsumerLoop()
        {
            while (_running)
            {
                while (_queue.TryDequeue (out var item))
                {
                    if (Persist (item.Value))
                        item.WhenPersisted?.Start ();
                }

                Thread.Sleep (5);
            }
        }

        public Task? Enqueue (T item, Action? whenPersisted = null)
        {
            if (_stopping)
                throw new InvalidOperationException("Queue stopping operation");

            if (!_running)
                throw new InvalidOperationException ("Queue not running");

            Task? result = null;
            if (whenPersisted != null)
                result = new Task (whenPersisted);

            _queue.Enqueue(new Item (item, result));
            return result;
        }

        abstract protected bool Persist (T item);

        public void Dispose ()
        {
            StopNoWait ();
        }

        readonly ConcurrentQueue<Item> _queue;
        Thread? _thread;

        bool _stopping = false;
        bool _running = false;
    }
}

