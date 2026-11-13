using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Red toolbox puzzle. Goes on the toolbox root, next to its InspectableItem.
/// In the inspect view: click each latch (an InspectPart) to flip it open. When all latches are
/// open the lid swings up and the tool inside becomes clickable. Clicking the tool puts it in
/// PlayerInventory and removes it from the box.
/// Built by Vigil > Set Up Toolbox and Vent.
/// </summary>
public class Toolbox : MonoBehaviour
{
    [Header("Latches")]
    [Tooltip("InspectParts on the latch hinges. Each rotates around its local X axis when clicked.")]
    [SerializeField] private InspectPart[] latches;
    [SerializeField] private float latchOpenAngle = -160f;
    [SerializeField, Min(0f)] private float latchOpenDuration = 0.35f;

    [Header("Lid")]
    [Tooltip("Pivot on the lid's back edge. Rotates around its local X axis.")]
    [SerializeField] private Transform lidHinge;
    [SerializeField] private float lidOpenAngle = 105f;
    [SerializeField, Min(0f)] private float lidOpenDuration = 0.8f;
    [SerializeField, TextArea(1, 3)] private string openedMessage = "There's a screwdriver inside.";

    [Header("Tool inside")]
    [Tooltip("InspectPart on the tool. Not clickable until the lid is open.")]
    [SerializeField] private InspectPart tool;
    [SerializeField] private string toolItemId = "screwdriver";
    [SerializeField] private string toolDisplayName = "Screwdriver";

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onToolTaken;

    public bool IsOpen { get; private set; }

    private int latchesOpen;

    private void Awake()
    {
        foreach (InspectPart latch in latches)
        {
            if (latch == null)
                continue;
            InspectPart thisLatch = latch;
            thisLatch.onUsed.AddListener(() => OpenLatch(thisLatch.transform));
        }

        if (tool != null)
        {
            tool.Interactable = false;
            tool.onUsed.AddListener(TakeTool);
        }
    }

    private void OpenLatch(Transform hinge)
    {
        Quaternion closed = hinge.localRotation;
        ItemInspector.AnimateMirrored(hinge, latchOpenDuration,
            (t, k) => t.localRotation = closed * Quaternion.Euler(latchOpenAngle * k, 0f, 0f),
            () =>
            {
                latchesOpen++;
                if (latchesOpen >= CountLatches())
                    OpenLid();
            });
    }

    private void OpenLid()
    {
        if (IsOpen)
            return;
        IsOpen = true;

        if (lidHinge == null)
        {
            Opened();
            return;
        }

        Quaternion closed = lidHinge.localRotation;
        ItemInspector.AnimateMirrored(lidHinge, lidOpenDuration,
            (t, k) => t.localRotation = closed * Quaternion.Euler(lidOpenAngle * k, 0f, 0f),
            Opened);
    }

    private void Opened()
    {
        if (tool != null && tool.gameObject.activeSelf)
        {
            tool.Interactable = true;
            ItemInspector.ShowMessage(openedMessage);
        }
        onOpened?.Invoke();
    }

    private void TakeTool()
    {
        PlayerInventory.Add(toolItemId, toolDisplayName);
        ItemInspector.SetActiveMirrored(tool.transform, false);
        onToolTaken?.Invoke();
    }

    private int CountLatches()
    {
        int count = 0;
        foreach (InspectPart latch in latches)
            if (latch != null)
                count++;
        return count;
    }
}
