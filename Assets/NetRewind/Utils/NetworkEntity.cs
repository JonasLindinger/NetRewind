using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils
{
    public abstract class NetworkEntity : NetworkBehaviour
    {
        public SyncType SyncType => syncType;

        [Header("Network Sync")] 
        [SerializeField] private SyncType syncType = SyncType.Full;
        [Space(10)]
        
        private static List<NetworkEntity> networkEntities = new List<NetworkEntity>();
        private ulong uniqueDeterministicId;

        private List<NetworkEntity> subNetworkEntities = new List<NetworkEntity>();
        private NetworkEntity parent;
        
        protected void Start()
        {
            Register();
            InternalOnPostRegister();
            OnPostRegister();
            
            networkEntities.Add(this);

            InternalOnStart();
        }

        public override void OnDestroy()
        {
            networkEntities.Remove(this);
            
            Unregister();
            OnPostUnregister();

            InternalOnDestroy();
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

        /// <summary>
        /// Adds this object as a Sub Network Entity to the Parent Entity
        /// </summary>
        /// <param name="parentEntity"></param>
        public void NetworkGroupToAParent(NetworkEntity parentEntity)
        {
            // Unregister from Register, so that everything is linked to the parentEntity
            Unregister();
            
            // Set the parent
            parent = parentEntity;
            parent.subNetworkEntities.Add(this); // Todo: Proceed the sub network entity system
        }

        /// <summary>
        /// Removes this object from the NetworkParent
        /// </summary>
        public void NetworkUnGroupFromAParent()
        {
            // Check if this object is in a group
            if (parent == null)
            {
                if (NetworkRunner.Runner.DebugMode == DebugMode.All || NetworkRunner.Runner.DebugMode == DebugMode.ErrorsOnly)
                    Debug.LogWarning("Can't perform Network ungroup, because this NetworkEntity is not in a group!");
                return;
            }
            
            // Add this object back to register
            Register();
            
            // Set the parent to null
            parent.subNetworkEntities.Remove(this);
            parent = null;
        }

        /// <summary>
        /// Adds a NetworkEntity to this object (which will be the parent).
        /// </summary>
        /// <param name="groupEntity"></param>
        public void AddNetworkEntityToThisGroup(NetworkEntity groupEntity)
        {
            groupEntity.NetworkGroupToAParent(this);
        }

        /// <summary>
        /// Removes a NetworkEntity from this Group.
        /// </summary>
        /// <param name="groupEntity"></param>
        public void RemoveNetworkEntityFromThisGroup(NetworkEntity groupEntity)
        {
            groupEntity.NetworkUnGroupFromAParent();
        }
        
        public static void TriggerSimulationTick(uint tick)
        {
            foreach (var entity in networkEntities)
                entity.OnTick(tick);
        }
        
        protected virtual void OnPostRegister() {}
        protected virtual void OnPostUnregister() {}
        protected virtual void InternalOnStart() {}
        protected virtual void InternalOnDestroy() {}
        protected virtual void InternalOnPostRegister() {}
        protected virtual void OnTick(uint tick) {}

        public abstract IState GetCurrentState();
        public abstract void SetState(uint tick, IState state);
        public abstract void StateUpdate(uint tick, IState state);
    }
}