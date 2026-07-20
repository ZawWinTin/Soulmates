using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A collectible star placed on the map. When either slime steps onto its tile, it marks the level's
/// 3rd ("collected") star on the GameController and disappears with a little pop. Put this on the ROOT
/// and assign <see cref="visual"/> to the star SPRITE child — only that child bobs/sways/pops, so the
/// sibling ground shadow and glow stay planted on the floor. Drop it on a reachable tile in a level.
/// </summary>
public class StarPickup : MonoBehaviour
{
    public float collectRadius = 0.32f; // fallback only (no tilemap)
    public float bobAmount = 0.06f;
    public float bobSpeed = 2f;
    public float swayDegrees = 8f; // gentle in-plane wobble
    public Transform visual; // the sprite to bob/sway/pop; if null, this transform is used

    private Transform p1,
        p2;
    private PlayerController pc1,
        pc2; // to skip collection while a slime is mid-jump
    private Vector3 basePos; // the ROOT's fixed position — used for collision (never bobs)
    private Vector3 visRest; // the visual's resting local position
    private bool collected;

    private Tilemap groundTilemap; // collection is by exact grid CELL, not distance
    private Vector3Int starCell;

    // Depth sorting: the star floats, so sorting its sprite by the camera axis (sprite centre) puts it
    // too far back. We sort by the star's TILE (basePos) vs the slimes and flip its order around theirs.
    public int slimeOrder = 2; // the order the slimes render at
    private SpriteRenderer starSprite;
    private ParticleSystemRenderer sparkleRenderer;
    static readonly Vector3 SortAxis = new Vector3(0f, 1f, -0.26f); // camera Transparency Sort Axis

    Transform Vis => visual != null ? visual : transform;

    void Start()
    {
        var a = GameObject.FindGameObjectWithTag("Player1");
        var b = GameObject.FindGameObjectWithTag("Player2");
        p1 = a != null ? a.transform : null;
        p2 = b != null ? b.transform : null;
        pc1 = a != null ? a.GetComponent<PlayerController>() : null;
        pc2 = b != null ? b.GetComponent<PlayerController>() : null;
        basePos = transform.position;
        visRest = visual != null ? visual.localPosition : Vector3.zero;

        var tm = GameObject.FindGameObjectWithTag("GroundTileMap");
        groundTilemap =
            (tm != null ? tm.GetComponent<Tilemap>() : null) ?? FindObjectOfType<Tilemap>();
        if (groundTilemap != null)
            starCell = groundTilemap.WorldToCell(basePos); // WorldToCell tolerates the lift (same as the game's win-tile check)

        var starT = visual != null ? visual : transform.Find("Star");
        starSprite = starT != null ? starT.GetComponent<SpriteRenderer>() : null;
        var sparkT = transform.Find("Sparkles");
        sparkleRenderer = sparkT != null ? sparkT.GetComponent<ParticleSystemRenderer>() : null;
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

        DepthSort();

        if (Near(p1, pc1) || Near(p2, pc2))
            Collect();
    }

    // Place the star one order above the slimes when its TILE is in front of the nearest slime, else
    // one below — using the star's tile (basePos), not the floating sprite, so the float can't skew it.
    void DepthSort()
    {
        if (starSprite == null)
            return;
        float star = Vector3.Dot(basePos, SortAxis);
        float frontmostSlime = Mathf.Min(Depth(p1, pc1), Depth(p2, pc2));
        int order = star <= frontmostSlime ? slimeOrder + 1 : slimeOrder - 1;
        starSprite.sortingOrder = order;
        if (sparkleRenderer != null)
            sparkleRenderer.sortingOrder = order + 1;
    }

    // Depth of a slime by its TILE, with the jump height (currentLift) removed — otherwise a hopping
    // slime reads as "further back" and wrongly flips the star in front of it.
    float Depth(Transform t, PlayerController pc)
    {
        if (t == null || !t.gameObject.activeInHierarchy)
            return float.PositiveInfinity;
        float lift = pc != null ? pc.currentLift : 0f;
        return Vector3.Dot(t.position - Vector3.up * lift, SortAxis);
    }

    // Collect ONLY when a slime is on the SAME grid cell as the star. The floating sprite is purely
    // visual, so a slime on an adjacent tile (even one the float leans toward) never triggers it.
    bool Near(Transform p, PlayerController pc)
    {
        if (p == null || !p.gameObject.activeInHierarchy)
            return false;
        // Only when the slime is SETTLED on a tile — never mid-jump, whose arc sweeps over other tiles
        // (incl. the star's) on the way to its landing tile.
        if (pc != null && pc.isMoving)
            return false;
        if (groundTilemap == null)
            return Vector2.Distance(p.position, basePos) <= collectRadius; // fallback: no tilemap

        return groundTilemap.WorldToCell(p.position) == starCell;
    }

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
