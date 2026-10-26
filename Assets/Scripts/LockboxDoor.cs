using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on the lockbox door's HINGE (an empty object placed on the hinge edge, with the
/// door mesh + collider as a child). Once Unlock() is called, aim at the door, hold left mouse
/// and drag to swing it open. Let go past the threshold and it swings the rest of the way.
/// </summary>
public class LockboxDoor : MonoBehaviour
{
    [Header("Hinge")]
    [Tooltip("Local axis the door rotates around. Y = side-hinged door, X = top lid.")]
    [SerializeField] private Vector3 hingeAxis = Vector3.up;
    [Tooltip("How far it opens, in degrees. Flip the sign if it swings into the box.")]
    [SerializeField] private float openAngle = 110f;

    [Header("Dragging")]
    [Tooltip("Screen direction you drag the mouse to open it. (0,1) = drag up, (-1,0) = drag left.")]
    [SerializeField] private Vector2 openDragDirection = new Vector2(-1f, 0f);
    [SerializeField, Min(0.01f)] private float degreesPerPixel = 0.4f;
    [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
    [Tooltip("Let go past this fraction of openAngle and the door finishes opening by itself.")]
    [SerializeField, Range(0f, 1f)] private float snapOpenThreshold = 0.6f;
    [Tooltip("Let go before the threshold and the door swings back shut.")]
    [SerializeField] private bool closeIfReleasedEarly = true;
    [SerializeField, Min(0.05f)] private float snapDuration = 0.35f;

    [Header("State")]
    [SerializeField] private bool startsLocked = true;

    [Header("Events")]
    public UnityEvent onFullyOpened;

    public bool IsLocked { get; private set; }
    public bool IsFullyOpen { get; private set; }

    private Quaternion closedRotation;
    private float currentAngle;
    private bool dragging;
    private Coroutine snapRoutine;
    private PlayerController playerController;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        IsLocked = startsLocked;
    }

    /// <summary>Hook this to LockboxKeypadController's On Unlocked event.</summary>
    public void Unlock()
    {
        IsLocked = false;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (dragging)
        {
            UpdateDrag(mouse);
            return;
        }

        if (IsLocked || IsFullyOpen || snapRoutine != null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (ItemInspector.IsOpen)
            return;

        // Same rule as InspectableItem: if the player controller is off, another UI owns the mouse.
        playerController ??= FindAnyObjectByType<PlayerController>();
        if (playerController != null && !playerController.enabled)
            return;

        if (AimingAtDoor())
            BeginDrag();
    }

    private bool AimingAtDoor()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // crosshair
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        Transform hitTransform = hit.collider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private void BeginDrag()
    {
        dragging = true;

        // Freeze look/movement so the mouse drives the door instead of the camera.
        if (playerController != null)
            playerController.enabled = false;
    }

    private void UpdateDrag(Mouse mouse)
    {
        Vector2 delta = mouse.delta.ReadValue();
        float amount = Vector2.Dot(delta, openDragDirection.normalized) * degreesPerPixel;

        // amount is in "degrees toward open", so it works for negative openAngle too.
        float progress = Mathf.Clamp01((Mathf.Abs(currentAngle) + amount) / Mathf.Abs(openAngle));
        SetAngle(progress * openAngle);

        if (!mouse.leftButton.isPressed)
            EndDrag();
    }

    private void EndDrag()
    {
        dragging = false;

        if (playerController != null)
            playerController.enabled = true;

        float progress = Mathf.Abs(currentAngle) / Mathf.Abs(openAngle);
        if (progress >= snapOpenThreshold)
            snapRoutine = StartCoroutine(SnapTo(openAngle, true));
        else if (closeIfReleasedEarly)
            snapRoutine = StartCoroutine(SnapTo(0f, false));
    }

    private IEnumerator SnapTo(float targetAngle, bool opening)
    {
        float startAngle = currentAngle;
        for (float elapsed = 0f; elapsed < snapDuration; elapsed += Time.deltaTime)
        {
            SetAngle(Mathf.Lerp(startAngle, targetAngle, Mathf.SmoothStep(0f, 1f, elapsed / snapDuration)));
            yield return null;
        }

        SetAngle(targetAngle);
        snapRoutine = null;

        if (opening)
        {
            IsFullyOpen = true;
            onFullyOpened?.Invoke();
        }
    }

    private void SetAngle(float angle)
    {
        currentAngle = angle;
        transform.localRotation = closedRotation * Quaternion.AngleAxis(angle, hingeAxis.normalized);
    }

    private void OnDisable()
    {
        // Never leave the player frozen if this gets turned off mid-drag.
        if (dragging && playerController != null)
            playerController.enabled = true;
        dragging = false;
    }
}
