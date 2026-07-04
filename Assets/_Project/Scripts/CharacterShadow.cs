using UnityEngine;

/// <summary>
/// A soft ground shadow that tracks a character but stays GROUNDED — it never rises when the
/// character hops, back-flips or bobs. It is a sibling (not a child) of the character so it can't
/// inherit squash/stretch or a 360° flip spin. Each LateUpdate it copies the character's horizontal
/// position, removes its current lift (so it sticks to the floor), and shrinks + fades a little as
/// the character rises.
///
/// Works for two cases:
///  • Slimes — set <see cref="liftSource"/>; lift comes from PlayerController.currentLift.
///  • The hug couple — leave liftSource null; lift is derived from how far the followed transform
///    has risen above its resting local Y (covers HugLife's bob and the couple-hop arc).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterShadow : MonoBehaviour
{
    [Tooltip("Slime lift source (optional). When set, follow/followRenderer default to it.")]
    public PlayerController liftSource;

    [Tooltip(
        "Second lift source — for the hug couple, the OTHER slime (couple hops are driven by the moving slime's currentLift, not the hug's localPosition)."
    )]
    public PlayerController liftSourceB;

    [Tooltip(
        "Also treat the followed transform's rise above its rest as lift (the hug couple's idle bob). Off for solo slimes, whose localPosition changes when they move tiles."
    )]
    public bool useLocalBob = false;

    [Tooltip("Transform to track (defaults to liftSource's transform).")]
    public Transform follow;

    [Tooltip("Renderer used for sizing, visibility and sorting (defaults to liftSource's).")]
    public SpriteRenderer followRenderer;

    [Tooltip("Shadow width as a fraction of the character's sprite width.")]
    public float widthFactor = 0.85f;

    [Tooltip(
        "How far down toward the sprite's bottom to plant the shadow (0 = pivot, 1 = texture bottom). "
            + "Lower = nearer the body, higher = nearer the floor."
    )]
    public float footFactor = 0.32f;

    [Tooltip("Extra manual vertical nudge on top of the computed foot position.")]
    public float groundYOffset = 0f;

    [Tooltip("How much the shadow shrinks/fades per world-unit of jump height.")]
    public float liftShrink = 0.5f;

    public float baseAlpha = 0.42f;

    private SpriteRenderer shadowSR;
    private float baseScale; // shadow world width when grounded
    private float footOffset; // distance from the pivot down to the planted position (negative)
    private float restLocalY; // followed transform's resting local Y (for the lift-from-position case)
    private bool sized; // baseScale/footOffset computed once the character is first visible

    void Awake()
    {
        shadowSR = GetComponent<SpriteRenderer>();
        if (liftSource != null)
        {
            if (follow == null)
                follow = liftSource.transform;
            if (followRenderer == null)
                followRenderer = liftSource.GetComponent<SpriteRenderer>();
        }
        if (follow != null)
            restLocalY = follow.localPosition.y;
        var c = shadowSR.color;
        c.a = baseAlpha;
        shadowSR.color = c;
    }

    void LateUpdate()
    {
        if (follow == null || followRenderer == null)
            return;

        // Hide the shadow whenever the character is hidden (inactive, or tucked inside the couple).
        bool show = follow.gameObject.activeInHierarchy && followRenderer.enabled;
        if (shadowSR.enabled != show)
            shadowSR.enabled = show;
        if (!show)
            return;

        // Size + foot offset, computed the first frame the character is actually visible (its bounds
        // are only valid once active — important for the hug, which starts disabled).
        if (!sized && followRenderer.sprite != null)
        {
            baseScale = followRenderer.bounds.size.x * widthFactor;
            footOffset = -(follow.position.y - followRenderer.bounds.min.y) * footFactor;
            sized = true;
        }

        // How high the character currently is above the floor — the max of every contributing source.
        float lift = 0f;
        if (liftSource != null)
            lift = Mathf.Max(lift, liftSource.currentLift);
        if (liftSourceB != null)
            lift = Mathf.Max(lift, liftSourceB.currentLift); // couple hop is driven by a slime's lift
        if (useLocalBob)
        {
            float sy = follow.parent != null ? follow.parent.lossyScale.y : 1f;
            lift = Mathf.Max(lift, (follow.localPosition.y - restLocalY) * sy); // couple idle bob
        }

        Vector3 p = follow.position;
        p.y -= lift; // remove the lift → stay on the floor
        p.y += footOffset + groundYOffset; // drop to the feet so it reads as a contact shadow
        transform.position = p;

        // Higher → smaller, softer shadow — but never fully gone (keep the grounding cue while airborne).
        float k = Mathf.Clamp(1f - Mathf.Max(0f, lift) * liftShrink, 0.4f, 1f);
        transform.localScale = new Vector3(baseScale * k, baseScale * k, 1f);
        var c = shadowSR.color;
        c.a = baseAlpha * k;
        shadowSR.color = c;

        // Always draw just behind the character in the same sorting layer.
        shadowSR.sortingLayerID = followRenderer.sortingLayerID;
        shadowSR.sortingOrder = followRenderer.sortingOrder - 1;
    }
}
