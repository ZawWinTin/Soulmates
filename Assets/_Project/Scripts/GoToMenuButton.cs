using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Reliably makes a "to the main menu" button work (pause-menu Menu, win-screen Exit).
///
/// The Game Canvas is a prefab INSTANCE in every level scene, and those scenes OVERRIDE this button's
/// serialized onClick to a stale target — so wiring it on the prefab does nothing in-game. This
/// component is added to the prefab as a NEW component (additions DO propagate to instances, unlike
/// overridden properties), and at runtime it disables the broken persistent calls and drives
/// PauseMenu.LoadMenu from code. Override-proof.
/// </summary>
[RequireComponent(typeof(Button))]
public class GoToMenuButton : MonoBehaviour
{
    void Start()
    {
        var btn = GetComponent<Button>();
        // turn off any (scene-overridden, possibly broken) persistent onClick calls
        int n = btn.onClick.GetPersistentEventCount();
        for (int i = 0; i < n; i++)
            btn.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        btn.onClick.AddListener(GoMenu);
    }

    void GoMenu()
    {
        var pm = FindObjectOfType<PauseMenu>();
        if (pm != null)
            pm.LoadMenu();
    }
}
