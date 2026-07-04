using System.Collections;
using UnityEngine;

/// <summary>
/// A collectible star placed on the map. When either slime steps onto its tile, it marks the level's
/// 3rd ("collected") star on the GameController and disappears with a little pop. Put this on the ROOT
/// and assign <see cref="visual"/> to the star SPRITE child — only that child bobs/sways/pops, so the
/// sibling ground shadow and glow stay planted on the floor. Drop it on a reachable tile in a level.
/// </summary>
public class StarPickup : MonoBehaviour
{
    public float collectRadius = 0.32f; // ~within one cell
    public float bobAmount = 0.06f;
    public float bobSpeed = 2f;
    public float swayDegrees = 8f; // gentle in-plane wobble
    public Transform visual; // the sprite to bob/sway/pop; if null, this transform is used

    private Transform p1,
        p2;
    private Vector3 basePos; // the ROOT's fixed position — used for collision (never bobs)
    private Vector3 visRest; // the visual's resting local position
    private bool collected;

    Transform Vis => visual != null ? visual : transform;

    void Start()
    {
        var a = GameObject.FindGameObjectWithTag("Player1");
        var b = GameObject.FindGameObjectWithTag("Player2");
        p1 = a != null ? a.transform : null;
        p2 = b != null ? b.transform : null;
        basePos = transform.position;
        visRest = visual != null ? visual.localPosition : Vector3.zero;
    }

    void Update()
    {
        if (collected)
            return;

        // idle life: bob + a gentle in-plane sway on the SPRITE only, so the shadow stays grounded.
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        if (visual != null)
            visual.localPosition = visRest + Vector3.up * bob;
        else
            transform.position = basePos + Vector3.up * bob;
        Vis.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.5f) * swayDegrees);

        if (Near(p1) || Near(p2))
            Collect();
    }

    bool Near(Transform p) =>
        p != null
        && p.gameObject.activeInHierarchy
        && Vector2.Distance(p.position, basePos) <= collectRadius;

    void Collect()
    {
        collected = true;
        var gc = FindObjectOfType<GameController>();
        if (gc != null)
            gc.starCollected = true;
        AudioManager.instance?.Play("StarCollect"); // chime through the SFX mixer group
        StartCoroutine(PopAway());
    }

    IEnumerator PopAway()
    {
        Transform v = Vis;
        Vector3 s0 = v.localScale;
        float t = 0f,
            dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            v.localScale = s0 * (1f + 0.6f * Mathf.Sin(k * Mathf.PI)); // quick pop
            if (visual != null)
                visual.localPosition = visRest + Vector3.up * (k * 0.4f); // rise
            else
                transform.position = basePos + Vector3.up * (k * 0.4f);
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
