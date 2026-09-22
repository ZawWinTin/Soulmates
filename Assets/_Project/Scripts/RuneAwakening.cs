using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Preserves the carved rune art; a restrained glow marks an awakened goal.</summary>
public sealed class RuneAwakening : MonoBehaviour
{
    Light2D[] lights;
    float pulseTime = -1;
    bool awakened;

    void Awake()
    {
        lights = GetComponentsInChildren<Light2D>(true);
        foreach (var light in lights)
        {
            var breathing = light.GetComponent<PlayerLightController>();
            if (breathing != null)
                breathing.enabled = false;
        }
    }

    public void Awaken()
    {
        awakened = true;
        pulseTime = 0;
        if (PlayerPrefs.GetInt("GardenReducedMotion", 0) == 0)
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
                particles.Emit(6);
    }

    void Update()
    {
        if (GetComponent<Rigidbody2D>() != null)
            return; // The wrong-goal dissolve owns its light.
        bool reduced = PlayerPrefs.GetInt("GardenReducedMotion", 0) != 0;
        if (pulseTime >= 0)
            pulseTime += Time.deltaTime;
        float pulse =
            !reduced && pulseTime >= 0 && pulseTime < .8f
                ? Mathf.Sin(pulseTime / .8f * Mathf.PI)
                : 0;
        float breathe = reduced ? .5f : (Mathf.Sin(Time.time * 1.5f) + 1) * .5f;
        foreach (var light in lights)
        {
            // Narrow the pool so highlights do not wash out the engraving.
            light.intensity = (awakened ? .65f : Mathf.Lerp(.36f, .52f, breathe)) + pulse * .25f;
            light.pointLightOuterRadius = (awakened ? 1.15f : 1.05f) + pulse * .25f;
        }
    }
}
