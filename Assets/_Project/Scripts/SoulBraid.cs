using UnityEngine;

// The "soul braid" shown when the two slimes meet on one tile: a blue wisp and a pink wisp rise
// from the tile, twisting around the hug like a double helix (passing behind it, then in front),
// and knot together above it, where the candy heart pops out. Lives on the stack's Heart object,
// which GameController shows for a moment on each stack, so OnEnable starts it.
// The wisp heads leave a trail of soft puffs: puffs on the far side of the helix go into
// backPuffs (sorted under the hug), the near side into frontPuffs (over it). Wired by
// Editor/SoulBraidSetup.
public class SoulBraid : MonoBehaviour
{
    public ParticleSystem backPuffs,
        frontPuffs,
        hearts;

    [Header("Shape")]
    [Tooltip("Seconds from the tile to the knot.")]
    public float braidDuration = 1.1f;
    public float startHeight = -0.12f; // local Y of the tile surface (Heart sits 0.15 above the tile)
    public float knotHeight = 1.3f; // local Y where the two wisps meet
    public float startRadius = 0.56f; // how wide the helix starts around the hug
    public float twists = 1.75f; // full turns before the knot
    public float isoTilt = 0.22f; // fake depth: the near side of the helix sits a bit lower

    [Header("Puffs")]
    public float puffSpacing = 0.011f; // world distance between puffs along a wisp
    public float frontSize = 0.15f;
    public float backSize = 0.1f;
    public float knotBurst = 14f; // puffs thrown out where the wisps knot

    public Color boyColor = new Color(0.557f, 0.773f, 1f); // #8EC5FF
    public Color girlColor = new Color(1f, 0.702f, 0.776f); // #FFB3C6

    private float t;
    private bool knotted;
    private readonly Vector3[] lastHead = new Vector3[2];
    private readonly float[] carry = new float[2];

    void OnEnable()
    {
        t = 0f;
        knotted = false;
        for (int s = 0; s < 2; s++)
        {
            lastHead[s] = Head(s, 0f, out _);
            carry[s] = 0f;
        }
        // Reduced motion: no braid, just the heart.
        if (PlayerPrefs.GetInt("GardenReducedMotion", 0) != 0)
            Knot(false);
    }

    void Update()
    {
        if (knotted)
            return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / braidDuration);
        for (int s = 0; s < 2; s++)
            Trail(s, k);
        if (k >= 1f)
            Knot(true);
    }

    // Position of wisp s (0 = boy, 1 = girl) at progress k, in local space; depth is -1 (far) to 1 (near).
    private Vector3 Head(int s, float k, out float depth)
    {
        float angle = (k * twists + s * 0.5f) * Mathf.PI * 2f;
        // eases in from the tile, tightens to nothing at the knot
        float radius = startRadius * Mathf.Pow(1f - k, 0.9f) * Mathf.SmoothStep(0.55f, 1f, k * 6f);
        float rise = 1f - (1f - k) * (1f - k);
        depth = Mathf.Cos(angle);
        return new Vector3(
            Mathf.Sin(angle) * radius,
            Mathf.Lerp(startHeight, knotHeight, rise) - depth * radius * isoTilt,
            0f
        );
    }

    // Lay puffs evenly between the head's last and current position (smooth at any frame rate).
    private void Trail(int s, float k)
    {
        Vector3 head = Head(s, k, out float depth);
        Vector3 from = lastHead[s];
        float dist = Vector3.Distance(from, head) + carry[s];
        Color tint = s == 0 ? boyColor : girlColor;
        float taper = Mathf.Lerp(1f, 0.6f, k); // thinner as the wisps tighten
        while (dist >= puffSpacing)
        {
            dist -= puffSpacing;
            float f = 1f - dist / Mathf.Max(Vector3.Distance(from, head), 0.0001f);
            Vector3 p = Vector3.Lerp(from, head, Mathf.Clamp01(f));
            float near = depth * 0.5f + 0.5f;
            Puff(
                depth > 0f ? frontPuffs : backPuffs,
                p,
                Mathf.Lerp(backSize, frontSize, near) * taper,
                Color.Lerp(tint * new Color(0.85f, 0.85f, 0.9f, 0.7f), tint, near)
            );
        }
        carry[s] = dist;
        lastHead[s] = head;
    }

    private void Knot(bool burst)
    {
        knotted = true;
        Vector3 top = new Vector3(0f, knotHeight, 0f);
        if (burst)
            for (int i = 0; i < knotBurst; i++)
            {
                Vector2 off = Random.insideUnitCircle * 0.12f;
                Puff(
                    backPuffs, // behind the heart, so it doesn't smudge it
                    top + (Vector3)off,
                    frontSize * Random.Range(0.8f, 1.3f),
                    Color.Lerp(boyColor, girlColor, Random.value),
                    off * 2.5f
                );
            }
        if (hearts != null)
            hearts.Emit(
                new ParticleSystem.EmitParams { position = top, applyShapeToPosition = false },
                1
            );
    }

    private static void Puff(
        ParticleSystem ps,
        Vector3 pos,
        float size,
        Color color,
        Vector3 velocity = default
    )
    {
        if (ps == null)
            return;
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = velocity,
            startSize = size,
            startColor = color,
            rotation = Random.Range(0f, 360f),
            applyShapeToPosition = false,
        };
        ps.Emit(p, 1);
    }
}
