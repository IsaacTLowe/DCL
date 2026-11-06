using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time setup for the Demo scene: Vigil > Set Up Polaroids and Exit Door.
///  1. Polaroids show photos instead of numbers. Under the UV lamp, four of them reveal the exit code
///     in rainbow order: 7 red, 8 orange, 6 yellow, 1 green.
///  2. Adds a keypad (a copy of the lockbox keypad) on the wall beside Door B, code 7861.
///  3. Makes Door B an ExitDoor: unlocked by that keypad, click to open, and the player wins.
///  4. Turns off the old "inspect the book to win" path.
/// Everything is one Undo step. Save the scene afterwards. Safe to delete this file once the scene is saved.
/// </summary>
public static class VigilDoorPuzzleSetup
{
    private const string ExitCode = "7861";
    private const string PhotoFolder = "Assets/Art/Polaroids";
    private const string DoorName = "Door B";
    private const string KeypadName = "DoorKeypad";

    private static readonly (string polaroid, string digit, Color color)[] Clues =
    {
        ("Polaroid1", "7", new Color(0.95f, 0.12f, 0.10f)),   // red
        ("Polaroid3", "8", new Color(1.00f, 0.50f, 0.05f)),   // orange
        ("Polaroid5", "6", new Color(1.00f, 0.90f, 0.10f)),   // yellow
        ("Polaroid6", "1", new Color(0.20f, 0.95f, 0.25f)),   // green
    };

    // Also on the Assets menu, so it shows up when you right-click in the Project window.
    [MenuItem("Vigil/Set Up Polaroids and Exit Door")]
    [MenuItem("Assets/Vigil - Set Up Polaroids and Exit Door", false, 0)]
    private static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Vigil setup", "Stop Play mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        bool go = EditorUtility.DisplayDialog("Vigil setup",
            $"This edits the open scene ({scene.name}).\n\n" +
            "Before you continue, look at Door B from INSIDE the room in the Scene view. " +
            "The keypad goes on the wall on the side you're looking from.",
            "Continue", "Cancel");
        if (!go)
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Vigil polaroid + exit door setup");

        var log = new StringBuilder();
        SetUpPolaroids(scene, log);

        ExitDoor exitDoor = SetUpExitDoor(scene, log, out Transform panel, out Transform frame);
        if (exitDoor != null)
            SetUpDoorKeypad(scene, exitDoor, panel, frame, log);

        TurnOffBookWin(log);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("Vigil setup:\n" + log);
        EditorUtility.DisplayDialog("Vigil setup", log + "\nSave the scene (Cmd+S) to keep these changes.", "OK");
    }

    // ------------------------------------------------------------------ polaroids

    private static void SetUpPolaroids(Scene scene, StringBuilder log)
    {
        List<Polaroid> polaroids = InScene<Polaroid>(scene);
        int photos = 0;

        foreach (Polaroid polaroid in polaroids)
        {
            var so = new SerializedObject(polaroid);
            so.FindProperty("visibleText").stringValue = "";

            var clue = Clues.FirstOrDefault(c => c.polaroid == polaroid.gameObject.name);
            bool isClue = clue.polaroid != null;
            so.FindProperty("hiddenText").stringValue = isClue ? clue.digit : "";
            if (isClue)
                so.FindProperty("hiddenColor").colorValue = clue.color;

            Match number = Regex.Match(polaroid.gameObject.name, @"\d+");
            if (number.Success)
            {
                string path = $"{PhotoFolder}/photo_{int.Parse(number.Value):00}.png";
                Texture photo = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (photo != null)
                {
                    so.FindProperty("photo").objectReferenceValue = photo;
                    photos++;
                }
            }

            // Update the labels too, so the scene file matches what the game shows.
            if (so.FindProperty("visibleNumber").objectReferenceValue is TMP_Text visible)
            {
                Undo.RecordObject(visible, "Clear polaroid number");
                visible.text = "";
            }
            if (so.FindProperty("hiddenNumber").objectReferenceValue is TMP_Text hidden)
            {
                Undo.RecordObject(hidden, "Set hidden number");
                hidden.text = isClue ? clue.digit : "";
                if (isClue)
                    hidden.color = clue.color;
            }

            so.ApplyModifiedProperties();
        }

        log.AppendLine($"Polaroids: {polaroids.Count} found, {photos} photos assigned, visible numbers cleared.");
        foreach (var clue in Clues)
        {
            bool found = polaroids.Any(p => p.gameObject.name == clue.polaroid);
            log.AppendLine($"  {clue.polaroid} hides {clue.digit}{(found ? "" : "  (NOT FOUND)")}");
        }
    }

    // ------------------------------------------------------------------ exit door

    private static ExitDoor SetUpExitDoor(Scene scene, StringBuilder log, out Transform panel, out Transform frame)
    {
        panel = null;
        frame = null;

        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == DoorName);
        if (root == null)
        {
            log.AppendLine($"Door: no root object named \"{DoorName}\" in the scene. Skipped the door and keypad.");
            return null;
        }

        // The model has two meshes: the detailed door panel (with handles) and a simple frame.
        MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.sharedMesh != null)
            .OrderByDescending(m => m.sharedMesh.vertexCount)
            .ToArray();
        if (meshes.Length == 0)
        {
            log.AppendLine("Door: Door B has no meshes. Skipped.");
            return null;
        }
        panel = meshes[0].transform;
        frame = meshes.Length > 1 ? meshes[1].transform : null;
        if (panel == root.transform)
            log.AppendLine("Door: WARNING the panel is the root object, so the frame will swing with it.");

        // The root had a placeholder 1x1x1 box. Give the panel its own collider (it moves with the door).
        BoxCollider rootBox = root.GetComponent<BoxCollider>();
        if (rootBox != null && rootBox.enabled)
        {
            Undo.RecordObject(rootBox, "Disable placeholder collider");
            rootBox.enabled = false;
        }
        if (panel.GetComponent<Collider>() == null)
            Undo.AddComponent<BoxCollider>(panel.gameObject);   // auto-fits the panel mesh

        ExitDoor door = root.GetComponent<ExitDoor>();
        if (door == null)
            door = Undo.AddComponent<ExitDoor>(root);

        bool hingePositive = HingeOnPositiveSide(panel, out string how);
        var so = new SerializedObject(door);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("hingeOnPositiveSide").boolValue = hingePositive;
        so.ApplyModifiedProperties();

        log.AppendLine($"Door: ExitDoor added to {DoorName}, panel = \"{panel.name}\", " +
                       $"frame = \"{(frame != null ? frame.name : "none")}\". Hinge side {how}.");
        return door;
    }

    /// <summary>The handles stick out of both faces of the panel; the hinge is on the other end.</summary>
    private static bool HingeOnPositiveSide(Transform panel, out string how)
    {
        Bounds bounds = WorldBounds(panel);
        Vector3 width = WidthAxis(bounds);
        Vector3 thickness = width == Vector3.right ? Vector3.forward : Vector3.right;

        Mesh mesh = panel.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh.isReadable || Application.isEditor ? mesh.vertices : null;
        if (vertices != null && vertices.Length > 0)
        {
            float centerT = Vector3.Dot(bounds.center, thickness);
            float halfT = Vector3.Dot(bounds.extents, thickness);
            float sum = 0f;
            int count = 0;
            foreach (Vector3 v in vertices)
            {
                Vector3 world = panel.TransformPoint(v);
                if (Mathf.Abs(Vector3.Dot(world, thickness) - centerT) > halfT * 0.5f)
                {
                    sum += Vector3.Dot(world, width);
                    count++;
                }
            }

            if (count > 0)
            {
                bool handlePositive = sum / count > Vector3.Dot(bounds.center, width);
                how = $"found from the handle position (hinge on the {(handlePositive ? "negative" : "positive")} side)";
                return !handlePositive;
            }
        }

        how = "guessed (couldn't read the mesh). Select Door B and check the yellow hinge line";
        return false;
    }

    // ------------------------------------------------------------------ door keypad

    private static void SetUpDoorKeypad(Scene scene, ExitDoor door, Transform panel, Transform frame, StringBuilder log)
    {
        if (scene.GetRootGameObjects().Any(g => g.name == KeypadName))
        {
            log.AppendLine($"Keypad: \"{KeypadName}\" already exists, left it alone. Delete it and run again to rebuild.");
            return;
        }

        LockboxKeypadController lockbox = InScene<LockboxKeypadController>(scene).FirstOrDefault();
        GameObject sourceCanvas = lockbox != null
            ? new SerializedObject(lockbox).FindProperty("keypadCanvas").objectReferenceValue as GameObject
            : null;
        if (sourceCanvas == null)
        {
            log.AppendLine("Keypad: couldn't find the lockbox keypad and its canvas to copy. Skipped.");
            return;
        }

        // The lockbox's 1 and 2 buttons call the lockbox controller directly. Switch them to
        // LockboxKeypadButton like the others, so each button belongs to its own keypad.
        WireButtons(sourceCanvas, lockbox);

        // ---- where the keypad goes
        Bounds panelBounds = WorldBounds(panel);
        Bounds frameBounds = frame != null ? WorldBounds(frame) : panelBounds;
        Vector3 width = WidthAxis(panelBounds);
        Vector3 thickness = width == Vector3.right ? Vector3.forward : Vector3.right;

        Vector3 viewer = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.camera.transform.position
            : Object.FindAnyObjectByType<PlayerController>()?.transform.position ?? panelBounds.center + thickness;
        float roomSide = Vector3.Dot(viewer - panelBounds.center, thickness) >= 0f ? 1f : -1f;
        Vector3 intoRoom = thickness * roomSide;

        bool hingePositive = new SerializedObject(door).FindProperty("hingeOnPositiveSide").boolValue;
        Vector3 towardHandle = width * (hingePositive ? -1f : 1f);

        var sourceRect = (RectTransform)sourceCanvas.transform;
        Vector3[] corners = new Vector3[4];
        sourceRect.GetWorldCorners(corners);
        float canvasWidth = (corners[3] - corners[0]).magnitude;
        float canvasHeight = (corners[1] - corners[0]).magnitude;

        Vector3 position = frameBounds.center
                           + towardHandle * (Vector3.Dot(frameBounds.extents, width) + 0.15f + canvasWidth * 0.5f)
                           + intoRoom * (Vector3.Dot(frameBounds.extents, thickness) + 0.01f);
        position.y = panelBounds.min.y + Mathf.Min(1.35f, panelBounds.size.y * 0.45f);

        // Snap onto the wall surface if there is a collider behind that spot.
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(position + intoRoom * 0.6f, -intoRoom, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit wall = hits.Where(h => !h.collider.transform.IsChildOf(door.transform))
                              .OrderBy(h => h.distance).FirstOrDefault();
        bool onWall = wall.collider != null;
        if (onWall)
            position = wall.point + intoRoom * 0.005f;

        // ---- keypad object
        var keypadObject = new GameObject(KeypadName);
        Undo.RegisterCreatedObjectUndo(keypadObject, "Create door keypad");
        keypadObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(intoRoom, Vector3.up));

        // A copy of the lockbox keypad canvas, facing the room the same way it faces out of the lockbox.
        GameObject canvas = Object.Instantiate(sourceCanvas);
        Undo.RegisterCreatedObjectUndo(canvas, "Copy keypad canvas");
        canvas.name = "DoorKeypadCanvas";
        canvas.transform.SetParent(keypadObject.transform, false);
        canvas.transform.rotation = keypadObject.transform.rotation
                                    * (Quaternion.Inverse(lockbox.transform.rotation) * sourceCanvas.transform.rotation);
        canvas.transform.localScale = sourceCanvas.transform.lossyScale;
        ((RectTransform)canvas.transform).GetWorldCorners(corners);
        Vector3 canvasCenter = (corners[0] + corners[2]) * 0.5f;
        canvas.transform.position += (position + intoRoom * 0.004f) - canvasCenter;

        TMP_Text title = canvas.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "TitleTMP");
        if (title != null)
            title.text = "Exit Door";

        // Click target. It sits just behind the canvas, so the camera zoom centers on the keypad.
        var box = Undo.AddComponent<BoxCollider>(keypadObject);
        box.size = new Vector3(canvasWidth + 0.04f, canvasHeight + 0.04f, 0.03f);
        box.center = new Vector3(0f, 0f, -0.015f);

        // ---- controller
        var controller = Undo.AddComponent<LockboxKeypadController>(keypadObject);
        var source = new SerializedObject(lockbox);
        var so = new SerializedObject(controller);
        so.FindProperty("keypadCanvas").objectReferenceValue = canvas;
        so.FindProperty("keypadCanvasGroup").objectReferenceValue = canvas.GetComponent<CanvasGroup>();
        so.FindProperty("codeDisplay").objectReferenceValue = Matching<TMP_Text>(source.FindProperty("codeDisplay"), sourceCanvas, canvas);
        so.FindProperty("statusDisplay").objectReferenceValue = Matching<TMP_Text>(source.FindProperty("statusDisplay"), sourceCanvas, canvas);
        so.FindProperty("accessCode").stringValue = ExitCode;
        so.ApplyModifiedProperties();

        Undo.RecordObject(controller, "Hook keypad to door");
        UnityEventTools.AddVoidPersistentListener(controller.onUnlocked, door.Unlock);

        WireButtons(canvas, controller);

        log.AppendLine($"Keypad: \"{KeypadName}\" placed {(onWall ? "on the wall" : "by the frame (no wall collider found)")} " +
                       $"beside the handle, code {ExitCode}, On Unlocked -> ExitDoor.Unlock.");
    }

    /// <summary>Points every button in a keypad canvas at that keypad's controller.</summary>
    private static void WireButtons(GameObject canvas, LockboxKeypadController controller)
    {
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            LockboxKeypadButton keypadButton = button.GetComponent<LockboxKeypadButton>();

            if (keypadButton == null)
            {
                // Buttons wired straight to a controller in their On Click list: read the action, then
                // replace that listener with a LockboxKeypadButton.
                string action = null;
                var buttonSo = new SerializedObject(button);
                SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (!(button.onClick.GetPersistentTarget(i) is LockboxKeypadController))
                        continue;

                    string method = button.onClick.GetPersistentMethodName(i);
                    string argument = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                    action = method switch
                    {
                        "PressDigit" => argument,
                        "ClearCode" => "CLEAR",
                        "SubmitCode" => "ENTER",
                        "CloseKeypad" => "CLOSE",
                        _ => action,
                    };

                    Undo.RecordObject(button, "Rewire keypad button");
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }

                if (string.IsNullOrEmpty(action))
                    continue;   // not a keypad button

                keypadButton = Undo.AddComponent<LockboxKeypadButton>(button.gameObject);
                var newSo = new SerializedObject(keypadButton);
                newSo.FindProperty("action").stringValue = action;
                newSo.ApplyModifiedProperties();
            }

            var keypadSo = new SerializedObject(keypadButton);
            keypadSo.FindProperty("keypad").objectReferenceValue = controller;
            keypadSo.ApplyModifiedProperties();
        }
    }

    // ------------------------------------------------------------------ win

    private static void TurnOffBookWin(StringBuilder log)
    {
        GameController game = Object.FindAnyObjectByType<GameController>();
        if (game == null)
        {
            log.AppendLine("Win: no GameController in the scene.");
            return;
        }

        var so = new SerializedObject(game);
        so.FindProperty("winItem").objectReferenceValue = null;
        so.ApplyModifiedProperties();
        log.AppendLine("Win: GameController's Win Item cleared, so only the exit door wins now.");
    }

    // ------------------------------------------------------------------ helpers

    private static List<T> InScene<T>(Scene scene) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(c => !EditorUtility.IsPersistent(c) && c.gameObject.scene == scene)
            .ToList();
    }

    /// <summary>Finds the object in the copy that sits at the same place as the original's reference.</summary>
    private static T Matching<T>(SerializedProperty original, GameObject sourceRoot, GameObject copyRoot) where T : Component
    {
        if (!(original.objectReferenceValue is T component))
            return null;
        if (component.gameObject == sourceRoot)
            return copyRoot.GetComponent<T>();

        string path = AnimationUtility.CalculateTransformPath(component.transform, sourceRoot.transform);
        Transform match = copyRoot.transform.Find(path);
        return match != null ? match.GetComponent<T>() : null;
    }

    private static Bounds WorldBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(target.position, Vector3.one * 0.1f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Vector3 WidthAxis(Bounds bounds)
    {
        return bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
    }
}
