using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

// Baked storage-room kit. Prefabs face -Z, with their origin on the floor and dimensions in metres.
public static class UnclaimedDepositoAssets
{
    public const string Folder = "Assets/Modelos/Deposito";
    const string Materials = Folder + "/Materiais";

    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";

    [MenuItem("Tools/Unclaimed/Deposito/Build Prefabs")]
    public static void Build()
    {
        if (!Directory.Exists(Folder)) { Debug.LogWarning("Unclaimed: missing " + Folder); return; }
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materiais");
        if (!AssetDatabase.IsValidFolder(Folder + "/Prefabs")) AssetDatabase.CreateFolder(Folder, "Prefabs");
        string[] files = Directory.GetFiles(Folder, "*.fbx", SearchOption.TopDirectoryOnly);
        System.Array.Sort(files, System.StringComparer.Ordinal);
        foreach (string path in files) BuildSimple(Path.GetFileNameWithoutExtension(path));
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: storage-room prefabs built in " + Folder + "/Prefabs.");
    }

    static Mesh Import(string file, out Material material)
    {
        material = null;
        string path = Folder + "/" + file + ".fbx", prefix = Folder + "/Texturas/" + file;
        foreach (string suffix in new[] { "_BaseMap", "_MetallicSmoothness", "_Normal", "_Occlusion" })
            if (!File.Exists(prefix + suffix + ".png")) { Debug.LogWarning("Unclaimed: missing " + prefix + suffix + ".png; skipping " + file); return null; }
        if (!(AssetImporter.GetAtPath(path) is ModelImporter model)) { Debug.LogWarning("Unclaimed: missing " + path); return null; }
        model.globalScale = 1f;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importAnimation = model.importCameras = model.importLights = model.importBlendShapes = false;
        model.meshCompression = ModelImporterMeshCompression.Off;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.generateSecondaryUV = true;
        model.SaveAndReimport();
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) return mesh;
        string mp = Materials + "/" + file + ".mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = file }; AssetDatabase.CreateAsset(material, mp); }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", Tex(prefix + "_BaseMap.png", true, false));
        material.SetTexture("_MetallicGlossMap", Tex(prefix + "_MetallicSmoothness.png", false, false));
        material.SetTexture("_BumpMap", Tex(prefix + "_Normal.png", false, true));
        material.SetTexture("_OcclusionMap", Tex(prefix + "_Occlusion.png", false, false));
        material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 1f); material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.SetFloat("_BumpScale", 1f); material.SetFloat("_OcclusionStrength", 1f); material.SetFloat("_Cull", 2f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return mesh;
    }

    static Texture2D Tex(string path, bool srgb, bool normal)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb; ti.maxTextureSize = 2048; ti.anisoLevel = 8;
            ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Box(GameObject go, float x, float y, float z, float w, float h, float d)
    {
        var b = go.AddComponent<BoxCollider>(); b.center = new Vector3(x, y, z); b.size = new Vector3(w, h, d);
    }

    static void BuildSimple(string file)
    {
        Mesh mesh = Import(file, out Material material);
        if (mesh == null) return;
        var root = new GameObject(file);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            Bounds b = mesh.bounds;
            switch (file)
            {
                case "EstanteRasa": Shelves(root, .40f, false); break;
                case "EstanteEstreita": Shelves(root, .30f, false); break;
                case "EstanteFunda": Shelves(root, .60f, true); break;
                case "CaixaSacoCadaver": case "CaixaLuvas": case "CaixaAventais": case "CaixaAlgodao":
                case "CaixaCompressas": case "CaixaMascaras": case "CaixaAberta": case "CaixaOrganizadora":
                case "Estrado": case "Bandeja": case "GalaoFormol":
                    Box(root, b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z);
                    break;
                case "Carrinho":
                    Box(root, b.center.x, .105f, b.center.z, b.size.x, .21f, b.size.z);
                    Box(root, b.center.x, b.max.y / 2, b.max.z - .03f, b.size.x, b.max.y, .06f);
                    var marker = new GameObject("Ponto de encaixe - caixa").transform;
                    marker.SetParent(root.transform, false);
                    marker.localPosition = new Vector3(b.center.x, .21f, b.center.z);
                    break;
                default: break;   // small supplies carry no collider
            }
            // Match the rest-room kit's static furniture flags; the trolley remains movable.
            root.isStatic = file != "Carrinho";
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(file));
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void Shelves(GameObject root, float depth, bool deep)
    {
        float[] tops = deep ? new[] { .10f, .55f, 1f, 1.45f, 1.98f } : new[] { .10f, .47f, .84f, 1.21f, 1.58f, 1.98f };
        foreach (float y in tops) Box(root, 0, y - .015f, 0, .92f, .03f, depth);
        foreach (float x in new[] { -.444f, .444f })
            foreach (float z in new[] { -depth / 2 + .016f, depth / 2 - .016f })
                Box(root, x, .99f, z, .032f, 1.98f, .032f);
    }
}
