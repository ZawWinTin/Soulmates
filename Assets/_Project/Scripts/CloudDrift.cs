using System.Collections.Generic;
using UnityEngine;

// Subtle living sky: scatters a handful of soft clouds across the gameplay backdrop (in front of the
// static sky) and drifts them slowly — smaller clouds drift slower for a gentle parallax. Clouds are
// spawned at RUNTIME (so nothing clutters the scene/prefab) — visible in Play mode, not edit mode.
// Wired by ArtRevampSetup.ApplyGameplayBackground; tune the fields live in the Inspector.
public class CloudDrift : MonoBehaviour
{
    public Sprite[] cloudSprites; // the individual puffs sliced from clouds.png
    public Material material; // unlit, so the players' 2D glow lights don't tint the clouds
    public float baseSpeed = 0.35f; // world units / sec for a full-size cloud — gentle
    public float spanX = 24f; // horizontal range clouds scatter across + wrap (≈ a bit > view width)
    public float spanY = 1.6f; // vertical scatter band around this object's height
    public float scale = 1f; // base size; each cloud varies 0.75–1.3× around it
    public float alpha = 0.6f; // soft, so it layers over the baked sky clouds without clutter
    public int sortingOrder = -900; // in front of the sky (-1000), behind the gameplay (>= 0)
    public int count = 7;

    struct Cloud
    {
        public Transform t;
        public float speed;
    }

    List<Cloud> clouds;
    float wrap;

    void Start()
    {
        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            enabled = false;
            return;
        }
        wrap = spanX * 0.5f + 4f; // wrap just off-screen
        clouds = new List<Cloud>(count);
        for (int i = 0; i < count; i++)
        {
            var sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
            if (sprite == null)
                continue;
            var go = new GameObject("Cloud" + i, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            float sc = scale * Random.Range(0.75f, 1.3f);
            go.transform.localScale = Vector3.one * sc;
            go.transform.localPosition = new Vector3(
                Random.Range(-wrap, wrap),
                Random.Range(-spanY, spanY),
                0f
            );
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(1f, 1f, 1f, alpha);
            if (material != null)
                sr.sharedMaterial = material;
            // smaller clouds read as farther away → drift a little slower (parallax depth)
            clouds.Add(new Cloud { t = go.transform, speed = baseSpeed * sc });
        }
    }

    void Update()
    {
        if (clouds == null)
            return;
        foreach (var c in clouds)
        {
            var p = c.t.localPosition;
            p.x -= c.speed * Time.deltaTime;
            if (p.x < -wrap)
                p.x += 2f * wrap; // endless gentle drift
            c.t.localPosition = p;
        }
    }
}
