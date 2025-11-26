namespace NetRewind.Utils.Simulation
{
    public abstract class NetObject : NetBehaviour
    {
        protected override void OnTickTriggered(uint tick)
        {
            OnTick(tick);
        }

        protected abstract void OnTick(uint tick);
        protected override bool IsPredicted() => false;
    }
}