using Unity.Netcode;

namespace NetRewind.Utils.Simulation.State
{
    public interface IState : INetworkSerializable
    {
        /// <summary>
        /// Compares the server state with the predicted local state at that tick.
        /// CompareResult is used to determine the action to take.
        /// the uint is used in the case of a PartialObjectCorrection correction to determine which part has to be corrected. Use this as an indicator.
        /// </summary>
        /// <param name="localState"></param>
        /// <param name="serverState"></param>
        /// <returns></returns>
        public uint Compare(IState localState, IState serverState);
    }
}