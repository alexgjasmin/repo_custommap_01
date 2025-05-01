using System.Collections.Generic;
using UnityEngine;

public class FlameLight : MonoBehaviour
{
    [Header("Light Selection")]
    [Tooltip("Whether to use lights from children or specify lights manually")]
    public bool useChildLights = true;

    [Tooltip("Manually assigned lights (if not using child lights)")]
    public List<Light> manualLights = new List<Light>();

    [Header("Intensity Settings")]
    [Tooltip("Curve that controls light intensity over time")]
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.33f, 0.7f),
        new Keyframe(0.66f, 1.2f),
        new Keyframe(1f, 1f)
    );

    [Tooltip("Base intensity of the light")]
    public float baseIntensity = 1.0f;

    [Tooltip("How much the intensity can vary from the base")]
    [Range(0f, 2f)]
    public float intensityVariation = 0.2f;

    [Tooltip("Speed of the intensity flickering")]
    public float intensitySpeed = 1.0f;

    [Header("Color Settings")]
    [Tooltip("Enable color variation")]
    public bool enableColorVariation = false;

    [Tooltip("Base color of the light")]
    public Color baseColor = Color.yellow;

    [Tooltip("Secondary color to blend with")]
    public Color secondaryColor = new Color(1f, 0.5f, 0f); // Orange

    [Tooltip("Curve that controls color blending over time")]
    public AnimationCurve colorBlendCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f)
    );

    [Tooltip("Speed of the color variation")]
    public float colorSpeed = 0.7f;

    [Header("Advanced Settings")]
    [Tooltip("Random seed for variation")]
    public int randomSeed = 0;

    [Tooltip("Apply a random offset to each light to prevent uniformity")]
    public bool randomizeOffset = true;

    [Tooltip("Preserve the original intensity as the base")]
    public bool preserveOriginalIntensity = true;

    [Tooltip("Preserve the original color as the base")]
    public bool preserveOriginalColor = true;

    private List<Light> flameLights = new List<Light>();
    private Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();
    private Dictionary<Light, Color> originalColors = new Dictionary<Light, Color>();
    private Dictionary<Light, float> timeOffsets = new Dictionary<Light, float>();

    private void Awake()
    {
        // Find all relevant lights
        if (useChildLights)
        {
            // Get all lights from children
            flameLights.AddRange(GetComponentsInChildren<Light>());
        }
        else if (manualLights.Count > 0)
        {
            // Use manually assigned lights
            flameLights = manualLights;
        }

        // If no lights found, log warning
        if (flameLights.Count == 0)
        {
            Debug.LogWarning("FlameLight script on " + gameObject.name + " couldn't find any lights to control!");
            return;
        }

        // Initialize random if needed
        if (randomSeed != 0)
        {
            Random.InitState(randomSeed);
        }

        // Store original values and set up offsets
        foreach (Light light in flameLights)
        {
            // Store original intensity
            originalIntensities[light] = light.intensity;

            // Store original color
            originalColors[light] = light.color;

            // Set base values if not preserving originals
            if (!preserveOriginalIntensity)
            {
                light.intensity = baseIntensity;
            }

            if (!preserveOriginalColor && !enableColorVariation)
            {
                light.color = baseColor;
            }

            // Create random offset for this specific light
            if (randomizeOffset)
            {
                timeOffsets[light] = Random.Range(0f, 100f);
            }
            else
            {
                timeOffsets[light] = 0f;
            }
        }
    }

    private void Update()
    {
        foreach (Light light in flameLights)
        {
            // Skip invalid lights
            if (light == null) continue;

            // Calculate time values for curves
            float intensityTime = (Time.time * intensitySpeed + timeOffsets[light]) % 1f;
            float colorTime = (Time.time * colorSpeed + timeOffsets[light]) % 1f;

            // Get actual base intensity (either original or set value)
            float actualBaseIntensity = preserveOriginalIntensity ? originalIntensities[light] : baseIntensity;

            // Update light intensity
            float intensityFactor = intensityCurve.Evaluate(intensityTime);
            float finalIntensity = actualBaseIntensity +
                (intensityFactor - 0.5f) * 2f * intensityVariation;
            light.intensity = Mathf.Max(0.01f, finalIntensity); // Prevent negative intensity

            // Update light color if enabled
            if (enableColorVariation)
            {
                Color actualBaseColor = preserveOriginalColor ? originalColors[light] : baseColor;
                float colorFactor = colorBlendCurve.Evaluate(colorTime);
                light.color = Color.Lerp(actualBaseColor, secondaryColor, colorFactor);
            }
        }
    }

    // Reset to initial values when script is reset in editor
    private void OnDisable()
    {
        // Restore original values when disabled
        foreach (Light light in flameLights)
        {
            if (light == null) continue;

            if (originalIntensities.ContainsKey(light))
                light.intensity = originalIntensities[light];

            if (originalColors.ContainsKey(light))
                light.color = originalColors[light];
        }
    }

    // Reset to initial values when script is reset in editor
    private void Reset()
    {
        intensityCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.33f, 0.7f),
            new Keyframe(0.66f, 1.2f),
            new Keyframe(1f, 1f)
        );

        colorBlendCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );
    }
}