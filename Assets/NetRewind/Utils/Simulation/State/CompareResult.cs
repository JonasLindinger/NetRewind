namespace NetRewind.Utils.Simulation.State
{
    public enum CompareResult : uint
    {
        /// <summary>
        /// When everything is fine.
        /// </summary>
        Equal = 0,
        /// <summary>
        /// When the entire world should be corrected
        /// </summary>
        WorldCorrection = 1,
    }
}