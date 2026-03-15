using System;

namespace NetRewind.Utils.Sync
{
    public struct RollbackInfo
    {
        public NetObject NetObject;
        public uint Tick;
        public Action Method;
    }
}