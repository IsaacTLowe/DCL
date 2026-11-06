using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Runtime-created inventory bar. Tab toggles the bar while the player is exploring.
/// The slots are intentionally empty placeholders for a future item system.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static bool IsOpen => instance != null && instance.isOpen;

    private const int SlotCount = 6;

    private static InventoryUI instance;

    private GameObject inventoryCanvas;
    private PlayerController playerController;
    private bool playerControllerWasEnabled;
    private bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAtRuntime()
    {
        if (instance != null)
            return;

        GameObject inventoryObject = new GameObject("InventoryUI");
        instance = inventoryObject.AddComponent<InventoryUI>();
        DontDestroyOnLoad(inventoryObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Update()
    {
        // If the scene changed while the bar was open, its player no longer owns the cursor.
        if (isOpen && playerController == null)
        {
            isOpen = false;
            playerControllerWasEnabled = false;
            if (inventoryCanvas != null)
                inventoryCanvas.SetActive(false);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame)
            return;

        if (isOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        // Existing inspect and puzzle views already own the mouse and cursor.
        if (ItemInspector.IsOpen || LetterPuzzle.IsFocused)
            return;

        // Unity's destroyed objects compare equal to null, but null-coalescing would
        // retain their managed reference after a room restart.
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
        if (playerController == null || !playerController.isActiveAndEnabled)
            return;

        BuildIfNeeded();

        playerControllerWasEnabled = playerController.enabled;
        playerController.enabled = false;

        inventoryCanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isOpen = true;
    }

    private void Close()
    {
        isOpen = false;
        inventoryCanvas.SetActive(false);

        if (playerController != null && playerControllerWasEnabled)
            playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void BuildIfNeeded()
    {
        if (inventoryCanvas != null)
            return;

        inventoryCanvas = new GameObject("InventoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        inventoryCanvas.transform.SetParent(transform, false);

        Canvas canvas = inventoryCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;

        CanvasScaler scaler = inventoryCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bar = new GameObject("InventoryBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(inventoryCanvas.transform, false);

        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = new Vector2(512f, 94f);

        Image barImage = bar.GetComponent<Image>();
        barImage.color = new Color(0f, 0f, 0f, 0.78f);
        barImage.raycastTarget = false;

        const float slotSize = 72f;
        const float slotSpacing = 8f;
        float firstSlotX = -((SlotCount - 1) * (slotSize + slotSpacing)) * 0.5f;

        for (int index = 0; index < SlotCount; index++)
        {
            GameObject slot = new GameObject($"Slot {index + 1}", typeof(RectTransform), typeof(Image), typeof(Outline));
            slot.transform.SetParent(bar.transform, false);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2(firstSlotX + index * (slotSize + slotSpacing), 0f);
            slotRect.sizeDelta = new Vector2(slotSize, slotSize);

            Image slotImage = slot.GetComponent<Image>();
            slotImage.color = new Color(1f, 1f, 1f, 0.08f);
            slotImage.raycastTarget = false;

            Outline outline = slot.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        inventoryCanvas.SetActive(false);
    }
}
