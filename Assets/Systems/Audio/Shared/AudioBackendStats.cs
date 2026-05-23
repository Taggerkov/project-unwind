#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Systems.Audio.Shared
{
    /// <summary>
    /// A point-in-time snapshot of audio backend internals, for diagnostics tooling.
    /// Compiled only in the editor and development builds.
    /// </summary>
    public readonly struct AudioBackendStats
    {
        /// <summary>Constructs the stats from values captured at the call site.</summary>
        public AudioBackendStats(int activeSources, int createdSources, int configuredPoolSize, bool poolGrew,
            int cachedClips, int inFlightLoads)
        {
            ActiveSources = activeSources;
            CreatedSources = createdSources;
            ConfiguredPoolSize = configuredPoolSize;
            PoolGrew = poolGrew;
            CachedClips = cachedClips;
            InFlightLoads = inFlightLoads;
        }

        /// <summary>Sources currently rented out for active playback.</summary>
        public int ActiveSources { get; }

        /// <summary>Total sources the pool has created so far.</summary>
        public int CreatedSources { get; }

        /// <summary>The pool size configured in <see cref="AudioSettings"/>.</summary>
        public int ConfiguredPoolSize { get; }

        /// <summary>True once the pool has grown past its configured size.</summary>
        public bool PoolGrew { get; }

        /// <summary>Clips currently held in the preload cache.</summary>
        public int CachedClips { get; }

        /// <summary>Loads currently in progress.</summary>
        public int InFlightLoads { get; }
    }
}
#endif
