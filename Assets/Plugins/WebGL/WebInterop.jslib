mergeInto(LibraryManager.library, {
  // Navigates the current tab to the given URL. Tries the top-level window first
  // (in case the build is embedded in a same-origin iframe) and falls back to the
  // local window when that is blocked.
  RedirectSameTab: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    try {
      window.top.location.href = url;
    } catch (e) {
      window.location.href = url;
    }
  }
});
