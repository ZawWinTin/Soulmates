using UnityEngine;
using UnityEngine.EventSystems;

// Juicy button feel: gently scales up on hover and dips on press, with a smooth spring. Uses
// UNSCALED time so it still animates while the game is paused (pause/options menus). Added to every
// button by ArtRevampSetup.SkinButton.
[RequireComponent(typeof(RectTransform))]
public class ButtonBounce
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
{
    public float hoverScale = 1.06f;
    public float pressScale = 0.93f;
    public float speed = 14f;

    Vector3 baseScale = Vector3.one;
    bool captured;
    float target = 1f;

    void OnEnable()
    {
        if (!captured)
        {
            baseScale = transform.localScale;
            captured = true;
        }
        target = 1f;
        transform.localScale = baseScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            baseScale * target,
            Time.unscaledDeltaTime * speed
        );
    }

    public void OnPointerEnter(PointerEventData e) => target = hoverScale;

    public void OnPointerExit(PointerEventData e) => target = 1f;

    public void OnPointerDown(PointerEventData e) => target = pressScale;

    public void OnPointerUp(PointerEventData e) => target = hoverScale;
}
