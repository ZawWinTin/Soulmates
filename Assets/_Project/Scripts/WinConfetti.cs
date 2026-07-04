using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gentle falling confetti for the Level Complete card. Spawns a pool of small heart/star images that
/// drift down with a sideways sway + spin, then recycle at the top. Clipped to its own (card-sized)
/// rect by a RectMask2D. Unscaled time so it runs while the game is paused.
/// </summary>
public class WinConfetti : MonoBehaviour
{
    public Sprite[] pieces;
    public Color[] colors;
    public int count = 16;
    public float minSpeed = 55f,
        maxSpeed = 120f;
    public Vector2 sizeRange = new Vector2(14f, 26f);

    class Bit
    {
        public RectTransform rt;
        public float speed,
            sway,
            phase,
            spin,
            baseX;
    }

    readonly List<Bit> bits = new List<Bit>();
    bool built;

    void OnEnable()
    {
        if (!built)
            Build();
        // stagger them down the column so it's not one synchronized wave
        foreach (var b in bits)
            Recycle(b, Random.Range(0f, AreaH()));
    }

    float AreaW() => ((RectTransform)transform).rect.width;

    float AreaH() => ((RectTransform)transform).rect.height;

    void Build()
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject(
                "bit" + i,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            if (pieces != null && pieces.Length > 0)
                img.sprite = pieces[i % pieces.Length];
            img.color =
                colors != null && colors.Length > 0 ? colors[i % colors.Length] : Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float s = Random.Range(sizeRange.x, sizeRange.y);
            rt.sizeDelta = new Vector2(s, s);
            var b = new Bit { rt = rt };
            bits.Add(b);
            Recycle(b, 0f);
        }
        built = true;
    }

    void Recycle(Bit b, float startFall)
    {
        b.baseX = Random.Range(-AreaW() * 0.5f, AreaW() * 0.5f);
        b.speed = Random.Range(minSpeed, maxSpeed);
        b.sway = Random.Range(8f, 26f);
        b.phase = Random.Range(0f, 6.28f);
        b.spin = Random.Range(-120f, 120f);
        b.rt.anchoredPosition = new Vector2(b.baseX, AreaH() * 0.5f + 24f - startFall);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float bottom = -AreaH() * 0.5f - 24f;
        foreach (var b in bits)
        {
            var pos = b.rt.anchoredPosition;
            pos.y -= b.speed * dt;
            b.phase += dt * 2f;
            pos.x = b.baseX + Mathf.Sin(b.phase) * b.sway;
            b.rt.anchoredPosition = pos;
            b.rt.Rotate(0f, 0f, b.spin * dt);
            if (pos.y < bottom)
                Recycle(b, 0f);
        }
    }
}
