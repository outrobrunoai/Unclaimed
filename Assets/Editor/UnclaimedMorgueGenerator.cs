// Place this file in Assets/Editor/UnclaimedMorgueGenerator.cs.
// Run Tools > Unclaimed > Generate Morgue Prototype in an open Unity scene.
// Room order, shape and connections follow the floor plan in Assets/Planta/planta_necroterio.png,
// but the layout is measured in meters so rooms can be sized for first-person play.
// Room finishes live in partial files next to this one (UnclaimedMorgueGenerator.Autopsia.cs).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class UnclaimedMorgueGenerator
{
    // Any object whose name starts with this prefix counts as an existing morgue,
    // including the older 8-room layout saved in Assets/Scenes/Morgue.unity.
    private const string GroupPrefix = "Unclaimed - Necroterio";
    private const string GroupName = GroupPrefix + " (planta)";
    private const string BaseFolder = "Assets/UnclaimedPrototype";

    // Layout coordinates are meters on wall centerlines: x = east, z = north.
    // A room's free interior is WallThickness smaller than its rect in each direction.
    // Room and door coordinates are multiples of Cell, so rooms rasterize exactly.
    private const float Cell = 0.125f;
    private const float MinX = -17f, MinZ = -16f, MaxX = 23.5f, MaxZ = 18f;

    private const float WallHeight = 3.2f;
    private const float WallThickness = 0.2f;
    private const float FenceHeight = 2.2f;
    private const float FenceThickness = 0.15f;
    private const float DoorHeight = 2.2f;

    // Roof and envelope: a slab on top of the walls, closed by a parapet with a coping.
    private const float SlabThickness = 0.2f;
    private const float ParapetHeight = 0.5f;
    private const float CopingThickness = 0.05f;
    private const float FacadeBaseHeight = 0.6f;

    // Trim. Everything below is visual only (no colliders), so the approved passages never change.
    private const float FinishOffset = 0.003f;
    private const float SkirtingHeight = 0.1f, SkirtingThickness = 0.015f;
    private const float FrameLining = 0.03f, FrameProjection = 0.01f;
    private const float CasingWidth = 0.07f, CasingThickness = 0.02f;
    private const float ThresholdHeight = 0.006f;

    // DrawerFaceX/DrawerZ and related dimensions serve only the cold chamber prefab fallback.
    private const float DrawerFaceX = 12.7f, DrawerBackX = 14.9f, DrawerWidth = 0.9f;
    private static readonly float[] DrawerZ = { -3f, -2.05f, -1.1f, 2f, 2.95f, 3.9f };

    private enum Kind { Exterior, Interior, Patio }

    private sealed class Zone
    {
        public string Name;
        public Kind Kind;
        public Material Floor;
        public Rect[] Rects;
    }

    // Finish layers for a room, drawn as visual-only panels in front of the structural boxes.
    private sealed class ZoneFinish
    {
        public Material Floor, Skirting, Wainscot, UpperWall, Ceiling;
        public float WainscotHeight;
        // Optional trims: a slim strip where the wainscot meets the upper wall (paint line, tile edge
        // profile) and an angle along the ceiling (ceiling trim / moulding).
        public Material Friso, CeilingTrim;
    }

    private readonly struct Door
    {
        public readonly string Name;
        public readonly bool Vertical; // true: the opening is in a wall running north-south (x = Line)
        public readonly float Line, Center, Width;
        public readonly bool Header;
        // Prefab of a modelled frame (and door) for this opening; replaces the generic jambs, casings and sill.
        public readonly string Frame;
        public readonly float Yaw;

        public Door(string name, bool vertical, float line, float center, float width, bool header = true, string frame = null, float? yaw = null)
        {
            Name = name; Vertical = vertical; Line = line; Center = center; Width = width; Header = header; Frame = frame;
            Yaw = yaw ?? (vertical ? -90f : 0f);
        }
    }

    private static string PortaPrefab(string name) => UnclaimedPortasAssets.PrefabPath(name);

    private static readonly Door[] Doors =
    {
        new Door("Entrada principal", false, -12.375f, -11.375f, 2f,
            frame: UnclaimedRecepcaoAssets.Folder + "/" + UnclaimedRecepcaoAssets.EntranceFramePrefab + ".prefab"),
        // 1.0 m like a bathroom door (was a 1.5 m gap); same axis. Frame, leaf and hardware are one prefab.
        new Door("Porta WC", false, -7.125f, -11.5f, 1.0f,
            frame: UnclaimedRecepcaoAssets.WCFolder + "/" + UnclaimedRecepcaoAssets.WCDoorPrefab + ".prefab"),
        // Restricted circulation: active leaf + fixed leaf, locked until the player has the key from the support
        // cabinet (first puzzle: reception computer -> padlock -> key -> this door).
        new Door("Porta recepcao - circulacao", false, -6f, -8f, 1.5f,
            frame: UnclaimedRecepcaoAssets.Folder + "/" + UnclaimedRecepcaoAssets.CirculationDoorPrefab + ".prefab"),
        new Door("Porta recepcao - arquivo", true, -6.5f, -8f, 1.5f,
            frame: UnclaimedRecepcaoAssets.Folder + "/" + UnclaimedRecepcaoAssets.ArchiveDoorPrefab + ".prefab", yaw: 90f),
        // Interior door kit (Blender/Portas, UnclaimedPortasAssets): families per use; they swing into the room, away from the corridor.
        new Door("Porta arquivo - circulacao", true, -2f, -4.75f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Adm), yaw: 90f),
        new Door("Porta descanso", true, -5.25f, -0.25f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Descanso), yaw: -90f),
        new Door("Porta deposito", true, -4.5f, 3.5f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Deposito), yaw: -90f),
        new Door("Porta sala tecnica", false, 5.75f, -3.5f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Tecnica), yaw: 0f),
        new Door("Porta autopsia - circulacao", true, -2f, 0f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Clinica), yaw: 90f),
        // Cold-room doors open out of the cold room (back face inside), with an inside safety release.
        new Door("Porta autopsia - camara fria", true, 7.5f, 0.25f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Fria), yaw: -90f),
        new Door("Porta camara fria - recebimento", false, 5.25f, 9.75f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Fria), yaw: 0f),
        new Door("Porta circulacao - vestibulo", true, 3.25f, 4.5f, 1.25f),
        // Not drawn on the plan: closes the service loop corridor -> recebimento -> camara fria -> autopsia.
        new Door("Porta vestibulo - recebimento", false, 5.25f, 5.375f, 1.5f, frame: PortaPrefab(UnclaimedPortasAssets.Clinica), yaw: 0f),
        new Door("Porta dupla recebimento - patio", true, 12.75f, 8.375f, 2f),
        new Door("Portao de servico", false, 5.25f, 18f, 2.5f, header: false),
    };

    private static Transform root;
    private static Transform floorsGroup, wallsGroup, claddingGroup, skirtingGroup, doorsGroup, ceilingsGroup,
        envelopeGroup, roomsGroup, lightsGroup;
    private static int[,] grid;
    private static bool[] doorUsed;
    private static Dictionary<string, ZoneFinish> finishes;
    private static Material floorPublic, floorStaff, floorClinical, corridorMat, patioMat, exteriorMat;
    private static Material wallMat, furnitureMat, bodyMat, clueMat;
    private static Material ceilingMat, frameMat, thresholdMat, skirtingMat, facadeMat, facadeBaseMat, copingMat;

    [MenuItem("Tools/Unclaimed/Generate Morgue Prototype")]
    public static void Generate()
    {
        GameObject existing = FindExistingMorgue(SceneManager.GetActiveScene());
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.LogWarning($"Unclaimed: '{existing.name}' already exists in scene '{existing.scene.name}'. " +
                "Generation skipped to avoid a duplicate; delete or rename that group yourself to generate again.",
                existing);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Unclaimed morgue prototype");

        var container = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(container, "Generate morgue");
        root = container.transform;

        PrepareMaterials();
        finishes = new Dictionary<string, ZoneFinish> { ["Autopsia"] = AutopsiaFinish() };
        finishes["Recepcao"] = RecepcaoFinish();   // after the autopsy: reuses its ceiling, decals and fittings
        finishes["WC"] = WCFinish();
        finishes["Arquivo e escritorio"] = ArquivoFinish();
        corridorLegs = null;
        finishes[CirculacaoRoom] = CorridorFinish(0);   // leg 0; the other legs are picked per wall face (CladCorridor)
        finishes[VestibuleRoom] = CorridorFinish(2);
        finishes[DescansoRoom] = DescansoFinish();
        finishes["Deposito"] = DepositoFinish();
        finishes["Sala tecnica"] = TecnicaFinish();

        floorsGroup = Group("Pisos");
        wallsGroup = Group("Paredes");
        claddingGroup = Group("Revestimentos");
        skirtingGroup = Group("Rodapes");
        doorsGroup = Group("Portas");
        ceilingsGroup = Group("Tetos");
        envelopeGroup = Group("Fechamento externo");
        Transform outside = Group("Area externa"), furniture = Group("Moveis");
        roomsGroup = Group("Ambientes");
        Transform clues = Group("Pistas"), markers = Group("Marcadores");
        lightsGroup = Group("Luzes");

        List<Zone> zones = Zones();
        grid = Rasterize(zones);
        doorUsed = new bool[Doors.Length];
        Floors(zones);
        Walls(zones);
        Doorways(zones);
        StopStripsAtCasings();   // UnclaimedMorgueGenerator.Acabamentos.cs
        UnifyWallFinishes();
        Ceilings(zones);
        Exterior(outside);
        Furniture(furniture);
        BuildAutopsia(Sub(roomsGroup, "Autopsia"));
        BuildRecepcao(Sub(roomsGroup, "Recepcao"));
        BuildArquivo(Sub(roomsGroup, "Arquivo e escritorio"));
        BuildWC(Sub(roomsGroup, "WC"));
        BuildDescanso(Sub(roomsGroup, DescansoRoom));
        BuildDeposito(Sub(roomsGroup, "Deposito"));
        BuildTecnica(Sub(roomsGroup, "Sala tecnica"));
        BuildCirculacao(Sub(roomsGroup, CirculacaoRoom));
        Clues(clues);
        UnclaimedRecebimentoAssets.InstallGenerated(Sub(roomsGroup, "Recebimento de corpos"));
        PlaceRecebimentoDocuments(Sub(roomsGroup, "Recebimento de corpos"));
        UnclaimedPatioRecebimentoAssets.InstallGenerated(outside);
        Markers(markers);
        UnclaimedCamaraFriaAssets.InstallGenerated(Sub(roomsGroup, "Camara fria"));
        RoomLights(ceilingsGroup);
        CorridorLights(ceilingsGroup);
        NightEnvironment();
        PrepareStaticLighting(zones);

        for (int d = 0; d < Doors.Length; d++)
            if (!doorUsed[d])
                Debug.LogWarning($"Unclaimed: door '{Doors[d].Name}' is not on a wall between two areas; check its coordinates.");

        Selection.activeGameObject = container;
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Unclaimed: prototype generated from planta_necroterio.png. Save the scene and add your player controller at INICIO.");
    }

    // Shared materials (created once, reused when they already exist); the corridor patch needs them too.
    private static void PrepareMaterials()
    {
        floorPublic = GetMaterial("Floor - publico", new Color(0.44f, 0.40f, 0.33f));
        floorStaff = GetMaterial("Floor - equipe", new Color(0.31f, 0.36f, 0.42f));
        floorClinical = GetMaterial("Floor - clinico", new Color(0.30f, 0.40f, 0.37f));
        corridorMat = GetMaterial("Corridor", new Color(0.20f, 0.25f, 0.27f));
        patioMat = GetMaterial("Patio", new Color(0.40f, 0.40f, 0.38f));
        exteriorMat = GetMaterial("Exterior", new Color(0.17f, 0.19f, 0.16f));
        wallMat = GetMaterial("Walls", new Color(0.68f, 0.72f, 0.69f));
        furnitureMat = GetMaterial("Furniture", new Color(0.28f, 0.36f, 0.40f));
        bodyMat = GetMaterial("Body placeholders", new Color(0.58f, 0.60f, 0.56f));
        clueMat = GetMaterial("Clue placeholders", new Color(0.90f, 0.66f, 0.17f));
        ceilingMat = Mat(BaseFolder, "Teto", new Color(0.80f, 0.80f, 0.78f), 0.1f);
        frameMat = Mat(BaseFolder, "Batente", new Color(0.50f, 0.52f, 0.50f), 0.35f);
        thresholdMat = Mat(BaseFolder, "Soleira", new Color(0.24f, 0.24f, 0.24f), 0.45f);
        skirtingMat = Mat(BaseFolder, "Rodape", new Color(0.24f, 0.25f, 0.25f), 0.3f);
        facadeMat = Mat(BaseFolder, "Fachada", new Color(0.60f, 0.60f, 0.56f), 0.1f);
        facadeBaseMat = Mat(BaseFolder, "Barrado externo", new Color(0.33f, 0.33f, 0.31f), 0.15f);
        copingMat = Mat(BaseFolder, "Capeamento", new Color(0.46f, 0.47f, 0.47f), 0.35f, 0.3f);
    }

    private static GameObject FindExistingMorgue(Scene scene)
    {
        foreach (GameObject top in scene.GetRootGameObjects())
            foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(GroupPrefix, StringComparison.Ordinal))
                    return t.gameObject;
        return null;
    }

    // Free interior sizes are noted for the rooms sized for first-person play.
    private static List<Zone> Zones() => new List<Zone>
    {
        // Free 8.05 x 5.05 m, plus the alcove under the corridor that holds the corridor door.
        Z("Recepcao", Kind.Interior, floorPublic, R(-14.75f, -12.375f, -6.5f, -7.125f), R(-10.375f, -7.125f, -6.5f, -6f)),
        Z("WC", Kind.Interior, floorStaff, R(-14.75f, -7.125f, -10.375f, -3.875f)),                 // free 4.175 x 3.05 m
        Z("Arquivo e escritorio", Kind.Interior, floorPublic, R(-6.5f, -10.25f, 3.5f, -6f), R(-2f, -6f, 3.5f, -3.5f)),
        Z("Descanso e vestiario", Kind.Interior, floorStaff, R(-13f, -3.875f, -5.25f, 0.875f)),
        Z("Deposito", Kind.Interior, floorStaff, R(-8f, 0.875f, -4.5f, 6f)),
        Z("Sala tecnica", Kind.Interior, floorStaff, R(-4.5f, 5.75f, 3.25f, 9f)),
        Z("Autopsia", Kind.Interior, floorClinical, R(-2f, -3.5f, 7.5f, 3.625f)),
        Z("Camara fria", Kind.Interior, floorClinical, R(7.5f, -3.5f, 15f, 5.25f)),                  // free 7.3 x 8.55 m
        Z("Recebimento de corpos", Kind.Interior, floorClinical, R(3.25f, 5.25f, 12.75f, 11.5f)),    // free 9.3 x 6.05 m
        // Reception branch runs east, turns north past the staff rooms, then east toward the service wing.
        Z("Circulacao", Kind.Interior, corridorMat,
            R(-10.375f, -6f, -2f, -3.875f), R(-5.25f, -3.875f, -2f, 0.875f), R(-4.5f, 0.875f, -2f, 3.625f), R(-4.5f, 3.625f, 3.25f, 5.75f)),
        Z("Vestibulo de servico", Kind.Interior, corridorMat, R(3.25f, 3.625f, 7.5f, 5.25f)),
        // Free about 8.1 x 10.1 m, plus the strip wrapping the recebimento's north-east corner.
        Z("Patio de servico", Kind.Patio, patioMat, R(12.75f, 5.25f, 21f, 15.5f), R(9.25f, 11.5f, 12.75f, 13.25f)),
    };

    private static Zone Z(string name, Kind kind, Material floor, params Rect[] rects) =>
        new Zone { Name = name, Kind = kind, Floor = floor, Rects = rects };

    private static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0, z0, x1, z1);

    private static int G(float meters) => Mathf.RoundToInt(meters / Cell);

    private static void Floors(List<Zone> zones)
    {
        foreach (Zone zone in zones)
            for (int i = 0; i < zone.Rects.Length; i++)
            {
                Rect r = zone.Rects[i];
                string suffix = zone.Rects.Length > 1 ? " " + (i + 1) : "";
                Block("Piso - " + zone.Name + suffix, floorsGroup, r.xMin, r.yMin, r.xMax, r.yMax, -0.2f, 0.2f, zone.Floor);
                if (finishes.TryGetValue(zone.Name, out ZoneFinish f) && f.Floor != null)
                    Panel("Piso acabado - " + zone.Name + suffix, Sub(claddingGroup, zone.Name),
                        new Vector3(r.xMin, FinishOffset, r.yMin), Vector3.right, Vector3.forward, r.width, r.height, Vector3.up,
                        CorridorFloor(zone.Name, i, f.Floor));
            }
    }

    // Roof slab over every room; it is also the visible ceiling where a room has no ceiling finish.
    private static void Ceilings(List<Zone> zones)
    {
        foreach (Zone zone in zones)
        {
            if (zone.Kind != Kind.Interior) continue;
            finishes.TryGetValue(zone.Name, out ZoneFinish f);
            for (int i = 0; i < zone.Rects.Length; i++)
            {
                Rect r = zone.Rects[i];
                string suffix = zone.Rects.Length > 1 ? " " + (i + 1) : "";
                Block("Laje - " + zone.Name + suffix, ceilingsGroup, r.xMin, r.yMin, r.xMax, r.yMax, WallHeight, SlabThickness, ceilingMat);
                if (f?.Ceiling != null)
                    Panel("Forro - " + zone.Name + suffix, ceilingsGroup, new Vector3(r.xMin, WallHeight - FinishOffset, r.yMin),
                        Vector3.right, Vector3.forward, r.width, r.height, Vector3.down, f.Ceiling);
            }
        }
    }

    // Grid of zone ids (0 = exterior) at Cell resolution; walls go wherever neighbouring ids differ.
    private static int[,] Rasterize(List<Zone> zones)
    {
        var ids = new int[G(MaxX - MinX), G(MaxZ - MinZ)];
        for (int z = 0; z < zones.Count; z++)
            foreach (Rect r in zones[z].Rects)
                for (int i = G(r.xMin - MinX); i < G(r.xMax - MinX); i++)
                    for (int j = G(r.yMin - MinZ); j < G(r.yMax - MinZ); j++)
                        ids[i, j] = z + 1;
        return ids;
    }

    private static void Walls(List<Zone> zones)
    {
        int w = grid.GetLength(0), h = grid.GetLength(1);
        for (int i = 1; i < w; i++)
            WallLine(zones, true, i, h, j => grid[i - 1, j], j => grid[i, j]);
        for (int j = 1; j < h; j++)
            WallLine(zones, false, j, w, i => grid[i, j - 1], i => grid[i, j]);
    }

    // Merges the cell edges along one grid line into wall segments, leaving door spans open,
    // then dresses both faces of each segment.
    private static void WallLine(List<Zone> zones, bool vertical, int line, int count,
        Func<int, int> before, Func<int, int> after)
    {
        var none = new Vector2Int(-1, -1);
        var pairs = new Vector2Int[count];
        var door = new bool[count];
        var low = new int[count];
        var high = new int[count];
        for (int k = 0; k < count; k++)
        {
            low[k] = before(k);
            high[k] = after(k);
            int d = low[k] != high[k] ? DoorAt(vertical, line, k) : -1;
            door[k] = d >= 0;
            if (door[k]) doorUsed[d] = true;
            pairs[k] = low[k] == high[k] || door[k] ? none : new Vector2Int(Mathf.Min(low[k], high[k]), Mathf.Max(low[k], high[k]));
        }

        int start = 0;
        for (int k = 1; k <= count; k++)
        {
            if (k < count && pairs[k] == pairs[start]) continue;
            if (pairs[start] != none)
            {
                WallSegment(zones, vertical, line, pairs[start], start, k, start > 0 && !door[start - 1], k < count && !door[k]);
                for (int side = 0; side < 2; side++)
                {
                    int[] sideZones = side == 0 ? low : high;
                    int zone = sideZones[start], other = side == 0 ? high[start] : low[start];
                    WallFace(zones, vertical, line, side == 1, zone, other, start, k,
                        CornerWrap(start - 1, sideZones, zone, door, pairs, none), CornerWrap(k, sideZones, zone, door, pairs, none));
                }
            }
            start = k;
        }
    }

    // How far a face layer runs past the end of its wall: around a convex corner it wraps over the wall's end;
    // at a door jamb, an inner corner or where the same room's face continues it stops at the line end.
    private static bool CornerWrap(int edge, int[] sideZones, int zone, bool[] door, Vector2Int[] pairs, Vector2Int none) =>
        edge >= 0 && edge < door.Length && !door[edge] && sideZones[edge] == zone && pairs[edge] == none;

    // Index of the door covering edge k of a grid line, or -1.
    private static int DoorAt(bool vertical, int line, int k)
    {
        float lineMin = vertical ? MinX : MinZ, alongMin = vertical ? MinZ : MinX;
        for (int d = 0; d < Doors.Length; d++)
        {
            Door door = Doors[d];
            if (door.Vertical != vertical || G(door.Line - lineMin) != line) continue;
            if (k >= G(door.Center - door.Width / 2 - alongMin) && k + 1 <= G(door.Center + door.Width / 2 - alongMin))
                return d;
        }
        return -1;
    }

    // Ends are stretched by half a thickness to close corners, except where they frame a door.
    private static void WallSegment(List<Zone> zones, bool vertical, int line, Vector2Int pair,
        int from, int to, bool extendStart, bool extendEnd)
    {
        bool fence = KindOf(zones, pair.x) != Kind.Interior && KindOf(zones, pair.y) != Kind.Interior;
        float thickness = fence ? FenceThickness : WallThickness;
        float height = fence ? FenceHeight : WallHeight;

        float at = (vertical ? MinX : MinZ) + line * Cell;
        float a = (vertical ? MinZ : MinX) + from * Cell, b = (vertical ? MinZ : MinX) + to * Cell;
        if (extendStart) a -= thickness * 0.5f;
        if (extendEnd) b += thickness * 0.5f;

        string name = fence ? "Muro do patio"
            : pair.x == 0 ? "Parede externa - " + zones[pair.y - 1].Name
            : "Parede " + zones[pair.x - 1].Name + " / " + zones[pair.y - 1].Name;
        LineBox(name, wallsGroup, vertical, at, a, b, -thickness / 2, thickness / 2, 0, height, wallMat, true);

        // Building walls facing outside carry the parapet that closes the roof.
        bool outer = !fence && (KindOf(zones, pair.x) != Kind.Interior || KindOf(zones, pair.y) != Kind.Interior);
        if (outer) Parapet(vertical, at, a, b, extendStart, extendEnd);
    }

    private static void Parapet(bool vertical, float at, float a, float b, bool extendStart, bool extendEnd)
    {
        const float half = WallThickness / 2, lip = 0.05f;
        float top = WallHeight + SlabThickness + ParapetHeight;
        LineBox("Platibanda", envelopeGroup, vertical, at, a, b, -half, half, WallHeight, top, facadeMat, true);
        LineBox("Capeamento", envelopeGroup, vertical, at, a - (extendStart ? lip : 0), b + (extendEnd ? lip : 0),
            -half - lip, half + lip, top, top + CopingThickness, copingMat, false);
    }

    // Skirting and cladding on the room side, facade on the outside; fences stay bare.
    private static void WallFace(List<Zone> zones, bool vertical, int line, bool highSide, int zone, int other,
        int from, int to, bool wrapStart, bool wrapEnd)
    {
        Kind kind = KindOf(zones, zone), otherKind = KindOf(zones, other);
        if (kind != Kind.Interior && otherKind != Kind.Interior) return;

        float at = (vertical ? MinX : MinZ) + line * Cell;
        float sign = highSide ? 1f : -1f;
        float face = at + sign * WallThickness / 2;
        float a = (vertical ? MinZ : MinX) + from * Cell, b = (vertical ? MinZ : MinX) + to * Cell;
        float wrapPanel = WallThickness / 2 + FinishOffset;

        if (kind == Kind.Interior)
        {
            string room = zones[zone - 1].Name;
            finishes.TryGetValue(room, out ZoneFinish f);
            float wrapStrip = WallThickness / 2 + StripWrap(vertical, SkirtingThickness);
            FaceStrip("Rodape - " + room, skirtingGroup, vertical, face, sign,
                a - (wrapStart ? wrapStrip : 0), b + (wrapEnd ? wrapStrip : 0), 0, SkirtingHeight, SkirtingThickness, f?.Skirting ?? skirtingMat);
            if (f != null)
                CladFace(room, f, vertical, face, sign, a - (wrapStart ? wrapPanel : 0), b + (wrapEnd ? wrapPanel : 0), 0,
                    wrapStart, wrapEnd);
        }
        else
        {
            float a2 = a - (wrapStart ? wrapPanel : 0), b2 = b + (wrapEnd ? wrapPanel : 0);
            FacePanel("Barrado externo", envelopeGroup, vertical, face, sign, a2, b2, -0.02f, FacadeBaseHeight, facadeBaseMat);
            FacePanel("Fachada", envelopeGroup, vertical, face, sign, a2, b2, FacadeBaseHeight, WallHeight, facadeMat);
        }
    }

    // Around a convex corner the two faces' strips (skirting, friso, ceiling trim) must not overlap, or their
    // coplanar end faces flicker between two lightmaps: the strip on a line along X runs over the corner by its
    // own depth and covers it, the strip along Z butts against it.
    private static float StripWrap(bool vertical, float depth) => vertical ? 0f : depth;

    // Wainscot up to the finish's height and paint above it, clipped to start at `fromY`. a and b already
    // include the panels' wrap round convex corners (wrapStart / wrapEnd say where).
    private static void CladFace(string room, ZoneFinish f, bool vertical, float face, float sign, float a, float b, float fromY,
        bool wrapStart = false, bool wrapEnd = false)
    {
        // The corridors change finish leg by leg (UnclaimedMorgueGenerator.Circulacao.cs).
        if (room == CirculacaoRoom || room == VestibuleRoom) CladCorridor(room, vertical, face, sign, a, b, fromY, wrapStart, wrapEnd);
        else CladFaceCore(room, f, vertical, face, sign, a, b, fromY, wrapStart, wrapEnd);
    }

    private static void CladFaceCore(string room, ZoneFinish f, bool vertical, float face, float sign, float a, float b, float fromY,
        bool wrapStart = false, bool wrapEnd = false)
    {
        Transform parent = Sub(claddingGroup, room);
        if (f.Wainscot != null && f.WainscotHeight > fromY)
            FacePanel("Revestimento - " + room, parent, vertical, face, sign, a, b, fromY, Mathf.Min(f.WainscotHeight, WallHeight), f.Wainscot);
        if (f.UpperWall != null)
            FacePanel("Pintura - " + room, parent, vertical, face, sign, a, b, Mathf.Max(f.WainscotHeight, fromY), WallHeight, f.UpperWall);
        if (f.Friso != null && f.WainscotHeight > fromY && f.WainscotHeight < WallHeight)
        {
            const float depth = 0.003f;
            float w = StripWrap(vertical, depth);
            FaceStrip("Friso - " + room, parent, vertical, face + sign * FinishOffset, sign, a - (wrapStart ? w : 0), b + (wrapEnd ? w : 0),
                f.WainscotHeight - 0.006f, f.WainscotHeight + 0.006f, depth, f.Friso);
        }
        if (f.CeilingTrim != null)
        {
            const float depth = 0.012f;
            float w = StripWrap(vertical, depth);
            FaceStrip("Arremate do forro - " + room, parent, vertical, face + sign * FinishOffset, sign, a - (wrapStart ? w : 0), b + (wrapEnd ? w : 0),
                WallHeight - 0.025f, WallHeight - FinishOffset, depth, f.CeilingTrim);
        }
    }

    private static Kind KindOf(List<Zone> zones, int id) => id == 0 ? Kind.Exterior : zones[id - 1].Kind;

    // Each doorway: header above the opening (with collider, as before), frame linings, casings on both faces,
    // a threshold, the finish of the room on each side of the header and, on outer walls, the parapet above it.
    private static void Doorways(List<Zone> zones)
    {
        const float half = WallThickness / 2;
        foreach (Door d in Doors)
        {
            Transform g = Sub(doorsGroup, d.Name);
            float a = d.Center - d.Width / 2, b = d.Center + d.Width / 2;
            int lineIndex = G(d.Line - (d.Vertical ? MinX : MinZ)), alongIndex = G(d.Center - (d.Vertical ? MinZ : MinX));
            int low = d.Vertical ? grid[lineIndex - 1, alongIndex] : grid[alongIndex, lineIndex - 1];
            int high = d.Vertical ? grid[lineIndex, alongIndex] : grid[alongIndex, lineIndex];

            if (!d.Header)
            {
                // Gate in the yard fence: masonry posts just outside the opening.
                LineBox("Pilar do portao", g, d.Vertical, d.Line, a - 0.3f, a, -0.15f, 0.15f, 0, FenceHeight + 0.2f, facadeMat, false);
                LineBox("Pilar do portao", g, d.Vertical, d.Line, b, b + 0.3f, -0.15f, 0.15f, 0, FenceHeight + 0.2f, facadeMat, false);
                continue;
            }

            LineBox("Verga", g, d.Vertical, d.Line, a, b, -half, half, DoorHeight, WallHeight, wallMat, true);
            var framePrefab = d.Frame != null ? AssetDatabase.LoadAssetAtPath<GameObject>(d.Frame) : null;
            if (framePrefab != null)
            {
                // Modelled frame: its origin is the opening's centre on the floor, its front (-Z) on the
                // low side of the wall line.
                var frame = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, g);
                Undo.RegisterCreatedObjectUndo(frame, "Create " + d.Name + " frame");
                frame.transform.SetPositionAndRotation(d.Vertical ? new Vector3(d.Line, 0f, d.Center) : new Vector3(d.Center, 0f, d.Line),
                    Quaternion.Euler(0f, d.Yaw, 0f));
                foreach (FolhaDePorta leaf in frame.GetComponentsInChildren<FolhaDePorta>()) leaf.AplicarEstadoInicial();
            }
            if (framePrefab == null && d.Name == ArchiveDoorName) ArchiveDoorLeaf(g, d);
            if (d.Name == UnclaimedPatioRecebimentoAssets.DoorGroup) UnclaimedPatioRecebimentoAssets.PlaceDoorIn(g);
            float depth = half + FrameProjection;
            if (framePrefab == null)
            {
                LineBox("Batente", g, d.Vertical, d.Line, a, a + FrameLining, -depth, depth, 0, DoorHeight, frameMat, false);
                LineBox("Batente", g, d.Vertical, d.Line, b - FrameLining, b, -depth, depth, 0, DoorHeight, frameMat, false);
                // Head and threshold fit between the side linings: overlapping them, two boxes shared faces at the corners.
                LineBox("Batente superior", g, d.Vertical, d.Line, a + FrameLining, b - FrameLining, -depth, depth, DoorHeight - FrameLining, DoorHeight, frameMat, false);
                LineBox("Soleira", g, d.Vertical, d.Line, a + FrameLining, b - FrameLining, -depth, depth, 0, ThresholdHeight, thresholdMat, false);
            }
            foreach (float sign in new[] { -1f, 1f })
            {
                float c0 = sign * half, c1 = sign * (half + CasingThickness);
                if (framePrefab == null)
                {
                    LineBox("Guarnicao", g, d.Vertical, d.Line, a - CasingWidth, a, Mathf.Min(c0, c1), Mathf.Max(c0, c1), 0, DoorHeight, frameMat, false);
                    LineBox("Guarnicao", g, d.Vertical, d.Line, b, b + CasingWidth, Mathf.Min(c0, c1), Mathf.Max(c0, c1), 0, DoorHeight, frameMat, false);
                    LineBox("Guarnicao", g, d.Vertical, d.Line, a - CasingWidth, b + CasingWidth, Mathf.Min(c0, c1), Mathf.Max(c0, c1), DoorHeight, DoorHeight + CasingWidth, frameMat, false);
                }

                int zone = sign > 0 ? high : low, other = sign > 0 ? low : high;
                float face = d.Line + sign * half;
                if (KindOf(zones, zone) == Kind.Interior)
                {
                    if (finishes.TryGetValue(zones[zone - 1].Name, out ZoneFinish f))
                        CladFace(zones[zone - 1].Name, f, d.Vertical, face, sign, a, b, DoorHeight);
                }
                else if (KindOf(zones, other) == Kind.Interior)
                {
                    FacePanel("Fachada", envelopeGroup, d.Vertical, face, sign, a, b, DoorHeight, WallHeight, facadeMat);
                }
            }

            bool outer = (KindOf(zones, low) == Kind.Interior) != (KindOf(zones, high) == Kind.Interior);
            if (outer)
            {
                float top = WallHeight + SlabThickness + ParapetHeight;
                LineBox("Platibanda", envelopeGroup, d.Vertical, d.Line, a, b, -half, half, WallHeight, top, facadeMat, true);
                LineBox("Capeamento", envelopeGroup, d.Vertical, d.Line, a, b, -half - 0.05f, half + 0.05f, top, top + CopingThickness, copingMat, false);
            }
        }
    }

    // Furniture keeps human scale; it is placed per room and never scaled with the rooms.
    // The autopsy room is furnished in BuildAutopsia.
    private static void Furniture(Transform p)
    {
        // Recepcao: layout and Blender models in UnclaimedMorgueGenerator.Recepcao.cs.
        ReceptionFurniture(p);


        // Descanso e vestiario: lockers, benches and the coffee corner come from BuildDescanso (Descanso.cs).

        // Technical-room equipment comes from BuildTecnica (Tecnica.cs).

        // Recebimento: fixtures on the walls, stretcher in the middle with at least 1.2 m all around it.
        Block("Pia de lavagem de corpos", p, 3.45f, 7.3f, 4.35f, 9.5f, 0, 0.9f, furnitureMat);
        Block("Bancada com pia - recebimento", p, 5f, 10.75f, 7f, 11.35f, 0, 0.9f, furnitureMat);
        Block("Maca de recebimento", p, 6.95f, 8f, 9.05f, 8.75f, 0, 0.85f, furnitureMat);
        Block("Corpo provisório - recebimento", p, 7.15f, 8.05f, 8.85f, 8.7f, 0.85f, 0.15f, bodyMat);
        Block("Mesa auxiliar", p, 11.8f, 10.75f, 12.6f, 11.35f, 0, 0.8f, furnitureMat);

        // Keep the provisional equipment only until the modular prefab has been built.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedCamaraFriaAssets.Prefab) == null)
        {
            for (int i = 0; i < DrawerZ.Length; i++)
            {
                float z = DrawerZ[i];
                Block("Gaveta mortuaria " + (i + 1), p, DrawerFaceX, z, DrawerBackX, z + DrawerWidth, 0, 1.1f, furnitureMat);
                Block("Corpo provisório " + (i + 1), p, DrawerFaceX + 0.2f, z + 0.125f, DrawerBackX - 0.2f, z + DrawerWidth - 0.125f,
                    1.1f, 0.14f, bodyMat);
            }
            Block("Maca da camara fria", p, 12.75f, 0.5f, 14.85f, 1.25f, 0, 0.85f, furnitureMat);
        }

        if (!HasAdminPrefab("Mesa do servidor")) OfficeDesk(p);
        if (!HasAdminPrefab("Arquivo de aco")) Block("Arquivo de prontuarios", p, 2.65f, -6.5f, 3.25f, -4f, 0, 1.6f, furnitureMat);
        if (!HasAdminPrefab("Estante de aco") && !HasAdminPrefab("Estante da coordenacao")) Block("Estante de pastas", p, 2.65f, -8.5f, 3.25f, -6.75f, 0, 2f, furnitureMat);
        if (!HasAdminPrefab("Armario de pertences")) Block("Armario de pertences", p, 2.65f, -10f, 3.25f, -8.75f, 0, 1.8f, furnitureMat);

        // Patio: equipment against the north and east fences keeps the yard open for vehicles.
        Block("Cacamba", p, 19.1f, 13.45f, 20.85f, 15.35f, 0, 1.4f, furnitureMat);
        Block("Paletes", p, 17.1f, 14.25f, 18.3f, 15.35f, 0, 0.3f, furnitureMat);
        Block("Armarios de servico", p, 20.25f, 10f, 20.85f, 11.2f, 0, 1.2f, furnitureMat);
    }

    // Standard office desk (1.8 x 0.9 m, top at 0.75 m) with a swivel chair whose seat sits below the top.
    private static void OfficeDesk(Transform p)
    {
        Transform desk = Solid("Mesa do escritorio", p, -3.525f, -8.45f, -1.725f, -7.55f, 0, 0.75f);
        Piece("Tampo", desk, -3.525f, 0.71f, -8.45f, -1.725f, 0.75f, -7.55f, furnitureMat);
        Piece("Painel lateral", desk, -3.5f, 0, -8.42f, -3.47f, 0.71f, -7.58f, furnitureMat);
        Piece("Painel lateral", desk, -1.78f, 0, -8.42f, -1.75f, 0.71f, -7.58f, furnitureMat);
        Piece("Painel frontal", desk, -3.47f, 0.2f, -8.43f, -1.78f, 0.71f, -8.41f, furnitureMat);
        Piece("Gaveteiro", desk, -2.2f, 0.12f, -8.4f, -1.78f, 0.7f, -7.65f, furnitureMat);

        Transform chair = Solid("Cadeira do escritorio", p, -2.925f, -7.4f, -2.325f, -6.8f, 0, 0.95f);
        Cyl("Base", chair, new Vector3(-2.625f, 0.03f, -7.1f), 0.58f, 0.06f, furnitureMat);
        Cyl("Coluna", chair, new Vector3(-2.625f, 0.25f, -7.1f), 0.06f, 0.38f, furnitureMat);
        Piece("Assento", chair, -2.865f, 0.43f, -7.35f, -2.385f, 0.49f, -6.89f, furnitureMat);
        Piece("Encosto", chair, -2.855f, 0.52f, -6.89f, -2.395f, 0.95f, -6.84f, furnitureMat);
    }

    // The prototype's placeholder clues (register book, displaced instrument, autopsy report) were removed in stage 2:
    // the case's evidence is now the documents of each room (Assets/Resources/Historia/evidencias.json).
    private static void Clues(Transform p)
    {
        foreach (string old in new[] { "Pista - registro de entrada", "Pista - objeto pessoal", "Pista - prontuario",
                     "Pista - instrumento deslocado", "Pista - laudo da autopsia" })
        {
            Transform t = p.Find(old);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    private static void Markers(Transform p)
    {
        Marker("INICIO - entrada do turno", p, -11.375f, -11.5f, 0.1f);
        Marker("EVENTO - objeto muda de lugar", p, 1.25f, -9.375f, 0.5f);
        Marker("EVENTO - som na camara fria", p, 10.25f, 0.875f, 0.5f);
        Marker("FINAL - identificacao do culpado", p, -3.625f, -1.5f, 0.1f);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedCamaraFriaAssets.Prefab) == null)
            for (int i = 0; i < DrawerZ.Length; i++)
                Marker("EXAMINAR - corpo " + (i + 1), p, (DrawerFaceX + DrawerBackX) / 2, DrawerZ[i] + DrawerWidth / 2, 1.4f);
    }

    private static Transform Group(string name) => Sub(root, name);

    private static Transform Sub(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    // A box over [x0, x1] x [z0, z1] (m), from `bottom` up to `bottom + height`, with a collider.
    private static GameObject Block(string name, Transform parent, float x0, float z0, float x1, float z1,
        float bottom, float height, Material material)
    {
        var center = new Vector3((x0 + x1) * 0.5f, bottom + height * 0.5f, (z0 + z1) * 0.5f);
        var size = new Vector3(Mathf.Abs(x1 - x0), height, Mathf.Abs(z1 - z0));
        return Box(name, parent, center, size, material);
    }

    // A box aligned to a wall line: `along` runs with the wall, `across` is measured from the line.
    private static void LineBox(string name, Transform parent, bool vertical, float line, float along0, float along1,
        float across0, float across1, float y0, float y1, Material material, bool collider)
    {
        var size = vertical
            ? new Vector3(across1 - across0, y1 - y0, along1 - along0)
            : new Vector3(along1 - along0, y1 - y0, across1 - across0);
        float across = line + (across0 + across1) * 0.5f, along = (along0 + along1) * 0.5f;
        var center = vertical ? new Vector3(across, (y0 + y1) * 0.5f, along) : new Vector3(along, (y0 + y1) * 0.5f, across);
        if (collider) Box(name, parent, center, size, material);
        else Part(name, parent, PrimitiveType.Cube, center, size, Quaternion.identity, material);
    }

    // A thin box standing in front of a wall face (skirting, rails).
    private static void FaceStrip(string name, Transform parent, bool vertical, float face, float sign,
        float along0, float along1, float y0, float y1, float depth, Material material)
    {
        float c1 = face + sign * depth;
        LineBox(name, parent, vertical, 0, along0, along1, Mathf.Min(face, c1), Mathf.Max(face, c1), y0, y1, material, false);
    }

    // A finish panel just in front of a wall face; UVs in meters so tiles line up from piece to piece.
    private static void FacePanel(string name, Transform parent, bool vertical, float face, float sign,
        float along0, float along1, float y0, float y1, Material material)
    {
        if (along1 - along0 <= 0.001f || y1 - y0 <= 0.001f) return;
        float plane = face + sign * FinishOffset;
        Vector3 normal = vertical ? new Vector3(sign, 0, 0) : new Vector3(0, 0, sign);
        Vector3 axis = vertical ? Vector3.forward : Vector3.right;
        Vector3 origin = vertical ? new Vector3(plane, y0, along0) : new Vector3(along0, y0, plane);
        Panel(name, parent, origin, axis, Vector3.up, along1 - along0, y1 - y0, normal, material);
    }

    // Visual-only quad. With worldUv the UVs are the vertex positions along the axes (meters),
    // otherwise they run 0..1 across the quad (decals).
    private static GameObject Panel(string name, Transform parent, Vector3 origin, Vector3 axisU, Vector3 axisV,
        float sizeU, float sizeV, Vector3 normal, Material material, bool worldUv = true)
    {
        var v = new[] { origin, origin + axisU * sizeU, origin + axisU * sizeU + axisV * sizeV, origin + axisV * sizeV };
        var uv = worldUv
            ? Array.ConvertAll(v, p => new Vector2(Vector3.Dot(p, axisU), Vector3.Dot(p, axisV)))
            : new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        int[] tris = Vector3.Dot(Vector3.Cross(v[1] - v[0], v[2] - v[0]), normal) > 0
            ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
        float w = Vector3.Dot(Vector3.Cross(normal, axisU), axisV) > 0 ? 1f : -1f;
        var tangent = new Vector4(axisU.x, axisU.y, axisU.z, w);
        var mesh = new Mesh
        {
            name = name, vertices = v, uv = uv, triangles = tris,
            uv2 = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },   // lightmap
            normals = new[] { normal, normal, normal, normal },
            tangents = new[] { tangent, tangent, tangent, tangent },
        };
        mesh.RecalculateBounds();

        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return obj;
    }

    private static void Marker(string name, Transform parent, float x, float z, float height)
    {
        var marker = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(marker, "Create marker");
        marker.transform.SetParent(parent, false);
        marker.transform.position = new Vector3(x, height, z);
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    // Visual-only primitive (no collider) used for trim and the parts of composite furniture.
    private static GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 center, Vector3 size,
        Quaternion rotation, Material material)
    {
        var obj = GameObject.CreatePrimitive(type);
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.SetPositionAndRotation(center, rotation);
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    // Visual-only box given by its min and max corners.
    private static GameObject Piece(string name, Transform parent, float x0, float y0, float z0, float x1, float y1, float z1,
        Material material) =>
        Part(name, parent, PrimitiveType.Cube, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
            new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), Quaternion.identity, material);

    // Visual-only cylinder; by default its axis is vertical.
    private static GameObject Cyl(string name, Transform parent, Vector3 center, float diameter, float height,
        Material material, Vector3? euler = null) =>
        Part(name, parent, PrimitiveType.Cylinder, center, new Vector3(diameter, height * 0.5f, diameter),
            Quaternion.Euler(euler ?? Vector3.zero), material);

    // Root of a composite piece of furniture: one box collider keeps its footprint simple.
    private static Transform Solid(string name, Transform parent, float x0, float z0, float x1, float z1, float y0, float y1)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(parent, false);
        var collider = obj.AddComponent<BoxCollider>();
        collider.center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
        collider.size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
        return obj.transform;
    }

    private static Material GetMaterial(string name, Color color)
    {
        const string folder = BaseFolder;
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "UnclaimedPrototype");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        string pipeline = GraphicsSettings.currentRenderPipeline == null
            ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().Name;
        string shaderName = pipeline.Contains("HDRenderPipeline") ? "HDRP/Lit"
            : pipeline.Contains("Universal") ? "Universal Render Pipeline/Lit" : "Standard";
        var shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
        material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else material.color = color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // URP Lit material, created once: an existing asset is returned untouched, so tweaks made in the Editor survive.
    // Texture tiling is given in meters because finish panels carry UVs in meters.
    // glossMap: metallic (R) and smoothness (A) map; _Smoothness then scales the map's smoothness.
    private static Material Mat(string folder, string name, Color color, float smoothness, float metallic = 0f,
        string baseMap = null, string normalMap = null, float tileMeters = 1f, Color? emission = null, bool transparent = false,
        string glossMap = null)
    {
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        EnsureFolder(folder);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        if (baseMap != null) material.SetTexture("_BaseMap", Tex(baseMap, false));
        if (normalMap != null) material.SetTexture("_BumpMap", Tex(normalMap, true));
        if (glossMap != null) material.SetTexture("_MetallicGlossMap", Tex(glossMap, false));
        material.SetTextureScale("_BaseMap", Vector2.one / tileMeters);
        if (emission.HasValue)
        {
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
        }
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Loads a texture, fixing the import settings the finish depends on (normal maps, clamped decals).
    private static Texture2D Tex(string path, bool normal)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool decal = path.Contains("/Decal_"), changed = false;
            if (path.EndsWith("_MS.png") && importer.sRGBTexture)   // metallic / smoothness maps are linear data
            {
                importer.sRGBTexture = false;
                changed = true;
            }
            if (normal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (decal && (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) Debug.LogWarning("Unclaimed: missing texture " + path);
        return texture;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
