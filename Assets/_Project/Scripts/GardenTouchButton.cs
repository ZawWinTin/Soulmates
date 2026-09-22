using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One pointer per direction; independent pads allow both slimes to hop together.</summary>
public sealed class GardenTouchButton
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
{
    public PlayerController player;
    public Vector2 direction;
    int pointer = int.MinValue;
    float nextStep;
    Image background;
    Color rest;

    void Awake()
    {
        background = GetComponent<Image>();
    }

    void Start()
    {
        rest = background.color;
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (pointer != int.MinValue || Time.timeScale == 0 || player == null)
            return;
        pointer = data.pointerId;
        background.color = Color.Lerp(rest, Color.white, .3f);
        Step();
    }

    void Step()
    {
        if (player != null)
            player.RequestMove(direction);
        nextStep = Time.unscaledTime + .4f;
    }

    void Update()
    {
        if (pointer != int.MinValue && Time.timeScale > 0 && Time.unscaledTime >= nextStep)
            Step();
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (pointer == data.pointerId)
            Release();
    }

    public void OnPointerExit(PointerEventData data)
    {
        if (pointer == data.pointerId)
            Release();
    }

    void OnDisable()
    {
        Release();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
            Release();
    }

    void Release()
    {
        pointer = int.MinValue;
        if (background != null)
            background.color = rest;
    }
}
