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

public static class UnclaimedGramado
{
    const string Menu = "Tools/Unclaimed/Gramado/";
    const string Folder = "Assets/Modelos/Entorno/Vegetacao/Gramado";
    const string BuildingName = "Unclaimed - Necroterio (planta)";
    const string ParentPath = "Area externa/Vegetacao", RootName = "Gramado volumetrico";
    const int Seed = 73129, GeometryMask = 1, ObstacleMask = 2;
    const float MinX = -18.6f, MaxX = 21f, MinZ = -19f, MaxZ = 18f;
    const float ChunkSize = 4f, Cell = 0.1f, RayTop = 30f, RayLength = 40f;
    const float DenseDensity = 20f, SparseDensity = 4f, CandidateDensity = 28f;
    const float LargePatch = 2.5f, SmallPatch = 0.7f;
    // Approximate area coverage for the two-scale value-noise field, before edge thinning.
    const float DenseHole = 0.30f, SparseHole = 0.45f;
    const float DenseCoverage = 0.88f, SparseCoverage = 0.60f;
    const float EdgeWidth = 0.35f, EdgeClearance = 0.03f;
    const float ObstacleRadius = 0.1f, ObstacleHeight = 0.12f, TallGrassRadius = 0.4f;
    const float LodDistance = 12f, CullDistance = 40f, WindMargin = 0.15f;

    sealed class Surface
    {
        public int[] TriangleEnds;
        public byte[] Grass;
        public bool[] Soil;

        public int Submesh(int triangle)
        {
            if (triangle < 0) return -1;
            for (int i = 0; i < TriangleEnds.Length; i++)
                if (triangle < TriangleEnds[i]) return i;
            return -1;
        }

        public byte GrassAt(int triangle)
        {
            int index = Submesh(triangle);
            return index < 0 ? (byte)0 : Grass[index];
        }
    }

    sealed class Variant
    {
        public readonly Mesh[] Meshes = new Mesh[2];
        public readonly Matrix4x4[] Transforms = new Matrix4x4[2];
    }

    struct Tuft
    {
        public Vector3 Position, Scale;
        public float Yaw;
        public int Variant;
    }

    sealed class Chunk
    {
        public Vector2Int Key;
        public Vector3 Origin;
        public readonly List<Tuft> Tufts = new List<Tuft>();
        public readonly Mesh[] Meshes = new Mesh[2];
    }

    sealed class EdgeMap
    {
        public int Width, Height;
        public float X, Z;
        public float[] Distance;

        public float At(float x, float z)
        {
            int ix = Mathf.Clamp(Mathf.RoundToInt((x - X) / Cell), 0, Width - 1);
            int iz = Mathf.Clamp(Mathf.RoundToInt((z - Z) / Cell), 0, Height - 1);
            // Subtract the cell half-diagonal and the sample offset conservatively.
            float offset = Vector2.Distance(new Vector2(x, z), new Vector2(X + ix * Cell, Z + iz * Cell));
            return Mathf.Max(0, Distance[iz * Width + ix] - Cell * 0.707107f - offset);
        }
    }

    [MenuItem(Menu + "Build Lawn Grass")]
    static void BuildAll() => Debug.Log(Build(null));

    [MenuItem(Menu + "Build Lawn Grass (pilot)")]
    static void BuildPilot() => Debug.Log(Build(new Rect(-16f, -18.5f, 3.3f, 4.2f)));

    public static string Build(Rect? regiao)
    {
        var timer = Stopwatch.StartNew();
        Scene scene = SceneManager.GetActiveScene(), preview = default;
        var chunks = new SortedDictionary<Vector2Int, Chunk>(Comparer<Vector2Int>.Create((a, b) =>
        {
            int order = a.y.CompareTo(b.y);
            return order != 0 ? order : a.x.CompareTo(b.x);
        }));
        var obstacleMeshes = new List<Mesh>();
        int undo = -1;
        try
        {
            Transform parent = FindParent(scene);
            ValidateParent(parent);
            Transform previous = parent.Find(RootName);
            if (regiao.HasValue && previous) ValidateParent(previous);
            Rect area = SearchArea(regiao);
            if (area.width <= 0 || area.height <= 0) return "Gramado: região fora do lote; nada alterado.";
            Variant[] variants = LoadVariants(out Material material);
            preview = EditorSceneManager.NewPreviewScene();
            PhysicsScene physics = preview.GetPhysicsScene();
            if (!physics.IsValid() || physics.Equals(Physics.defaultPhysicsScene))
                throw new InvalidOperationException("Preview scene must own an isolated 3D physics scene.");
            Dictionary<Collider, Surface> surfaces = MirrorGeometry(scene, preview, obstacleMeshes);
            Physics.SyncTransforms();
            EdgeMap edges = SampleEdges(physics, surfaces, area);
            int[] counts = Scatter(physics, surfaces, edges, area, chunks);
            long[] triangles = Combine(chunks, variants);
            Progress("Ready to replace generated chunks", 0.99f);

            // All cancelable work finishes before deleting any previous output.
            EnsureFolder(Folder);
            Undo.IncrementCurrentGroup();
            undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build lawn grass");
            ClearOutput(parent, regiao.HasValue ? (Rect?)area : null);
            Transform root = parent.Find(RootName);
            if (!root) root = CreateObject(RootName, parent, scene).transform;
            foreach (Chunk chunk in chunks.Values) CreateChunk(chunk, root, scene, material);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"Gramado: Gramado {counts[1]} tufos; Grama rala {counts[2]} tufos; " +
                $"{chunks.Count} chunks; triângulos LOD0 {triangles[0]}, LOD1 {triangles[1]}; {timer.Elapsed.TotalSeconds:F2}s.";
        }
        catch (OperationCanceledException)
        {
            return $"Gramado: cancelado; gramado anterior preservado ({timer.Elapsed.TotalSeconds:F2}s).";
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return "Gramado: falha — " + exception.Message;
        }
        finally
        {
            if (undo >= 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
            }
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (Mesh mesh in obstacleMeshes) if (mesh) Object.DestroyImmediate(mesh);
            foreach (Chunk chunk in chunks.Values)
                for (int lod = 0; lod < 2; lod++)
                    if (chunk.Meshes[lod] && !EditorUtility.IsPersistent(chunk.Meshes[lod]))
                        Object.DestroyImmediate(chunk.Meshes[lod]);
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(Menu + "Clear Lawn Grass")]
    static void Clear()
    {
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform parent = FindParent(scene);
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear lawn grass");
            try { ClearOutput(parent, null); EditorSceneManager.MarkSceneDirty(scene); }
            finally { Undo.CollapseUndoOperations(undo); }
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    static Transform FindParent(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "PrototipoNecroterio")
            throw new InvalidOperationException("Activate PrototipoNecroterio before building or clearing lawn grass.");
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == BuildingName)
            {
                Transform parent = root.transform.Find(ParentPath);
                if (parent) return parent;
            }
        throw new InvalidOperationException("Missing hierarchy: " + BuildingName + "/" + ParentPath);
    }

    static void ValidateParent(Transform parent)
    {
        // Generated meshes use metre-sized, world-aligned chunks. Avoid silently distorting them.
        Matrix4x4 matrix = parent.localToWorldMatrix;
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                if (Mathf.Abs(matrix[row, column] - (row == column ? 1f : 0f)) > 0.0001f)
                    throw new InvalidOperationException("Lawn parent ancestry must have world identity rotation and scale.");
    }

    static Rect SearchArea(Rect? region)
    {
        if (!region.HasValue) return Rect.MinMaxRect(MinX, MinZ, MaxX, MaxZ);
        Rect r = region.Value;
        if (float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.width) || float.IsNaN(r.height) ||
            float.IsInfinity(r.x) || float.IsInfinity(r.y) || float.IsInfinity(r.width) || float.IsInfinity(r.height) ||
            r.width <= 0 || r.height <= 0)
            throw new ArgumentException("Region must be a finite, positive-size world XZ rectangle.");
        return Rect.MinMaxRect(Mathf.Max(MinX, r.xMin), Mathf.Max(MinZ, r.yMin),
            Mathf.Min(MaxX, r.xMax), Mathf.Min(MaxZ, r.yMax));
    }

    static Variant[] LoadVariants(out Material material)
    {
        var variants = new Variant[4];
        material = null;
        for (int i = 0; i < variants.Length; i++)
        {
            string name = "Grama_Tufo_" + (char)('A' + i);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedEntornoAssets.PrefabPath(name));
            if (!prefab) throw new InvalidOperationException("Missing grass prefab: " + name);
            var variant = new Variant();
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int lod = 0; lod < 2; lod++)
            {
                foreach (MeshFilter filter in filters)
                    if (filter.name == name + "_LOD" + lod)
                    {
                        variant.Meshes[lod] = filter.sharedMesh;
                        variant.Transforms[lod] = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        if (i == 0 && lod == 0)
                        {
                            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                            if (renderer) material = renderer.sharedMaterial;
                        }
                        break;
                    }
                Mesh mesh = variant.Meshes[lod];
                // Editor scripts can read and combine imported meshes even when isReadable is off (kept off for builds).
                if (!mesh || mesh.subMeshCount != 1 || !mesh.HasVertexAttribute(VertexAttribute.Color))
                    throw new InvalidOperationException("Grass LOD must be single-submesh and have wind vertex colors: " + name + "_LOD" + lod);
            }
            variants[i] = variant;
        }
        if (!material) throw new InvalidOperationException("Missing Grama_Tufo_A LOD0 material.");
        return variants;
    }

    static Dictionary<Collider, Surface> MirrorGeometry(Scene scene, Scene preview, List<Mesh> temporaryMeshes)
    {
        var mirrors = new Dictionary<Transform, Transform>();
        var surfaces = new Dictionary<Collider, Surface>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Progress("Mirroring geometry", 0.05f * r / Mathf.Max(1, roots.Length));
            MeshRenderer[] renderers = roots[r].GetComponentsInChildren<MeshRenderer>(false);
            for (int index = 0; index < renderers.Length; index++)
            {
                if ((index & 63) == 0) Progress("Mirroring geometry", 0.05f * r / Mathf.Max(1, roots.Length));
                MeshRenderer renderer = renderers[index];
                if (Excluded(renderer)) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                Mesh mesh = filter.sharedMesh;
                Material[] materials = renderer.sharedMaterials;
                var surface = new Surface { TriangleEnds = new int[mesh.subMeshCount],
                    Grass = new byte[mesh.subMeshCount], Soil = new bool[mesh.subMeshCount] };
                int triangleEnd = 0, obstacleCount = 0;
                var obstacleParts = new List<CombineInstance>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var descriptor = mesh.GetSubMesh(sub);
                    if (descriptor.topology == MeshTopology.Triangles) triangleEnd += descriptor.indexCount / 3;
                    surface.TriangleEnds[sub] = triangleEnd;
                    Material mat = materials.Length == 0 ? null : materials[Mathf.Min(sub, materials.Length - 1)];
                    string name = mat ? Normalize(mat.name) : "";
                    surface.Grass[sub] = name == "gramado" ? (byte)1 : name == "grama rala" ? (byte)2 : (byte)0;
                    surface.Soil[sub] = surface.Grass[sub] != 0 || name.Contains("terra");
                    if (!surface.Soil[sub] && descriptor.topology == MeshTopology.Triangles && descriptor.indexCount > 0)
                    {
                        obstacleCount++;
                        obstacleParts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = Matrix4x4.identity });
                    }
                }
                Transform mirror = Mirror(renderer.transform, preview, mirrors);
                mirror.gameObject.layer = 0;
                MeshCollider collider = mirror.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                surfaces.Add(collider, surface);
                if (obstacleCount == 0) continue;
                // Dedicated layer: overlap tests never include soil, even in mixed-material meshes.
                var obstacle = new GameObject("Lawn obstacle");
                SceneManager.MoveGameObjectToScene(obstacle, preview);
                obstacle.transform.SetParent(mirror, false);
                obstacle.layer = 1;
                MeshCollider blocker = obstacle.AddComponent<MeshCollider>();
                if (obstacleCount == mesh.subMeshCount) blocker.sharedMesh = mesh;
                else
                {
                    var obstacleMesh = new Mesh { name = "Lawn obstacle", indexFormat = IndexFormat.UInt32 };
                    temporaryMeshes.Add(obstacleMesh);
                    obstacleMesh.CombineMeshes(obstacleParts.ToArray(), true, false);
                    blocker.sharedMesh = obstacleMesh;
                }
            }
        }
        return surfaces;
    }

    // Same complete-ancestry mirror as the rain baker; preserves nested TRS and shear.
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

    static bool Sample(PhysicsScene physics, Dictionary<Collider, Surface> surfaces, float x, float z,
        out RaycastHit hit, out byte type)
    {
        type = 0;
        if (!physics.Raycast(new Vector3(x, RayTop, z), Vector3.down, out hit, RayLength,
            GeometryMask, QueryTriggerInteraction.Ignore) || hit.normal.y <= 0.85f) return false;
        if (surfaces.TryGetValue(hit.collider, out Surface surface)) type = surface.GrassAt(hit.triangleIndex);
        return type != 0;
    }

    static EdgeMap SampleEdges(PhysicsScene physics, Dictionary<Collider, Surface> surfaces, Rect area)
    {
        // World-aligned halo prevents artificial borders along the pilot rectangle.
        float x0 = Mathf.Floor((area.xMin - EdgeWidth - Cell * 2) / Cell) * Cell;
        float z0 = Mathf.Floor((area.yMin - EdgeWidth - Cell * 2) / Cell) * Cell;
        int width = Mathf.CeilToInt((area.xMax + EdgeWidth + Cell * 2 - x0) / Cell) + 1;
        int height = Mathf.CeilToInt((area.yMax + EdgeWidth + Cell * 2 - z0) / Cell) + 1;
        var map = new EdgeMap { Width = width, Height = height, X = x0, Z = z0, Distance = new float[width * height] };
        for (int z = 0; z < height; z++)
        {
            if ((z & 7) == 0) Progress("Classifying lawn edges", 0.05f + 0.5f * z / height);
            for (int x = 0; x < width; x++)
            {
                float wx = x0 + x * Cell, wz = z0 + z * Cell;
                bool inside = wx >= MinX && wx < MaxX && wz >= MinZ && wz < MaxZ;
                map.Distance[z * width + x] = inside && Sample(physics, surfaces, wx, wz, out _, out _) ? 1000f : 0;
            }
        }
        float diagonal = Cell * 1.41421356f;
        for (int sweep = 0; sweep < 2; sweep++)
            for (int k = 0; k < map.Distance.Length; k++)
            {
                int i = sweep == 0 ? k : map.Distance.Length - 1 - k;
                int x = i % width, z = i / width, direction = sweep == 0 ? -1 : 1;
                float best = map.Distance[i];
                int nx = x + direction, nz = z + direction;
                if (nx >= 0 && nx < width) best = Mathf.Min(best, map.Distance[z * width + nx] + Cell);
                if (nz >= 0 && nz < height)
                {
                    best = Mathf.Min(best, map.Distance[nz * width + x] + Cell);
                    if (x > 0) best = Mathf.Min(best, map.Distance[nz * width + x - 1] + diagonal);
                    if (x + 1 < width) best = Mathf.Min(best, map.Distance[nz * width + x + 1] + diagonal);
                }
                map.Distance[i] = best;
            }
        return map;
    }

    static int[] Scatter(PhysicsScene physics, Dictionary<Collider, Surface> surfaces, EdgeMap edges,
        Rect area, SortedDictionary<Vector2Int, Chunk> chunks)
    {
        float step = 1f / Mathf.Sqrt(CandidateDensity);
        int x0 = Mathf.FloorToInt(area.xMin / step), x1 = Mathf.CeilToInt(area.xMax / step);
        int z0 = Mathf.FloorToInt(area.yMin / step), z1 = Mathf.CeilToInt(area.yMax / step);
        var overlap = new Collider[1];
        var counts = new int[3];
        for (int z = z0; z < z1; z++)
        {
            if ((z & 7) == 0) Progress("Scattering grass tufts", 0.55f + 0.25f * (z - z0) / Mathf.Max(1, z1 - z0));
            for (int x = x0; x < x1; x++)
            {
                // Bounded jitter leaves a minimum centre spacing of 0.3 grid cells.
                float wx = (x + 0.5f + (Random01(x, z, 1) - 0.5f) * 0.7f) * step;
                float wz = (z + 0.5f + (Random01(x, z, 2) - 0.5f) * 0.7f) * step;
                if (!area.Contains(new Vector2(wx, wz))) continue;
                float distance = edges.At(wx, wz);
                if (distance < EdgeClearance) continue;
                float mask = 0.7f * Noise(wx / LargePatch, wz / LargePatch, 11)
                    + 0.3f * Noise(wx / SmallPatch, wz / SmallPatch, 23);
                if (!Sample(physics, surfaces, wx, wz, out RaycastHit hit, out byte type)) continue;
                float threshold = type == 1 ? DenseHole : SparseHole;
                if (mask < threshold) continue;
                float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / EdgeWidth));
                float density = type == 1 ? DenseDensity : SparseDensity;
                float coverage = type == 1 ? DenseCoverage : SparseCoverage;
                float chance = density / CandidateDensity / coverage * Mathf.Lerp(0.65f, 1.35f, mask)
                    * Mathf.Lerp(0.35f, 1, edge);
                if (Random01(x, z, 3) >= chance) continue;
                if (!ClearEdgeRing(physics, surfaces, wx, wz)) continue;
                Vector3 probe = hit.point + Vector3.up * ObstacleHeight;
                if (physics.OverlapSphere(probe, ObstacleRadius, overlap, ObstacleMask, QueryTriggerInteraction.Ignore) > 0) continue;
                float height = Mathf.Lerp(0.6f, 1.3f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(threshold, 1, mask)));
                height *= Mathf.Lerp(0.65f, 1, edge);
                // Nested non-alloc overlaps approximate nearest-object distance in three rings.
                float tall = 1;
                if (physics.OverlapSphere(probe, TallGrassRadius, overlap, ObstacleMask, QueryTriggerInteraction.Ignore) > 0)
                {
                    tall = 1.08f;
                    if (physics.OverlapSphere(probe, 0.3f, overlap, ObstacleMask, QueryTriggerInteraction.Ignore) > 0) tall = 1.16f;
                    if (physics.OverlapSphere(probe, 0.2f, overlap, ObstacleMask, QueryTriggerInteraction.Ignore) > 0) tall = 1.25f;
                }
                float scale = Mathf.Lerp(0.85f, 1.15f, Random01(x, z, 4));
                var key = new Vector2Int(Mathf.FloorToInt(wx / ChunkSize), Mathf.FloorToInt(wz / ChunkSize));
                if (!chunks.TryGetValue(key, out Chunk chunk))
                {
                    chunk = new Chunk { Key = key, Origin = new Vector3((key.x + 0.5f) * ChunkSize, hit.point.y, (key.y + 0.5f) * ChunkSize) };
                    chunks.Add(key, chunk);
                }
                chunk.Tufts.Add(new Tuft { Position = hit.point - Vector3.up * 0.01f,
                    Scale = new Vector3(scale, scale * height * tall, scale),
                    Yaw = Random01(x, z, 5) * 360, Variant = Mathf.Min(3, (int)(Random01(x, z, 6) * 4)) });
                counts[type]++;
            }
        }
        return counts;
    }

    static bool ClearEdgeRing(PhysicsScene physics, Dictionary<Collider, Surface> surfaces, float x, float z)
    {
        // Resolve the final 3 cm against geometry rather than relying only on the 10 cm map.
        // Eight probes also catch narrow paving/drain details that fall between map samples.
        const float Diagonal = EdgeClearance * 0.70710678f;
        for (int i = 0; i < 8; i++)
        {
            float dx, dz;
            switch (i)
            {
                case 0: dx = EdgeClearance; dz = 0; break;
                case 1: dx = -EdgeClearance; dz = 0; break;
                case 2: dx = 0; dz = EdgeClearance; break;
                case 3: dx = 0; dz = -EdgeClearance; break;
                case 4: dx = Diagonal; dz = Diagonal; break;
                case 5: dx = -Diagonal; dz = Diagonal; break;
                case 6: dx = Diagonal; dz = -Diagonal; break;
                default: dx = -Diagonal; dz = -Diagonal; break;
            }
            if (x + dx < MinX || x + dx >= MaxX || z + dz < MinZ || z + dz >= MaxZ ||
                !Sample(physics, surfaces, x + dx, z + dz, out _, out _)) return false;
        }
        return true;
    }

    static long[] Combine(SortedDictionary<Vector2Int, Chunk> chunks, Variant[] variants)
    {
        var triangles = new long[2];
        int index = 0;
        foreach (Chunk chunk in chunks.Values)
        {
            Progress("Combining grass chunks", 0.8f + 0.18f * index++ / Mathf.Max(1, chunks.Count));
            for (int lod = 0; lod < 2; lod++)
            {
                var instances = new CombineInstance[chunk.Tufts.Count];
                long vertices = 0;
                for (int i = 0; i < instances.Length; i++)
                {
                    Tuft tuft = chunk.Tufts[i];
                    Variant variant = variants[tuft.Variant];
                    Mesh mesh = variant.Meshes[lod];
                    vertices += mesh.vertexCount;
                    triangles[lod] += (long)mesh.GetIndexCount(0) / 3;
                    instances[i] = new CombineInstance { mesh = mesh, subMeshIndex = 0,
                        transform = Matrix4x4.TRS(tuft.Position - chunk.Origin, Quaternion.Euler(0, tuft.Yaw, 0), tuft.Scale)
                            * variant.Transforms[lod] };
                }
                var combined = new Mesh { name = ChunkName(chunk.Key) + "_LOD" + lod,
                    indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                chunk.Meshes[lod] = combined;
                // CombineMeshes copies vertex colors unchanged: RGBA encodes per-blade wind weights.
                combined.CombineMeshes(instances, true, true, false);
                combined.RecalculateBounds();
            }
        }
        return triangles;
    }

    static void CreateChunk(Chunk chunk, Transform root, Scene scene, Material material)
    {
        GameObject go = CreateObject(ChunkName(chunk.Key), root, scene);
        go.transform.position = chunk.Origin;
        var renderers = new Renderer[2];
        for (int lod = 0; lod < 2; lod++)
        {
            Mesh mesh = chunk.Meshes[lod];
            AssetDatabase.CreateAsset(mesh, Folder + "/" + mesh.name + ".asset");
            AssetDatabase.SaveAssetIfDirty(mesh);
            GameObject child = CreateObject("LOD" + lod, go.transform, scene);
            Undo.AddComponent<MeshFilter>(child).sharedMesh = mesh;
            MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(child);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.receiveGI = ReceiveGI.LightProbes;
            renderers[lod] = renderer;
        }
        LODGroup group = Undo.AddComponent<LODGroup>(go);
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[] { new LOD(0.2f, new[] { renderers[0] }), new LOD(0.05f, new[] { renderers[1] }) });
        group.RecalculateBounds();
        float projection = 2 * Mathf.Tan(30 * Mathf.Deg2Rad);
        float near = Mathf.Clamp(group.size / (projection * LodDistance), 0.0002f, 0.99f);
        float far = Mathf.Clamp(group.size / (projection * CullDistance), 0.0001f, near - 0.0001f);
        group.SetLODs(new[] { new LOD(near, new[] { renderers[0] }), new LOD(far, new[] { renderers[1] }) });
        // Configure while inactive so ExecuteAlways.OnEnable sees the requested margin immediately.
        go.SetActive(false);
        LimitesVento wind = Undo.AddComponent<LimitesVento>(go);
        wind.margem = WindMargin;
        go.SetActive(true);
    }

    static void ClearOutput(Transform parent, Rect? region)
    {
        Transform root = parent.Find(RootName);
        if (root)
        {
            if (!region.HasValue) Undo.DestroyObjectImmediate(root.gameObject);
            else
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.GetChild(i);
                    if (TryChunkKey(child.name, out Vector2Int key) && ChunkRect(key).Overlaps(region.Value))
                        Undo.DestroyObjectImmediate(child.gameObject);
                }
        }
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { Folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            int slash = path.LastIndexOf('/');
            if (path.Substring(0, slash) != Folder || !path.EndsWith(".asset", StringComparison.Ordinal)) continue;
            string name = path.Substring(slash + 1, path.Length - slash - 7);
            if (!name.EndsWith("_LOD0", StringComparison.Ordinal) && !name.EndsWith("_LOD1", StringComparison.Ordinal)) continue;
            if (!TryChunkKey(name.Substring(0, name.Length - 5), out Vector2Int key)) continue;
            if (!region.HasValue || ChunkRect(key).Overlaps(region.Value))
                if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("Cannot delete generated mesh: " + path);
        }
    }

    static bool TryChunkKey(string name, out Vector2Int key)
    {
        key = default;
        string[] parts = name.Split('_');
        if (parts.Length != 3 || parts[0] != "Gramado" ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) return false;
        key = new Vector2Int(x, z);
        return name == ChunkName(key);
    }

    static string ChunkName(Vector2Int key) => "Gramado_" + key.x.ToString(CultureInfo.InvariantCulture) + "_" + key.y.ToString(CultureInfo.InvariantCulture);
    static Rect ChunkRect(Vector2Int key) => new Rect(key.x * ChunkSize, key.y * ChunkSize, ChunkSize, ChunkSize);

    static GameObject CreateObject(string name, Transform parent, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetParent(parent, false);
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, separator));
        AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
    }

    static float Random01(int x, int z, int salt)
    {
        unchecked
        {
            uint value = (uint)x * 374761393u + (uint)z * 668265263u + (uint)(Seed + salt * 1013) * 2246822519u;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777216f;
        }
    }

    static float Noise(float x, float z, int salt)
    {
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        float tx = Mathf.SmoothStep(0, 1, x - ix), tz = Mathf.SmoothStep(0, 1, z - iz);
        return Mathf.Lerp(Mathf.Lerp(Random01(ix, iz, salt), Random01(ix + 1, iz, salt), tx),
            Mathf.Lerp(Random01(ix, iz + 1, salt), Random01(ix + 1, iz + 1, salt), tx), tz);
    }

    // Matches UnclaimedChuva.Excluded, including wiring and the rain/vegetation ancestors.
    static bool Excluded(MeshRenderer renderer)
    {
        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) return true;
        for (Transform t = renderer.transform; t; t = t.parent)
            if (t.name == "Chuva leve" || t.name == "Vegetacao") return true;
        if (WireName(renderer.name)) return true;
        foreach (Material material in renderer.sharedMaterials) if (material && WireName(material.name)) return true;
        return false;
    }

    static bool WireName(string name)
    {
        string value = Normalize(name);
        return value.Contains("cabo") || value.Contains("fiacao") || value.Contains("ramal");
    }

    static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(char.ToLowerInvariant(c));
        return result.ToString();
    }

    static void Progress(string message, float progress)
    {
        if (EditorUtility.DisplayCancelableProgressBar("Build lawn grass", message, progress))
            throw new OperationCanceledException();
    }
}
