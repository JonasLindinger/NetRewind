namespace NetRewind.DONOTUSE
{
    public static class TickSystemHandler
    {
        public static void OnSimulationTick(uint tick)
        {
            
        }

        #if Server
        public static void OnStateTick(uint _)
        {
            
        }
        #endif
        
        #if Client
        public static void OnInputTick(uint _)
        {
            
        }
        #endif
    }
}