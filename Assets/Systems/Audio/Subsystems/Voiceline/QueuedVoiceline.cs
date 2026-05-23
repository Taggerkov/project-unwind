namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// Internal data structure representing a voiceline in the priority queue.
    /// Pairs a VoicelineEvent with its runtime priority level.
    /// </summary>
    internal readonly struct QueuedVoiceline
    {
        /// <summary>The voiceline event to be played.</summary>
        public readonly VoicelineEvent VoicelineEvent;

        /// <summary>The priority level for queue ordering and interruption rules.</summary>
        public readonly VoicelinePriority Priority;

        /// <summary>
        /// Creates a new queued voiceline with the specified event and priority.
        /// </summary>
        /// <param name="voicelineEvent">The voiceline event to be played.</param>
        /// <param name="priority">The priority level for this voiceline.</param>
        public QueuedVoiceline(VoicelineEvent voicelineEvent, VoicelinePriority priority)
        {
            VoicelineEvent = voicelineEvent;
            Priority = priority;
        }
    }
}
