using UnityEngine;

/// <summary>
/// One piece of the torn letter. Put it on each piece quad (it needs a collider), as a child of
/// LetterPuzzle. Where the piece sits in the editor when the game starts is its solved spot.
/// It has no input of its own: LetterPuzzle moves, turns and places it.
/// </summary>
public class TornPiece : MonoBehaviour
{
    [Tooltip("How close (letter units) the piece must be dropped to its spot to snap in.")]
    [SerializeField, Min(0f)] private float snapDistance = 0.05f;
    [Tooltip("How close (degrees) its rotation must be to snap in. Pieces turn in 90° steps.")]
    [SerializeField, Range(0f, 45f)] private float snapAngle = 5f;

    public bool IsPlaced { get; private set; }

    private Vector3 solvedPos;
    private Quaternion solvedRot;

    private void Awake()
    {
        solvedPos = transform.localPosition;   // the editor layout is the answer
        solvedRot = transform.localRotation;
    }

    /// <summary>Throws the piece somewhere near its spot, always turned the wrong way.</summary>
    public void Scatter(float range, Vector2 playArea, float height)
    {
        Vector3 p = solvedPos + new Vector3(Random.Range(-range, range), 0f, Random.Range(-range, range));
        p.x = Mathf.Clamp(p.x, -playArea.x, playArea.x);
        p.z = Mathf.Clamp(p.z, -playArea.y, playArea.y);
        p.y = height;
        transform.localPosition = p;

        int turns = Random.Range(1, 4);   // 1, 2 or 3 -> never starts the right way up
        transform.localRotation = Quaternion.AngleAxis(90f * turns, Vector3.up) * solvedRot;
    }

    /// <summary>Spins the piece 90° flat on the desk, around its own center.</summary>
    public void RotateQuarterTurn(bool clockwise = true)
    {
        if (IsPlaced)
            return;

        // Around the PARENT's up axis. The quad is tipped 90° on X to lie flat, so its own
        // up axis lies along the desk, and spinning around that would stand it on its edge.
        // Seen from above, a positive turn around up is clockwise in Unity.
        float angle = clockwise ? 90f : -90f;
        transform.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * transform.localRotation;
    }

    /// <summary>Snaps into the solved spot if it's close enough and turned the right way.</summary>
    public bool TryPlace()
    {
        if (IsPlaced)
            return false;

        // Compare on the desk only; the pile height doesn't count.
        Vector3 flat = transform.localPosition;
        flat.y = solvedPos.y;
        bool closeEnough = Vector3.Distance(flat, solvedPos) < snapDistance;
        bool rotatedRight = Quaternion.Angle(transform.localRotation, solvedRot) < snapAngle;
        if (!closeEnough || !rotatedRight)
            return false;

        transform.localPosition = solvedPos;
        transform.localRotation = solvedRot;
        IsPlaced = true;
        return true;
    }
}
