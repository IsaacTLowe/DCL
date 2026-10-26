using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Opens the lockbox keypad when one of this object's colliders (or a child collider)
/// is clicked, and handles the keypad's display and button events.
/// </summary>
public class LockboxKeypadController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameObject keypadCanvas;
    [SerializeField] private CanvasGroup keypadCanvasGroup;
    [SerializeField] private TMP_Text codeDisplay;
    [SerializeField] private TMP_Text statusDisplay;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 4f;
    [SerializeField] private string accessCode = "1357";
    [SerializeField, Min(1)] private int maximumCodeLength = 8;

    [Header("Camera focus")]
    [SerializeField, Min(0.1f)] private float cameraFocusDistance = 1.05f;
    [SerializeField, Min(0.05f)] private float cameraFocusDuration = 0.4f;
    [SerializeField, Range(20f, 90f)] private float cameraFocusFieldOfView = 38f;

    private Camera playerCamera;
    private PlayerController playerController;
    private bool playerControllerWasEnabled;
    private string enteredCode = string.Empty;
    private bool isOpen;
    private bool isClosing;
    private bool keypadInputEnabled;
    private bool hasStoredCameraPose;
    private Vector3 storedCameraPosition;
    private Quaternion storedCameraRotation;
    private float storedCameraFieldOfView;
    private Coroutine cameraTransition;

    private void Awake()
    {
        playerCamera = Camera.main;
        playerController = FindAnyObjectByType<PlayerController>();

        if (keypadCanvasGroup == null && keypadCanvas != null)
            keypadCanvasGroup = keypadCanvas.GetComponent<CanvasGroup>();

        SetKeypadInputEnabled(false);
    }

    private void Update()
    {
        if (isOpen)
        {
            HandleKeyboardShortcuts();
            return;
        }

        if (isClosing)
            return;

        if (ItemInspector.IsOpen)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        TryOpenFromClick();
    }

    /// <summary>Bound to the digit buttons in the keypad Canvas.</summary>
    public void PressDigit(string digit)
    {
        if (!isOpen || !keypadInputEnabled || string.IsNullOrEmpty(digit) || enteredCode.Length >= maximumCodeLength)
            return;

        enteredCode += digit[0];
        SetStatus("ENTER CODE");
        RefreshCodeDisplay();
    }

    /// <summary>Bound to the CLR button in the keypad Canvas.</summary>
    public void ClearCode()
    {
        if (!isOpen || !keypadInputEnabled)
            return;

        enteredCode = string.Empty;
        SetStatus("ENTER CODE");
        RefreshCodeDisplay();
    }

    /// <summary>Bound to the ENTER button in the keypad Canvas.</summary>
    public void SubmitCode()
    {
        if (!isOpen || !keypadInputEnabled || enteredCode.Length == 0)
            return;

        bool isCorrect = enteredCode == accessCode;
        SetStatus(isCorrect ? "ACCESS GRANTED" : "ACCESS DENIED");

        if (!isCorrect)
        {
            enteredCode = string.Empty;
            RefreshCodeDisplay();
        }
    }

    /// <summary>Bound to the close button in the keypad Canvas.</summary>
    public void CloseKeypad()
    {
        if (!isOpen)
            return;

        isOpen = false;
        SetKeypadInputEnabled(false);

        if (hasStoredCameraPose && playerCamera != null)
        {
            isClosing = true;
            if (cameraTransition != null)
                StopCoroutine(cameraTransition);
            cameraTransition = StartCoroutine(ReturnCameraThenResume());
            return;
        }

        ResumeGameplay();
    }

    private void TryOpenFromClick()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return;

        Vector2 pointerPosition = Cursor.lockState == CursorLockMode.Locked
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();

        Ray ray = playerCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            return;

        Transform hitTransform = hit.collider.transform;
        if (hitTransform != transform && !hitTransform.IsChildOf(transform))
            return;

        OpenKeypad();
    }

    private void OpenKeypad()
    {
        if (keypadCanvas == null)
        {
            Debug.LogWarning("Lockbox keypad Canvas has not been assigned.", this);
            return;
        }

        isOpen = true;
        SetKeypadInputEnabled(false);
        enteredCode = string.Empty;
        SetStatus("ENTER CODE");
        RefreshCodeDisplay();
        EnsureUiInput();

        playerController ??= FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerControllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        FocusCameraOnLockbox();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void FocusCameraOnLockbox()
    {
        if (playerCamera == null)
            return;

        if (cameraTransition != null)
            StopCoroutine(cameraTransition);

        Transform cameraTransform = playerCamera.transform;
        storedCameraPosition = cameraTransform.position;
        storedCameraRotation = cameraTransform.rotation;
        storedCameraFieldOfView = playerCamera.fieldOfView;
        hasStoredCameraPose = true;

        Bounds lockboxBounds = GetLockboxBounds();
        Vector3 focusPoint = lockboxBounds.center;
        Vector3 targetPosition = focusPoint + transform.forward * cameraFocusDistance;
        Quaternion targetRotation = Quaternion.LookRotation(focusPoint - targetPosition, transform.up);
        cameraTransition = StartCoroutine(TransitionCamera(
            targetPosition,
            targetRotation,
            cameraFocusFieldOfView,
            cameraFocusDuration,
            true));
    }

    private Bounds GetLockboxBounds()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        Bounds bounds = colliders[0].bounds;
        for (int index = 1; index < colliders.Length; index++)
            bounds.Encapsulate(colliders[index].bounds);

        return bounds;
    }

    private IEnumerator TransitionCamera(
        Vector3 targetPosition,
        Quaternion targetRotation,
        float targetFieldOfView,
        float duration,
        bool enableKeypadInputAfterTransition = false)
    {
        if (playerCamera == null)
            yield break;

        Transform cameraTransform = playerCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        float startFieldOfView = playerCamera.fieldOfView;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, progress),
                Quaternion.Slerp(startRotation, targetRotation, progress));
            playerCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, progress);
            yield return null;
        }

        cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
        playerCamera.fieldOfView = targetFieldOfView;
        cameraTransition = null;

        if (enableKeypadInputAfterTransition && isOpen)
            SetKeypadInputEnabled(true);
    }

    private IEnumerator ReturnCameraThenResume()
    {
        yield return TransitionCamera(
            storedCameraPosition,
            storedCameraRotation,
            storedCameraFieldOfView,
            cameraFocusDuration);

        hasStoredCameraPose = false;
        isClosing = false;
        ResumeGameplay();
    }

    private void ResumeGameplay()
    {
        if (playerController != null && playerControllerWasEnabled)
            playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetKeypadInputEnabled(bool isEnabled)
    {
        keypadInputEnabled = isEnabled;

        if (keypadCanvasGroup == null)
            return;

        keypadCanvasGroup.interactable = isEnabled;
        keypadCanvasGroup.blocksRaycasts = isEnabled;
    }

    private static void EnsureUiInput()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
            return;

        InputSystemUIInputModule uiInput = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (uiInput != null && uiInput.actionsAsset == null)
            uiInput.AssignDefaultActions();
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseKeypad();
            return;
        }

        if (!keypadInputEnabled)
            return;

        for (int digit = 0; digit <= 9; digit++)
        {
            Key topRowKey = (Key)((int)Key.Digit0 + digit);
            Key numpadKey = (Key)((int)Key.Numpad0 + digit);
            if (Keyboard.current[topRowKey].wasPressedThisFrame || Keyboard.current[numpadKey].wasPressedThisFrame)
            {
                PressDigit(digit.ToString());
                return;
            }
        }

        if (Keyboard.current.backspaceKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)
            ClearCode();
        else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            SubmitCode();
    }

    private void RefreshCodeDisplay()
    {
        if (codeDisplay != null)
            codeDisplay.text = enteredCode.Length == 0 ? "----" : new string('\u2022', enteredCode.Length);
    }

    private void SetStatus(string message)
    {
        if (statusDisplay != null)
            statusDisplay.text = message;
    }
}
