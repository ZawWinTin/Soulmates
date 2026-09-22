using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Soft hover lift, press squash, and a tiny current-island float.</summary>
public sealed class GardenIslandFeedback
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
{
    public RectTransform visual;
    public bool current;
    Vector2 origin;
    Button button;
    bool hovering,
        pressing;

    void Start()
    {
        origin = visual.anchoredPosition;
        button = GetComponent<Button>();
    }

    void Update()
    {
        if (visual == null || button == null || !button.interactable)
            return;
        bool reduced = PlayerPrefs.GetInt("GardenReducedMotion", 0) == 1;
        float lift = !reduced && current ? Mathf.Sin(Time.unscaledTime * 1.8f) * 2 : 0;
        if (hovering && !pressing && !reduced)
            lift += 4;
        float scale =
            pressing ? .96f
            : hovering && !reduced ? 1.025f
            : 1;
        float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
        visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * scale, blend);
        visual.anchoredPosition = Vector2.Lerp(
            visual.anchoredPosition,
            origin + Vector2.up * lift,
            blend
        );
    }

    public void OnPointerEnter(PointerEventData data) => hovering = true;

    public void OnPointerExit(PointerEventData data)
    {
        hovering = false;
        pressing = false;
    }

    public void OnPointerDown(PointerEventData data) => pressing = true;

    public void OnPointerUp(PointerEventData data) => pressing = false;

    void OnDisable()
    {
        hovering = false;
        pressing = false;
    }
}
