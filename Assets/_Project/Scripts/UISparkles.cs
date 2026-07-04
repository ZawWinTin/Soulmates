using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Emits little UI sparkle sprites from this RectTransform — used at the time-bar's leading edge so
/// it shimmers like the win-tile. Canvas-friendly (a world ParticleSystem won't render in a
/// Screen-Space canvas), pooled, and self-contained. Each sparkle pops in, drifts, spins, fades.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UISparkles : MonoBehaviour
{
    public Sprite sparkleSprite;
    public Color color = new Color(1f, 0.9f, 0.5f, 1f);
    public float rate = 14f; // sparkles per second
    public float life = 0.55f;
    public float size = 12f; // peak size in px
    public float drift = 18f; // px travelled over a life
    public float spawnJitter = 4f; // px spread around the emit point

    RectTransform rt;
    float acc;
    readonly List<Spark> pool = new List<Spark>();

    class Spark
    {
        public RectTransform t;
        public Image img;
        public float age,
            life,
            spin;
        public Vector2 vel;
        public bool alive;
    }

    void Awake() => rt = GetComponent<RectTransform>();

    void Update()
    {
        acc += rate * Time.deltaTime;
        while (acc >= 1f)
        {
            acc -= 1f;
            Spawn();
        }

        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[i];
            if (!s.alive)
                continue;
            s.age += Time.deltaTime;
            float k = s.age / s.life;
            if (k >= 1f)
            {
                s.alive = false;
                s.t.gameObject.SetActive(false);
                continue;
            }
            s.t.anchoredPosition += s.vel * Time.deltaTime;
            float pop = Mathf.Sin(k * Mathf.PI); // 0 → 1 → 0
            s.t.localScale = new Vector3(pop, pop, 1f);
            s.t.localRotation = Quaternion.Euler(0f, 0f, s.spin * s.age);
            var c = color;
            c.a = color.a * pop;
            s.img.color = c;
        }
    }

    void Spawn()
    {
        Spark s = null;
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].alive)
            {
                s = pool[i];
                break;
            }
        if (s == null)
        {
            var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rt, false);
            s = new Spark { t = go.GetComponent<RectTransform>(), img = go.GetComponent<Image>() };
            s.img.raycastTarget = false;
            s.img.sprite = sparkleSprite;
            s.img.preserveAspect = true;
            pool.Add(s);
        }

        s.alive = true;
        s.age = 0f;
        s.life = life * Random.Range(0.7f, 1.25f);
        s.spin = Random.Range(-160f, 160f);
        s.t.gameObject.SetActive(true);
        s.t.sizeDelta = Vector2.one * (size * Random.Range(0.6f, 1.2f));
        s.t.anchoredPosition = new Vector2(
            Random.Range(-spawnJitter, spawnJitter),
            Random.Range(-spawnJitter, spawnJitter)
        );
        float ang = Random.Range(0f, Mathf.PI * 2f);
        s.vel = new Vector2(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) * 1.3f) * drift; // bias upward
    }
}
