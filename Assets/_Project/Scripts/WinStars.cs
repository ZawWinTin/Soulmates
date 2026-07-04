using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Star-rating reveal. Each child is a SLOT (a dim "empty" star). Its children are [0] the GOLD star
/// and [1] an optional white FLASH. Earned slots reveal their gold with a drop-in + bouncy overshoot
/// and a flash burst; the rest stay empty/dim. Unscaled time → works on the paused win overlay.
/// </summary>
public class WinStars : MonoBehaviour
{
    [Range(0, 3)]
    public int earned = 3;
    public float startDelay = 0.3f;
    public float stagger = 0.22f;
    public float popTime = 0.4f;
    public float dropHeight = 26f;

    Transform Gold(int slot) =>
        transform.GetChild(slot).childCount > 0 ? transform.GetChild(slot).GetChild(0) : null;

    Transform Flash(int slot) =>
        transform.GetChild(slot).childCount > 1 ? transform.GetChild(slot).GetChild(1) : null;

    void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Reveal());
    }

    IEnumerator Reveal()
    {
        int n = transform.childCount;
        for (int i = 0; i < n; i++) // hide golds + flashes
        {
            var g = Gold(i);
            if (g != null)
                g.localScale = Vector3.zero;
            var f = Flash(i);
            if (f != null)
                SetAlpha(f, 0f);
        }
        yield return new WaitForSecondsRealtime(startDelay);
        for (int i = 0; i < n && i < earned; i++)
        {
            StartCoroutine(Pop(Gold(i), Flash(i)));
            yield return new WaitForSecondsRealtime(stagger);
        }
    }

    IEnumerator Pop(Transform gold, Transform flash)
    {
        if (gold == null)
            yield break;
        var grt = gold as RectTransform;
        if (flash != null)
            StartCoroutine(FlashBurst(flash));

        float e = 0f;
        while (e < popTime)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / popTime);
            float s = EaseOutBack(k);
            gold.localScale = new Vector3(s, s, 1f);
            if (grt != null)
                grt.anchoredPosition = new Vector2(0f, Mathf.Lerp(dropHeight, 0f, EaseOutCubic(k)));
            yield return null;
        }
        gold.localScale = Vector3.one;
        if (grt != null)
            grt.anchoredPosition = Vector2.zero;
    }

    IEnumerator FlashBurst(Transform flash)
    {
        float e = 0f,
            dur = 0.32f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / dur);
            float s = Mathf.Lerp(0.5f, 2.1f, k);
            flash.localScale = new Vector3(s, s, 1f);
            SetAlpha(flash, Mathf.Lerp(0.85f, 0f, k));
            yield return null;
        }
        SetAlpha(flash, 0f);
    }

    static void SetAlpha(Transform t, float a)
    {
        if (t.TryGetComponent(out Image img))
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 2.6f,
            c3 = c1 + 1f;
        float xm = x - 1f;
        return 1f + c3 * xm * xm * xm + c1 * xm * xm;
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
