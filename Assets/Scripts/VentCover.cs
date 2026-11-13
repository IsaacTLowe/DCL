using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Screwed-on vent cover the player can crawl through. Goes on the vent root, next to its InspectableItem.
/// In the inspect view: click each screw (an InspectPart that needs the screwdriver) to back it out.
/// When every screw is out the cover pops off, and clicking the dark opening behind it loads sceneToLoad.
/// Built by Vigil > Set Up Toolbox and Vent. The root's local -Z points out of the wall.
/// </summary>
public class VentCover : MonoBehaviour
{
    [Header("Screws")]
    [Tooltip("InspectParts on the screws. Each screw's local +Z points into the wall.")]
    [SerializeField] private InspectPart[] screws;
    [SerializeField, Min(0f)] private float unscrewTurns = 3f;
    [SerializeField, Min(0f)] private float unscrewDistance = 0.025f;
    [SerializeField, Min(0f)] private float unscrewDuration = 1f;

    [Header("Cover")]
    [Tooltip("The grille. Pops off and falls once all screws are out.")]
    [SerializeField] private Transform cover;
    [SerializeField, Min(0f)] private float coverFallDuration = 0.6f;
    [SerializeField, TextArea(1, 3)] private string coverOffMessage = "The cover's off. Click the opening to crawl in.";

    [Header("Opening")]
    [Tooltip("InspectPart on the dark hole behind the cover. Clickable once the cover is off.")]
    [SerializeField] private InspectPart opening;
    [Tooltip("Scene to load when the player crawls in. Must be in File > Build Profiles.")]
    [SerializeField] private string sceneToLoad = "Vent";
    [SerializeField, Min(0f)] private float fadeDuration = 0.8f;

    [Header("Events")]
    public UnityEvent onCoverRemoved;
    public UnityEvent onEntered;

    public bool IsCoverOff { get; private set; }

    private int screwsOut;
    private bool entering;

    private void Awake()
    {
        foreach (InspectPart screw in screws)
        {
            if (screw == null)
                continue;
            InspectPart thisScrew = screw;
            thisScrew.onUsed.AddListener(() => Unscrew(thisScrew.transform));
        }

        if (opening != null)
        {
            opening.Interactable = false;
            opening.onUsed.AddListener(Enter);
        }
    }

    private void Unscrew(Transform screw)
    {
        Vector3 startPosition = screw.localPosition;
        Quaternion startRotation = screw.localRotation;
        Vector3 outward = startRotation * Vector3.back;   // out of the wall, in the parent's space

        ItemInspector.AnimateMirrored(screw, unscrewDuration,
            (t, k) =>
            {
                t.localPosition = startPosition + outward * (unscrewDistance * k);
                // Counter-clockwise as seen from the front = loosening.
                t.localRotation = startRotation * Quaternion.AngleAxis(360f * unscrewTurns * k, Vector3.forward);
            },
            () =>
            {
                ItemInspector.SetActiveMirrored(screw, false);
                screwsOut++;
                if (screwsOut >= CountScrews())
                    RemoveCover();
            });
    }

    private void RemoveCover()
    {
        if (IsCoverOff)
            return;
        IsCoverOff = true;

        if (cover == null)
        {
            CoverRemoved();
            return;
        }

        Vector3 startPosition = cover.localPosition;
        Quaternion startRotation = cover.localRotation;
        ItemInspector.AnimateMirrored(cover, coverFallDuration,
            (t, k) =>
            {
                // Pull away from the wall, drop, and tip forward.
                t.localPosition = startPosition + new Vector3(0f, -0.35f * k * k, -0.25f * k);
                t.localRotation = Quaternion.Euler(-35f * k, 0f, 0f) * startRotation;
            },
            () =>
            {
                ItemInspector.SetActiveMirrored(cover, false);
                CoverRemoved();
            });
    }

    private void CoverRemoved()
    {
        if (opening != null)
            opening.Interactable = true;
        ItemInspector.ShowMessage(coverOffMessage);
        onCoverRemoved?.Invoke();
    }

    /// <summary>Fades out and loads the next scene. Also callable from a UnityEvent.</summary>
    public void Enter()
    {
        if (entering)
            return;
        entering = true;
        onEntered?.Invoke();
        ScreenFader.FadeAndLoad(sceneToLoad, fadeDuration);
    }

    private int CountScrews()
    {
        int count = 0;
        foreach (InspectPart screw in screws)
            if (screw != null)
                count++;
        return count;
    }
}
