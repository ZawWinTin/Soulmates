using UnityEngine;

// Gentle continuous up-down bob (and optional tiny breathing scale) to give a static element — like
// the menu logo — a soft sense of life. Unscaled time so it runs on paused menus too.
public class FloatBob : MonoBehaviour
{
    public float amount = 10f; // vertical travel in UI units
    public float speed = 1.4f; // bobs per ~6s
    public float scaleAmount = 0.015f; // subtle breathing (0 = off)

    Vector3 basePos;
    Vector3 baseScale;
    bool captured;
    float phase;

    void OnEnable()
    {
        if (!captured)
        {
            basePos = transform.localPosition;
            baseScale = transform.localScale;
            captured = true;
        }
        phase = 0f;
    }

    void Update()
    {
        phase += Time.unscaledDeltaTime * speed;
        float s = Mathf.Sin(phase);
        transform.localPosition = basePos + Vector3.up * (s * amount);
        if (scaleAmount > 0f)
            transform.localScale = baseScale * (1f + s * scaleAmount);
    }
}
