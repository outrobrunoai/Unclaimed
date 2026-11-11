// Modular cold chamber and isolated installation into the existing cold room.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class UnclaimedCamaraFriaAssets
{
    public const string Folder = "Assets/Modelos/CamaraFria";
    public const string Prefab = Folder + "/Prefabs/CamaraMortuaria.prefab";
    // Second cabinet: one column, three levels (C-05..C-07), same doors, rails and trays as the approved 2x2.
    public const string TowerPrefab = Folder + "/Prefabs/CamaraMortuariaTorre.prefab";
    const string TowerModel = "ModuloTorre";
    const string Group = "Camara fria - equipamentos";
    const string RootName = "Unclaimed - Necroterio (planta)";
    const string Finishes = "Assets/UnclaimedPrototype/Autopsia/";
    static readonly string[] Equipment = { "ControladorCamara", "QuadroOcupacao", "InstalacoesCamara", "UnidadeCondensadora",
                                           "CarroElevador", "SuporteBandejas", "PostoConferencia" };
    static readonly string[] Models = { "ModuloCamara", "PortaCompartimentoD", "PortaCompartimentoE", "TrilhoIntermediario", "MacaCompartimento",
                                        "ControladorCamara", "QuadroOcupacao", "InstalacoesCamara", "UnidadeCondensadora",
                                        TowerModel, "CarroElevador", "SuporteBandejas", "PostoConferencia" };
    static readonly string[] Names = { "C-01", "C-02", "C-03", "C-04" };
    static readonly string[] TowerNames = { "C-05", "C-06", "C-07" };

    [Serializable] class PivotFile
    {
        public float[] dimensoes;
        public Dictionary<string, Compartment> compartimentos;
        public Dictionary<string, PivotFile> modulos;
    }
    [Serializable] class Compartment
    {
        public int coluna, nivel;
        public string porta;
        public float[] referencia, pinoPorta, abertura;
        public float aberturaPortaGraus, cursoMaca, cursoIntermediario;
    }
    static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);

    static Texture2D Tex(string file, bool normal = false, bool srgb = false)
    {
        if (!(AssetImporter.GetAtPath(file) is TextureImporter ti)) throw new FileNotFoundException(file);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb; ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4;
        ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
    }
    static Material MaterialAt(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    [MenuItem("Tools/Unclaimed/Camara Fria/Build Prefabs")]
    public static void Build()
    {
        var pivots = JsonConvert.DeserializeObject<PivotFile>(File.ReadAllText("Blender/CamaraFria/revisao/pivos.json"));
        Directory.CreateDirectory(Folder + "/Materiais/Etiquetas");
        Directory.CreateDirectory(Folder + "/Prefabs");
        AssetDatabase.Refresh();
        foreach (string name in Models)
        {
            string path = Folder + "/" + name + ".fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) throw new FileNotFoundException(path);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
            mi.isReadable = false; mi.generateSecondaryUV = false; mi.addCollider = false; mi.SaveAndReimport();
            var m = MaterialAt(Folder + "/Materiais/" + name + ".mat");
            string t = Folder + "/Texturas/" + name;
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", Tex(t + "_BaseMap.png", srgb: true));
            m.SetTexture("_MetallicGlossMap", Tex(t + "_MetallicSmoothness.png"));
            m.SetTexture("_BumpMap", Tex(t + "_Normal.png", normal: true));
            m.SetTexture("_OcclusionMap", Tex(t + "_Occlusion.png"));
            m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", 1); m.SetFloat("_BumpScale", .75f);
            m.SetFloat("_OcclusionStrength", .65f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
        }
        BuildOccupants();
        BuildCabinet("Camara mortuaria 2x2", "ModuloCamara", pivots, Names, Prefab);
        if (pivots.modulos != null && pivots.modulos.TryGetValue(TowerModel, out var tower))
            BuildCabinet("Camara mortuaria torre 1x3", TowerModel, tower, TowerNames, TowerPrefab);
        foreach (string name in Equipment) BuildEquipment(name);
        AssetDatabase.SaveAssets();
    }
    static void BuildCabinet(string title, string model, PivotFile module, string[] names, string path)
    {
        var root = new GameObject(title);
        try
        {
            var cabinet = Model(model, "Gabinete", root.transform);
            var all = module.compartimentos.Values.ToArray();
            CabinetCollision(cabinet.transform, all, module.dimensoes[0], module.dimensoes[1]);
            foreach (string name in names) BuildCompartment(root.transform, name, module.compartimentos[name], all);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    // Occupants (Blender/CamaraFria/scripts/ocupantes.py), in the tray's frame. C-01 and C-04: sheet only (fully
    // covered, the body is never seen); C-02: Leo, body with the never-seen part reduced, sheet, splint and
    // wristband 083 (only the left arm is out of the sheet). The sheet reuses the autopsy linen so both rooms share one fabric.
    const string Occupants = Folder + "/Ocupantes";
    static Material Lit(string name, Color color, float smoothness, Texture2D map = null, bool twoSided = false)
    {
        var m = MaterialAt(Folder + "/Materiais/" + name + ".mat");
        m.shader = Shader.Find("Universal Render Pipeline/Lit"); m.shaderKeywords = new string[0];
        m.SetColor("_BaseColor", color); m.SetTexture("_BaseMap", map); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", 0);
        m.SetTexture("_BumpMap", null); m.SetTexture("_MetallicGlossMap", null); m.SetTexture("_OcclusionMap", null);
        m.SetFloat("_Cull", twoSided ? 0 : 2); m.doubleSidedGI = twoSided;
        EditorUtility.SetDirty(m); return m;
    }
    static void OccupantPart(Transform parent, string model, string name, bool shadow, params Material[] materials)
    {
        string path = Occupants + "/" + model + ".fbx";
        if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) throw new FileNotFoundException(path);
        mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importCameras = false; mi.importLights = false;
        mi.importAnimation = false; mi.isReadable = false; mi.addCollider = false;
        mi.importNormals = ModelImporterNormals.Import; mi.SaveAndReimport();
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent, false); go.name = name;
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = materials.Length == r.sharedMaterials.Length || materials.Length > 1 ? materials : Enumerable.Repeat(materials[0], r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.BlendProbes; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
    }
    static void BuildOccupants()
    {
        if (!Directory.Exists(Occupants)) return;
        var linen = AssetDatabase.LoadAssetAtPath<Material>("Assets/Modelos/Autopsia/Materiais/Lencol - corpo.mat");
        if (linen == null) throw new FileNotFoundException("Assets/Modelos/Autopsia/Materiais/Lencol - corpo.mat");
        foreach (string name in Names)
        {
            string key = "Ocupante" + name.Replace("-", "");
            if (!File.Exists(Occupants + "/" + key + "_Lencol.fbx")) continue;
            var root = new GameObject("Ocupante " + name);
            try
            {
                OccupantPart(root.transform, key + "_Lencol", "Lencol", true, linen);
                if (File.Exists(Occupants + "/" + key + "_Corpo.fbx"))
                {
                    string t = Occupants + "/Texturas/" + key;
                    var skin = Lit("Pele - " + key, Color.white, .32f, Tex(t + "_Pele_BaseMap.png", srgb: true));
                    skin.SetTexture("_BumpMap", Tex(t + "_Pele_Normal.png", normal: true)); skin.SetFloat("_BumpScale", .7f); skin.EnableKeyword("_NORMALMAP");
                    OccupantPart(root.transform, key + "_Corpo", "Corpo", true, skin);
                    OccupantPart(root.transform, key + "_Tala", "Tala", true,
                        Lit("Atadura crepe", new Color(.86f, .82f, .74f), .08f), Lit("Tala aluminio acolchoada", new Color(.30f, .42f, .55f), .35f),
                        Lit("Velcro", new Color(.10f, .11f, .12f), .12f));
                    var band = Lit("Pulseira 083", Color.white, .45f, Tex(Folder + "/Texturas/Etiquetas/Pulseira_083.png", srgb: true));
                    OccupantPart(root.transform, key + "_Pulseira", "Pulseira 083", false, band);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/Ocupante_" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    // Wall items: origin at the back face, front toward local -Z. Services have no collider (overhead/flush with the floor);
    // the condensing unit stands outside the building, its box only keeps it consistent with other props.
    static void BuildEquipment(string name)
    {
        var root = new GameObject(name);
        try
        {
            var model = Model(name, "Modelo", root.transform);
            if (name != "InstalacoesCamara")
            {
                var b = new Bounds(); bool first = true;
                foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mb = f.sharedMesh.bounds; var m = root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2 - Vector3.one));
                        if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                    }
                }
                Box(root, b.center, b.size);
            }
            if (name == "QuadroOcupacao") Label(root.transform, "QuadroOcupacao", new Vector3(0, 0, -.0125f), new Vector2(.60f, .45f));
            if (name == "PostoConferencia")
            {
                // Blank printed form on the clipboard (lying on the shelf) and the label of the tag dispenser.
                var form = Label(root.transform, "FormularioConferencia", new Vector3(-.085f, .0085f, -.150f), new Vector2(.210f, .297f));
                if (form != null) form.transform.localRotation = Quaternion.LookRotation(Vector3.down, new Vector3(-.99863f, 0, .052336f));
                Label(root.transform, "RotuloEtiquetas", new Vector3(.190f, .067f, -.1232f), new Vector2(.180f, .060f));
            }
            if (name == "ControladorCamara")
            {
                var lcd = Label(root.transform, "VisorControlador", new Vector3(0, .0155f, -.0775f), new Vector2(.070f, .039f));
                if (lcd != null)
                {
                    // Backlit LCD: emission from its own face so it reads in the dark room without adding a light.
                    var m = lcd.GetComponent<MeshRenderer>().sharedMaterial;
                    m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap")); m.SetColor("_EmissionColor", Color.white * .55f);
                    m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + name + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
    }
    static GameObject Model(string model, string name, Transform parent)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + model + ".fbx"), parent, false);
        go.name = name; go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materiais/" + model + ".mat");
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.BlendProbes; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
        return go;
    }
    static void Box(GameObject go, Vector3 center, Vector3 size)
    {
        var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size;
    }
    static void CabinetCollision(Transform cabinet, Compartment[] compartments, float width, float height)
    {
        float w = width / 2, h = height;
        var parent = Child("Colisao", cabinet).transform;
        void Slab(string name, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            Box(Child(name, parent), new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
                new Vector3(x1 - x0, y1 - y0, z1 - z0));
        }
        Slab("Fundo", -w, w, .10f, .18f, .08f, 2.33f);
        Slab("Teto", -w, w, h - .08f, h, .08f, 2.33f);
        Slab("Lateral esquerda", -w, -w + .08f, .10f, h, 0, 2.33f);
        Slab("Lateral direita", w - .08f, w, .10f, h, 0, 2.33f);
        Slab("Traseira", -w, w, .10f, h, 2.25f, 2.33f);
        if (compartments.Select(c => c.coluna).Distinct().Count() > 1) Slab("Divisoria central", -.05f, .05f, .10f, h, .08f, 2.33f);
        // Stainless partition under every compartment that has another one below it (0.555-0.585 m above the lower rail).
        foreach (var c in compartments.Where(c => compartments.Any(o => o.coluna == c.coluna && o.nivel > c.nivel)))
            Slab("Nivel " + c.coluna + "-" + c.nivel, Mathf.Max(-w + .08f, c.referencia[0] - .5325f), Mathf.Min(w - .08f, c.referencia[0] + .5325f),
                c.referencia[1] + .555f, c.referencia[1] + .585f, .08f, 2.09f);
        Slab("Base", -w + .065f, w - .065f, 0, .10f, .05f, 2.28f);
        // Partition the front plane at every opening edge; only solid cells receive boxes.
        float[] xs = compartments.SelectMany(c => new[] { c.abertura[0], c.abertura[2] }).Concat(new[] { -w, w }).Distinct().OrderBy(x => x).ToArray();
        float[] ys = compartments.SelectMany(c => new[] { c.abertura[1], c.abertura[3] }).Concat(new[] { .10f, h }).Distinct().OrderBy(y => y).ToArray();
        for (int x = 0; x < xs.Length - 1; x++)
            for (int y = 0; y < ys.Length - 1; y++)
            {
                float cx = (xs[x] + xs[x + 1]) / 2, cy = (ys[y] + ys[y + 1]) / 2;
                if (compartments.Any(c => cx > c.abertura[0] && cx < c.abertura[2] && cy > c.abertura[1] && cy < c.abertura[3])) continue;
                Slab("Frente " + x + "-" + y, xs[x], xs[x + 1], ys[y], ys[y + 1], 0, .08f);
            }
    }
    static PecaDeMovel Moving(GameObject go, Transform root, bool door, float travel, string name)
    {
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var p = go.AddComponent<PecaDeMovel>(); p.movel = root;
        p.tipo = door ? PecaDeMovel.TipoDePeca.Porta : PecaDeMovel.TipoDePeca.Gaveta;
        p.eixo = door ? Vector3.up : Vector3.back; p.curso = travel; p.duracao = door ? .9f : 1.8f;
        if (door) p.passoDeTeste = 1f;
        p.textoAbrir = (door ? "Abrir porta " : "Puxar maca ") + name;
        p.textoFechar = (door ? "Fechar porta " : "Recolher maca ") + name;
        return p;
    }
    static void BuildCompartment(Transform root, string name, Compartment c, Compartment[] all)
    {
        var compartment = Child(name, root).transform; compartment.localPosition = V(c.referencia);
        var door = Model(c.porta, "Porta", compartment);
        door.transform.localPosition = V(c.pinoPorta) - V(c.referencia);
        var p = Moving(door, root, true, c.aberturaPortaGraus, name);
        float side = c.porta == "PortaCompartimentoD" ? -1 : 1;
        Box(door, new Vector3(side * .412f, .275f, .008f), new Vector3(.80f, .55f, .070f));
        var rail = Model("TrilhoIntermediario", "Trilho intermediario", compartment);
        var tray = Model("MacaCompartimento", "Maca", compartment);
        var movingTray = Moving(tray, root, false, c.cursoMaca, name);
        // Handles included (they reach 7.5 cm past the pan at both ends): a closed door stops the tray at once.
        Box(tray, new Vector3(0, .075f, 1.075f), new Vector3(.62f, .16f, 2.03f));
        var follower = new SerializedObject(rail.AddComponent<SeguidorDeCurso>());
        follower.FindProperty("fonte").objectReferenceValue = movingTray;
        follower.FindProperty("direcao").vector3Value = Vector3.back;
        follower.FindProperty("fator").floatValue = c.cursoIntermediario / c.cursoMaca;
        follower.ApplyModifiedPropertiesWithoutUndo();
        var lightObject = Child("Luz interna", compartment); lightObject.transform.localPosition = new Vector3(0, .20f, .35f);
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Point;
        light.color = new Color(1, .96f, .9f); light.intensity = .22f; light.range = 1.1f;   // service light: reads inside, does not light up the open doors light.shadows = LightShadows.None; light.enabled = false;
        var switcher = new SerializedObject(lightObject.AddComponent<LuzDeCompartimento>());
        switcher.FindProperty("porta").objectReferenceValue = p; switcher.FindProperty("luz").objectReferenceValue = light;
        switcher.ApplyModifiedPropertiesWithoutUndo();
        var occupant = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/Ocupante_" + name + ".prefab");
        if (occupant != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(occupant, tray.transform);
            go.name = "Ocupante"; go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        }
        // Position plate above the door: on the top cornice for the highest level, on the front panel for the others.
        bool top = !all.Any(o => o.coluna == c.coluna && o.nivel > c.nivel);
        Label(root, "Posicao_" + name, new Vector3(c.referencia[0], c.referencia[1] + (top ? .57f : .585f), top ? -.0095f : -.0015f), new Vector2(.12f, .05f));
        Label(door.transform, name == "C-01" || name == "C-04" ? "Ficha_" + name : "Ficha_vazia", new Vector3(side * .412f, .44f, -.0320f), new Vector2(.164f, .104f));
        if (name == "C-03") Label(door.transform, "Interdicao_C-03", new Vector3(.33f, .20f, -.0285f), new Vector2(.30f, .20f));
        // Hand polish around the latch pull. Strength follows reach only (waist/chest height is used most), never occupancy.
        float y = c.referencia[1];
        Wear(door.transform, "MarcasPuxador", "Marcas de uso - puxador", new Vector3(side * .66f, .30f, -.0276f), Quaternion.identity,
            new Vector2(.30f, .30f) * (side < 0 ? new Vector2(-1, 1) : Vector2.one), y > 1.5f ? .35f : y > .8f ? .9f : .6f);
    }
    // Alpha-blended wear quad (Blender/CamaraFria/scripts/impressos.py, Texturas/Desgaste); one material per strength.
    static GameObject Wear(Transform parent, string file, string name, Vector3 position, Quaternion rotation, Vector2 size, float strength)
    {
        string path = Folder + "/Texturas/Desgaste/" + file + ".png";
        if (!File.Exists(path)) { Debug.LogWarning("Missing cold chamber wear texture: " + path); return null; }
        Directory.CreateDirectory(Folder + "/Materiais/Desgaste");
        var m = MaterialAt(Folder + "/Materiais/Desgaste/" + file + " " + Mathf.RoundToInt(strength * 100) + ".mat");
        m.shaderKeywords = new string[0];
        m.SetTexture("_BaseMap", Tex(path, srgb: true)); m.SetColor("_BaseColor", new Color(1, 1, 1, strength));
        // Diffuse tint only: no specular or reflection of its own, otherwise the whole quad shows as a glossy rectangle.
        m.SetFloat("_Smoothness", 0); m.SetFloat("_Metallic", 0);
        m.SetFloat("_BlendModePreserveSpecular", 0); m.SetFloat("_SpecularHighlights", 0); m.SetFloat("_EnvironmentReflections", 0);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_AlphaClip", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetShaderPassEnabled("ShadowCaster", false);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m);
        var go = Child(name, parent); go.transform.localPosition = position; go.transform.localRotation = rotation;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }
    static GameObject Label(Transform parent, string file, Vector3 position, Vector2 size)
    {
        string path = Folder + "/Texturas/Etiquetas/" + file + ".png";
        if (!File.Exists(path)) { Debug.LogWarning("Missing cold chamber label: " + path); return null; }
        var m = MaterialAt(Folder + "/Materiais/Etiquetas/" + file + ".mat");
        m.SetTexture("_BaseMap", Tex(path, srgb: true)); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.SetFloat("_Smoothness", .3f);
        m.EnableKeyword("_ALPHATEST_ON"); EditorUtility.SetDirty(m);
        var go = Child(file, parent); go.transform.localPosition = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/PrototipoNecroterio.unity")
            throw new InvalidOperationException("Open PrototipoNecroterio in Edit Mode.");
    }
    static Transform Sub(Transform parent, string name)
    {
        var found = parent == null ? GameObject.Find(name)?.transform : parent.Find(name);
        if (found != null) return found;
        var go = Child(name, parent); Undo.RegisterCreatedObjectUndo(go, "Create " + name); return go.transform;
    }
    static void Remove(Transform parent, string name)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }
    [MenuItem("Tools/Unclaimed/Camara Fria/Update Camara Fria")]
    public static void Update()
    {
        RequireScene();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) == null) throw new FileNotFoundException(Prefab);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TowerPrefab) == null) throw new FileNotFoundException(TowerPrefab);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Update camara fria");
        try
        {
            var root = Sub(null, RootName);
            var room = Sub(Sub(root, "Ambientes"), "Camara fria");
            InstallRoom(room);
            for (int i = 1; i <= 6; i++)
            {
                Remove(root.Find("Moveis"), "Gaveta mortuaria " + i);
                Remove(root.Find("Moveis"), "Corpo provisório " + i);
                Remove(root.Find("Marcadores"), "EXAMINAR - corpo " + i);
            }
            Remove(root.Find("Moveis"), "Maca da camara fria");
            BakeReflection();
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
        }
        finally { Undo.CollapseUndoOperations(undo); }
    }

    // The room probe sees the finishes above; Custom mode with its own texture, like the doorway probes, because a probe
    // baked on its own outside a full lighting bake is not kept in the scene's LightingData.
    const string Reflection = Folder + "/Reflexo Camara fria.exr";
    [MenuItem("Tools/Unclaimed/Camara Fria/Bake Camara Fria Reflection")]
    public static void BakeReflection()
    {
        RequireScene();
        var probe = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include).FirstOrDefault(p => p.name == "Reflexo - Camara fria");
        if (probe == null) { Debug.LogWarning("Unclaimed: cold room reflection probe not found."); return; }
        if (!Lightmapping.BakeReflectionProbe(probe, Reflection)) { Debug.LogWarning("Unclaimed: could not bake the cold room reflection.", probe); return; }
        AssetDatabase.ImportAsset(Reflection);
        Undo.RecordObject(probe, "Bake cold room reflection");
        probe.mode = ReflectionProbeMode.Custom;
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(Reflection);
    }
    public static void InstallGenerated(Transform room)
    {
        if (room != null && AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null) InstallRoom(room);
    }
    static GameObject Place(string path, Transform parent, Vector3 position, float yaw, bool local = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("Missing cold room equipment: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Place cold room equipment");
        if (local) { go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); }
        else go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        return go;
    }
    static void InstallRoom(Transform room)
    {
        Remove(room, Group);
        var parent = Child(Group, room).transform; Undo.RegisterCreatedObjectUndo(parent.gameObject, "Install cold room");
        // East wall bank, fronts on x = 12.565 facing west: the approved 2x2 in the south-east corner and, 30 mm to its
        // north, the three-level tower (cabinet-local x = -1.7775). Wall items follow in the 2x2's local frame.
        var cabinet = Place(Prefab, parent, new Vector3(12.565f, 0, -2.195f), 90).transform;
        var tower = Place(TowerPrefab, parent, new Vector3(12.565f, 0, -.4175f), 90)?.transform;
        var markers = Sub(room.parent.parent, "Marcadores");
        foreach (string name in Names.Concat(TowerNames))
        {
            Remove(markers, "EXAMINAR - " + name);
            var unit = cabinet.Find(name) ?? tower?.Find(name);
            if (unit == null) continue;
            var marker = Child("EXAMINAR - " + name, markers); Undo.RegisterCreatedObjectUndo(marker, "Create marker");
            marker.transform.position = unit.position + Vector3.up * .35f + cabinet.TransformDirection(Vector3.back) * .5f;
        }
        Place(Folder + "/Prefabs/ControladorCamara.prefab", cabinet, new Vector3(-2.945f, 1.45f, 2.33f), 0, true);
        Place(Folder + "/Prefabs/QuadroOcupacao.prefab", cabinet, new Vector3(-3.845f, 1.50f, 2.33f), 0, true);
        // Checking post right under the occupancy board: writing shelf, clipboard and blank identification tags.
        Place(Folder + "/Prefabs/PostoConferencia.prefab", cabinet, new Vector3(-3.845f, 1.00f, 2.33f), 0, true);
        Place(Folder + "/Prefabs/InstalacoesCamara.prefab", cabinet, Vector3.zero, 0, true);
        Place(Folder + "/Prefabs/UnidadeCondensadora.prefab", cabinet, new Vector3(0, 1.95f, 2.54f), 180, true);
        // One lifting trolley serves the three levels (it replaces the plain transfer stretcher, which could not reach
        // C-05 and repeated the receiving room's equipment). Parked along the east wall, push handle towards the room.
        Place(Folder + "/Prefabs/CarroElevador.prefab", parent, new Vector3(14.43f, 0, 3.35f), 0);
        // Parking rack for removable trays on the south wall, beside the manoeuvring area in front of the cabinets
        // (its east end, x = 9.83, stays clear of the trolley lined up 0.5 m short of the C-02/C-04 column, x = 10.05).
        Place(Folder + "/Prefabs/SuporteBandejas.prefab", parent, new Vector3(8.80f, 0, -3.397f), 180);
        Place(UnclaimedRecebimentoAssets.PrefabPath("SuporteEPI"), parent, new Vector3(11.50f, 1.22f, 5.115f), 0);
        DressRoom(parent);
        // Transfer wear on the floor where trays come out onto the trolley: fine scratches along the tray axis.
        var floor = Quaternion.Euler(90, 0, 0);
        Wear(parent, "RiscosTransferencia", "Riscos de transferencia - 2x2", new Vector3(11.30f, .0045f, -2.20f), floor, new Vector2(2.4f, 1.2f), .8f);
        Wear(parent, "RiscosTransferencia", "Riscos de transferencia - torre", new Vector3(11.36f, .0045f, -.42f), floor, new Vector2(-2.4f, 1.0f), .55f);
    }
    static void DressRoom(Transform parent)
    {
        var tile = AssetDatabase.LoadAssetAtPath<Material>(Finishes + "Azulejo.mat");
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Finishes + "Pintura.mat");
        void Wall(string id, Vector3 origin, Vector3 along, Vector3 normal, float width, float from = 0)
        {
            if (from < 2.1f) Panel(parent, id + " - azulejo", origin + Vector3.up * from, along, Vector3.up, width, 2.1f - from, normal, tile);
            float low = Mathf.Max(2.1f, from);
            Panel(parent, id + " - pintura", origin + Vector3.up * low, along, Vector3.up, width, 3.2f - low, normal, paint);
        }
        Wall("Oeste sul", new Vector3(7.603f, 0, -3.4f), Vector3.forward, Vector3.right, 2.9f);
        Wall("Oeste norte", new Vector3(7.603f, 0, 1), Vector3.forward, Vector3.right, 4.15f);
        Wall("Sobre porta oeste", new Vector3(7.603f, 0, -.5f), Vector3.forward, Vector3.right, 1.5f, 2.2f);
        Wall("Norte oeste", new Vector3(7.6f, 0, 5.147f), Vector3.right, Vector3.back, 1.4f);
        Wall("Norte leste", new Vector3(10.5f, 0, 5.147f), Vector3.right, Vector3.back, 4.4f);
        Wall("Sobre porta norte", new Vector3(9, 0, 5.147f), Vector3.right, Vector3.back, 1.5f, 2.2f);
        Wall("Leste", new Vector3(14.897f, 0, -3.4f), Vector3.forward, Vector3.left, 8.55f);
        Wall("Sul", new Vector3(7.6f, 0, -3.397f), Vector3.right, Vector3.forward, 7.3f);
        Panel(parent, "Piso acabado", new Vector3(7.6f, .003f, -3.4f), Vector3.right, Vector3.forward, 7.3f, 8.55f, Vector3.up,
            AssetDatabase.LoadAssetAtPath<Material>(Finishes + "Piso porcelanato.mat"));
        Panel(parent, "Forro acabado", new Vector3(7.6f, 3.197f, -3.4f), Vector3.right, Vector3.forward, 7.3f, 8.55f, Vector3.down,
            AssetDatabase.LoadAssetAtPath<Material>(Finishes + "Forro mineral.mat"));
        void Rail(string name, Vector3 p, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = p; go.transform.localScale = size; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Finishes + "Metal pintado.mat");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);
            Undo.RegisterCreatedObjectUndo(go, "Cold room protection");
        }
        // Bump rails only where wheeled equipment runs along a wall: south wall between the tray rack and the cabinets,
        // west wall beside the route between the doors, east wall behind the parked lifting trolley (frame and table).
        Rail("Protetor sul", new Vector3(11.25f, .79f, -3.382f), new Vector3(2.5f, .13f, .035f));
        Rail("Protetor oeste", new Vector3(7.618f, .79f, 2.9f), new Vector3(.035f, .13f, 3.6f));
        Rail("Protetor leste", new Vector3(14.879f, .79f, 3.55f), new Vector3(.035f, .13f, 3.0f));
        Rail("Protetor leste - rodizios", new Vector3(14.879f, .24f, 3.55f), new Vector3(.035f, .13f, 3.0f));
    }
    static void Panel(Transform parent, string id, Vector3 o, Vector3 u, Vector3 v, float w, float h, Vector3 normal, Material mat)
    {
        Vector3[] vs = { o, o + u * w, o + u * w + v * h, o + v * h };
        int[] triangles = Vector3.Dot(Vector3.Cross(u, v), normal) > 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
        Vector3 n = parent.InverseTransformDirection(normal);
        var mesh = new Mesh { name = id, vertices = vs.Select(parent.InverseTransformPoint).ToArray(), triangles = triangles,
            normals = new[] { n, n, n, n }, uv = vs.Select(p => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v))).ToArray() };
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        Directory.CreateDirectory(Folder + "/Revestimentos");
        AssetDatabase.Refresh();
        string path = Folder + "/Revestimentos/" + id + ".asset";
        var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (stored == null) { AssetDatabase.CreateAsset(mesh, path); stored = mesh; }
        else { EditorUtility.CopySerialized(mesh, stored); EditorUtility.SetDirty(stored); Object.DestroyImmediate(mesh); }
        var go = Child(id, parent); go.AddComponent<MeshFilter>().sharedMesh = stored;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
        // Seen by the room's reflection probe (otherwise the stainless reflects the bare walls behind the finish);
        // not lightmap static, so the finishes stay on light probes without a new bake.
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);
        Undo.RegisterCreatedObjectUndo(go, "Cold room finish");
    }
}
