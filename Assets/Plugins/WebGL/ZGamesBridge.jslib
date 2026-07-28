// z-games web bridge (WebGL only).
// react-unity-webgl exposes dispatchReactUnityEvent; the z-games player
// listens for these events (see z-games ai_docs/specs/unity-integration.md):
//   "exit"     → navigate back to the game's detail page
//   "Ready"    → game booted; site replies with SetContext + LoadData
//   "SaveData" → payload is a JSON progress blob for the site to persist
// Every call is try/catch-guarded so a standalone build (plain index.html,
// where dispatchReactUnityEvent doesn't exist) doesn't crash.
mergeInto(LibraryManager.library, {
  ZGamesNotifyExit: function () {
    try {
      window.dispatchReactUnityEvent("exit");
    } catch (e) {
      console.warn("z-games bridge: exit event not delivered", e);
    }
  },

  ZGamesNotifyReady: function () {
    try {
      window.dispatchReactUnityEvent("Ready");
    } catch (e) {
      console.warn("z-games bridge: Ready event not delivered", e);
    }
  },

  // json is a pointer to a UTF-8 C string coming from C# — convert it
  // to a JS string before handing it to the site.
  ZGamesNotifySave: function (json) {
    try {
      window.dispatchReactUnityEvent("SaveData", UTF8ToString(json));
    } catch (e) {
      console.warn("z-games bridge: SaveData event not delivered", e);
    }
  },
});
