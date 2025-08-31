namespace NetRewind.DONOTUSE
{
    public static class TickSystemHandler
    {
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