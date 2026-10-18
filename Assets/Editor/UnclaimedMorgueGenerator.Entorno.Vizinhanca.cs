// The street and the neighbourhood: sidewalks, asphalt, poles and wiring, and procedural Brazilian houses with
// thick walls, real openings, eaves, front walls and gates. No interiors: windows show dark glass, a curtain or
// a shallow dark room box. Conventions in UnclaimedMorgueGenerator.Entorno.cs.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class UnclaimedMorgueGenerator
{
    private const float StreetNear = -19f, CurbNear = -21.5f, CurbFar = -28.5f, StreetFar = -31f;
    private const float SidewalkTop = -0.12f, AsphaltTop = -0.27f, HouseWall = 0.15f;
    private static readonly float[] NearPoles = { -31f, -4.5f, 22f, 48f }, FarPoles = { -32f, 9f, 40f };

    private static Material exTerracotta;

    // ------------------------------------------------------------------ openings and walls

    // An opening in a house wall. side: 0 south (-z), 1 east (+x), 2 north (+z), 3 west (-x); center is the world
    // coordinate along the wall (x on south/north walls, z on east/west ones); sill and height in meters from y = 0.
    // kind: 'd' wooden door, 'p' steel door, 's' rolling shutter, 'w' dark glass, 'c' curtain, 'l' lit curtain,
    // 'i' dark room behind clear glass, 'v' clear glass onto a real interior.
    private struct Opening
    {
        public int Side;
        public float Center, Width, Sill, Height;
        public char Kind;
        public bool Grille;

        public Opening(int side, float center, float width, float sill, float height, char kind, bool grille = false)
        {
            Side = side; Center = center; Width = width; Sill = sill; Height = height; Kind = kind; Grille = grille;
        }
    }

    // Point on a wall of the box [x0, x1] x [z0, z1]: s along the wall, d inward from the outer face, y up.
    private static Vector3 WallPoint(int side, float x0, float z0, float x1, float z1, float s, float d, float y)
    {
        switch (side)
        {
            case 0: return new Vector3(s, y, z0 + d);
            case 1: return new Vector3(x1 - d, y, s);
            case 2: return new Vector3(s, y, z1 - d);
            default: return new Vector3(x0 + d, y, s);
        }
    }

    private static Vector3 SideNormal(int side) =>
        side == 0 ? Vector3.back : side == 1 ? Vector3.right : side == 2 ? Vector3.forward : Vector3.left;

    // Four walls of a box with their openings cut out (pieces round each opening), plus frames, glass, sills,
    // grilles, doors and what shows behind the glass (in `details`, which is not lightmapped).
    private static void HouseWalls(ExtMesh walls, ExtMesh details, float x0, float z0, float x1, float z1, float y0, float y1,
        Material plaster, List<Opening> openings)
    {
        const float t = HouseWall;
        for (int side = 0; side < 4; side++)
        {
            bool alongX = side == 0 || side == 2;
            float s0 = alongX ? x0 : z0 + t, s1 = alongX ? x1 : z1 - t;
            var mine = openings.FindAll(o => o.Side == side);
            mine.Sort((a, b) => a.Center.CompareTo(b.Center));
            void piece(float sa, float sb, float ya, float yb)
            {
                if (sb - sa < 0.005f || yb - ya < 0.005f) return;
                walls.Box(WallPoint(side, x0, z0, x1, z1, sa, 0, ya), WallPoint(side, x0, z0, x1, z1, sb, t, yb), plaster);
            }
            float cursor = s0;
            foreach (Opening o in mine)
            {
                float a = o.Center - o.Width / 2, b = o.Center + o.Width / 2;
                piece(cursor, a, y0, y1);
                piece(a, b, y0, Mathf.Max(y0, o.Sill));
                piece(a, b, Mathf.Min(y1, o.Sill + o.Height), y1);
                cursor = b;
                OpeningInsert(details, side, x0, z0, x1, z1, o);
            }
            piece(cursor, s1, y0, y1);
        }
    }

    private static void OpeningInsert(ExtMesh m, int side, float x0, float z0, float x1, float z1, Opening o)
    {
        Vector3 P(float s, float d, float y) => WallPoint(side, x0, z0, x1, z1, s, d, y);
        void box(float sa, float sb, float da, float db, float ya, float yb, Material mat) => m.Box(P(sa, da, ya), P(sb, db, yb), mat);
        Vector3 n = SideNormal(side);
        float a = o.Center - o.Width / 2, b = o.Center + o.Width / 2, y0 = o.Sill, y1 = o.Sill + o.Height;
        bool window = o.Kind != 'd' && o.Kind != 'p' && o.Kind != 's';
        bool flip = side == 0 || side == 3;    // texture right = world right seen from outside
        void face(float d, Material mat, bool stretch)
        {
            Vector3 p0 = P(a, d, y0), p1 = P(b, d, y0), p2 = P(b, d, y1), p3 = P(a, d, y1);
            if (stretch)
            {
                float u0 = flip ? 1 : 0, u1 = 1 - u0;
                m.Quad(p0, p1, p2, p3, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1), n, mat);
            }
            else m.Quad(p0, p1, p2, p3, n, mat);
        }

        if (o.Kind == 'd' || o.Kind == 'p')
        {
            Material leaf = o.Kind == 'd' ? exDoorWood : exSteelSheet;
            box(a, b, 0.05f, 0.09f, y0, y1, leaf);
            box(a + 0.08f, a + 0.11f, -0.0f, 0.05f, y0 + 1.0f, y0 + 1.04f, exGalvanized);   // handle
            return;
        }
        if (o.Kind == 's')
        {
            face(0.04f, exShutter, false);
            box(a - 0.05f, b + 0.05f, -0.28f, 0.0f, y1, y1 + 0.36f, exSteelSheet);           // roller housing
            box(a - 0.06f, a, -0.06f, 0.04f, y0, y1, exSteelSheet);                          // guides
            box(b, b + 0.06f, -0.06f, 0.04f, y0, y1, exSteelSheet);
            return;
        }
        // Window: frame, mullion when wide, glazing and what is behind it, a projecting sill, an optional grille.
        const float fw = 0.04f, fd0 = 0.04f, fd1 = 0.08f;
        box(a, a + fw, fd0, fd1, y0, y1, exWhitePaint);
        box(b - fw, b, fd0, fd1, y0, y1, exWhitePaint);
        box(a + fw, b - fw, fd0, fd1, y0, y0 + fw, exWhitePaint);
        box(a + fw, b - fw, fd0, fd1, y1 - fw, y1, exWhitePaint);
        if (o.Width > 1.1f) box(o.Center - 0.02f, o.Center + 0.02f, fd0, fd1, y0, y1, exWhitePaint);
        switch (o.Kind)
        {
            case 'w': face(0.06f, exDarkGlass, false); break;
            case 'c': face(0.06f, exClearGlass, false); face(0.1f, exCurtainOff, true); break;
            case 'l': face(0.06f, exClearGlass, false); face(0.1f, exCurtainLit, true); break;
            case 'v': face(0.06f, exClearGlass, false); break;
            default:   // 'i': a shallow dark room box behind clear glass
                face(0.06f, exClearGlass, false);
                face(0.45f, exInterior, true);
                m.Quad(P(a, 0.08f, y0), P(a, 0.45f, y0), P(a, 0.45f, y1), P(a, 0.08f, y1), (P(b, 0, 0) - P(a, 0, 0)).normalized, exRoomShadow);
                m.Quad(P(b, 0.08f, y0), P(b, 0.45f, y0), P(b, 0.45f, y1), P(b, 0.08f, y1), (P(a, 0, 0) - P(b, 0, 0)).normalized, exRoomShadow);
                m.Quad(P(a, 0.08f, y0), P(b, 0.08f, y0), P(b, 0.45f, y0), P(a, 0.45f, y0), Vector3.up, exRoomShadow);
                m.Quad(P(a, 0.08f, y1), P(b, 0.08f, y1), P(b, 0.45f, y1), P(a, 0.45f, y1), Vector3.down, exRoomShadow);
                break;
        }
        if (y0 > 0.3f) box(a - 0.05f, b + 0.05f, -0.05f, 0.04f, y0 - 0.04f, y0, exConcrete);
        if (o.Grille)
        {
            int bars = Mathf.Max(2, Mathf.RoundToInt(o.Width / 0.12f));
            for (int i = 1; i < bars; i++)
            {
                float s = Mathf.Lerp(a, b, i / (float)bars);
                m.Beam(P(s, -0.05f, y0), P(s, -0.05f, y1), 0.016f, 0.016f, exRailing, false);
            }
            foreach (float y in new[] { y0 + 0.02f, (y0 + y1) / 2, y1 - 0.02f })
                m.Beam(P(a - 0.02f, -0.05f, y), P(b + 0.02f, -0.05f, y), 0.012f, 0.03f, exRailing);
            box(a - 0.03f, a - 0.01f, -0.06f, 0.0f, y0, y1, exRailing);
            box(b + 0.01f, b + 0.03f, -0.06f, 0.0f, y0, y1, exRailing);
        }
    }

    // ------------------------------------------------------------------ houses

    private enum RoofKind { GableX, GableZ, Hip, Flat }

    private sealed class HouseSpec
    {
        public string Name;
        public float X0, Z0, X1, Z1;
        public int Floors = 1;
        public float FloorHeight = 2.9f;
        public Material Walls, Roof;
        public RoofKind Kind;
        public float Overhang = 0.6f;
        public bool Gutter, WaterTank;
        public List<Opening> Openings = new List<Opening>();
        public List<Rect> Balconies = new List<Rect>();   // x/z rects of slabs on the upper floor
        public float WallTop => Floors * FloorHeight;
    }

    // Lays a row of openings evenly along one side of a floor. Letters: 'd' door, 'p' steel door, 's' rolling
    // shutter (3.5 m), 'w'/'c'/'l'/'i' windows 1.2 x 1.2, 'B' wide window 2.0 x 1.3 with a curtain, 'b' small
    // high window, '-' plain wall.
    private static void Row(HouseSpec h, int side, int floor, string letters, bool grille = false)
    {
        bool alongX = side == 0 || side == 2;
        float s0 = alongX ? h.X0 : h.Z0, s1 = alongX ? h.X1 : h.Z1, y = floor * h.FloorHeight;
        for (int i = 0; i < letters.Length; i++)
        {
            float c = Mathf.Lerp(s0, s1, (i + 0.5f) / letters.Length);
            char k = letters[i];
            switch (k)
            {
                case '-': break;
                case 'd': case 'p': h.Openings.Add(new Opening(side, c, 0.9f, y, 2.1f, k)); break;
                case 's': h.Openings.Add(new Opening(side, c, 3.5f, y, 2.9f, 's')); break;
                case 'B': h.Openings.Add(new Opening(side, c, 2.0f, y + 0.9f, 1.3f, 'c', grille)); break;
                case 'b': h.Openings.Add(new Opening(side, c, 0.6f, y + 1.6f, 0.5f, 'w')); break;
                default: h.Openings.Add(new Opening(side, c, 1.2f, y + 1.0f, 1.2f, k, grille)); break;
            }
        }
    }

    private static void BuildHouse(Transform parent, HouseSpec h)
    {
        var walls = new ExtMesh();
        var details = new ExtMesh();
        float top = h.WallTop;
        HouseWalls(walls, details, h.X0, h.Z0, h.X1, h.Z1, -0.1f, top, h.Walls, h.Openings);
        // Concrete floor band between storeys and a dark plinth.
        for (int f = 1; f < h.Floors; f++)
            walls.Box(h.X0 - 0.03f, f * h.FloorHeight - 0.1f, h.Z0 - 0.03f, h.X1 + 0.03f, f * h.FloorHeight + 0.05f, h.Z1 + 0.03f, h.Walls, 63 - 4 - 8);
        walls.Box(h.X0 - 0.015f, -0.1f, h.Z0 - 0.015f, h.X1 + 0.015f, 0.35f, h.Z1 + 0.015f, exStainedBase(h.Walls), 63 - 4 - 8);
        foreach (Rect r in h.Balconies)
        {
            float y = h.FloorHeight;
            walls.Box(r.xMin, y - 0.12f, r.yMin, r.xMax, y + 0.02f, r.yMax, exConcrete);
            details.Box(r.xMin, y + 0.02f, r.yMin, r.xMax, y + 1.0f, r.yMin + 0.03f, exRailing, 63 - 4);
        }
        BuildRoof(walls, details, h);
        walls.Emit(h.Name, parent, true, 0.15f);
        details.Emit(h.Name + " - esquadrias", parent, false);
    }

    // Plinth colour: the wall paint darkened (one material per wall colour).
    private static Material exStainedBase(Material walls)
    {
        Color c = walls.GetColor("_BaseColor") * 0.62f;
        c.a = 1f;
        return Mat(EntornoMaterials, walls.name + " - barrado", c, 0.08f, 0f, EntornoTextures + "plastered_wall_02_diff.jpg",
            EntornoTextures + "plastered_wall_02_nor.jpg", 2f);
    }

    private static void BuildRoof(ExtMesh m, ExtMesh details, HouseSpec h)
    {
        float top = h.WallTop, o = h.Overhang;
        if (h.Kind == RoofKind.Flat)
        {
            // Slab, parapet with coping, and a water tank on its base.
            m.Box(h.X0 - 0.05f, top, h.Z0 - 0.05f, h.X1 + 0.05f, top + 0.15f, h.Z1 + 0.05f, exConcrete);
            float py = top + 0.15f, ph = py + 0.75f;
            m.Box(h.X0 - 0.05f, py, h.Z0 - 0.05f, h.X1 + 0.05f, ph, h.Z0 + 0.1f, h.Walls);
            m.Box(h.X0 - 0.05f, py, h.Z1 - 0.1f, h.X1 + 0.05f, ph, h.Z1 + 0.05f, h.Walls);
            m.Box(h.X0 - 0.05f, py, h.Z0 + 0.1f, h.X0 + 0.1f, ph, h.Z1 - 0.1f, h.Walls);
            m.Box(h.X1 - 0.1f, py, h.Z0 + 0.1f, h.X1 + 0.05f, ph, h.Z1 - 0.1f, h.Walls);
            m.Box(h.X0 - 0.08f, ph, h.Z0 - 0.08f, h.X1 + 0.08f, ph + 0.03f, h.Z0 + 0.13f, exConcrete);
            m.Box(h.X0 - 0.08f, ph, h.Z1 - 0.13f, h.X1 + 0.08f, ph + 0.03f, h.Z1 + 0.08f, exConcrete);
            m.Box(h.X0 - 0.08f, ph, h.Z0 + 0.13f, h.X0 + 0.13f, ph + 0.03f, h.Z1 - 0.13f, exConcrete);
            m.Box(h.X1 - 0.13f, ph, h.Z0 + 0.13f, h.X1 + 0.08f, ph + 0.03f, h.Z1 - 0.13f, exConcrete);
            if (h.WaterTank) WaterTank(details, new Vector3(h.X1 - 1.4f, py, h.Z1 - 1.4f));
            return;
        }

        // Pitched roofs: tiled faces rising from the eave line, a fascia band all round, a flat soffit under the
        // eaves and closed gable ends.
        float slope = h.Roof == exMetalRoof ? 0.18f : 0.34f, ye = top + 0.18f;
        float rx0 = h.X0 - o, rx1 = h.X1 + o, rz0 = h.Z0 - o, rz1 = h.Z1 + o;
        if (h.Kind == RoofKind.GableX) { rx0 = h.X0 - 0.2f; rx1 = h.X1 + 0.2f; }
        if (h.Kind == RoofKind.GableZ) { rz0 = h.Z0 - 0.2f; rz1 = h.Z1 + 0.2f; }
        Vector3 v(float x, float y, float z) => new Vector3(x, y, z);
        m.Quad(v(rx0, top, rz0), v(rx1, top, rz0), v(rx1, top, rz1), v(rx0, top, rz1), Vector3.down, exWhitePaint);
        m.Quad(v(rx0, top, rz0), v(rx1, top, rz0), v(rx1, ye, rz0), v(rx0, ye, rz0), Vector3.back, exFascia);
        m.Quad(v(rx0, top, rz1), v(rx1, top, rz1), v(rx1, ye, rz1), v(rx0, ye, rz1), Vector3.forward, exFascia);
        m.Quad(v(rx0, top, rz0), v(rx0, top, rz1), v(rx0, ye, rz1), v(rx0, ye, rz0), Vector3.left, exFascia);
        m.Quad(v(rx1, top, rz0), v(rx1, top, rz1), v(rx1, ye, rz1), v(rx1, ye, rz0), Vector3.right, exFascia);
        float zm = (rz0 + rz1) / 2, xm = (rx0 + rx1) / 2;
        switch (h.Kind)
        {
            case RoofKind.GableX:
            {
                float yr = ye + slope * (rz1 - rz0) / 2;
                RoofFace(m, h.Roof, Vector3.right, v(rx0, ye, rz0), v(rx1, ye, rz0), v(rx1, yr, zm), v(rx0, yr, zm));
                RoofFace(m, h.Roof, Vector3.right, v(rx1, ye, rz1), v(rx0, ye, rz1), v(rx0, yr, zm), v(rx1, yr, zm));
                m.Tri(v(rx0, ye, rz0), v(rx0, ye, rz1), v(rx0, yr, zm), Vector3.left, h.Walls);
                m.Tri(v(rx1, ye, rz0), v(rx1, ye, rz1), v(rx1, yr, zm), Vector3.right, h.Walls);
                details.Beam(v(rx0 - 0.05f, yr + 0.06f, zm), v(rx1 + 0.05f, yr + 0.06f, zm), 0.26f, 0.12f, h.Roof);   // ridge cap
                if (h.Gutter) Gutter(details, v(rx0, ye, rz0), v(rx1, ye, rz0), Vector3.back);
                break;
            }
            case RoofKind.GableZ:
            {
                float yr = ye + slope * (rx1 - rx0) / 2;
                RoofFace(m, h.Roof, Vector3.forward, v(rx0, ye, rz1), v(rx0, ye, rz0), v(xm, yr, rz0), v(xm, yr, rz1));
                RoofFace(m, h.Roof, Vector3.forward, v(rx1, ye, rz0), v(rx1, ye, rz1), v(xm, yr, rz1), v(xm, yr, rz0));
                m.Tri(v(rx0, ye, rz0), v(rx1, ye, rz0), v(xm, yr, rz0), Vector3.back, h.Walls);
                m.Tri(v(rx0, ye, rz1), v(rx1, ye, rz1), v(xm, yr, rz1), Vector3.forward, h.Walls);
                details.Beam(v(xm, yr + 0.06f, rz0 - 0.05f), v(xm, yr + 0.06f, rz1 + 0.05f), 0.26f, 0.12f, h.Roof);
                if (h.Gutter) Gutter(details, v(rx0, ye, rz0), v(rx0, ye, rz1), Vector3.left);
                break;
            }
            default:   // hip: ridge along the long side
            {
                float w = rx1 - rx0, d = rz1 - rz0;
                if (w >= d)
                {
                    float yr = ye + slope * d / 2, ra = rx0 + d / 2, rb = rx1 - d / 2;
                    RoofFace(m, h.Roof, Vector3.right, v(rx0, ye, rz0), v(rx1, ye, rz0), v(rb, yr, zm), v(ra, yr, zm));
                    RoofFace(m, h.Roof, Vector3.right, v(rx1, ye, rz1), v(rx0, ye, rz1), v(ra, yr, zm), v(rb, yr, zm));
                    RoofFace(m, h.Roof, Vector3.forward, v(rx0, ye, rz1), v(rx0, ye, rz0), v(ra, yr, zm));
                    RoofFace(m, h.Roof, Vector3.forward, v(rx1, ye, rz0), v(rx1, ye, rz1), v(rb, yr, zm));
                    details.Beam(v(ra, yr + 0.05f, zm), v(rb, yr + 0.05f, zm), 0.24f, 0.1f, h.Roof);
                }
                else
                {
                    float yr = ye + slope * w / 2, ra = rz0 + w / 2, rb = rz1 - w / 2;
                    RoofFace(m, h.Roof, Vector3.forward, v(rx0, ye, rz1), v(rx0, ye, rz0), v(xm, yr, ra), v(xm, yr, rb));
                    RoofFace(m, h.Roof, Vector3.forward, v(rx1, ye, rz0), v(rx1, ye, rz1), v(xm, yr, rb), v(xm, yr, ra));
                    RoofFace(m, h.Roof, Vector3.right, v(rx0, ye, rz0), v(rx1, ye, rz0), v(xm, yr, ra));
                    RoofFace(m, h.Roof, Vector3.right, v(rx1, ye, rz1), v(rx0, ye, rz1), v(xm, yr, rb));
                    details.Beam(v(xm, yr + 0.05f, ra), v(xm, yr + 0.05f, rb), 0.24f, 0.1f, h.Roof);
                }
                if (h.Gutter) Gutter(details, v(rx0, ye, rz0), v(rx1, ye, rz0), Vector3.back);
                break;
            }
        }
        if (h.WaterTank) WaterTank(details, new Vector3(xm + 0.8f, ye + 0.6f, zm + 0.4f));
    }

    // Tiled roof face (3 or 4 corners, starting with the eave edge); tile rows run along `eave`.
    private static void RoofFace(ExtMesh m, Material mat, Vector3 eave, params Vector3[] p)
    {
        Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
        if (n.y < 0) n = -n;
        Vector3 up = Vector3.Cross(n, eave).normalized;
        if (Vector3.Dot(up, Vector3.up) < 0) up = -up;
        Vector2 uv(Vector3 q) => new Vector2(Vector3.Dot(q, eave), Vector3.Dot(q, up));
        if (p.Length == 4) m.Quad(p[0], p[1], p[2], p[3], uv(p[0]), uv(p[1]), uv(p[2]), uv(p[3]), n, mat);
        else m.Tri(p[0], p[1], p[2], uv(p[0]), uv(p[1]), uv(p[2]), n, mat);
    }

    private static void Gutter(ExtMesh m, Vector3 a, Vector3 b, Vector3 outward)
    {
        Vector3 off = outward * 0.07f + Vector3.down * 0.06f;
        m.Beam(a + off, b + off, 0.1f, 0.09f, exGalvanized);
        Vector3 foot = new Vector3(b.x, -0.05f, b.z) + outward * 0.07f - (b - a).normalized * 0.3f;
        m.Cylinder(b + off - (b - a).normalized * 0.3f, foot + Vector3.up * 0.25f, 0.045f, 0.045f, 8, exGalvanized);
    }

    private static void WaterTank(ExtMesh m, Vector3 at)
    {
        m.Box(at - new Vector3(0.6f, 0, 0.6f), at + new Vector3(0.6f, 0.12f, 0.6f), exConcrete);
        m.Cylinder(at + Vector3.up * 0.12f, at + Vector3.up * 0.95f, 0.55f, 0.6f, 16, exWater);
        m.Cylinder(at + Vector3.up * 0.95f, at + Vector3.up * 1.05f, 0.62f, 0.5f, 16, exWater);
    }

    // Street wall of a lot along x at z = line (front faces `street`, +-z), with gates (x0, x1, kind):
    // 'g' railing pair, 'r' sliding bars, 'b' tilting garage sheet, 'c' blind sheet.
    private static void FrontWall(ExtMesh walls, ExtMesh metal, float line, float x0, float x1, float height, Material mat,
        params (float a, float b, char kind)[] gates)
    {
        var cuts = new List<(float a, float b, char kind)>(gates);
        cuts.Sort((p, q) => p.a.CompareTo(q.a));
        float cursor = x0;
        void run(float a, float b)
        {
            if (b - a < 0.01f) return;
            walls.Box(a, -0.15f, line - 0.075f, b, height - 0.03f, line + 0.075f, mat, 63 - 8);
            walls.Box(a, height - 0.03f, line - 0.1f, b, height, line + 0.1f, exConcrete);
        }
        foreach (var g in cuts)
        {
            run(cursor, g.a - 0.3f);
            MasonryPost(walls, g.a - 0.3f, line, height + 0.1f);
            MasonryPost(walls, g.b, line, height + 0.1f);
            Vector3 a = new Vector3(g.a, 0, line), b = new Vector3(g.b, 0, line);
            float gh = Mathf.Min(height, 2.1f);
            switch (g.kind)
            {
                case 'g':
                    GateRailing(metal, a, (a + b) / 2 - Vector3.right * 0.02f, gh);
                    GateRailing(metal, (a + b) / 2 + Vector3.right * 0.02f, b, gh);
                    break;
                case 'r':
                    for (float x = g.a + 0.06f; x < g.b; x += 0.1f)
                        metal.Beam(new Vector3(x, 0.05f, line), new Vector3(x, gh, line), 0.02f, 0.02f, exRailing, false);
                    foreach (float y in new[] { 0.08f, gh / 2, gh - 0.03f })
                        metal.Beam(new Vector3(g.a, y, line), new Vector3(g.b, y, line), 0.04f, 0.05f, exRailing);
                    break;
                default:   // 'b', 'c'
                    SheetLeaf(metal, a, b, 0.05f, gh);
                    break;
            }
            cursor = g.b + 0.3f;
        }
        run(cursor, x1);
    }

    // Plain wall between lots, bare block, along x or z.
    private static void LotWall(ExtMesh m, float x0, float z0, float x1, float z1, float height = 2.0f)
    {
        m.Box(x0, -0.15f, z0, x1, height - 0.03f, z1, exBlock, 63 - 8);
        m.Box(x0 - 0.02f, height - 0.03f, z0 - 0.02f, x1 + 0.02f, height, z1 + 0.02f, exConcrete);
    }

    private static HouseSpec House(string name, float x0, float z0, float x1, float z1, RoofKind kind, Material walls, Material roof,
        int floors = 1)
    {
        return new HouseSpec { Name = name, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Kind = kind, Walls = walls, Roof = roof, Floors = floors };
    }

    private static void BuildNeighbourhood(Transform parent)
    {
        exTerracotta = Plaster("terracota", new Color(0.72f, 0.45f, 0.33f));
        if (exCurtainLit.GetTexture("_EmissionMap") == null)
        {
            exCurtainLit.SetTexture("_EmissionMap", exCurtainLit.GetTexture("_BaseMap"));
            UnityEditor.EditorUtility.SetDirty(exCurtainLit);
        }
        Material grey = Plaster("cinza claro", new Color(0.78f, 0.78f, 0.76f)), yellow = Plaster("amarelo desbotado", new Color(0.86f, 0.76f, 0.5f));
        Material aqua = Plaster("verde-agua", new Color(0.6f, 0.78f, 0.72f)), dirty = Plaster("branco sujo", new Color(0.8f, 0.78f, 0.72f));
        Material pink = Plaster("rosa claro", new Color(0.88f, 0.7f, 0.68f)), blue = Plaster("azul claro", new Color(0.62f, 0.72f, 0.82f));
        Material beige = Plaster("bege", new Color(0.84f, 0.76f, 0.62f)), cream = Plaster("creme", new Color(0.9f, 0.86f, 0.74f));
        Material ochre = Plaster("ocre", new Color(0.8f, 0.62f, 0.38f)), lilac = Plaster("lilas desbotado", new Color(0.74f, 0.68f, 0.78f));

        var houses = new List<HouseSpec>();
        // Across the street (fronts face north).
        HouseSpec h = House("Casa 1", -30f, -42f, -20f, -34f, RoofKind.Flat, grey, null, 2);
        Row(h, 2, 0, "-dww", true); Row(h, 2, 1, "w-l"); Row(h, 1, 0, "b-w"); Row(h, 3, 0, "wb"); Row(h, 3, 1, "w-"); Row(h, 0, 0, "wb"); Row(h, 0, 1, "ww");
        h.Balconies.Add(Rect.MinMaxRect(-29.2f, -34f, -26.0f, -33.2f)); h.WaterTank = true;
        houses.Add(h);
        h = House("Casa 2", -18f, -41f, -9f, -35f, RoofKind.Hip, yellow, exRoofTiles);
        Row(h, 2, 0, "Bdw", true); Row(h, 3, 0, "w-b"); Row(h, 1, 0, "bw"); Row(h, 0, 0, "w-w"); h.Gutter = true;
        houses.Add(h);
        h = House("Casa 3", -6f, -42f, 1f, -35f, RoofKind.GableX, aqua, exRoofTiles);
        Row(h, 2, 0, "wdw", true); Row(h, 3, 0, "-w"); Row(h, 1, 0, "bw"); Row(h, 0, 0, "ww");
        houses.Add(h);
        h = House("Casa 4", 5f, -43f, 12f, -37f, RoofKind.GableZ, dirty, exRoofTiles);
        Row(h, 2, 0, "wd", true); Row(h, 1, 0, "-b"); Row(h, 3, 0, "w-"); Row(h, 0, 0, "w");
        houses.Add(h);
        h = House("Casa 5 - mercearia", 14f, -40f, 26f, -32f, RoofKind.Flat, exTerracotta, null, 2);
        Row(h, 2, 0, "-s-p"); Row(h, 2, 1, "wlw"); Row(h, 3, 1, "-w"); Row(h, 1, 1, "w-"); Row(h, 0, 0, "bb"); Row(h, 0, 1, "ww");
        h.WaterTank = true;
        houses.Add(h);
        h = House("Casa 14", -43f, -42f, -36f, -34f, RoofKind.Hip, cream, exRoofTiles);
        Row(h, 2, 0, "wd", true); Row(h, 1, 0, "w-");
        houses.Add(h);
        h = House("Casa 15", 30f, -41f, 38f, -34f, RoofKind.GableX, lilac, exRoofTiles);
        Row(h, 2, 0, "wdw", true); Row(h, 3, 0, "w-");
        houses.Add(h);
        h = House("Casa 16 - deposito", 42f, -42f, 54f, -33f, RoofKind.GableX, dirty, exMetalRoof);
        Row(h, 2, 0, "-s-p"); Row(h, 3, 0, "b-b"); h.Overhang = 0.4f;
        houses.Add(h);
        // This side of the street (fronts face south), the immediate neighbours and the houses behind.
        h = House("Casa 6", -29f, -15f, -20f, -3f, RoofKind.Hip, pink, exRoofTiles);
        Row(h, 0, 0, "wdw", true); Row(h, 1, 0, "bw-w", true); Row(h, 3, 0, "w-w"); Row(h, 2, 0, "wb"); h.WaterTank = true;
        houses.Add(h);
        h = House("Casa 7", 24f, -12f, 32f, 0f, RoofKind.GableZ, blue, exRoofTiles, 2);
        Row(h, 0, 0, "dw", true); Row(h, 0, 1, "ww"); Row(h, 3, 0, "w-w"); Row(h, 3, 1, "-lw"); Row(h, 1, 0, "w-b"); Row(h, 1, 1, "ww");
        Row(h, 2, 0, "w"); Row(h, 2, 1, "w"); h.Gutter = true;
        houses.Add(h);
        h = House("Casa 8", -10f, 22f, 0f, 30f, RoofKind.GableX, beige, exRoofTiles);
        Row(h, 0, 0, "bw-w"); Row(h, 2, 0, "wdw"); Row(h, 1, 0, "w"); Row(h, 3, 0, "w");
        houses.Add(h);
        h = House("Casa 9", 6f, 21f, 16f, 29f, RoofKind.Flat, grey, null, 2);
        Row(h, 0, 0, "wb-"); Row(h, 0, 1, "l-w"); Row(h, 2, 0, "wdw"); Row(h, 2, 1, "ww"); Row(h, 3, 1, "w"); h.WaterTank = true;
        houses.Add(h);
        h = House("Casa 10", -31f, 7f, -22f, 16f, RoofKind.Hip, ochre, exMetalRoof);
        Row(h, 1, 0, "w-b"); Row(h, 0, 0, "dw"); Row(h, 2, 0, "w");
        houses.Add(h);
        h = House("Casa 11", -43f, -14f, -34f, -4f, RoofKind.GableX, beige, exRoofTiles, 2);
        Row(h, 0, 0, "wdw", true); Row(h, 0, 1, "w-w"); Row(h, 1, 0, "b-w"); Row(h, 1, 1, "w");
        houses.Add(h);
        h = House("Casa 12", 36f, -13f, 44f, -3f, RoofKind.Hip, cream, exRoofTiles);
        Row(h, 0, 0, "wdw", true); Row(h, 3, 0, "w-b");
        houses.Add(h);
        h = House("Casa 13", 48f, -14f, 56f, -2f, RoofKind.GableZ, yellow, exRoofTiles);
        Row(h, 0, 0, "dw", true); Row(h, 3, 0, "-w-");
        houses.Add(h);
        // The street's ends: a corner house past the cross street, and a house behind a high wall at the dead end.
        h = House("Casa de esquina", -64f, -33f, -56f, -18f, RoofKind.Flat, aqua, null, 2);
        Row(h, 1, 0, "w-dw", true); Row(h, 1, 1, "wlw"); Row(h, 2, 0, "w"); Row(h, 0, 0, "w"); h.WaterTank = true;
        houses.Add(h);
        h = House("Casa do fim da rua", 61.5f, -30f, 70f, -20f, RoofKind.GableZ, pink, exRoofTiles, 2);
        Row(h, 3, 1, "w-c"); Row(h, 3, 0, "---");
        houses.Add(h);
        neighbourRoofs.Clear();
        foreach (HouseSpec spec in houses)
        {
            BuildHouse(parent, spec);
            float o = spec.Kind == RoofKind.Flat ? 0.1f : spec.Overhang;
            neighbourRoofs.Add(Rect.MinMaxRect(spec.X0 - o - 0.2f, spec.Z0 - o - 0.2f, spec.X1 + o + 0.2f, spec.Z1 + o + 0.2f));
        }

        var walls = new ExtMesh();
        var metal = new ExtMesh();
        var extras = new ExtMesh();
        // Street walls and gates of the lots across the street (line z = -31), then side walls between lots.
        FrontWall(walls, metal, StreetFar - 0.1f, -45f, -34f, 1.9f, cream, (-41.5f, -40.5f, 'g'));
        FrontWall(walls, metal, StreetFar - 0.1f, -34f, -19f, 2.3f, grey, (-28f, -25f, 'b'), (-22.5f, -21.5f, 'g'));
        FrontWall(walls, metal, StreetFar - 0.1f, -19f, -7.5f, 1.8f, yellow, (-14f, -13f, 'g'), (-9.3f, -7.8f, 'r'));
        FrontWall(walls, metal, StreetFar - 0.1f, -7.5f, 3.5f, 1.2f, aqua, (-3f, -2f, 'g'), (-6.9f, -4.5f, 'g'));
        Railing(metal, new Vector3(-7.2f, 0, StreetFar - 0.1f), new Vector3(-3.3f, 0, StreetFar - 0.1f), 1.2f, 1.9f, 0.12f, exRailing, 2f, 1.85f);
        Railing(metal, new Vector3(-1.7f, 0, StreetFar - 0.1f), new Vector3(3.5f, 0, StreetFar - 0.1f), 1.2f, 1.9f, 0.12f, exRailing, 2f, 1.85f);
        FrontWall(walls, metal, StreetFar - 0.1f, 3.5f, 13f, 2.0f, exBlock, (7f, 8f, 'c'), (9.5f, 12f, 'c'));
        walls.Box(3.5f, -0.15f, StreetFar - 0.18f, 6.7f, 1.97f, StreetFar - 0.175f, dirty, 16);                // plastered only in part
        FrontWall(walls, metal, StreetFar - 0.1f, 28f, 40f, 1.9f, lilac, (33.5f, 34.5f, 'g'), (35.5f, 38.5f, 'b'));
        FrontWall(walls, metal, StreetFar - 0.1f, 40f, 58f, 2.4f, dirty, (46f, 50f, 'c'));
        foreach (float x in new[] { -45f, -34f, -19f, -7.5f, 3.5f, 13f, 28f, 40f, 58f })
            LotWall(walls, x - 0.075f, -47f, x + 0.075f, StreetFar - 0.175f);
        LotWall(walls, -45f, -47.15f, 58f, -47f, 2.2f);
        // This side: the neighbours' street walls (line z = -19), side walls, and the back lots.
        FrontWall(walls, metal, StreetNear + 0.1f, -45f, -31f, 2.0f, beige, (-40.5f, -39.5f, 'g'), (-37f, -34f, 'b'));
        FrontWall(walls, metal, StreetNear + 0.1f, -31f, LotW, 2.0f, pink, (-27f, -26f, 'g'), (-24.5f, -21.5f, 'r'));
        FrontWall(walls, metal, StreetNear + 0.1f, LotE, 34f, 2.0f, blue, (27.5f, 28.5f, 'g'), (30f, 33f, 'b'));
        FrontWall(walls, metal, StreetNear + 0.1f, 34f, 46f, 1.9f, cream, (39.5f, 40.5f, 'g'));
        FrontWall(walls, metal, StreetNear + 0.1f, 46f, 58f, 2.6f, yellow, (50f, 51f, 'c'), (53f, 56f, 'c'));
        foreach (float x in new[] { -45f, -31f, 34f, 46f, 58f })
            LotWall(walls, x - 0.075f, StreetNear + 0.175f, x + 0.075f, 18f);
        LotWall(walls, -31f, 4.925f, LotW, 5.075f);
        LotWall(walls, -45f, 17.925f, LotW, 18.075f);
        LotWall(walls, LotE, 17.925f, 58f, 18.075f);
        LotWall(walls, -18.5f, 33.925f, 21f, 34.075f);
        LotWall(walls, 1.925f, LotN, 2.075f, 33.925f);
        LotWall(walls, -18.575f, LotN, -18.425f, 33.925f);
        LotWall(walls, 20.925f, LotN, 21.075f, 33.925f);
        // Dead end: kerb, a short sidewalk and a high wall in front of the last house.
        walls.Box(60f, -0.2f, StreetFar, 60.2f, 3.0f, StreetNear, dirty);
        walls.Box(59.95f, 3.0f, StreetFar, 60.25f, 3.04f, StreetNear, exConcrete);

        // Porch of Casa 3: tiled lean-to on two columns, with its lamp lit.
        float px0 = -5f, px1 = 0f, pz0 = -35f, pz1 = -33.4f, py0 = 2.75f, py1 = 2.35f;
        extras.Box(px0, -0.05f, pz0, px1, 0.12f, pz1, exScreed, 63 - 4);
        foreach (float x in new[] { px0 + 0.1f, px1 - 0.1f })
            extras.Box(x - 0.08f, 0.12f, pz1 - 0.26f, x + 0.08f, py1, pz1 - 0.1f, aqua);
        extras.Box(px0, py1 - 0.15f, pz1 - 0.28f, px1, py1, pz1 - 0.08f, aqua);
        Vector3 q0 = new Vector3(px0 - 0.1f, py0 + 0.1f, pz0 + 0.6f), q1 = new Vector3(px1 + 0.1f, py0 + 0.1f, pz0 + 0.6f);
        Vector3 q2 = new Vector3(px1 + 0.1f, py1 + 0.05f, pz1 + 0.15f), q3 = new Vector3(px0 - 0.1f, py1 + 0.05f, pz1 + 0.15f);
        RoofFace(extras, exRoofTiles, Vector3.right, q3, q2, q1, q0);
        extras.Quad(q0 + Vector3.down * 0.08f, q1 + Vector3.down * 0.08f, q2 + Vector3.down * 0.08f, q3 + Vector3.down * 0.08f, Vector3.down, exWhitePaint);
        extras.Quad(q3 + Vector3.down * 0.08f, q2 + Vector3.down * 0.08f, q2, q3, Vector3.forward, exFascia);
        extras.Quad(q0 + Vector3.down * 0.08f, q3 + Vector3.down * 0.08f, q3, q0, Vector3.left, exFascia);
        extras.Quad(q1 + Vector3.down * 0.08f, q2 + Vector3.down * 0.08f, q2, q1, Vector3.right, exFascia);
        var bulb = new ExtMesh();
        Vector3 lamp = new Vector3(-2.5f, 2.5f, pz0 + 0.12f);
        extras.Box(lamp + new Vector3(-0.06f, 0.0f, -0.12f), lamp + new Vector3(0.06f, 0.12f, 0.0f), exWhitePaint);
        bulb.Cylinder(lamp + new Vector3(0, 0.0f, 0.06f), lamp + new Vector3(0, -0.11f, 0.06f), 0.03f, 0.035f, 10, exWarmBulb);
        bulb.Emit("Casa 3 - lampada do alpendre", parent, false, 0.2f, ShadowCastingMode.Off);
        ExteriorPoint("alpendre - casa 3", lamp + new Vector3(0, -0.15f, 0.12f), 6f, 1.2f, 2700f);
        // Casa 3's garden pots.
        foreach (Vector3 pot in new[] { new Vector3(-5.6f, 0f, -32.2f), new Vector3(-4.9f, 0f, -32.0f), new Vector3(0.6f, 0f, -32.4f) })
            extras.Cylinder(pot, pot + Vector3.up * 0.4f, 0.17f, 0.24f, 10, exTerracottaPot());
        // Casa 2's side carport roof (corrugated metal on two posts).
        foreach (float z in new[] { -40f, -34.2f })
            extras.Cylinder(new Vector3(-7.8f, 0f, z), new Vector3(-7.8f, 2.35f, z), 0.05f, 0.05f, 8, exRailing);
        Vector3 c0 = new Vector3(-9f, 2.75f, -40.3f), c1 = new Vector3(-7.6f, 2.4f, -40.3f), c2 = new Vector3(-7.6f, 2.4f, -33.9f), c3 = new Vector3(-9f, 2.75f, -33.9f);
        RoofFace(extras, exMetalRoof, Vector3.forward, c1, c2, c3, c0);
        extras.Quad(c0 + Vector3.down * 0.01f, c1 + Vector3.down * 0.01f, c2 + Vector3.down * 0.01f, c3 + Vector3.down * 0.01f, Vector3.down, exGalvanized);
        // The grocery's faded sign band above the shutter.
        extras.Box(16.5f, 3.15f, -32f, 23.5f, 3.75f, -31.96f, Plaster("letreiro desbotado", new Color(0.62f, 0.3f, 0.22f)), 32);

        walls.Emit("Muros da vizinhanca", parent, true, 0.12f);
        metal.Emit("Portoes e grades da vizinhanca", parent, false);
        extras.Emit("Alpendres, coberturas e vasos", parent, false);
        BuildNeighbourGround(parent);
    }

    private static Material exTerracottaPot() => Mat(EntornoMaterials, "Vaso de barro", new Color(0.55f, 0.3f, 0.2f), 0.15f);

    // Ground of the neighbouring lots (at y = 0, cement in front, grass or soil behind) and a far ground sheet.
    private static void BuildNeighbourGround(Transform parent)
    {
        var g = new ExtMesh();
        // Across the street.
        g.Ground(-45f, -47f, 58f, -37f, 0f, 0.02f, exSparseGrass, 1.5f);
        g.Ground(-45f, -37f, 58f, StreetFar - 0.175f, 0f, 0.01f, exScreed, 1.5f);
        // This side: the neighbours' lots west and east of the morgue, and the back lots.
        g.Ground(-45f, StreetNear + 0.175f, LotW, -9f, 0f, 0.01f, exScreed, 1.5f);
        g.Ground(-45f, -9f, LotW, 34f, 0f, 0.025f, exSparseGrass, 1.5f);
        g.Ground(LotE, StreetNear + 0.175f, 58f, -10f, 0f, 0.01f, exScreed, 1.5f);
        g.Ground(LotE, -10f, 58f, 34f, 0f, 0.025f, exSparseGrass, 1.5f);
        g.Ground(LotW, LotN, LotE, 34f, 0f, 0.025f, exSoil, 1.5f);
        // Past the street's ends: the corner lot beyond the cross street, the lot behind the dead-end wall.
        g.Ground(-70f, -45f, -55f, 20f, 0f, 0.02f, exScreed, 1.5f);
        g.Ground(60.2f, -45f, 76f, 20f, 0f, 0.025f, exSparseGrass, 1.5f);
        g.Emit("Terreno - lotes vizinhos", parent, true, 0.08f, ShadowCastingMode.Off);
        var far = new ExtMesh();
        far.Quad(new Vector3(-120f, -0.4f, -120f), new Vector3(120f, -0.4f, -120f), new Vector3(120f, -0.4f, 120f), new Vector3(-120f, -0.4f, 120f),
            new Vector2(-30, -30), new Vector2(30, -30), new Vector2(30, 30), new Vector2(-30, 30), Vector3.up, exSoil);
        far.Emit("Terreno - fundo distante", parent, false, 0.2f, ShadowCastingMode.Off);
    }

    // ------------------------------------------------------------------ the street

    private static Vector3 StreetPoleTop(float x, bool near) => new Vector3(x, 9f, near ? CurbNear + 0.4f : CurbFar - 0.4f);

    private static void BuildStreet(Transform street)
    {
        var m = new ExtMesh();
        const float x0 = -45f, x1 = 58f;
        // Sidewalk on the morgue's side: pavers, a grass strip by the kerb, a lowered kerb at the service gate.
        m.Box(x0, -0.35f, -20.8f, x1, SidewalkTop, StreetNear, exPavers);
        m.Box(-12.4f, -0.35f, CurbNear + 0.15f, -10.4f, SidewalkTop, -20.8f, exPavers);
        m.Box(13.6f, -0.35f, CurbNear + 0.15f, 19.4f, SidewalkTop - 0.005f, -20.8f, exDriveway);
        m.Box(x0, AsphaltTop - 0.15f, CurbNear, x1, SidewalkTop, CurbNear + 0.15f, exConcrete);
        m.Box(13.6f, AsphaltTop - 0.15f, CurbNear - 0.002f, 19.4f, AsphaltTop + 0.04f, CurbNear + 0.153f, exConcrete);
        // Gutters, asphalt, kerb and sidewalk on the far side.
        m.Box(x0, AsphaltTop - 0.2f, CurbNear - 0.3f, x1, AsphaltTop + 0.005f, CurbNear, exConcrete);
        m.Box(-52f, AsphaltTop - 0.2f, CurbFar + 0.3f, x1, AsphaltTop, CurbNear - 0.3f, exAsphalt);
        m.Box(x0, AsphaltTop - 0.2f, CurbFar, x1, AsphaltTop + 0.005f, CurbFar + 0.3f, exConcrete);
        m.Box(x0, AsphaltTop - 0.15f, CurbFar - 0.15f, x1, SidewalkTop, CurbFar, exConcrete);
        m.Box(x0, -0.35f, StreetFar - 0.175f, x1, SidewalkTop, CurbFar - 0.15f, exPavers);
        // The cross street at the west end and its corners; the dead end at the east.
        m.Box(-52f, AsphaltTop - 0.2f, -70f, -45f, AsphaltTop, CurbFar + 0.3f, exAsphalt);
        m.Box(-52f, AsphaltTop - 0.2f, CurbNear - 0.3f, -45f, AsphaltTop, 20f, exAsphalt);
        m.Box(-45f, -0.35f, StreetNear, -42.5f, SidewalkTop, 20f, exPavers);
        m.Box(-45f, -0.35f, -70f, -42.5f, SidewalkTop, StreetFar - 0.175f, exPavers);
        m.Box(-55f, -0.35f, -70f, -52f, SidewalkTop, 20f, exPavers);
        m.Box(x1, -0.35f, StreetFar, 60f, SidewalkTop, StreetNear, exPavers);
        m.Emit("Rua e calcadas", street, true, 0.12f, ShadowCastingMode.Off);

        var grass = new ExtMesh();
        grass.Ground(x0, CurbNear + 0.15f, -12.4f, -20.8f, SidewalkTop - 0.02f, 0.01f, exLawn, 0.7f);
        grass.Ground(-10.4f, CurbNear + 0.15f, 13.6f, -20.8f, SidewalkTop - 0.02f, 0.01f, exLawn, 0.7f);
        grass.Ground(19.4f, CurbNear + 0.15f, x1, -20.8f, SidewalkTop - 0.02f, 0.01f, exLawn, 0.7f);
        // Tree pits on the far sidewalk.
        foreach (float x in new[] { -15f, 4f, 28.5f })
        {
            grass.Box(x - 0.5f, SidewalkTop - 0.05f, -30.4f, x + 0.5f, SidewalkTop + 0.002f, -29.4f, exSoil, 8);
            grass.Box(x - 0.55f, SidewalkTop - 0.05f, -30.45f, x + 0.55f, SidewalkTop + 0.02f, -30.4f, exConcrete);
            grass.Box(x - 0.55f, SidewalkTop - 0.05f, -29.4f, x + 0.55f, SidewalkTop + 0.02f, -29.35f, exConcrete);
            grass.Box(x - 0.55f, SidewalkTop - 0.05f, -30.4f, x - 0.5f, SidewalkTop + 0.02f, -29.4f, exConcrete);
            grass.Box(x + 0.5f, SidewalkTop - 0.05f, -30.4f, x + 0.55f, SidewalkTop + 0.02f, -29.4f, exConcrete);
        }
        grass.Emit("Faixa de grama e covas", street, false, 0.2f, ShadowCastingMode.Off);

        // Storm drains in the kerb and a manhole cover.
        var drains = new ExtMesh();
        foreach (var (x, near) in new[] { (-9f, true), (16f, false) })
        {
            float kerb = near ? CurbNear : CurbFar, dir = near ? 1f : -1f;
            drains.Box(x - 0.5f, AsphaltTop + 0.006f, kerb - dir * 0.28f, x + 0.5f, AsphaltTop + 0.012f, kerb, exGrate);
            drains.Box(x - 0.45f, AsphaltTop + 0.012f, near ? kerb - 0.002f : kerb - 0.005f, x + 0.45f, AsphaltTop + 0.1f, near ? kerb + 0.005f : kerb + 0.002f, exRoomShadow);
        }
        drains.Cylinder(new Vector3(2f, AsphaltTop - 0.01f, -25f), new Vector3(2f, AsphaltTop + 0.008f, -25f), 0.32f, 0.32f, 20, exGrate);
        drains.Emit("Bocas de lobo e tampa", street, false, 0.2f, ShadowCastingMode.Off);

        // Concrete poles with arms and lamps, cross-arms, wires between poles on each side, a transformer.
        var poles = new ExtMesh();
        var lenses = new ExtMesh();
        foreach (float x in NearPoles) StreetPole(poles, lenses, street, x, true);
        foreach (float x in FarPoles) StreetPole(poles, lenses, street, x, false);
        // Wires cast no shadow: a few millimetres thick, they only broke into dashes in the shadow map.
        var wires = new ExtMesh();
        WireSpan(wires, NearPoles, true);
        WireSpan(wires, FarPoles, false);
        wires.Emit("Fiacao", street, false, 0.2f, ShadowCastingMode.Off);
        Vector3 tr = StreetPoleTop(22f, true) + new Vector3(0f, -2.6f, 0.38f);
        poles.Cylinder(tr, tr + Vector3.up * 1.0f, 0.32f, 0.32f, 12, exGalvanized);
        poles.Box(tr + new Vector3(-0.08f, 0.2f, -0.3f), tr + new Vector3(0.08f, 0.3f, -0.12f), exGalvanized);
        poles.Box(tr + new Vector3(-0.08f, 0.75f, -0.3f), tr + new Vector3(0.08f, 0.85f, -0.12f), exGalvanized);
        poles.Cylinder(tr + Vector3.up * 1.0f, tr + Vector3.up * 1.25f, 0.05f, 0.03f, 6, exPvc);
        poles.Emit("Postes", street, false);
        lenses.Emit("Luminarias publicas", street, false, 0.2f, ShadowCastingMode.Off);
        Collider("Colisor - poste", street, -4.65f, CurbNear + 0.25f, -4.35f, CurbNear + 0.55f, -0.2f, 3f);
    }

    private static void StreetPole(ExtMesh m, ExtMesh lens, Transform street, float x, bool near)
    {
        Vector3 top = StreetPoleTop(x, near), foot = new Vector3(top.x, -0.3f, top.z);
        float toRoad = near ? -1f : 1f;
        m.Cylinder(foot, top, 0.15f, 0.09f, 10, exPolePaint);
        m.Cylinder(top, top + Vector3.up * 0.05f, 0.09f, 0.0f, 10, exPolePaint);
        // Cross-arm for the primary wires, along z.
        m.Box(top + new Vector3(-0.05f, -0.45f, -0.6f), top + new Vector3(0.05f, -0.35f, 0.6f), exPolePaint);
        foreach (float dz in new[] { -0.5f, 0f, 0.5f })
            m.Cylinder(top + new Vector3(0, -0.35f, dz), top + new Vector3(0, -0.22f, dz), 0.035f, 0.025f, 6, exWhitePaint);
        // Lamp arm out over the road, and the head.
        Vector3 a0 = top + Vector3.down * 1.5f, a1 = a0 + new Vector3(0, 0.35f, toRoad * 1.5f);
        m.Cylinder(a0, a1, 0.03f, 0.03f, 6, exGalvanized);
        m.Cylinder(a0 + Vector3.down * 0.4f, (a0 + a1) / 2, 0.02f, 0.02f, 6, exGalvanized);
        Vector3 head = a1 + new Vector3(0, -0.06f, toRoad * 0.22f);
        m.OrientedBox(head, new Vector3(0.24f, 0.11f, 0.52f), Quaternion.identity, exGalvanized);
        bool sodium = Mathf.Approximately(x, -4.5f), lit = sodium || x == 22f || !near;
        lens.Box(head + new Vector3(-0.1f, -0.065f, -0.2f), head + new Vector3(0.1f, -0.055f, 0.2f),
            lit ? (sodium ? exSodium : exLed) : exDarkGlass, 4);
        if (lit)
            ExteriorSpot((sodium ? "poste de sodio x" : "poste LED x") + x, head + Vector3.down * 0.1f, new Vector3(0, -1f, -toRoad * 0.3f),
                sodium ? 120f : 110f, sodium ? 18f : 15f, sodium ? 8f : 3f, sodium ? 2100f : 4000f, sodium);
    }

    // Five wires per span: three primaries on the cross-arm, a secondary and the telecom cable lower down.
    private static void WireSpan(ExtMesh m, float[] poles, bool near)
    {
        for (int i = 0; i + 1 < poles.Length; i++)
        {
            Vector3 a = StreetPoleTop(poles[i], near), b = StreetPoleTop(poles[i + 1], near);
            float span = b.x - a.x;
            foreach (float dz in new[] { -0.5f, 0f, 0.5f })
                m.Cable(a + new Vector3(0, -0.2f, dz), b + new Vector3(0, -0.2f, dz), 0.008f * span, 0.016f, exCable, 12);
            m.Cable(a + new Vector3(0, -1.6f, 0.12f), b + new Vector3(0, -1.6f, 0.12f), 0.012f * span, 0.018f, exCable, 12);
            m.Cable(a + new Vector3(0, -2.4f, 0.13f), b + new Vector3(0, -2.4f, 0.13f), 0.016f * span, 0.022f, exCable, 12);
            m.Cable(a + new Vector3(0, -2.7f, 0.13f), b + new Vector3(0, -2.7f, 0.13f), 0.018f * span, 0.018f, exCable, 12);
        }
    }
}
