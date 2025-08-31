namespace NetRewind.Utils
{
    public class PlayerNetworkEntity : PredictedNetworkEntity
    {
        #if Client
        public static PlayerNetworkEntity Local { get; private set; }
        #endif
        
        protected override IState GetCurrentState()
        {
            throw new System.NotImplementedException();
        }

        protected override void SetState(uint tick, IState state)
        {
            throw new System.NotImplementedException();
        }

        protected override void ApplyState(uint tick, IState state)
        {
            throw new System.NotImplementedException();
        }

        protected override bool ShouldBePredicted()
        {
            throw new System.NotImplementedException();
        }

        protected override bool DoWeNeedToReconcile(uint tick, IState predictedState, IState serverState)
        {
            throw new System.NotImplementedException();
        }

        public virtual IData GetPlayerData() => null;
    }
}