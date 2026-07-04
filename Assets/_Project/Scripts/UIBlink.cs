using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Periodically swaps a UI Image to a "blink" sprite for a moment, then back — so a static character
/// image (e.g. the slimes on the Level Complete screen) looks alive. Unscaled time so it blinks even
/// while the game is paused / on the win overlay.
/// </summary>
[RequireComponent(typeof(Image))]
public class UIBlink : MonoBehaviour
{
    public Sprite normalSprite;
    public Sprite blinkSprite;
    public Vector2 interval = new Vector2(2.2f, 4.5f); // random seconds between blinks
    public float blinkDuration = 0.12f;

    private Image img;

    void Awake()
    {
        img = GetComponent<Image>();
        if (normalSprite == null)
            normalSprite = img.sprite;
    }

    void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        while (blinkSprite != null && img != null)
        {
            yield return new WaitForSecondsRealtime(Random.Range(interval.x, interval.y));
            img.sprite = blinkSprite;
            yield return new WaitForSecondsRealtime(blinkDuration);
            img.sprite = normalSprite;
        }
    }
}
