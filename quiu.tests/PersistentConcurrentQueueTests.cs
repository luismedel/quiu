using System;
using quiu.core;

namespace quiu.tests
{
    public class PersistentConcurrentQueueTests
    {
        class Queue : PersistentConcurrentQueue<int>
        {
            public List<int> Items => _items;

            protected override bool Persist (int item)
            {
                _items.Add (item);
                return true;
            }

            readonly List<int> _items = new List<int> ();
        }

        [Fact]
        public void Test_Enqueue()
        {
            const int COUNT = 1000;

            var tasks = new List<Task> ();
            var processed = 0;

            using (var q = new Queue())
            {
                q.Start ();

                for (int i = 0; i < COUNT; i++)
                    tasks.Add (q.Enqueue (i, () => Interlocked.Increment(ref processed))!);

                q.Stop (wait:true);

                Assert.Equal (COUNT, tasks.Count);
                Task.WhenAll (tasks).GetAwaiter ().GetResult ();
                Assert.Equal (COUNT, processed);
            }
        }
    }
}

