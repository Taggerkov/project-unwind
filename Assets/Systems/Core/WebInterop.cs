#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Systems.Core
{
    /// <summary>
    /// Bridges to the browser-side functions defined in <c>Assets/Plugins/WebGL/WebInterop.jslib</c>.
    /// Only compiled for the Web target (WebGL/WebGPU), where these externs resolve at runtime.
    /// </summary>
    internal static class WebInterop
    {
        /// <summary>Navigates the current browser tab to the given URL.</summary>
        /// <param name="url">The absolute URL to navigate to.</param>
        [DllImport("__Internal")]
        public static extern void RedirectSameTab(string url);
    }
}
#endif
