// Place this file in Assets/Editor/UnclaimedMorgueGenerator.cs.
// Run Tools > Unclaimed > Generate Morgue Prototype in an open Unity scene.
// Room order, shape and connections follow the floor plan in Assets/Planta/planta_necroterio.png,
// but the layout is measured in meters so rooms can be sized for first-person play.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class UnclaimedMorgueGenerator
{
    // Any object whose name starts with this prefix counts as an existing morgue,
    // including the older 8-room layout saved in Assets/Scenes/Morgue.unity.
    private const string GroupPrefix = "Unclaimed - Necroterio";
    private const string GroupName = GroupPrefix + " (planta)";

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

    // Camara fria drawers sit on the east wall facing west (2.2 m deep, 0.9 m wide). The free strip in
    // front of each one fits the tray pulled all the way out plus a stretcher lined up after it.
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

    private readonly struct Door
    {
        public readonly string Name;
        public readonly bool Vertical; // true: the opening is in a wall running north-south (x = Line)
        public readonly float Line, Center, Width;
        public readonly bool Header;

        public Door(string name, bool vertical, float line, float center, float width, bool header = true)
        {
            Name = name; Vertical = vertical; Line = line; Center = center; Width = width; Header = header;
        }
    }

    private static readonly Door[] Doors =
    {
        new Door("Entrada principal", false, -12.375f, -11.375f, 2f),
        new Door("Porta WC", false, -7.125f, -11.5f, 1.5f),
        new Door("Porta recepcao - circulacao", false, -6f, -8f, 1.5f),
        new Door("Porta recepcao - arquivo", true, -6.5f, -8f, 1.5f),
        new Door("Porta arquivo - circulacao", true, -2f, -4.75f, 1.5f),
        new Door("Porta descanso", true, -5.25f, -0.25f, 1.5f),
        new Door("Porta deposito", true, -4.5f, 3.5f, 1.5f),
        new Door("Porta sala tecnica", false, 5.75f, -3.5f, 1.5f),
        new Door("Porta autopsia - circulacao", true, -2f, 0f, 1.5f),
        new Door("Porta autopsia - camara fria", true, 7.5f, 0.25f, 1.5f),
        new Door("Porta camara fria - recebimento", false, 5.25f, 9.75f, 1.5f),
        new Door("Porta circulacao - vestibulo", true, 3.25f, 4.5f, 1.25f),
        // Not drawn on the plan: closes the service loop corridor -> recebimento -> camara fria -> autopsia.
        new Door("Porta vestibulo - recebimento", false, 5.25f, 5.375f, 1.5f),
        new Door("Porta dupla recebimento - patio", true, 12.75f, 8.375f, 2f),
        new Door("Portao de servico", false, 5.25f, 18f, 2.5f, header: false),
    };

    private static Transform root;
    private static bool[] doorUsed;
    private static Material floorPublic, floorStaff, floorClinical, corridorMat, patioMat, exteriorMat;
    private static Material wallMat, furnitureMat, bodyMat, clueMat;

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

        List<Zone> zones = Zones();
        doorUsed = new bool[Doors.Length];
        Floors(zones, Group("Pisos"));
        Walls(zones, Rasterize(zones), Group("Paredes"));
        DoorHeaders(Group("Portas"));
        Exterior(Group("Area externa"));
        Furniture(Group("Moveis"));
        Clues(Group("Pistas"));
        Markers(Group("Marcadores"));
        Lights(Group("Luzes"));

        for (int d = 0; d < Doors.Length; d++)
            if (!doorUsed[d])
                Debug.LogWarning($"Unclaimed: door '{Doors[d].Name}' is not on a wall between two areas; check its coordinates.");

        Selection.activeGameObject = container;
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Unclaimed: prototype generated from planta_necroterio.png. Save the scene and add your player controller at INICIO.");
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

    private static void Floors(List<Zone> zones, Transform parent)
    {
        foreach (Zone zone in zones)
            for (int i = 0; i < zone.Rects.Length; i++)
            {
                Rect r = zone.Rects[i];
                string suffix = zone.Rects.Length > 1 ? " " + (i + 1) : "";
                Block("Piso - " + zone.Name + suffix, parent, r.xMin, r.yMin, r.xMax, r.yMax, -0.2f, 0.2f, zone.Floor);
            }
    }

    // Grid of zone ids (0 = exterior) at Cell resolution; walls go wherever neighbouring ids differ.
    private static int[,] Rasterize(List<Zone> zones)
    {
        var grid = new int[G(MaxX - MinX), G(MaxZ - MinZ)];
        for (int z = 0; z < zones.Count; z++)
            foreach (Rect r in zones[z].Rects)
                for (int i = G(r.xMin - MinX); i < G(r.xMax - MinX); i++)
                    for (int j = G(r.yMin - MinZ); j < G(r.yMax - MinZ); j++)
                        grid[i, j] = z + 1;
        return grid;
    }

    private static void Walls(List<Zone> zones, int[,] grid, Transform parent)
    {
        int w = grid.GetLength(0), h = grid.GetLength(1);
        for (int i = 1; i < w; i++)
            WallLine(zones, parent, true, i, h, j => grid[i - 1, j], j => grid[i, j]);
        for (int j = 1; j < h; j++)
            WallLine(zones, parent, false, j, w, i => grid[i, j - 1], i => grid[i, j]);
    }

    // Merges the cell edges along one grid line into wall segments, leaving door spans open.
    private static void WallLine(List<Zone> zones, Transform parent, bool vertical, int line, int count,
        Func<int, int> before, Func<int, int> after)
    {
        var none = new Vector2Int(-1, -1);
        var pairs = new Vector2Int[count];
        var door = new bool[count];
        for (int k = 0; k < count; k++)
        {
            int a = before(k), b = after(k);
            int d = a != b ? DoorAt(vertical, line, k) : -1;
            door[k] = d >= 0;
            if (door[k]) doorUsed[d] = true;
            pairs[k] = a == b || door[k] ? none : new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
        }

        int start = 0;
        for (int k = 1; k <= count; k++)
        {
            if (k < count && pairs[k] == pairs[start]) continue;
            if (pairs[start] != none)
                WallSegment(zones, parent, vertical, line, pairs[start], start, k,
                    start > 0 && !door[start - 1], k < count && !door[k]);
            start = k;
        }
    }

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
    private static void WallSegment(List<Zone> zones, Transform parent, bool vertical, int line, Vector2Int pair,
        int from, int to, bool extendStart, bool extendEnd)
    {
        bool fence = KindOf(zones, pair.x) != Kind.Interior && KindOf(zones, pair.y) != Kind.Interior;
        float thickness = fence ? FenceThickness : WallThickness;
        float height = fence ? FenceHeight : WallHeight;

        float at = (vertical ? MinX : MinZ) + line * Cell;
        float a = (vertical ? MinZ : MinX) + from * Cell, b = (vertical ? MinZ : MinX) + to * Cell;
        Vector3 p0 = vertical ? new Vector3(at, 0, a) : new Vector3(a, 0, at);
        Vector3 p1 = vertical ? new Vector3(at, 0, b) : new Vector3(b, 0, at);
        Vector3 dir = (p1 - p0).normalized;
        if (extendStart) p0 -= dir * thickness * 0.5f;
        if (extendEnd) p1 += dir * thickness * 0.5f;

        float length = Vector3.Distance(p0, p1);
        Vector3 size = vertical ? new Vector3(thickness, height, length) : new Vector3(length, height, thickness);
        string name = fence ? "Muro do patio"
            : pair.x == 0 ? "Parede externa - " + zones[pair.y - 1].Name
            : "Parede " + zones[pair.x - 1].Name + " / " + zones[pair.y - 1].Name;
        Box(name, parent, (p0 + p1) * 0.5f + Vector3.up * height * 0.5f, size, wallMat);
    }

    private static Kind KindOf(List<Zone> zones, int id) => id == 0 ? Kind.Exterior : zones[id - 1].Kind;

    // A short header marks each doorway without filling the walkable opening.
    private static void DoorHeaders(Transform parent)
    {
        const float half = WallThickness / 2;
        foreach (Door d in Doors)
        {
            if (!d.Header) continue;
            float a = d.Center - d.Width / 2, b = d.Center + d.Width / 2;
            if (d.Vertical)
                Block(d.Name + " - verga", parent, d.Line - half, a, d.Line + half, b,
                    DoorHeight, WallHeight - DoorHeight, wallMat);
            else
                Block(d.Name + " - verga", parent, a, d.Line - half, b, d.Line + half,
                    DoorHeight, WallHeight - DoorHeight, wallMat);
        }
    }

    // Ground around the building keeps the main entrance and the service gate usable;
    // invisible colliders at its edges stop the player from walking off the map.
    private static void Exterior(Transform parent)
    {
        Block("Terreno externo", parent, MinX, MinZ, MaxX, MaxZ, -0.22f, 0.2f, exteriorMat);
        Block("Calcada da entrada", parent, -14f, -13.875f, -8.375f, -12.375f, -0.2f, 0.2f, patioMat);
        Limit("Limite norte", parent, MinX, MaxZ, MaxX, MaxZ + 0.25f);
        Limit("Limite sul", parent, MinX, MinZ - 0.25f, MaxX, MinZ);
        Limit("Limite oeste", parent, MinX - 0.25f, MinZ, MinX, MaxZ);
        Limit("Limite leste", parent, MaxX, MinZ, MaxX + 0.25f, MaxZ);
    }

    // Furniture keeps human scale; it is placed per room and never scaled with the rooms.
    private static void Furniture(Transform p)
    {
        // Recepcao: desk on the west wall, chairs in the south-east corner, entrance strip left clear.
        Block("Balcao de recepcao", p, -14.55f, -10.75f, -13.35f, -8.25f, 0, 1.1f, furnitureMat);
        Block("Cadeira de espera 1", p, -8.6f, -12.15f, -8f, -11.55f, 0, 0.9f, furnitureMat);
        Block("Cadeira de espera 2", p, -7.85f, -12.15f, -7.25f, -11.55f, 0, 0.9f, furnitureMat);
        Block("Vaso de planta", p, -14.55f, -7.85f, -13.95f, -7.25f, 0, 0.8f, furnitureMat);

        Block("Pia do WC", p, -12.2f, -4.425f, -11.6f, -3.975f, 0, 0.9f, furnitureMat);
        Block("Vaso sanitario", p, -14.65f, -5.85f, -14.25f, -5.15f, 0, 0.45f, furnitureMat);

        Block("Armarios do vestiario", p, -12.8f, -2.75f, -12.2f, 0f, 0, 2f, furnitureMat);
        Block("Banco do vestiario", p, -10.5f, -2.65f, -8.7f, -2.25f, 0, 0.45f, furnitureMat);
        Block("Bancada de cafe", p, -7.5f, -3.7f, -5.5f, -3.1f, 0, 0.9f, furnitureMat);

        Block("Estante do deposito", p, -7.8f, 3f, -7.3f, 5.5f, 0, 2f, furnitureMat);
        Block("Balde e esfregao", p, -5.875f, 1.5f, -5.375f, 2f, 0, 0.5f, furnitureMat);

        Block("Quadro eletrico", p, -3.125f, 8.5f, -2.125f, 8.8f, 0, 2f, furnitureMat);
        Block("Condensadora", p, 0.75f, 5.95f, 2.25f, 6.95f, 0, 1f, furnitureMat);
        Block("Tubulacao", p, -1f, 8.5f, 3f, 8.8f, 2.4f, 0.3f, furnitureMat);

        // Recebimento: fixtures on the walls, stretcher in the middle with at least 1.2 m all around it.
        Block("Pia de lavagem de corpos", p, 3.45f, 7.3f, 4.35f, 9.5f, 0, 0.9f, furnitureMat);
        Block("Bancada com pia - recebimento", p, 5f, 10.75f, 7f, 11.35f, 0, 0.9f, furnitureMat);
        Block("Maca de recebimento", p, 6.95f, 8f, 9.05f, 8.75f, 0, 0.85f, furnitureMat);
        Block("Corpo provisório - recebimento", p, 7.15f, 8.05f, 8.85f, 8.7f, 0.85f, 0.15f, bodyMat);
        Block("Mesa auxiliar", p, 11.8f, 10.75f, 12.6f, 11.35f, 0, 0.8f, furnitureMat);

        Block("Bancada com pia - autopsia", p, 0.125f, 2.625f, 3.625f, 3.375f, 0, 0.9f, furnitureMat);
        Block("Carrinho de instrumentos", p, 5.5f, 2.05f, 6.3f, 3.25f, 0, 0.9f, furnitureMat);
        Block("Mesa de autopsia", p, 1.55f, -0.9f, 3.85f, 0f, 0, 0.9f, furnitureMat);
        Block("Corpo provisório - autopsia", p, 1.8f, -0.775f, 3.6f, -0.125f, 0.9f, 0.15f, bodyMat);
        Block("Luminaria cirurgica", p, -1f, 1.5f, -0.5f, 2f, 0, 1.8f, furnitureMat);
        Block("Pia da autopsia", p, 5.875f, -3.25f, 6.875f, -2.625f, 0, 0.9f, furnitureMat);
        Block("Banco da autopsia", p, 1.375f, -2.85f, 1.975f, -2.25f, 0, 0.5f, furnitureMat);

        // Camara fria: the stretcher is parked between the drawer banks, out of every drawer's free strip.
        for (int i = 0; i < DrawerZ.Length; i++)
        {
            float z = DrawerZ[i];
            Block("Gaveta mortuaria " + (i + 1), p, DrawerFaceX, z, DrawerBackX, z + DrawerWidth, 0, 1.1f, furnitureMat);
            Block("Corpo provisório " + (i + 1), p, DrawerFaceX + 0.2f, z + 0.125f, DrawerBackX - 0.2f, z + DrawerWidth - 0.125f,
                1.1f, 0.14f, bodyMat);
        }
        Block("Maca da camara fria", p, 12.75f, 0.5f, 14.85f, 1.25f, 0, 0.85f, furnitureMat);

        Block("Mesa do escritorio", p, -3.425f, -8.4f, -1.825f, -7.6f, 0, 0.8f, furnitureMat);
        Block("Cadeira do escritorio", p, -2.925f, -7.4f, -2.325f, -6.8f, 0, 0.9f, furnitureMat);
        Block("Arquivo de prontuarios", p, 2.65f, -6.5f, 3.25f, -4f, 0, 1.6f, furnitureMat);
        Block("Estante de pastas", p, 2.65f, -8.5f, 3.25f, -6.75f, 0, 2f, furnitureMat);
        Block("Armario de pertences", p, 2.65f, -10f, 3.25f, -8.75f, 0, 1.8f, furnitureMat);

        // Patio: equipment against the north and east fences keeps the yard open for vehicles.
        Block("Cacamba", p, 19.1f, 13.45f, 20.85f, 15.35f, 0, 1.4f, furnitureMat);
        Block("Paletes", p, 17.1f, 14.25f, 18.3f, 15.35f, 0, 0.3f, furnitureMat);
        Block("Armarios de servico", p, 20.25f, 10f, 20.85f, 11.2f, 0, 1.2f, furnitureMat);
    }

    private static void Clues(Transform p)
    {
        Clue("Pista - registro de entrada", p, -13.95f, -9.5f, 1.1f);
        Clue("Pista - objeto pessoal", p, 2.95f, -9.375f, 1.8f);
        Clue("Pista - prontuario", p, -2.25f, -7.875f, 0.8f);
        Clue("Pista - instrumento deslocado", p, 6f, 11.05f, 0.9f);
        Clue("Pista - laudo da autopsia", p, 3f, 3f, 0.9f);
    }

    private static void Markers(Transform p)
    {
        Marker("INICIO - entrada do turno", p, -11.375f, -11.5f, 0.1f);
        Marker("EVENTO - objeto muda de lugar", p, 1.25f, -9.375f, 0.5f);
        Marker("EVENTO - som na camara fria", p, 10.25f, 0.875f, 0.5f);
        Marker("FINAL - identificacao do culpado", p, -3.625f, -1.5f, 0.1f);
        for (int i = 0; i < DrawerZ.Length; i++)
            Marker("EXAMINAR - corpo " + (i + 1), p, (DrawerFaceX + DrawerBackX) / 2, DrawerZ[i] + DrawerWidth / 2, 1.4f);
    }

    // Lights are placeholders so the floor plan can be inspected immediately.
    private static void Lights(Transform p)
    {
        var cold = new Color(0.75f, 0.88f, 1f);
        Light("Luz - recepcao", p, -10.6f, -9.75f, 8f, 2.2f);
        Light("Luz - WC", p, -12.55f, -5.5f, 5f, 1.5f);
        Light("Luz - arquivo", p, -1.5f, -8.125f, 7f, 2f);
        Light("Luz - escritorio", p, 0.75f, -4.75f, 5f, 1.5f);
        Light("Luz - descanso", p, -9.125f, -1.5f, 6.5f, 2f);
        Light("Luz - deposito", p, -6.25f, 3.45f, 4.5f, 1.5f);
        Light("Luz - sala tecnica", p, -0.625f, 7.375f, 6.5f, 2f);
        Light("Luz - autopsia", p, 2.75f, 0f, 8f, 2.5f);
        Light("Luz - camara fria", p, 10.25f, -1.6f, 7f, 1.4f, cold);
        Light("Luz - camara fria", p, 10.25f, 3.4f, 7f, 1.4f, cold);
        Light("Luz - recebimento", p, 5.6f, 8.4f, 7f, 2f);
        Light("Luz - recebimento", p, 10.4f, 8.4f, 7f, 2f);
        Light("Luz - vestibulo", p, 5.375f, 4.45f, 4f, 1.5f);
        Light("Luz - patio", p, 15f, 8.5f, 8f, 2f);
        Light("Luz - patio", p, 18.5f, 12.5f, 8f, 2f);
        foreach (Vector2 c in new[] { new Vector2(-7.25f, -4.925f), new Vector2(-3.625f, -1.5f),
                     new Vector2(-3.25f, 2.25f), new Vector2(-0.625f, 4.7f) })
            Light("Luz - circulacao", p, c.x, c.y, 6f, 2.5f);
    }

    private static Transform Group(string name)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(root, false);
        return obj.transform;
    }

    // A box over [x0, x1] x [z0, z1] (m), from `bottom` up to `bottom + height`.
    private static GameObject Block(string name, Transform parent, float x0, float z0, float x1, float z1,
        float bottom, float height, Material material)
    {
        var center = new Vector3((x0 + x1) * 0.5f, bottom + height * 0.5f, (z0 + z1) * 0.5f);
        var size = new Vector3(Mathf.Abs(x1 - x0), height, Mathf.Abs(z1 - z0));
        return Box(name, parent, center, size, material);
    }

    private static void Clue(string name, Transform parent, float x, float z, float surface)
    {
        Box(name, parent, new Vector3(x, surface + 0.075f, z), new Vector3(0.25f, 0.15f, 0.25f), clueMat);
    }

    private static void Marker(string name, Transform parent, float x, float z, float height)
    {
        var marker = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(marker, "Create marker");
        marker.transform.SetParent(parent, false);
        marker.transform.position = new Vector3(x, height, z);
    }

    private static void Limit(string name, Transform parent, float x0, float z0, float x1, float z1)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = new Vector3((x0 + x1) * 0.5f, 1.5f, (z0 + z1) * 0.5f);
        obj.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(x1 - x0), 3f, Mathf.Abs(z1 - z0));
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

    private static void Light(string name, Transform parent, float x, float z, float range, float intensity,
        Color? color = null)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create light");
        obj.transform.SetParent(parent, false);
        obj.transform.position = new Vector3(x, 2.8f, z);
        var light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        if (color.HasValue) light.color = color.Value;
    }

    private static Material GetMaterial(string name, Color color)
    {
        const string folder = "Assets/UnclaimedPrototype";
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
}
