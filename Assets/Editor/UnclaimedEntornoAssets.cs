using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnclaimedEntornoAssets
{
    public const string Folder = "Assets/Modelos/Entorno/Vegetacao";
    public const string PrefabFolder = Folder + "/Prefabs";
    private const string TextureFolder = Folder + "/Texturas";
    private const string MaterialFolder = Folder + "/Materiais";

    private sealed class Plant
    {
        public string Name;
        public float Height, Radius, ColliderHeight;
        public bool HasCollider, VertexColors;
        public readonly SortedDictionary<int, int> Tris = new SortedDictionary<int, int>();
        public readonly List<string> Materials = new List<string>();
        public float[] Lods;
        public readonly Dictionary<string, Dictionary<string, string>> Wind = new Dictionary<string, Dictionary<string, string>>();
    }

    public static string PrefabPath(string nome) => PrefabFolder + "/" + nome + ".prefab";

    [MenuItem("Tools/Unclaimed/Build Entorno Prefabs")]
    public static void BuildPrefabs()
    {
        EnsureFolder(Folder, "Materiais");
        EnsureFolder(Folder, "Prefabs");
        ConfigureTextures();
        var plants = ReadManifest();
        var materials = BuildMaterials(plants);
        foreach (Plant plant in plants)
            BuildPlant(plant, materials);
        AssetDatabase.SaveAssets();
    }

    private static List<Plant> ReadManifest()
    {
        string path = Folder + "/vegetacao.json";
        var result = new List<Plant>();
        if (!File.Exists(path))
        {
            Debug.LogWarning("Unclaimed: missing vegetation manifest " + path + ".");
            return result;
        }
        string json = File.ReadAllText(path);
        foreach (var entry in PlantEntries(json))
        {
            string body = entry.Value;
            var fields = ObjectFields(body);
            var plant = new Plant { Name = ReadString(body, "nome", entry.Key) };
            if (fields.TryGetValue("altura", out string height))
                plant.Height = float.Parse(height, System.Globalization.CultureInfo.InvariantCulture);
            if (fields.TryGetValue("lods", out string lodValues))
            {
                string[] values = lodValues.Trim().Trim('[', ']').Split(',');
                var transitions = new List<float>();
                float previous = float.PositiveInfinity;
                foreach (string value in values)
                {
                    if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                        || float.IsNaN(v) || v < 0 || v > 1 || v >= previous)
                        throw new FormatException("Unclaimed: lods must be descending screen fractions in [0,1]: " + plant.Name);
                    transitions.Add(v); previous = v;
                }
                plant.Lods = transitions.ToArray();
            }
            plant.VertexColors = fields.TryGetValue("cor_vertice", out string colors) && colors == "true";
            if (fields.TryGetValue("vento", out string wind))
                foreach (var material in ObjectFields(wind)) plant.Wind[material.Key] = ObjectFields(material.Value);
            Match collider = Regex.Match(body, "\\\"colisor\\\"\\s*:\\s*\\{(?<v>[^{}]*)\\}");
            if (collider.Success)
            {
                plant.HasCollider = true;
                plant.Radius = ReadFloat(collider.Groups["v"].Value, "raio");
                plant.ColliderHeight = ReadFloat(collider.Groups["v"].Value, "altura");
            }
            Match tris = Regex.Match(body, "\\\"tris\\\"\\s*:\\s*\\{(?<v>[^{}]*)\\}");
            if (tris.Success)
                foreach (Match tri in Regex.Matches(tris.Groups["v"].Value, "\\\"(?<lod>\\d+)\\\"\\s*:\\s*(?<count>\\d+)"))
                    plant.Tris[int.Parse(tri.Groups["lod"].Value)] = int.Parse(tri.Groups["count"].Value);
            Match mats = Regex.Match(body, "\\\"materiais\\\"\\s*:\\s*\\[(?<v>[^]]*)\\]");
            if (mats.Success)
                foreach (Match mat in Regex.Matches(mats.Groups["v"].Value, "\\\"(?<name>[^\\\"]+)\\\"")) plant.Materials.Add(mat.Groups["name"].Value);
            result.Add(plant);
        }
        return result;
    }

    // Read balanced JSON values without depending on an optional JSON package.
    // Strings are skipped as tokens, so braces and escaped quotes inside names are safe.
    private static Dictionary<string, string> ObjectFields(string json)
    {
        var fields = new Dictionary<string, string>();
        int i = 0;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        if (i >= json.Length || json[i++] != '{') return fields;
        while (i < json.Length)
        {
            while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;
            if (i >= json.Length || json[i] == '}') break;
            int keyStart = i;
            SkipString(json, ref i);
            string key = json.Substring(keyStart + 1, i - keyStart - 2);
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i++] != ':') throw new FormatException("Unclaimed: invalid vegetation JSON property.");
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            int start = i, depth = 0;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"') { SkipString(json, ref i); continue; }
                if (depth == 0 && (c == ',' || c == '}')) break;
                if (c == '{' || c == '[') depth++;
                if (c == '}' || c == ']') depth--;
                i++;
            }
            fields[key] = json.Substring(start, i - start).Trim();
        }
        return fields;
    }

    private static void SkipString(string json, ref int i)
    {
        if (i >= json.Length || json[i++] != '"') throw new FormatException("Unclaimed: expected JSON string.");
        while (i < json.Length)
        {
            char c = json[i++];
            if (c == '\\') { i++; continue; }
            if (c == '"') return;
        }
        throw new FormatException("Unclaimed: unterminated JSON string.");
    }

    private static IEnumerable<KeyValuePair<string, string>> PlantEntries(string json)
    {
        foreach (var entry in ObjectFields(json))
        {
            if (!entry.Value.StartsWith("{", StringComparison.Ordinal)) continue;
            if (ObjectFields(entry.Value).ContainsKey("nome")) yield return entry;
            else foreach (var child in PlantEntries(entry.Value)) yield return child;
        }
    }

    private static string ReadString(string source, string key, string fallback)
    {
        Match match = Regex.Match(source, "\\\"" + key + "\\\"\\s*:\\s*\\\"(?<v>[^\\\"]+)\\\"");
        return match.Success ? match.Groups["v"].Value : fallback;
    }

    private static float ReadFloat(string source, string key)
    {
        Match match = Regex.Match(source, "\\\"" + key + "\\\"\\s*:\\s*(?<v>-?[0-9]+(?:\\.[0-9]+)?)");
        return match.Success ? float.Parse(match.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
    }

    private static void ConfigureTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            bool normal = path.EndsWith("_Normal.png", StringComparison.OrdinalIgnoreCase);
            bool impostor = path.Contains("_Impostor_");
            bool bark = Path.GetFileName(path).StartsWith("Casca_", StringComparison.Ordinal);
            bool changed = false;
            if (normal)
            {
                changed |= importer.textureType != TextureImporterType.NormalMap;
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (path.EndsWith("_BaseMap.png", StringComparison.OrdinalIgnoreCase))
            {
                changed |= importer.alphaIsTransparency != !bark || importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                    !importer.mipmapEnabled || !importer.mipMapsPreserveCoverage || importer.alphaTestReferenceValue != 0.5f ||
                    importer.wrapMode != (bark ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) || importer.maxTextureSize != (impostor ? 1024 : 2048) ||
                    importer.textureCompression != TextureImporterCompression.CompressedHQ;
                importer.alphaIsTransparency = !bark;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.wrapMode = bark ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.maxTextureSize = impostor ? 1024 : 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            if (changed) importer.SaveAndReimport();
        }
    }

    private static Dictionary<string, Material> BuildMaterials(List<Plant> plants)
    {
        var names = new HashSet<string> { "Casca", "Casca_Sibipiruna", "Casca_Mangueira", "Folhas_Largas", "Folhas_Compostas", "Folhas_Estreitas",
            "Plantas_Procedurais", "Grama_Laminas" };
        foreach (Plant plant in plants)
            foreach (string name in plant.Materials) if (name.StartsWith("Impostor_", StringComparison.Ordinal)) names.Add(name);
        var result = new Dictionary<string, Material>();
        foreach (string name in names)
        {
            // Bark scans are opaque and single-sided; grass blades are opaque geometry seen from both sides.
            bool bark = name.StartsWith("Casca", StringComparison.Ordinal);
            bool cutout = !bark && name != "Grama_Laminas";
            // The kit renders impostors to <Plant>_Impostor_BaseMap.png for the material slot Impostor_<Plant>.
            string basePath = name.StartsWith("Impostor_", StringComparison.Ordinal)
                ? TextureFolder + "/" + name.Substring("Impostor_".Length) + "_Impostor_BaseMap.png"
                : TextureFolder + "/" + name + "_BaseMap.png";
            if (!File.Exists(basePath))
            {
                Debug.LogWarning("Unclaimed: missing vegetation texture " + basePath + " for material " + name + ".");
                continue;
            }
            string matPath = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, matPath);
            }
            Shader shader = Shader.Find("Unclaimed/Vegetacao") ?? Shader.Find("Universal Render Pipeline/Lit");
            bool initializeWind = material.shader != shader;
            material.shader = shader;
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            // Leaves get a slightly cool tint so the 2700-3300 K garden lamps don't turn them yellow-olive.
            material.SetColor("_BaseColor", name.StartsWith("Folhas_", StringComparison.Ordinal) ? new Color(0.78f, 0.9f, 0.88f) : Color.white);
            material.SetFloat("_Smoothness", bark ? 0.1f : 0.15f);
            material.SetFloat("_ReceiveShadows", 1f);
            if (bark)
            {
                string normalPath = TextureFolder + "/" + name + "_Normal.png";
                if (!File.Exists(normalPath)) normalPath = TextureFolder + "/Casca_Normal.png";
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal == null) Debug.LogWarning("Unclaimed: missing vegetation texture " + normalPath + ".");
                material.SetTexture("_BumpMap", normal);
            }
            else
            {
                material.SetFloat("_Cutoff", 0.5f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (shader.name == "Universal Render Pipeline/Lit") BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            else
            {
                if (initializeWind) SetWindDefaults(material, name, 2f);
                SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
                material.SetFloat("_UsarNormal", material.GetTexture("_BumpMap") != null ? 1 : 0);
            }
            material.SetFloat("_AlphaClip", cutout ? 1 : 0);
            material.SetFloat("_Cull", bark ? 2 : 0);
            SetKeyword(material, "_ALPHATEST_ON", cutout);
            material.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            result[name] = material;
        }
        return result;
    }

    private static void BuildPlant(Plant plant, Dictionary<string, Material> materials)
    {
        string fbx = Folder + "/" + plant.Name + ".fbx";
        if (!File.Exists(fbx))
        {
            Debug.LogWarning("Unclaimed: missing FBX " + fbx + ".");
            return;
        }
        if (AssetImporter.GetAtPath(fbx) is ModelImporter importer && ConfigureModel(importer)) importer.SaveAndReimport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null)
        {
            Debug.LogWarning("Unclaimed: could not load FBX " + fbx + ".");
            return;
        }
        var root = new GameObject(plant.Name);
        try
        {
            // Overrides get persistent plant-specific material variants, never mutate shared defaults.
            var plantMaterials = new Dictionary<string, Material>(materials);
            foreach (string name in plant.Materials)
            {
                if (!materials.TryGetValue(name, out Material source) || source.shader.name != "Unclaimed/Vegetacao") continue;
                string variantPath = MaterialFolder + "/" + plant.Name + "__" + name + ".mat";
                Material variant = AssetDatabase.LoadAssetAtPath<Material>(variantPath);
                if (variant == null)
                {
                    variant = new Material(source) { name = plant.Name + "__" + name };
                    AssetDatabase.CreateAsset(variant, variantPath);
                }
                // Refresh surface settings but keep the artist's wind tuning between builds.
                string[] tuning = { "_VentoTronco", "_VentoRamos", "_VentoFolhas", "_VentoFrequencia", "_VentoSemCor", "_VentoFolhaSemCor", "_Translucidez" };
                float[] retained = new float[tuning.Length];
                for (int k = 0; k < tuning.Length; k++) retained[k] = variant.GetFloat(tuning[k]);
                variant.shader = source.shader;
                variant.CopyPropertiesFromMaterial(source);
                for (int k = 0; k < tuning.Length; k++) variant.SetFloat(tuning[k], retained[k]);
                variant.SetFloat("_VentoAltura", Mathf.Max(0.05f, plant.Height));
                // Kit meshes now carry the RGBA wind weights; impostor quads keep the height fallback.
                if (plant.VertexColors && !name.StartsWith("Impostor_", StringComparison.Ordinal)) variant.SetFloat("_VentoSemCor", 0f);
                variant.enableInstancing = true;
                if (plant.Wind.TryGetValue(name, out var wind))
                {
                    string[] keys = { "tronco", "ramos", "folhas", "frequencia" };
                    string[] properties = { "_VentoTronco", "_VentoRamos", "_VentoFolhas", "_VentoFrequencia" };
                    for (int k = 0; k < keys.Length; k++)
                        if (wind.TryGetValue(keys[k], out string value))
                        {
                            float v = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                            if (float.IsNaN(v) || float.IsInfinity(v) || v < 0) throw new FormatException("Unclaimed: invalid wind amplitude/frequency for " + plant.Name);
                            variant.SetFloat(properties[k], v);
                        }
                }
                EditorUtility.SetDirty(variant);
                plantMaterials[name] = variant;
            }
            var lodRenderers = new List<Renderer[]>();
            var lodTris = new List<int>();
            foreach (KeyValuePair<int, int> lodEntry in plant.Tris)
            {
                int lod = lodEntry.Key;
                Transform source = FindLod(model.transform, plant.Name + "_LOD" + lod);
                if (source == null)
                {
                    Debug.LogWarning("Unclaimed: " + fbx + " has no mesh named " + plant.Name + "_LOD" + lod + ".");
                    continue;
                }
                GameObject child = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
                child.name = plant.Name + "_LOD" + lod;
                child.transform.localPosition = source.localPosition;
                child.transform.localRotation = source.localRotation;
                child.transform.localScale = source.localScale;
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    renderer.receiveShadows = !plant.Materials.Contains("Impostor_" + plant.Name) || lod < 2;
                    renderer.shadowCastingMode = CastShadows(plant.Name, lod) ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    if (renderer is MeshRenderer meshRenderer) meshRenderer.receiveGI = ReceiveGI.LightProbes;
                    AssignMaterials(renderer, plantMaterials);
                }
                lodRenderers.Add(renderers);
                lodTris.Add(lodEntry.Value);
            }
            if (lodRenderers.Count > 0)
            {
                var group = root.AddComponent<LODGroup>();
                LOD[] lods = new LOD[lodRenderers.Count];
                if (plant.Lods != null && plant.Lods.Length != lods.Length)
                    throw new FormatException("Unclaimed: lods must contain one screen fraction per imported LOD: " + plant.Name);
                for (int i = 0; i < lods.Length; i++) lods[i] = new LOD(plant.Lods != null ? plant.Lods[i] : Transition(plant.Name, i, lods.Length), lodRenderers[i]);
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.RecalculateBounds();
            }
            if (plant.HasCollider)
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = plant.Radius;
                capsule.height = plant.ColliderHeight;
                capsule.center = new Vector3(0f, plant.ColliderHeight * 0.5f, 0f);
            }
            root.AddComponent<LimitesVento>();
            string path = PrefabPath(plant.Name);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("Unclaimed: " + plant.Name + " — " + lodRenderers.Count + " LODs, tris " + string.Join("/", lodTris) + ", collider " + (plant.HasCollider ? "capsule" : "none") + ".");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
    }

    // Initial amplitudes in metres (trunk / branches / leaves), natural frequency in Hz:
    // Casca*: 0.04 / 0.10 / 0, 0.35; Folhas_*: 0.04 / 0.10 / 0.025, 0.35 (translucency 0.12);
    // Plantas_Procedurais: 0.10 / 0.06 / 0.02, 0.8; Impostor_*: 0.04 / 0 / 0, 0.35.
    // Current meshes have no vertex colors; disable _VentoSemCor after importing RGBA wind weights.
    private static void SetWindDefaults(Material material, string name, float height)
    {
        bool procedural = name == "Plantas_Procedurais" || name == "Grama_Laminas", impostor = name.StartsWith("Impostor_", StringComparison.Ordinal);
        material.SetFloat("_VentoTronco", procedural ? 0.10f : 0.04f);
        material.SetFloat("_VentoRamos", impostor ? 0 : procedural ? 0.06f : 0.10f);
        material.SetFloat("_VentoFolhas", impostor || name.StartsWith("Casca", StringComparison.Ordinal) ? 0 : procedural ? 0.02f : 0.025f);
        material.SetFloat("_VentoFrequencia", procedural ? 0.8f : 0.35f);
        material.SetFloat("_VentoAltura", Mathf.Max(0.05f, height));
        material.SetFloat("_VentoSemCor", 1);
        material.SetFloat("_VentoFolhaSemCor", impostor || name.StartsWith("Casca", StringComparison.Ordinal) ? 0 : 1);
        material.SetFloat("_Translucidez", impostor || name.StartsWith("Casca", StringComparison.Ordinal) ? 0 : 0.12f);
    }

    private static bool ConfigureModel(ModelImporter model)
    {
        // Materials stay embedded so the renderers keep the Blender slot names; AssignMaterials swaps them by name.
        bool changed = model.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
            model.materialLocation != ModelImporterMaterialLocation.InPrefab || !model.bakeAxisConversion ||
            model.importNormals != ModelImporterNormals.Import ||
            model.importAnimation || model.importCameras || model.importLights || model.generateSecondaryUV || model.isReadable ||
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

    private static Transform FindLod(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
        // A single-mesh FBX imports with the mesh on the root, named after the file.
        return name.EndsWith("_LOD0", StringComparison.Ordinal) && root.childCount == 0 && root.GetComponent<MeshFilter>() != null ? root : null;
    }

    private static void AssignMaterials(Renderer renderer, Dictionary<string, Material> materials)
    {
        Material[] assigned = renderer.sharedMaterials;
        for (int i = 0; i < assigned.Length; i++)
        {
            string slot = assigned[i] != null ? assigned[i].name : "";
            if (materials.TryGetValue(slot, out Material material)) assigned[i] = material;
        }
        renderer.sharedMaterials = assigned;
    }

    private static bool IsTreeOrShrub(string name) => name.StartsWith("Arvore_", StringComparison.Ordinal) || name.StartsWith("Arbusto_", StringComparison.Ordinal) || name.StartsWith("Bananeira_", StringComparison.Ordinal);
    private static bool CastShadows(string name, int lod) => IsTreeOrShrub(name) && lod < 2;

    private static float Transition(string name, int lod, int count)
    {
        if (lod == count - 1) return IsTreeOrShrub(name) ? 0.015f : 0.02f;
        if (name.StartsWith("Arvore_", StringComparison.Ordinal)) return lod == 0 ? 0.35f : 0.12f;
        if (IsTreeOrShrub(name)) return lod == 0 ? 0.25f : 0.06f;
        if (name.StartsWith("Folhas_Secas", StringComparison.Ordinal) || name == "Trepadeira_Muro") return 0.03f;
        return lod == 0 ? 0.12f : 0.04f;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
