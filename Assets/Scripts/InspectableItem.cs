using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on any object (it needs a collider) that the player can click to pick up and look at.
/// Aim the crosshair at it and left-click. An inventory can also call Inspect() directly.
/// </summary>
public class InspectableItem : MonoBehaviour
{
    [Header("Shown in the inspect view")]
    [SerializeField] private string displayName = "Item";
    [SerializeField, TextArea(2, 4)] private string description = "";

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
    [Tooltip("Hide the object in the world while it's being inspected (looks like you picked it up).")]
    [SerializeField] private bool hideWhileInspecting = true;
    [Tooltip("Keep the object gone after closing. The player took it.")]
    [SerializeField] private bool collectOnInspect = false;

    [Header("Presentation")]
    [Tooltip("Optional: a different model/prefab to show instead of this object.")]
    [SerializeField] private GameObject inspectModel;
    [Tooltip("Off: starts facing you the way you saw it in the world. On: starts at the rotation below.")]
    [SerializeField] private bool useCustomStartRotation = false;
    [SerializeField] private Vector3 customStartRotation = Vector3.zero;

    [Header("Events")]
    public UnityEvent onInspectOpened;
    public UnityEvent onInspectClosed;

    public string DisplayName => displayName;
    public string Description => description;
    public GameObject InspectModel => inspectModel;
    public bool IsCollected { get; private set; }

    private PlayerController playerController;
    private bool hiddenByInspector;

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (ItemInspector.IsOpen)
            return;

        // If the player controller is off, some other UI (like the keypad) has control.
        playerController ??= FindAnyObjectByType<PlayerController>();
        if (playerController != null && !playerController.enabled)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // crosshair
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            return;

        Transform hitTransform = hit.collider.transform;
        if (hitTransform != transform && !hitTransform.IsChildOf(transform))
            return;

        Inspect();
    }

    /// <summary>Opens the inspect view for this item. Works even if the item is hidden/collected.</summary>
    public void Inspect()
    {
        ItemInspector.GetOrCreate().Open(this);
    }

    /// <summary>Rotation the inspected copy starts at (relative to the inspect camera).</summary>
    public Quaternion GetStartRotation(Transform viewer)
    {
        if (useCustomStartRotation)
            return Quaternion.Euler(customStartRotation);

        if (viewer == null)
            return Quaternion.identity;

        // Same orientation relative to the view as it had in the world, so opening feels continuous.
        return Quaternion.Inverse(viewer.rotation) * transform.rotation;
    }

    internal void NotifyOpened()
    {
        if ((hideWhileInspecting || collectOnInspect) && gameObject.activeSelf)
        {
            hiddenByInspector = true;
            gameObject.SetActive(false);
        }
        onInspectOpened?.Invoke();
    }

    internal void NotifyClosed()
    {
        if (collectOnInspect)
            IsCollected = true;
        else if (hiddenByInspector)
            gameObject.SetActive(true);

        hiddenByInspector = false;
        onInspectClosed?.Invoke();
    }
}