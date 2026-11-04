using System.Collections.Generic;
using UnityEngine;

public class RevealLight : MonoBehaviour
{
    public static readonly List<RevealLight> All = new List<RevealLight>();

    [SerializeField] private Transform origin;
    [SerializeField] private Light beam;
    [SerializeField] private GameObject glowWhenOn;

    public bool IsOn { get; private set; }
    public Vector3 Position => (origin != null ? origin : transform).position;

    private HoldableItem holdable;

    private void Awake()
    {
        holdable = GetComponentInParent<HoldableItem>();
        SetOn(false);
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Update()
    {
        bool on = holdable != null && holdable.IsShown;
        if (on != IsOn)
            SetOn(on);
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