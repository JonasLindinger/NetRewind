using System.Threading.Tasks;

namespace NetRewind.Utils
{
    public abstract class PredictedNetworkEntity : NetworkEntity
    {
        public bool IsPredicted { get; private set; }

        protected override void InternalOnPostRegister()
        {
            SetUp();
        }

        private async void SetUp()
        {
            // Wait until the object is spawned in the network.
            while (!IsSpawned)
                await Task.Delay(1);
            
            IsPredicted = ShouldBePredicted();
            InternalOnSpawn();
        }

        protected virtual void InternalOnSpawn() {}
            
        protected abstract bool ShouldBePredicted();
        public abstract bool DoWeNeedToReconcile(uint tick, IState predictedState, IState serverState);
    }
}