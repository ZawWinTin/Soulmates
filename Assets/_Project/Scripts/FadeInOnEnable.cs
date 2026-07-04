using System.Collections;
using UnityEngine;

/// <summary>
/// Softly fades a UI panel in whenever it becomes active. Added to the menu
/// sub-screens (MainMenu / OptionsMenu / PlayMenu) so switching between them
/// feels smooth instead of snapping. Uses unscaled time so it also works while
/// the game is paused (Time.timeScale == 0).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FadeInOnEnable : MonoBehaviour
{
    public float duration = 0.25f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t / duration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }
}
