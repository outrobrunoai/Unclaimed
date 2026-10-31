using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

// Rest / changing room kit (Blender/Descanso/scripts/gerar.py -> Assets/Modelos/Descanso). Prefabs face -Z, origin on the
// floor (wall-hung pieces: on the wall plane). The steel locker module carries two separate doors hinged on their own
// pivots and driven by the existing PecaDeMovel (Porta); the lockers keep their interior free for belongings.
public static class UnclaimedDescansoAssets
{
    public const string Folder = "Assets/Modelos/Descanso";
    const string Materials = Folder + "/Materiais";
    const string Prints = "Assets/UnclaimedPrototype/Descanso";
    public const float DoorWidth = .326f, HingeY = -.239f;
    // Hinge axis of each locker in the module frame (left edge of the door as seen from the front).
    public static readonly float[] HingeX = { -.328f, .002f };

    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";

    static readonly (string file, string prefab)[] Simple =
    {
        ("Banco", "Banco do vestiario"), ("Cabideiro", "Cabideiro de parede"), ("Jaleco", "Jaleco"), ("JalecoV2", "Jaleco v2"), ("CapaDeChuva", "Capa de chuva"),
        ("Mochila", "Mochila"), ("Tamancos", "Tamancos"), ("Botas", "Botas"), ("Mesa", "Mesa de descanso"), ("BalcaoCopa", "Balcao da copa"),
        ("Frigobar", "Frigobar"), ("Marmita", "Marmita"), ("ItensCopa", "Itens da copa"), ("QuadroAvisos", "Quadro de avisos do descanso"),
        ("Cadeado", "Cadeado"),
    };

    [MenuItem("Tools/Unclaimed/Descanso/Build Prefabs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materiais");
        if (!AssetDatabase.IsValidFolder(Folder + "/Prefabs")) AssetDatabase.CreateFolder(Folder, "Prefabs");
        foreach (var (file, prefab) in Simple) BuildSimple(file, prefab);
        BuildLocker();
        BuildClock();
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: rest-room prefabs built in " + Folder + "/Prefabs.");
    }

    static Mesh Import(string file, out Material material, bool baked = true)
    {
        material = null;
        string path = Folder + "/" + file + ".fbx", prefix = Folder + "/Texturas/" + file;
        if (baked) foreach (string suffix in new[] { "_BaseMap", "_MetallicSmoothness", "_Normal", "_Occlusion" })
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
        if (mesh == null || !baked) return mesh;
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

    static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material m)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static void BuildSimple(string file, string prefab)
    {
        Mesh mesh = Import(file, out Material mat);
        if (mesh == null) return;
        var root = new GameObject(prefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var b = mesh.bounds;
            switch (file)
            {
                case "Banco":
                    Box(root, 0, .433f, 0, 1.6f, .034f, .34f);
                    foreach (float x in new[] { -.66f, .66f }) Box(root, x, .22f, 0, .04f, .43f, .30f);
                    break;
                case "Mesa":
                    Box(root, 0, .725f, 0, .9f, .03f, .9f);
                    foreach (float x in new[] { -.39f, .39f }) foreach (float z in new[] { -.39f, .39f }) Box(root, x, .36f, z, .04f, .72f, .04f);
                    break;
                case "BalcaoCopa": case "Frigobar": case "Cabideiro": Box(root, b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z); break;
                default: break;   // belongings, notice board and small items carry no collider
            }
            root.isStatic = file == "BalcaoCopa" || file == "Mesa" || file == "Banco" || file == "Frigobar" || file == "Cabideiro";
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(prefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void BuildLocker()
    {
        Mesh body = Import("Roupeiro", out Material bodyMat);
        Mesh door = Import("PortaRoupeiro", out Material doorMat);
        if (body == null || door == null) return;
        var root = new GameObject("Roupeiro");
        try
        {
            MeshObject("Corpo", root.transform, body, bodyMat);
            // Steel shell: two sides, divider, top, back, floor and plinth; the front stays open behind the doors.
            foreach (float x in new[] { -.327f, .327f }) Box(root, x, 1.01f, 0, .012f, 1.82f, .46f);
            Box(root, 0, 1.01f, 0, .012f, 1.82f, .44f);
            Box(root, 0, 1.914f, 0, .66f, .016f, .46f);
            Box(root, 0, 1.01f, .224f, .66f, 1.82f, .012f);
            Box(root, 0, .106f, -.01f, .64f, .012f, .44f);
            Box(root, 0, .05f, -.01f, .62f, .10f, .43f);
            for (int k = 0; k < 2; k++)
            {
                var part = new GameObject("Porta " + (k + 1)).transform;
                part.SetParent(root.transform, false);
                part.localPosition = new Vector3(HingeX[k], 0, HingeY);
                MeshObject("Malha", part, door, doorMat);
                var rb = part.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                var p = part.gameObject.AddComponent<PecaDeMovel>();
                p.movel = root.transform;
                p.fonte = part.gameObject.AddComponent<AudioSource>(); p.fonte.playOnAwake = false;
                p.fonte.spatialBlend = 1f; p.fonte.minDistance = .5f; p.fonte.maxDistance = 10f;
                p.tipo = PecaDeMovel.TipoDePeca.Porta; p.eixo = Vector3.up; p.curso = 105f; p.duracao = .7f; p.passoDeTeste = 1f;
                p.textoAbrir = "Abrir armário"; p.textoFechar = "Fechar armário";
                p.textoTrancada = "Armário trancado com cadeado do funcionário";
                Box(p.gameObject, DoorWidth / 2, 1.01f, 0, DoorWidth, 1.79f, .018f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("Roupeiro"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Dial: printed disc just behind the hands, a separate unbaked piece with its own material.
    static void BuildClock()
    {
        Mesh frame = Import("Relogio", out Material frameMat);
        Mesh dial = Import("RelogioMostrador", out _, false);
        if (frame == null || dial == null) return;
        string mp = Materials + "/Relogio mostrador.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
        m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .35f); m.SetFloat("_Metallic", 0); m.SetFloat("_Cull", 0);
        m.SetTexture("_BaseMap", Tex(Prints + "/Relogio_Mostrador.png", true, false));
        EditorUtility.SetDirty(m);
        var root = new GameObject("Relogio de parede");
        try
        {
            MeshObject("Corpo", root.transform, frame, frameMat);
            var d = MeshObject("Mostrador", root.transform, dial, m);
            d.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("Relogio de parede"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Name card printed on a card in the door's label frame (door frame: hinge axis at the origin, front at z = 0).
    public static void ApplyNameCard(Transform door, string card)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Prints + "/Etiquetas/" + card.Replace(' ', '_').Replace(".", "") + ".png");
        if (tex == null) { Debug.LogWarning("Unclaimed: missing name card " + card); return; }
        string mp = Materials + "/Etiqueta " + card.Replace(' ', '_').Replace(".", "") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
        var ti = AssetImporter.GetAtPath(Prints + "/Etiquetas/" + card.Replace(' ', '_').Replace(".", "") + ".png") as TextureImporter;
        if (ti != null && !ti.sRGBTexture) { ti.sRGBTexture = true; ti.SaveAndReimport(); }
        m.SetColor("_BaseColor", Color.white); m.SetTexture("_BaseMap", tex); m.SetFloat("_Smoothness", .25f); m.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(m);
        Transform old = door.Find("Etiqueta impressa");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Etiqueta impressa"); go.transform.SetParent(door, false);
        go.transform.localPosition = new Vector3(DoorWidth / 2 + .004f, 1.52f, -.0095f);
        go.transform.localScale = new Vector3(.078f, .026f, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = Quad();
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Mesh Quad()
    {
        const string path = Folder + "/Cartao.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh
        {
            name = "Cartao",
            vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 },
        };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
