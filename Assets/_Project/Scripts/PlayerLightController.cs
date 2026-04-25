using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlayerLightController : MonoBehaviour
{
    private Light2D light2D;

    [Header("Breathing Settings")]
    public float minIntensity = 0.5f;
    public float maxIntensity = 0.8f;
    public float breathingSpeed = 0.5f;

    public float minRadius = 0.3f;
    public float maxRadius = 0.8f;


    // Optional parameters for more natural feel
    public bool useRandomOffset = true;
    public float intensityOffset = 0.0f;

    private float randomOffset;
    // Start is called before the first frame update
    void Start()
    {
        light2D = GetComponent<Light2D>();

        if (light2D == null)
        {
            Debug.LogError("No Light2D component found on this GameObject!");
            enabled = false;
            return;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Calculate breathing using sine wave with optional offset
        float breathingFactor = Mathf.Sin((Time.time * breathingSpeed) + randomOffset);

        // Remap from -1,1 to 0,1 range
        breathingFactor = (breathingFactor + 1f) * 0.5f;

        // Apply intensity based on min/max values
        float currentIntensity = Mathf.Lerp(minIntensity, maxIntensity, breathingFactor) + intensityOffset;

        // Apply to light
        light2D.intensity = currentIntensity;

        // Apply radius based on min/max values
        float currentRadius = Mathf.Lerp(minRadius, maxRadius, breathingFactor);
        light2D.pointLightOuterRadius = currentRadius;
    }
}
