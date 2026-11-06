using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Room timer. Counts down from startTime as soon as the scene starts. Win() stops the clock and
/// shows the win message: ExitDoor calls it when the player opens the unlocked exit door.
/// (Optionally, closing the inspect view on a Win Item can also win; leave Win Item empty to turn that off.)
/// At zero, shows a message and restarts the room. Put it on an empty GameObject. It builds its own UI.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField, Min(1f)] private float startTime = 60f;
    [Tooltip("Below this many seconds the clock turns red.")]
    [SerializeField, Min(0f)] private float warningTime = 10f;
    [Header("Win condition")]
    [Tooltip("Optional. Inspecting this item and closing the view (Esc / X) also wins. Leave empty when the exit door is the win.")]
    [SerializeField] private InspectableItem winItem;
    [Tooltip("Optional. Leave empty and the first LockboxDoor in the scene is used.")]
    [SerializeField] private LockboxDoor lockboxDoor;
    [Tooltip("Only count the win if the lockbox door is fully open, so the book can't be reached some other way.")]
    [SerializeField] private bool requireDoorOpen = true;

    [Header("Win")]
    [Tooltip("{0} is replaced with the time left, e.g. 0:23.")]
    [SerializeField] private string winMessage = "You escaped!\n<size=50%>{0} left</size>";
    [Tooltip("How long the win message stays up. 0 = stays until the scene changes.")]
    [SerializeField, Min(0f)] private float winMessageDuration = 4f;
    [SerializeField] private Color winColor = new Color(1f, 0.8f, 0.3f, 1f);

    [Header("Out of time")]
    [SerializeField] private string timeUpMessage = "Out of time";
    [SerializeField, Min(0f)] private float restartDelay = 3f;
    [Tooltip("Off: only fire onTimeUp, e.g. to play a scare instead of restarting.")]
    [SerializeField] private bool restartOnTimeUp = true;

    [Header("Look")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color warningColor = new Color(0.9f, 0.15f, 0.1f, 1f);

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onTimeUp;
    public UnityEvent onStopped;

    public float TimeRemaining { get; private set; }
    public bool IsRunning { get; private set; }

    private TMP_Text clockText;
    private TMP_Text messageText;

    private void Start()
    {
        BuildUi();

        if (lockboxDoor == null)
            lockboxDoor = FindAnyObjectByType<LockboxDoor>();

        if (winItem != null)
            winItem.onInspectClosed.AddListener(OnWinItemClosed);

        TimeRemaining = startTime;
        IsRunning = true;
        RefreshClock();
    }

    private void OnDestroy()
    {
        if (winItem != null)
            winItem.onInspectClosed.RemoveListener(OnWinItemClosed);
    }

    /// <summary>Runs when the player closes the inspect view on the win item.</summary>
    private void OnWinItemClosed()
    {
        if (requireDoorOpen && lockboxDoor != null && !lockboxDoor.IsFullyOpen)
            return;

        Win();
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        // Scaled time on purpose: the clock keeps ticking while inspecting items, using the
        // keypad, or solving the letter, because none of those pause the game.
        TimeRemaining = Mathf.Max(TimeRemaining - Time.deltaTime, 0f);
        RefreshClock();

        if (TimeRemaining <= 0f)
            TimeUp();
    }

    /// <summary>Stops the clock and shows the win message. Called by ExitDoor when it opens.</summary>
    public void Win()
    {
        if (!IsRunning)
            return;   // already won, or time already ran out

        Stop();

        int seconds = Mathf.CeilToInt(TimeRemaining);
        messageText.text = string.Format(winMessage, $"{seconds / 60}:{seconds % 60:00}");
        messageText.color = winColor;
        messageText.gameObject.SetActive(true);
        onWin?.Invoke();

        if (winMessageDuration > 0f)
            StartCoroutine(HideMessageAfter(winMessageDuration));
    }

    private IEnumerator HideMessageAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        messageText.gameObject.SetActive(false);
    }

    /// <summary>Stops the clock where it is, with no message. Call from any UnityEvent.</summary>
    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        clockText.color = normalColor;
        onStopped?.Invoke();
    }

    private void TimeUp()
    {
        IsRunning = false;
        messageText.text = timeUpMessage;
        messageText.color = warningColor;
        messageText.gameObject.SetActive(true);
        onTimeUp?.Invoke();

        if (restartOnTimeUp)
            StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSecondsRealtime(restartDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void RefreshClock()
    {
        // Round up, so the clock shows 0:01 until the very last moment, not 0:00 for a second.
        int seconds = Mathf.CeilToInt(TimeRemaining);
        clockText.text = $"{seconds / 60}:{seconds % 60:00}";
        clockText.color = TimeRemaining <= warningTime ? warningColor : normalColor;
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("GameControllerCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // under the letter hint (400) and ItemInspector (500)

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)canvasObject.transform;

        // Clock: top-right corner.
        clockText = CreateText("Clock", root, 56f, FontStyles.Bold, TextAlignmentOptions.TopRight);
        RectTransform clockRect = clockText.rectTransform;
        clockRect.anchorMin = clockRect.anchorMax = clockRect.pivot = new Vector2(1f, 1f);
        clockRect.anchoredPosition = new Vector2(-40f, -30f);
        clockRect.sizeDelta = new Vector2(300f, 80f);

        // Time-up message: center of the screen, hidden until needed.
        messageText = CreateText("Message", root, 90f, FontStyles.Bold, TextAlignmentOptions.Center);
        messageText.color = warningColor;
        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.offsetMin = messageRect.offsetMax = Vector2.zero;
        messageText.gameObject.SetActive(false);
    }

    private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;   // never blocks clicks
        return text;
    }
}
