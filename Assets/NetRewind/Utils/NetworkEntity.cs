using System.Collections.Generic;
using Unity.Netcode;

namespace NetRewind.Utils
{
    public abstract class NetworkEntity : NetworkBehaviour
    {
        private static List<NetworkEntity> networkEntities = new List<NetworkEntity>();
        private ulong uniqueDeterministicId;

        protected void Start()
        {
            Register();
            InternalOnPostRegister();
            OnPostRegister();
            
            networkEntities.Add(this);
        }

        public override void OnDestroy()
        {
            networkEntities.Remove(this);
            
            Unregister();
            OnPostUnregister();
        }

        private void Register()
        {
            // Creating a unique deterministic ID for the component.
            // By using the NetworkBehaviourId and NetworkObjectId and combining them, we get a unique ID.
            // Combining: Shifts the NetworkObjectId to the higher 32 bits.
            // And Combines it with the NetworkBehaviourId in the lower 32 bits.
            uniqueDeterministicId = NetworkObjectId << 32 | NetworkBehaviourId;

            NetworkRegister.Register(uniqueDeterministicId, NetworkObjectId, this);
        }

        private void Unregister()
        {
            NetworkRegister.Unregister(uniqueDeterministicId);
        }

        public static void TriggerSimulationTick(uint tick)
        {
            foreach (var entity in networkEntities)
                entity.OnTick(tick);
        }
        
        protected virtual void OnPostRegister() {}
        protected virtual void OnPostUnregister() {}
        protected virtual void InternalOnPostRegister() {}
        protected virtual void OnTick(uint tick) {}

        protected abstract IState GetCurrentState();
        protected abstract void SetState(uint tick, IState state);
        protected abstract void ApplyState(uint tick, IState state);
    }
}