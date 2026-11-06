using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time setup: Vigil > Add Rainbow to Book. Puts a rainbow decal on the book's cover (TestItem),
/// under the label, as a hint that the polaroid colors are read in rainbow order.
/// The decal is a child quad, so it also shows in the inspect view. One Undo step; save the scene after.
/// </summary>
public static class VigilBookRainbow
{
    private const string BookName = "TestItem";
    private const string DecalName = "Rainbow";
    private const string TexturePath = "Assets/Art/Book/book_rainbow.png";
    private const string MaterialPath = "Assets/Materials/BookRainbow.mat";
    private const string TemplateMaterialPath = "Assets/Materials/Piece_01.mat";   // URP Lit, alpha clip

    [MenuItem("Vigil/Add Rainbow to Book")]
    [MenuItem("Assets/Vigil - Add Rainbow to Book", false, 1)]
    private static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Vigil", "Stop Play mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        InspectableItem book = Resources.FindObjectsOfTypeAll<InspectableItem>()
            .FirstOrDefault(i => !EditorUtility.IsPersistent(i) && i.gameObject.scene == scene && i.gameObject.name == BookName);
        if (book == null)
        {
            EditorUtility.DisplayDialog("Vigil", $"No \"{BookName}\" with an InspectableItem in the open scene.", "OK");
            return;
        }
        if (book.transform.Find(DecalName) != null)
        {
            EditorUtility.DisplayDialog("Vigil", "The book already has a rainbow. Delete it and run again to rebuild.", "OK");
            return;
        }

        Material material = GetOrCreateMaterial();
        if (material == null)
        {
            EditorUtility.DisplayDialog("Vigil", $"Couldn't load {TexturePath}. Let Unity finish importing, then try again.", "OK");
            return;
        }

        // A quad lying flat on the cover, picture facing up, with the top of the rainbow toward the label.
        GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(decal.GetComponent<Collider>());
        decal.name = DecalName;
        Undo.RegisterCreatedObjectUndo(decal, "Add rainbow to book");
        decal.transform.SetParent(book.transform, false);
        decal.transform.localPosition = new Vector3(0f, 0.0405f, -0.065f);   // cover top is y = 0.04
        decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);       // same as the label text
        decal.transform.localScale = new Vector3(0.2f, 0.12f, 1f);           // matches the 640x384 image

        MeshRenderer renderer = decal.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = decal;
        EditorUtility.DisplayDialog("Vigil", "Added a rainbow to the book's cover. Save the scene (Cmd+S) to keep it.", "OK");
    }

    private static Material GetOrCreateMaterial()
    {
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            return null;
        if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.alphaIsTransparency = true;   // no dark fringe around the cut-out edges
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            if (!AssetDatabase.CopyAsset(TemplateMaterialPath, MaterialPath))
            {
                Debug.LogError($"Vigil: couldn't copy {TemplateMaterialPath} to {MaterialPath}.");
                return null;
            }
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.5f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
