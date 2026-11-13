using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time setup for the Demo scene: Vigil > Set Up Toolbox and Vent.
///  1. A red toolbox on the floor (left front corner). Inspect it, click both latches, the lid opens,
///     click the screwdriver to take it.
///  2. Turns the existing "Vent" (high on the back wall, by the bookshelves) into a puzzle: it goes
///     under a new "CrawlVent" parent with two screws and a dark opening. Inspect it, click each screw
///     (needs the screwdriver), the cover falls off, click the opening to crawl in.
///  3. A placeholder Assets/Scenes/Vent.unity (dark tunnel + text), added to the build.
/// The scene changes are one Undo step. Save the scene afterwards. Safe to delete this file once saved.
/// Running it again offers to replace: the vent is put back where it was first, then rebuilt.
/// The toolbox spot is a constant below, or just move the Toolbox object in the Scene view.
/// </summary>
public static class VigilToolboxVentSetup
{
    private const string DemoScenePath = "Assets/Scenes/Demo.unity";
    private const string MaterialFolderParent = "Assets/Materials";
    private const string MaterialFolder = "Assets/Materials/ToolboxVent";
    private const string ExistingVentName = "Vent";
    private const string VentScenePath = "Assets/Scenes/Vent.unity";
    private const string VentSceneName = "Vent";
    private const string ToolboxName = "Toolbox";
    private const string VentName = "CrawlVent";
    private const string ScrewdriverId = "screwdriver";

    // ---- placement (world space) ----
    private const float FallbackFloorY = 0.495f;
    private static readonly Vector2 ToolboxXZ = new Vector2(-5.2f, -2.2f);   // left front corner
    private const float ToolboxYaw = -50f;                                    // latches face into the room
    // The vent is ~5 m above eye level, so it needs a long reach to be clickable from the floor.
    // Lower this once the player has a way up (e.g. the ladder).
    private const float VentInteractionDistance = 7f;

    // ---- toolbox size (meters) ----
    private const float BoxWidth = 0.5f, BoxDepth = 0.24f, BoxHeight = 0.2f, Wall = 0.012f, LidHeight = 0.06f;

    private struct Materials
    {
        public Material red, interior, metal, black, screwdriverHandle, hole, tunnel;
    }

    [MenuItem("Vigil/Set Up Toolbox and Vent")]
    [MenuItem("Assets/Vigil - Set Up Toolbox and Vent", false, 1)]
    private static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Vigil setup", "Stop Play mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DemoScenePath &&
            !EditorUtility.DisplayDialog("Vigil setup",
                $"The open scene is \"{scene.path}\", not {DemoScenePath}. Set up the toolbox and vent here anyway?",
                "Continue", "Cancel"))
            return;

        GameObject oldToolbox = GameObject.Find(ToolboxName);
        GameObject oldVent = GameObject.Find(VentName);
        if ((oldToolbox != null || oldVent != null) &&
            !EditorUtility.DisplayDialog("Vigil setup",
                "This scene already has a Toolbox and/or CrawlVent. Replace them?", "Replace", "Cancel"))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Vigil toolbox + vent setup");

        if (oldToolbox != null) Undo.DestroyObjectImmediate(oldToolbox);
        if (oldVent != null) TearDownVent(oldVent);   // puts the original Vent back first
        Physics.SyncTransforms();

        GameObject existingVent = FindExistingVent();
        if (existingVent == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.DisplayDialog("Vigil setup",
                $"Couldn't find the \"{ExistingVentName}\" object (the vent on the back wall by the bookshelves).", "OK");
            return;
        }

        var log = new StringBuilder();
        Materials materials = CreateMaterials();

        GameObject toolbox = BuildToolbox(materials, log);
        Undo.RegisterCreatedObjectUndo(toolbox, "Create toolbox");
        GameObject vent = BuildVent(materials, existingVent, log);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        CreateVentScene(materials, log);
        AddVentSceneToBuild(log);

        Selection.objects = new Object[] { toolbox, vent };
        Debug.Log("Vigil toolbox + vent setup:\n" + log);
        EditorUtility.DisplayDialog("Vigil setup", log + "\nSave the scene (Cmd+S) to keep these changes.", "OK");
    }

    // ------------------------------------------------------------------ toolbox

    private static GameObject BuildToolbox(Materials m, StringBuilder log)
    {
        float floorY = FindFloorY(ToolboxXZ, out bool foundFloor);

        GameObject root = new GameObject(ToolboxName);
        root.transform.SetPositionAndRotation(new Vector3(ToolboxXZ.x, floorY, ToolboxXZ.y),
            Quaternion.Euler(0f, ToolboxYaw, 0f));
        Transform r = root.transform;

        // Open-topped body (local -Z is the front, where the latches are).
        Transform body = Empty("Body", r, Vector3.zero);
        Box("Bottom", body, new Vector3(0f, Wall / 2f, 0f), new Vector3(BoxWidth, Wall, BoxDepth), m.red);
        Box("Front", body, new Vector3(0f, BoxHeight / 2f, -BoxDepth / 2f + Wall / 2f), new Vector3(BoxWidth, BoxHeight, Wall), m.red);
        Box("Back", body, new Vector3(0f, BoxHeight / 2f, BoxDepth / 2f - Wall / 2f), new Vector3(BoxWidth, BoxHeight, Wall), m.red);
        Box("Left", body, new Vector3(-BoxWidth / 2f + Wall / 2f, BoxHeight / 2f, 0f), new Vector3(Wall, BoxHeight, BoxDepth - 2f * Wall), m.red);
        Box("Right", body, new Vector3(BoxWidth / 2f - Wall / 2f, BoxHeight / 2f, 0f), new Vector3(Wall, BoxHeight, BoxDepth - 2f * Wall), m.red);
        Box("Liner", body, new Vector3(0f, Wall + 0.001f, 0f), new Vector3(BoxWidth - 2f * Wall, 0.002f, BoxDepth - 2f * Wall), m.interior, collider: false);

        // Lid, hinged along the back top edge. Rotating the hinge +X swings the front up and back.
        Transform lidHinge = Empty("LidHinge", r, new Vector3(0f, BoxHeight, BoxDepth / 2f));
        Transform lid = Empty("Lid", lidHinge, Vector3.zero);
        Box("Top", lid, new Vector3(0f, LidHeight - Wall / 2f, -BoxDepth / 2f), new Vector3(BoxWidth, Wall, BoxDepth), m.red);
        Box("FrontSkirt", lid, new Vector3(0f, LidHeight / 2f, -BoxDepth + Wall / 2f), new Vector3(BoxWidth, LidHeight, Wall), m.red);
        Box("BackSkirt", lid, new Vector3(0f, LidHeight / 2f, -Wall / 2f), new Vector3(BoxWidth, LidHeight, Wall), m.red);
        Box("LeftSkirt", lid, new Vector3(-BoxWidth / 2f + Wall / 2f, LidHeight / 2f, -BoxDepth / 2f), new Vector3(Wall, LidHeight, BoxDepth - 2f * Wall), m.red);
        Box("RightSkirt", lid, new Vector3(BoxWidth / 2f - Wall / 2f, LidHeight / 2f, -BoxDepth / 2f), new Vector3(Wall, LidHeight, BoxDepth - 2f * Wall), m.red);
        Box("HandlePostL", lid, new Vector3(-0.09f, LidHeight + 0.02f, -BoxDepth / 2f), new Vector3(0.02f, 0.04f, 0.025f), m.black);
        Box("HandlePostR", lid, new Vector3(0.09f, LidHeight + 0.02f, -BoxDepth / 2f), new Vector3(0.02f, 0.04f, 0.025f), m.black);
        Box("HandleBar", lid, new Vector3(0f, LidHeight + 0.045f, -BoxDepth / 2f), new Vector3(0.22f, 0.022f, 0.03f), m.black);

        // Latches: plates over the body/lid seam on the front, hinged at the bottom.
        InspectPart latchL = BuildLatch("LatchL", r, -0.15f, m);
        InspectPart latchR = BuildLatch("LatchR", r, 0.15f, m);

        // Screwdriver lying in the bottom of the box, along local X.
        Transform screwdriver = Empty("Screwdriver", r, new Vector3(0f, Wall + 0.016f, -0.02f));
        screwdriver.localRotation = Quaternion.Euler(0f, 15f, 0f);
        Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
        Cylinder("Handle", screwdriver, new Vector3(-0.06f, 0f, 0f), alongX, 0.032f, 0.1f, m.screwdriverHandle);
        Cylinder("Collar", screwdriver, new Vector3(-0.004f, 0f, 0f), alongX, 0.026f, 0.012f, m.black);
        Cylinder("Shaft", screwdriver, new Vector3(0.065f, 0f, 0f), alongX, 0.007f, 0.13f, m.metal);
        Box("Tip", screwdriver, new Vector3(0.135f, 0f, 0f), new Vector3(0.012f, 0.002f, 0.008f), m.metal, collider: false);
        BoxCollider screwdriverCollider = screwdriver.gameObject.AddComponent<BoxCollider>();
        screwdriverCollider.center = new Vector3(0.015f, 0f, 0f);
        screwdriverCollider.size = new Vector3(0.27f, 0.04f, 0.045f);
        InspectPart screwdriverPart = screwdriver.gameObject.AddComponent<InspectPart>();
        SetPart(screwdriverPart, interactable: false, requiredItem: "", missingMessage: "",
            useMessage: "Picked up the screwdriver.");

        InspectableItem item = root.AddComponent<InspectableItem>();
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("displayName").stringValue = "Toolbox";
        itemSo.FindProperty("description").stringValue = "A heavy red toolbox. Two latches hold the lid shut.";
        itemSo.FindProperty("interactionDistance").floatValue = 3.5f;
        itemSo.FindProperty("hideWhileInspecting").boolValue = true;
        itemSo.FindProperty("collectOnInspect").boolValue = false;
        itemSo.FindProperty("useCustomStartRotation").boolValue = true;
        itemSo.FindProperty("customStartRotation").vector3Value = new Vector3(25f, 20f, 0f);
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        Toolbox toolbox = root.AddComponent<Toolbox>();
        var boxSo = new SerializedObject(toolbox);
        SetArray(boxSo.FindProperty("latches"), latchL, latchR);
        boxSo.FindProperty("lidHinge").objectReferenceValue = lidHinge;
        boxSo.FindProperty("tool").objectReferenceValue = screwdriverPart;
        boxSo.FindProperty("toolItemId").stringValue = ScrewdriverId;
        boxSo.FindProperty("toolDisplayName").stringValue = "Screwdriver";
        boxSo.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine($"• Toolbox on the floor at ({ToolboxXZ.x}, {floorY:0.###}, {ToolboxXZ.y})" +
                       (foundFloor ? "." : " (floor not found by raycast, used the default height — check it)."));
        return root;
    }

    private static InspectPart BuildLatch(string name, Transform parent, float x, Materials m)
    {
        // Hinge sits on the body front, 3.5 cm below the seam. Rotating it -X flips the plate down and out.
        Transform hinge = Empty(name, parent, new Vector3(x, BoxHeight - 0.035f, -BoxDepth / 2f - 0.001f));
        GameObject plate = Box("Plate", hinge, new Vector3(0f, 0.04f, -0.005f), new Vector3(0.05f, 0.08f, 0.008f), m.metal);
        BoxCollider plateCollider = plate.GetComponent<BoxCollider>();
        plateCollider.size = new Vector3(1.3f, 1.1f, 3f);   // a bit bigger than the plate, easier to click
        Box("Clasp", hinge, new Vector3(0f, 0.07f, -0.011f), new Vector3(0.03f, 0.012f, 0.006f), m.metal, collider: false);
        Cylinder("Barrel", hinge, new Vector3(0f, 0f, -0.004f), Quaternion.Euler(0f, 0f, 90f), 0.012f, 0.055f, m.metal);

        InspectPart part = hinge.gameObject.AddComponent<InspectPart>();
        SetPart(part, interactable: true, requiredItem: "", missingMessage: "", useMessage: "");
        return part;
    }

    // ------------------------------------------------------------------ vent

    private static GameObject BuildVent(Materials m, GameObject ventObject, StringBuilder log)
    {
        Transform vent = ventObject.transform;
        Vector3 center = WorldBounds(vent).center;

        // Which way is the wall? The model is flat along its local Y; try both sides of it.
        Vector3 intoWall = vent.up;
        if (!Physics.Raycast(center, vent.up, 0.5f, ~0, QueryTriggerInteraction.Ignore) &&
            Physics.Raycast(center, -vent.up, 0.5f, ~0, QueryTriggerInteraction.Ignore))
            intoWall = -vent.up;

        // New parent on the wall surface behind the vent. Local +Z points into the wall, -Z into the room.
        GameObject root = new GameObject(VentName);
        Transform r = root.transform;
        r.SetPositionAndRotation(center, Quaternion.LookRotation(intoWall, Vector3.up));
        Bounds b = LocalBounds(vent, r);
        r.position = r.TransformPoint(new Vector3(b.center.x, b.center.y, b.max.z));
        Undo.RegisterCreatedObjectUndo(root, "Create vent puzzle");

        // The existing vent becomes the cover. Its world position doesn't change.
        Undo.SetTransformParent(vent, r, "Move vent under CrawlVent");
        b = LocalBounds(vent, r);

        Bounds coverOwn = LocalBounds(vent, vent);
        BoxCollider coverCollider = Undo.AddComponent<BoxCollider>(ventObject);
        coverCollider.center = coverOwn.center;
        coverCollider.size = coverOwn.size;

        // Dark hole behind the cover.
        GameObject opening = GameObject.CreatePrimitive(PrimitiveType.Quad);
        opening.name = "Opening";
        opening.transform.SetParent(r, false);
        opening.transform.localPosition = new Vector3(0f, 0f, -0.003f);
        opening.transform.localScale = new Vector3(Mathf.Max(0.1f, b.size.x - 0.08f), Mathf.Max(0.1f, b.size.y - 0.06f), 1f);
        opening.GetComponent<Renderer>().sharedMaterial = m.hole;
        opening.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Object.DestroyImmediate(opening.GetComponent<Collider>());
        BoxCollider openingCollider = opening.AddComponent<BoxCollider>();
        openingCollider.size = new Vector3(1f, 1f, 0.02f);
        InspectPart openingPart = opening.AddComponent<InspectPart>();
        SetPart(openingPart, interactable: false, requiredItem: "", missingMessage: "", useMessage: "");

        // Two screws on the ends of the frame. Each screw's local +Z points into the wall.
        float screwX = b.size.x / 2f - 0.07f;
        InspectPart screwL = BuildScrew("ScrewL", r, new Vector3(-screwX, 0f, b.min.z), 20f, m);
        InspectPart screwR = BuildScrew("ScrewR", r, new Vector3(screwX, 0f, b.min.z), -35f, m);

        InspectableItem item = root.AddComponent<InspectableItem>();
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("displayName").stringValue = "Vent";
        itemSo.FindProperty("description").stringValue = "A vent high up by the ceiling. Two screws hold the cover on.";
        itemSo.FindProperty("interactionDistance").floatValue = VentInteractionDistance;
        itemSo.FindProperty("hideWhileInspecting").boolValue = true;
        itemSo.FindProperty("collectOnInspect").boolValue = false;
        itemSo.FindProperty("useCustomStartRotation").boolValue = true;
        itemSo.FindProperty("customStartRotation").vector3Value = new Vector3(8f, -12f, 0f);
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        VentCover ventCover = root.AddComponent<VentCover>();
        var ventSo = new SerializedObject(ventCover);
        SetArray(ventSo.FindProperty("screws"), screwL, screwR);
        ventSo.FindProperty("cover").objectReferenceValue = vent;
        ventSo.FindProperty("opening").objectReferenceValue = openingPart;
        ventSo.FindProperty("sceneToLoad").stringValue = VentSceneName;
        ventSo.ApplyModifiedPropertiesWithoutUndo();

        Vector3 p = r.position;
        log.AppendLine($"• The \"{ExistingVentName}\" on the wall at ({p.x:0.##}, {p.y:0.##}, {p.z:0.##}) is now the vent puzzle " +
                       $"(under {VentName}). Clickable from up to {VentInteractionDistance} m away.");
        return root;
    }

    /// <summary>The vent by the bookshelves: a root-level object named "Vent".</summary>
    private static GameObject FindExistingVent()
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == ExistingVentName)
                return go;
        return null;
    }

    /// <summary>Undoes a previous run: moves the original vent back to the scene root, then deletes CrawlVent.</summary>
    private static void TearDownVent(GameObject oldRoot)
    {
        VentCover ventCover = oldRoot.GetComponent<VentCover>();
        Transform cover = ventCover != null
            ? new SerializedObject(ventCover).FindProperty("cover").objectReferenceValue as Transform
            : null;

        if (cover != null)
        {
            Undo.SetTransformParent(cover, null, "Restore vent");
            Undo.RecordObject(cover.gameObject, "Restore vent");
            cover.gameObject.SetActive(true);
            BoxCollider added = cover.GetComponent<BoxCollider>();
            if (added != null)
                Undo.DestroyObjectImmediate(added);
        }
        Undo.DestroyObjectImmediate(oldRoot);
    }

    private static InspectPart BuildScrew(string name, Transform parent, Vector3 position, float slotAngle, Materials m)
    {
        Transform screw = Empty(name, parent, position);
        Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
        Cylinder("Head", screw, new Vector3(0f, 0f, -0.004f), alongZ, 0.04f, 0.008f, m.metal);
        Cylinder("Thread", screw, new Vector3(0f, 0f, 0.015f), alongZ, 0.01f, 0.03f, m.metal);
        GameObject slot = Box("Slot", screw, new Vector3(0f, 0f, -0.0085f), new Vector3(0.03f, 0.005f, 0.002f), m.black, collider: false);
        slot.transform.localRotation = Quaternion.Euler(0f, 0f, slotAngle);

        BoxCollider col = screw.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, -0.006f);
        col.size = new Vector3(0.065f, 0.065f, 0.02f);   // bigger than the head, easier to click

        InspectPart part = screw.gameObject.AddComponent<InspectPart>();
        SetPart(part, interactable: true, requiredItem: ScrewdriverId,
            missingMessage: "It's screwed on tight. You'll need a screwdriver.", useMessage: "");
        return part;
    }

    // ------------------------------------------------------------------ vent scene

    private static void CreateVentScene(Materials m, StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VentScenePath) != null)
        {
            log.AppendLine($"• {VentScenePath} already exists, left it alone.");
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        Scene ventScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(ventScene);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.035f);
        RenderSettings.skybox = null;

        // A short, dark duct running along +Z.
        const float width = 0.9f, height = 0.75f, length = 10f, t = 0.05f;
        Transform duct = new GameObject("Duct").transform;
        Box("Floor", duct, new Vector3(0f, -t / 2f, length / 2f), new Vector3(width + 2f * t, t, length), m.tunnel);
        Box("Ceiling", duct, new Vector3(0f, height + t / 2f, length / 2f), new Vector3(width + 2f * t, t, length), m.tunnel);
        Box("WallL", duct, new Vector3(-width / 2f - t / 2f, height / 2f, length / 2f), new Vector3(t, height, length), m.tunnel);
        Box("WallR", duct, new Vector3(width / 2f + t / 2f, height / 2f, length / 2f), new Vector3(t, height, length), m.tunnel);
        Box("End", duct, new Vector3(0f, height / 2f, length + t / 2f), new Vector3(width, height, t), m.tunnel);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0.38f, 0.3f), Quaternion.Euler(4f, 0f, 0f));
        Camera cam = cameraObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.02f;
        cameraObject.AddComponent<AudioListener>();

        Light near = new GameObject("Near Light").AddComponent<Light>();
        near.type = LightType.Point;
        near.transform.position = new Vector3(0f, 0.6f, 1.2f);
        near.range = 3.5f;
        near.intensity = 1.2f;
        near.color = new Color(1f, 0.85f, 0.65f);

        Light far = new GameObject("Far Light").AddComponent<Light>();
        far.type = LightType.Point;
        far.transform.position = new Vector3(0f, 0.4f, 8.5f);
        far.range = 3f;
        far.intensity = 0.6f;
        far.color = new Color(0.6f, 0.75f, 1f);

        new GameObject("Placeholder").AddComponent<PlaceholderScene>();

        EditorSceneManager.SaveScene(ventScene, VentScenePath);
        SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(ventScene, true);
        log.AppendLine($"• Created {VentScenePath} (placeholder: dark duct + text, Enter goes back to the main menu).");
    }

    private static void AddVentSceneToBuild(StringBuilder log)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == VentScenePath))
            return;

        scenes.Add(new EditorBuildSettingsScene(VentScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.AppendLine("• Added Vent to the build scene list.");
    }

    // ------------------------------------------------------------------ helpers

    private static Materials CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(MaterialFolderParent, "ToolboxVent");

        return new Materials
        {
            red = GetOrCreateMaterial("Toolbox_Red", new Color(0.62f, 0.05f, 0.04f), 0.3f, 0.55f),
            interior = GetOrCreateMaterial("Toolbox_Interior", new Color(0.08f, 0.08f, 0.09f), 0f, 0.2f),
            metal = GetOrCreateMaterial("Toolbox_Metal", new Color(0.72f, 0.72f, 0.75f), 1f, 0.7f),
            black = GetOrCreateMaterial("Toolbox_Black", new Color(0.06f, 0.06f, 0.06f), 0f, 0.35f),
            screwdriverHandle = GetOrCreateMaterial("Screwdriver_Handle", new Color(0.95f, 0.72f, 0.08f), 0f, 0.6f),
            hole = GetOrCreateMaterial("Vent_Hole", Color.black, 0f, 0f, unlit: true),
            tunnel = GetOrCreateMaterial("Vent_Duct", new Color(0.2f, 0.2f, 0.22f), 0.6f, 0.4f),
        };
    }

    private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness, bool unlit = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static float FindFloorY(Vector2 xz, out bool found)
    {
        // Lowest upward-facing surface under this spot.
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(xz.x, 7f, xz.y), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit hit in hits)
            if (hit.normal.y > 0.9f && hit.point.y < best)
                best = hit.point.y;

        found = best < float.MaxValue;
        return found ? best : FallbackFloorY;
    }

    private static Bounds WorldBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(target.position, Vector3.one * 0.5f);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>Bounds of every mesh under target, in space's local coordinates.</summary>
    private static Bounds LocalBounds(Transform target, Transform space)
    {
        bool any = false;
        Bounds result = new Bounds();
        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;

            Bounds mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                {
                    result = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                    result.Encapsulate(p);
            }
        }
        return any ? result : new Bounds(Vector3.zero, new Vector3(1.2f, 0.6f, 0.12f));
    }

    private static Transform Empty(string name, Transform parent, Vector3 localPosition)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>Cylinder with no collider. Unity's cylinder runs along local Y; rotation turns it.</summary>
    private static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
        float diameter, float length, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = new Vector3(diameter, length / 2f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static void SetPart(InspectPart part, bool interactable, string requiredItem, string missingMessage, string useMessage)
    {
        var so = new SerializedObject(part);
        so.FindProperty("interactable").boolValue = interactable;
        so.FindProperty("singleUse").boolValue = true;
        so.FindProperty("requiredItemId").stringValue = requiredItem;
        so.FindProperty("missingItemMessage").stringValue = missingMessage;
        so.FindProperty("useMessage").stringValue = useMessage;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty array, params Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
