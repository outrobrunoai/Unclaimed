using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class UnclaimedChuva
{
    const string Menu = "Tools/Unclaimed/Chuva/";
    const string Folder = "Assets/UnclaimedPrototype/Clima";
    const string RootName = "Chuva leve";
    const float Cell = 0.125f, MinX = -42, MaxX = 44, MinZ = -42, MaxZ = 40, RayTop = 30;
    const int BlurRadius = 4;

    [MenuItem(Menu + "Setup Rain Group")]
    public static void SetupRainGroup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup light rain");
        GameObject root = FindRoot(scene, RootName) ?? CreateObject(RootName, null, scene);
        ChuvaLeve rain = root.GetComponent<ChuvaLeve>();
        if (!rain) rain = Undo.AddComponent<ChuvaLeve>(root);
        Undo.RecordObject(rain, "Complete rain references");
        Material drops = GetMaterial("Gotas", "Unclaimed/Chuva gotas", 0);
        Material splashes = GetMaterial("Respingos", "Unclaimed/Chuva gotas", 1);
        Material ripples = GetMaterial("Ondulacoes", "Unclaimed/Chuva gotas", 2);
        if (!rain.gotas) rain.gotas = CompleteParticles(root, "Gotas", 0, drops);
        if (!rain.particulasRespingos) rain.particulasRespingos = CompleteParticles(root, "Respingos", 1, splashes);
        if (!rain.particulasOndulacoes) rain.particulasOndulacoes = CompleteParticles(root, "Ondulacoes", 2, ripples);
        GameObject audioObject = Child(root, "Audio chuva");
        AudioSource source = audioObject.GetComponent<AudioSource>();
        if (!source)
        {
            source = Undo.AddComponent<AudioSource>(audioObject);
            source.loop = true; source.playOnAwake = false; source.spatialBlend = 0;
            source.volume = 0.35f;
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/Chuva_leve_loop.wav");
        }
        AudioLowPassFilter filter = audioObject.GetComponent<AudioLowPassFilter>();
        if (!filter) filter = Undo.AddComponent<AudioLowPassFilter>(audioObject);
        if (!rain.audioChuva) rain.audioChuva = source;
        if (!rain.filtroAudio) rain.filtroAudio = filter;
        Undo.RecordObject(source, "Configure rain playback");
        source.playOnAwake = false;
        Undo.RecordObject(filter, "Set outdoor rain cutoff");
        filter.cutoffFrequency = 22000f;

        GameObject indoorObject = Child(root, "Audio chuva - interior");
        AudioSource indoorSource = indoorObject.GetComponent<AudioSource>();
        if (!indoorSource)
        {
            indoorSource = Undo.AddComponent<AudioSource>(indoorObject);
            indoorSource.loop = true; indoorSource.playOnAwake = false; indoorSource.spatialBlend = 0;
            indoorSource.volume = 0;
            indoorSource.clip = rain.audioChuva.clip;
        }
        AudioLowPassFilter indoorFilter = indoorObject.GetComponent<AudioLowPassFilter>();
        if (!indoorFilter)
        {
            indoorFilter = Undo.AddComponent<AudioLowPassFilter>(indoorObject);
            indoorFilter.cutoffFrequency = 1100f;
        }
        if (!rain.audioInterno) rain.audioInterno = indoorSource;
        if (!rain.filtroInterno) rain.filtroInterno = indoorFilter;

        GameObject portalRoot = Child(root, "Portais");
        GameObject portalObject = Child(portalRoot, "Entrada principal");
        PortalDeChuva portal = portalObject.GetComponent<PortalDeChuva>();
        if (!portal)
        {
            Undo.RecordObject(portalObject.transform, "Place entrance rain portal");
            portalObject.transform.SetPositionAndRotation(new Vector3(-11.38f, 1.3f, -12.38f), Quaternion.identity);
            portal = Undo.AddComponent<PortalDeChuva>(portalObject);
            portal.largura = 2;
        }
        Undo.RecordObject(portal, "Complete rain portal references");
        if (portal.folhas == null || portal.folhas.Length == 0)
        {
            GameObject building = FindRoot(scene, "Unclaimed - Necroterio (planta)");
            Transform entrance = building ? building.transform.Find("Portas/Entrada principal") : null;
            if (entrance) portal.folhas = entrance.GetComponentsInChildren<FolhaDePorta>(true);
        }
        if (!portal.fonte)
        {
            GameObject portalAudio = Child(portalObject, "Audio portal");
            AudioSource portalSource = portalAudio.GetComponent<AudioSource>();
            if (!portalSource) portalSource = Undo.AddComponent<AudioSource>(portalAudio);
            Undo.RecordObject(portalSource, "Configure entrance rain audio");
            portalSource.clip = rain.audioChuva.clip;
            portalSource.loop = true; portalSource.playOnAwake = false; portalSource.spatialBlend = 1;
            portalSource.volume = 0; portalSource.dopplerLevel = 0;
            portalSource.minDistance = 1; portalSource.maxDistance = 500;
            portalSource.rolloffMode = AudioRolloffMode.Custom;
            portalSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, 1));
            portal.fonte = portalSource;
        }
        if (!portal.filtro)
        {
            portal.filtro = portal.fonte.GetComponent<AudioLowPassFilter>();
            if (!portal.filtro) portal.filtro = Undo.AddComponent<AudioLowPassFilter>(portal.fonte.gameObject);
            Undo.RecordObject(portal.filtro, "Set entrance rain cutoff");
            portal.filtro.cutoffFrequency = portal.corteFechada;
        }
        if (rain.portais == null || rain.portais.Count == 0) rain.portais = new List<PortalDeChuva> { portal };
        EditorUtility.SetDirty(portal);
        if (!rain.mapa) rain.mapa = AssetDatabase.LoadAssetAtPath<MapaAbrigoChuva>(Folder + "/MapaAbrigoChuva.asset");
        CompleteOverlay(root);
        EditorUtility.SetDirty(rain);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = root;
    }

    static ParticleSystem CompleteParticles(GameObject root, string name, int mode, Material material)
    {
        GameObject go = Child(root, name);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        bool created = !ps;
        if (created)
        {
            ps = Undo.AddComponent<ParticleSystem>(go);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = mode == 0;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = mode == 0 ? 6000 : mode == 1 ? 600 : 120;
            main.startSpeed = 0; main.startColor = Color.white;
            main.startLifetime = mode == 0 ? 11f / 7 : mode == 1 ? 0.25f : 0.7f;
            main.startSize = mode == 0 ? 0.007f : mode == 1 ? 0.009f : 0.22f;
            main.gravityModifier = mode == 1 ? 1 : 0;
            var emission = ps.emission;
            emission.enabled = mode == 0;
            emission.rateOverTime = mode == 0 ? 880 : 0;
            var shape = ps.shape;
            shape.enabled = mode == 0;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36, 11, 36);
            var collision = ps.collision; collision.enabled = false;
            var lights = ps.lights; lights.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            if (mode == 0)
            {
                go.transform.localPosition = new Vector3(0, 4.9f, 0);
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0.13f, 0.37f);
                velocity.y = new ParticleSystem.MinMaxCurve(-7.8f, -6.2f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.2f);
            }
            if (mode == 1)
            {
                var color = ps.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                color.color = gradient;
            }
            // Ripple expansion is shader-driven: 0.22 m quad, radius 0.14 -> 0.94.
            // AgePercent follows UV, so the shader reads normalized age from TEXCOORD0.z.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode == 0 ? ParticleSystemRenderMode.Stretch
                : mode == 2 ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 1;
            renderer.velocityScale = (0.28f - 0.007f) / 7;
            renderer.cameraVelocityScale = 0;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent });
            ConfigureRenderer(renderer);
        }
        var existingRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (existingRenderer && (created || !existingRenderer.sharedMaterial))
        {
            Undo.RecordObject(existingRenderer, "Assign rain material");
            existingRenderer.sharedMaterial = material;
        }
        return ps;
    }

    static void ConfigureRenderer(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    static GameObject CompleteOverlay(GameObject root)
    {
        GameObject go = Child(root, "Superficies molhadas");
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (!mf)
        {
            mf = Undo.AddComponent<MeshFilter>(go);
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Malhas/Chao_molhado.asset");
        }
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (!mr)
        {
            mr = Undo.AddComponent<MeshRenderer>(go);
            ConfigureRenderer(mr);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }
        if (!mr.sharedMaterial)
        {
            Undo.RecordObject(mr, "Assign wet overlay material");
            mr.sharedMaterial = GetMaterial("Chao molhado", "Unclaimed/Chao molhado", -1);
        }
        return go;
    }

    static Material GetMaterial(string name, string shaderName, int mode)
    {
        EnsureFolder(Folder + "/Materiais");
        string path = Folder + "/Materiais/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        Shader shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException("Missing shader: " + shaderName);
        material = new Material(shader) { name = name };
        if (mode >= 0)
        {
            material.SetFloat("_Modo", mode);
            material.SetFloat("_Opacidade", mode == 2 ? 0.3f : 0.35f);
            if (mode == 2) material.SetFloat("_AbrigoEpsilon", -0.025f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    [MenuItem(Menu + "Bake Shelter Map")]
    public static void BakeShelterMap()
    {
        Scene source = SceneManager.GetActiveScene();
        Scene preview = default;
        var timer = Stopwatch.StartNew();
        MapaAbrigoChuva temporaryMap = null;
        Texture2D temporaryTexture = null;
        try
        {
            preview = EditorSceneManager.NewPreviewScene();
            PhysicsScene physics = preview.GetPhysicsScene();
            // Never fall back to the active scene's colliders, including invisible barriers.
            if (!physics.IsValid() || physics.Equals(Physics.defaultPhysicsScene))
                throw new InvalidOperationException("Preview scene must own an isolated 3D physics scene.");
            var mirrors = new Dictionary<Transform, Transform>();
            var sources = new Dictionary<Collider, MeshRenderer>();
            foreach (MeshRenderer renderer in Renderers(source))
            {
                if (Excluded(renderer)) continue;
                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                Transform mirror = Mirror(renderer.transform, preview, mirrors);
                MeshCollider collider = mirror.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                sources.Add(collider, renderer);
            }
            // Synchronize newly-created static collider transforms before querying this local scene.
            Physics.SyncTransforms();
            int width = Mathf.CeilToInt((MaxX - MinX) / Cell);
            int height = Mathf.CeilToInt((MaxZ - MinZ) / Cell);
            int count = width * height;
            float[] heights = new float[count], exposure = new float[count];
            byte[] types = new byte[count];
            int[] counts = new int[5];
            for (int z = 0; z < height; z++)
            {
                if ((z & 7) == 0 && EditorUtility.DisplayCancelableProgressBar("Bake rain shelter", "Sampling geometry", (float)z / height * 0.8f)) return;
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    Vector3 origin = new Vector3(MinX + (x + 0.5f) * Cell, RayTop, MinZ + (z + 0.5f) * Cell);
                    TipoSuperficie type = TipoSuperficie.Nenhum;
                    heights[index] = -10;
                    if (physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayTop + 10, ~0, QueryTriggerInteraction.Ignore))
                    {
                        heights[index] = hit.point.y;
                        if (sources.TryGetValue(hit.collider, out MeshRenderer renderer))
                            type = Classify(HitMaterial(renderer, hit.triangleIndex), hit.normal.y);
                        else type = TipoSuperficie.Outro;
                        exposure[index] = IsGround(type) && hit.normal.y > 0.7f ? 1 : 0;
                    }
                    types[index] = (byte)type; counts[(int)type]++;
                }
            }
            if (EditorUtility.DisplayCancelableProgressBar("Bake rain shelter", "Blurring exposure and packing texture", 0.85f)) return;
            float[] depth = InteriorDepth(physics, sources, heights, width, height, source);
            float[] blurred = Blur(exposure, width, height);
            byte[] exposureBytes = new byte[count];
            Color[] pixels = new Color[count];
            for (int i = 0; i < count; i++)
            {
                exposureBytes[i] = (byte)Mathf.RoundToInt(blurred[i] * 255);
                pixels[i] = new Color(heights[i], exposureBytes[i] / 255f, types[i] / 255f, 1);
            }
            temporaryTexture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            { name = "Abrigo (altura, exposicao, tipo)", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            temporaryTexture.SetPixels(pixels); temporaryTexture.Apply(false, false);
            EnsureFolder(Folder);
            string path = Folder + "/MapaAbrigoChuva.asset";
            MapaAbrigoChuva map = AssetDatabase.LoadAssetAtPath<MapaAbrigoChuva>(path);
            if (!map)
            {
                temporaryMap = ScriptableObject.CreateInstance<MapaAbrigoChuva>();
                map = temporaryMap;
                AssetDatabase.CreateAsset(map, path);
                temporaryMap = null;
            }
            else Undo.RecordObject(map, "Update rain shelter map");
            map.origem = new Vector2(MinX, MinZ); map.celula = Cell;
            map.largura = width; map.altura = height;
            map.alturas = heights; map.tipos = types; map.exposicao = exposureBytes; map.profundidadeInterna = depth;
            if (map.textura && AssetDatabase.IsSubAsset(map.textura) && AssetDatabase.GetAssetPath(map.textura) == path)
            {
                // Copy into the existing sub-asset to preserve its local file ID.
                Undo.RecordObject(map.textura, "Update shelter texture");
                EditorUtility.CopySerialized(temporaryTexture, map.textura);
                Object.DestroyImmediate(temporaryTexture); temporaryTexture = null;
            }
            else
            {
                AssetDatabase.AddObjectToAsset(temporaryTexture, map);
                map.textura = temporaryTexture; temporaryTexture = null;
            }
            EditorUtility.SetDirty(map.textura); EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            GameObject root = FindRoot(source, RootName);
            ChuvaLeve rain = root ? root.GetComponent<ChuvaLeve>() : null;
            if (rain)
            {
                Undo.RecordObject(rain, "Assign shelter map"); rain.mapa = map;
                EditorUtility.SetDirty(rain); EditorSceneManager.MarkSceneDirty(source);
            }
            Debug.Log($"Rain shelter baked: {width} x {height}, {timer.Elapsed.TotalSeconds:F2}s. " +
                $"None {counts[0]}, hard {counts[1]}, soil {counts[2]}, vegetation {counts[3]}, other {counts[4]}.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            if (temporaryTexture) Object.DestroyImmediate(temporaryTexture);
            if (temporaryMap) Object.DestroyImmediate(temporaryMap);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            EditorUtility.ClearProgressBar();
        }
    }

    // Interior = covered cells over the building's own floors ("Pisos"); open patios stay outside.
    // Depth is the walking distance from outside through doorways (floor-reaching walls block it),
    // so standing inside next to a facade wall is still fully indoors.
    static float[] InteriorDepth(PhysicsScene physics, Dictionary<Collider, MeshRenderer> sources, float[] heights,
        int width, int height, Scene scene)
    {
        const float MaxDepth = 6, FloorProbe = 2f;
        int count = width * height;
        var interior = new bool[count];
        var wall = new bool[count];
        var hits = new RaycastHit[16];
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                Vector3 origin = new Vector3(MinX + (x + 0.5f) * Cell, FloorProbe, MinZ + (z + 0.5f) * Cell);
                int n = physics.Raycast(origin, Vector3.down, hits, FloorProbe + 1, ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < n; h++)
                    // The generated Pisos group also contains the service patio. A new outdoor canopy
                    // shelters its floor from drops without turning that floor into building interior.
                    if (sources.TryGetValue(hits[h].collider, out MeshRenderer renderer) && UnderGroup(renderer.transform, "Pisos")
                        && !renderer.name.StartsWith("Piso - Patio de servico", StringComparison.Ordinal)
                        && heights[index] > hits[h].point.y + 2) { interior[index] = true; break; }
            }
        GameObject building = FindRoot(scene, "Unclaimed - Necroterio (planta)");
        Transform walls = building ? building.transform.Find("Paredes") : null;
        if (walls)
            foreach (Renderer renderer in walls.GetComponentsInChildren<Renderer>(false))
            {
                Bounds b = renderer.bounds;
                if (b.min.y > 0.5f || b.size.y < 2) continue; // headers over doors do not block walking
                int x0 = Mathf.Max(0, Mathf.CeilToInt((b.min.x - MinX) / Cell - 0.5f)), x1 = Mathf.Min(width - 1, Mathf.FloorToInt((b.max.x - MinX) / Cell - 0.5f));
                int z0 = Mathf.Max(0, Mathf.CeilToInt((b.min.z - MinZ) / Cell - 0.5f)), z1 = Mathf.Min(height - 1, Mathf.FloorToInt((b.max.z - MinZ) / Cell - 0.5f));
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) wall[z * width + x] = true;
            }
        var depth = new float[count];
        for (int i = 0; i < count; i++) depth[i] = interior[i] && !wall[i] ? MaxDepth : 0;
        // Obstacle-aware chamfer passes (8-neighbour); a few sweeps converge for this floor plan.
        float diagonal = Cell * 1.41421356f;
        for (int pass = 0; pass < 40; pass++)
        {
            bool changed = false;
            for (int sweep = 0; sweep < 2; sweep++)
                for (int k = 0; k < count; k++)
                {
                    int i = sweep == 0 ? k : count - 1 - k;
                    if (depth[i] <= 0 || wall[i]) continue;
                    int x = i % width, z = i / width;
                    float best = depth[i];
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                            int j = nz * width + nx;
                            if (wall[j]) continue;
                            float candidate = depth[j] + (dx != 0 && dz != 0 ? diagonal : Cell);
                            if (candidate < best) best = candidate;
                        }
                    if (best < depth[i] - 1e-4f) { depth[i] = best; changed = true; }
                }
            if (!changed) break;
        }
        return depth;
    }

    static bool UnderGroup(Transform t, string group)
    {
        for (; t; t = t.parent) if (t.name == group) return true;
        return false;
    }

    // Mirror the complete TRS ancestry: unlike lossyScale decomposition, this preserves shear.
    static Transform Mirror(Transform source, Scene preview, Dictionary<Transform, Transform> mirrors)
    {
        if (mirrors.TryGetValue(source, out Transform existing)) return existing;
        Transform parent = source.parent ? Mirror(source.parent, preview, mirrors) : null;
        var go = new GameObject(source.name);
        SceneManager.MoveGameObjectToScene(go, preview);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = source.localPosition;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale = source.localScale;
        mirrors.Add(source, go.transform);
        return go.transform;
    }

    static Material HitMaterial(MeshRenderer renderer, int triangle)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        Material[] materials = renderer.sharedMaterials;
        int firstTriangle = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            var submesh = mesh.GetSubMesh(i);
            if (submesh.topology != MeshTopology.Triangles) continue;
            int triangleCount = submesh.indexCount / 3;
            if (triangle >= firstTriangle && triangle < firstTriangle + triangleCount)
                return materials.Length == 0 ? null : materials[Mathf.Min(i, materials.Length - 1)];
            firstTriangle += triangleCount;
        }
        return null;
    }

    static float[] Blur(float[] input, int width, int height)
    {
        var horizontal = new float[input.Length];
        var output = new float[input.Length];
        for (int z = 0; z < height; z++)
        {
            float sum = 0;
            for (int k = -BlurRadius; k <= BlurRadius; k++) if (k >= 0 && k < width) sum += input[z * width + k];
            for (int x = 0; x < width; x++)
            {
                horizontal[z * width + x] = sum / (BlurRadius * 2 + 1);
                int remove = x - BlurRadius, add = x + BlurRadius + 1;
                if (remove >= 0) sum -= input[z * width + remove];
                if (add < width) sum += input[z * width + add];
            }
        }
        for (int x = 0; x < width; x++)
        {
            float sum = 0;
            for (int k = -BlurRadius; k <= BlurRadius; k++) if (k >= 0 && k < height) sum += horizontal[k * width + x];
            for (int z = 0; z < height; z++)
            {
                output[z * width + x] = sum / (BlurRadius * 2 + 1);
                int remove = z - BlurRadius, add = z + BlurRadius + 1;
                if (remove >= 0) sum -= horizontal[remove * width + x];
                if (add < height) sum += horizontal[add * width + x];
            }
        }
        return output;
    }

    [MenuItem(Menu + "Build Wet Overlay")]
    public static void BuildWetOverlay()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject building = FindRoot(scene, "Unclaimed - Necroterio (planta)");
        Transform exterior = building ? building.transform.Find("Area externa") : null;
        if (!exterior) { Debug.LogWarning("Area externa was not found in the active scene."); return; }
        GameObject root = FindRoot(scene, RootName);
        if (!root) { Debug.LogWarning("Run Setup Rain Group first."); return; }
        Mesh temporaryMesh = null;
        try
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            MeshRenderer[] renderers = exterior.GetComponentsInChildren<MeshRenderer>(false);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Build wet overlay", renderers[r].name, (float)r / Mathf.Max(1, renderers.Length))) return;
                MeshRenderer renderer = renderers[r];
                if (Excluded(renderer)) continue;
                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                Mesh mesh = mf.sharedMesh;
                Vector3[] points = mesh.vertices;
                Material[] materials = renderer.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Material material = materials.Length == 0 ? null : materials[Mathf.Min(s, materials.Length - 1)];
                    if (Classify(material, 1) != TipoSuperficie.Duro || Normalize(material.name).Contains("grelha")
                        || mesh.GetSubMesh(s).topology != MeshTopology.Triangles) continue;
                    int[] indices = mesh.GetTriangles(s);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = renderer.localToWorldMatrix.MultiplyPoint3x4(points[indices[i]]);
                        Vector3 b = renderer.localToWorldMatrix.MultiplyPoint3x4(points[indices[i + 1]]);
                        Vector3 c = renderer.localToWorldMatrix.MultiplyPoint3x4(points[indices[i + 2]]);
                        if (Vector3.Cross(b - a, c - a).normalized.y <= 0.85f || (a.y + b.y + c.y) / 3 >= 0.8f) continue;
                        int first = vertices.Count;
                        vertices.Add(a); vertices.Add(b); vertices.Add(c);
                        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                    }
                }
            }
            if (triangles.Count == 0) { Debug.LogWarning("No eligible wet floor triangles were found; existing mesh retained."); return; }
            // Store world coordinates; the scene instance is reset to a world identity transform.
            temporaryMesh = new Mesh { name = "Chao molhado", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            temporaryMesh.SetVertices(vertices); temporaryMesh.SetTriangles(triangles, 0);
            temporaryMesh.RecalculateNormals(); temporaryMesh.RecalculateBounds();
            EnsureFolder(Folder + "/Malhas");
            string path = Folder + "/Malhas/Chao_molhado.asset";
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset)
            {
                Undo.RecordObject(asset, "Update wet floor mesh");
                EditorUtility.CopySerialized(temporaryMesh, asset);
                Object.DestroyImmediate(temporaryMesh); temporaryMesh = null;
                EditorUtility.SetDirty(asset);
            }
            else { asset = temporaryMesh; AssetDatabase.CreateAsset(asset, path); temporaryMesh = null; }
            GameObject overlay = CompleteOverlay(root);
            Undo.RecordObject(overlay.transform, "Position world-space overlay");
            // Preserve the root hierarchy while cancelling its current TRS for the world-space mesh.
            // Rain roots created by setup have identity rotation and scale.
            if (root.transform.rotation != Quaternion.identity || root.transform.lossyScale != Vector3.one)
            {
                // Transform vertices instead of changing the user's adjusted root.
                Mesh localMesh = Object.Instantiate(asset);
                Vector3[] localVertices = localMesh.vertices;
                Matrix4x4 inverse = overlay.transform.worldToLocalMatrix;
                for (int i = 0; i < localVertices.Length; i++) localVertices[i] = inverse.MultiplyPoint3x4(localVertices[i]);
                localMesh.vertices = localVertices; localMesh.RecalculateNormals(); localMesh.RecalculateBounds();
                EditorUtility.CopySerialized(localMesh, asset); Object.DestroyImmediate(localMesh);
                EditorUtility.SetDirty(asset);
            }
            else
            {
                overlay.transform.position = Vector3.zero;
                overlay.transform.rotation = Quaternion.identity;
                overlay.transform.localScale = Vector3.one;
            }
            MeshFilter filter = overlay.GetComponent<MeshFilter>();
            Undo.RecordObject(filter, "Assign wet floor mesh"); filter.sharedMesh = asset;
            MeshRenderer overlayRenderer = overlay.GetComponent<MeshRenderer>();
            Undo.RecordObject(overlayRenderer, "Configure wet floor renderer");
            overlayRenderer.sharedMaterial = GetMaterial("Chao molhado", "Unclaimed/Chao molhado", -1);
            ConfigureRenderer(overlayRenderer);
            Undo.RecordObject(overlay, "Clear wet overlay static flags");
            GameObjectUtility.SetStaticEditorFlags(overlay, 0);
            AssetDatabase.SaveAssetIfDirty(asset); EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Wet overlay built: {triangles.Count / 3} triangles.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            if (temporaryMesh) Object.DestroyImmediate(temporaryMesh);
            EditorUtility.ClearProgressBar();
        }
    }

    static IEnumerable<MeshRenderer> Renderers(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(false)) yield return renderer;
    }

    static bool Excluded(MeshRenderer renderer)
    {
        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) return true;
        for (Transform t = renderer.transform; t; t = t.parent)
            if (t.name == RootName || t.name == "Vegetacao") return true;
        if (WireName(renderer.name)) return true;
        foreach (Material material in renderer.sharedMaterials) if (material && WireName(material.name)) return true;
        return false;
    }
    static bool WireName(string name)
    {
        string value = Normalize(name);
        return value.Contains("cabo") || value.Contains("fiacao") || value.Contains("ramal");
    }
    static bool IsGround(TipoSuperficie type) => type == TipoSuperficie.Duro || type == TipoSuperficie.Terra || type == TipoSuperficie.Vegetacao;
    static TipoSuperficie Classify(Material material, float normalY)
    {
        if (!material) return TipoSuperficie.Outro;
        string name = Normalize(material.name);
        foreach (string term in new[] { "cimentado", "concreto", "calcada", "acesso de servico", "asfalto", "pintura branca", "grelha" })
            if (name.Contains(term)) return normalY > 0.7f ? TipoSuperficie.Duro : TipoSuperficie.Outro;
        if (name.Contains("terra") || name.Contains("brita")) return TipoSuperficie.Terra;
        if (name.Contains("grama") || name.Contains("gramado")) return TipoSuperficie.Vegetacao;
        return TipoSuperficie.Outro;
    }
    static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(char.ToLowerInvariant(c));
        return result.ToString();
    }
    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }
    static GameObject Child(GameObject parent, string name)
    {
        Transform child = parent.transform.Find(name);
        return child ? child.gameObject : CreateObject(name, parent.transform, parent.scene);
    }
    static GameObject CreateObject(string name, Transform parent, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        if (parent) Undo.SetTransformParent(go.transform, parent, "Parent " + name);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }
}
