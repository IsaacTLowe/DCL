using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The UV lamp's light. Only shines while its HoldableItem is held and shown (not while the
/// inspector, letter puzzle or a keypad has hidden it) AND the player has it switched on.
/// F toggles the switch while it's held and shown.
/// </summary>
public class RevealLight : MonoBehaviour
{
    public static readonly List<RevealLight> All = new List<RevealLight>();

    [SerializeField] private Transform origin;
    [SerializeField] private Light beam;
    [SerializeField] private GameObject glowWhenOn;

    [Header("Switch")]
    [Tooltip("Whether the lamp is switched on when first picked up.")]
    [SerializeField] private bool startSwitchedOn = true;
    [SerializeField] private AudioClip switchOnSound;
    [Tooltip("Leave empty to use the switch-on sound for both.")]
    [SerializeField] private AudioClip switchOffSound;
    [SerializeField, Range(0f, 1f)] private float switchVolume = 1f;

    public bool IsOn { get; private set; }
    public bool IsSwitchedOn { get; private set; }
    public Vector3 Position => (origin != null ? origin : transform).position;

    private HoldableItem holdable;
    private AudioSource audioSource;

    private void Awake()
    {
        holdable = GetComponentInParent<HoldableItem>();
        IsSwitchedOn = startSwitchedOn;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D: it's in the player's hand

        SetOn(false);
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Update()
    {
        bool shown = holdable != null && holdable.IsShown;

        // IsShown is already false while the inspector, letter puzzle or a keypad is open,
        // so F can't toggle the lamp behind those UIs.
        if (shown && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            Toggle();

        bool on = shown && IsSwitchedOn;
        if (on != IsOn)
            SetOn(on);
    }

    /// <summary>Flips the switch and plays the click. Also callable from a UnityEvent.</summary>
    public void Toggle()
    {
        IsSwitchedOn = !IsSwitchedOn;

        AudioClip clip = IsSwitchedOn || switchOffSound == null ? switchOnSound : switchOffSound;
        if (clip != null)
            audioSource.PlayOneShot(clip, switchVolume);
    }

    private void SetOn(bool on)
    {
        IsOn = on;
        if (beam != null)
            beam.enabled = on;
        if (glowWhenOn != null)
            glowWhenOn.SetActive(on);
    }
}
