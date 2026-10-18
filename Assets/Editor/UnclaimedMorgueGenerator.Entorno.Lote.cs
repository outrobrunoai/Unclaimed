// The morgue's lot: ground, paving and drainage, boundary walls, railings and locked gates, the facade's
// fittings and the service yard. Conventions in UnclaimedMorgueGenerator.Entorno.cs.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class UnclaimedMorgueGenerator
{
    // Lot lines and the building faces the yard meets (outer faces: zone edge +- half a wall).
    private const float LotW = -18.5f, LotE = 21f, LotS = -19f, LotN = 18f;
    private const float ReceptionFace = -12.475f, ArchiveFace = -10.35f, ArchiveEast = 3.6f, BuildingWest = -14.85f, AutopsyFace = -3.6f;
    private const float WalkS = -13.375f, LandingS = -14f, SideGateZ = -8f, EastRailX = 7f;
    private const float BoundaryHeight = 2.2f, BoundaryThickness = 0.15f;

    // Outer footprint of the building and the yard fence (zones grown by half a wall).
    private static List<Rect> Footprint()
    {
        var list = new List<Rect>();
        foreach (Zone z in Zones())
        {
            float grow = z.Kind == Kind.Interior ? WallThickness / 2 : FenceThickness / 2;
            foreach (Rect r in z.Rects) list.Add(Rect.MinMaxRect(r.xMin - grow, r.yMin - grow, r.xMax + grow, r.yMax + grow));
        }
        return list;
    }

    private static bool Inside(List<Rect> rects, float x, float z, float margin = 0f)
    {
        foreach (Rect r in rects)
            if (x > r.xMin - margin && x < r.xMax + margin && z > r.yMin - margin && z < r.yMax + margin) return true;
        return false;
    }

    // ------------------------------------------------------------------ ground, paving, drainage

    private static void BuildLotGround(Transform terrain, Transform paving, Transform drainage)
    {
        List<Rect> footprint = Footprint();
        var ground = new ExtMesh();
        // Front yard (the explorable area): tended lawn, a shrub bed along the west wall, soil beds.
        ground.Ground(-18.35f, -18.9f, -17.1f, LandingS, -0.05f, 0.02f, exSoil);           // shrub bed by the west wall
        ground.Ground(-17.1f, -18.9f, -12.48f, LandingS, -0.05f, 0.015f, exLawn);          // west lawn
        ground.Ground(-10.32f, -18.9f, 6.925f, LandingS, -0.05f, 0.015f, exLawn);          // east lawn
        ground.Ground(-15.25f, LandingS, -13.2f, WalkS, -0.05f, 0.015f, exLawn);
        ground.Ground(-9.55f, LandingS, ArchiveEast, WalkS, -0.05f, 0.015f, exLawn);
        ground.Ground(-18.35f, LandingS, -15.25f, SideGateZ, -0.05f, 0.02f, exSparseGrass); // west strip up to the side gate
        ground.Ground(-6.4f, ReceptionFace, ArchiveEast, -10.75f, -0.05f, 0.02f, exSoil);   // bed in front of the archive
        ground.Ground(ArchiveEast, LandingS, 6.925f, ArchiveFace, -0.05f, 0.02f, exSoil);   // nook east of the archive
        ground.Ground(4f, ArchiveFace, 6.925f, AutopsyFace - 0.4f, -0.05f, 0.02f, exSoil);
        ground.Emit("Terreno - jardim frontal", terrain, true, 0.35f, ShadowCastingMode.Off);

        // Beyond the barriers: rougher ground, never walked on.
        var back = new ExtMesh();
        back.Ground(7.075f, -18.9f, 14f, -8.6f, -0.06f, 0.03f, exSparseGrass);              // service yard
        back.Ground(7.075f, -8.6f, 8.6f, -4.2f, -0.06f, 0.03f, exSparseGrass);
        var skip = new List<Rect>(footprint)
        {
            Rect.MinMaxRect(7f, -19f, 21f, AutopsyFace), Rect.MinMaxRect(15.1f, -19f, 21f, 5.2f),
            Rect.MinMaxRect(3.6f, -19f, 7f, AutopsyFace),
        };
        GroundSkipping(back, -18.35f, SideGateZ, -13.7f, 17.85f, -0.06f, 0.035f, exSoil, skip);   // west strip to the back
        GroundSkipping(back, -13.7f, SideGateZ, 20.925f, 17.85f, -0.06f, 0.035f, exSparseGrass, skip);
        back.Emit("Terreno - fundos e patio de manobra", terrain, true, 0.15f, ShadowCastingMode.Off);
        // The lot is walked on at about -0.04: one flat collider (paving adds its own, 4 cm higher).
        Collider("Colisor - chao do lote", terrain, LotW, LotS, LotE, LotN, -0.3f, -0.04f);

        // Paving of the explorable area: walkway along the facade, entrance landing, path to the gate.
        var pave = new ExtMesh();
        var paved = new List<Rect>
        {
            Rect.MinMaxRect(BuildingWest, WalkS, -13.2f, ReceptionFace),     // protective walkway, west of the landing
            Rect.MinMaxRect(-9.55f, WalkS, ArchiveEast, ReceptionFace),      // ... east of it, on to the nook
            Rect.MinMaxRect(-13.2f, -13.85f, -9.55f, ReceptionFace),         // entrance landing
            Rect.MinMaxRect(-12.4f, -19.1f, -10.4f, LandingS),               // path to the pedestrian gate
        };
        foreach (Rect r in paved)
        {
            pave.Box(r.xMin, -0.15f, r.yMin, r.xMax, 0f, r.yMax, exScreed);
            Collider("Colisor - pavimento", paving, r.xMin, r.yMin, r.xMax, r.yMax, -0.15f, 0f);
        }
        // Gravel strips where the walls meet soil, held by garden curbs.
        var gravel = new List<Rect>
        {
            Rect.MinMaxRect(-15.25f, WalkS, BuildingWest, SideGateZ),
            Rect.MinMaxRect(-6.4f, -10.75f, ArchiveEast, ArchiveFace),
            Rect.MinMaxRect(ArchiveEast, ArchiveFace, 4f, AutopsyFace),
            Rect.MinMaxRect(4f, AutopsyFace - 0.4f, 6.925f, AutopsyFace),
        };
        foreach (Rect r in gravel) pave.Box(r.xMin, -0.1f, r.yMin, r.xMax, -0.012f, r.yMax, exGravel);
        var curbs = new List<Rect>
        {
            Rect.MinMaxRect(-12.48f, -18.9f, -12.4f, LandingS), Rect.MinMaxRect(-10.4f, -18.9f, -10.32f, LandingS),
            Rect.MinMaxRect(-15.25f, WalkS - 0.08f, -13.2f, WalkS), Rect.MinMaxRect(-9.55f, WalkS - 0.08f, ArchiveEast + 0.08f, WalkS),
            Rect.MinMaxRect(-13.28f, LandingS, -13.2f, WalkS), Rect.MinMaxRect(-9.55f, LandingS, -9.47f, WalkS),
            Rect.MinMaxRect(-15.33f, WalkS, -15.25f, SideGateZ), Rect.MinMaxRect(-6.4f, ReceptionFace, ArchiveEast, ReceptionFace + 0.08f),
            Rect.MinMaxRect(-6.4f, -10.83f, ArchiveEast, -10.75f), Rect.MinMaxRect(ArchiveEast, WalkS, ArchiveEast + 0.08f, ArchiveFace),
            Rect.MinMaxRect(4f, ArchiveFace, 4.08f, AutopsyFace - 0.4f), Rect.MinMaxRect(4.08f, AutopsyFace - 0.48f, 6.925f, AutopsyFace - 0.4f),
        };
        foreach (Rect r in curbs) pave.Box(r.xMin, -0.12f, r.yMin, r.xMax, 0.04f, r.yMax, exConcrete);

        // Concrete apron round the building where nobody goes (keeps the walls out of the soil).
        var apron = new List<Rect>
        {
            Rect.MinMaxRect(-15.45f, SideGateZ, BuildingWest, -3.175f), Rect.MinMaxRect(BuildingWest, -3.775f, -13.7f, -3.175f),
            Rect.MinMaxRect(-13.7f, -3.775f, -13.1f, 1.575f), Rect.MinMaxRect(-13.1f, 0.975f, -8.7f, 1.575f),
            Rect.MinMaxRect(-8.7f, 1.575f, -8.1f, 6.7f), Rect.MinMaxRect(-8.1f, 6.1f, -4.6f, 6.7f),
            Rect.MinMaxRect(-5.2f, 6.7f, -4.6f, 9.7f), Rect.MinMaxRect(-4.6f, 9.1f, 3.15f, 9.7f),
            Rect.MinMaxRect(2.55f, 9.7f, 3.15f, 12.2f), Rect.MinMaxRect(3.15f, 11.6f, 9.175f, 12.2f),
            Rect.MinMaxRect(7.075f, -4.2f, 8.6f, AutopsyFace),
        };
        foreach (Rect r in apron) pave.Box(r.xMin, -0.15f, r.yMin, r.xMax, 0f, r.yMax, exScreed);

        // Two raised brick planters against the facade beside the door, clear of the glass leaves' swing.
        foreach (float cx in new[] { -13.9f, -8.85f })
        {
            float x0 = cx - 0.6f, x1 = cx + 0.6f, z0 = ReceptionFace - 0.6f, z1 = ReceptionFace;
            pave.Box(x0, 0f, z0, x1, 0.37f, z1, exWallPlaster, 63 - 4 - 8);
            pave.Box(x0 - 0.02f, 0.37f, z0 - 0.02f, x1 + 0.02f, 0.4f, z0 + 0.1f, exConcrete);       // coping, front and sides
            pave.Box(x0 - 0.02f, 0.37f, z0 + 0.1f, x0 + 0.1f, 0.4f, z1, exConcrete);
            pave.Box(x1 - 0.1f, 0.37f, z0 + 0.1f, x1 + 0.02f, 0.4f, z1, exConcrete);
            pave.Box(x0 + 0.1f, 0.3f, z0 + 0.1f, x1 - 0.1f, 0.35f, z1 - 0.003f, exSoil, 8);
            Collider("Colisor - canteiro elevado", paving, x0, z0, x1, z1, 0f, 0.4f);
        }
        pave.Emit("Pavimentos - area de entrada", paving, true, 0.35f);

        // Concrete bench under the west tree, facing east.
        var bench = new ExtMesh();
        bench.Box(-16.2f, 0.37f, -16.0f, -15.8f, 0.45f, -14.4f, exConcrete);
        bench.Box(-16.15f, -0.05f, -15.85f, -15.85f, 0.37f, -15.6f, exConcrete);
        bench.Box(-16.15f, -0.05f, -14.8f, -15.85f, 0.37f, -14.55f, exConcrete);
        bench.Emit("Banco de concreto", paving, false);
        Collider("Colisor - banco", paving, -16.2f, -16.0f, -15.8f, -14.4f, 0f, 0.45f);

        // Drainage: a grated channel across the landing, downpipes into grated boxes, and the boxes' covers.
        var drains = new ExtMesh();
        drains.Box(-13.2f, -0.12f, LandingS, -9.55f, -0.004f, -13.85f, exGrate);
        drains.Box(-13.2f, -0.12f, LandingS - 0.03f, -9.55f, 0f, LandingS, exConcrete);
        var pipes = new ExtMesh();
        Downpipe(pipes, drains, new Vector3(-14.6f, 0, ReceptionFace), Vector3.back);
        Downpipe(pipes, drains, new Vector3(-6.75f, 0, ReceptionFace), Vector3.back);
        Downpipe(pipes, drains, new Vector3(3.3f, 0, ArchiveFace), Vector3.back);
        Downpipe(pipes, drains, new Vector3(ArchiveEast, 0, -6.0f), Vector3.right);
        Downpipe(pipes, drains, new Vector3(BuildingWest, 0, -4.2f), Vector3.left);
        Downpipe(pipes, drains, new Vector3(-13.1f, 0, 0.5f), Vector3.left);
        Downpipe(pipes, drains, new Vector3(0f, 0, 9.1f), Vector3.forward);
        Downpipe(pipes, drains, new Vector3(14.6f, 0, AutopsyFace), Vector3.back);
        drains.Emit("Canaleta e caixas de ralo", drainage, false);
        pipes.Emit("Condutores pluviais", drainage, false);
    }

    // Ground grid that leaves out the cells lying wholly inside `skip` (under the building, or paved).
    private static void GroundSkipping(ExtMesh m, float x0, float z0, float x1, float z1, float y, float amp, Material mat, List<Rect> skip)
    {
        const float step = 0.5f;
        for (float x = x0; x < x1 - 1e-3f; x += step)
            for (float z = z0; z < z1 - 1e-3f; z += step)
            {
                float xb = Mathf.Min(x + step, x1), zb = Mathf.Min(z + step, z1);
                bool hidden = false;
                foreach (Rect r in skip)
                    if (x >= r.xMin && xb <= r.xMax && z >= r.yMin && zb <= r.yMax) { hidden = true; break; }
                if (!hidden) m.Ground(x, z, xb, zb, y, amp, mat, step);
            }
    }

    // PVC downpipe on a wall face: elbow into the parapet at the top, clamps, a shoe at the foot, and the
    // grated box it drains into.
    private static void Downpipe(ExtMesh pipe, ExtMesh drain, Vector3 face, Vector3 normal)
    {
        const float r = 0.05f, gap = 0.06f;
        Vector3 c = face + normal * (gap + r);
        if (UnclaimedExteriorFinishes.Available("Condutor_Pluvial_DN100"))
            UnclaimedExteriorFinishes.Place("Condutor_Pluvial_DN100", root.Find("Area externa/Drenagem"), face, normal, "Condutor DN100");
        else
        {
            pipe.Cylinder(c + Vector3.up * 0.22f, c + Vector3.up * 3.42f, r, r, 8, exPvc, false);
            pipe.Cylinder(c + Vector3.up * 3.42f, face + Vector3.up * 3.62f + normal * 0.02f, r, r, 8, exPvc, false);   // into the parapet
            pipe.Cylinder(c + Vector3.up * 0.22f, c + normal * 0.16f + Vector3.up * 0.07f, r, r, 8, exPvc);               // shoe
            Vector3 side = Vector3.Cross(Vector3.up, normal);
            for (float y = 0.9f; y < 3.3f; y += 1.2f)
            {
                pipe.Beam(c + Vector3.up * y - side * 0.065f, c + Vector3.up * y + side * 0.065f, 0.03f, 0.13f, exGalvanized);
                pipe.Beam(face + Vector3.up * y, c + Vector3.up * y - normal * r, 0.02f, 0.03f, exGalvanized);
            }
        }
        Vector3 box = face + normal * 0.32f;
        Vector3 h = new Vector3(0.15f, 0, 0.15f);
        drain.Box(box - h + Vector3.up * -0.08f, box + h + Vector3.up * 0.02f, exConcrete, 63 - 8);
        drain.Box(box - h + new Vector3(0.025f, 0.021f, 0.025f), box + h + new Vector3(-0.025f, 0.024f, -0.025f), exGrate);
        drain.Box(box - h + new Vector3(0f, 0.02f, 0f), box + new Vector3(0.15f, 0.021f, -0.125f), exConcrete, 8);
        drain.Box(box + new Vector3(-0.15f, 0.02f, 0.125f), box + new Vector3(0.15f, 0.021f, 0.15f), exConcrete, 8);
        drain.Box(box + new Vector3(-0.15f, 0.02f, -0.125f), box + new Vector3(-0.125f, 0.021f, 0.125f), exConcrete, 8);
        drain.Box(box + new Vector3(0.125f, 0.02f, -0.125f), box + new Vector3(0.15f, 0.021f, 0.125f), exConcrete, 8);
    }

    // ------------------------------------------------------------------ walls, railings, gates

    private static void BuildLotBoundaries(Transform fences, Transform limits)
    {
        var walls = new ExtMesh();
        var metal = new ExtMesh();
        // Boundary walls: plastered and painted on the lot side, bare block on the neighbour's.
        BoundaryWall(walls, LotW, LotS + 0.1f, LotW + BoundaryThickness, LotN, Vector3.right);
        BoundaryWall(walls, LotW + BoundaryThickness, LotN - BoundaryThickness, LotE - 0.075f, LotN, Vector3.back);
        BoundaryWall(walls, LotE - 0.075f, LotS + 0.1f, LotE + 0.075f, 5.175f, Vector3.left);
        BoundaryWall(walls, LotE - 0.075f, 15.575f, LotE + 0.075f, LotN - BoundaryThickness, Vector3.left);
        Collider("Colisor - muro oeste", fences, LotW, LotS, LotW + BoundaryThickness, LotN, -0.3f, BoundaryHeight);
        Collider("Colisor - muro norte", fences, LotW, LotN - BoundaryThickness, LotE, LotN, -0.3f, BoundaryHeight);
        Collider("Colisor - muro leste", fences, LotE - 0.075f, LotS, LotE + 0.075f, 5.25f, -0.3f, BoundaryHeight);
        Collider("Colisor - muro leste - fundos", fences, LotE - 0.075f, 15.5f, LotE + 0.075f, LotN, -0.3f, BoundaryHeight);

        // Street front: low wall with a railing, masonry posts at the gates and corners.
        float[] posts = { LotW, -12.7f, -10.4f, EastRailX - 0.15f, 13.7f, 19f, LotE - 0.3f };
        void FrontRun(float a, float b)
        {
            walls.Box(a, -0.12f, LotS - 0.1f, b, 0.57f, LotS + 0.1f, exWallPlaster, 63 - 8);
            walls.Box(a, 0.57f, LotS - 0.12f, b, 0.6f, LotS + 0.12f, exConcrete);
            Railing(metal, new Vector3(a, 0, LotS), new Vector3(b, 0, LotS), 0.6f, 2.0f, 0.12f, exRailing, 2.5f, 0.65f, 1.95f);
        }
        FrontRun(LotW + 0.3f, -12.7f);
        FrontRun(-10.1f, EastRailX - 0.15f);
        FrontRun(EastRailX + 0.15f, 13.7f);
        FrontRun(19.3f, LotE - 0.3f);
        foreach (float x in posts) MasonryPost(walls, x, LotS, 2.15f);
        Collider("Colisor - gradil frontal", fences, LotW, LotS - 0.12f, LotE, LotS + 0.12f, -0.3f, BoundaryHeight);

        // Pedestrian gate: two railing leaves, chained and padlocked in the middle.
        GateRailing(metal, new Vector3(-12.4f, 0, LotS), new Vector3(-11.42f, 0, LotS), 2.0f);
        GateRailing(metal, new Vector3(-11.38f, 0, LotS), new Vector3(-10.4f, 0, LotS), 2.0f);
        Padlock(metal, fences, new Vector3(-11.4f, 1.05f, LotS - 0.03f), Vector3.back);
        // Water meter box set into the low wall west of the gate.
        walls.Box(-13.65f, 0.1f, LotS - 0.11f, -13.15f, 0.5f, LotS - 0.1f, exSteelSheet);
        walls.Box(-13.5f, 0.32f, LotS - 0.115f, -13.3f, 0.42f, LotS - 0.11f, exDarkGlass);

        // Service vehicle gate: a sliding leaf (plain sheet to 1 m, bars above) closed on its rail.
        walls.Box(9f, -0.13f, LotS + 0.12f, 19.2f, -0.1f, LotS + 0.2f, exGalvanized);
        SlidingGate(metal, 13.95f, 19.05f, LotS + 0.16f);
        walls.Emit("Muros e mureta", fences, true, 0.35f);

        // West side gate (blind sheet, padlocked) closing the strip along the building.
        var side = new ExtMesh();
        MasonryPost(side, LotW + BoundaryThickness, SideGateZ, 2.3f);
        MasonryPost(side, BuildingWest - 0.3f, SideGateZ, 2.3f);
        SheetLeaf(metal, new Vector3(-18.05f, 0, SideGateZ), new Vector3(-16.62f, 0, SideGateZ), 0.05f, 2.05f);
        SheetLeaf(metal, new Vector3(-16.58f, 0, SideGateZ), new Vector3(-15.15f, 0, SideGateZ), 0.05f, 2.05f);
        Padlock(metal, fences, new Vector3(-16.6f, 1.1f, SideGateZ - 0.04f), Vector3.back);
        Collider("Colisor - portao lateral", fences, LotW, SideGateZ - 0.15f, BuildingWest, SideGateZ + 0.15f, -0.3f, 2.3f);

        // East railing between the front yard and the service yard, with a padlocked pedestrian gate.
        void EastRun(float z0, float z1)
        {
            side.Box(EastRailX - 0.075f, -0.1f, z0, EastRailX + 0.075f, 0.27f, z1, exWallPlaster, 63 - 8);
            side.Box(EastRailX - 0.09f, 0.27f, z0, EastRailX + 0.09f, 0.3f, z1, exConcrete);
            Railing(metal, new Vector3(EastRailX, 0, z0), new Vector3(EastRailX, 0, z1), 0.3f, 2.0f, 0.12f, exRailing, 2.5f, 0.35f, 1.95f);
        }
        EastRun(LotS + 0.15f, -15.65f);
        EastRun(-14.55f, AutopsyFace);
        metal.Beam(new Vector3(EastRailX, -0.05f, -15.65f), new Vector3(EastRailX, 2.05f, -15.65f), 0.07f, 0.07f, exRailing);
        metal.Beam(new Vector3(EastRailX, -0.05f, -14.55f), new Vector3(EastRailX, 2.05f, -14.55f), 0.07f, 0.07f, exRailing);
        GateRailing(metal, new Vector3(EastRailX, 0, -15.6f), new Vector3(EastRailX, 0, -14.6f), 2.0f);
        Padlock(metal, fences, new Vector3(EastRailX - 0.03f, 1.05f, -14.62f), Vector3.left);
        Collider("Colisor - gradil leste", fences, EastRailX - 0.12f, LotS, EastRailX + 0.12f, AutopsyFace, -0.3f, BoundaryHeight);

        // Leaves of the yard gate (its posts come from the plan): the service yard stays closed to the outside.
        SheetLeaf(metal, new Vector3(16.75f, 0, 5.25f), new Vector3(17.98f, 0, 5.25f), 0.04f, 2.1f);
        SheetLeaf(metal, new Vector3(18.02f, 0, 5.25f), new Vector3(19.25f, 0, 5.25f), 0.04f, 2.1f);
        Padlock(metal, fences, new Vector3(18f, 1.1f, 5.21f), Vector3.back);
        Collider("Colisor - portao do patio", fences, 16.45f, 5.15f, 19.55f, 5.35f, -0.3f, 2.4f);
        side.Emit("Portao lateral e mureta leste", fences, true, 0.35f);
        metal.Emit("Gradis e portoes", fences, false);

        // Wall creeper stains low on the walls the yard sees.
        var stains = new ExtMesh();
        stains.Quad(new Vector3(LotW + 0.153f, -0.05f, -18.8f), new Vector3(LotW + 0.153f, -0.05f, SideGateZ - 0.2f),
            new Vector3(LotW + 0.153f, 0.45f, SideGateZ - 0.2f), new Vector3(LotW + 0.153f, 0.45f, -18.8f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Vector3.right, exStain);
        stains.Emit("Mancha de umidade - muro oeste", fences, false, 0.2f, ShadowCastingMode.Off);

        // Safety net only outside the explorable area: behind the front railing, the west wall and the gates.
        Collider("Limite - calcada", limits, LotW - 1f, LotS - 0.5f, LotE + 1f, LotS - 0.3f, -0.5f, 3f);
        Collider("Limite - lote oeste", limits, LotW - 0.5f, LotS - 0.5f, LotW - 0.3f, LotN, -0.5f, 3f);
        Collider("Limite - faixa lateral", limits, LotW, SideGateZ + 0.3f, BuildingWest, SideGateZ + 0.5f, -0.5f, 3f);
        Collider("Limite - patio de manobra", limits, EastRailX + 0.3f, LotS, EastRailX + 0.5f, AutopsyFace, -0.5f, 3f);
    }

    // Wall from (x0, z0) to (x1, z1) (a thin box), painted on the side facing `lotSide`, with a concrete coping.
    private static void BoundaryWall(ExtMesh m, float x0, float z0, float x1, float z1, Vector3 lotSide)
    {
        int painted = lotSide.x > 0 ? 2 : lotSide.x < 0 ? 1 : lotSide.z > 0 ? 32 : 16;
        m.Box(x0, -0.1f, z0, x1, BoundaryHeight - 0.03f, z1, exWallPlaster, painted);
        m.Box(x0, -0.1f, z0, x1, BoundaryHeight - 0.03f, z1, exBlock, 63 - 8 - 4 - painted);
        bool alongZ = z1 - z0 > x1 - x0;
        float dx = alongZ ? 0.03f : 0f, dz = alongZ ? 0f : 0.03f;
        m.Box(x0 - dx, BoundaryHeight - 0.03f, z0 - dz, x1 + dx, BoundaryHeight, z1 + dz, exConcrete);
    }

    // Masonry post 0.3 x 0.3 on a wall line running along x: it spans [at, at + 0.3] and is centred on z = line.
    private static void MasonryPost(ExtMesh m, float at, float line, float height)
    {
        float x0 = at, x1 = at + 0.3f, z0 = line - 0.15f, z1 = line + 0.15f;
        m.Box(x0, -0.12f, z0, x1, height - 0.04f, z1, exWallPlaster, 63 - 8);
        m.Box(x0 - 0.025f, height - 0.04f, z0 - 0.025f, x1 + 0.025f, height, z1 + 0.025f, exConcrete);
    }

    // Railing of square bars along a straight line: bars every `spacing`, heavier posts every ~`post` meters
    // and flat rails at the given heights.
    private static void Railing(ExtMesh m, Vector3 a, Vector3 b, float y0, float y1, float spacing, Material mat,
        float post, params float[] rails)
    {
        a.y = 0; b.y = 0;
        float len = Vector3.Distance(a, b);
        Vector3 u = (b - a) / len;
        int bars = Mathf.Max(1, Mathf.RoundToInt(len / spacing)), posts = Mathf.Max(1, Mathf.RoundToInt(len / post));
        for (int i = 1; i < bars; i++)
        {
            Vector3 p = a + u * (len * i / bars);
            m.Beam(p + Vector3.up * y0, p + Vector3.up * y1, 0.02f, 0.02f, mat, false);
        }
        for (int i = 0; i <= posts; i++)
        {
            Vector3 p = a + u * Mathf.Clamp(len * i / posts, 0.03f, len - 0.03f);
            m.Beam(p + Vector3.up * (y0 - 0.01f), p + Vector3.up * (y1 + 0.03f), 0.05f, 0.05f, mat);
        }
        foreach (float h in rails) m.Beam(a + Vector3.up * h, b + Vector3.up * h, 0.02f, 0.04f, mat);
    }

    // Gate leaf of railing: frame, bars every 0.11 m.
    private static void GateRailing(ExtMesh m, Vector3 a, Vector3 b, float height)
    {
        a.y = 0; b.y = 0;
        Vector3 u = (b - a).normalized;
        float bottom = 0.04f;
        m.Beam(a + Vector3.up * bottom, a + Vector3.up * height, 0.04f, 0.04f, exRailing);
        m.Beam(b + Vector3.up * bottom, b + Vector3.up * height, 0.04f, 0.04f, exRailing);
        foreach (float h in new[] { bottom + 0.02f, 1.0f, height - 0.02f })
            m.Beam(a + Vector3.up * h, b + Vector3.up * h, 0.03f, 0.04f, exRailing);
        int bars = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.11f));
        for (int i = 1; i < bars; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)bars);
            m.Beam(p + Vector3.up * bottom, p + Vector3.up * height, 0.018f, 0.018f, exRailing, false);
        }
    }

    // Blind gate leaf of painted sheet in a tube frame.
    private static void SheetLeaf(ExtMesh m, Vector3 a, Vector3 b, float bottom, float top)
    {
        a.y = 0; b.y = 0;
        Vector3 u = (b - a).normalized, n = Vector3.Cross(u, Vector3.up);
        m.Beam(a + Vector3.up * bottom, a + Vector3.up * top, 0.05f, 0.05f, exSteelSheet);
        m.Beam(b + Vector3.up * bottom, b + Vector3.up * top, 0.05f, 0.05f, exSteelSheet);
        m.Beam(a + Vector3.up * (bottom + 0.025f), b + Vector3.up * (bottom + 0.025f), 0.05f, 0.05f, exSteelSheet);
        m.Beam(a + Vector3.up * (top - 0.025f), b + Vector3.up * (top - 0.025f), 0.05f, 0.05f, exSteelSheet);
        Vector3 c = (a + b) / 2 + Vector3.up * (bottom + top) / 2;
        m.OrientedBox(c, new Vector3(Vector3.Distance(a, b) - 0.04f, top - bottom - 0.04f, 0.015f), Quaternion.LookRotation(n), exSteelSheet);
        // Pressed horizontal ribs on both faces.
        for (float y = bottom + 0.35f; y < top - 0.2f; y += 0.35f)
            m.OrientedBox((a + b) / 2 + Vector3.up * y, new Vector3(Vector3.Distance(a, b) - 0.06f, 0.03f, 0.03f), Quaternion.LookRotation(n), exSteelSheet);
    }

    private static void SlidingGate(ExtMesh m, float x0, float x1, float z)
    {
        Vector3 a = new Vector3(x0, 0, z), b = new Vector3(x1, 0, z);
        m.Beam(a + Vector3.up * 0.06f, a + Vector3.up * 2.0f, 0.05f, 0.05f, exRailing);
        m.Beam(b + Vector3.up * 0.06f, b + Vector3.up * 2.0f, 0.05f, 0.05f, exRailing);
        foreach (float h in new[] { 0.09f, 1.0f, 1.97f }) m.Beam(a + Vector3.up * h, b + Vector3.up * h, 0.05f, 0.05f, exRailing);
        m.Box(x0, 0.1f, z - 0.008f, x1, 0.98f, z + 0.008f, exSteelSheet);
        int bars = Mathf.RoundToInt((x1 - x0) / 0.12f);
        for (int i = 1; i < bars; i++)
        {
            float x = Mathf.Lerp(x0, x1, i / (float)bars);
            m.Beam(new Vector3(x, 1.0f, z), new Vector3(x, 1.97f, z), 0.02f, 0.02f, exRailing, false);
        }
        for (float x = x0 + 0.4f; x < x1; x += (x1 - x0) - 0.8f)   // wheels on the rail
            m.Cylinder(new Vector3(x, 0.0f, z - 0.03f), new Vector3(x, 0.0f, z + 0.03f), 0.06f, 0.06f, 8, exGalvanized);
    }

    // Chain round the meeting stiles with a brass-less steel padlock hanging from it.
    private static void Padlock(ExtMesh m, Transform parent, Vector3 at, Vector3 outward)
    {
        if (UnclaimedExteriorFinishes.Available("Cadeado_Chave_Corrente"))
        {
            UnclaimedExteriorFinishes.Place("Cadeado_Chave_Corrente", parent, at + (Mathf.Abs(at.z + 8.04f) < .01f || Mathf.Abs(at.z - 5.21f) < .01f ? Vector3.forward * .027f : Vector3.zero), outward, "Cadeado de chave - portao");
            return;
        }
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            Vector3 p = at + side * Mathf.Lerp(-0.06f, 0.06f, t) + Vector3.up * (0.04f * Mathf.Sin(t * Mathf.PI)) + outward * 0.01f;
            m.Box(p - new Vector3(0.012f, 0.008f, 0.012f), p + new Vector3(0.012f, 0.008f, 0.012f), exGalvanized);
        }
        Vector3 body = at + outward * 0.03f - Vector3.up * 0.06f;
        m.Box(body - new Vector3(0.025f, 0.03f, 0.025f), body + new Vector3(0.025f, 0.03f, 0.025f), exGalvanized);
        m.Beam(body + Vector3.up * 0.03f - side * 0.015f, body + Vector3.up * 0.065f - side * 0.015f, 0.008f, 0.008f, exGalvanized);
        m.Beam(body + Vector3.up * 0.03f + side * 0.015f, body + Vector3.up * 0.065f + side * 0.015f, 0.008f, 0.008f, exGalvanized);
        m.Beam(body + Vector3.up * 0.065f - side * 0.019f, body + Vector3.up * 0.065f + side * 0.019f, 0.008f, 0.008f, exGalvanized);
    }

    // ------------------------------------------------------------------ facade fittings

    private static void BuildFacadeFittings(Transform facade)
    {
        string t = EntornoTextures;
        // Main identification follows approved direction A; other categories keep their own layouts.
        Material sign(string name) => Mat(EntornoMaterials, name, Color.white, name=="Placa_Fachada"?0.16f:0.25f, 0f,
            name=="Placa_Fachada"?UnclaimedExteriorFinishes.Folder+"/Texturas/"+name+".png":t+name+".png");
        ExteriorSign("Placa - identificacao", facade, new Vector3(-11.375f, 3.08f, ReceptionFace), Vector3.back, 2.4f, 0.52f,
            sign("Placa_Fachada"), exGalvanized, 0.008f);
        ExteriorSign("Placa - atendimento", facade, new Vector3(-9.9f, 1.55f, ReceptionFace), Vector3.back, 0.4f, 0.3f,
            sign("Placa_Atendimento"), exPvc, 0.006f);
        ExteriorSign("Placa - numero", facade, new Vector3(-12.55f, 1.55f, LotS - 0.15f), Vector3.back, 0.24f, 0.12f,
            sign("Placa_Numero"), exGalvanized, 0.005f);
        ExteriorSign("Placa - nao estacione", facade, new Vector3(20.1f, 0.33f, LotS - 0.1f), Vector3.back, 0.5f, 0.25f,
            sign("Placa_NaoEstacione"), exGalvanized, 0.004f);

        var m = new ExtMesh();
        // Intercom by the door.
        if (UnclaimedExteriorFinishes.Available("Interfone_Externo"))
            UnclaimedExteriorFinishes.Place("Interfone_Externo", facade, new Vector3(-9.7f,1.3f,ReceptionFace), Vector3.back, "Interfone externo");
        else
        {
            m.Box(-9.76f, 1.2f, ReceptionFace - 0.05f, -9.64f, 1.4f, ReceptionFace, exPvc);
            m.Box(-9.73f, 1.25f, ReceptionFace - 0.055f, -9.67f, 1.29f, ReceptionFace - 0.05f, exRailing);
            m.Box(-9.74f, 1.31f, ReceptionFace - 0.052f, -9.66f, 1.37f, ReceptionFace - 0.05f, exGrate);
        }
        // Security camera at the reception's south-west corner, aimed at the pedestrian gate.
        Vector3 corner = new Vector3(BuildingWest, 3.0f, ReceptionFace);
        Vector3 arm = corner + new Vector3(-0.12f, 0, -0.12f);
        m.Box(corner + new Vector3(-0.005f, -0.08f, -0.06f), corner + new Vector3(0.0f, 0.08f, 0.0f), exWhitePaint);
        m.Beam(corner + new Vector3(-0.02f, 0, -0.02f), arm, 0.03f, 0.03f, exWhitePaint);
        Vector3 aim = (new Vector3(-11.4f, 1.0f, LotS) - arm).normalized;
        m.OrientedBox(arm + aim * 0.14f + Vector3.down * 0.04f, new Vector3(0.09f, 0.08f, 0.26f), Quaternion.LookRotation(aim), exWhitePaint);
        m.OrientedBox(arm + aim * 0.275f + Vector3.down * 0.04f, new Vector3(0.06f, 0.05f, 0.01f), Quaternion.LookRotation(aim), exDarkGlass);

        // Two condensing units on brackets in the nook, their drains dripping onto the gravel.
        foreach (float z in new[] { -9.2f, -7.9f })
        {
            if (UnclaimedExteriorFinishes.Available("Condensadora_Split"))
            {
                UnclaimedExteriorFinishes.Place(z < -8.5f ? "Condensadora_Split" : "Condensadora_Split_Usada", facade, new Vector3(ArchiveEast,1.8f,z), Vector3.right, "Condensadora split");
                Decal("Mancha do dreno", facade, new Vector3(ArchiveEast + 0.21f,-0.004f,z + 0.3f), Vector3.up, Vector3.right, Vector3.forward,0.45f,0.4f,exStain);
                continue;
            }
            float x0 = ArchiveEast + 0.04f, x1 = x0 + 0.3f, y0 = 1.8f;
            m.Box(x0, y0, z - 0.4f, x1, y0 + 0.55f, z + 0.4f, exWhitePaint, 63 - 2);
            m.Box(x1 - 0.004f, y0 + 0.04f, z - 0.36f, x1, y0 + 0.51f, z + 0.1f, exGrate, 2);
            m.Box(x1 - 0.004f, y0, z - 0.4f, x1, y0 + 0.04f, z + 0.4f, exWhitePaint, 2);
            m.Box(x1 - 0.004f, y0 + 0.51f, z - 0.4f, x1, y0 + 0.55f, z + 0.4f, exWhitePaint, 2);
            m.Box(x1 - 0.004f, y0 + 0.04f, z - 0.4f, x1, y0 + 0.51f, z - 0.36f, exWhitePaint, 2);
            m.Box(x1 - 0.004f, y0 + 0.04f, z + 0.1f, x1, y0 + 0.51f, z + 0.4f, exWhitePaint, 2);
            m.Cylinder(new Vector3(x1 - 0.003f, y0 + 0.275f, z - 0.13f), new Vector3(x1 + 0.005f, y0 + 0.275f, z - 0.13f), 0.2f, 0.2f, 16, exGrate, false);
            foreach (float dz in new[] { -0.32f, 0.32f })   // L brackets
            {
                m.Box(ArchiveEast, y0 - 0.04f, z + dz - 0.02f, x1 + 0.05f, y0, z + dz + 0.02f, exGalvanized);
                m.Beam(new Vector3(ArchiveEast, y0 - 0.35f, z + dz), new Vector3(x1 + 0.03f, y0 - 0.02f, z + dz), 0.03f, 0.03f, exGalvanized);
            }
            m.Cylinder(new Vector3(x0 + 0.05f, y0 + 0.2f, z + 0.41f), new Vector3(ArchiveEast, y0 + 0.2f, z + 0.41f), 0.012f, 0.012f, 6, exGalvanized);
            m.Cylinder(new Vector3(x0 + 0.1f, y0 + 0.12f, z + 0.41f), new Vector3(ArchiveEast, y0 + 0.12f, z + 0.41f), 0.008f, 0.008f, 6, exGalvanized);
            m.Cylinder(new Vector3(x0 + 0.15f, y0, z + 0.3f), new Vector3(x0 + 0.17f, 0.03f, z + 0.3f), 0.008f, 0.008f, 5, exPvc);
            Decal("Mancha do dreno", facade, new Vector3(x0 + 0.17f, -0.004f, z + 0.3f), Vector3.up, Vector3.right, Vector3.forward, 0.45f, 0.4f, exStain);
        }
        // Garden tap with a hose on its reel.
        if (UnclaimedExteriorFinishes.Available("Agua_Jardim_DN25"))
            UnclaimedExteriorFinishes.Place("Agua_Jardim_DN25", facade, new Vector3(ArchiveEast,0,-10f), Vector3.right, "Agua - torneira e mangueira");
        else
        {
            Vector3 tap = new Vector3(ArchiveEast, 0.6f, -10.0f);
            m.Cylinder(tap, tap + Vector3.right * 0.08f, 0.012f, 0.012f, 6, exGalvanized);
            m.Box(tap + new Vector3(0.06f, 0.01f, -0.015f), tap + new Vector3(0.09f, 0.05f, 0.015f), exGalvanized);
            Vector3 reel = new Vector3(ArchiveEast + 0.12f, 0.95f, -10.0f);
            m.Box(new Vector3(ArchiveEast, 0.75f, -10.04f), new Vector3(ArchiveEast + 0.2f, 0.8f, -9.96f), exGalvanized);
            m.Cylinder(reel + Vector3.back * 0.12f, reel + Vector3.forward * 0.12f, 0.17f, 0.17f, 14, MatHose());
            m.Cylinder(tap + Vector3.right * 0.08f, new Vector3(ArchiveEast + 0.15f, 0.02f, -10.3f), 0.012f, 0.012f, 5, MatHose());
    
        }

        // Electricity: concrete service post behind the low wall, meter box in a masonry pillar, cables.
        Vector3 postFoot = new Vector3(-7.5f, -0.05f, LotS + 0.35f), postTop = postFoot + Vector3.up * 6.05f;
        m.Cylinder(postFoot, postTop, 0.1f, 0.08f, 10, exPolePaint);
        m.Box(-7.2f, -0.1f, LotS - 0.17f, -6.65f, 1.6f, LotS + 0.17f, exWallPlaster);
        m.Box(-7.225f, 1.6f, LotS - 0.195f, -6.625f, 1.64f, LotS + 0.195f, exConcrete);
        m.Box(-7.1f, 0.85f, LotS - 0.2f, -6.75f, 1.45f, LotS - 0.17f, exPvc);
        m.Box(-7.0f, 1.15f, LotS - 0.205f, -6.85f, 1.3f, LotS - 0.2f, exDarkGlass);
        m.Cylinder(new Vector3(-7.4f, 0.5f, LotS + 0.26f), postTop - Vector3.up * 0.3f + new Vector3(0.1f, 0, -0.09f), 0.02f, 0.02f, 6, exPvc);
        m.Box(postTop + new Vector3(-0.35f, -0.25f, -0.05f), postTop + new Vector3(0.35f, -0.17f, 0.05f), exGalvanized);   // cross-arm
        var wires = new ExtMesh();
        wires.Cable(postTop + new Vector3(0.3f, -0.2f, 0f), new Vector3(-7.0f, 3.75f, ReceptionFace - 0.05f), 0.35f, 0.014f, exCable, 8);
        wires.Cable(postTop + new Vector3(-0.3f, -0.2f, 0f), StreetPoleTop(-4.5f, true) + new Vector3(0f, -1.2f, 0f), 0.3f, 0.014f, exCable, 8);
        wires.Emit("Ramal de entrada", facade, false, 0.2f, ShadowCastingMode.Off);
        m.Emit("Instalacoes da fachada", facade, false);
        Collider("Colisor - poste de entrada", facade, -7.6f, LotS + 0.25f, -7.4f, LotS + 0.45f, 0f, 3f);

        // Access lighting: a floodlight in the nook and a low garden post with a milky globe by the path.
        FloodLight(facade, "Refletor - nicho do arquivo", new Vector3(ArchiveEast, 3.0f, -10.0f), Vector3.right, 120f, 2.5f, 10f, 3500f);
        Vector3 g = new Vector3(-13.0f, -0.05f, -16.8f);
        GameObject gardenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedPosteJardim.PrefabPath);
        if (gardenPrefab != null)
        {
            var garden = (GameObject)PrefabUtility.InstantiatePrefab(gardenPrefab);
            Undo.RegisterCreatedObjectUndo(garden, "Create Poste de jardim");
            garden.transform.SetParent(facade, false);
            garden.transform.SetPositionAndRotation(g, Quaternion.identity);
            garden.name = "Poste de jardim";
        }
        else
        {
            var garden = new ExtMesh();
            garden.Cylinder(g, g + Vector3.up * 0.25f, 0.09f, 0.08f, 10, exConcrete);
            garden.Cylinder(g, g + Vector3.up * 2.55f, 0.035f, 0.03f, 8, exRailing);
            garden.Cylinder(g + Vector3.up * 2.55f, g + Vector3.up * 2.6f, 0.07f, 0.07f, 10, exRailing);
            garden.Emit("Poste de jardim", facade, false);
            var globe = new ExtMesh();
            globe.Cylinder(g + Vector3.up * 2.6f, g + Vector3.up * 2.72f, 0.12f, 0.15f, 12, exGlobe, false);
            globe.Cylinder(g + Vector3.up * 2.72f, g + Vector3.up * 2.86f, 0.15f, 0.06f, 12, exGlobe);
            globe.Emit("Globo leitoso", facade, false, 0.2f, ShadowCastingMode.Off);
            Collider("Colisor - poste de jardim", facade, -13.1f, -16.9f, -12.9f, -16.7f, 0f, 2.6f);
        }
        ExteriorPoint("poste de jardim", g + Vector3.up * 2.72f, 6f, 1.5f, 2700f);
    }

    // Physical Blender cassette; old procedural plate remains the fallback if the kit is absent.
    private static void ExteriorSign(string name, Transform parent, Vector3 wallPoint, Vector3 normal,
        float width, float height, Material face, Material backing, float depth)
    {
        string asset = face.name;
        if (UnclaimedExteriorFinishes.Available(asset))
            UnclaimedExteriorFinishes.Place(asset, parent, wallPoint, normal, name);
        else Plaque(name,parent,wallPoint,normal,width,height,face,backing,depth);
    }

    private static Material MatHose() => Mat(EntornoMaterials, "Mangueira verde", new Color(0.12f, 0.32f, 0.16f), 0.35f);

    // The approved box floodlight (WallLamp model:false); its light moves to this exterior's own light group.
    private static void FloodLight(Transform parent, string name, Vector3 wallPoint, Vector3 normal, float angle, float intensity,
        float range, float kelvin)
    {
        WallLamp(parent, name, wallPoint, normal, angle, intensity, range, kelvin);
        Transform light = OutdoorLightsGroup().Find("Luz - " + name);
        if (light != null) light.SetParent(exteriorLights, true);
    }

    // ------------------------------------------------------------------ service yard (seen, not reachable)

    private static void BuildServiceYard(Transform yard)
    {
        var pave = new ExtMesh();
        pave.Box(14f, -0.18f, LotS - 0.1f, LotE - 0.075f, -0.02f, AutopsyFace, exDriveway);
        pave.Box(15.1f, -0.18f, AutopsyFace, LotE - 0.075f, -0.02f, 5.175f, exDriveway);
        pave.Box(8.6f, -0.15f, -8.6f, 14f, -0.02f, AutopsyFace, exScreed);
        pave.Emit("Acesso de servico", yard, true, 0.15f, ShadowCastingMode.Off);
        Collider("Colisor - acesso de servico", yard, 8.6f, LotS, LotE, 5.2f, -0.3f, -0.02f);
        // Worn parking lines under the carport.
        var lines = new ExtMesh();
        foreach (float x in new[] { 8.8f, 11.3f, 13.8f })
            lines.Box(x - 0.05f, -0.019f, -8.4f, x + 0.05f, -0.017f, -4.6f, exWhitePaint, 8);
        lines.Emit("Faixas das vagas", yard, false, 0.2f, ShadowCastingMode.Off);

        // Carport: four steel posts, beams and a gently sloped corrugated roof.
        var carport = new ExtMesh();
        float cx0 = 8.8f, cx1 = 13.8f, cz0 = -8.4f, cz1 = -4.2f;
        foreach (float x in new[] { cx0, cx1 })
            foreach (float z in new[] { cz0, cz1 })
                carport.Cylinder(new Vector3(x, -0.02f, z), new Vector3(x, z < -6 ? 2.6f : 2.88f, z), 0.05f, 0.05f, 8, exRailing);
        carport.Beam(new Vector3(cx0 - 0.1f, 2.62f, cz0), new Vector3(cx1 + 0.1f, 2.62f, cz0), 0.08f, 0.12f, exRailing);
        carport.Beam(new Vector3(cx0 - 0.1f, 2.9f, cz1), new Vector3(cx1 + 0.1f, 2.9f, cz1), 0.08f, 0.12f, exRailing);
        for (float x = cx0; x <= cx1 + 0.01f; x += (cx1 - cx0) / 4)
            carport.Beam(new Vector3(x, 2.7f, cz0 - 0.3f), new Vector3(x, 2.98f, cz1 + 0.25f), 0.05f, 0.1f, exRailing);
        Vector3 r0 = new Vector3(cx0 - 0.3f, 2.76f, cz0 - 0.4f), r1 = new Vector3(cx1 + 0.3f, 2.76f, cz0 - 0.4f);
        Vector3 r2 = new Vector3(cx1 + 0.3f, 3.04f, cz1 + 0.3f), r3 = new Vector3(cx0 - 0.3f, 3.04f, cz1 + 0.3f);
        Vector3 rn = Vector3.Cross(r3 - r0, r1 - r0).normalized;
        carport.Quad(r0, r1, r2, r3, rn, exMetalRoof);
        carport.Quad(r0 - rn * 0.01f, r1 - rn * 0.01f, r2 - rn * 0.01f, r3 - rn * 0.01f, -rn, exGalvanized);
        carport.Emit("Cobertura das vagas", yard, false);

        // Gatehouse: plastered block walls, a slab with a drip edge, windows to the gate, a lit desk inside.
        BuildGatehouse(yard);
    }

    private static void BuildGatehouse(Transform yard)
    {
        const float x0 = 11.0f, z0 = -18.35f, x1 = 13.4f, z1 = -15.95f, top = 2.6f;
        var m = new ExtMesh();
        var openings = new List<Opening>
        {
            new Opening(1, -17.6f, 0.8f, 0.0f, 2.1f, 'd'),          // door on the east face
            new Opening(1, -16.55f, 0.8f, 1.0f, 1.1f, 'v'),        // window to the drive
            new Opening(0, 12.2f, 1.4f, 1.0f, 1.1f, 'v'),          // window to the gate
        };
        Material plaster = Plaster("guarita", new Color(0.86f, 0.85f, 0.8f));
        var frames = new ExtMesh();
        HouseWalls(m, frames, x0, z0, x1, z1, -0.05f, top, plaster, openings);
        m.Box(x0 - 0.4f, top, z0 - 0.4f, x1 + 0.4f, top + 0.15f, z1 + 0.4f, exConcrete);
        m.Box(x0 - 0.42f, top - 0.03f, z0 - 0.42f, x1 + 0.42f, top, z1 + 0.42f, exConcrete, 63 - 8);
        m.Box(x0, -0.05f, z0, x1, 0.02f, z1, exScreed, 8);   // floor inside
        m.Box(x0, top - 0.005f, z0, x1, top, z1, exWhitePaint, 4);
        m.Emit("Guarita", yard, true, 0.3f);
        frames.Emit("Guarita - esquadrias", yard, false);
        // Interior seen through the windows: desk, chair, and a tube lamp that is on.
        var inside = new ExtMesh();
        inside.Box(11.25f, 0.72f, -18.15f, 12.9f, 0.76f, -17.6f, exDoorWood);
        inside.Box(11.3f, 0f, -18.1f, 11.35f, 0.72f, -17.65f, exDoorWood);
        inside.Box(12.8f, 0f, -18.1f, 12.85f, 0.72f, -17.65f, exDoorWood);
        inside.Box(12.0f, 0.42f, -17.4f, 12.42f, 0.46f, -17.0f, exRailing);
        inside.Box(12.0f, 0.46f, -17.02f, 12.42f, 0.9f, -16.98f, exRailing);
        inside.Box(12.18f, 0f, -17.22f, 12.24f, 0.42f, -17.16f, exGalvanized);
        inside.Box(11.4f, 0.76f, -18.1f, 11.75f, 1.0f, -17.95f, exRailing);        // small monitor
        inside.Emit("Guarita - interior", yard, false);
        var tube = new ExtMesh();
        tube.Box(11.6f, top - 0.06f, -17.2f, 12.8f, top - 0.005f, -17.08f, exLed);
        tube.Emit("Guarita - luminaria", yard, false, 0.2f, ShadowCastingMode.Off);
        ExteriorPoint("guarita - interior", new Vector3(12.2f, top - 0.2f, -17.15f), 4f, 0.4f, 4000f);
        ExteriorSign("Placa - portaria", yard, new Vector3(x1, 2.3f, -17.6f), Vector3.right, 0.5f, 0.19f,
            Mat(EntornoMaterials, "Placa_Guarita", Color.white, 0.25f, 0f, EntornoTextures + "Placa_Guarita.png"), exGalvanized, 0.006f);
        FloodLight(yard, "Refletor - guarita", new Vector3(x1, 2.45f, -16.9f), Vector3.right, 120f, 2.5f, 11f, 4000f);
    }
}
