using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [SerializeField] private float size = 15f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.9f);

    private GameObject canvasObject;

    private void Start()
    {
        canvasObject = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(Outline));
        dot.transform.SetParent(canvasObject.transform, false);
        ((RectTransform)dot.transform).sizeDelta = new Vector2(size, size);

        Image image = dot.GetComponent<Image>();
        image.sprite = CreateCircle();
        image.color = color;
        image.raycastTarget = false;
        dot.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
    }

    private void Update()
    {
        canvasObject.SetActive(Cursor.lockState == CursorLockMode.Locked);
    }

    private static Sprite CreateCircle()
    {
        const int resolution = 64;
        float radius = resolution / 2f;

        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance)));
            }
        }

        texture.Apply(true);
        return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
}