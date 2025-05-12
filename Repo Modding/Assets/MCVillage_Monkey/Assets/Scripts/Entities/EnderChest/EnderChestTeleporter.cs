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

    [Header("Debug Settings")]
    [Tooltip("Enable/disable all debug logging")]
    [SerializeField] private bool enableDebugLogs = true;
    [Tooltip("Enable/disable Unity GUI debug overlays")]
    [SerializeField] private bool enableGUIDebugging = false;
    [Tooltip("Enable/disable debug key commands (R to reset)")]
    [SerializeField] private bool enableDebugKeyCommands = false;
    [Tooltip("Debug log cooldown time remaining")]
    [SerializeField] private bool logCooldownRemaining = false;
    [Tooltip("Draw debug gizmos in scene view")]
    [SerializeField] private bool drawDebugGizmos = true;
    [Tooltip("Interval for cooldown debug logs (seconds)")]
    [SerializeField] private float cooldownLogInterval = 0.5f;
    [Tooltip("Key to reset teleporter for testing")]
    [SerializeField] private KeyCode resetKey = KeyCode.R;

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
        DebugLog(gameObject.name + " found " + enderChests.Length + " EnderChests in the scene.");

        // Log information about each found chest for debugging
        foreach (GameObject chest in enderChests)
        {
            DebugLog("Found EnderChest: " + chest.name + " at position " + chest.transform.position);

            // Check if it has TeleportTarget
            Transform target = chest.transform.Find("TeleportTarget");
            if (target != null)
            {
                DebugLog("  - Has TeleportTarget at position " + target.position);
            }
            else
            {
                DebugLogWarning("  - Missing TeleportTarget! Add a child named 'TeleportTarget' to " + chest.name);
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
                DebugLog($"Lockout expired for chest: {chest.name}");
            }
            else
            {
                // Update timer value
                chestLockoutTimers[chest] = timeRemaining;
            }
        }

        // DEBUG: Add key press to reset the teleporter for testing
        if (enableDebugKeyCommands && Input.GetKeyDown(resetKey))
        {
            ResetTeleporter();
            DebugLog($"Teleporter reset triggered by '{resetKey}' key");
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
                    DebugLogWarning("No Animator component found on " + gameObject.name +
                                    " or its children. Chest animations will not play.");
                }
                else
                {
                    DebugLog("Found Animator in child of " + gameObject.name);
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
                DebugLog("Found PlayerDetectVolume: " + PlayerDetectVolume.name);

                // Make sure it has a collider set as trigger
                Collider detectCollider = PlayerDetectVolume.GetComponent<Collider>();
                if (detectCollider != null)
                {
                    detectCollider.isTrigger = true;
                }
                else
                {
                    DebugLogWarning("PlayerDetectVolume has no collider! Adding a BoxCollider");
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
                DebugLogWarning("No PlayerDetectVolume found as child! Player proximity detection won't work.");
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
            DebugLog("TeleportVolume not found as direct child, trying recursive search...");
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("TeleportVolume"))
                {
                    volumeTransform = child;
                    DebugLog("Found TeleportVolume through recursive search: " + child.name);
                    break;
                }
            }
        }

        // If still not found, check if this GameObject itself has a collider
        if (volumeTransform == null)
        {
            DebugLog("No TeleportVolume child found, checking if this GameObject has a collider...");
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
            {
                volumeTransform = transform;
                DebugLog("Using this GameObject as TeleportVolume since it has a collider");
            }
        }

        // If we found a transform, set up the teleport volume
        if (volumeTransform != null)
        {
            teleportVolume = volumeTransform.gameObject;
            DebugLog("TeleportVolume set to: " + teleportVolume.name);

            // Check for collider component
            Collider volumeCollider = teleportVolume.GetComponent<Collider>();

            // If no collider exists, add a box collider as fallback
            if (volumeCollider == null)
            {
                DebugLogWarning("No collider found on TeleportVolume, adding a BoxCollider");
                volumeCollider = teleportVolume.AddComponent<BoxCollider>();
                BoxCollider boxCollider = volumeCollider as BoxCollider;
                boxCollider.size = new Vector3(1.5f, 1.5f, 1.5f);
                boxCollider.center = Vector3.zero;
                boxCollider.isTrigger = true;
            }
            else
            {
                volumeCollider.isTrigger = true;
                DebugLog("Found and configured existing collider on TeleportVolume");
            }

            // Add collider component to this GameObject if we're not already using it
            if (teleportVolume != gameObject)
            {
                // Set up this script to receive trigger events
                Rigidbody rb = teleportVolume.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    DebugLog("Adding non-physics Rigidbody to TeleportVolume for trigger detection");
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

                    // Copy debug settings
                    volumeTeleporter.enableDebugLogs = this.enableDebugLogs;
                    volumeTeleporter.enableGUIDebugging = this.enableGUIDebugging;
                    volumeTeleporter.enableDebugKeyCommands = this.enableDebugKeyCommands;
                    volumeTeleporter.logCooldownRemaining = this.logCooldownRemaining;
                    volumeTeleporter.drawDebugGizmos = this.drawDebugGizmos;
                    volumeTeleporter.cooldownLogInterval = this.cooldownLogInterval;
                    volumeTeleporter.resetKey = this.resetKey;

                    // Disable this instance to prevent duplicate actions
                    DebugLog("Moved teleporter script to TeleportVolume object and disabled this instance");
                    this.enabled = false;
                }
            }
        }
        else
        {
            DebugLogError("No TeleportVolume found or created! Teleportation will not work.");
        }
    }

    private void FindTeleportTarget()
    {
        // First try direct child
        Transform targetTransform = transform.Find("TeleportTarget");

        // If not found as direct child, try deeper search
        if (targetTransform == null)
        {
            DebugLog("TeleportTarget not found as direct child, trying recursive search...");
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("TeleportTarget"))
                {
                    targetTransform = child;
                    DebugLog("Found TeleportTarget through recursive search: " + child.name);
                    break;
                }
            }
        }

        // If still not found, create one
        if (targetTransform == null)
        {
            DebugLogWarning("TeleportTarget not found, creating one...");
            GameObject targetObj = new GameObject("TeleportTarget");
            targetObj.transform.parent = transform;
            targetObj.transform.localPosition = new Vector3(0, 0, 2); // 2 units in front
            targetObj.transform.localRotation = Quaternion.identity;
            targetTransform = targetObj.transform;
        }

        teleportTarget = targetTransform.gameObject;
        DebugLog("TeleportTarget set to: " + teleportTarget.name + " at position " + teleportTarget.transform.position);
    }

    // Animation control methods
    public void OpenChest()
    {
        // Don't open if on cooldown, teleporting, or no animator
        if (isOnCooldown)
        {
            DebugLog("Cannot open chest - cooldown active");
            return;
        }

        if (isTeleporting)
        {
            DebugLog("Cannot open chest - teleportation in progress");
            return;
        }

        if (chestAnimator == null)
        {
            DebugLogWarning("Cannot open chest - no animator assigned!");
            return;
        }

        DebugLog("Setting chest to open state: " + openedParameterName);
        chestAnimator.SetBool(openedParameterName, true);
    }

    public void CloseChest()
    {
        if (chestAnimator != null)
        {
            DebugLog("Setting chest to closed state: " + openedParameterName);
            chestAnimator.SetBool(openedParameterName, false);

            // Start cooldown when chest is closed
            StartChestCooldown();
        }
        else
        {
            DebugLogWarning("Cannot close chest - no animator assigned!");
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
        DebugLog("Starting chest cooldown for " + chestReopenCooldown + " seconds");
        isOnCooldown = true;

        float startTime = Time.time;
        float elapsedTime = 0f;
        float lastLogTime = 0f;

        while (elapsedTime < chestReopenCooldown)
        {
            elapsedTime = Time.time - startTime;
            float remainingTime = chestReopenCooldown - elapsedTime;

            // Log remaining time at specified intervals
            if (logCooldownRemaining &&
                (Time.time - lastLogTime >= cooldownLogInterval))
            {
                DebugLog("Cooldown remaining: " + remainingTime.ToString("F1") + " seconds");
                lastLogTime = Time.time;
            }

            yield return null;
        }

        isOnCooldown = false;
        DebugLog("Chest cooldown finished - chest can now be reopened");

        // If player is still in range, reopen the chest
        if (playerInDetectionRange && !isTeleporting)
        {
            OpenChest();
            DebugLog("Player still in range after cooldown - reopening chest");
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

        DebugLog("Teleporter state has been reset");

        // Open chest if player is in range
        if (playerInDetectionRange && chestAnimator != null)
        {
            chestAnimator.SetBool(openedParameterName, true);
            DebugLog("Reopening chest after reset because player is in range");
        }
        else if (chestAnimator != null)
        {
            chestAnimator.SetBool(openedParameterName, false);
            DebugLog("Keeping chest closed after reset because player is not in range");
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
            DebugLog("Player entered detection range - opening chest");
        }
        else if (isOnCooldown)
        {
            DebugLog("Player entered detection range but chest is on cooldown");
        }
        else
        {
            DebugLog("Player entered detection range but teleportation in progress - not opening chest");
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
            DebugLog("Player exited detection range - closing chest");
        }
        else
        {
            DebugLog("Player exited detection range but teleportation in progress - not closing chest");
        }
    }

    // This will be called for ANY collider attached to the GameObject this script is on
    private void OnTriggerEnter(Collider other)
    {
        DebugLog("Trigger entered by: " + other.name + " with tag: " + other.tag);

        // Check if the entering object is the player
        if (other.CompareTag("Player"))
        {
            // Get the root chest this component belongs to
            GameObject thisChest = GetRootEnderChest();

            // Check if this chest is on lockout
            if (chestLockoutTimers.ContainsKey(thisChest))
            {
                float lockoutRemaining = chestLockoutTimers[thisChest];
                DebugLog("This chest is on lockout for " + lockoutRemaining.ToString("F1") + " more seconds");
                return;
            }

            // Check if we can teleport normally
            if (canTeleport && !teleportInitiated)
            {
                DebugLog("Player entered " + gameObject.name + " teleport volume. Initiating teleport sequence...");
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
        DebugLog("Teleport sequence started - waiting " + teleportDelay + " seconds before teleporting...");

        // Wait for teleport delay
        yield return new WaitForSeconds(teleportDelay);

        // Get the root GameObject of this chest (the EnderChest object)
        GameObject thisChest = GetRootEnderChest();
        DebugLog("Current chest identified as: " + thisChest.name);

        // Find all teleport chests in the scene
        GameObject[] allChests = GameObject.FindGameObjectsWithTag(teleportTag);
        List<GameObject> validTargets = new List<GameObject>();

        // Find valid target chests (excluding this one)
        foreach (GameObject chest in allChests)
        {
            // Skip if this is the current chest or part of the current chest
            if (IsPartOfChest(chest, thisChest))
            {
                DebugLog("Skipping this chest: " + chest.name);
                continue;
            }

            // Find the teleport target (direct child or anywhere in hierarchy)
            Transform targetPoint = FindTargetInHierarchy(chest);

            if (targetPoint != null)
            {
                validTargets.Add(chest);
                DebugLog("Added valid target chest: " + chest.name);
            }
        }

        // Handle case where no valid targets were found
        if (validTargets.Count == 0)
        {
            DebugLogError("No valid teleport targets found! Make sure other chests have the 'teleChest' tag and a 'TeleportTarget' child GameObject.");
            teleportInitiated = false;
            canTeleport = true;
            isTeleporting = false; // Reset teleporting flag
            yield break;
        }

        // Select a random target chest
        int randomIndex = Random.Range(0, validTargets.Count);
        GameObject targetChest = validTargets[randomIndex];
        Transform destinationPoint = FindTargetInHierarchy(targetChest);

        DebugLog("Selected target chest: " + targetChest.name + " with target at " + destinationPoint.position);

        // Find Controller - this is the object we'll teleport
        GameObject controller = FindControllerToTeleport();

        if (controller == null)
        {
            DebugLogError("Could not find Controller to teleport!");
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
        DebugLog("TELEPORTING Controller from " + originalPosition + " to " + destinationPoint.position);
        controller.transform.position = destinationPoint.position;
        controller.transform.rotation = destinationPoint.rotation;

        // Verify teleportation
        DebugLog("Controller teleported to " + controller.transform.position);
        DebugLog("Distance to target: " + Vector3.Distance(controller.transform.position, destinationPoint.position));

        // Set the destination chest to be locked out for the specified duration
        chestLockoutTimers[targetChest] = destinationLockoutDuration;
        DebugLog("Locked out destination chest " + targetChest.name + " for " + destinationLockoutDuration + " seconds");

        // Wait for cooldown
        yield return new WaitForSeconds(postTeleportCooldown);

        // Reset teleport flags
        teleportInitiated = false;
        canTeleport = true;
        isTeleporting = false;

        DebugLog("Local teleport cooldown finished. Ready for next teleport.");
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
                    DebugLog("Found Controller as child of Player: " + controller.name);
                }
                else
                {
                    // Method 3: Search all children recursively
                    foreach (Transform child in playerObject.GetComponentsInChildren<Transform>())
                    {
                        if (child.name.Contains("Controller"))
                        {
                            controller = child.gameObject;
                            DebugLog("Found Controller through recursive search: " + controller.name);
                            break;
                        }
                    }
                }
            }
        }

        return controller;
    }

    // Wrapper methods for Debug that check if debugging is enabled
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{gameObject.name}] {message}");
        }
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[{gameObject.name}] {message}");
        }
    }

    private void DebugLogError(string message)
    {
        // Always log errors, even if debug logs are disabled
        Debug.LogError($"[{gameObject.name}] {message}");
    }

    // Visualize teleport volumes and targets in the editor
    void OnDrawGizmos()
    {
        if (!drawDebugGizmos)
            return;

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
        if (!enableGUIDebugging)
            return;

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