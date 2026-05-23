using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// A generic priority queue that maintains items ordered by priority.
    /// Items with higher priority values are dequeued first.
    /// Items with the same priority maintain FIFO order.
    /// </summary>
    /// <typeparam name="T">The type of items in the queue.</typeparam>
    internal sealed class PriorityQueue<T>
    {
        /// <summary>Per-priority FIFO queues keyed by negated priority so higher values sort first in the ascending <see cref="SortedList{TKey,TValue}"/>.</summary>
        private readonly SortedList<int, Queue<T>> _queues = new();

        /// <summary>Total number of items across all priority buckets.</summary>
        private int _count;

        /// <summary>
        /// Gets the total number of items in the queue across all priorities.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Enqueues an item with the specified priority.
        /// Higher priority values are dequeued first.
        /// Items with the same priority maintain FIFO order.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <param name="priority">The priority value. Higher values have higher priority.</param>
        public void Enqueue(T item, int priority)
        {
            // Use negative priority for descending order
            int key = -priority;

            if (!_queues.TryGetValue(key, out var queue))
            {
                queue = new Queue<T>();
                _queues[key] = queue;
            }

            queue.Enqueue(item);
            _count++;
        }

        /// <summary>
        /// Dequeues and returns the highest priority item.
        /// If multiple items have the same priority, returns the oldest (FIFO).
        /// </summary>
        /// <returns>The highest priority item.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
        public T Dequeue()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("Cannot dequeue from an empty priority queue.");
            }

            // Get the first (highest priority) queue
            var firstKey = _queues.Keys[0];
            var queue = _queues[firstKey];

            var item = queue.Dequeue();
            _count--;

            // Remove empty queue
            if (queue.Count == 0)
            {
                _queues.Remove(firstKey);
            }

            return item;
        }

        /// <summary>
        /// Returns the highest priority item without removing it.
        /// </summary>
        /// <returns>The highest priority item.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
        public T Peek()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("Cannot peek an empty priority queue.");
            }

            var firstKey = _queues.Keys[0];
            var queue = _queues[firstKey];
            return queue.Peek();
        }

        /// <summary>
        /// Removes all items from the queue.
        /// </summary>
        public void Clear()
        {
            _queues.Clear();
            _count = 0;
        }

        /// <summary>
        /// Returns a read-only list of all items in the queue in priority order.
        /// </summary>
        /// <returns>A read-only list of all queued items.</returns>
        public IReadOnlyList<T> GetAllItems()
        {
            var items = new List<T>(_count);

            foreach (var queue in _queues.Values)
            {
                items.AddRange(queue);
            }

            return items;
        }

        /// <summary>
        /// Returns true if any item in the queue satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <param name="predicate">The condition to test each item against.</param>
        /// <returns>True if a matching item was found; false otherwise.</returns>
        public bool Contains(Func<T, bool> predicate)
        {
            foreach (var queue in _queues.Values)
                foreach (var item in queue)
                    if (predicate(item)) return true;
            return false;
        }

        /// <summary>
        /// Removes the first item in priority order that satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <param name="predicate">The condition to test each item against, evaluated in priority order.</param>
        /// <returns>True if a matching item was found and removed; false if no match was found.</returns>
        public bool RemoveFirst(Func<T, bool> predicate)
        {
            foreach (var pair in _queues)
            {
                var arr = pair.Value.ToArray();
                var idx = -1;
                for (var i = 0; i < arr.Length; i++)
                {
                    if (!predicate(arr[i])) continue;
                    idx = i;
                    break;
                }

                if (idx < 0) continue;

                pair.Value.Clear();
                for (var i = 0; i < arr.Length; i++)
                    if (i != idx) pair.Value.Enqueue(arr[i]);

                _count--;
                if (pair.Value.Count == 0) _queues.Remove(pair.Key);
                return true;
            }

            return false;
        }
    }
}
