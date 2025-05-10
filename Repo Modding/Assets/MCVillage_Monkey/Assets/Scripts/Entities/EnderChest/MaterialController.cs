using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialRevealController : MonoBehaviour
{
    // Lists to store renderers for each category
    [Header("Target Settings")]
    [Tooltip("If true, automatically finds all renderers in child objects")]
    public bool autoFindRenderers = true;

    [Tooltip("Manual list of renderers if not using auto-find")]
    public List<Renderer> manualRenderersList;

    // Material property settings
    [Header("Reveal Animation Settings")]
    [Tooltip("The shader property name to control")]
    public string revealPropertyName = "_Reveal_Amount";

    // Animation settings
    [Tooltip("The animation curve that controls how the reveal effect progresses")]
    public AnimationCurve revealCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Duration of the reveal animation in seconds")]
    public float animationDuration = 1.0f;

    [Tooltip("If true, animation plays automatically on start")]
    public bool playOnStart = false;

    [Tooltip("If true, automatically toggles eye textures when reveal animation completes")]
    public bool autoToggleOnAnimationComplete = true;

    [Tooltip("If true, automatically resets eye textures when hide animation starts")]
    public bool autoToggleOnHideAnimation = true;

    // Eye texture settings
    [Header("Eye Texture Settings")]
    [Tooltip("The shader property name for the eye texture")]
    public string eyeTexturePropertyName = "_enderEyeTexture";

    [Tooltip("First eye texture sprite")]
    public Texture2D eyeTexture1;

    [Tooltip("Second eye texture sprite")]
    public Texture2D eyeTexture2;

    [Tooltip("Current eye texture index (0 = texture1, 1 = texture2)")]
    [Range(0, 1)]
    public int currentEyeTextureIndex = 0;

    // Eye emissive settings
    [Header("Eye Emissive Settings")]
    [Tooltip("The shader property name for the eye emissive map")]
    public string eyeEmissivePropertyName = "_enderEyeEmissive";

    [Tooltip("First eye emissive map")]
    public Texture2D eyeEmissive1;

    [Tooltip("Second eye emissive map")]
    public Texture2D eyeEmissive2;

    [Tooltip("Current eye emissive index (0 = emissive1, 1 = emissive2)")]
    [Range(0, 1)]
    public int currentEyeEmissiveIndex = 0;

    // Remember original states
    [Header("Internal State (Read Only)")]
    [SerializeField] private int originalEyeTextureIndex = 0;
    [SerializeField] private int originalEyeEmissiveIndex = 0;

    // Runtime variables
    private float animationTime = 0f;
    private bool isPlaying = false;
    private float currentRevealValue = 0f;
    private bool animationCompletedToggle = false;

    // Property IDs for the shader parameters
    private int revealAmountID;
    private int eyeTextureID;
    private int eyeEmissiveID;

    // List to store all affected renderers
    private List<Renderer> allRenderers = new List<Renderer>();

    void Awake()
    {
        // Cache the property IDs for better performance
        revealAmountID = Shader.PropertyToID(revealPropertyName);
        eyeTextureID = Shader.PropertyToID(eyeTexturePropertyName);
        eyeEmissiveID = Shader.PropertyToID(eyeEmissivePropertyName);

        // Collect renderers if using auto-find
        if (autoFindRenderers)
        {
            CollectAllRenderers();
        }
        else
        {
            // Use manually assigned renderers
            allRenderers = new List<Renderer>(manualRenderersList);
        }
    }

    void Start()
    {
        // Remember the original texture states
        originalEyeTextureIndex = currentEyeTextureIndex;
        originalEyeEmissiveIndex = currentEyeEmissiveIndex;

        // Initialize all materials to starting values
        SetRevealValue(-0.1f);
        UpdateEyeTexture(currentEyeTextureIndex);
        UpdateEyeEmissive(currentEyeEmissiveIndex);

        // Play animation on start if enabled
        if (playOnStart)
        {
            PlayRevealAnimation();
        }
    }

    void Update()
    {
        // Update animation if playing
        if (isPlaying)
        {
            // Increase animation time
            animationTime += Time.deltaTime;

            // Calculate progress (0 to 1)
            float progress = Mathf.Clamp01(animationTime / animationDuration);

            // Evaluate the curve at current progress
            float revealValue = revealCurve.Evaluate(progress);

            // Apply the value
            SetRevealValue(revealValue);

            // Check if animation just completed (reached end for the first time)
            if (progress >= 1.0f && !animationCompletedToggle)
            {
                // Set the flag to prevent repeated toggling
                animationCompletedToggle = true;

                // Auto-toggle if enabled
                if (autoToggleOnAnimationComplete)
                {
                    ToggleEyeTextureAndEmissive();
                }

                // Stop animation
                isPlaying = false;
            }
        }
    }

    // Collect all renderers in children
    void CollectAllRenderers()
    {
        allRenderers.Clear();

        // Get all renderers in children, including inactive ones
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in childRenderers)
        {
            // Check if renderer has a material with any of the properties
            if (renderer.sharedMaterial &&
                (renderer.sharedMaterial.HasProperty(revealAmountID) ||
                 renderer.sharedMaterial.HasProperty(eyeTextureID) ||
                 renderer.sharedMaterial.HasProperty(eyeEmissiveID)))
            {
                allRenderers.Add(renderer);
            }
        }

        Debug.Log($"Found {allRenderers.Count} renderers with controllable properties");
    }

    // Set the reveal value directly (0 to 1)
    public void SetRevealValue(float value)
    {
        // Clamp value between 0 and 1
        currentRevealValue = Mathf.Clamp01(value);

        // Apply to all materials
        UpdateRevealProperty();
    }

    // Apply the current reveal value to all materials
    void UpdateRevealProperty()
    {
        foreach (Renderer renderer in allRenderers)
        {
            // Check if the renderer is valid and has the property
            if (renderer != null && renderer.sharedMaterial.HasProperty(revealAmountID))
            {
                // Use material property blocks for better performance
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                // Get current properties
                renderer.GetPropertyBlock(propBlock);

                // Set the reveal amount
                propBlock.SetFloat(revealAmountID, currentRevealValue);

                // Apply the properties
                renderer.SetPropertyBlock(propBlock);
            }
        }
    }

    // Update the eye texture on all materials
    void UpdateEyeTexture(int textureIndex)
    {
        // Select texture based on index
        Texture2D selectedTexture = (textureIndex == 0) ? eyeTexture1 : eyeTexture2;

        // If no texture is assigned, log warning and return
        if (selectedTexture == null)
        {
            Debug.LogWarning($"Eye texture {textureIndex + 1} is not assigned!");
            return;
        }

        foreach (Renderer renderer in allRenderers)
        {
            // Check if the renderer is valid and has the property
            if (renderer != null && renderer.sharedMaterial.HasProperty(eyeTextureID))
            {
                // Use material property blocks for better performance
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                // Get current properties
                renderer.GetPropertyBlock(propBlock);

                // Set the eye texture
                propBlock.SetTexture(eyeTextureID, selectedTexture);

                // Apply the properties
                renderer.SetPropertyBlock(propBlock);
            }
        }

        currentEyeTextureIndex = textureIndex;
    }

    // Update the eye emissive map on all materials
    void UpdateEyeEmissive(int emissiveIndex)
    {
        // Select emissive map based on index
        Texture2D selectedEmissive = (emissiveIndex == 0) ? eyeEmissive1 : eyeEmissive2;

        // If no emissive map is assigned, log warning and return
        if (selectedEmissive == null)
        {
            Debug.LogWarning($"Eye emissive map {emissiveIndex + 1} is not assigned!");
            return;
        }

        foreach (Renderer renderer in allRenderers)
        {
            // Check if the renderer is valid and has the property
            if (renderer != null && renderer.sharedMaterial.HasProperty(eyeEmissiveID))
            {
                // Use material property blocks for better performance
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                // Get current properties
                renderer.GetPropertyBlock(propBlock);

                // Set the eye emissive map
                propBlock.SetTexture(eyeEmissiveID, selectedEmissive);

                // Apply the properties
                renderer.SetPropertyBlock(propBlock);
            }
        }

        currentEyeEmissiveIndex = emissiveIndex;
    }

    // Reset textures to their original states
    public void ResetToOriginalTextures()
    {
        UpdateEyeTexture(originalEyeTextureIndex);
        UpdateEyeEmissive(originalEyeEmissiveIndex);
    }

    // Toggle between eye textures
    public void ToggleEyeTexture()
    {
        int newIndex = (currentEyeTextureIndex == 0) ? 1 : 0;
        UpdateEyeTexture(newIndex);
    }

    // Set specific eye texture
    public void SetEyeTexture(int textureIndex)
    {
        // Ensure index is valid (0 or 1)
        textureIndex = Mathf.Clamp(textureIndex, 0, 1);
        UpdateEyeTexture(textureIndex);
    }

    // Toggle between eye emissive maps
    public void ToggleEyeEmissive()
    {
        int newIndex = (currentEyeEmissiveIndex == 0) ? 1 : 0;
        UpdateEyeEmissive(newIndex);
    }

    // Set specific eye emissive map
    public void SetEyeEmissive(int emissiveIndex)
    {
        // Ensure index is valid (0 or 1)
        emissiveIndex = Mathf.Clamp(emissiveIndex, 0, 1);
        UpdateEyeEmissive(emissiveIndex);
    }

    // Toggle both eye texture and emissive map simultaneously
    public void ToggleEyeTextureAndEmissive()
    {
        ToggleEyeTexture();
        ToggleEyeEmissive();
    }

    // Set both eye texture and emissive map simultaneously
    public void SetEyeTextureAndEmissive(int index)
    {
        SetEyeTexture(index);
        SetEyeEmissive(index);
    }

    // Play the reveal animation from current value to 1
    public void PlayRevealAnimation()
    {
        isPlaying = true;
        animationCompletedToggle = false; // Reset animation completion flag
        animationTime = currentRevealValue * animationDuration; // Start from current progress
    }

    // Play the reveal animation from 0 to 1
    public void PlayRevealAnimationFromStart()
    {
        SetRevealValue(0);
        isPlaying = true;
        animationCompletedToggle = false; // Reset animation completion flag
        animationTime = 0f;
    }

    // Play the hide animation (from current value to 0)
    public void PlayHideAnimation()
    {
        // Reset textures if auto-toggle is enabled
        if (autoToggleOnHideAnimation)
        {
            ResetToOriginalTextures();
        }

        StartCoroutine(PlayHideAnimationCoroutine());
    }

    // Coroutine for hide animation (reverse of reveal)
    private IEnumerator PlayHideAnimationCoroutine()
    {
        float startValue = currentRevealValue;
        float startTime = Time.time;

        while (Time.time - startTime < animationDuration)
        {
            // Calculate progress (0 to 1)
            float progress = (Time.time - startTime) / animationDuration;

            // Use the curve in reverse
            float revealValue = Mathf.Lerp(startValue, 0, revealCurve.Evaluate(progress));

            // Apply the value
            SetRevealValue(revealValue);

            yield return null;
        }

        // Ensure we end at exactly 0
        SetRevealValue(0);
    }

    // Methods that can be called by the Animator

    // Reveal animation controls
    public void TriggerRevealAnimation()
    {
        PlayRevealAnimation();
    }

    public void TriggerHideAnimation()
    {
        PlayHideAnimation();
    }

    // Eye texture controls
    public void TriggerEyeTextureToggle()
    {
        ToggleEyeTexture();
    }

    public void TriggerSetEyeTexture(int textureIndex)
    {
        SetEyeTexture(textureIndex);
    }

    // Eye emissive controls
    public void TriggerEyeEmissiveToggle()
    {
        ToggleEyeEmissive();
    }

    public void TriggerSetEyeEmissive(int emissiveIndex)
    {
        SetEyeEmissive(emissiveIndex);
    }

    // Combined controls
    public void TriggerEyeTextureAndEmissiveToggle()
    {
        ToggleEyeTextureAndEmissive();
    }

    public void TriggerSetEyeTextureAndEmissive(int index)
    {
        SetEyeTextureAndEmissive(index);
    }

    public void TriggerResetToOriginalTextures()
    {
        ResetToOriginalTextures();
    }

    // Optional: For debugging or editor control
    void OnValidate()
    {
        // Cache the property IDs
        revealAmountID = Shader.PropertyToID(revealPropertyName);
        eyeTextureID = Shader.PropertyToID(eyeTexturePropertyName);
        eyeEmissiveID = Shader.PropertyToID(eyeEmissivePropertyName);

        // Update in editor if in play mode
        if (Application.isPlaying && allRenderers.Count > 0)
        {
            UpdateEyeTexture(currentEyeTextureIndex);
            UpdateEyeEmissive(currentEyeEmissiveIndex);
        }
    }
}