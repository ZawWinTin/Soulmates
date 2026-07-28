// z-games web bridge (WebGL only).
// react-unity-webgl exposes dispatchReactUnityEvent; the z-games player
// listens for "exit" and navigates back to the game's detail page.
// Guarded so a standalone build (index.html) doesn't crash.
mergeInto(LibraryManager.library, {
  ZGamesNotifyExit: function () {
    try {
      window.dispatchReactUnityEvent("exit");
    } catch (e) {
      console.warn("z-games bridge: exit event not delivered", e);
    }
  },
});
