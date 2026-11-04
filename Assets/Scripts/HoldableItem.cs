using UnityEngine;
using UnityEngine.InputSystem;

public class HoldableItem : MonoBehaviour
{
    public static HoldableItem Held { get; private set; }

    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Vector3 holdPosition = new Vector3(0.25f, -0.22f, 0.45f);

    public bool IsHeld { get; private set; }
    public bool IsShown => IsHeld && !hidden;

    private PlayerController playerController;
    private Renderer[] renderers;
    private bool hidden;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (IsHeld)
        {
            SetHidden(LetterPuzzle.IsFocused || ItemInspector.IsOpen);
            return;
        }

        if (Held != null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;
        if (ItemInspector.IsOpen || LetterPuzzle.IsFocused)
            return;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null && !playerController.enabled)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.IsChildOf(transform))
            PickUp(cam);
    }

    private void PickUp(Camera cam)
    {
        IsHeld = true;
        Held = this;

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        transform.SetParent(cam.transform, false);
        transform.localPosition = holdPosition;
        transform.localRotation = Quaternion.identity;
    }

    private void SetHidden(bool hide)
    {
        if (hidden == hide)
            return;
        hidden = hide;
        foreach (Renderer r in renderers)
            r.enabled = !hide;
    }

    private void OnDestroy()
    {
        if (Held == this)
            Held = null;
    }
}