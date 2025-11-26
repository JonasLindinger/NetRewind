using System;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;

namespace NetRewind.Utils.Player
{
    public abstract class NetPlayer : NetInput
    {
        private static NetPlayer _localPlayer;
        
        protected override void InternalSpawn()
        {
            #if Client
            if (IsOwner)
            {
                if (_localPlayer != null)
                {
                    Debug.LogError("There can only be one local player! (script that inherits "+ nameof(NetInput) + ")");
                }
                else
                {
                    _localPlayer = this;
                }
            }
            #endif
        }

        protected override void InternalDespawn()
        {
            #if Client
            if (_localPlayer == this)
                _localPlayer = null;
            #endif
        }
        
        public static IData TryGetAdditionalData()
        {
            try
            {
                return _localPlayer.GetAdditionalData();
            }
            catch (Exception e) { }
            
            return new DefaultPlayerData();
        }
        
        protected virtual IData GetAdditionalData()
        {
            throw new NotImplementedException();
        }
    }
}