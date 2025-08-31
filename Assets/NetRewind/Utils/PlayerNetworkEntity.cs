using System.Collections.Generic;
using NetRewind.DONOTUSE;

namespace NetRewind.Utils
{
    public abstract class PlayerNetworkEntity : PredictedNetworkEntity
    {
        #if Client
        public static PlayerNetworkEntity Local { get; private set; }
        #endif

        #if Server
        private static List<PlayerNetworkEntity> players = new List<PlayerNetworkEntity>();
        #endif
        
        protected override void InternalOnSpawn()
        {
            #if Client
            if (IsOwner)
                Local = this;

            if (IsServer)
                players.Add(this);
            #endif
        }

        protected override void InternalOnDestroy()
        {
            #if Client
            if (Local == this)
                Local = null;

            if (IsServer)
                players.Remove(this);
            #endif
        }

        protected override void OnTick(uint tick)
        {
            // Check if this player is owner or the executer is a server (or host)
            if (!(IsServer || IsOwner)) return;

            if (IsOwner && !IsServer)
                // Simple client
                OnTickSimpleClient(tick);
            else
                // Server or Host
                OnTickServer(tick);
        }

        private void OnTickSimpleClient(uint tick)
        {
            #if Client && !Server
            // If this is a simple client (not host)
            ClientInputState input = InputCollector.GetInput(tick);
            if (input != null)
                Local.OnTick(tick, input);
            #endif
        }

        private void OnTickServer(uint tick)
        {
            #if Server
            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    // This is the host player, so we use the local input
                    ClientInputState input = InputCollector.GetInput(tick);
                    if (input != null)
                        player.OnTick(tick, input);
                }
                else
                {
                    // Just a player who (hopefully) sent an input for this player
                    ClientInputState input = InputSender.GetInputFromClient(player.OwnerClientId, tick);
                    if (input != null)
                        player.OnTick(tick, input);
                }
            }
            #endif
        }
        
        protected virtual void OnTick(uint tick, ClientInputState input) { }

        // The player should only be predicted if this is the local player
        protected override bool ShouldBePredicted() => IsOwner;
        
        public virtual IData GetPlayerData() => null;
    }
}