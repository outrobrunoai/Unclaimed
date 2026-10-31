using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

// Interior door kit (Blender/Portas/scripts/gerar.py -> Assets/Modelos/Portas): imports the models, builds one URP Lit
// material per baked atlas and assembles the door prefabs the map generator places (Door.Frame). Prefab frame: origin on
// the opening's centre at floor level, swing side +Z, leaves on pivots at the jambs (a leaf hinged on the right is
// modelled once and mirrored in a child, so its collider never has a negative scale). Dimensions mirror the Blender
// parameters (PORTA_CIRC and the clinical / cold-room overrides).
public static class UnclaimedPortasAssets
{
    public const string Folder = "Assets/Modelos/Portas";
    public const string GlassMaterialPath = "Assets/Modelos/Recepcao/Materiais/Vidro temperado.mat";

    // Prefab names.
    public const string Adm = "Porta institucional - administrativa", Descanso = "Porta institucional - descanso",
        Deposito = "Porta institucional - deposito", Tecnica = "Porta tecnica", Clinica = "Porta clinica dupla",
        Fria = "Porta frigorifica dupla", Mirrored = " (espelhada)";

    static readonly string[] Baked =
    {
        "PortaInstMarco", "PortaInstAdm", "PortaInstAdmSec", "PortaInstDescanso", "PortaInstDescansoSec", "PortaInstDeposito",
        "PortaInstDepositoSec", "PortaTecAtiva", "PortaTecSec", "PortaClinicaMarco", "PortaClinicaFolha", "PortaFriaMarco", "PortaFriaFolha",
    };
    const string GlassModel = "PortaClinicaVidro";

    // Shared by every frame (Blender mkP): wall half-thickness plus casing, nominal raw opening, leaf clearances.
    const float Wall = 0.103f, FlangeT = 0.012f, Raw = 1.5f, Height = 2.2f, Gap = 0.003f, CenterGap = 0.004f, Hinge0 = 0.0015f;

    sealed class Fam
    {
        public float LeafT, StopU, RebateU, SillTop, FloorGap, Knuckle;
        public float Face => Wall + FlangeT;
        public float Back => Face - LeafT;
        public float Jx => Raw / 2 - RebateU;
        public float PivotX => Jx - Hinge0;             // |x| of the hinge axis
        public float PivotZ => Face + Knuckle;
        public float Z0 => SillTop + FloorGap;
        public float Z1 => Height - RebateU - Gap;
        public float FullWidth => 2 * Jx - 2 * Gap - CenterGap;
    }

    static readonly Fam FamA = new Fam { LeafT = 0.040f, StopU = 0.045f, RebateU = 0.030f, SillTop = 0.004f, FloorGap = 0.008f, Knuckle = 0.0065f };
    static readonly Fam FamC = new Fam { LeafT = 0.045f, StopU = 0.045f, RebateU = 0.030f, SillTop = 0.005f, FloorGap = 0.007f, Knuckle = 0.0065f };
    static readonly Fam FamD = new Fam { LeafT = 0.090f, StopU = 0.050f, RebateU = 0.034f, SillTop = 0.006f, FloorGap = 0.008f, Knuckle = 0.0085f };

    sealed class Leaf
    {
        public string Model, Name;
        public float Width;
        public int Side;               // -1: hinged on the -X jamb, +1: on the +X jamb
        public bool Glass;
        public string Open, Close;
    }

    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";

    [MenuItem("Tools/Unclaimed/Portas/Build Door Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder + "/Materiais");
        Directory.CreateDirectory(Folder + "/Prefabs");
        AssetDatabase.Refresh();
        foreach (string n in Baked) ImportModel(n);
        ImportModel(GlassModel);
        foreach (string n in Baked) EnsureMaterial(n);

        // A: institutional, active leaf on the -X jamb with a narrower secondary leaf on the other; also mirrored.
        foreach (var (prefab, act, sec) in new[] { (Adm, "PortaInstAdm", "PortaInstAdmSec"), (Descanso, "PortaInstDescanso", "PortaInstDescansoSec"),
                                                   (Deposito, "PortaInstDeposito", "PortaInstDepositoSec"), (Tecnica, "PortaTecAtiva", "PortaTecSec") })
            foreach (bool mirror in new[] { false, true })
            {
                float wa = 0.86f, ws = FamA.FullWidth - wa;
                int s = mirror ? 1 : -1;
                BuildDoor(prefab + (mirror ? Mirrored : ""), FamA, "PortaInstMarco", new[]
                {
                    new Leaf { Model = act, Name = "Folha", Width = wa, Side = s, Open = prefab == Tecnica ? "Abrir porta técnica" : "Abrir porta", Close = "Fechar porta" },
                    new Leaf { Model = sec, Name = "Folha secundaria", Width = ws, Side = -s, Open = "Abrir folha secundária", Close = "Fechar folha secundária" },
                }, duration: prefab == Tecnica ? 1.2f : 1.1f);
            }

        float wc = FamC.FullWidth / 2;
        BuildDoor(Clinica, FamC, "PortaClinicaMarco", new[]
        {
            new Leaf { Model = "PortaClinicaFolha", Name = "Folha esquerda", Width = wc, Side = -1, Glass = true, Open = "Abrir porta", Close = "Fechar porta" },
            new Leaf { Model = "PortaClinicaFolha", Name = "Folha direita", Width = wc, Side = 1, Glass = true, Open = "Abrir porta", Close = "Fechar porta" },
        }, duration: 0.9f);

        float wd = FamD.FullWidth / 2;
        BuildDoor(Fria, FamD, "PortaFriaMarco", new[]
        {
            new Leaf { Model = "PortaFriaFolha", Name = "Folha esquerda", Width = wd, Side = -1, Open = "Abrir porta frigorífica", Close = "Fechar porta frigorífica" },
            new Leaf { Model = "PortaFriaFolha", Name = "Folha direita", Width = wd, Side = 1, Open = "Abrir porta frigorífica", Close = "Fechar porta frigorífica" },
        }, duration: 1.6f);
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: door prefabs built in " + Folder + "/Prefabs.");
    }

    static void ImportModel(string name)
    {
        string fbx = Folder + "/" + name + ".fbx";
        if (!(AssetImporter.GetAtPath(fbx) is ModelImporter mi)) throw new FileNotFoundException(fbx);
        mi.materialImportMode = name == GlassModel ? ModelImporterMaterialImportMode.ImportViaMaterialDescription : ModelImporterMaterialImportMode.None;
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

    static Material EnsureMaterial(string name)
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

    static Mesh MeshOf(string name) => AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + name + ".fbx");

    static GameObject MeshObject(string name, Transform parent, string model, Material m)
    {
        Mesh mesh = MeshOf(model);
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

    static void BuildDoor(string prefabName, Fam f, string frameModel, Leaf[] leaves, float duration)
    {
        var root = new GameObject(prefabName);
        try
        {
            Mesh frame = MeshOf(frameModel);
            root.AddComponent<MeshFilter>().sharedMesh = frame;
            var fr = root.AddComponent<MeshRenderer>();
            fr.sharedMaterials = Enumerable.Repeat(EnsureMaterial(frameModel), frame.subMeshCount).ToArray();
            fr.lightProbeUsage = LightProbeUsage.BlendProbes; fr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            float hw = Raw / 2;
            foreach (float sx in new[] { -1f, 1f })
            {
                // Stop (reception side of the leaves) and rebate (beside the leaf edge) parts of each jamb: split so none overlaps a leaf.
                var stop = root.AddComponent<BoxCollider>();
                stop.center = new Vector3(sx * (hw - f.StopU / 2), 1.1f, (-(Wall + FlangeT) + f.Back) / 2);
                stop.size = new Vector3(f.StopU, 2.2f, f.Back + Wall + FlangeT);
                var rebate = root.AddComponent<BoxCollider>();
                rebate.center = new Vector3(sx * (f.Jx + f.RebateU / 2), 1.1f, (f.Back + f.Face) / 2);
                rebate.size = new Vector3(f.RebateU, 2.2f, f.LeafT);
            }
            foreach (Leaf l in leaves) BuildLeaf(root.transform, f, l, duration);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(prefabName));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void BuildLeaf(Transform root, Fam f, Leaf l, float duration)
    {
        var leaf = new GameObject(l.Name).transform;
        leaf.SetParent(root, false);
        leaf.localPosition = new Vector3(l.Side * f.PivotX, 0f, f.PivotZ);
        float x0 = Gap - Hinge0, mid = x0 + l.Width / 2;
        var box = leaf.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(-l.Side * mid, (f.Z0 + f.Z1) / 2, (f.Back + f.Face) / 2 - f.PivotZ);
        box.size = new Vector3(l.Width, f.Z1 - f.Z0, f.LeafT);
        var body = leaf.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = false;
        var audio = leaf.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 1f; audio.minDistance = 0.5f; audio.maxDistance = 10f;
        var door = leaf.gameObject.AddComponent<FolhaDePorta>();
        door.sentido = l.Side < 0 ? -1 : 1;     // swing side is +Z: a leaf hinged on -X turns with -1, mirrored with +1
        door.anguloMaximo = 95f;
        door.duracao = duration;
        door.alcance = 2f;
        door.iniciarAberta = false;
        door.fonte = audio;
        door.textoAbrir = l.Open; door.textoFechar = l.Close;

        Transform geometry = leaf;
        if (l.Side > 0)
        {
            geometry = new GameObject("Espelhada").transform;
            geometry.SetParent(leaf, false);
            geometry.localScale = new Vector3(-1, 1, 1);
        }
        MeshObject("Malha", geometry, l.Model, EnsureMaterial(l.Model));
        if (l.Glass)
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (glass == null) Debug.LogWarning("Unclaimed: glass material missing at " + GlassMaterialPath);
            else
            {
                var pane = MeshObject("Visor", geometry, GlassModel, glass);
                pane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
