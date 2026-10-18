using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnclaimedPosteJardim
{
    public const string PrefabPath = "Assets/Modelos/Entorno/PosteJardim/Poste_Jardim.prefab";
    private const string Folder = "Assets/Modelos/Entorno/PosteJardim";
    private const string MaterialFolder = Folder + "/Materiais";
    private const string FbxPath = Folder + "/Poste_Jardim_Meshy.fbx";

    [MenuItem("Tools/Unclaimed/Build Poste Jardim")]
    public static void BuildPrefab()
    {
        EnsureFolder("Assets/Modelos/Entorno", "PosteJardim");
        EnsureFolder(Folder, "Materiais");
        if (!File.Exists(FbxPath))
        {
            Debug.LogWarning("Unclaimed: missing FBX " + FbxPath + ".");
            return;
        }
        if (AssetImporter.GetAtPath(FbxPath) is ModelImporter importer && ConfigureModel(importer)) importer.SaveAndReimport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogWarning("Unclaimed: could not load FBX " + FbxPath + ".");
            return;
        }
        Transform posteSource = FindChild(model.transform, "Poste");
        Transform globeSource = FindChild(model.transform, "Globo");
        Transform supportSource = FindChild(model.transform, "Apoio");
        if (posteSource == null || globeSource == null || supportSource == null)
        {
            Debug.LogWarning("Unclaimed: " + FbxPath + " must contain mesh objects Poste, Globo, and Apoio.");
            return;
        }

        Shader globeShader = Shader.Find("Unclaimed/Globo Leitoso");
        if (globeShader == null)
        {
            Debug.LogWarning("Unclaimed: shader Unclaimed/Globo Leitoso was not found.");
            return;
        }
        Material concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnclaimedPrototype/Entorno/Materiais/Concreto.mat");
        Material soil = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnclaimedPrototype/Entorno/Materiais/Terra com folhas.mat");
        if (concrete == null || soil == null)
        {
            Debug.LogWarning("Unclaimed: missing support material(s): " +
                (concrete == null ? "Assets/UnclaimedPrototype/Entorno/Materiais/Concreto.mat " : "") +
                (soil == null ? "Assets/UnclaimedPrototype/Entorno/Materiais/Terra com folhas.mat" : ""));
            return;
        }

        Material metal = Mat("Poste jardim - metal pintado", new Color(0.045f, 0.05f, 0.05f), 0.45f, 0.3f);
        Material globe = GlobeMat(globeShader);
        Material screws = Mat("Poste jardim - parafusos", new Color(0.55f, 0.56f, 0.56f), 0.45f, 0.7f);
        var root = new GameObject("Poste de jardim");
        try
        {
            GameObject poste = CopyChild(posteSource, root.transform);
            GameObject globo = CopyChild(globeSource, root.transform);
            GameObject apoio = CopyChild(supportSource, root.transform);
            ConfigureRenderer(poste.GetComponentsInChildren<Renderer>(true), ShadowCastingMode.On);
            ConfigureRenderer(globo.GetComponentsInChildren<Renderer>(true), ShadowCastingMode.Off);
            ConfigureRenderer(apoio.GetComponentsInChildren<Renderer>(true), ShadowCastingMode.On);
            AssignPosteMaterials(poste.GetComponentsInChildren<Renderer>(true), metal, screws);
            AssignGlobeMaterials(globo.GetComponentsInChildren<Renderer>(true), globe);
            AssignSupportMaterials(apoio.GetComponentsInChildren<Renderer>(true), concrete, soil);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.1f;
            capsule.height = 2.8f;
            capsule.center = new Vector3(0f, 1.31f, 0f);
            var supportCollider = root.AddComponent<BoxCollider>();
            supportCollider.center = Vector3.zero;
            supportCollider.size = new Vector3(0.46f, 0.18f, 0.46f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Unclaimed: Poste de jardim prefab, tris Poste/Globo/Apoio " +
                Triangles(poste) + "/" + Triangles(globo) + "/" + Triangles(apoio) + ".");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }

    private static bool ConfigureModel(ModelImporter model)
    {
        bool changed = model.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
            model.materialLocation != ModelImporterMaterialLocation.InPrefab || !model.bakeAxisConversion ||
            model.importNormals != ModelImporterNormals.Import || model.importAnimation || model.importCameras ||
            model.importLights || model.generateSecondaryUV || model.isReadable ||
            model.meshCompression != ModelImporterMeshCompression.Low || model.globalScale != 1f;
        model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        model.materialLocation = ModelImporterMaterialLocation.InPrefab;
        model.bakeAxisConversion = true;
        model.importNormals = ModelImporterNormals.Import;
        model.importAnimation = false;
        model.importCameras = false;
        model.importLights = false;
        model.generateSecondaryUV = false;
        model.isReadable = false;
        model.meshCompression = ModelImporterMeshCompression.Low;
        model.globalScale = 1f;
        return changed;
    }

    private static Material Mat(string name, Color color, float smoothness, float metallic, Color? emission = null)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GlobeMat(Shader shader)
    {
        string path = MaterialFolder + "/Poste jardim - globo leitoso.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "Poste jardim - globo leitoso" };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.SetColor("_BaseColor", new Color(0.93f, 0.91f, 0.86f, 1f));
        material.SetFloat("_Smoothness", 0.75f);
        material.SetColor("_EmissionColor", new Color(0.95f, 0.80f, 0.58f, 1f) * 1.6f);
        material.SetFloat("_CoreIntensity", 1f);
        material.SetFloat("_RimIntensity", 0.22f);
        material.SetFloat("_FresnelPower", 1.3f);
        material.SetVector("_BulbCenter", new Vector4(0f, 2.72f, 0f, 0f));
        material.SetFloat("_GlobeRadius", 0.14f);
        material.SetFloat("_BottomDarken", 0.5f);
        material.SetFloat("_TopDarken", 0.2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != root && child.name == name) return child;
        return null;
    }

    private static GameObject CopyChild(Transform source, Transform parent)
    {
        GameObject child = UnityEngine.Object.Instantiate(source.gameObject, parent);
        child.name = source.name;
        child.transform.localPosition = source.localPosition;
        child.transform.localRotation = source.localRotation;
        child.transform.localScale = source.localScale;
        return child;
    }

    private static void ConfigureRenderer(Renderer[] renderers, ShadowCastingMode shadows)
    {
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            if (renderer is MeshRenderer meshRenderer) meshRenderer.receiveGI = ReceiveGI.LightProbes;
        }
    }

    private static void AssignPosteMaterials(Renderer[] renderers, Material metal, Material screws)
    {
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string slot = materials[i] != null ? materials[i].name : "";
                if (slot == "Metal_Preto" && metal != null) materials[i] = metal;
                else if (slot == "Parafusos") materials[i] = screws;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void AssignGlobeMaterials(Renderer[] renderers, Material globe)
    {
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].name == "Globo_Leitoso") materials[i] = globe;
            renderer.sharedMaterials = materials;
        }
    }

    private static void AssignSupportMaterials(Renderer[] renderers, Material concrete, Material soil)
    {
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string slot = materials[i] != null ? materials[i].name : "";
                if (slot == "Concreto") materials[i] = concrete;
                else if (slot == "Terra") materials[i] = soil;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static int Triangles(GameObject root)
    {
        int count = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
        }
        return count;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
