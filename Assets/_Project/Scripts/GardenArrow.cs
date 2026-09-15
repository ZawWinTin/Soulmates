using UnityEngine;
using UnityEngine.UI;

/// <summary>Rotates the shared SVG-exported arrow sprite without relying on font glyphs.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GardenArrow : Image
{
    public Vector2 direction
    {
        set
        {
            if (rectTransform.pivot != new Vector2(.5f, .5f))
            {
                var delta = new Vector2(.5f, .5f) - rectTransform.pivot;
                rectTransform.anchoredPosition += Vector2.Scale(delta, rectTransform.sizeDelta);
                rectTransform.pivot = new Vector2(.5f, .5f);
            }
            rectTransform.localRotation = Quaternion.Euler(
                0,
                0,
                Mathf.Atan2(value.y, value.x) * Mathf.Rad2Deg - 90
            );
            preserveAspect = true;
        }
    }
}
