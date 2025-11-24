namespace NetRewind.Utils.Simulation.State
{
    public enum CompareResult : byte
    {
        /// <summary>
        /// When everything is fine.
        /// </summary>
        Equal,
        /// <summary>
        /// When the entire object should be corrected
        /// </summary>
        FullObjectCorrection,
        /// <summary>
        /// When only a part of the object should be corrected.
        /// </summary>
        PartialObjectCorrection,
        /// <summary>
        /// Reconcile a group of objects.
        /// </summary>
        GroupCorrection, // Todo: Implement
        /// <summary>
        /// Reconcile the entire world.
        /// </summary>
        WorldCorrection,
    }
}