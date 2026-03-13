using System;

namespace NetRewind.Utils.Simulation.State
{
    public struct RollbackInfo
    {
        public NetObject NetObject;
        public uint Tick;
        public Action method;
    }
}