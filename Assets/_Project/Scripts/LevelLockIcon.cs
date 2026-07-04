using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a lock icon (and hides the number) on a level button when it is locked.
/// "Locked" is driven by the Button's interactable flag, which PlayOptions sets
/// based on the player's saved progress. Refreshes whenever the button is shown.
/// </summary>
[RequireComponent(typeof(Button))]
public class LevelLockIcon : MonoBehaviour
{
    public GameObject lockIcon;
    public GameObject numberLabel;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (button == null)
            button = GetComponent<Button>();
        bool locked = button != null && !button.interactable;
        if (lockIcon != null)
            lockIcon.SetActive(locked);
        if (numberLabel != null)
            numberLabel.SetActive(!locked);
    }
}
