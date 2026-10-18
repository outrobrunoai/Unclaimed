// Planting of the exterior with the vegetation kit (Tools > Unclaimed > Build Entorno Prefabs): explicit trees,
// bananas and shrubs, then seeded scatter of grasses, weeds and leaf litter, tidier in the front yard and rougher
// behind the barriers. Conventions in UnclaimedMorgueGenerator.Entorno.cs.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class UnclaimedMorgueGenerator
{
    private static readonly HashSet<string> missingPlants = new HashSet<string>();
    private static readonly List<Rect> neighbourRoofs = new List<Rect>();
    private static List<Rect> noPlant;
    // Spots kept clear of scattered ground cover without disturbing the seeded layout: the garden post's footing.
    private static readonly List<Rect> noScatter = new List<Rect> { Rect.MinMaxRect(-13.6f, -17.4f, -12.4f, -16.2f) };
    private static readonly List<Vector3> trunks = new List<Vector3>();

    // Ground height under a point (matches the ground meshes closely enough to seat a plant).
    private static float GroundY(float x, float z)
    {
        if (z < StreetNear)
        {
            if (z > CurbNear) return z < -20.8f ? SidewalkTop - 0.02f : SidewalkTop;
            if (z < CurbFar && z > StreetFar) return SidewalkTop;
            if (z >= CurbFar) return AsphaltTop;
        }
        if (x > LotW && x < LotE && z > LotS && z < LotN)
        {
            bool front = z < SideGateZ && x < EastRailX;
            return front ? -0.05f + 0.015f * GroundNoise(x, z) : -0.06f + 0.035f * GroundNoise(x, z);
        }
        return 0.025f * GroundNoise(x, z);
    }

    private static GameObject Plant(Transform parent, string prefabName, float x, float z, float yaw, float scale,
        float tiltX = 0f, float tiltZ = 0f, float y = float.NaN)
    {
        string ornamental = prefabName == "Espada_Sao_Jorge" ? (yaw > 180f ? "Espada_Ornamental_A" : "Espada_Ornamental_B") :
            prefabName.StartsWith("Arbusto_Redondo") && z > -11.9f && z < -11.3f && x > -6.4f && x < 3.6f ? "Filodendro_Ornamental" : null;
        var prefab = ornamental != null && UnclaimedExteriorFinishes.Available(ornamental)
            ? AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedExteriorFinishes.PrefabPath(ornamental))
            : AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedEntornoAssets.PrefabPath(prefabName));
        if (prefab == null)
        {
            missingPlants.Add(prefabName);
            return null;
        }
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + prefabName);
        float ground = float.IsNaN(y) ? GroundY(x, z) - 0.01f : y;
        obj.transform.SetPositionAndRotation(new Vector3(x, ground, z), Quaternion.Euler(tiltX, yaw, tiltZ));
        obj.transform.localScale = Vector3.one * scale;
        return obj;
    }

    private static bool Plantable(float x, float z, float radius)
    {
        if (Inside(noPlant, x, z, radius)) return false;
        foreach (Vector3 t in trunks)
            if ((new Vector2(x - t.x, z - t.z)).sqrMagnitude < (t.y + radius) * (t.y + radius)) return false;
        return true;
    }

    // Seeded scatter inside `area` with a minimum spacing between plants of this call (and earlier calls sharing `taken`).
    private static void Scatter(Transform parent, System.Random rng, string[] prefabs, Rect area, int count, float spacing,
        float scaleMin, float scaleMax, float tiltMax, List<Vector2> taken, float clearance = 0.15f)
    {
        int placed = 0;
        for (int attempt = 0; attempt < count * 40 && placed < count; attempt++)
        {
            float x = Mathf.Lerp(area.xMin, area.xMax, (float)rng.NextDouble());
            float z = Mathf.Lerp(area.yMin, area.yMax, (float)rng.NextDouble());
            if (!Plantable(x, z, clearance)) continue;
            bool close = false;
            foreach (Vector2 p in taken)
                if ((p - new Vector2(x, z)).sqrMagnitude < spacing * spacing) { close = true; break; }
            if (close) continue;
            taken.Add(new Vector2(x, z));
            float tilt = tiltMax * (float)(rng.NextDouble() * 2 - 1), tilt2 = tiltMax * (float)(rng.NextDouble() * 2 - 1);
            string prefab = prefabs[rng.Next(prefabs.Length)];
            float yaw = (float)rng.NextDouble() * 360f, scale = Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble());
            // Dropped after every draw so the seeded layout around it stays the same.
            if (!Inside(noScatter, x, z, 0f)) Plant(parent, prefab, x, z, yaw, scale, tilt, tilt2);
            placed++;
        }
    }

    // A few clusters of 2-4 plants along a line (edges of lawns, foot of walls).
    private static void Clusters(Transform parent, System.Random rng, string[] prefabs, Vector2 a, Vector2 b, int clusters,
        float scaleMin, float scaleMax, float tiltMax, List<Vector2> taken)
    {
        for (int c = 0; c < clusters; c++)
        {
            Vector2 centre = Vector2.Lerp(a, b, (c + 0.2f + 0.6f * (float)rng.NextDouble()) / clusters);
            Scatter(parent, rng, prefabs, Rect.MinMaxRect(centre.x - 0.45f, centre.y - 0.45f, centre.x + 0.45f, centre.y + 0.45f),
                2 + rng.Next(3), 0.28f, scaleMin, scaleMax, tiltMax, taken);
        }
    }

    private static void BuildVegetation(Transform veg)
    {
        missingPlants.Clear();
        trunks.Clear();
        Transform trees = Sub(veg, "Arvores"), shrubs = Sub(veg, "Arbustos e bananeiras"), cover = Sub(veg, "Forracao"),
            litter = Sub(veg, "Folhas secas"), vines = Sub(veg, "Trepadeiras"), far = Sub(veg, "Silhuetas distantes");

        // Where nothing grows: the building, paving, walls, gates' swing, drains, poles.
        noPlant = new List<Rect>();
        foreach (Rect r in Footprint()) noPlant.Add(Rect.MinMaxRect(r.xMin - 0.25f, r.yMin - 0.25f, r.xMax + 0.25f, r.yMax + 0.25f));
        noPlant.AddRange(new[]
        {
            Rect.MinMaxRect(BuildingWest - 0.05f, WalkS - 0.15f, ArchiveEast + 0.15f, ReceptionFace),   // walkway and curbs
            Rect.MinMaxRect(-13.35f, LandingS - 0.1f, -9.4f, ReceptionFace),                            // landing, channel
            Rect.MinMaxRect(-12.55f, LotS, -10.25f, LandingS),                                           // path and curbs
            Rect.MinMaxRect(-15.45f, SideGateZ - 0.1f, BuildingWest, 1.6f),                              // apron west
            Rect.MinMaxRect(-16.35f, -16.15f, -15.65f, -14.25f),                                         // bench
            Rect.MinMaxRect(LotW - 0.1f, LotS - 0.3f, LotW + 0.4f, LotN + 0.1f),                          // west wall
            Rect.MinMaxRect(LotW, LotN - 0.4f, LotE, LotN + 0.1f),                                        // north wall
            Rect.MinMaxRect(LotE - 0.35f, LotS, LotE + 0.2f, LotN),                                       // east wall
            Rect.MinMaxRect(LotW, LotS - 0.3f, LotE, LotS + 0.3f),                                        // street front
            Rect.MinMaxRect(EastRailX - 0.25f, LotS, EastRailX + 0.25f, AutopsyFace),                     // east railing
            Rect.MinMaxRect(LotW, SideGateZ - 0.7f, BuildingWest, SideGateZ + 0.7f),                      // side gate and its swing
            Rect.MinMaxRect(EastRailX - 0.8f, -16.2f, EastRailX + 0.8f, -14.0f),                          // east pedestrian gate
            Rect.MinMaxRect(13.8f, LotS, LotE, 5.6f), Rect.MinMaxRect(8.4f, -8.8f, 14f, AutopsyFace),      // drive, carport
            Rect.MinMaxRect(10.6f, -18.75f, 13.8f, -15.55f),                                              // gatehouse
            Rect.MinMaxRect(-7.75f, LotS, -6.4f, -18.3f), Rect.MinMaxRect(-13.25f, -17.05f, -12.75f, -16.55f), // posts
            Rect.MinMaxRect(-60f, StreetFar - 0.2f, 62f, -20.8f),                                         // street and paved sidewalks
            Rect.MinMaxRect(-60f, -20.8f, 62f, StreetNear + 0.3f),
        });
        foreach (Vector3 face in new[] { new Vector3(-14.6f, 0, ReceptionFace - 0.32f), new Vector3(-6.75f, 0, ReceptionFace - 0.32f),
                     new Vector3(3.3f, 0, ArchiveFace - 0.32f), new Vector3(ArchiveEast + 0.32f, 0, -6f), new Vector3(BuildingWest - 0.32f, 0, -4.2f),
                     new Vector3(-13.42f, 0, 0.5f), new Vector3(0f, 0, 9.42f), new Vector3(14.6f, 0, AutopsyFace - 0.32f) })
            noPlant.Add(Rect.MinMaxRect(face.x - 0.35f, face.z - 0.35f, face.x + 0.35f, face.z + 0.35f));
        foreach (Rect r in neighbourRoofs) noPlant.Add(r);

        // Trees: one big shade tree per side of the front yard, smaller ones by the street, more behind the walls.
        var treeList = new (string name, float x, float z, float scale)[]
        {
            ("Arvore_Sibipiruna_A", -16.2f, -17.7f, 0.9f), ("Arvore_Pequena", -4.5f, -16.0f, 1.0f), ("Arvore_Sibipiruna_B", 2.0f, -17.0f, 0.95f),
            ("Arvore_Pequena", -9.0f, -21.05f, 0.95f), ("Arvore_Sibipiruna_A", 10.0f, -21.05f, 0.85f),
            ("Arvore_Sibipiruna_B", -15f, -29.9f, 0.8f), ("Arvore_Sibipiruna_A", 4f, -29.9f, 0.85f), ("Arvore_Pequena", 28.5f, -29.9f, 1.05f),
            ("Arvore_Pequena", 9.0f, -12.8f, 1.0f), ("Arvore_Mangueira_B", -15.0f, 8.0f, 0.95f),
            ("Arvore_Mangueira_A", -8.5f, 13.5f, 1.0f), ("Arvore_Sibipiruna_B", 1.0f, 15.5f, 0.9f),
            ("Arvore_Mangueira_B", -25f, 1.8f, 0.9f), ("Arvore_Mangueira_A", 28f, 5.5f, 1.0f), ("Arvore_Pequena", 2.4f, -32.4f, 0.95f),
            ("Arvore_Sibipiruna_A", -27f, 22f, 0.9f), ("Arvore_Pequena", 18.8f, 31.5f, 1.0f), ("Arvore_Mangueira_B", -14.5f, 28f, 1.0f),
        };
        // Headings checked for the branching crowns (Blender/Entorno/scripts/arvores.py): no wood or leaves inside walls,
        // fences or the guard booth, and >1.2 m from lamp globes (the front-yard tree turns a crown gap to the garden post).
        var heading = new Dictionary<(float, float), float> { [(-16.2f, -17.7f)] = 20f, [(10f, -21.05f)] = 330f, [(2f, -17f)] = 0f, [(-15f, 8f)] = 0f };
        var rng = new System.Random(840);
        foreach (var (name, x, z, scale) in treeList)
        {
            float yaw = (float)rng.NextDouble() * 360f;
            if (heading.TryGetValue((x, z), out float fixedYaw)) yaw = fixedYaw;
            Plant(trees, name, x, z, yaw, scale);
            trunks.Add(new Vector3(x, 0.35f, z));
        }
        var bananas = new (string name, float x, float z, float scale)[]
        {
            ("Bananeira_A", 5.8f, -5.8f, 0.85f), ("Bananeira_B", -16.4f, -1.4f, 1.0f), ("Bananeira_A", -15.8f, 1.6f, 0.9f),
            ("Bananeira_B", -3.0f, 15.0f, 1.0f), ("Bananeira_A", -37f, 0f, 1.0f),
        };
        foreach (var (name, x, z, scale) in bananas)
        {
            Plant(shrubs, name, x, z, (float)rng.NextDouble() * 360f, scale);
            trunks.Add(new Vector3(x, 0.3f, z));
        }

        // Shrubs: a group by the west wall, a gappy row behind the front railing, groups in the archive bed.
        var shrubList = new (string name, float x, float z, float scale)[]
        {
            ("Arbusto_Redondo_A", -17.45f, -18.05f, 0.75f), ("Arbusto_Redondo_B", -17.5f, -15.9f, 0.8f), ("Arbusto_Redondo_A", -17.4f, -14.85f, 0.7f),
            ("Arbusto_Redondo_B", -17.35f, -17.0f, 0.65f),
            ("Arbusto_Estreito", -9.3f, -18.2f, 0.75f), ("Arbusto_Estreito", -8.3f, -18.25f, 0.7f), ("Arbusto_Estreito", -5.8f, -18.2f, 0.8f),
            ("Arbusto_Estreito", -4.7f, -18.25f, 0.7f), ("Arbusto_Estreito", 2.2f, -18.2f, 0.75f), ("Arbusto_Estreito", 3.3f, -18.25f, 0.8f),
            ("Arbusto_Estreito", 5.6f, -18.2f, 0.72f),
            ("Arbusto_Redondo_A", -4.8f, -11.6f, 0.78f), ("Arbusto_Redondo_B", -4.0f, -11.65f, 0.7f), ("Arbusto_Redondo_B", -0.5f, -11.6f, 0.75f),
            ("Arbusto_Redondo_A", 2.0f, -11.6f, 0.8f), ("Arbusto_Redondo_B", 2.8f, -11.55f, 0.62f),
            ("Arbusto_Estreito", -17.6f, -5.5f, 0.8f), ("Arbusto_Estreito", -17.7f, 4.5f, 0.85f),
            ("Arbusto_Redondo_A", -5.6f, -32.3f, 0.7f), ("Arbusto_Redondo_B", 0.2f, -32.1f, 0.65f), ("Arbusto_Estreito", -10f, -32.0f, 0.8f),
        };
        foreach (var (name, x, z, scale) in shrubList)
        {
            Plant(shrubs, name, x, z, (float)rng.NextDouble() * 360f, scale);
            trunks.Add(new Vector3(x, 0.25f, z));
        }
        // Mother-in-law's tongue in the two raised planters by the door.
        foreach (float cx in new[] { -13.9f, -8.85f })
            foreach (float dx in new[] { -0.32f, 0.02f, 0.34f })
                Plant(shrubs, "Espada_Sao_Jorge", cx + dx, ReceptionFace - 0.3f + (float)(rng.NextDouble() - 0.5) * 0.12f,
                    (float)rng.NextDouble() * 360f, 0.8f + 0.2f * (float)rng.NextDouble(), y: 0.345f);

        string[] tall = { "Capim_Alto_A", "Capim_Alto_B" }, tuft = { "Tufo_Grama_A", "Tufo_Grama_B" },
            weed = { "Erva_Daninha_A", "Erva_Daninha_B" }, leaves = { "Folhas_Secas_A", "Folhas_Secas_B" };

        // Front yard, tended: tufts along the edges, a few weeds at the foot of walls and posts, little tall grass.
        var front = new List<Vector2>();
        var tidy = new System.Random(1);
        Clusters(cover, tidy, tuft, new Vector2(-12.7f, -18.6f), new Vector2(-12.7f, -14.3f), 3, 0.8f, 1.1f, 4f, front);
        Clusters(cover, tidy, tuft, new Vector2(-10.05f, -18.6f), new Vector2(-10.05f, -14.3f), 3, 0.8f, 1.1f, 4f, front);
        Clusters(cover, tidy, tuft, new Vector2(-15f, WalkS - 0.35f), new Vector2(3.3f, WalkS - 0.35f), 6, 0.8f, 1.1f, 4f, front);
        Clusters(cover, tidy, tuft, new Vector2(-16.8f, -18.5f), new Vector2(6.5f, -18.5f), 7, 0.85f, 1.15f, 4f, front);
        Clusters(cover, tidy, tuft, new Vector2(6.55f, -18.5f), new Vector2(6.55f, -13.6f), 2, 0.85f, 1.1f, 4f, front);
        Scatter(cover, tidy, weed, Rect.MinMaxRect(-18.1f, -18.7f, 6.7f, -18.45f), 7, 0.6f, 0.8f, 1.1f, 4f, front);
        Scatter(cover, tidy, weed, Rect.MinMaxRect(-18.05f, -14f, -15.4f, SideGateZ - 0.75f), 4, 0.5f, 0.8f, 1.1f, 4f, front);
        Scatter(cover, tidy, weed, Rect.MinMaxRect(-6.3f, -10.75f, 3.5f, -10.4f), 3, 0.8f, 0.7f, 1.0f, 4f, front, 0f);   // gravel strip
        Scatter(cover, tidy, tall, Rect.MinMaxRect(-18.05f, -14f, -16.5f, SideGateZ - 0.75f), 6, 0.55f, 0.85f, 1.1f, 4f, front);
        Scatter(cover, tidy, tall, Rect.MinMaxRect(4.1f, -13.8f, 6.7f, -4.1f), 9, 0.6f, 0.8f, 1.15f, 4f, front);
        Scatter(cover, tidy, weed, Rect.MinMaxRect(4.1f, -13.8f, 6.7f, -4.1f), 5, 0.5f, 0.8f, 1.1f, 4f, front);
        Scatter(cover, tidy, tuft, Rect.MinMaxRect(4.1f, -13.8f, 6.7f, -4.1f), 6, 0.45f, 0.9f, 1.2f, 4f, front);

        // Behind the barriers, rough: dense tall grass along walls, weeds and tufts everywhere.
        var rough = new System.Random(2);
        var back = new List<Vector2>();
        Scatter(cover, rough, tall, Rect.MinMaxRect(-18.1f, SideGateZ + 0.75f, -17.4f, 17.5f), 30, 0.5f, 0.9f, 1.3f, 4f, back);
        Scatter(cover, rough, tall, Rect.MinMaxRect(-18f, 17f, 20.5f, 17.6f), 34, 0.5f, 0.9f, 1.3f, 4f, back);
        Scatter(cover, rough, tall, Rect.MinMaxRect(-18f, SideGateZ, 12.5f, 17f), 40, 0.6f, 0.85f, 1.25f, 4f, back);
        Scatter(cover, rough, tall, Rect.MinMaxRect(EastRailX + 0.25f, -18.6f, 8.0f, AutopsyFace - 0.2f), 14, 0.5f, 0.85f, 1.25f, 4f, back);
        Scatter(cover, rough, tall, Rect.MinMaxRect(7.3f, -18.65f, 13.7f, -18.2f), 8, 0.5f, 0.85f, 1.2f, 4f, back);
        Scatter(cover, rough, weed, Rect.MinMaxRect(-18f, SideGateZ, 20.5f, 17.6f), 34, 0.5f, 0.8f, 1.2f, 4f, back);
        Scatter(cover, rough, weed, Rect.MinMaxRect(7.3f, -18.6f, 13.7f, -4.0f), 14, 0.5f, 0.8f, 1.2f, 4f, back);
        Scatter(cover, rough, tuft, Rect.MinMaxRect(-18f, SideGateZ, 20.5f, 17.6f), 50, 0.45f, 0.9f, 1.3f, 4f, back);
        Scatter(cover, rough, tuft, Rect.MinMaxRect(7.3f, -18.6f, 13.7f, -4.0f), 20, 0.45f, 0.9f, 1.3f, 4f, back);
        // Weeds in cracks of the service drive and the public sidewalk (placed on the paving itself).
        foreach (Vector3 crack in new[] { new Vector3(15.2f, -0.02f, -14.3f), new Vector3(19.9f, -0.02f, -7.6f), new Vector3(16.8f, -0.02f, 0.4f),
                     new Vector3(14.3f, -0.02f, -5.1f), new Vector3(-15.4f, SidewalkTop, -19.25f), new Vector3(1.4f, SidewalkTop, -19.3f),
                     new Vector3(24.7f, SidewalkTop, -19.25f), new Vector3(-3.0f, SidewalkTop, -30.8f) })
            Plant(cover, weed[rough.Next(2)], crack.x, crack.z, (float)rough.NextDouble() * 360f, 0.6f + 0.3f * (float)rough.NextDouble(), y: crack.y);
        // Grass strip of the sidewalk: tufts and weeds at the kerb and round the pole.
        Scatter(cover, rough, tuft, Rect.MinMaxRect(-18f, -21.3f, 21f, -20.85f), 16, 0.6f, 0.8f, 1.1f, 4f, back, 0f);

        // Leaf litter under the trees (some reaching the paving), and in the gutter.
        var leafRng = new System.Random(3);
        foreach (var (name, x, z, scale) in treeList)
        {
            int n = name.Contains("Pequena") ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                float a = (float)leafRng.NextDouble() * Mathf.PI * 2, d = 0.6f + 2.2f * (float)leafRng.NextDouble() * scale;
                float lx = x + Mathf.Cos(a) * d, lz = z + Mathf.Sin(a) * d;
                if (Inside(Footprint(), lx, lz, 0.9f)) continue;
                float ly = GroundY(lx, lz) + 0.004f;
                if (lz > LandingS && lz < ReceptionFace && lx > BuildingWest && lx < ArchiveEast) ly = 0.003f;
                if (lx > -12.4f && lx < -10.4f && lz < LandingS && lz > LotS) ly = 0.003f;
                Plant(litter, leaves[leafRng.Next(2)], lx, lz, (float)leafRng.NextDouble() * 360f, 0.8f + 0.4f * (float)leafRng.NextDouble(), y: ly);
            }
        }
        Plant(litter, "Folhas_Secas_A", -12.0f, -15.6f, 40f, 0.9f, y: 0.003f);                 // blown onto the path
        Plant(litter, "Folhas_Secas_B", -9.4f, CurbNear - 0.18f, 10f, 0.7f, y: AsphaltTop + 0.008f);   // in the gutter
        Plant(litter, "Folhas_Secas_A", 9.6f, CurbNear - 0.18f, 200f, 0.75f, y: AsphaltTop + 0.008f);

        // Creeper on the walls: two stretches of the front yard's west wall, one at the back, one on the north wall.
        Plant(vines, "Trepadeira_Muro", LotW + BoundaryThickness + 0.02f, -13.6f, -90f, 1.0f, y: -0.06f);
        Plant(vines, "Trepadeira_Muro", LotW + BoundaryThickness + 0.02f, -11.0f, -90f, 0.9f, y: -0.06f);
        Plant(vines, "Trepadeira_Muro", LotW + BoundaryThickness + 0.02f, 3.0f, -90f, 1.1f, y: -0.07f);
        Plant(vines, "Trepadeira_Muro", -6.0f, LotN - BoundaryThickness - 0.02f, 0f, 1.05f, y: -0.07f);

        // Distant tree tops closing the horizon beyond the houses.
        var farRng = new System.Random(4);
        string[] big = { "Arvore_Mangueira_A", "Arvore_Mangueira_B", "Arvore_Sibipiruna_A", "Arvore_Sibipiruna_B" };
        var farTaken = new List<Vector2>();
        for (int attempt = 0, placedFar = 0; attempt < 600 && placedFar < 20; attempt++)
        {
            float a = (float)farRng.NextDouble() * Mathf.PI * 2, d = 35f + 25f * (float)farRng.NextDouble();
            float x = Mathf.Cos(a) * d, z = -5f + Mathf.Sin(a) * d;
            if (z < StreetNear + 1f && z > StreetFar - 1f && x > -56f && x < 62f) continue;            // the street
            if (x > -53f && x < -44f) continue;                                                        // the cross street
            if (Inside(neighbourRoofs, x, z, 4.5f) || (x > LotW - 3f && x < LotE + 3f && z > LotS && z < LotN + 3f)) continue;
            bool close = false;
            foreach (Vector2 p in farTaken) if ((p - new Vector2(x, z)).sqrMagnitude < 64f) { close = true; break; }
            if (close) continue;
            farTaken.Add(new Vector2(x, z));
            Plant(far, big[farRng.Next(big.Length)], x, z, (float)farRng.NextDouble() * 360f, 0.9f + 0.25f * (float)farRng.NextDouble());
            placedFar++;
        }

        if (missingPlants.Count > 0)
            Debug.LogWarning("Unclaimed: vegetation prefabs missing (" + string.Join(", ", missingPlants) +
                "); run Tools > Unclaimed > Build Entorno Prefabs and rebuild the exterior.");
    }
}
