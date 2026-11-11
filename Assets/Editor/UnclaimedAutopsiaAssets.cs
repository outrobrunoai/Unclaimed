using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnclaimedAutopsiaAssets
{
    public const string Folder = "Assets/Modelos/Autopsia";
    static readonly string[] Models = { "BancadaAutopsia", "ArmarioSuspenso", "ArmarioInstrumental", "LavatorioJoelho",
        "BalancaVisceras", "LuminariaExame", "ComandoLuminaria", "QuadroBranco" };
    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";

    static Texture2D Tex(string file, bool normal = false, bool srgb = false)
    {
        if (!(AssetImporter.GetAtPath(file) is TextureImporter ti)) throw new FileNotFoundException(file);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb; ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
    }

    static GameObject Model(string name)
    {
        string path = Folder + "/" + name + ".fbx";
        if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) throw new FileNotFoundException(path);
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
        mi.isReadable = false; mi.generateSecondaryUV = true; mi.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static Material Material(string name)
    {
        string path = Folder + "/Materiais/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    static void Dress(GameObject go, Material material, bool shadow = true)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            if (!shadow) r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.ContributeGI);
        }
    }

    // Transform every mesh-bound corner into prefab space, including imported axis transforms.
    static Bounds BoundsIn(GameObject model, Transform root)
    {
        var bounds = new Bounds(); bool first = true;
        foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
        {
            var b = f.sharedMesh.bounds;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                var p = root.InverseTransformPoint(f.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        }
        return bounds;
    }

    static GameObject BodyModel(string name)
    {
        string path = Folder + "/Corpo/" + name + ".fbx";
        if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) throw new FileNotFoundException(path);
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
        mi.generateSecondaryUV = false; mi.isReadable = false; mi.addCollider = false;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static Texture2D BodyTexture(string path, bool normal = false, bool printed = false, bool repeat = false)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) throw new FileNotFoundException(path);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal; ti.alphaIsTransparency = printed;
        ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.anisoLevel = 4;
        ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material BodyMaterial(string name, Color color, float smoothness, float metallic = 0,
        Texture2D texture = null, bool clip = false, bool transparent = false)
    {
        var m = Material(name);
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.shaderKeywords = new string[0];
        m.SetColor("_BaseColor", color); m.SetTexture("_BaseMap", texture);
        m.SetTexture("_BumpMap", null); m.SetTexture("_MetallicGlossMap", null); m.SetTexture("_OcclusionMap", null);
        m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Surface", transparent ? 1 : 0); m.SetFloat("_Blend", 0);
        m.SetFloat("_AlphaClip", clip ? 1 : 0); m.SetFloat("_Cutoff", .5f);
        m.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
        m.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        m.SetFloat("_ZWrite", transparent ? 0 : 1); m.SetFloat("_Cull", 2);
        m.doubleSidedGI = false; m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetOverrideTag("RenderType", transparent ? "Transparent" : clip ? "TransparentCutout" : "Opaque");
        m.renderQueue = (int)(transparent ? RenderQueue.Transparent : clip ? RenderQueue.AlphaTest : RenderQueue.Geometry);
        if (clip) m.EnableKeyword("_ALPHATEST_ON");
        if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetShaderPassEnabled("ShadowCaster", !transparent);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject BodyPart(Transform parent, string name, string model, bool shadow, params Material[] materials)
    {
        var go = Object.Instantiate(BodyModel(model), parent, false); go.name = name;
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        Dress(go, materials[0], shadow);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = materials;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (r is MeshRenderer mesh) mesh.receiveGI = ReceiveGI.LightProbes;
        }
        return go;
    }

    [MenuItem("Tools/Unclaimed/Autopsia/Build Body Prefabs")]
    public static void BuildBody()
    {
        Directory.CreateDirectory(Folder + "/Materiais"); Directory.CreateDirectory(Folder + "/Prefabs");
        AssetDatabase.Refresh();
        string textures = "Assets/UnclaimedPrototype/Autopsia/Texturas/";
        var skin = BodyMaterial("Pele - corpo", Color.white, .32f,
            texture: BodyTexture(Folder + "/Corpo/Texturas/CorpoOcupado_Pele_BaseMap.png"));
        skin.SetTexture("_BumpMap", BodyTexture(Folder + "/Corpo/Texturas/CorpoOcupado_Pele_Normal.png", normal: true));
        skin.SetFloat("_BumpScale", .7f); skin.EnableKeyword("_NORMALMAP");
        // Cotton weave and slight laundering mottle (sheet UVs are in metres; one 0.25 m tile).
        var linen = BodyMaterial("Lencol - corpo", Color.white, .15f,
            texture: BodyTexture(textures + "TecidoLencol.png", repeat: true));
        linen.SetTextureScale("_BaseMap", new Vector2(4, 4));
        linen.SetFloat("_Cull", 0); linen.doubleSidedGI = true;
        var sticker = BodyMaterial("Adesivo - mao", Color.white, .2f,
            texture: BodyTexture(textures + "AdesivoMao.png", printed: true), clip: true);
        var tag = BodyMaterial("Etiqueta 083", Color.white, .25f,
            texture: BodyTexture(textures + "Etiqueta083.png", printed: true), clip: true);
        var pin = BodyMaterial("Alfinete", new Color(.62f, .62f, .62f), .7f, 1);
        var sole = BodyMaterial("Marca de sola umida", new Color(.35f, .33f, .30f, .55f), .85f,
            texture: BodyTexture(textures + "Decal_MarcaSola.png", printed: true), transparent: true);
        var notice = BodyMaterial("Aviso provisorio", Color.white, .1f,
            texture: BodyTexture(textures + "AvisoProvisorio.png", printed: true));

        var occupied = new GameObject("Corpo - autopsia (ocupado)");
        try
        {
            BodyPart(occupied.transform, "Corpo", "CorpoOcupado", true, skin);
            // Never-seen region under the sheet: its shadow is never visible, so it stays out of the shadow pass.
            BodyPart(occupied.transform, "Corpo oculto", "CorpoOcupadoOculto", false, skin);
            BodyPart(occupied.transform, "Lencol", "LencolOcupado", true, linen);
            BodyPart(occupied.transform, "Adesivo", "AdesivoMao", false, sticker);
            BodyPart(occupied.transform, "Etiqueta 083", "EtiquetaMovimentacao", false, tag, pin);
            PrefabUtility.SaveAsPrefabAsset(occupied, PrefabPath("CorpoOcupado"));
        }
        finally { Object.DestroyImmediate(occupied); }

        var p08 = new GameObject("P08 - lencol caido");
        try
        {
            BodyPart(p08.transform, "Lencol", "LencolP08", true, linen);
            BodyPart(p08.transform, "Etiqueta 083", "EtiquetaP08", false, tag, pin);
            var mark = BodyPart(p08.transform, "Marca de sola", "MarcaSolaP08", false, sole);
            foreach (var r in mark.GetComponentsInChildren<Renderer>(true))
            {
                r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(p08, PrefabPath("P08_LencolCaido"));
        }
        finally { Object.DestroyImmediate(p08); }

        var sign = new GameObject("Aviso provisorio");
        try
        {
            var paper = new GameObject("Papel", typeof(MeshFilter), typeof(MeshRenderer));
            paper.transform.SetParent(sign.transform, false);
            paper.transform.localScale = new Vector3(.21f, .148f, 1);
            paper.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            Dress(paper, notice, false);
            paper.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
            PrefabUtility.SaveAsPrefabAsset(sign, PrefabPath("AvisoProvisorio"));
        }
        finally { Object.DestroyImmediate(sign); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Unclaimed/Autopsia/Build Prefabs")]
    public static void Build() => BuildSelected(Models);

    public static void BuildSelected(params string[] names)
    {
        Directory.CreateDirectory(Folder + "/Materiais"); Directory.CreateDirectory(Folder + "/Prefabs");
        AssetDatabase.Refresh();
        foreach (string name in names)
        {
            var source = Model(name);
            var m = Material(name);
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
            var go = Object.Instantiate(source); go.name = name;
            try
            {
                Dress(go, m);
                if (name == "ArmarioSuspenso" || name == "ArmarioInstrumental")
                {
                    var glassSource = Model(name + "Vidro");
                    var glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnclaimedPortasAssets.GlassMaterialPath);
                    if (glassMaterial == null)
                        Debug.LogWarning("Unclaimed: glass material missing at " + UnclaimedPortasAssets.GlassMaterialPath + "; skipping glass.");
                    else
                    {
                        var glass = Object.Instantiate(glassSource, go.transform, false); glass.name = "Vidro";
                        Dress(glass, glassMaterial, false);
                    }
                }
                if (name == "LuminariaExame")
                {
                    var lens = Object.Instantiate(Model("LuminariaExameLente"), go.transform, false); lens.name = "Lente";
                    var lm = Material("Lente da luminaria de exame");
                    lm.SetFloat("_Surface", 0); lm.renderQueue = (int)RenderQueue.Geometry;
                    lm.SetColor("_BaseColor", new Color(.86f, .86f, .83f));
                    lm.SetFloat("_Smoothness", .35f); lm.SetFloat("_Metallic", 0);
                    lm.EnableKeyword("_EMISSION"); lm.SetColor("_EmissionColor", new Color(2.4f, 2.25f, 2f));
                    lm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    EditorUtility.SetDirty(lm); Dress(lens, lm, false);
                    var lightObject = new GameObject("Luz - luminaria de exame");
                    lightObject.transform.SetParent(go.transform, false);
                    // Export maps Blender (x,y,z) to Unity (x,z,y): below the lens is -Y.
                    lightObject.transform.localPosition = BoundsIn(lens, go.transform).center + Vector3.down * .005f;
                    lightObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var light = lightObject.AddComponent<Light>();
                    light.type = LightType.Spot; light.spotAngle = 50; light.innerSpotAngle = 22;
                    light.range = 3.2f; light.intensity = 3; light.useColorTemperature = true; light.colorTemperature = 4500;
                    light.shadows = LightShadows.Soft; light.lightmapBakeType = LightmapBakeType.Realtime; light.enabled = false;
                    var control = go.AddComponent<LuminariaExame>();
                    var so = new SerializedObject(control);
                    so.FindProperty("luz").objectReferenceValue = light;
                    so.FindProperty("lente").objectReferenceValue = lens.GetComponentInChildren<Renderer>(true);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    control.Ligar(false);
                }
                if (name == "BancadaAutopsia" || name == "ArmarioInstrumental" || name == "LavatorioJoelho" || name == "ArmarioSuspenso")
                {
                    var b = BoundsIn(go, go.transform);
                    var collider = go.AddComponent<BoxCollider>(); collider.center = b.center; collider.size = b.size;
                }
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(name));
            }
            finally { Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets();
    }
}
