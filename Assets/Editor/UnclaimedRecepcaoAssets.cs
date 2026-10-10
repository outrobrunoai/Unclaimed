// Builds the URP materials and prefabs of the reception furniture modelled in Blender
// (source and scripts in Blender/Recepcao; run scripts/gerar_tudo.py there first).
// Run Tools > Unclaimed > Build Recepcao Prefabs after the FBX files or baked textures change.
// Each asset has one mesh and one material on a baked atlas (BaseMap, MetallicSmoothness with
// smoothness in alpha, Normal, Occlusion); its prefab carries simple box colliders (none for the desk
// kit and the register book, which rest on the counter's collider).
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

public static class UnclaimedRecepcaoAssets
{
    public const string Folder = "Assets/Modelos/Recepcao";

    private readonly struct Asset
    {
        public readonly string File, Prefab;
        public readonly int Atlas;
        public readonly Bounds[] Colliders; // prefab-local, metres; null = one box around the mesh

        public Asset(string file, string prefab, int atlas, params Bounds[] colliders)
        {
            File = file; Prefab = prefab; Atlas = atlas; Colliders = colliders;
        }
    }

    // Prefab-local frame: origin on the floor at the centre of the reserved footprint, front towards -Z.
    private static readonly Asset[] Assets =
    {
        new Asset("BalcaoRecepcao", "Balcao de recepcao", 2048,
            new Bounds(new Vector3(0f, 0.55f, -0.24f), new Vector3(2.5f, 1.1f, 0.72f))),
        new Asset("CadeiraEspera", "Cadeira de espera", 1024,
            new Bounds(new Vector3(0f, 0.42f, 0.015f), new Vector3(0.47f, 0.84f, 0.48f))),
        new Asset("CadeiraAtendente", "Cadeira do atendente", 1024,
            new Bounds(new Vector3(0f, 0.51f, 0f), new Vector3(0.62f, 1.02f, 0.62f))),
        new Asset("ArmarioApoio", "Armario de apoio", 1024,
            new Bounds(new Vector3(0f, 0.475f, 0f), new Vector3(0.9f, 0.95f, 0.45f))),
        new Asset("LongarinaEspera", "Longarina de espera", 1024, null),
        new Asset("EquipamentosBalcao", "Equipamentos do balcao", 2048),
        new Asset("LivroRegistro", "Livro de registro", 512),
        new Asset("Bebedouro", "Bebedouro", 1024, null),
        new Asset("Lixeira", "Lixeira", 512, null),
        // Baked parts of the composite prefabs below (no prefab of their own).
        new Asset("LuminariaSobrepor", null, 1024),
        new Asset("LuminariaExterna", null, 512),
        new Asset("PortaVidroFixos", null, 512),
        new Asset("PortaVidroFolha", null, 1024),
    };

    // Composite prefabs: surface ceiling fitting, entrance wall lamp, glass entrance door.
    public const string FittingPrefab = "Luminaria de sobrepor", WallLampPrefab = "Arandela externa",
        DoorPrefab = "Porta de vidro - entrada";
    // Light parts of the fittings: diffuser (slot 0) and emissive tubes / LED plate (slot 1).
    public const string LightPartName = "Difusor e lampadas";
    public const string DiffuserLit = "Difusor prismatico aceso", DiffuserOff = "Difusor prismatico apagado";
    private const string Textures = "Assets/UnclaimedPrototype/Recepcao/Texturas/";
    private const string TubeMaterials = "Assets/UnclaimedPrototype/Autopsia/";
    // Door (Blender PORTA, mapped to Unity: x, z -> y, y -> z): left pivot axis and leaf size. The leaves
    // carry FolhaDePorta (Assets/Scripts): open/close by interaction, obstruction test, initial state.
    public const float DoorPivotX = -0.909f, DoorOpenAngle = 88f;
    private static readonly Vector3 LeafCenter = new Vector3(0.426f, 1.09f, 0f), LeafSize = new Vector3(0.968f, 2.16f, 0.05f);

    // Lit monitor screen of the desk kit (prefab-local; Blender KIT dimensions, screen tilted back 6 degrees).
    private const string ScreenTexture = "Assets/UnclaimedPrototype/Recepcao/Texturas/Tela_Sistema.png";
    private static readonly Vector3 ScreenCenter = new Vector3(0.15f, 1.0085f, -0.196f);
    private static readonly Vector3 ScreenNormal = new Vector3(0f, Mathf.Sin(6f * Mathf.Deg2Rad), Mathf.Cos(6f * Mathf.Deg2Rad));
    private static readonly Vector2 ScreenSize = new Vector2(0.506f, 0.293f);

    public static string PrefabPath(string prefab) => Folder + "/" + prefab + ".prefab";

    // Material of a light part by fitting state (0 lit, 1 aged tube, 2 burnt out): diffuser and tube.
    public static Material[] LightMaterials(int state)
    {
        string tube = state == 0 ? "Difusor aceso" : state == 1 ? "Difusor gasto" : "Difusor apagado";
        return new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materiais/" + (state == 2 ? DiffuserOff : DiffuserLit) + ".mat"),
            AssetDatabase.LoadAssetAtPath<Material>(TubeMaterials + tube + ".mat"),
        };
    }

    [MenuItem("Tools/Unclaimed/Build Recepcao Prefabs")]
    public static void BuildAll()
    {
        foreach (Asset a in Assets) Build(a);
        BuildFittings();
        BuildDoor();
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: reception prefabs rebuilt in " + Folder + ".");
    }

    private static void Build(Asset a)
    {
        string fbx = Folder + "/" + a.File + ".fbx", tex = Folder + "/Texturas/" + a.File;
        if (AssetImporter.GetAtPath(fbx) is ModelImporter model)
        {
            model.globalScale = 1f;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importAnimation = false;
            model.importCameras = model.importLights = false;
            model.importBlendShapes = false;
            model.meshCompression = ModelImporterMeshCompression.Off;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.SaveAndReimport();
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fbx);
        if (mesh == null)
        {
            Debug.LogWarning("Unclaimed: missing " + fbx + "; run Blender/Recepcao/scripts/gerar_tudo.py first.");
            return;
        }

        string matFolder = Folder + "/Materiais", matPath = matFolder + "/" + (a.Prefab ?? a.File) + ".mat";
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(Folder, "Materiais");
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = a.Prefab ?? a.File };
            AssetDatabase.CreateAsset(material, matPath);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", Tex(tex + "_BaseMap.png", a.Atlas, srgb: true));
        material.SetTexture("_MetallicGlossMap", Tex(tex + "_MetallicSmoothness.png", a.Atlas, srgb: false));
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.SetTexture("_BumpMap", Tex(tex + "_Normal.png", a.Atlas, srgb: false, normal: true));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_OcclusionMap", Tex(tex + "_Occlusion.png", a.Atlas, srgb: false));
        material.SetFloat("_OcclusionStrength", 1f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);

        if (a.Prefab == null) return;
        var root = new GameObject(a.Prefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            foreach (Bounds b in a.Colliders ?? new[] { mesh.bounds })
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            if (a.File == "EquipamentosBalcao") AddScreen(root.transform, matFolder);
            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(a.Prefab));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Unlit quad over the baked (dark) screen so the monitor reads as switched on.
    private static void AddScreen(Transform root, string matFolder)
    {
        string path = matFolder + "/Tela do sistema.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Tela do sistema" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ScreenTexture));
        material.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
        EditorUtility.SetDirty(material);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = "Tela ligada";
        quad.transform.SetParent(root, false);
        // Unity's quad faces -Z, so its -Z axis is turned to the screen's normal.
        quad.transform.localPosition = ScreenCenter + ScreenNormal * 0.0012f;
        quad.transform.localRotation = Quaternion.LookRotation(-ScreenNormal, Vector3.up);
        quad.transform.localScale = new Vector3(ScreenSize.x, ScreenSize.y, 1f);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material BakedMaterial(string file) =>
        AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materiais/" + file + ".mat");

    private static Mesh MeshOf(string file) => AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + file + ".fbx");

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return obj;
    }

    // URP Lit material created or updated in Materiais; transparent ones keep specular highlights and
    // reflections (glass, acrylic).
    private static Material LitMaterial(string name, Color color, float smoothness, bool transparent,
        Texture2D baseMap = null, Texture2D normalMap = null, Texture2D glossMap = null, Color? emission = null, Vector2? tiling = null)
    {
        string path = Folder + "/Materiais/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", baseMap);
        m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        m.SetTexture("_BumpMap", normalMap);
        m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_MetallicGlossMap", glossMap);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_BlendModePreserveSpecular", 1f);
        m.SetColor("_EmissionColor", emission ?? Color.black);
        m.globalIlluminationFlags = emission.HasValue ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        if (emission.HasValue) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    // Ceiling fitting and entrance lamp: baked body on the root, light parts in a child whose materials
    // the map generator sets per fitting state. The lights themselves stay in the generator.
    private static void BuildFittings()
    {
        var prism = Tex(Textures + "Difusor_Prismatico_Normal.png", 128, srgb: false, normal: true);
        // Milky prismatic acrylic: lets the tubes show as brighter bands and glows a little itself.
        var tiling = new Vector2(38f, 7.5f);   // 32 mm tile over the 1.25 x 0.24 m diffuser
        LitMaterial(DiffuserLit, new Color(0.94f, 0.95f, 0.95f, 0.8f), 0.55f, true, null, prism, null, new Color(0.62f, 0.61f, 0.58f), tiling);
        LitMaterial(DiffuserOff, new Color(0.86f, 0.87f, 0.87f, 0.85f), 0.55f, true, null, prism, null, null, tiling);
        foreach (var (prefab, body, light) in new[] { (FittingPrefab, "LuminariaSobrepor", "LuminariaSobrepor_Luz"),
                                                    (WallLampPrefab, "LuminariaExterna", "LuminariaExterna_Luz") })
        {
            Mesh bodyMesh = MeshOf(body), lightMesh = MeshOf(light);
            if (bodyMesh == null || lightMesh == null) { Debug.LogWarning("Unclaimed: missing " + body + " meshes."); continue; }
            var root = new GameObject(prefab);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial(body);
                MeshObject(LightPartName, root.transform, lightMesh, LightMaterials(0));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(prefab));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    // Glass door: fixed parts on the root; each leaf is a pivot transform on its pivot axis carrying the
    // fittings, the glass and its collider (the right leaf mirrors the left one inside a child, so the
    // collider never has a negative scale). The prefab is saved closed; FolhaDePorta applies the initial
    // state (open, on the floor stops) on Awake and the generator applies it in the Editor.
    private static void BuildDoor()
    {
        Mesh fixedMesh = MeshOf("PortaVidroFixos"), leafMesh = MeshOf("PortaVidroFolha"), glassMesh = MeshOf("PortaVidroVidro");
        if (fixedMesh == null || leafMesh == null || glassMesh == null) { Debug.LogWarning("Unclaimed: missing door meshes."); return; }
        Material glass = LitMaterial("Vidro temperado", Color.white, 1f, true,
            Tex(Textures + "Vidro_Porta_Base.png", 1024, srgb: true), null, Tex(Textures + "Vidro_Porta_MetallicSmoothness.png", 1024, srgb: false));
        var root = new GameObject(DoorPrefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = fixedMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial("PortaVidroFixos");
            foreach (var (name, side) in new[] { ("Folha esquerda", 1f), ("Folha direita", -1f) })
            {
                var leaf = new GameObject(name).transform;
                leaf.SetParent(root.transform, false);
                leaf.localPosition = new Vector3(side * DoorPivotX, 0f, 0f);
                var box = leaf.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(side * LeafCenter.x, LeafCenter.y, 0f);
                box.size = LeafSize;
                // Moving leaf: kinematic body, opens outwards (left +, right -) up to the floor stops.
                var body = leaf.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var audio = leaf.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.minDistance = 1f;
                audio.maxDistance = 15f;
                var door = leaf.gameObject.AddComponent<FolhaDePorta>();
                door.sentido = side > 0 ? 1 : -1;
                door.anguloMaximo = DoorOpenAngle;
                door.duracao = 1f;
                door.alcance = 2f;
                door.iniciarAberta = true;
                door.aberturaInicial = 1f;
                door.fonte = audio;   // clips: none in the project yet
                Transform geometry = leaf;
                if (side < 0)
                {
                    geometry = new GameObject("Espelhada").transform;
                    geometry.SetParent(leaf, false);
                    geometry.localScale = new Vector3(-1f, 1f, 1f);
                }
                MeshObject("Ferragens", geometry, leafMesh, BakedMaterial("PortaVidroFolha"));
                var pane = MeshObject("Vidro", geometry, glassMesh, glass);
                pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(DoorPrefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Texture2D Tex(string path, int size, bool srgb, bool normal = false)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != size)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = size;
                importer.SaveAndReimport();
            }
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) Debug.LogWarning("Unclaimed: missing texture " + path);
        return texture;
    }
}
