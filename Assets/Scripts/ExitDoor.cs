using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// The room's exit. Put it on the door's root object and drag the swinging part into Panel.
/// It starts locked: hook Unlock() to the door keypad's On Unlocked event. Once unlocked, clicking the
/// door with the crosshair swings it open, away from the player, and calls GameController.Win().
/// Clicking it while locked gives a small rattle.
///
/// The hinge is worked out from the panel's bounds, so the model's pivot doesn't matter. This assumes
/// the door lines up with the world X or Z axis. Select the door to see the hinge as a yellow line.
/// </summary>
public class ExitDoor : MonoBehaviour
{
    [Header("Door")]
    [Tooltip("The part that swings: the door panel, not the frame.")]
    [SerializeField] private Transform panel;
    [Tooltip("Which end of the panel the hinge is on. Flip it if the door swings from the handle side.")]
    [SerializeField] private bool hingeOnPositiveSide;
    [SerializeField, Range(10f, 170f)] private float openAngle = 100f;
    [SerializeField, Min(0.1f)] private float openDuration = 1.4f;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
    [SerializeField] private bool startsLocked = true;

    [Header("Win")]
    [Tooltip("Call GameController.Win() the moment the door starts to open.")]
    [SerializeField] private bool winOnOpen = true;

    [Header("Events")]
    public UnityEvent onLockedClick;
    public UnityEvent onOpened;

    public bool IsLocked { get; private set; }
    /// <summary>True from the moment the door starts opening.</summary>
    public bool IsOpen { get; private set; }

    private Vector3 hingePoint;
    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private Vector3 closedCenter;
    private PlayerController playerController;
    private Coroutine motion;

    private void Awake()
    {
        IsLocked = startsLocked;
        if (panel == null)
            panel = transform;

        closedPosition = panel.position;
        closedRotation = panel.rotation;
        Bounds bounds = PanelBounds();
        closedCenter = bounds.center;
        hingePoint = HingeFromBounds(bounds);
    }

    /// <summary>Hook this to the door keypad's On Unlocked event.</summary>
    public void Unlock()
    {
        IsLocked = false;
    }

    /// <summary>Opens the door now (if it's unlocked), swinging away from the camera.</summary>
    public void Open()
    {
        if (IsLocked || IsOpen)
            return;

        Camera cam = Camera.main;
        OpenAwayFrom(cam != null ? cam.transform.position : transform.position + transform.forward);
    }

    private void Update()
    {
        if (IsOpen || motion != null)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        // Another UI owns the mouse.
        if (ItemInspector.IsOpen || LetterPuzzle.IsFocused || LockboxKeypadController.Active != null)
            return;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null && !playerController.enabled)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            return;
        if (!hit.collider.transform.IsChildOf(transform))
            return;

        if (IsLocked)
        {
            onLockedClick?.Invoke();
            motion = StartCoroutine(Rattle());
            return;
        }

        OpenAwayFrom(cam.transform.position);
    }

    private void OpenAwayFrom(Vector3 viewer)
    {
        IsOpen = true;

        if (winOnOpen)
        {
            GameController game = FindAnyObjectByType<GameController>();
            if (game != null)
                game.Win();
        }

        if (motion != null)
            StopCoroutine(motion);
        motion = StartCoroutine(Swing(SwingSign(viewer) * openAngle));
    }

    /// <summary>+1 or -1: whichever way moves the door's free edge away from the viewer.</summary>
    private float SwingSign(Vector3 viewer)
    {
        Vector3 freeEdge = closedCenter + (closedCenter - hingePoint);
        Vector3 plus = hingePoint + Quaternion.AngleAxis(30f, Vector3.up) * (freeEdge - hingePoint);
        Vector3 minus = hingePoint + Quaternion.AngleAxis(-30f, Vector3.up) * (freeEdge - hingePoint);
        return (plus - viewer).sqrMagnitude >= (minus - viewer).sqrMagnitude ? 1f : -1f;
    }

    private IEnumerator Swing(float targetAngle)
    {
        for (float t = 0f; t < openDuration; t += Time.deltaTime)
        {
            SetAngle(Mathf.SmoothStep(0f, targetAngle, t / openDuration));
            yield return null;
        }

        SetAngle(targetAngle);
        motion = null;
        onOpened?.Invoke();
    }

    private IEnumerator Rattle()
    {
        const float duration = 0.3f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAngle(Mathf.Sin(t * 60f) * 1.5f * (1f - t / duration));
            yield return null;
        }

        SetAngle(0f);
        motion = null;
    }

    /// <summary>Rotates the panel around the hinge line, relative to its closed pose.</summary>
    private void SetAngle(float angle)
    {
        Quaternion turn = Quaternion.AngleAxis(angle, Vector3.up);
        panel.SetPositionAndRotation(hingePoint + turn * (closedPosition - hingePoint), turn * closedRotation);
    }

    private Bounds PanelBounds()
    {
        Transform target = panel != null ? panel : transform;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(target.position, Vector3.one * 0.1f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>The door's width runs along whichever of world X or Z its bounds are longer in.</summary>
    private static Vector3 WidthAxis(Bounds bounds)
    {
        return bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
    }

    private Vector3 HingeFromBounds(Bounds bounds)
    {
        Vector3 axis = WidthAxis(bounds);
        float halfWidth = Vector3.Dot(bounds.extents, axis);
        return bounds.center + axis * (hingeOnPositiveSide ? halfWidth : -halfWidth);
    }

    private void OnDrawGizmosSelected()
    {
        Bounds bounds = PanelBounds();
        Vector3 hinge = Application.isPlaying ? hingePoint : HingeFromBounds(bounds);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(hinge.x, bounds.min.y, hinge.z), new Vector3(hinge.x, bounds.max.y, hinge.z));
    }
}
