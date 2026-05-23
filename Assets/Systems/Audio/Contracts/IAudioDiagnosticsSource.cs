#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Systems.Audio.Shared;

namespace Systems.Audio.Contracts
{
    /// <summary>
    /// Optional capability for a backend that can report runtime statistics.
    /// Kept separate from <see cref="IAudioService"/> so the core playback contract stays minimal and
    /// backends without instrumentation are not forced to implement it.
    /// Compiled only in the editor and development builds.
    /// </summary>
    internal interface IAudioDiagnosticsSource
    {
        /// <summary>Captures current backend statistics. Returns false when unavailable.</summary>
        /// <param name="stats">The populated stats when available; default otherwise.</param>
        bool TryGetStats(out AudioBackendStats stats);
    }
}
#endif
