using UnityEngine;

public class EnderChestAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;

    [Tooltip("The trigger parameter name for opening the chest")]
    [SerializeField] private string openTriggerName = "isOpened";

    [Tooltip("The trigger parameter name for closing the chest")]
    [SerializeField] private string closeTriggerName = "isClosed";

    [Header("Detection Settings")]
    [Tooltip("The GameObject with the collider that detects player proximity")]
    [SerializeField] private GameObject playerDetectVolume;

    private bool playerInRange = false;

    private void Awake()
    {
        // If animator is not assigned, try to find it on this GameObject
        if (animator == null)
        {
            animator = GetComponent<Animator>();

            // If still not found, try to find it in children
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();

                if (animator == null)
                {
                    Debug.LogError("No Animator component found on " + gameObject.name + " or its children!");
                }
                else
                {
                    Debug.Log("Found Animator in child of " + gameObject.name);
                }
            }
            else
            {
                Debug.Log("Found Animator on " + gameObject.name);
            }
        }

        // Find player detect volume if not assigned
        if (playerDetectVolume == null)
        {
            Transform detectTransform = transform.Find("PlayerDetectVolume");

            if (detectTransform != null)
            {
                playerDetectVolume = detectTransform.gameObject;
                Debug.Log("Found PlayerDetectVolume: " + playerDetectVolume.name);

                // Make sure it has a collider set as trigger
                Collider detectCollider = playerDetectVolume.GetComponent<Collider>();
                if (detectCollider != null)
                {
                    detectCollider.isTrigger = true;
                }
                else
                {
                    Debug.LogWarning("PlayerDetectVolume has no collider! Adding a BoxCollider");
                    BoxCollider boxCollider = playerDetectVolume.AddComponent<BoxCollider>();
                    boxCollider.isTrigger = true;
                    boxCollider.size = new Vector3(3f, 2f, 3f); // Adjust size as needed
                    boxCollider.center = Vector3.zero;
                }

                // Add PlayerDetector component to handle trigger events
                PlayerDetector detector = playerDetectVolume.GetComponent<PlayerDetector>();
                if (detector == null)
                {
                    detector = playerDetectVolume.AddComponent<PlayerDetector>();
                    detector.SetParentAnimator(this);
                }
            }
            else
            {
                Debug.LogWarning("No PlayerDetectVolume found! Player proximity detection won't work.");
            }
        }
    }

    /// <summary>
    /// Triggers the chest opening animation
    /// </summary>
    public void OpenChest()
    {
        if (animator != null)
        {
            Debug.Log("Triggering open animation: " + openTriggerName);
            animator.SetTrigger(openTriggerName);
        }
        else
        {
            Debug.LogWarning("Cannot trigger open animation - no animator assigned!");
        }
    }

    /// <summary>
    /// Triggers the chest closing animation
    /// </summary>
    public void CloseChest()
    {
        if (animator != null)
        {
            Debug.Log("Triggering close animation: " + closeTriggerName);
            animator.SetTrigger(closeTriggerName);
        }
        else
        {
            Debug.LogWarning("Cannot trigger close animation - no animator assigned!");
        }
    }

    /// <summary>
    /// Called by PlayerDetector when player enters the detection range
    /// </summary>
    public void OnPlayerEnterRange()
    {
        playerInRange = true;
        OpenChest();
    }

    /// <summary>
    /// Called by PlayerDetector when player exits the detection range
    /// </summary>
    public void OnPlayerExitRange()
    {
        playerInRange = false;
        CloseChest();
    }

    /// <summary>
    /// Check if player is currently in range of the chest
    /// </summary>
    public bool IsPlayerInRange()
    {
        return playerInRange;
    }

    /// <summary>
    /// Check if an animation with the given name is currently playing
    /// </summary>
    public bool IsAnimationPlaying(string stateName)
    {
        if (animator != null)
        {
            // Check if the specified state is playing in any layer
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }
        return false;
    }
}

/// <summary>
/// Helper component that handles player detection trigger events
/// </summary>
public class PlayerDetector : MonoBehaviour
{
    private EnderChestAnimator parentAnimator;

    public void SetParentAnimator(EnderChestAnimator animator)
    {
        parentAnimator = animator;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && parentAnimator != null)
        {
            Debug.Log("Player entered detect range of " + gameObject.name);
            parentAnimator.OnPlayerEnterRange();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && parentAnimator != null)
        {
            Debug.Log("Player exited detect range of " + gameObject.name);
            parentAnimator.OnPlayerExitRange();
        }
    }
}