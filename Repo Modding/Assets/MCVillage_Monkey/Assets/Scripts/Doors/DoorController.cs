using UnityEngine;
using System.Collections.Generic;

public class AutoDoorController : MonoBehaviour
{
    [Header("Door Settings")]
    public Transform pivotPoint;
    public float rotationAngle = 90f;
    public float openSpeed = 3.0f;

    [Header("Player Detection")]
    public LayerMask playerLayer;
    public float detectionRadius = 3f;

    [Header("Audio Settings")]
    public bool useAudio = false;
    [Tooltip("Will be played when door starts opening")]
    public List<AudioClip> openSounds;
    [Tooltip("Will be played when door starts closing")]
    public List<AudioClip> closeSounds;
    [Range(0f, 1f)]
    public float soundVolume = 0.7f;

    [Header("Animation Settings")]
    public bool useAnimation = false;
    [Tooltip("For using a separate Animator component")]
    public Animator doorAnimator;
    [Tooltip("Animation clips that can be played when opening")]
    public List<AnimationClip> openAnimations;
    [Tooltip("Animation clips that can be played when closing")]
    public List<AnimationClip> closeAnimations;
    [Tooltip("For using Animation component instead of Animator")]
    public Animation doorAnimation;

    [Header("Debug")]
    public bool debugMode = true;

    private Transform playerTransform;
    private bool isDoorOpen = false;
    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private Vector3 closedPosition;
    private bool playerInRange = false;
    private bool playerBehindDoor = false;
    private AudioSource audioSource;

    // Add state tracking to prevent sound/animation spam
    private bool isPlayingOpenSound = false;
    private bool isPlayingCloseSound = false;

    void Start()
    {
        // Set up pivot point if none provided
        if (pivotPoint == null)
        {
            GameObject pivotObject = new GameObject(name + "_Pivot");
            Renderer doorRenderer = GetComponent<Renderer>();

            if (doorRenderer != null)
            {
                // Position pivot at the left edge of the door
                Vector3 pivotPos = transform.position;
                pivotPos -= transform.right * (doorRenderer.bounds.size.x / 2);
                pivotObject.transform.position = pivotPos;
            }
            else
            {
                pivotObject.transform.position = transform.position;
            }

            pivotObject.transform.rotation = transform.rotation;
            pivotPoint = pivotObject.transform;
        }

        // Make door a child of pivot
        Transform originalParent = transform.parent;
        transform.SetParent(pivotPoint, true);

        // Store initial state
        closedRotation = pivotPoint.rotation;
        targetRotation = closedRotation;
        closedPosition = transform.localPosition;

        // Set up audio if enabled
        if (useAudio)
        {
            // Check if we have an AudioSource, add one if not
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // 3D sound
                audioSource.volume = soundVolume;
            }
        }

        // Set up animation if enabled
        if (useAnimation)
        {
            // If using Animator but none assigned, try to get from this object
            if (doorAnimator == null)
            {
                doorAnimator = GetComponent<Animator>();
            }

            // If using Animation but none assigned, try to get from this object
            if (doorAnimation == null && doorAnimator == null)
            {
                doorAnimation = GetComponent<Animation>();
            }

            // Set up animation clips if using Animation component
            if (doorAnimation != null && openAnimations.Count > 0)
            {
                foreach (AnimationClip clip in openAnimations)
                {
                    if (clip != null && !doorAnimation.GetClip(clip.name))
                    {
                        doorAnimation.AddClip(clip, clip.name);
                    }
                }
            }

            if (doorAnimation != null && closeAnimations.Count > 0)
            {
                foreach (AnimationClip clip in closeAnimations)
                {
                    if (clip != null && !doorAnimation.GetClip(clip.name))
                    {
                        doorAnimation.AddClip(clip, clip.name);
                    }
                }
            }
        }

        if (debugMode)
        {
            Debug.Log("Auto door initialized with pivot at: " + pivotPoint.position);
            Debug.Log("Audio enabled: " + useAudio + ", Animation enabled: " + useAnimation);
        }
    }

    void Update()
    {
        // Check for player
        DetectPlayer();

        // Handle door state
        if (playerInRange && !isDoorOpen)
        {
            OpenDoor();
        }
        else if (!playerInRange && isDoorOpen)
        {
            CloseDoor();
        }

        // Smooth rotation
        if (pivotPoint.rotation != targetRotation)
        {
            pivotPoint.rotation = Quaternion.Slerp(pivotPoint.rotation, targetRotation, openSpeed * Time.deltaTime);

            // Check if door is almost fully open or closed
            if (Quaternion.Angle(pivotPoint.rotation, targetRotation) < 0.1f)
            {
                pivotPoint.rotation = targetRotation;

                // Reset sound flags when finished moving
                isPlayingOpenSound = false;
                isPlayingCloseSound = false;
            }
        }
    }

    void DetectPlayer()
    {
        // Find if player is in range
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);

        // Update player info
        if (hitColliders.Length > 0)
        {
            // Player entered range
            playerTransform = hitColliders[0].transform;
            playerInRange = true;

            // Determine which side of the door they're on
            Vector3 doorToPlayer = playerTransform.position - transform.position;
            doorToPlayer.y = 0; // Project onto XZ plane

            float dot = Vector3.Dot(transform.forward, doorToPlayer.normalized);
            playerBehindDoor = (dot < 0);
        }
        else
        {
            // Player exited range
            playerInRange = false;
        }
    }

    void OpenDoor()
    {
        // Don't open if already open
        if (isDoorOpen) return;

        // Choose rotation direction based on player position
        float angle = playerBehindDoor ? rotationAngle : -rotationAngle;

        // Set target rotation for the pivot point
        targetRotation = closedRotation * Quaternion.Euler(0, angle, 0);

        if (debugMode)
        {
            Debug.Log("Opening door " + (playerBehindDoor ? "NORTH" : "SOUTH"));
        }

        // Play sound if enabled
        if (useAudio && !isPlayingOpenSound && openSounds.Count > 0)
        {
            PlayRandomSound(openSounds);
            isPlayingOpenSound = true;
        }

        // Play animation if enabled
        if (useAnimation)
        {
            PlayAnimation(true);
        }

        // If not using smooth rotation, apply immediately
        if (openSpeed <= 0)
        {
            pivotPoint.rotation = targetRotation;
        }

        isDoorOpen = true;
    }

    void CloseDoor()
    {
        // Don't close if already closed
        if (!isDoorOpen) return;

        // Set target rotation back to closed
        targetRotation = closedRotation;

        if (debugMode)
        {
            Debug.Log("Closing door");
        }

        // Play sound if enabled
        if (useAudio && !isPlayingCloseSound && closeSounds.Count > 0)
        {
            PlayRandomSound(closeSounds);
            isPlayingCloseSound = true;
        }

        // Play animation if enabled
        if (useAnimation)
        {
            PlayAnimation(false);
        }

        // If not using smooth rotation, apply immediately
        if (openSpeed <= 0)
        {
            pivotPoint.rotation = targetRotation;
        }

        isDoorOpen = false;
    }

    void PlayRandomSound(List<AudioClip> sounds)
    {
        if (audioSource != null && sounds.Count > 0)
        {
            // Choose a random sound from the list
            int index = Random.Range(0, sounds.Count);
            AudioClip clipToPlay = sounds[index];

            if (clipToPlay != null)
            {
                audioSource.volume = soundVolume;
                audioSource.PlayOneShot(clipToPlay);

                if (debugMode)
                {
                    Debug.Log("Playing sound: " + clipToPlay.name);
                }
            }
        }
    }

    void PlayAnimation(bool isOpening)
    {
        // Using Animator component
        if (doorAnimator != null)
        {
            if (isOpening)
            {
                doorAnimator.SetBool("Open", true);

                if (debugMode)
                {
                    Debug.Log("Playing Animator opening animation");
                }
            }
            else
            {
                doorAnimator.SetBool("Open", false);

                if (debugMode)
                {
                    Debug.Log("Playing Animator closing animation");
                }
            }
        }
        // Using Animation component
        else if (doorAnimation != null)
        {
            List<AnimationClip> clipsToUse = isOpening ? openAnimations : closeAnimations;

            if (clipsToUse.Count > 0)
            {
                // Choose a random animation from the list
                int index = Random.Range(0, clipsToUse.Count);
                AnimationClip clipToPlay = clipsToUse[index];

                if (clipToPlay != null)
                {
                    doorAnimation.Play(clipToPlay.name);

                    if (debugMode)
                    {
                        Debug.Log("Playing Animation clip: " + clipToPlay.name);
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw door direction
        if (Application.isPlaying)
        {
            // Forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 2);

            // Draw pivot point
            if (pivotPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(pivotPoint.position, 0.1f);
                Gizmos.DrawLine(transform.position, pivotPoint.position);
            }

            // Draw player position if detected
            if (playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, playerTransform.position);
                Gizmos.DrawSphere(playerTransform.position, 0.2f);
            }
        }
    }
}