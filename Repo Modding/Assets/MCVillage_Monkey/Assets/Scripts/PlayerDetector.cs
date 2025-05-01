using UnityEngine;
using System.Collections.Generic;

public class PlayerDetector : MonoBehaviour
{
    private Collider detectionCollider;
    private Animator animator;
    private bool playerDetected = false;

    private void Start()
    {
        // Find the first trigger collider in children
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (col.isTrigger)
            {
                detectionCollider = col;
                Debug.Log($"Using collider: {col.name} for detection");
                break;
            }
        }

        if (detectionCollider == null)
        {
            Debug.LogError("No trigger collider found in children!");
        }

        // Get the animator component (either on this object or parent)
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            // Try to find in parent if not on this object
            animator = GetComponentInParent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("No Animator component found on this object or its parent!");
        }
    }

    private void Update()
    {
        if (detectionCollider != null && animator != null)
        {
            // Create an array to store the results
            Collider[] results = new Collider[10]; // Adjust size as needed
            int count = 0;

            // Use the appropriate overlap method based on collider type
            if (detectionCollider is BoxCollider boxCollider)
            {
                // For a box collider
                count = Physics.OverlapBoxNonAlloc(
                    boxCollider.transform.position + boxCollider.center,
                    boxCollider.size / 2,
                    results,
                    boxCollider.transform.rotation,
                    Physics.AllLayers
                );
            }
            else if (detectionCollider is SphereCollider sphereCollider)
            {
                // For a sphere collider
                count = Physics.OverlapSphereNonAlloc(
                    sphereCollider.transform.position + sphereCollider.center,
                    sphereCollider.radius,
                    results,
                    Physics.AllLayers
                );
            }

            // Check if player is detected
            bool foundPlayer = false;

            for (int i = 0; i < count; i++)
            {
                // Skip self and children
                if (results[i].transform.IsChildOf(transform))
                    continue;

                if (results[i].CompareTag("Player"))
                {
                    foundPlayer = true;
                    if (!playerDetected)
                    {
                        Debug.Log("Player entered detection area");
                        playerDetected = true;
                    }
                    break;
                }
            }

            // If no player found but we previously detected one
            if (!foundPlayer && playerDetected)
            {
                Debug.Log("Player exited detection area");
                playerDetected = false;
            }

            // Update animator parameter
            animator.SetBool("isOpened", playerDetected);
        }
    }
}