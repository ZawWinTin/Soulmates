using System.Collections;
using UnityEngine;

/// <summary>
/// Smooth open transition for a UI overlay: fades the whole panel in and gives a soft "pop" (a small
/// scale-up with a gentle overshoot) to a target card. Uses UNSCALED time so it plays while the game
/// is paused (Time.timeScale == 0). Added to Pause / Pause-Options / Help / Level-Complete overlays.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PopInOnEnable : MonoBehaviour
{
    public float duration = 0.22f;
    public float startScale = 0.9f;
    public RectTransform scaleTarget; // the card to pop (the dim/backdrop just fades)

    private CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (scaleTarget == null)
            scaleTarget = transform as RectTransform;
    }

    void OnEnable()
    {
        if (cg == null)
            cg = GetComponent<CanvasGroup>();
        StopAllCoroutines();
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        cg.alpha = 0f;
        if (scaleTarget != null)
            scaleTarget.localScale = new Vector3(startScale, startScale, 1f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            cg.alpha = k;
            if (scaleTarget != null)
            {
                float s = Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(k));
                scaleTarget.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }
        cg.alpha = 1f;
        if (scaleTarget != null)
            scaleTarget.localScale = Vector3.one;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f,
            c3 = c1 + 1f;
        float xm = x - 1f;
        return 1f + c3 * xm * xm * xm + c1 * xm * xm;
    }
}
