using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class Helper
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // Implemented in Assets/Plugins/WebGL/ZGamesBridge.jslib — tells the
    // z-games site to navigate back to this game's detail page.
    [DllImport("__Internal")]
    private static extern void ZGamesNotifyExit();
#endif

    public static void QuitGame()
    {
        Debug.Log("Game Quit!");
#if UNITY_WEBGL && !UNITY_EDITOR
        ZGamesNotifyExit();
#else
        Application.Quit();
#endif
    }
}
