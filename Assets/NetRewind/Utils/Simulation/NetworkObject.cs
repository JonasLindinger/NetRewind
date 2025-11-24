namespace NetRewind.Utils.Simulation
{
    public abstract class NetworkObject : RegisteredNetworkObject
    {
        protected override void OnTickTriggered(uint tick)
        {
            OnTick(tick);
        }

        protected abstract void OnTick(uint tick);
    }
}