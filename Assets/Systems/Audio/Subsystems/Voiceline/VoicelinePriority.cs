namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// Defines the priority level of a voiceline for queue ordering and interruption rules.
    /// Higher priority voicelines can interrupt lower priority ones.
    /// </summary>
    public enum VoicelinePriority
    {
        /// <summary>Normal priority. Cannot interrupt other voicelines.</summary>
        Normal = 0,
        
        /// <summary>High priority. Can interrupt Normal priority voicelines.</summary>
        High = 1,
        
        /// <summary>Critical priority. Can interrupt High and Normal priority voicelines.</summary>
        Critical = 2
    }
}
