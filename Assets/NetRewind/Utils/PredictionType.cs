namespace NetRewind.Utils
{
    public enum PredictionType
    {
        /// <summary>
        /// Perfect for Client Side Prediction in FPS Shooter
        /// When the Server sends a GameState, we check if we predicted wrong. if we did, reconcile everything.
        /// </summary>
        Player,
        /// <summary>
        /// Perfect for Client Side Prediction in Games like Rocket League.
        /// -> EVERYTIME the Server sends a GameState, we apply it and reconcile everything.
        /// </summary>
        All
    }
}