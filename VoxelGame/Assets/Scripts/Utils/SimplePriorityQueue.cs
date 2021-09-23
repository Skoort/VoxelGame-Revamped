using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils
{
    public class SimplePriorityQueue<Item>
    {
        private List<int> _orderedPriorities;
        private Dictionary<int, Queue<Item>> _priorityQueue;

        private bool _lowestPriorityFirst;

        public SimplePriorityQueue(bool lowestPriorityFirst = false)
        {
            _lowestPriorityFirst = lowestPriorityFirst;

            Clear();
        }

        public void Clear()
        {
            _orderedPriorities.Clear();
            _priorityQueue.Clear();
        }

        public void EnqueueRange(IEnumerable<Item> items, Func<Item, int> priorityFunc)
        {
            foreach (var item in items)
            {
                var priority = priorityFunc?.Invoke(item) ?? 0;
                Enqueue(item, priority);
            }
        }

        public void Enqueue(Item item, int priority)
        {
            if (_priorityQueue.TryGetValue(priority, out var items))
            {
                items = new Queue<Item>();
                _priorityQueue.Add(priority, items);
                AddNewPriority(priority);
            }
            items.Enqueue(item);
        }

        private void AddNewPriority(int priority)
        {
            for (int i = 0; i < _orderedPriorities.Count; ++i)
            {
                if (    _lowestPriorityFirst && _orderedPriorities[i] > i
                    || !_lowestPriorityFirst && _orderedPriorities[i] < i)
                {
                    _orderedPriorities.Insert(i, priority);
                    return;
                }
            }

            _orderedPriorities.Insert(_orderedPriorities.Count - 1, priority);  // Objects with the highest priority go at the end of the list for efficiency.
        }

        public Item Dequeue()
        {
            int lastIndex = _orderedPriorities.Count - 1;

            var priority = _orderedPriorities[lastIndex];
            var items = _priorityQueue[priority];

            var item = items.Dequeue();

            if (items.Count == 0)
            {
                _orderedPriorities.RemoveAt(lastIndex);
                _priorityQueue.Remove(priority);
            }

            return item;
        }

        public void RefreshPriorities(Func<Item, int> priorityFunc)
        {
            var items = new List<Item>();
            foreach (var priority in _orderedPriorities)
            {
                foreach (var chunk in _priorityQueue[priority])
                {
                    items.Add(chunk);
                }
            }

            Clear();

            EnqueueRange(items, priorityFunc);
        }
    }
}
