using UnityEngine;

// Gives the hugging couple some life when it appears on a shared tile: a quick entrance "pop"
// (squash-stretch hop, like the slimes' jump), then a gentle continuous breathing bob (like their
// idle). Mirrors the slimes' feel so the hug doesn't look like a static pasted sprite.
public class HugLife : MonoBehaviour
{
    public float bobAmount = 0.035f; // vertical breathing travel
    public float bobSpeed = 2.4f; // breathing speed
    public float squash = 0.04f; // breathing squash/stretch
    public float popHeight = 0.08f; // entrance hop height
    public float popStretch = 0.35f; // entrance scale overshoot at the peak
    public float popDuration = 0.28f;

    // Set true by GameController while the couple is hopping — then it drives the transform
    // (position + squash) itself, and HugLife stops idling so the two don't fight.
    [HideInInspector]
    public bool moving;

    private Vector3 baseScale;
    private Vector3 basePos;
    private float phase;
    private float popT = -1f; // -1 = not popping

    public Vector3 BaseScale => baseScale;

    void OnEnable()
    {
        baseScale = transform.localScale;
        basePos = transform.localPosition;
        phase = 0f;
        popT = 0f; // begin the entrance pop
        moving = false;
    }

    void OnDisable()
    {
        // restore so the next time it's shown it starts clean
        transform.localScale = baseScale;
        transform.localPosition = basePos;
    }

    void Update()
    {
        // while the couple is hopping, GameController owns the transform (matches the slimes' jump)
        if (moving)
            return;

        // entrance pop: a quick squash → stretch overshoot with a little hop
        if (popT >= 0f)
        {
            popT += Time.deltaTime;
            float k = popT / popDuration;
            if (k >= 1f)
            {
                popT = -1f;
            }
            else
            {
                float arc = Mathf.Sin(k * Mathf.PI); // 0 → 1 → 0
                transform.localScale = new Vector3(
                    baseScale.x * (1f + popStretch * 0.5f * arc),
                    baseScale.y * (1f + popStretch * arc),
                    baseScale.z
                );
                transform.localPosition = basePos + Vector3.up * (arc * popHeight);
                return; // the pop owns the transform this frame
            }
        }

        // gentle breathing bob (position + slight squash/stretch)
        phase += Time.deltaTime * bobSpeed;
        float b = Mathf.Sin(phase);
        transform.localPosition = basePos + Vector3.up * (b * bobAmount);
        float s = 1f + b * squash;
        transform.localScale = new Vector3(baseScale.x / s, baseScale.y * s, baseScale.z);
    }
}
