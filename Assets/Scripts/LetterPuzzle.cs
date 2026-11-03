using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Torn-letter puzzle. Aim the crosshair at the letter and left-click to lean over the desk:
/// the camera moves above the letter and the cursor unlocks. Left-drag a piece to move it,
/// right-click or E to turn the piece under the mouse 90° clockwise, Q for counter-clockwise
/// (works while dragging too), Esc to stand back up. When every piece snaps into
/// place, the glow turns on and onSolved fires.
/// Put this on the parent of the TornPiece quads, with the pieces laid out SOLVED in the editor.
/// All distances below are in "letter units": the letter is 1 unit wide before scaling.
/// </summary>
public class LetterPuzzle : MonoBehaviour
{
    /// <summary>True while any letter puzzle has the camera. Other click scripts can check this.</summary>
    public static bool IsFocused => focused != null;
    private static LetterPuzzle focused;

    [Header("Scene references")]
    [SerializeField] private GameObject glow;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

    [Header("Pieces")]
    [Tooltip("How far each piece is thrown from its solved spot at the start.")]
    [SerializeField, Min(0f)] private float scatterRange = 0.45f;
    [Tooltip("Half-size of the area pieces can be scattered/dragged in (x = width, y = depth).")]
    [SerializeField] private Vector2 playArea = new Vector2(0.85f, 1.0f);
    [Tooltip("Height gap between loose pieces, so they never flicker when they overlap.")]
    [SerializeField, Min(0.0001f)] private float stackStep = 0.002f;
    [Tooltip("How far the held piece floats above the others.")]
    [SerializeField, Min(0f)] private float dragLift = 0.02f;

    [Header("Camera focus")]
    [SerializeField, Range(20f, 90f)] private float focusFieldOfView = 40f;
    [SerializeField, Min(0.05f)] private float focusDuration = 0.4f;
    [Tooltip("Extra space around the play area when framing the view.")]
    [SerializeField, Min(0f)] private float framingPadding = 0.1f;

    [Header("Controls hint")]
    [SerializeField] private string controlsHint =
        "Drag to move   |   Right-click or E to rotate   |   Q to rotate back   |   Esc to stand up";
    [SerializeField] private string solvedHint = "Esc to stand up";
    [SerializeField] private Color hintBarColor = new Color(0f, 0f, 0f, 0.75f);

    [Header("Events")]
    [Tooltip("Runs once when the last piece snaps in.")]
    public UnityEvent onSolved;

    public bool IsSolved { get; private set; }

    private readonly List<TornPiece> pieces = new List<TornPiece>();
    private int placedCount;

    private Camera playerCamera;
    private PlayerController playerController;
    private bool playerControllerWasEnabled;
    private bool isOpen;
    private bool isClosing;
    private bool inputEnabled;
    private Vector3 storedCameraPosition;
    private Quaternion storedCameraRotation;
    private float storedFieldOfView;
    private Coroutine cameraTransition;

    private TornPiece held;
    private Vector2 grabOffset;   // piece center minus grab point, in letter units (x, z)

    private GameObject hintCanvas;
    private TMP_Text hintText;

    private void Awake()
    {
        GetComponentsInChildren(pieces);
        if (glow != null)
            glow.SetActive(false);
    }

    private void Start()
    {
        // Every TornPiece has recorded its solved pose in Awake, so it's safe to move them now.
        for (int i = 0; i < pieces.Count; i++)
            pieces[i].Scatter(scatterRange, playArea, (i + 1) * stackStep);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (isOpen)
        {
            HandleFocusedInput(mouse);
            return;
        }

        if (isClosing || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (ItemInspector.IsOpen)
            return;

        // Same rule as InspectableItem: if the player controller is off, another UI owns the mouse.
        playerController ??= FindAnyObjectByType<PlayerController>();
        if (playerController != null && !playerController.enabled)
            return;

        if (AimingAtLetter())
            Open();
    }

    // ------------------------------------------------------------------ focused input

    private void HandleFocusedInput(Mouse mouse)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (!inputEnabled)
            return;

        // Turn input: right-click or E = clockwise, Q = counter-clockwise. 0 = no turn this frame.
        int turn = 0;
        if (mouse.rightButton.wasPressedThisFrame || (keyboard != null && keyboard.eKey.wasPressedThisFrame))
            turn = 1;
        else if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            turn = -1;

        if (held != null)
        {
            if (turn != 0)
                held.RotateQuarterTurn(turn > 0);

            if (mouse.leftButton.isPressed)
                DragHeld(mouse);
            else
                Drop();
            return;
        }

        bool left = mouse.leftButton.wasPressedThisFrame;
        if (!left && turn == 0)
            return;

        TornPiece piece = PieceUnderPointer(mouse);
        if (piece == null)
            return;

        if (turn != 0)
        {
            piece.RotateQuarterTurn(turn > 0);
            Restack(piece);
            TryPlace(piece);
        }
        else
        {
            PickUp(piece, mouse);
        }
    }

    private void PickUp(TornPiece piece, Mouse mouse)
    {
        if (!PointerOnLetterPlane(mouse, out Vector2 grabPoint))
            return;

        held = piece;
        Vector3 local = piece.transform.localPosition;
        grabOffset = new Vector2(local.x, local.z) - grabPoint;
        DragHeld(mouse);
    }

    private void DragHeld(Mouse mouse)
    {
        if (!PointerOnLetterPlane(mouse, out Vector2 point))
            return;

        Vector2 target = point + grabOffset;
        target.x = Mathf.Clamp(target.x, -playArea.x, playArea.x);
        target.y = Mathf.Clamp(target.y, -playArea.y, playArea.y);
        held.transform.localPosition = new Vector3(target.x, (pieces.Count + 1) * stackStep + dragLift, target.y);
    }

    private void Drop()
    {
        TornPiece piece = held;
        held = null;
        Restack(piece);
        TryPlace(piece);
    }

    private void TryPlace(TornPiece piece)
    {
        if (!piece.TryPlace())
            return;

        placedCount++;
        if (placedCount >= pieces.Count && !IsSolved)
            Solve();
    }

    /// <summary>Puts <paramref name="top"/> on top of the loose pile and re-packs the heights.</summary>
    private void Restack(TornPiece top)
    {
        List<TornPiece> loose = pieces.FindAll(p => !p.IsPlaced && p != top);
        loose.Sort((a, b) => a.transform.localPosition.y.CompareTo(b.transform.localPosition.y));
        loose.Add(top);

        for (int i = 0; i < loose.Count; i++)
        {
            Vector3 p = loose[i].transform.localPosition;
            p.y = (i + 1) * stackStep;
            loose[i].transform.localPosition = p;
        }
    }

    private void Solve()
    {
        IsSolved = true;
        if (glow != null)
            glow.SetActive(true);
        if (hintText != null)
            hintText.text = solvedHint;
        onSolved?.Invoke();
        Debug.Log("Letter solved: 1357", this);
    }

    // ------------------------------------------------------------------ raycasts

    private bool AimingAtLetter()
    {
        playerCamera ??= Camera.main;
        if (playerCamera == null)
            return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // crosshair
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider.transform.IsChildOf(transform);
    }

    /// <summary>The closest loose piece under the mouse (the top of the pile, since the camera looks down).</summary>
    private TornPiece PieceUnderPointer(Mouse mouse)
    {
        Ray ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Ignore);

        TornPiece best = null;
        float bestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            TornPiece piece = hit.collider.GetComponent<TornPiece>();
            if (piece == null || piece.IsPlaced || !piece.transform.IsChildOf(transform))
                continue;

            if (hit.distance < bestDistance)
            {
                best = piece;
                bestDistance = hit.distance;
            }
        }
        return best;
    }

    /// <summary>Where the mouse ray meets the letter's plane, in letter units (x, z).</summary>
    private bool PointerOnLetterPlane(Mouse mouse, out Vector2 point)
    {
        point = Vector2.zero;
        Ray ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());
        Plane plane = new Plane(transform.up, transform.position);
        if (!plane.Raycast(ray, out float distance))
            return false;

        Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
        point = new Vector2(local.x, local.z);
        return true;
    }

    // ------------------------------------------------------------------ open / close

    private void Open()
    {
        isOpen = true;
        inputEnabled = false;
        focused = this;

        playerController ??= FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerControllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        Transform cam = playerCamera.transform;
        storedCameraPosition = cam.position;
        storedCameraRotation = cam.rotation;
        storedFieldOfView = playerCamera.fieldOfView;

        GetFocusPose(out Vector3 position, out Quaternion rotation);
        StartCameraTransition(position, rotation, focusFieldOfView, () =>
        {
            inputEnabled = isOpen;
            if (isOpen)
                ShowHint(true);   // appears once the camera has settled
        });

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Stand back up. Also fine to hook to a UI button.</summary>
    public void Close()
    {
        if (!isOpen)
            return;

        if (held != null)
            Drop();

        isOpen = false;
        inputEnabled = false;
        isClosing = true;
        ShowHint(false);
        StartCameraTransition(storedCameraPosition, storedCameraRotation, storedFieldOfView, ResumeGameplay);
    }

    private void ResumeGameplay()
    {
        isClosing = false;
        if (focused == this)
            focused = null;

        if (playerController != null && playerControllerWasEnabled)
            playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Straight above the letter, looking down, high enough to see the whole play area.</summary>
    private void GetFocusPose(out Vector3 position, out Quaternion rotation)
    {
        const float pieceMargin = 0.3f; // pieces poke out past their centers
        Vector3 scale = transform.lossyScale;
        float halfWidth = (playArea.x + pieceMargin) * scale.x;
        float halfDepth = (playArea.y + pieceMargin) * scale.z;

        float tanHalf = Mathf.Tan(focusFieldOfView * 0.5f * Mathf.Deg2Rad);
        float height = Mathf.Max(halfDepth / tanHalf, halfWidth / (tanHalf * playerCamera.aspect));
        height *= 1f + framingPadding;

        position = transform.position + transform.up * height;
        rotation = Quaternion.LookRotation(-transform.up, transform.forward); // top of letter = top of screen
    }

    private void StartCameraTransition(Vector3 position, Quaternion rotation, float fieldOfView, System.Action onDone)
    {
        if (cameraTransition != null)
            StopCoroutine(cameraTransition);
        cameraTransition = StartCoroutine(TransitionCamera(position, rotation, fieldOfView, onDone));
    }

    private IEnumerator TransitionCamera(Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView,
        System.Action onDone)
    {
        Transform cam = playerCamera.transform;
        Vector3 startPosition = cam.position;
        Quaternion startRotation = cam.rotation;
        float startFieldOfView = playerCamera.fieldOfView;

        for (float elapsed = 0f; elapsed < focusDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / focusDuration);
            cam.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Slerp(startRotation, targetRotation, t));
            playerCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, t);
            yield return null;
        }

        cam.SetPositionAndRotation(targetPosition, targetRotation);
        playerCamera.fieldOfView = targetFieldOfView;
        cameraTransition = null;
        onDone?.Invoke();
    }

    private void OnDisable()
    {
        // Never leave the player frozen if this gets turned off while focused.
        if (isOpen || isClosing)
        {
            if (playerCamera != null)
            {
                playerCamera.transform.SetPositionAndRotation(storedCameraPosition, storedCameraRotation);
                playerCamera.fieldOfView = storedFieldOfView;
            }
            isOpen = false;
            held = null;
            ShowHint(false);
            ResumeGameplay();
        }
    }

    // ------------------------------------------------------------------ controls hint

    private void ShowHint(bool show)
    {
        if (show && hintCanvas == null)
            BuildHint();
        if (hintCanvas == null)
            return;

        hintText.text = IsSolved ? solvedHint : controlsHint;
        hintCanvas.SetActive(show);
    }

    /// <summary>A thin bar along the bottom of the screen, built in code like ItemInspector's UI.</summary>
    private void BuildHint()
    {
        hintCanvas = new GameObject("LetterPuzzleHint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hintCanvas.transform.SetParent(transform, false);

        Canvas canvas = hintCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // under ItemInspector (500)

        CanvasScaler scaler = hintCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(hintCanvas.transform, false);
        RectTransform barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(0f, 64f);
        Image barImage = bar.GetComponent<Image>();
        barImage.color = hintBarColor;
        barImage.raycastTarget = false;   // never blocks clicks on the pieces

        GameObject textObject = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(barRect, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 26f;
        text.color = new Color(1f, 1f, 1f, 0.85f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        hintText = text;

        hintCanvas.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // Shows the play area on the desk so you can check it fits the table.
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(playArea.x * 2f, 0f, playArea.y * 2f));
    }
}
