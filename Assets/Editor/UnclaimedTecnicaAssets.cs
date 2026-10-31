using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

// Baked technical-room kit. Prefabs face -Z, with their origin on the floor and dimensions in metres.
public static class UnclaimedTecnicaAssets
{
    public const string Folder = "Assets/Modelos/Tecnica";
    const string Materials = Folder + "/Materiais";

    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";

    [MenuItem("Tools/Unclaimed/Tecnica/Build Prefabs")]
    public static void Build()
    {
        if (!Directory.Exists(Folder)) { Debug.LogWarning("Unclaimed: missing " + Folder); return; }
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materiais");
        if (!AssetDatabase.IsValidFolder(Folder + "/Prefabs")) AssetDatabase.CreateFolder(Folder, "Prefabs");
        foreach (string cor in new[] { "verde", "ambar", "vermelho" })
        { LensMaterial(cor, false); LensMaterial(cor, true); }
        string[] files = { "PainelContingencia", "QuadroGeral", "QuadroCamaraFria", "Infraestrutura", "Bancada", "Seletor", "SeletorChave", "Sinaleiro", "SinaleiroLente" };
        foreach (string file in files) BuildSimple(file);
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: technical-room prefabs built in " + Folder + "/Prefabs.");
    }

    static Mesh Import(string file, out Material material)
    {
        material = null;
        string path = Folder + "/" + file + ".fbx", prefix = Folder + "/Texturas/" + file;
        if (file != "SinaleiroLente")
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
        if (file == "SinaleiroLente") { material = LensMaterial("verde", false); return mesh; }
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
            ti.sRGBTexture = srgb; ti.maxTextureSize = 4096; ti.anisoLevel = 8;   // the tray and the bench bake at 4096
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
            switch (file)
            {
                case "QuadroGeral": Box(root, 0, 1.35f, -.09f, .60f, .90f, .18f); break;
                case "PainelContingencia": Box(root, 0, 1.35f, -.10f, .60f, .70f, .20f); break;
                case "QuadroCamaraFria": Box(root, 0, 1.40f, -.08f, .40f, .50f, .16f); break;
                case "Bancada":
                    Box(root, 0, .885f, -.32f, 1.20f, .03f, .60f);
                    Box(root, 0, .435f, -.32f, 1.20f, .87f, .60f);
                    Box(root, 0, 1.44f, -.015f, .97f, .64f, .03f);
                    break;
            }
            root.isStatic = file != "Seletor" && file != "SeletorChave" && file != "Sinaleiro" && file != "SinaleiroLente";
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(file));
        }
        finally { Object.DestroyImmediate(root); }
    }

    static Material LitMaterial(string name)
    {
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materiais");
        string path = Materials + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    internal static Material LensMaterial(string cor, bool aceso)
    {
        Color off, on, emission;
        switch (cor)
        {
            case "ambar":
                off = new Color(.20f, .12f, .02f); on = new Color(.95f, .55f, .08f); emission = new Color(1.6f, .8f, .10f); break;
            case "vermelho":
                off = new Color(.20f, .03f, .02f); on = new Color(.85f, .12f, .08f); emission = new Color(1.6f, .16f, .10f); break;
            default:
                cor = "verde";
                off = new Color(.04f, .16f, .06f); on = new Color(.20f, .85f, .30f); emission = new Color(.25f, 1.6f, .40f); break;
        }
        var material = LitMaterial("Lente - " + cor + (aceso ? " acesa" : " apagada"));
        material.SetColor("_BaseColor", aceso ? on : off);
        material.SetFloat("_Smoothness", .8f);
        material.SetColor("_EmissionColor", aceso ? emission : Color.black);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        if (aceso) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    internal static Material DisplayMaterial(string texture)
    {
        var material = LitMaterial("Visor - " + Path.GetFileNameWithoutExtension(texture));
        // Digits also go in the base map, so the display reads even where emission is not drawn
        var digits = Tex(Folder + "/Texturas/" + texture, true, false);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", digits);
        material.SetFloat("_Smoothness", .85f);
        material.SetTexture("_EmissionMap", digits);
        material.SetColor("_EmissionColor", Color.white * 1.4f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }
}
