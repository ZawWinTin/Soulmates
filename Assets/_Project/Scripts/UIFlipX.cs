using UnityEngine;

/// <summary>
/// Spins a flat UI sprite like a coin by oscillating its horizontal scale (x: 1 → -1 → 1) on a
/// smooth cosine. Used on the time-star so it reads as "alive" while the meter drains.
/// </summary>
public class UIFlipX : MonoBehaviour
{
    public float period = 2.2f; // seconds per full flip cycle
    public bool unscaled = false; // true = keep spinning while the game is paused

    float baseX = 1f;
    float t;

    void OnEnable()
    {
        baseX = Mathf.Abs(transform.localScale.x);
        if (baseX < 0.0001f)
            baseX = 1f;
    }

    void Update()
    {
        t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        float c = Mathf.Cos(t / Mathf.Max(0.01f, period) * Mathf.PI * 2f); // 1 → -1 → 1
        // Shape it so the star DWELLS near ±1 (showing a clean full star) and whips quickly through
        // the thin edge-on phase — a raw cosine lingers as an ugly pixelated sliver.
        float shaped = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.28f);
        var s = transform.localScale;
        s.x = baseX * shaped;
        transform.localScale = s;
    }
}
