using System;
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
        private T[] _buffer;
        private uint _head;
        private uint _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer{T}"/> class with the specified size.
        /// </summary>
        /// <param name="size">The maximum number of items the buffer can hold.</param>
        public CircularBuffer(uint size)
        {
            _buffer = new T[size];
        }

        /// <summary>
        /// Stores an item in the buffer at a given ID position.
        /// If the buffer is full, it will overwrite the oldest items.
        /// </summary>
        /// <param name="id">The ID representing the position of the item (used modulo buffer size).</param>
        /// <param name="item">The item to store.</param>
        public void Store(uint id, T item)
        {
            _buffer[id % _buffer.Length] = item;
            _head = id;
            _count++;
        }

        /// <summary>
        /// Retrieves an item from the buffer at a given ID position.
        /// </summary>
        /// <param name="id">The ID representing the position of the item.</param>
        /// <returns>The item stored at the given ID.</returns>
        public T Get(int id)
        {
            return _buffer[id % _buffer.Length];
        }

        /// <summary>
        /// Returns the latest items from the buffer in chronological order (oldest first, newest last).
        /// Handles wrap-around efficiently.
        /// </summary>
        /// <param name="targetLength">The number of latest items to retrieve.</param>
        /// <returns>An array of the latest items, up to <paramref name="targetLength"/>.</returns>
        public T[] GetLatestItems(uint targetLength)
        {
            // Clamp targetLength to current count
            if (targetLength > _count) targetLength = _count;
            T[] array = new T[targetLength];

            int bufferLength = _buffer.Length;
            int length = (int)targetLength;

            // Calculate the starting index in the circular buffer
            int start = (int)((_head + 1 - targetLength) % (uint)bufferLength);
            if (start < 0) start += bufferLength; // handle negative modulo

            // If items are contiguous (no wrap-around)
            if (start + length <= bufferLength)
            {
                Array.Copy(_buffer, start, array, 0, length);
            }
            else // wrap-around
            {
                int firstPart = bufferLength - start;
                Array.Copy(_buffer, start, array, 0, firstPart);
                Array.Copy(_buffer, 0, array, firstPart, length - firstPart);
            }

            return array;
        }
    }
}