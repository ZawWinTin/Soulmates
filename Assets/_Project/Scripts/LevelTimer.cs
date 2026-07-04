using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "time star" meter: a bar that starts FULL and drains as the clock runs toward the level's time
/// par. Finish while it still has fill → you keep the time star; if it empties, the star dims (lost).
/// Freezes on level completion so the result holds.
/// </summary>
public class LevelTimer : MonoBehaviour
{
    public Image fillBar; // Image Type = Sliced; drained by shrinking its right anchor (stays crisp)
    public Image parStar; // gold while fill remains, grey once empty
    public RectTransform handle; // slider knob that rides the draining edge

    static readonly Color Gold = Color.white; // star_3d is already gold — white tint shows it natural
    static readonly Color Grey = new Color(0.55f, 0.52f, 0.55f, 0.7f);

    GameController gc;
    float frozen = -1f;

    void Start()
    {
        gc = FindObjectOfType<GameController>();
    }

    void Update()
    {
        if (gc == null)
            return;
        if (gc.isLevelCompleted && frozen < 0f)
            frozen = Time.timeSinceLevelLoad;
        float t = frozen >= 0f ? frozen : Time.timeSinceLevelLoad;

        float remain = gc.timeParSeconds > 0f ? Mathf.Clamp01(1f - t / gc.timeParSeconds) : 0f;
        if (fillBar != null)
        {
            fillBar.enabled = remain > 0.01f; // hide the sliver when empty
            fillBar.rectTransform.anchorMax = new Vector2(Mathf.Max(remain, 0.0001f), 0.5f);
        }
        if (parStar != null)
            parStar.color = remain > 0f ? Gold : Grey;
        if (handle != null)
        {
            // ride the fill's leading edge (anchors are relative to the track)
            handle.anchorMin = new Vector2(remain, 0.5f);
            handle.anchorMax = new Vector2(remain, 0.5f);
            handle.anchoredPosition = Vector2.zero;
        }
    }
}
