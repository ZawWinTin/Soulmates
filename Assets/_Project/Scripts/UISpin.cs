using UnityEngine;

/// <summary>
/// Slowly rotates a UI element (e.g. the sunburst rays behind the win-screen slimes). Unscaled time
/// so it keeps turning on the paused win overlay.
/// </summary>
public class UISpin : MonoBehaviour
{
    public float degreesPerSecond = 12f;

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
