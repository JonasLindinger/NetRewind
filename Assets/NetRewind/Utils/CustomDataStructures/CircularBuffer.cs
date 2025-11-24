using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetRewind.Utils.CustomDataStructures
{
    /// <summary>
    /// A generic circular buffer (ring buffer) for storing items efficiently with a fixed size.
    /// Supports storing items by ID, retrieving a specific item, and getting the latest items in order.
    /// </summary>
    /// <typeparam name="T">Type of items stored in the buffer.</typeparam>
    public class CircularBuffer<T>
    {
        private CircularBufferEntry<T>[] _buffer;
        private uint _head;
        private uint _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer{T}"/> class with the specified size.
        /// </summary>
        /// <param name="size">The maximum number of items the buffer can hold.</param>
        public CircularBuffer(uint size)
        {
            _buffer = new CircularBufferEntry<T>[size];
        }

        /// <summary>
        /// Stores an item in the buffer at a given ID position.
        /// If the buffer is full, it will overwrite the oldest items.
        /// </summary>
        /// <param name="id">The ID representing the position of the item (used modulo buffer size).</param>
        /// <param name="item">The item to store.</param>
        public void Store(uint id, T item)
        {
            _buffer[id % _buffer.Length] = new CircularBufferEntry<T>(id, item);
            _head = id;
            _count++;
        }

        /// <summary>
        /// Retrieves an item from the buffer at a given ID position.
        /// </summary>
        /// <param name="id">The ID representing the position of the item.</param>
        /// <returns>The item stored at the given ID.</returns>
        public T Get(uint id)
        {
            CircularBufferEntry<T> entry = _buffer[id % _buffer.Length];
            if (id == entry.Id)
            {
                // Correct item found
                return entry.Entry;
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        /// <summary>
        /// Returns the latest items from the buffer in chronological order (oldest first, newest last).
        /// Handles wrap-around efficiently.
        /// </summary>
        /// <param name="targetLength">The number of latest items to retrieve.</param>
        /// <returns>An array of the latest items, up to <paramref name="targetLength"/>.</returns>
        public T[] GetLatestItems(uint targetLength)
        {
            if (targetLength == 0 || _count == 0)
                return Array.Empty<T>();

            uint bufferSize = (uint)_buffer.Length;

            if (targetLength > bufferSize)
                targetLength = bufferSize;

            T[] result = new T[targetLength];
            int found = 0;

            // currentId is the global ID we expect at each step (start at head, then head-1, ...)
            long currentId = _head; // use long to safely decrement below 0 check

            // Iterate at most bufferSize times (can't have more contiguous valid entries than buffer length)
            for (uint i = 0; i < bufferSize && found < targetLength; i++, currentId--)
            {
                if (currentId < 0) // no older IDs available
                    break;

                uint arrIndex = (uint)(currentId % (long)bufferSize);
                CircularBufferEntry<T> entry = _buffer[arrIndex];

                // If the slot doesn't match the expected global ID, it's a gap or overwritten -> stop
                if (entry.Id != (uint)currentId)
                    break;

                // If the stored entry is null/default -> stop (keeps contiguous guarantee)
                // "is null" works for reference types and nullable; it will be false for value types with non-null default
                if (entry.Entry is null)
                    break;

                result[found++] = entry.Entry;
            }

            if (found == 0)
                return Array.Empty<T>();

            if (found != result.Length)
                Array.Resize(ref result, found);

            // We collected newest → oldest, reverse to oldest → newest.
            Array.Reverse(result);

            return result;
        }
    }
}