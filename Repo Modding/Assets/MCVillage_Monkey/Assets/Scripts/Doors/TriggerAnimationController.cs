using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerAnimationController : MonoBehaviour
{
    [Tooltip("The trigger collider that will detect the player")]
    public Collider triggerVolume;

    [Tooltip("The animator component that will play the animations")]
    public Animator targetAnimator;

    [Tooltip("The tag of the player object to detect")]
    public string playerTag = "Player";

    [Tooltip("The animation trigger parameter to set when player enters")]
    public string enterAnimationTrigger = "PlayerEntered";

    [Tooltip("The animation trigger parameter to set when player exits")]
    public string exitAnimationTrigger = "PlayerExited";

    private void Start()
    {
        // Validate components
        if (triggerVolume == null)
        {
            Debug.LogError("Trigger volume not assigned on " + gameObject.name);
            enabled = false;
            return;
        }

        if (targetAnimator == null)
        {
            Debug.LogError("Target animator not assigned on " + gameObject.name);
            enabled = false;
            return;
        }

        // Make sure the trigger is actually a trigger
        if (!triggerVolume.isTrigger)
        {
            Debug.LogWarning("Assigned collider is not set as a trigger on " + gameObject.name);
            triggerVolume.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        // Subscribe to trigger events
        if (triggerVolume != null && triggerVolume.gameObject != gameObject)
        {
            TriggerEventRelay relay = triggerVolume.gameObject.GetComponent<TriggerEventRelay>();
            if (relay == null)
            {
                relay = triggerVolume.gameObject.AddComponent<TriggerEventRelay>();
            }

            relay.OnTriggerEnterEvent.AddListener(HandleTriggerEnter);
            relay.OnTriggerExitEvent.AddListener(HandleTriggerExit);
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from trigger events
        if (triggerVolume != null && triggerVolume.gameObject != gameObject)
        {
            TriggerEventRelay relay = triggerVolume.gameObject.GetComponent<TriggerEventRelay>();
            if (relay != null)
            {
                relay.OnTriggerEnterEvent.RemoveListener(HandleTriggerEnter);
                relay.OnTriggerExitEvent.RemoveListener(HandleTriggerExit);
            }
        }
    }

    // If the trigger is on this gameObject
    private void OnTriggerEnter(Collider other)
    {
        if (triggerVolume != null && triggerVolume.gameObject == gameObject && other.CompareTag(playerTag))
        {
            PlayEnterAnimation();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (triggerVolume != null && triggerVolume.gameObject == gameObject && other.CompareTag(playerTag))
        {
            PlayExitAnimation();
        }
    }

    // Event handlers for external trigger
    public void HandleTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayEnterAnimation();
        }
    }

    public void HandleTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayExitAnimation();
        }
    }

    public void PlayEnterAnimation()
    {
        if (targetAnimator != null && !string.IsNullOrEmpty(enterAnimationTrigger))
        {
            targetAnimator.SetTrigger(enterAnimationTrigger);
        }
    }

    public void PlayExitAnimation()
    {
        if (targetAnimator != null && !string.IsNullOrEmpty(exitAnimationTrigger))
        {
            targetAnimator.SetTrigger(exitAnimationTrigger);
        }
    }
}

// Helper class to relay trigger events from one GameObject to another
public class TriggerEventRelay : MonoBehaviour
{
    [System.Serializable]
    public class TriggerEvent : UnityEngine.Events.UnityEvent<Collider> { }

    public TriggerEvent OnTriggerEnterEvent = new TriggerEvent();
    public TriggerEvent OnTriggerExitEvent = new TriggerEvent();

    private void OnTriggerEnter(Collider other)
    {
        OnTriggerEnterEvent.Invoke(other);
    }

    private void OnTriggerExit(Collider other)
    {
        OnTriggerExitEvent.Invoke(other);
    }
}