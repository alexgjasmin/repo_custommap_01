using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnderChestTeleporter : MonoBehaviour
{
    [Header("Teleportation Settings")]
    [SerializeField] private float teleportDelay = 0.5f;
    [SerializeField] private float postTeleportCooldown = 2.0f;
    [Tooltip("How long in seconds this chest cannot teleport a player after receiving them")]
    [SerializeField] private float destinationLockoutDuration = 5.0f;
    [SerializeField] private string teleportTag = "teleChest";

    [Header("Animation Settings")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openedParameterName = "isOpened";
    [SerializeField] private GameObject PlayerDetectVolume;
    [Tooltip("How long the chest must remain closed before it can open again")]
    [SerializeField] private float chestReopenCooldown = 1.5f;

    private GameObject teleportVolume;
    private GameObject teleportTarget;
    private bool canTeleport = true;
    private bool teleportInitiated = false;
    private bool playerInDetectionRange = false;
    private bool isTeleporting = false;

    // New approach: use coroutine for chest cooldown instead of updating in Update()
    private bool isOnCooldown = false;
    private Coroutine cooldownCoroutine = null;

    // Dictionary to track which chests are locked out and for how long
    private static Dictionary<GameObject, float> chestLockoutTimers = new Dictionary<GameObject, float>();

    void Awake()
    {
        // Initialize the lockout dictionary if it hasn't been created yet
        if (chestLockoutTimers == null)
        {
            chestLockoutTimers = new Dictionary<GameObject, float>();
        }

        // Find all teleport chests in the scene
        GameObject[] enderChests = GameObject.FindGameObjectsWithTag(teleportTag);
        Debug.Log(gameObject.name + " found " + enderChests.Length + " EnderChests in the scene.");

        // Log information about each found chest for debugging
        foreach (GameObject chest in enderChests)
        {
            Debug.Log("Found EnderChest: " + chest.name + " at position " + chest.transform.position);

            // Check if it has TeleportTarget
            Transform target = chest.transform.Find("TeleportTarget");
            if (target != null)
            {
                Debug.Log("  - Has TeleportTarget at position " + target.position);
            }
            else
            {
                Debug.LogWarning("  - Missing TeleportTarget! Add a child named 'TeleportTarget' to " + chest.name);
            }
        }

        // Find and setup the teleport volume - with more robust checking
        FindAndSetupTeleportVolume();

        // Find the teleport target
        FindTeleportTarget();

        // Find and setup animator and player detection volume
        SetupAnimationComponents();

        // Ensure the chest is initially closed
        if (chestAnimator != null)
        {
            chestAnimator.SetBool(openedParameterName, false);
        }
    }

    private void Update()
    {
        // Update lockout timers for teleportation
        List<GameObject> chestKeys = new List<GameObject>(chestLockoutTimers.Keys);

        foreach (GameObject chest in chestKeys)
        {
            if (chest == null)
            {
                // Chest has been destroyed, remove it directly
                chestLockoutTimers.Remove(chest);
                continue;
            }

            // Update the timer
            float timeRemaining = chestLockoutTimers[chest] - Time.deltaTime;

            if (timeRemaining <= 0)
            {
                // Timer expired, remove directly
                chestLockoutTimers.Remove(chest);
                Debug.Log($"Lockout expired for chest: {chest.name}");
            }
            else
            {
                // Update timer value
                chestLockoutTimers[chest] = timeRemaining;
            }
        }

        // DEBUG: Add key press to reset the teleporter for testing
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetTeleporter();
            Debug.Log("Teleporter reset triggered by 'R' key");
        }
    }

    private void SetupAnimationComponents()
    {
        // Find animator if not assigned
        if (chestAnimator == null)
        {
            // Try to find on this GameObject
            chestAnimator = GetComponent<Animator>();

            // If not found, try to find in children
            if (chestAnimator == null)
            {
                chestAnimator = GetComponentInChildren<Animator>();

                if (chestAnimator == null)
                {
                    Debug.LogWarning("No Animator component found on " + gameObject.name +
                                    " or its children. Chest animations will not play.");
                }
                else
                {
                    Debug.Log("Found Animator in child of " + gameObject.name);
                }
            }
        }

        // Find player detection volume if not assigned
        if (PlayerDetectVolume == null)
        {
            Transform detectTransform = transform.Find("PlayerDetectVolume");

            if (detectTransform != null)
            {
                PlayerDetectVolume = detectTransform.gameObject;
                Debug.Log("Found PlayerDetectVolume: " + PlayerDetectVolume.name);

                // Make sure it has a collider set as trigger
                Collider detectCollider = PlayerDetectVolume.GetComponent<Collider>();
                if (detectCollider != null)
                {
                    detectCollider.isTrigger = true;
                }
                else
                {
                    Debug.LogWarning("PlayerDetectVolume has no collider! Adding a BoxCollider");
                    BoxCollider boxCollider = PlayerDetectVolume.AddComponent<BoxCollider>();
                    boxCollider.isTrigger = true;
                    boxCollider.size = new Vector3(3f, 2f, 3f); // Adjust size as needed
                    boxCollider.center = Vector3.zero;
                }

                // Add PlayerProximityDetector component to handle trigger events
                PlayerProximityDetector detector = PlayerDetectVolume.GetComponent<PlayerProximityDetector>();
                if (detector == null)
                {
                    detector = PlayerDetectVolume.AddComponent<PlayerProximityDetector>();
                    detector.SetParentTeleporter(this);
                }
            }
            else
            {
                Debug.LogWarning("No PlayerDetectVolume found as child! Player proximity detection won't work.");
            }
        }
        else
        {
            // Ensure the assigned PlayerDetectVolume has the detector component
            PlayerProximityDetector detector = PlayerDetectVolume.GetComponent<PlayerProximityDetector>();
            if (detector == null)
            {
                detector = PlayerDetectVolume.AddComponent<PlayerProximityDetector>();
                detector.SetParentTeleporter(this);
            }
        }
    }

    private void FindAndSetupTeleportVolume()
    {
        // First try direct child
        Transform volumeTransform = transform.Find("TeleportVolume");

        // If not found as direct child, try deeper search
        if (volumeTransform == null)
        {
            Debug.Log("TeleportVolume not found as direct child, trying recursive search...");
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("TeleportVolume"))
                {
                    volumeTransform = child;
                    Debug.Log("Found TeleportVolume through recursive search: " + child.name);
                    break;
                }
            }
        }

        // If still not found, check if this GameObject itself has a collider
        if (volumeTransform == null)
        {
            Debug.Log("No TeleportVolume child found, checking if this GameObject has a collider...");
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
            {
                volumeTransform = transform;
                Debug.Log("Using this GameObject as TeleportVolume since it has a collider");
            }
        }

        // If we found a transform, set up the teleport volume
        if (volumeTransform != null)
        {
            teleportVolume = volumeTransform.gameObject;
            Debug.Log("TeleportVolume set to: " + teleportVolume.name);

            // Check for collider component
            Collider volumeCollider = teleportVolume.GetComponent<Collider>();

            // If no collider exists, add a box collider as fallback
            if (volumeCollider == null)
            {
                Debug.LogWarning("No collider found on TeleportVolume, adding a BoxCollider");
                volumeCollider = teleportVolume.AddComponent<BoxCollider>();
                BoxCollider boxCollider = volumeCollider as BoxCollider;
                boxCollider.size = new Vector3(1.5f, 1.5f, 1.5f);
                boxCollider.center = Vector3.zero;
                boxCollider.isTrigger = true;
            }
            else
            {
                volumeCollider.isTrigger = true;
                Debug.Log("Found and configured existing collider on TeleportVolume");
            }

            // Add collider component to this GameObject if we're not already using it
            if (teleportVolume != gameObject)
            {
                // Set up this script to receive trigger events
                Rigidbody rb = teleportVolume.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    Debug.Log("Adding non-physics Rigidbody to TeleportVolume for trigger detection");
                    rb = teleportVolume.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                // Make sure we receive OnTriggerEnter events by attaching this script to TeleportVolume
                if (teleportVolume.GetComponent<EnderChestTeleporter>() == null &&
                    teleportVolume != this.gameObject)
                {
                    EnderChestTeleporter volumeTeleporter = teleportVolume.AddComponent<EnderChestTeleporter>();

                    // Copy serialized fields to the new component
                    volumeTeleporter.teleportDelay = this.teleportDelay;
                    volumeTeleporter.postTeleportCooldown = this.postTeleportCooldown;
                    volumeTeleporter.destinationLockoutDuration = this.destinationLockoutDuration;
                    volumeTeleporter.teleportTag = this.teleportTag;
                    volumeTeleporter.openedParameterName = this.openedParameterName;
                    volumeTeleporter.chestReopenCooldown = this.chestReopenCooldown;

                    // Disable this instance to prevent duplicate actions
                    Debug.Log("Moved teleporter script to TeleportVolume object and disabled this instance");
                    this.enabled = false;
                }
            }
        }
        else
        {
            Debug.LogError("No TeleportVolume found or created! Teleportation will not work.");
        }
    }

    private void FindTeleportTarget()
    {
        // First try direct child
        Transform targetTransform = transform.Find("TeleportTarget");

        // If not found as direct child, try deeper search
        if (targetTransform == null)
        {
            Debug.Log("TeleportTarget not found as direct child, trying recursive search...");
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("TeleportTarget"))
                {
                    targetTransform = child;
                    Debug.Log("Found TeleportTarget through recursive search: " + child.name);
                    break;
                }
            }
        }

        // If still not found, create one
        if (targetTransform == null)
        {
            Debug.LogWarning("TeleportTarget not found, creating one...");
            GameObject targetObj = new GameObject("TeleportTarget");
            targetObj.transform.parent = transform;
            targetObj.transform.localPosition = new Vector3(0, 0, 2); // 2 units in front
            targetObj.transform.localRotation = Quaternion.identity;
            targetTransform = targetObj.transform;
        }

        teleportTarget = targetTransform.gameObject;
        Debug.Log("TeleportTarget set to: " + teleportTarget.name + " at position " + teleportTarget.transform.position);
    }

    // Animation control methods
    public void OpenChest()
    {
        // Don't open if on cooldown, teleporting, or no animator
        if (isOnCooldown)
        {
            Debug.Log("Cannot open chest - cooldown active");
            return;
        }

        if (isTeleporting)
        {
            Debug.Log("Cannot open chest - teleportation in progress");
            return;
        }

        if (chestAnimator == null)
        {
            Debug.LogWarning("Cannot open chest - no animator assigned!");
            return;
        }

        Debug.Log("Setting chest to open state: " + openedParameterName);
        chestAnimator.SetBool(openedParameterName, true);
    }

    public void CloseChest()
    {
        if (chestAnimator != null)
        {
            Debug.Log("Setting chest to closed state: " + openedParameterName);
            chestAnimator.SetBool(openedParameterName, false);

            // Start cooldown when chest is closed
            StartChestCooldown();
        }
        else
        {
            Debug.LogWarning("Cannot close chest - no animator assigned!");
        }
    }

    private void StartChestCooldown()
    {
        // If there's already a cooldown running, stop it
        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
        }

        // Start a new cooldown coroutine
        cooldownCoroutine = StartCoroutine(ChestCooldownCoroutine());
    }

    private IEnumerator ChestCooldownCoroutine()
    {
        Debug.Log("Starting chest cooldown for " + chestReopenCooldown + " seconds");
        isOnCooldown = true;

        float startTime = Time.time;
        float elapsedTime = 0f;

        while (elapsedTime < chestReopenCooldown)
        {
            elapsedTime = Time.time - startTime;
            float remainingTime = chestReopenCooldown - elapsedTime;

            // Log every 0.5 seconds for debugging
            if (Mathf.FloorToInt(remainingTime * 2) != Mathf.FloorToInt((remainingTime + Time.deltaTime) * 2))
            {
                Debug.Log("Cooldown remaining: " + remainingTime.ToString("F1") + " seconds");
            }

            yield return null;
        }

        isOnCooldown = false;
        Debug.Log("Chest cooldown finished - chest can now be reopened");

        // If player is still in range, reopen the chest
        if (playerInDetectionRange && !isTeleporting)
        {
            OpenChest();
            Debug.Log("Player still in range after cooldown - reopening chest");
        }

        cooldownCoroutine = null;
    }

    public void ResetTeleporter()
    {
        // Cancel any active cooldown
        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = null;
        }

        // Reset all flags
        isOnCooldown = false;
        isTeleporting = false;
        teleportInitiated = false;
        canTeleport = true;

        Debug.Log("Teleporter state has been reset");

        // Open chest if player is in range
        if (playerInDetectionRange && chestAnimator != null)
        {
            chestAnimator.SetBool(openedParameterName, true);
            Debug.Log("Reopening chest after reset because player is in range");
        }
        else if (chestAnimator != null)
        {
            chestAnimator.SetBool(openedParameterName, false);
            Debug.Log("Keeping chest closed after reset because player is not in range");
        }
    }

    // Called by PlayerProximityDetector
    public void OnPlayerEnterDetectionRange()
    {
        playerInDetectionRange = true;

        // Only open the chest if we're not on cooldown and not teleporting
        if (!isOnCooldown && !isTeleporting)
        {
            OpenChest();
            Debug.Log("Player entered detection range - opening chest");
        }
        else if (isOnCooldown)
        {
            Debug.Log("Player entered detection range but chest is on cooldown");
        }
        else
        {
            Debug.Log("Player entered detection range but teleportation in progress - not opening chest");
        }
    }

    // Called by PlayerProximityDetector
    public void OnPlayerExitDetectionRange()
    {
        playerInDetectionRange = false;

        // Only close the chest if we're not currently teleporting
        if (!isTeleporting)
        {
            CloseChest();
            Debug.Log("Player exited detection range - closing chest");
        }
        else
        {
            Debug.Log("Player exited detection range but teleportation in progress - not closing chest");
        }
    }

    // This will be called for ANY collider attached to the GameObject this script is on
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger entered by: " + other.name + " with tag: " + other.tag);

        // Check if the entering object is the player
        if (other.CompareTag("Player"))
        {
            // Get the root chest this component belongs to
            GameObject thisChest = GetRootEnderChest();

            // Check if this chest is on lockout
            if (chestLockoutTimers.ContainsKey(thisChest))
            {
                float lockoutRemaining = chestLockoutTimers[thisChest];
                Debug.Log("This chest is on lockout for " + lockoutRemaining.ToString("F1") + " more seconds");
                return;
            }

            // Check if we can teleport normally
            if (canTeleport && !teleportInitiated)
            {
                Debug.Log("Player entered " + gameObject.name + " teleport volume. Initiating teleport sequence...");
                teleportInitiated = true;
                StartCoroutine(TeleportPlayer());
            }
        }
    }

    private IEnumerator TeleportPlayer()
    {
        if (!canTeleport) yield break;

        canTeleport = false;
        isTeleporting = true; // Set teleporting flag
        Debug.Log("Teleport sequence started - waiting " + teleportDelay + " seconds before teleporting...");

        // Wait for teleport delay
        yield return new WaitForSeconds(teleportDelay);

        // Get the root GameObject of this chest (the EnderChest object)
        GameObject thisChest = GetRootEnderChest();
        Debug.Log("Current chest identified as: " + thisChest.name);

        // Find all teleport chests in the scene
        GameObject[] allChests = GameObject.FindGameObjectsWithTag(teleportTag);
        List<GameObject> validTargets = new List<GameObject>();

        // Find valid target chests (excluding this one)
        foreach (GameObject chest in allChests)
        {
            // Skip if this is the current chest or part of the current chest
            if (IsPartOfChest(chest, thisChest))
            {
                Debug.Log("Skipping this chest: " + chest.name);
                continue;
            }

            // Find the teleport target (direct child or anywhere in hierarchy)
            Transform targetPoint = FindTargetInHierarchy(chest);

            if (targetPoint != null)
            {
                validTargets.Add(chest);
                Debug.Log("Added valid target chest: " + chest.name);
            }
        }

        // Handle case where no valid targets were found
        if (validTargets.Count == 0)
        {
            Debug.LogError("No valid teleport targets found! Make sure other chests have the 'teleChest' tag and a 'TeleportTarget' child GameObject.");
            teleportInitiated = false;
            canTeleport = true;
            isTeleporting = false; // Reset teleporting flag
            yield break;
        }

        // Select a random target chest
        int randomIndex = Random.Range(0, validTargets.Count);
        GameObject targetChest = validTargets[randomIndex];
        Transform destinationPoint = FindTargetInHierarchy(targetChest);

        Debug.Log("Selected target chest: " + targetChest.name + " with target at " + destinationPoint.position);

        // Find Controller - this is the object we'll teleport
        GameObject controller = FindControllerToTeleport();

        if (controller == null)
        {
            Debug.LogError("Could not find Controller to teleport!");
            teleportInitiated = false;
            canTeleport = true;
            isTeleporting = false; // Reset teleporting flag
            yield break;
        }

        // Store original position for verification
        Vector3 originalPosition = controller.transform.position;

        // Ensure chest is closed before teleporting
        CloseChest();

        // Wait a small amount of time for the animation to start playing
        yield return new WaitForSeconds(0.1f);

        // Directly teleport the controller
        Debug.Log("TELEPORTING Controller from " + originalPosition + " to " + destinationPoint.position);
        controller.transform.position = destinationPoint.position;
        controller.transform.rotation = destinationPoint.rotation;

        // Verify teleportation
        Debug.Log("Controller teleported to " + controller.transform.position);
        Debug.Log("Distance to target: " + Vector3.Distance(controller.transform.position, destinationPoint.position));

        // Set the destination chest to be locked out for the specified duration
        chestLockoutTimers[targetChest] = destinationLockoutDuration;
        Debug.Log("Locked out destination chest " + targetChest.name + " for " + destinationLockoutDuration + " seconds");

        // Wait for cooldown
        yield return new WaitForSeconds(postTeleportCooldown);

        // Reset teleport flags
        teleportInitiated = false;
        canTeleport = true;
        isTeleporting = false;

        Debug.Log("Local teleport cooldown finished. Ready for next teleport.");
    }

    // Get the root EnderChest GameObject that this teleporter belongs to
    private GameObject GetRootEnderChest()
    {
        // Start with this GameObject
        GameObject current = this.gameObject;

        // Go up the hierarchy until we find an object with the teleport tag
        // or until we reach the top of the hierarchy
        while (current != null)
        {
            if (current.CompareTag(teleportTag))
            {
                return current;
            }

            // Move up to parent if it exists
            if (current.transform.parent != null)
            {
                current = current.transform.parent.gameObject;
            }
            else
            {
                // No parent and no tag, so use current object
                break;
            }
        }

        // If we didn't find a parent with the tag, return this GameObject
        return this.gameObject;
    }

    // Check if a GameObject is part of a chest
    private bool IsPartOfChest(GameObject obj, GameObject chest)
    {
        // Check if it's the chest itself
        if (obj == chest)
            return true;

        // Check if it's a child of the chest
        Transform current = obj.transform;
        while (current.parent != null)
        {
            if (current.parent.gameObject == chest)
                return true;

            current = current.parent;
        }

        // Check if the chest is a child of this object
        current = chest.transform;
        while (current.parent != null)
        {
            if (current.parent.gameObject == obj)
                return true;

            current = current.parent;
        }

        return false;
    }

    private Transform FindTargetInHierarchy(GameObject chest)
    {
        // Try direct child first
        Transform target = chest.transform.Find("TeleportTarget");

        // If not found, try recursive search
        if (target == null)
        {
            foreach (Transform child in chest.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("TeleportTarget") && child != chest.transform)
                {
                    target = child;
                    break;
                }
            }
        }

        return target;
    }

    private GameObject FindControllerToTeleport()
    {
        // Method 1: Direct search by name in scene
        GameObject controller = GameObject.Find("Controller");

        // Method 2: Search as child of Player
        if (controller == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                Transform controllerTransform = playerObject.transform.Find("Controller");
                if (controllerTransform != null)
                {
                    controller = controllerTransform.gameObject;
                    Debug.Log("Found Controller as child of Player: " + controller.name);
                }
                else
                {
                    // Method 3: Search all children recursively
                    foreach (Transform child in playerObject.GetComponentsInChildren<Transform>())
                    {
                        if (child.name.Contains("Controller"))
                        {
                            controller = child.gameObject;
                            Debug.Log("Found Controller through recursive search: " + controller.name);
                            break;
                        }
                    }
                }
            }
        }

        return controller;
    }

    // Visualize teleport volumes and targets in the editor
    void OnDrawGizmos()
    {
        // Find the volume if not already set
        if (teleportVolume == null)
        {
            Transform volumeTransform = transform.Find("TeleportVolume");
            if (volumeTransform != null)
            {
                teleportVolume = volumeTransform.gameObject;
            }
        }

        // Find the target if not already set
        if (teleportTarget == null)
        {
            Transform targetTransform = transform.Find("TeleportTarget");
            if (targetTransform != null)
            {
                teleportTarget = targetTransform.gameObject;
            }
        }

        // Find player detect volume if not already set
        if (PlayerDetectVolume == null)
        {
            Transform detectTransform = transform.Find("PlayerDetectVolume");
            if (detectTransform != null)
            {
                PlayerDetectVolume = detectTransform.gameObject;
            }
        }

        // Draw teleport volume
        if (teleportVolume != null)
        {
            Gizmos.color = Color.green;
            Collider col = teleportVolume.GetComponent<Collider>();
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Matrix4x4 originalMatrix = Gizmos.matrix;
                Gizmos.matrix = teleportVolume.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = originalMatrix;
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawWireSphere(teleportVolume.transform.position + sphere.center, sphere.radius);
            }
            else
            {
                // Generic visualization if collider type is unknown
                Gizmos.DrawWireSphere(teleportVolume.transform.position, 0.5f);
            }
        }

        // Draw player detect volume
        if (PlayerDetectVolume != null)
        {
            Gizmos.color = Color.yellow;
            Collider col = PlayerDetectVolume.GetComponent<Collider>();
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Matrix4x4 originalMatrix = Gizmos.matrix;
                Gizmos.matrix = PlayerDetectVolume.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = originalMatrix;
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawWireSphere(PlayerDetectVolume.transform.position + sphere.center, sphere.radius);
            }
            else
            {
                // Generic visualization if collider type is unknown
                Gizmos.DrawWireSphere(PlayerDetectVolume.transform.position, 0.5f);
            }
        }

        // Draw teleport target
        if (teleportTarget != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(teleportTarget.transform.position, 0.2f);
            Gizmos.DrawLine(
                teleportTarget.transform.position,
                teleportTarget.transform.position + Vector3.up * 1.0f);
        }
    }

    // Optional: UI visualization for debugging
    void OnGUI()
    {
        GameObject thisChest = GetRootEnderChest();
        int yPosition = 10;

        // Display lockout status of this chest
        if (chestLockoutTimers.ContainsKey(thisChest))
        {
            float timeRemaining = chestLockoutTimers[thisChest];
            GUI.Label(new Rect(10, yPosition, 300, 30),
                      thisChest.name + " - Teleport Lockout: " + timeRemaining.ToString("F1") + " seconds");
            yPosition += 30;
        }

        // Display cooldown status
        if (isOnCooldown)
        {
            float timeRemaining = 0;
            if (cooldownCoroutine != null)
            {
                // Estimate remaining time
                timeRemaining = chestReopenCooldown - (Time.time % chestReopenCooldown);
            }

            GUI.Label(new Rect(10, yPosition, 300, 30),
                      thisChest.name + " - Reopen Cooldown: Active");
        }

        // Display player detection status
        yPosition += 30;
        string status = playerInDetectionRange ? "In Range" : "Not In Range";
        GUI.Label(new Rect(10, yPosition, 300, 30), "Player: " + status);
    }
}

// Helper class to detect player proximity for animation triggering

public class PlayerProximityDetector : MonoBehaviour
{
    private EnderChestTeleporter parentTeleporter;

    public void SetParentTeleporter(EnderChestTeleporter teleporter)
    {
        parentTeleporter = teleporter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && parentTeleporter != null)
        {
            parentTeleporter.OnPlayerEnterDetectionRange();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && parentTeleporter != null)
        {
            parentTeleporter.OnPlayerExitDetectionRange();
        }
    }
}