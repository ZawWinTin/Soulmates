using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the player's best star rating (0..3) for a level on its select button. Reads the saved
/// value from <see cref="SaveSystem"/> each time the button is shown, lighting up earned stars gold
/// and dimming the rest. Wired by the editor tool (buildIndex + the three star Images).
/// </summary>
public class LevelStars : MonoBehaviour
{
    public int buildIndex; // the level's scene build index (Level01 = 1 … Level09 = 9)
    public Image[] stars; // three star icons, left → right

    public Color earnedColor = Color.white; // star_3d is gold — white shows it natural
    public Color emptyColor = new Color(0.55f, 0.5f, 0.55f, 0.6f);

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (stars == null)
            return;
        // Hide the row while the level is locked (the lock icon shows there instead).
        var btn = GetComponentInParent<Button>();
        bool locked = btn != null && !btn.interactable;
        int n = locked ? -1 : SaveSystem.GetStars(buildIndex);
        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null)
            {
                stars[i].enabled = !locked;
                stars[i].color = i < n ? earnedColor : emptyColor;
            }
    }
}
