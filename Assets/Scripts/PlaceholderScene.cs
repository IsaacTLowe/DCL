using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Stand-in for a level that isn't built yet (used by the Vent scene).
/// Shows a title and a message, unlocks the cursor, and Enter or Esc goes back to returnScene.
/// </summary>
public class PlaceholderScene : MonoBehaviour
{
    [SerializeField] private string title = "The Vent";
    [SerializeField, TextArea(2, 5)] private string message =
        "You squeeze through the opening and crawl into the dark...\n<size=70%>(Placeholder: the vent level goes here.)</size>";
    [SerializeField] private string returnScene = "MainMenu";

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BuildUi();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
            || keyboard.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene(returnScene);
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("PlaceholderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)canvasObject.transform;
        CreateText(root, title, 64f, FontStyles.Bold, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.75f), 1f);
        CreateText(root, message, 32f, FontStyles.Normal, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.55f), 0.85f);
        CreateText(root, "Press Enter to return to the main menu", 22f, FontStyles.Normal,
            new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.12f), 0.45f);
    }

    private static void CreateText(RectTransform parent, string text, float size, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, float alpha)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = new Color(1f, 1f, 1f, alpha);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }
}
