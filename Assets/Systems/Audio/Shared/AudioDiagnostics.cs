using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Systems.Audio.Shared
{
    /// <summary>
    /// Development-only, scope-aware logging for audio authoring and misuse diagnostics.
    /// Every method is compiled out of release player builds: the <see cref="System.Diagnostics.ConditionalAttribute"/>
    /// markers strip each call — and the evaluation of its arguments — unless <c>UNITY_EDITOR</c> or
    /// <c>DEVELOPMENT_BUILD</c> is defined.
    /// </summary>
    /// <remarks>
    /// Messages are auto-tagged with the originating layer — core <c>Audio</c>, a subsystem such as
    /// <c>Audio/Music</c>, or a backend such as <c>Audio/BuiltIn</c> — and the calling type, both resolved from
    /// the compile-time caller path at no call-site cost. Callers therefore pass only the bare message.
    /// Use only for diagnostics, never for control flow: in a release build these calls do not run, so any
    /// branch or side effect in an argument expression would silently vanish. For the same reason, keep
    /// diagnostic-only state and computation inside Conditional methods (or <c>#if</c> blocks) so it does not
    /// survive into production.
    /// </remarks>
    internal static class AudioDiagnostics
    {
        /// <summary>Logs a development-only warning, auto-tagged with layer and calling type. Stripped from release builds.</summary>
        /// <param name="message">The warning text.</param>
        /// <param name="callerPath">Compiler-supplied source path of the call site. Do not pass explicitly.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(string message, [CallerFilePath] string callerPath = "")
            => Debug.LogWarning(Format(message, callerPath));

        /// <summary>Logs a development-only error, auto-tagged with layer and calling type. Stripped from release builds.</summary>
        /// <param name="message">The error text.</param>
        /// <param name="callerPath">Compiler-supplied source path of the call site. Do not pass explicitly.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Error(string message, [CallerFilePath] string callerPath = "")
            => Debug.LogError(Format(message, callerPath));

        /// <summary>Builds the <c>[layer] Component: message</c> line from the caller's source path.</summary>
        private static string Format(string message, string callerPath)
        {
            var component = Path.GetFileNameWithoutExtension(callerPath);
            return string.IsNullOrEmpty(component)
                ? $"[{Layer(callerPath)}] {message}"
                : $"[{Layer(callerPath)}] {component}: {message}";
        }

        /// <summary>Maps a source path to its audio layer: core system, a subsystem, or a backend implementation.</summary>
        private static string Layer(string callerPath)
        {
            var path = callerPath.Replace('\\', '/');
            if (path.Contains("/Audio/Music/")) return "Audio/Music";
            if (path.Contains("/Audio/Voiceline/")) return "Audio/Voiceline";
            return path.Contains("/Audio/Runtime/BuiltIn/") ? "Audio/BuiltIn" : "Audio";
        }
    }
}
