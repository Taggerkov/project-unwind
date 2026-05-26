namespace Systems.CPU
{
    /// <summary>Extension helpers for <see cref="EDefenceResponse"/> values.</summary>
    public static class DefenceResponseExtensions
    {
        /// <summary>
        /// One-shot responses are executed once and immediately mark the threat as resolved.
        /// Sustained responses persist until the threat naturally ends.
        /// </summary>
        public static bool IsOneShot(this EDefenceResponse response) => response switch
        {
            EDefenceResponse.CounterMove => true,
            EDefenceResponse.Guard => false,
            EDefenceResponse.Ignore => false,
            _ => false,
        };
    }
}