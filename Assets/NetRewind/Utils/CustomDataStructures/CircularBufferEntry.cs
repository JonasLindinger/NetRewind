namespace NetRewind.Utils.CustomDataStructures
{
    public struct CircularBufferEntry<T>
    {
        public uint Id { get; private set; }
        public T Entry { get; private set; }

        public CircularBufferEntry(uint id, T entry)
        {
            Id = id;
            Entry = entry;
        }
    }
}