// Lighting shared by the whole map (night shift): one ceiling fixture recipe for every room, outdoor
// lamps, the night sky, and baked indirect light for the static structure.
// Direct light stays realtime (Mixed lights, Baked Indirect mode); the bake adds only the bounce, which
// replaces the old fill lights. Walls, floors, ceilings and trim are the only GI-static objects:
// furniture and anything that may move is lit by light probes. Settings live in this scene's own
// Lighting Settings asset, so other scenes (Morgue.unity) keep theirs.
// Bake with Tools > Unclaimed > Bake Prototype Lighting (then re-bake the autopsy reflection).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class UnclaimedMorgueGenerator
{
    private const string LightingFolder = BaseFolder + "/Iluminacao";
    private const string LightingSettingsPath = LightingFolder + "/Iluminacao noturna.lighting";

    // Provisional night sky: one lat-long image (drawn by Blender/Ceu/ceu_noturno.py) on Unity's built-in
    // Skybox/Panoramic. The moon in that image sits opposite MoonEuler, so moonlight and sky agree; change
    // both together (MOON_EULER in the script).
    private const string SkyTexturePath = LightingFolder + "/Ceu_Noturno.png";
    private static readonly Vector3 MoonEuler = new Vector3(20f, 10f, 0f);   // low moon to the south-south-west
    private const float MoonIntensity = 0.12f;
    private static readonly Color MoonColor = new Color(0.68f, 0.76f, 0.95f);

    // Ceiling fixtures: a spot just under the diffuser with a wide cone and no inner cone, so the light
    // falls off gradually towards the walls (roughly the cosine spread of a diffuser) instead of drawing
    // the ring the old 100-150 degree cones left on the walls.
    private const float FixtureAngle = 170f, FixtureInner = 0f, FixtureRange = 8f;

    // Groups whose renderers are GI-static (lightmapped). Everything else gets light probes.
    private static readonly string[] StaticGroups =
        { "Pisos", "Paredes", "Revestimentos", "Rodapes", "Tetos", "Portas", "Fechamento externo", "Area externa" };

    // Surface-mounted tube fixture with its light. state: 0 lit, 1 aged tube, 2 burnt out.
    // model: the Blender fitting (prefab "Luminaria de sobrepor", reception for now); otherwise the
    // provisional box (1.25 x 0.32 m) the other rooms still use.
    private static void CeilingFixture(Transform ceiling, string lightName, string fixtureName, float x, float z,
        int state, float intensity, float kelvin, bool shadows, bool alongZ = false, bool model = false)
    {
        float lightY = 3.09f;
        var prefab = model ? AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(UnclaimedRecepcaoAssets.FittingPrefab)) : null;
        if (prefab != null)
        {
            var fitting = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ceiling);
            Undo.RegisterCreatedObjectUndo(fitting, "Create " + fixtureName);
            fitting.name = fixtureName;
            fitting.transform.SetPositionAndRotation(new Vector3(x, WallHeight - FinishOffset, z), Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f));
            fitting.transform.Find(UnclaimedRecepcaoAssets.LightPartName).GetComponent<MeshRenderer>().sharedMaterials =
                UnclaimedRecepcaoAssets.LightMaterials(state);
            lightY = WallHeight - FinishOffset - FittingDepth - 0.004f;   // just under the diffuser
        }
        else
        {
            Transform f = Sub(ceiling, fixtureName);
            float hx = alongZ ? 0.16f : 0.625f, hz = alongZ ? 0.625f : 0.16f, dx = alongZ ? 0.135f : 0.6f, dz = alongZ ? 0.6f : 0.135f;
            Piece("Corpo", f, x - hx, 3.11f, z - hz, x + hx, WallHeight, z + hz, whiteMetal);
            Piece("Difusor", f, x - dx, 3.104f, z - dz, x + dx, 3.11f, z + dz, state == 0 ? tube : state == 1 ? tubeAged : tubeDead);
        }
        if (state == 2) return;
        SpotLight(lightName, new Vector3(x, lightY, z), FixtureAngle, FixtureInner, FixtureRange,
            state == 0 ? intensity : intensity * 0.55f, state == 0 ? kelvin : 3600f, shadows);
    }

    // Height of the Blender fitting below the ceiling (LUMINARIA["height"] plus the end caps' overlap).
    private const float FittingDepth = 0.0735f;

    // Rooms without a finish yet: same fixtures and colour temperature logic as the finished rooms.
    private static void RoomLights(Transform ceilingRoot)
    {
        const float k = 4300f, cold = 5600f;
        var rooms = new (string room, float x, float z, float intensity, float kelvin, bool alongZ)[]
        {
            ("WC", -12.55f, -5.5f, 3.6f, k, false),
            ("Arquivo e escritorio", -4.2f, -8.1f, 4.2f, k, false),
            ("Arquivo e escritorio", -0.2f, -8.1f, 4.2f, k, false),
            ("Arquivo e escritorio", 0.75f, -4.75f, 3.6f, k, false),
            ("Descanso e vestiario", -11f, -1.5f, 3.9f, k, false),
            ("Descanso e vestiario", -7.3f, -1.5f, 3.9f, k, false),
            ("Deposito", -6.25f, 3.45f, 3.4f, k, true),
            ("Sala tecnica", -2.4f, 7.4f, 3.6f, k, false),
            ("Sala tecnica", 1.4f, 7.4f, 3.6f, k, false),
            ("Camara fria", 10.25f, -1.6f, 4.2f, cold, false),
            ("Camara fria", 10.25f, 3.4f, 4.2f, cold, false),
            ("Recebimento de corpos", 5.6f, 8.4f, 4.5f, k, false),
            ("Recebimento de corpos", 10.4f, 8.4f, 4.5f, k, false),
            ("Vestibulo de servico", 5.375f, 4.45f, 3.4f, k, false),
            ("Circulacao", -7.25f, -4.925f, 3.6f, k, false),
            ("Circulacao", -3.9f, -4.925f, 3.6f, k, false),
            ("Circulacao", -3.625f, -1.5f, 3.6f, k, true),
            ("Circulacao", -3.25f, 2.25f, 3.6f, k, true),
            ("Circulacao", -0.625f, 4.7f, 3.6f, k, false),
        };
        var count = new Dictionary<string, int>();
        foreach (var (room, x, z, intensity, kelvin, alongZ) in rooms)
        {
            count[room] = count.TryGetValue(room, out int n) ? n + 1 : 1;
            CeilingFixture(Sub(ceilingRoot, "Luminarias - " + room), "Luz - " + room + " - luminaria " + count[room],
                "Luminaria " + count[room], x, z, 0, intensity, kelvin, false, alongZ, model: room == "WC");   // finished rooms: modelled fitting
        }

        // Outside: a wall lamp over the main entrance and two floodlights on the building over the yard.
        Transform outside = Sub(ceilingRoot, "Iluminacao externa");
        WallLamp(outside, "Arandela - entrada principal", new Vector3(-11.375f, 2.55f, -12.375f - WallThickness / 2), Vector3.back,
            135f, 3.2f, 10f, 3300f, model: true);
        WallLamp(outside, "Refletor - porta do patio", new Vector3(12.75f + WallThickness / 2, 3.0f, 8.375f), Vector3.right,
            120f, 3.5f, 13f, 4300f);
        WallLamp(outside, "Refletor - patio norte", new Vector3(11f, 3.0f, 11.5f + WallThickness / 2), Vector3.forward,
            120f, 3.5f, 13f, 4300f);
    }

    // Lamp fixed to a wall face (point on the face, outward normal), aimed down and out. model: the
    // Blender wall lamp (prefab "Arandela externa", main entrance); otherwise a provisional box.
    private static void WallLamp(Transform parent, string name, Vector3 wallPoint, Vector3 normal, float angle,
        float intensity, float range, float kelvin, bool model = false)
    {
        Vector3 right = Vector3.Cross(normal, Vector3.up);
        Vector3 c = wallPoint + normal * 0.06f, lightAt = c - Vector3.up * 0.07f;
        var prefab = model ? AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(UnclaimedRecepcaoAssets.WallLampPrefab)) : null;
        if (prefab != null)
        {
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(lamp, "Create " + name);
            lamp.name = name;
            // Prefabs face -Z: the lamp's front goes out along the wall normal.
            lamp.transform.SetPositionAndRotation(wallPoint + normal * FinishOffset, Quaternion.LookRotation(-normal));
            lightAt = wallPoint + normal * 0.075f - Vector3.up * 0.06f;   // under the lamp's glass
        }
        else
        {
            Transform g = Sub(parent, name);
            Part("Corpo", g, PrimitiveType.Cube, c, new Vector3(0.28f, 0.1f, 0.12f), Quaternion.LookRotation(normal), whiteMetal);
            Part("Difusor", g, PrimitiveType.Cube, c - Vector3.up * 0.052f, new Vector3(0.24f, 0.006f, 0.1f), Quaternion.LookRotation(normal), tube);
        }
        Light light = SpotLight("Luz - " + name, lightAt, angle, 30f, range, intensity, kelvin, false);
        light.transform.rotation = Quaternion.LookRotation((Vector3.down * 1.6f + normal).normalized, right);
        light.transform.SetParent(OutdoorLightsGroup(), true);
    }

    // Night sky, moonlight and this scene's lighting settings. RenderSettings belong to the active scene.
    [MenuItem("Tools/Unclaimed/Apply Night Environment")]
    public static void NightEnvironment()
    {
        EnsureFolder(LightingFolder);
        RenderSettings.skybox = NightSky();
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.fog = false;

        // Moonlight replaces the default daylight; the old sun is switched off, not deleted. One moon only:
        // an existing "Luz da lua" is reused and moved into the outdoor lights group.
        // Searched in the active scene only (GameObject.Find would also see other open scenes).
        GameObject moonObject = null;
        foreach (GameObject top in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
                if (moonObject == null && t.name == "Luz da lua") moonObject = t.gameObject;
        if (moonObject == null)
        {
            moonObject = new GameObject("Luz da lua");
            Undo.RegisterCreatedObjectUndo(moonObject, "Create moonlight");
        }
        Transform outdoor = OutdoorLightsGroup();
        if (outdoor != null && moonObject.transform.parent != outdoor) Undo.SetTransformParent(moonObject.transform, outdoor, "Group moonlight");
        Undo.RecordObject(moonObject.transform, "Moonlight");
        moonObject.transform.rotation = Quaternion.Euler(MoonEuler);
        if (!moonObject.TryGetComponent(out Light moon)) moon = moonObject.AddComponent<Light>();
        Undo.RecordObject(moon, "Moonlight");
        moon.type = LightType.Directional;
        moon.intensity = MoonIntensity;
        moon.color = MoonColor;
        moon.shadows = LightShadows.Soft;
        moon.lightmapBakeType = LightmapBakeType.Mixed;
        RenderSettings.sun = moon;
        foreach (GameObject top in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (top.activeSelf && top.TryGetComponent(out Light old) && old.type == LightType.Directional && old != moon)
            {
                Undo.RecordObject(top, "Disable daylight");
                top.SetActive(false);
                Debug.Log("Unclaimed: disabled the daylight '" + top.name + "' (night shift; moonlight is 'Luz da lua').", top);
            }

        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Iluminacao noturna" };
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            // CPU: this machine has no OpenCL device, so the GPU lightmapper refuses to start.
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.lightmapResolution = 10f;
            settings.lightmapPadding = 4;
            settings.lightmapMaxSize = 2048;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.ao = false;
            settings.maxBounces = 3;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 160;
            settings.environmentSampleCount = 64;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
        }
        Lightmapping.lightingSettings = settings;
    }

    // Outdoor lights (moon, entrance lamp, yard floodlights) in one group under "Luzes", easy to adjust.
    private static Transform OutdoorLightsGroup()
    {
        Transform lights = lightsGroup;
        if (lights == null)
            foreach (GameObject top in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (top.name == GroupName) lights = top.transform.Find("Luzes");
        return lights != null ? Sub(lights, "Iluminacao externa") : null;
    }

    // The night sky material (Skybox/Panoramic, lat-long). The material asset keeps its GUID: an older
    // procedural "Ceu noturno" is converted in place.
    private static Material NightSky()
    {
        string path = LightingFolder + "/Ceu noturno.mat";
        if (AssetImporter.GetAtPath(SkyTexturePath) is TextureImporter importer &&
            (importer.mipmapEnabled || importer.wrapModeV != TextureWrapMode.Clamp || importer.maxTextureSize != 4096))
        {
            importer.mipmapEnabled = false;            // mip seams show at the panorama's wrap
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;            // a star is one texel: smaller loses them
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath);
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader panoramic = Shader.Find("Skybox/Panoramic");
        if (sky == null)
        {
            sky = new Material(panoramic) { name = "Ceu noturno" };
            AssetDatabase.CreateAsset(sky, path);
        }
        if (sky.shader != panoramic && texture != null)
        {
            sky.shader = panoramic;
            sky.SetTexture("_MainTex", texture);
            sky.SetFloat("_Mapping", 1f);        // latitude-longitude layout
            sky.SetFloat("_ImageType", 0f);      // 360 degrees
            sky.SetFloat("_MirrorOnBack", 0f);
            sky.SetFloat("_Layout", 0f);
            sky.SetFloat("_Rotation", 0f);       // the moon's direction is drawn into the image
            sky.SetFloat("_Exposure", 1f);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            EditorUtility.SetDirty(sky);
        }
        else if (texture == null)
            Debug.LogWarning("Unclaimed: missing " + SkyTexturePath + "; run Blender/Ceu/ceu_noturno.py.");
        return sky;
    }

    // Lightmap UVs for the structure, GI flags, light probes and reflection probes.
    private static void PrepareStaticLighting(List<Zone> zones)
    {
        var staticGroups = new HashSet<Transform>();
        foreach (string g in StaticGroups) staticGroups.Add(root.Find(g));
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool structural = false;
            for (Transform t = r.transform; t != null && t != root; t = t.parent)
                if (staticGroups.Contains(t)) { structural = true; break; }
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            Material m = r.sharedMaterial;
            bool emissiveOrClear = m != null && (m.renderQueue >= 3000 || m.IsKeywordEnabled("_EMISSION"));
            if (!structural || emissiveOrClear)
            {
                // Furniture, props and fittings may move or are too small for a lightmap: probes only.
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.ContributeGI);
                r.receiveGI = ReceiveGI.LightProbes;
                continue;
            }
            var filter = r.GetComponent<MeshFilter>();
            if (filter.sharedMesh != null && filter.sharedMesh.name == "Cube") filter.sharedMesh = LightmapCube(r.transform.lossyScale);
            if (filter.sharedMesh == null || filter.sharedMesh.uv2.Length == 0)
            {
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.ContributeGI);
                r.receiveGI = ReceiveGI.LightProbes;
                continue;
            }
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                flags | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            r.receiveGI = ReceiveGI.Lightmaps;
            // Ground, roof and facade are only seen at night from the entrance: coarse lightmaps.
            Transform top = r.transform;
            while (top.parent != root) top = top.parent;
            var so = new SerializedObject(r);
            so.FindProperty("m_ScaleInLightmap").floatValue =
                top.name == "Area externa" ? 0.15f : top.name == "Fechamento externo" ? 0.3f : 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        LightProbeGrid(zones);
        ReflectionProbes(zones);
    }

    // Copy of Unity's cube with lightmap UVs laid out in proportion to the faces' real sizes, so scaled
    // walls and slabs get even texel density (the built-in cube gives all six faces the same square).
    private static Mesh LightmapCube(Vector3 scale)
    {
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Vector3[] v = cube.vertices, n = cube.normals;
        var uv2 = new Vector2[v.Length];
        // Faces as (u axis, v axis) of the local box; sizes in metres.
        var faces = new List<(int vertex, int a, int b, float w, float h)>();
        var seen = new HashSet<Vector3Int>();
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 nn = n[i];
            int axis = Mathf.Abs(nn.x) > 0.5f ? 0 : Mathf.Abs(nn.y) > 0.5f ? 1 : 2;
            int a = axis == 0 ? 2 : 0, b = axis == 1 ? 2 : 1;
            var key = Vector3Int.RoundToInt(nn);
            if (seen.Add(key)) faces.Add((i, a, b, Mathf.Abs(scale[a]), Mathf.Abs(scale[b])));
        }
        // Shelf packing of the six rectangles, then a uniform scale into the unit square.
        faces.Sort((p, q) => q.h.CompareTo(p.h));
        float total = 0;
        foreach (var f in faces) total += f.w * f.h;
        float rowWidth = Mathf.Max(Mathf.Sqrt(total) * 1.15f, MaxW(faces));
        float pad = 0.02f * rowWidth;
        var origin = new Dictionary<Vector3Int, Vector2>();
        float x = 0, y = 0, rowH = 0, usedW = 0;
        foreach (var f in faces)
        {
            if (x > 0 && x + f.w > rowWidth) { x = 0; y += rowH + pad; rowH = 0; }
            origin[Vector3Int.RoundToInt(n[f.vertex])] = new Vector2(x, y);
            x += f.w + pad;
            usedW = Mathf.Max(usedW, x);
            rowH = Mathf.Max(rowH, f.h);
        }
        float size = Mathf.Max(usedW, y + rowH) + pad;
        for (int i = 0; i < v.Length; i++)
        {
            var key = Vector3Int.RoundToInt(n[i]);
            int axis = Mathf.Abs(n[i].x) > 0.5f ? 0 : Mathf.Abs(n[i].y) > 0.5f ? 1 : 2;
            int a = axis == 0 ? 2 : 0, b = axis == 1 ? 2 : 1;
            Vector2 o = origin[key];
            uv2[i] = new Vector2(pad + o.x + (v[i][a] + 0.5f) * Mathf.Abs(scale[a]),
                                 pad + o.y + (v[i][b] + 0.5f) * Mathf.Abs(scale[b])) / (size + pad);
        }
        var mesh = Object.Instantiate(cube);
        mesh.name = "Cube (lightmap UV)";
        mesh.uv2 = uv2;
        return mesh;
    }

    private static float MaxW(List<(int vertex, int a, int b, float w, float h)> faces)
    {
        float m = 0;
        foreach (var f in faces) m = Mathf.Max(m, f.w);
        return m;
    }

    // Probes every ~1.5 m inside each room (three heights) and around the entrance outside.
    private static void LightProbeGrid(List<Zone> zones)
    {
        var points = new List<Vector3>();
        const float inset = WallThickness / 2 + 0.3f, step = 1.5f;
        foreach (Zone zone in zones)
        {
            float[] heights = zone.Kind == Kind.Interior ? new[] { 0.3f, 1.3f, 2.6f } : new[] { 0.4f, 1.8f };
            foreach (Rect r in zone.Rects)
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt((r.width - 2 * inset) / step)), nz = Mathf.Max(1, Mathf.CeilToInt((r.height - 2 * inset) / step));
                for (int i = 0; i <= nx; i++)
                    for (int j = 0; j <= nz; j++)
                        foreach (float h in heights)
                            points.Add(new Vector3(Mathf.Lerp(r.xMin + inset, r.xMax - inset, i / (float)nx), h,
                                Mathf.Lerp(r.yMin + inset, r.yMax - inset, j / (float)nz)));
            }
        }
        for (float x = -15.5f; x <= -7.0f; x += 1.7f)          // sidewalk and the ground in front of the entrance
            foreach (float z in new[] { -12.8f, -14.0f, -15.5f })
                foreach (float h in new[] { 0.4f, 1.8f })
                    points.Add(new Vector3(x, h, z));
        var obj = new GameObject("Sondas de luz");
        Undo.RegisterCreatedObjectUndo(obj, "Create light probes");
        obj.transform.SetParent(lightsGroup, false);
        obj.AddComponent<LightProbeGroup>().probePositions = points.ToArray();
    }

    // A baked, box-projected reflection probe per room (the autopsy room keeps its custom one).
    private static void ReflectionProbes(List<Zone> zones)
    {
        Transform group = Sub(lightsGroup, "Sondas de reflexo");
        foreach (Zone zone in zones)
        {
            if (zone.Kind != Kind.Interior || zone.Name == "Autopsia") continue;
            for (int i = 0; i < zone.Rects.Length; i++)
            {
                Rect r = zone.Rects[i];
                if (r.width < 2f || r.height < 2f) continue;
                var obj = new GameObject("Reflexo - " + zone.Name + (zone.Rects.Length > 1 ? " " + (i + 1) : ""));
                Undo.RegisterCreatedObjectUndo(obj, "Create reflection probe");
                obj.transform.SetParent(group, false);
                obj.transform.position = new Vector3(r.center.x, 1.5f, r.center.y);
                var probe = obj.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.boxProjection = true;
                probe.size = new Vector3(r.width - WallThickness, WallHeight, r.height - WallThickness);
                probe.center = new Vector3(0f, WallHeight / 2 - 1.5f, 0f);
                probe.resolution = 128;
                probe.blendDistance = 0.3f;
                if (zone.Name == "WC")
                {
                    // The bathroom mirror shows this probe sharply, and it hangs on the wall, i.e. on the box
                    // edge: with a blend band its weight fell to ~0 there and it reflected the night sky.
                    probe.blendDistance = 0f;
                    probe.resolution = 256;
                }
            }
        }

        // The entrance frame straddles the facade, on the edge of the reception box, where that probe's weight
        // fades out (Forward+ blends per pixel and ignores probe anchors): a small probe over the doorway, seen
        // from inside, keeps the anodised lining and the door's metal from mirroring the night sky.
        var entrance = new GameObject("Reflexo - entrada");
        Undo.RegisterCreatedObjectUndo(entrance, "Create reflection probe");
        entrance.transform.SetParent(group, false);
        entrance.transform.position = new Vector3(-11.375f, 1.5f, -11.2f);
        var doorway = entrance.AddComponent<ReflectionProbe>();
        doorway.mode = ReflectionProbeMode.Baked;
        doorway.size = new Vector3(2.4f, WallHeight, 1.6f);                   // z -12.6 .. -11.0, covers both casings
        doorway.center = new Vector3(0f, WallHeight / 2 - 1.5f, -0.6f);
        doorway.importance = 2;
        doorway.blendDistance = 0f;
        doorway.resolution = 128;
    }

    [MenuItem("Tools/Unclaimed/Bake Prototype Lighting")]
    public static void BakePrototypeLighting()
    {
        if (Lightmapping.lightingSettings == null || Lightmapping.lightingSettings.name != "Iluminacao noturna")
        {
            Debug.LogWarning("Unclaimed: this scene does not use 'Iluminacao noturna'; run Tools > Unclaimed > Apply Night Environment first.");
            return;
        }
        Lightmapping.BakeAsync();
    }
}
