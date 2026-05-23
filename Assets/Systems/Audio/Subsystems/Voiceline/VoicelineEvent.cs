using UnityEngine;
using Systems.Audio.Shared;

namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// Defines a voiceline with audio, subtitle, duration, and priority information.
    /// Create via right-click: Create → Audio → Voiceline Event.
    /// </summary>
    [CreateAssetMenu(fileName = "VoicelineEvent", menuName = "Unwind/Audio/Voiceline Event", order = 3)]
    public sealed class VoicelineEvent : ScriptableObject
    {
        /// <summary>The audio event containing the voiceline audio clip.</summary>
        [SerializeField]
        [Tooltip("The AudioEvent containing the voiceline audio clip. Must have AudioCategory.Voice.")]
        private AudioEvent audioEvent;

        /// <summary>The localization key for the subtitle text.</summary>
        [SerializeField]
        [Tooltip("The localization key for the subtitle text (e.g., 'character.greeting').")]
        private string subtitleKey;

        /// <summary>Optional duration of the voiceline in seconds. Used for UI progress bars.</summary>
        [SerializeField]
        [Tooltip("Optional duration of the voiceline in seconds. Used for UI progress bars or timing.")]
        private float duration;

        /// <summary>The default priority level for this voiceline.</summary>
        [SerializeField]
        [Tooltip("The default priority level. Higher priority voicelines can interrupt lower priority ones.")]
        private VoicelinePriority defaultPriority = VoicelinePriority.Normal;

        /// <summary>The audio event containing the voiceline audio clip.</summary>
        public AudioEvent AudioEvent => audioEvent;

        /// <summary>The localization key for the subtitle text.</summary>
        public string SubtitleKey => subtitleKey;

        /// <summary>Optional duration of the voiceline in seconds.</summary>
        public float Duration => duration;

        /// <summary>The default priority level for this voiceline.</summary>
        public VoicelinePriority DefaultPriority => defaultPriority;
    }
}
