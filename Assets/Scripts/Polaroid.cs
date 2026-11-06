using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class Polaroid : MonoBehaviour
{
    [SerializeField] private string visibleText = "";
    [SerializeField] private string hiddenText = "";
    [Tooltip("Color of the hidden number when the UV lamp reveals it.")]
    [SerializeField] private Color hiddenColor = new Color(0.45f, 1f, 0.9f, 1f);
    [SerializeField] private Texture photo;
    [SerializeField] private float revealDistance = 1.2f;
    [SerializeField] private float fullRevealDistance = 0.7f;
    [SerializeField] private float fadeTime = 0.3f;

    [SerializeField] private TMP_Text visibleNumber;
    [SerializeField] private TMP_Text hiddenNumber;
    [SerializeField] private Renderer photoRenderer;

    private float reveal;

    private void Awake()
    {
        ApplyText();
        ApplyPhoto();
        if (hiddenNumber != null)
            hiddenNumber.alpha = 0f;
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null)
                return;
            ApplyText();
            ApplyPhoto();
        };
#endif
    }

    private void Update()
    {
        if (hiddenNumber == null || string.IsNullOrEmpty(hiddenText))
            return;

        reveal = Mathf.MoveTowards(reveal, TargetReveal(), Time.deltaTime / fadeTime);
        hiddenNumber.alpha = reveal;
    }

    private float TargetReveal()
    {
        float best = 0f;
        foreach (RevealLight lamp in RevealLight.All)
        {
            if (!lamp.IsOn)
                continue;

            Vector3 toLamp = lamp.Position - transform.position;
            if (Vector3.Dot(toLamp, -transform.forward) <= 0f)
                continue;

            float amount = 1f - Mathf.InverseLerp(fullRevealDistance, revealDistance, toLamp.magnitude);
            best = Mathf.Max(best, amount);
        }
        return best;
    }

    private void ApplyText()
    {
        if (visibleNumber != null)
            visibleNumber.text = visibleText;
        if (hiddenNumber != null)
        {
            hiddenNumber.text = hiddenText;
            hiddenNumber.color = hiddenColor;   // sets alpha to 1 too; Awake/Update fade it back
        }
    }

    private void ApplyPhoto()
    {
        if (photoRenderer == null || !gameObject.scene.IsValid())
            return;

        if (photo == null)
        {
            photoRenderer.SetPropertyBlock(null);
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", photo);
        block.SetColor("_BaseColor", Color.white);
        photoRenderer.SetPropertyBlock(block);
    }
}