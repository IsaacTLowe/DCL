using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fade to black, load a scene, fade back in. Creates itself; call ScreenFader.FadeAndLoad("Vent").
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader current;

    private Image image;

    public static void FadeAndLoad(string sceneName, float duration = 0.8f)
    {
        if (current != null)
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"ScreenFader: scene \"{sceneName}\" isn't in the build. Add it in File > Build Profiles.");
            return;
        }

        GameObject go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        current = go.AddComponent<ScreenFader>();
        current.Build();
        current.StartCoroutine(current.Run(sceneName, Mathf.Max(0.01f, duration)));
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    private IEnumerator Run(string sceneName, float duration)
    {
        yield return Fade(0f, 1f, duration);
        SceneManager.LoadScene(sceneName);
        yield return null;   // let the new scene start under the black screen
        yield return Fade(1f, 0f, duration);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        image.color = new Color(0f, 0f, 0f, alpha);
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // above the inspect view (500)

        GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image = imageObject.GetComponent<Image>();
        image.raycastTarget = true;   // swallow clicks while fading
        SetAlpha(0f);
    }
}
