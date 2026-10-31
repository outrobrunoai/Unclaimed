using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

// Corridor fittings (Blender/Circulacao/scripts/gerar.py -> Assets/Modelos/Circulacao): the wall-hung fire
// extinguisher with its printed inspection label, and the autonomous emergency light block. Prefabs face -Z, the wall
// plane is the prefab's local z = 0 plane (the fitting stands in front of it).
public static class UnclaimedCirculacaoAssets
{
    public const string Folder = "Assets/Modelos/Circulacao";
    public const string Extintor = "Extintor", Bloco = "Bloco de emergencia", Acionador = "Acionador manual";
    const string SinaisFolder = "Assets/UnclaimedPrototype/Circulacao/Sinais";
    public static readonly string[] Placas =
    {
        "ID_Arquivo", "ID_Descanso", "ID_Deposito", "ID_Autopsia", "ID_SalaTecnica", "ID_Recebimento", "ID_CamaraFria",
        "OR_Diretorio", "OR_Recebimento", "SEG_Extintor", "SEG_Alarme", "SEG_Risco", "SEG_Saida_Frente", "SEG_Saida_Direita",
    };

    public static string PlacaPrefab(string name) => PrefabPath("Placa " + name);

    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";

    [MenuItem("Tools/Unclaimed/Circulacao/Build Fitting Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder + "/Materiais");
        Directory.CreateDirectory(Folder + "/Prefabs");
        AssetDatabase.Refresh();
        foreach (string n in new[] { "Extintor", "ExtintorEtiqueta", "ExtintorMostrador", "BlocoEmergencia", "Acionador", "AcionadorFrente" }) ImportModel(n);
        BuildExtintor();
        BuildBloco();
        BuildAcionador();
        foreach (string n in Placas) BuildPlaca(n);
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: corridor fitting prefabs built in " + Folder + "/Prefabs.");
    }

    static void ImportModel(string name, string sub = "")
    {
        string fbx = Folder + "/" + sub + name + ".fbx";
        if (!(AssetImporter.GetAtPath(fbx) is ModelImporter mi)) throw new FileNotFoundException(fbx);
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importCameras = false; mi.importLights = false; mi.importAnimation = false; mi.importBlendShapes = false;
        mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk; mi.generateSecondaryUV = true;
        mi.SaveAndReimport();
    }

    static Texture2D Tex(string path, bool normal = false, bool srgb = false)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) throw new FileNotFoundException(path);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb; ti.mipmapEnabled = true; ti.maxTextureSize = 2048; ti.anisoLevel = 4;
        ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material BakedMaterial(string name)
    {
        string mp = Folder + "/Materiais/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
        string t = Folder + "/Texturas/" + name;
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", Tex(t + "_BaseMap.png", srgb: true));
        m.SetTexture("_MetallicGlossMap", Tex(t + "_MetallicSmoothness.png"));
        m.SetTexture("_BumpMap", Tex(t + "_Normal.png", normal: true));
        m.SetTexture("_OcclusionMap", Tex(t + "_Occlusion.png"));
        m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", 1); m.SetFloat("_BumpScale", 0.8f); m.SetFloat("_OcclusionStrength", 0.7f);
        m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP");
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject MeshObject(string name, Transform parent, string model, Material m)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + model + ".fbx");
        if (mesh == null) throw new FileNotFoundException(model);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = Enumerable.Repeat(m, mesh.subMeshCount).ToArray();
        r.lightProbeUsage = LightProbeUsage.BlendProbes;
        r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        return go;
    }

    static Material PrintedMaterial(string matName, string texPath, float smoothness, bool emissive = false)
    {
        string mp = Folder + "/Materiais/" + matName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
        var tex = Tex(texPath, srgb: true);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material PlainMaterial(string matName, Color c, float smoothness, float metallic = 0)
    {
        string mp = Folder + "/Materiais/" + matName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void AddBox(GameObject root, Bounds b, float pad = 0f)
    {
        var box = root.AddComponent<BoxCollider>();
        box.center = b.center; box.size = b.size + Vector3.one * pad;
    }

    static void BuildExtintor()
    {
        var root = new GameObject(Extintor);
        try
        {
            var corpo = MeshObject("Corpo", root.transform, "Extintor", BakedMaterial("Extintor"));
            var rotulo = MeshObject("Etiqueta", root.transform, "ExtintorEtiqueta", PrintedMaterial("Extintor rotulo", SinaisFolder + "/EQ_ExtintorRotulo.png", 0.3f));
            rotulo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var dial = MeshObject("Mostrador", root.transform, "ExtintorMostrador", PrintedMaterial("Extintor manometro", SinaisFolder + "/EQ_Manometro.png", 0.6f));
            dial.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // One box around the body and hose: nobody walks through a wall-hung extinguisher.
            AddBox(root, corpo.GetComponent<MeshFilter>().sharedMesh.bounds);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(Extintor));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void BuildAcionador()
    {
        var root = new GameObject(Acionador);
        try
        {
            var corpo = MeshObject("Corpo", root.transform, "Acionador", BakedMaterial("Acionador"));
            var frente = MeshObject("Frente", root.transform, "AcionadorFrente", PrintedMaterial("Acionador frente", SinaisFolder + "/EQ_AcionadorFrente.png", 0.4f));
            frente.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddBox(root, corpo.GetComponent<MeshFilter>().sharedMesh.bounds);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(Acionador));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // One mesh, three slots: 0 printed face, 1 edge/back, 2 screws (see Blender/Circulacao/scripts/placas.py).
    static void BuildPlaca(string name)
    {
        ImportModel(name, "Placas/");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Placas/" + name + ".fbx");
        if (mesh == null) throw new FileNotFoundException(name);
        bool exit = name.StartsWith("SEG_Saida"), orient = name.StartsWith("OR_"), safety = name.StartsWith("SEG_") && !exit;
        Color edge = exit ? new Color(0.82f, 0.81f, 0.76f) : orient ? new Color(0.20f, 0.27f, 0.23f) : safety ? new Color(0.55f, 0.55f, 0.52f) : new Color(0.78f, 0.76f, 0.70f);
        string kind = exit ? "exit" : orient ? "orient" : safety ? "safety" : "room";
        var front = PrintedMaterial("Placa frente " + name, SinaisFolder + "/" + name + ".png", exit ? 0.35f : 0.42f, exit);
        var rim = PlainMaterial("Placa borda " + kind, edge, 0.5f);
        var screw = PlainMaterial("Placa parafuso", new Color(0.72f, 0.72f, 0.70f), 0.6f, 1f);
        var root = new GameObject("Placa " + name);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = root.AddComponent<MeshRenderer>();
            // The importer orders the submeshes by its own rule, so the roles are told apart by geometry: the printed
            // face is the flat one, the screws have no UV, the rest is the edge.
            var mats = new Material[mesh.subMeshCount];
            var uv = mesh.uv; var vert = mesh.vertices;
            for (int sm = 0; sm < mats.Length; sm++)
            {
                float zMin = float.MaxValue, zMax = float.MinValue, uvSpan = 0f;
                foreach (int i in mesh.GetIndices(sm)) { zMin = Mathf.Min(zMin, vert[i].z); zMax = Mathf.Max(zMax, vert[i].z); uvSpan = Mathf.Max(uvSpan, uv[i].sqrMagnitude); }
                mats[sm] = uvSpan < 1e-6f ? screw : zMax - zMin < 1e-5f ? front : rim;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            PrefabUtility.SaveAsPrefabAsset(root, PlacaPrefab(name));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void BuildBloco()
    {
        var root = new GameObject(Bloco);
        try
        {
            MeshObject("Corpo", root.transform, "BlocoEmergencia", BakedMaterial("BlocoEmergencia"));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(Bloco));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
