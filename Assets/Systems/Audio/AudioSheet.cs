using AYellowpaper.SerializedCollections;
using Systems.Audio.Shared;
using UnityEngine;

namespace Systems.Audio
{
    /// <summary>
    /// ScriptableObject lookup table mapping numeric sound IDs to <see cref="AudioEvent"/> assets.
    /// Create via right-click: Create → Unwind Database → Audio → AudioSheet.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSheet", menuName = "Unwind Database/Audio/AudioSheet")]
    public class AudioSheet : ScriptableObject
    {
        /// <summary>All registered sound events keyed by their numeric ID.</summary>
        public SerializedDictionary<uint, AudioEvent> AudioEvents = new();

        /// <summary>
        /// Returns the <see cref="AudioEvent"/> registered under <paramref name="soundId"/>.
        /// Logs a warning and returns null if the ID is not registered.
        /// </summary>
        /// <param name="soundId">Numeric identifier assigned in the Inspector.</param>
        /// <returns>The matching <see cref="AudioEvent"/>, or null if not registered.</returns>
        public AudioEvent Get(uint soundId)
        {
            if (AudioEvents.TryGetValue(soundId, out var audioEvent)) return audioEvent;

            Debug.LogWarning($"[AudioSheet] Sound ID {soundId} not found.");
            return null;
        }
    }
}