using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Full-screen "look at the item" view. A copy of the item is rendered by its own camera
/// into a RenderTexture shown on a letterboxed, dimmed UI, so the item is always the main
/// thing on screen. Drag to spin it in 3D, scroll to zoom, Esc or the X button to leave.
/// You don't need to put this in the scene: InspectableItem creates one when needed.
/// Only add it to a GameObject yourself if you want to tweak the settings below.
/// </summary>
public class ItemInspector : MonoBehaviour
{
    public static ItemInspector Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.state != State.Closed;

    [Header("Look")]
    [SerializeField] private Color barColor = new Color(0f, 0f, 0f, 0.95f);
    [SerializeField] private Color backdropTint = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField, Range(0f, 0.4f)] private float barHeight = 0.2f;      // fraction of screen height
    [SerializeField, Range(0.3f, 1f)] private float screenFill = 0.8f;     // how much of the screen the item fills
    [SerializeField, Range(15f, 60f)] private float fieldOfView = 30f;     // low FOV = less fisheye distortion
    [SerializeField, Min(0f)] private float keyLightIntensity = 1.5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;

    [Header("Controls")]
    [SerializeField] private float dragDegreesPerPixel = 0.3f;
    [SerializeField] private float keyDegreesPerSecond = 120f;
    [SerializeField, Min(0f)] private float spinDamping = 4f;              // how fast a flicked item slows down
    [SerializeField, Min(0.1f)] private float minZoom = 0.5f;              // < 1 = closer
    [SerializeField, Min(0.1f)] private float maxZoom = 2f;                // > 1 = farther

    [Header("Stage")]
    [Tooltip("Where the inspected copy lives. Far from the level so the player camera never sees it.")]
    [SerializeField] private Vector3 stagePosition = new Vector3(0f, -5000f, 0f);

    private enum State { Closed, Opening, Open, Closing }
    private State state = State.Closed;
    private float fade;

    // Stage
    private Transform pivot;
    private Camera inspectCamera;
    private Light keyLight;
    private RenderTexture renderTexture;

    // UI
    private GameObject canvasObject;
    private CanvasGroup canvasGroup;
    private RawImage view;
    private TMP_Text titleText;
    private TMP_Text descriptionText;

    // Current session
    private InspectableItem currentItem;
    private GameObject currentCopy;
    private Quaternion startRotation;
    private float radius = 0.5f;
    private float fitDistance = 2f;
    private float zoom = 1f;
    private float targetZoom = 1f;
    private Vector2 spinVelocity;      // degrees/sec (x = yaw, y = pitch)
    private bool dragging;
    private int openedFrame;
    private PlayerController playerController;
    private bool playerControllerWasEnabled;

    public static ItemInspector GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        Instance = FindAnyObjectByType<ItemInspector>();
        if (Instance == null)
            Instance = new GameObject("ItemInspector").AddComponent<ItemInspector>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    // ------------------------------------------------------------------ open / close

    public void Open(InspectableItem item)
    {
        if (item == null || state != State.Closed)
            return;

        BuildIfNeeded();
        EnsureRenderTextureSize();

        Transform viewer = Camera.main != null ? Camera.main.transform : null;

        currentItem = item;
        currentCopy = CreateDisplayCopy(item);
        FrameCopy(item.GetStartRotation(viewer));

        titleText.text = item.DisplayName;
        descriptionText.text = item.Description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(item.Description));

        playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerControllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();

        zoom = targetZoom = 1f;
        spinVelocity = Vector2.zero;
        dragging = false;
        openedFrame = Time.frameCount;
        fade = 0f;

        canvasObject.SetActive(true);
        inspectCamera.enabled = true;
        keyLight.enabled = true;
        state = State.Opening;

        item.NotifyOpened();
    }

    /// <summary>Hooked to the X button; Esc also calls it.</summary>
    public void Close()
    {
        if (state == State.Closed || state == State.Closing)
            return;

        state = State.Closing;
        dragging = false;
    }

    private void FinishClose()
    {
        state = State.Closed;
        canvasObject.SetActive(false);
        inspectCamera.enabled = false;
        keyLight.enabled = false;

        if (currentCopy != null)
            Destroy(currentCopy);
        currentCopy = null;

        if (playerController != null && playerControllerWasEnabled)
            playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        InspectableItem item = currentItem;
        currentItem = null;
        if (item != null)
            item.NotifyClosed();
    }

    // ------------------------------------------------------------------ per frame

    private void Update()
    {
        if (state == State.Closed)
            return;

        float dt = Time.unscaledDeltaTime;

        UpdateFade(dt);
        if (state == State.Closed)
            return;

        EnsureRenderTextureSize();

        if (state != State.Closing)
            HandleInput(dt);

        ApplySpinAndZoom(dt);
    }

    private void UpdateFade(float dt)
    {
        float target = state == State.Closing ? 0f : 1f;
        fade = Mathf.MoveTowards(fade, target, dt / fadeDuration);
        canvasGroup.alpha = fade;
        pivot.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, fade));

        if (state == State.Opening && fade >= 1f)
            state = State.Open;
        else if (state == State.Closing && fade <= 0f)
            FinishClose();
    }

    private void HandleInput(float dt)
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
                ResetView();

            // Keyboard rotation: WASD / arrows to tumble, Q / E to roll.
            Vector2 keyRotate = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keyRotate.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keyRotate.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keyRotate.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keyRotate.y += 1f;

            float roll = 0f;
            if (keyboard.qKey.isPressed) roll += 1f;
            if (keyboard.eKey.isPressed) roll -= 1f;

            if (keyRotate != Vector2.zero || roll != 0f)
                Rotate(keyRotate * keyDegreesPerSecond * dt, roll * keyDegreesPerSecond * dt);
        }

        if (mouse == null || Time.frameCount == openedFrame) // ignore the click that opened us
            return;

        if (mouse.leftButton.wasPressedThisFrame && !PointerOverButton())
        {
            dragging = true;
            spinVelocity = Vector2.zero;
        }
        if (!mouse.leftButton.isPressed)
            dragging = false;

        if (dragging)
        {
            Vector2 degrees = mouse.delta.ReadValue() * dragDegreesPerPixel;
            Rotate(degrees);

            // Remember how fast we were dragging so a flick keeps it spinning after release.
            if (dt > 0f)
                spinVelocity = Vector2.Lerp(spinVelocity, degrees / dt, 0.5f);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            targetZoom = Mathf.Clamp(targetZoom * (scroll > 0f ? 0.9f : 1.1f), minZoom, maxZoom);
    }

    private void ApplySpinAndZoom(float dt)
    {
        if (!dragging && spinVelocity.sqrMagnitude > 0.01f)
        {
            Rotate(spinVelocity * dt);
            spinVelocity *= Mathf.Exp(-spinDamping * dt);
        }

        zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-12f * dt));

        Transform cam = inspectCamera.transform;
        float distance = Mathf.Max(fitDistance * zoom, radius * 1.1f);
        cam.position = pivot.position - cam.forward * distance;

        // Inverse-square falloff, so scale intensity by distance² to keep brightness constant.
        float lightDistance = Vector3.Distance(keyLight.transform.position, pivot.position);
        keyLight.intensity = keyLightIntensity * lightDistance * lightDistance;
        keyLight.range = lightDistance * 4f;
    }

    /// <summary>
    /// Rotates around the CAMERA's axes (not the object's), so dragging right always
    /// spins it right no matter how it's already turned. No gimbal lock.
    /// </summary>
    private void Rotate(Vector2 degrees, float rollDegrees = 0f)
    {
        Transform cam = inspectCamera.transform;
        pivot.rotation = Quaternion.AngleAxis(-degrees.x, cam.up)
                       * Quaternion.AngleAxis(degrees.y, cam.right)
                       * Quaternion.AngleAxis(rollDegrees, cam.forward)
                       * pivot.rotation;
    }

    private void ResetView()
    {
        pivot.rotation = startRotation;
        targetZoom = 1f;
        spinVelocity = Vector2.zero;
    }

    private static bool PointerOverButton()
    {
        // Only the X button is a raycast target, so this is true only over buttons.
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // ------------------------------------------------------------------ the copy

    private GameObject CreateDisplayCopy(InspectableItem item)
    {
        GameObject source = item.InspectModel != null ? item.InspectModel : item.gameObject;

        // Instantiate under an inactive parent so none of the copy's scripts wake up
        // (otherwise a second InspectableItem / puzzle script would start running).
        GameObject holder = new GameObject("InspectCopyHolder");
        holder.SetActive(false);

        GameObject copy = Instantiate(source, holder.transform);
        StripGameplayComponents(copy);

        copy.transform.SetParent(pivot, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = source.transform.lossyScale;
        copy.SetActive(true);

        Destroy(holder);
        return copy;
    }

    private static void StripGameplayComponents(GameObject root)
    {
        // Keep TextMeshPro so notes / labels on items still show their text.
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour.GetType().Namespace != "TMPro")
                DestroyImmediate(behaviour);

        foreach (Joint joint in root.GetComponentsInChildren<Joint>(true))
            DestroyImmediate(joint);
        foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(col);
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(body);
        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
            DestroyImmediate(audio);
    }

    /// <summary>Centers the copy on the pivot and works out how far back the camera sits.</summary>
    private void FrameCopy(Quaternion rotation)
    {
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        Renderer[] renderers = currentCopy.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(pivot.position, Vector3.one * 0.5f);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // Shift the copy so its visual center is on the pivot, so it spins around its middle.
        currentCopy.transform.position += pivot.position - bounds.center;

        // Radius of a sphere around the whole item, so it fits on screen at ANY rotation.
        radius = Mathf.Max(bounds.extents.magnitude, 0.01f);

        startRotation = rotation;
        pivot.rotation = startRotation;
        UpdateFitDistance();
    }

    private void UpdateFitDistance()
    {
        inspectCamera.fieldOfView = fieldOfView;

        float halfVertical = fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * inspectCamera.aspect);
        float halfAngle = Mathf.Min(halfVertical, halfHorizontal) * screenFill;

        // Distance at which a sphere of this radius covers halfAngle of the view.
        fitDistance = radius / Mathf.Sin(halfAngle);

        inspectCamera.nearClipPlane = Mathf.Max(0.01f, radius * 0.01f);
        inspectCamera.farClipPlane = fitDistance * maxZoom + radius * 2f;
        keyLight.transform.localPosition = new Vector3(0.5f, 0.6f, 0f) * fitDistance;
    }

    // ------------------------------------------------------------------ setup

    private void BuildIfNeeded()
    {
        if (inspectCamera != null)
            return;

        Transform stage = new GameObject("InspectStage").transform;
        stage.SetParent(transform, false);
        stage.position = stagePosition;
        stage.rotation = Quaternion.identity;

        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(stage, false);

        GameObject cameraObject = new GameObject("InspectCamera");
        cameraObject.transform.SetParent(stage, false);
        inspectCamera = cameraObject.AddComponent<Camera>();
        inspectCamera.clearFlags = CameraClearFlags.SolidColor;
        inspectCamera.backgroundColor = Color.clear;   // transparent, so the scene shows behind the item
        inspectCamera.allowHDR = false;
        inspectCamera.enabled = false;

        UniversalAdditionalCameraData urpData = inspectCamera.GetUniversalAdditionalCameraData();
        urpData.renderPostProcessing = false;           // post-processing can wipe the alpha channel
        urpData.renderShadows = false;

        keyLight = new GameObject("InspectKeyLight").AddComponent<Light>();
        keyLight.transform.SetParent(cameraObject.transform, false);
        keyLight.type = LightType.Point;
        keyLight.shadows = LightShadows.None;
        keyLight.enabled = false;

        BuildUi();
    }

    private void EnsureRenderTextureSize()
    {
        int width = Mathf.Max(Screen.width, 16);
        int height = Mathf.Max(Screen.height, 16);
        if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
            return;

        if (renderTexture != null)
        {
            inspectCamera.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "ItemInspectRT",
            antiAliasing = 4,
        };

        inspectCamera.targetTexture = renderTexture;
        inspectCamera.ResetAspect();
        view.texture = renderTexture;

        if (currentCopy != null)
            UpdateFitDistance();
    }

    private void BuildUi()
    {
        canvasObject = new GameObject("ItemInspectCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        RectTransform root = (RectTransform)canvasObject.transform;

        // Draw order = child order: backdrop, bars, text, item, then the X on top.
        CreateImage("Backdrop", root, backdropTint, Vector2.zero, Vector2.one);
        CreateImage("TopBar", root, barColor, new Vector2(0f, 1f - barHeight), Vector2.one);
        CreateImage("BottomBar", root, barColor, Vector2.zero, new Vector2(1f, barHeight));

        titleText = CreateText("Title", root, 46f, FontStyles.Bold, Color.white,
            new Vector2(0.1f, 1f - barHeight), new Vector2(0.9f, 1f));
        descriptionText = CreateText("Description", root, 28f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.85f),
            new Vector2(0.15f, barHeight * 0.4f), new Vector2(0.85f, barHeight));
        TMP_Text hint = CreateText("ControlsHint", root, 20f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.45f),
            Vector2.zero, new Vector2(1f, barHeight * 0.4f));
        hint.text = "Drag to rotate   |   Scroll to zoom   |   Q / E to roll   |   R to reset   |   Esc to close";

        GameObject viewObject = new GameObject("ItemView", typeof(RectTransform), typeof(RawImage));
        viewObject.transform.SetParent(root, false);
        Stretch((RectTransform)viewObject.transform, Vector2.zero, Vector2.one);
        view = viewObject.GetComponent<RawImage>();
        view.raycastTarget = false;

        BuildCloseButton(root);
        canvasObject.SetActive(false);
    }

    private void BuildCloseButton(RectTransform root)
    {
        GameObject buttonObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -28f);
        rect.sizeDelta = new Vector2(96f, 96f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.06f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.2f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.35f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(Close);

        // Draw the X from two rotated bars, so it doesn't depend on the font.
        for (int i = 0; i < 2; i++)
        {
            GameObject line = new GameObject("XLine", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(rect, false);
            RectTransform lineRect = (RectTransform)line.transform;
            lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.sizeDelta = new Vector2(6f, 54f);
            lineRect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            Image lineImage = line.GetComponent<Image>();
            lineImage.color = Color.white;
            lineImage.raycastTarget = false;
        }
    }

    private static Image CreateImage(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform, anchorMin, anchorMax);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;   // only the X button should block dragging
        return image;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style,
        Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform, anchorMin, anchorMax);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<EventSystem>();

        InputSystemUIInputModule uiInput = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (uiInput != null && uiInput.actionsAsset == null)
            uiInput.AssignDefaultActions();
    }
}