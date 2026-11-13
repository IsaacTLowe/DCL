using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A clickable piece of an InspectableItem (a latch, a screw, a tool lying inside a box).
/// Put it on a child of the item; it needs a collider on itself or one of its children.
/// While the item is open in the inspect view, clicking this piece runs onUsed instead of
/// starting a drag. Hovering it highlights it.
/// Puzzle scripts (Toolbox, VentCover) listen to onUsed and turn Interactable on and off.
/// </summary>
[DisallowMultipleComponent]
public class InspectPart : MonoBehaviour
{
    [Tooltip("Can be clicked right now. Puzzle scripts turn this on and off.")]
    [SerializeField] private bool interactable = true;
    [Tooltip("Stops being clickable after it's used once.")]
    [SerializeField] private bool singleUse = true;

    [Header("Needs an item")]
    [Tooltip("PlayerInventory id needed to use this, e.g. \"screwdriver\". Empty = no item needed.")]
    [SerializeField] private string requiredItemId = "";
    [SerializeField, TextArea(1, 3)] private string missingItemMessage = "";

    [Header("Feedback")]
    [Tooltip("Shown in the inspect view when this is used. Optional.")]
    [SerializeField, TextArea(1, 3)] private string useMessage = "";

    public UnityEvent onUsed;

    public bool Used { get; private set; }

    public bool Interactable
    {
        get => interactable && !(singleUse && Used);
        set => interactable = value;
    }

    /// <summary>
    /// Called by ItemInspector on click. Returns false if it can't be used yet.
    /// message is what to show the player (may be empty).
    /// </summary>
    public bool TryUse(out string message)
    {
        message = "";
        if (!Interactable)
            return false;

        if (!string.IsNullOrEmpty(requiredItemId) && !PlayerInventory.Has(requiredItemId))
        {
            message = missingItemMessage;
            return false;
        }

        Used = true;
        message = useMessage;
        onUsed?.Invoke();
        return true;
    }
}
