using System.Collections.Generic;
using System.Linq;

namespace NetRewind.Utils.Sync
{
    public class NetObjectSyncGroup
    {
        public SendingMode SendingMode;
        public List<NetObject> Members = new List<NetObject>();

        public NetObjectSyncGroup(SendingMode sendingMode)
        {
            SendingMode = sendingMode;
        }

        #if Server
        public void MergeWith(uint tick, NetObjectSyncGroup otherGroup)
        {
            foreach (var member in Members.ToList())
            {
                member.LeaveSyncGroup();
                member.EnterSyncGroup(tick, otherGroup);
            }
        }
        #endif
    }
}