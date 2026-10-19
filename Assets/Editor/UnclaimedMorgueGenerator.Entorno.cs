// Exterior of the morgue: a compact front yard closed by walls, railings and locked gates, the service yard
// behind a railing, the street and a few neighbouring houses. Layout in meters (x = east, z = north, y = up),
// the building's floor at y = 0. The lot and the street are in UnclaimedMorgueGenerator.Entorno.Lote.cs, the
// houses in UnclaimedMorgueGenerator.Entorno.Vizinhanca.cs.
//
// Everything lives in fixed sub-groups of "Area externa" (plus "Luzes/Iluminacao externa/Entorno" and
// "Luzes/Sondas de luz - entorno"). Tools > Unclaimed > Rebuild Exterior replaces only those groups: other children
// of "Area externa" (notably "Ajustes manuais") are never touched, and the interior is not regenerated.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class UnclaimedMorgueGenerator
{
    private const string EntornoFolder = BaseFolder + "/Entorno";
    private const string EntornoTextures = EntornoFolder + "/Texturas/";
    private const string EntornoMaterials = EntornoFolder + "/Materiais";
    private static readonly string[] ExteriorGroups =
        { "Terreno", "Pavimentos", "Drenagem", "Fechamentos", "Fachada", "Patio de manobra", "Rua", "Vizinhanca", "Vegetacao", "Limites" };
    private static readonly string[] LegacyExterior =
        { "Terreno externo", "Calcada da entrada", "Limite norte", "Limite sul", "Limite oeste", "Limite leste" };
    private const string ExteriorLightsName = "Entorno", ExteriorProbesName = "Sondas de luz - entorno", ManualTweaks = "Ajustes manuais";

    private static Transform exteriorLights;
    private static readonly List<Vector3> exteriorProbes = new List<Vector3>();

    // Shared exterior materials (one per finish / colour, reused by every piece).
    private static Material exLawn, exSparseGrass, exSoil, exLeafLitter, exGravel, exScreed, exConcrete, exPavers, exAsphalt,
        exDriveway, exWallPlaster, exBlock, exRoofTiles, exMetalRoof, exShutter, exRailing, exGalvanized, exDarkGlass,
        exPvc, exFascia, exPolePaint, exCable, exBareAluminium, exGrate, exWhitePaint, exCurtainLit, exCurtainOff, exInterior, exGlobe,
        exSodium, exLed, exWarmBulb, exClearGlass, exRoomShadow, exDoorWood, exSteelSheet, exWater, exStain;

    private static void Exterior(Transform outside)
    {
        ExteriorMaterials();
        exteriorProbes.Clear();
        Sub(outside, ManualTweaks);
        exteriorLights = Sub(Sub(lightsGroup, "Iluminacao externa"), ExteriorLightsName);

        BuildLotGround(Sub(outside, "Terreno"), Sub(outside, "Pavimentos"), Sub(outside, "Drenagem"));
        BuildLotBoundaries(Sub(outside, "Fechamentos"), Sub(outside, "Limites"));
        BuildFacadeFittings(Sub(outside, "Fachada"));
        BuildServiceYard(Sub(outside, "Patio de manobra"));
        BuildStreet(Sub(outside, "Rua"));
        BuildNeighbourhood(Sub(outside, "Vizinhanca"));
        BuildVegetation(Sub(outside, "Vegetacao"));
        ExteriorProbeGroup();
    }

    [MenuItem("Tools/Unclaimed/Rebuild Exterior")]
    public static void RebuildExterior()
    {
        GameObject top = null;
        foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == GroupName) top = g;
        if (top == null)
        {
            Debug.LogWarning("Unclaimed: no '" + GroupName + "' in the active scene; generate the morgue first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Unclaimed exterior");

        root = top.transform;
        lightsGroup = Sub(root, "Luzes");
        // Materials of the approved floodlight box (normally set up by the full generator).
        if (whiteMetal == null) whiteMetal = Mat(AutopsiaFolder, "Metal pintado", new Color(0.82f, 0.83f, 0.80f), 0.5f);
        if (tube == null) tube = Mat(AutopsiaFolder, "Difusor aceso", new Color(0.95f, 0.96f, 0.97f), 0.3f, 0f, emission: new Color(1.0f, 0.97f, 0.9f));
        Transform outside = Sub(root, "Area externa");
        foreach (string name in ExteriorGroups) RemoveChild(outside, name);
        foreach (string name in LegacyExterior) RemoveChild(outside, name);
        RemoveChild(Sub(lightsGroup, "Iluminacao externa"), ExteriorLightsName);
        RemoveChild(lightsGroup, ExteriorProbesName);

        Exterior(outside);
        Undo.CollapseUndoOperations(undo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(top.scene);
        Debug.Log("Unclaimed: exterior rebuilt. Re-bake the lighting (Tools > Unclaimed > Bake Prototype Lighting) and save the scene.");
    }

    private static void RemoveChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }

    private static void ExteriorMaterials()
    {
        string f = EntornoMaterials, t = EntornoTextures;
        Material tex(string name, string file, Color color, float smooth, float tile) =>
            Mat(f, name, color, smooth, 0f, t + file + "_diff.jpg", t + file + "_nor.jpg", tile);
        exLawn = tex("Gramado", "leafy_grass", new Color(0.55f, 0.82f, 0.5f), 0.05f, 3f);
        exSparseGrass = tex("Grama rala", "sparse_grass", new Color(0.85f, 1.0f, 0.8f), 0.05f, 3f);
        exSoil = tex("Terra com folhas", "brown_mud_leaves_01", new Color(0.8f, 0.78f, 0.74f), 0.05f, 2.5f);
        exLeafLitter = tex("Folhas no chao", "forest_leaves_02", new Color(0.75f, 0.72f, 0.66f), 0.05f, 2.5f);
        exGravel = tex("Brita", "gravel_floor_03", new Color(0.85f, 0.85f, 0.85f), 0.1f, 1.2f);
        exScreed = tex("Cimentado", "concrete_floor_worn_001", new Color(1.22f, 1.2f, 1.16f), 0.15f, 2f);
        exConcrete = tex("Concreto", "granular_concrete", new Color(0.78f, 0.78f, 0.76f), 0.15f, 1.5f);
        exPavers = tex("Calcada publica", "concrete_pavement_03", new Color(1.15f, 1.12f, 1.08f), 0.15f, 2f);
        exAsphalt = tex("Asfalto", "asphalt_02", new Color(0.62f, 0.62f, 0.64f), 0.25f, 4f);
        exDriveway = tex("Acesso de servico", "grooved_concrete_driveway", new Color(0.62f, 0.66f, 0.7f), 0.15f, 2.5f);
        exWallPlaster = tex("Reboco do muro", "plastered_wall_02", new Color(0.74f, 0.72f, 0.68f), 0.08f, 2f);
        exBlock = tex("Bloco aparente", "concrete_block_wall", new Color(1.2f, 1.18f, 1.15f), 0.08f, 2f);
        exRoofTiles = tex("Telha ceramica", "clay_roof_tiles_02", new Color(0.72f, 0.6f, 0.54f), 0.2f, 1.5f);
        exMetalRoof = tex("Telha metalica", "rusty_corrugated_iron", new Color(0.7f, 0.72f, 0.74f), 0.3f, 2f);
        exShutter = tex("Porta de enrolar", "rusty_metal_shutter", new Color(0.75f, 0.75f, 0.75f), 0.3f, 1f);
        exRailing = Mat(f, "Metal pintado escuro", new Color(0.13f, 0.15f, 0.15f), 0.35f, 0.4f);
        exGalvanized = Mat(f, "Metal galvanizado", new Color(0.55f, 0.56f, 0.56f), 0.45f, 0.7f);
        exSteelSheet = Mat(f, "Chapa pintada", new Color(0.22f, 0.27f, 0.25f), 0.3f, 0.3f);
        exDarkGlass = Mat(f, "Vidro escuro", new Color(0.05f, 0.06f, 0.07f), 0.92f, 0.2f);
        exPvc = Mat(f, "PVC cinza", new Color(0.62f, 0.63f, 0.62f), 0.3f);
        exFascia = Mat(f, "Testeira pintada", new Color(0.42f, 0.3f, 0.22f), 0.2f);
        exPolePaint = Mat(f, "Concreto do poste", new Color(0.58f, 0.58f, 0.56f), 0.1f);
        exCable = Mat(f, "Cabo", new Color(0.04f, 0.04f, 0.04f), 0.3f);
        exBareAluminium = Mat(f, "Aluminio nu", new Color(.42f, .43f, .44f), .45f, .8f);
        exGrate = Mat(f, "Grelha", new Color(0.09f, 0.09f, 0.09f), 0.35f, 0.6f);
        exWhitePaint = Mat(f, "Pintura branca gasta", new Color(0.78f, 0.78f, 0.74f), 0.15f);
        exDoorWood = Mat(f, "Porta de madeira", new Color(0.33f, 0.22f, 0.14f), 0.25f);
        exWater = Mat(f, "Caixa d'agua", new Color(0.2f, 0.33f, 0.52f), 0.4f);
        exStain = Mat(f, "Mancha de umidade", new Color(0.08f, 0.08f, 0.07f, 0.45f), 0.1f, 0f, null, null, 1f, null, true);
        exCurtainLit = Mat(f, "Cortina acesa", new Color(0.9f, 0.8f, 0.62f), 0.1f, 0f, t + "Janela_Cortina_Acesa.png", null, 1f,
            new Color(0.75f, 0.58f, 0.36f));
        exCurtainOff = Mat(f, "Cortina apagada", Color.white, 0.1f, 0f, t + "Janela_Cortina_Apagada.png");
        exInterior = Mat(f, "Interior escuro", Color.white, 0.05f, 0f, t + "Janela_Interior_Escuro.png");
        exGlobe = Mat(f, "Globo leitoso aceso", new Color(0.95f, 0.92f, 0.85f), 0.6f, 0f, null, null, 1f, new Color(0.95f, 0.82f, 0.62f));
        exSodium = Mat(f, "Lampada de sodio", new Color(1f, 0.7f, 0.35f), 0.6f, 0f, null, null, 1f, new Color(1f, 0.55f, 0.18f));
        exLed = Mat(f, "Luminaria LED", new Color(0.95f, 0.95f, 0.92f), 0.6f, 0f, null, null, 1f, new Color(0.85f, 0.86f, 0.84f));
        exClearGlass = Mat(f, "Vidro de janela", new Color(0.1f, 0.12f, 0.13f, 0.28f), 0.95f, 0f, null, null, 1f, null, true);
        exRoomShadow = Mat(f, "Interior sombra", new Color(0.035f, 0.033f, 0.03f), 0.05f);
        exWarmBulb = Mat(f, "Lampada morna", new Color(1f, 0.85f, 0.6f), 0.6f, 0f, null, null, 1f, new Color(1f, 0.72f, 0.4f));
    }

    // A plastered wall material in a given colour (one asset per colour name).
    private static Material Plaster(string colourName, Color c) =>
        Mat(EntornoMaterials, "Reboco " + colourName, c, 0.08f, 0f, EntornoTextures + "painted_plaster_wall_diff.jpg",
            EntornoTextures + "painted_plaster_wall_nor.jpg", 2f);

    // ------------------------------------------------------------------ mesh building

    // Collects quads per material with UVs in meters (textures tile across pieces) and emits one object.
    private sealed class ExtMesh
    {
        private readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<Material> mats = new List<Material>();
        private readonly List<List<int>> tris = new List<List<int>>();
        public bool Empty => v.Count == 0;

        private List<int> Sub(Material m)
        {
            int i = mats.IndexOf(m);
            if (i < 0) { mats.Add(m); tris.Add(new List<int>()); i = mats.Count - 1; }
            return tris[i];
        }

        // Quad a-b-c-d (any winding) facing `normal`, with explicit UVs.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal, Material m)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            for (int k = 0; k < 4; k++) n.Add(normal);
            bool ccw = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0;
            Sub(m).AddRange(ccw ? new[] { i, i + 1, i + 2, i, i + 2, i + 3 } : new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        // Quad with world-projected UVs (meters) on the plane most aligned with its normal.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Material m) =>
            Quad(a, b, c, d, Project(a, normal), Project(b, normal), Project(c, normal), Project(d, normal), normal, m);

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Material m)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(Project(a, normal)); uv.Add(Project(b, normal)); uv.Add(Project(c, normal));
            for (int k = 0; k < 3; k++) n.Add(normal);
            bool ccw = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0;
            Sub(m).AddRange(ccw ? new[] { i, i + 1, i + 2 } : new[] { i, i + 2, i + 1 });
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 normal, Material m)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            for (int k = 0; k < 3; k++) n.Add(normal);
            bool ccw = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0;
            Sub(m).AddRange(ccw ? new[] { i, i + 1, i + 2 } : new[] { i, i + 2, i + 1 });
        }

        public static Vector2 Project(Vector3 p, Vector3 normal)
        {
            Vector3 a = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z);
            return a.x >= a.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        }

        // Axis-aligned box; `faces` skips hidden faces (bit order: -x +x -y +y -z +z).
        public void Box(Vector3 min, Vector3 max, Material m, int faces = 63)
        {
            Vector3 p(float x, float y, float z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            if ((faces & 1) != 0) Quad(p(0, 0, 0), p(0, 0, 1), p(0, 1, 1), p(0, 1, 0), Vector3.left, m);
            if ((faces & 2) != 0) Quad(p(1, 0, 0), p(1, 0, 1), p(1, 1, 1), p(1, 1, 0), Vector3.right, m);
            if ((faces & 4) != 0) Quad(p(0, 0, 0), p(1, 0, 0), p(1, 0, 1), p(0, 0, 1), Vector3.down, m);
            if ((faces & 8) != 0) Quad(p(0, 1, 0), p(1, 1, 0), p(1, 1, 1), p(0, 1, 1), Vector3.up, m);
            if ((faces & 16) != 0) Quad(p(0, 0, 0), p(1, 0, 0), p(1, 1, 0), p(0, 1, 0), Vector3.back, m);
            if ((faces & 32) != 0) Quad(p(0, 0, 1), p(1, 0, 1), p(1, 1, 1), p(0, 1, 1), Vector3.forward, m);
        }

        public void Box(float x0, float y0, float z0, float x1, float y1, float z1, Material m, int faces = 63) =>
            Box(new Vector3(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1)), new Vector3(Mathf.Max(x0, x1), Mathf.Max(y0, y1), Mathf.Max(z0, z1)), m, faces);

        // Box of `size` centred at `c`, turned by `rot`; UVs from its local coordinates (textures follow slopes).
        public void OrientedBox(Vector3 c, Vector3 size, Quaternion rot, Material m, int faces = 63)
        {
            Vector3 h = size / 2;
            Vector3 w(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            void face(Vector3 nl, Vector3 a, Vector3 b, Vector3 cc, Vector3 d)
            {
                Vector2 pr(Vector3 l) => Project(Vector3.Scale(l, h) + new Vector3(c.x + c.z, c.y, c.z - c.x) * 0.37f, nl);
                Quad(w(a.x, a.y, a.z), w(b.x, b.y, b.z), w(cc.x, cc.y, cc.z), w(d.x, d.y, d.z), pr(a), pr(b), pr(cc), pr(d), rot * nl, m);
            }
            if ((faces & 1) != 0) face(Vector3.left, new Vector3(-1, -1, -1), new Vector3(-1, -1, 1), new Vector3(-1, 1, 1), new Vector3(-1, 1, -1));
            if ((faces & 2) != 0) face(Vector3.right, new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, -1));
            if ((faces & 4) != 0) face(Vector3.down, new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1));
            if ((faces & 8) != 0) face(Vector3.up, new Vector3(-1, 1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1));
            if ((faces & 16) != 0) face(Vector3.back, new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1));
            if ((faces & 32) != 0) face(Vector3.forward, new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1));
        }

        // Box from a to b (its long axis) with a square or rectangular section; used for bars, rails, posts, beams.
        public void Beam(Vector3 a, Vector3 b, float width, float depth, Material m, bool caps = true)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            OrientedBox((a + b) / 2, new Vector3(width, depth, dir.magnitude), Quaternion.LookRotation(dir, up), m, caps ? 63 : 15);
        }

        // Cylinder from a to b; `sides` facets, optional caps.
        public void Cylinder(Vector3 a, Vector3 b, float r0, float r1, int sides, Material m, bool caps = true)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(axis, u);
            float len = (b - a).magnitude, perim = 2 * Mathf.PI * Mathf.Max(r0, r1);
            for (int k = 0; k < sides; k++)
            {
                float t0 = 2 * Mathf.PI * k / sides, t1 = 2 * Mathf.PI * (k + 1) / sides;
                Vector3 d0 = u * Mathf.Cos(t0) + w * Mathf.Sin(t0), d1 = u * Mathf.Cos(t1) + w * Mathf.Sin(t1);
                Vector3 nn = (d0 + d1).normalized;
                Quad(a + d0 * r0, a + d1 * r0, b + d1 * r1, b + d0 * r1,
                    new Vector2(perim * k / sides, 0), new Vector2(perim * (k + 1) / sides, 0), new Vector2(perim * (k + 1) / sides, len), new Vector2(perim * k / sides, len), nn, m);
                if (caps)
                {
                    Tri(a, a + d1 * r0, a + d0 * r0, -axis, m);
                    Tri(b, b + d0 * r1, b + d1 * r1, axis, m);
                }
            }
        }

        // Catenary-like sagging cable between two points.
        public void Cable(Vector3 a, Vector3 b, float sag, float radius, Material m, int segments = 10)
        {
            Vector3 prev = a;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4 * t * (1 - t));
                Cylinder(prev, p, radius, radius, 4, m, false);
                prev = p;
            }
        }

        // Shared rings, radial normals and a parallel-transport frame; only the UV seam is duplicated.
        public void Tube(List<Vector3> path, float radius, Material m, int sides = 6)
        {
            if(path.Count<2 || radius<=0)return;
            sides=Mathf.Max(3,sides);
            int start=v.Count;float distance=0;
            Vector3 previous=(path[1]-path[0]).normalized;
            Vector3 frame=Vector3.Cross(previous,Mathf.Abs(previous.y)>.95f?Vector3.right:Vector3.up).normalized;
            for(int i=0;i<path.Count;i++){
                Vector3 tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
                frame=Quaternion.FromToRotation(previous,tangent)*frame;
                frame=(frame-tangent*Vector3.Dot(frame,tangent)).normalized;
                Vector3 binormal=Vector3.Cross(tangent,frame);
                if(i>0)distance+=Vector3.Distance(path[i-1],path[i]);
                for(int k=0;k<=sides;k++){
                    float angle=2*Mathf.PI*k/sides;
                    Vector3 radial=frame*Mathf.Cos(angle)+binormal*Mathf.Sin(angle);
                    v.Add(path[i]+radial*radius);n.Add(radial);uv.Add(new Vector2(k/(float)sides,distance));
                }
                previous=tangent;
            }
            var indices=Sub(m);
            for(int i=0;i<path.Count-1;i++)for(int k=0;k<sides;k++){
                int a=start+i*(sides+1)+k,b=a+sides+1;
                indices.AddRange(new[]{a,a+1,b+1,a,b+1,b});
            }
        }

        public static Vector3 WirePoint(Vector3 a,Vector3 b,float sag,float t) =>
            Vector3.Lerp(a,b,t)+Vector3.down*(4*sag*t*(1-t));

        static List<Vector3> WirePath(Vector3 a,Vector3 b,float sag,int segments)
        {
            var path=new List<Vector3>(segments+1);
            for(int i=0;i<=segments;i++)path.Add(WirePoint(a,b,sag,i/(float)segments));
            return path;
        }

        public void Wire(Vector3 a,Vector3 b,float sag,float radius,Material m,int sides=6)
        {
            Tube(WirePath(a,b,sag,Mathf.Max(12,Mathf.CeilToInt(Vector3.Distance(a,b)/.5f))),radius,m,sides);
        }

        public void Twisted(Vector3 a,Vector3 b,float sag,float strandRadius,int phases,float pitch,Material insulated,Material neutral)
        {
            pitch=Mathf.Max(.05f,pitch);phases=Mathf.Max(1,phases);
            // Arc-length upper bound also keeps at least ten samples per turn on sloped spans.
            int segments=Mathf.Max(12,Mathf.CeilToInt((Vector3.Distance(a,b)+2*Mathf.Abs(sag))*10/pitch));
            var axis=WirePath(a,b,sag,segments);
            Wire(a,b,sag,strandRadius*.85f,neutral);
            Vector3 previous=(axis[1]-axis[0]).normalized;
            Vector3 frame=Vector3.Cross(previous,Mathf.Abs(previous.y)>.95f?Vector3.right:Vector3.up).normalized;
            var paths=new List<Vector3>[phases];for(int k=0;k<phases;k++)paths[k]=new List<Vector3>();
            float distance=0;
            for(int i=0;i<axis.Count;i++){
                Vector3 tangent=(axis[Mathf.Min(i+1,segments)]-axis[Mathf.Max(0,i-1)]).normalized;
                frame=Quaternion.FromToRotation(previous,tangent)*frame;
                frame=(frame-tangent*Vector3.Dot(frame,tangent)).normalized;
                Vector3 binormal=Vector3.Cross(tangent,frame);
                if(i>0)distance+=Vector3.Distance(axis[i-1],axis[i]);
                for(int k=0;k<phases;k++){
                    float angle=2*Mathf.PI*(distance/pitch+k/(float)phases);
                    paths[k].Add(axis[i]+strandRadius*1.9f*(frame*Mathf.Cos(angle)+binormal*Mathf.Sin(angle)));
                }
                previous=tangent;
            }
            foreach(var path in paths)Tube(path,strandRadius,insulated);
        }

        public void Bundle(Vector3 a,Vector3 b,float sag,float[] radii,float spread,float tieSpacing,Material m,int seed)
        {
            if(radii==null || radii.Length==0)return;
            tieSpacing=Mathf.Max(.1f,tieSpacing);
            var random=new System.Random(seed);
            int segments=Mathf.Max(12,Mathf.CeilToInt(Vector3.Distance(a,b)/.25f));
            var axis=WirePath(a,b,sag,segments);
            var lengths=new float[axis.Count];
            for(int i=1;i<axis.Count;i++)lengths[i]=lengths[i-1]+Vector3.Distance(axis[i-1],axis[i]);
            Vector3 tangent=(b-a).normalized;
            Vector3 lateral=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.right:Vector3.up).normalized;
            Vector3 vertical=Vector3.Cross(tangent,lateral);
            float total=lengths[segments],maxRadius=0;
            // Independent cables fan out between ties, then converge to a compact cross section.
            for(int k=0;k<radii.Length;k++){
                float variation=(float)(random.NextDouble()*2-1)*.06f;
                float angle=(float)random.NextDouble()*2*Mathf.PI,offset=(float)random.NextDouble()*spread;
                Vector3 direction=lateral*Mathf.Cos(angle)+vertical*Mathf.Sin(angle);
                float packedAngle=2*Mathf.PI*k/radii.Length;
                Vector3 packed=(lateral*Mathf.Cos(packedAngle)+vertical*Mathf.Sin(packedAngle))*.012f;
                var path=new List<Vector3>();
                for(int i=0;i<axis.Count;i++){
                    float t=i/(float)segments,mod=Mathf.Abs(Mathf.Sin(Mathf.PI*lengths[i]/tieSpacing));
                    path.Add(WirePoint(a,b,sag,t)+packed+direction*offset*mod+Vector3.down*(sag*variation*4*t*(1-t)*mod));
                }
                Tube(path,radii[k],m);maxRadius=Mathf.Max(maxRadius,radii[k]);
            }
            int cursor=1;
            for(float distance=tieSpacing;distance<total;distance+=tieSpacing){
                while(cursor<segments && lengths[cursor]<distance)cursor++;
                float f=Mathf.InverseLerp(lengths[cursor-1],lengths[cursor],distance);
                float t=(cursor-1+f)/segments;
                Vector3 centre=WirePoint(a,b,sag,t);
                var ring=new List<Vector3>();
                for(int k=0;k<=8;k++){
                    float angle=2*Mathf.PI*k/8;
                    ring.Add(centre+(lateral*Mathf.Cos(angle)+vertical*Mathf.Sin(angle))*(.012f+maxRadius+.004f));
                }
                Tube(ring,.004f,m);
            }
        }

        // Ground patch: grid with a gentle deterministic undulation, continuous across neighbouring patches.
        public void Ground(float x0, float z0, float x1, float z1, float y, float amp, Material m, float step = 0.5f)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / step)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / step));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float xa = Mathf.Lerp(x0, x1, i / (float)nx), xb = Mathf.Lerp(x0, x1, (i + 1) / (float)nx);
                    float za = Mathf.Lerp(z0, z1, j / (float)nz), zb = Mathf.Lerp(z0, z1, (j + 1) / (float)nz);
                    Vector3 P(float x, float z) => new Vector3(x, y + amp * GroundNoise(x, z), z);
                    Vector3 a = P(xa, za), b = P(xb, za), c = P(xb, zb), d = P(xa, zb);
                    Quad(a, b, c, d, Vector3.Cross(d - a, b - a).normalized, m);
                }
        }

        public GameObject Emit(string name, Transform parent, bool lightmapped, float lightmapScale = 0.2f,
            ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            if (Empty) return null;
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = tris.Count;
            for (int i = 0; i < tris.Count; i++) mesh.SetTriangles(tris[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (lightmapped) Unwrapping.GenerateSecondaryUVSet(mesh);
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = obj.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            r.shadowCastingMode = shadows;
            SetExteriorStatic(r, lightmapped, lightmapScale);
            return obj;
        }
    }

    private static float GroundNoise(float x, float z) =>
        (Mathf.PerlinNoise(x * 0.35f + 31.7f, z * 0.35f + 12.1f) - 0.5f) * 1.4f + (Mathf.PerlinNoise(x * 1.3f + 5f, z * 1.3f + 9f) - 0.5f) * 0.6f;

    // Lightmapped structure (ground, walls, houses): GI-static; everything else uses light probes.
    private static void SetExteriorStatic(MeshRenderer r, bool lightmapped, float lightmapScale)
    {
        Material m = r.sharedMaterial;
        bool emissiveOrClear = m != null && (m.renderQueue >= 3000 || m.IsKeywordEnabled("_EMISSION"));
        if (lightmapped && !emissiveOrClear)
        {
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
            r.receiveGI = ReceiveGI.Lightmaps;
            var so = new SerializedObject(r);
            so.FindProperty("m_ScaleInLightmap").floatValue = lightmapScale;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            r.receiveGI = ReceiveGI.LightProbes;
        }
    }

    // Invisible box collider over [x0, x1] x [z0, z1] from y0 to y1 (walls, railings, gates are solid to the ground).
    private static void Collider(string name, Transform parent, float x0, float z0, float x1, float z1, float y0, float y1) =>
        Solid(name, parent, Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1), y0, y1);

    // ------------------------------------------------------------------ lights and probes

    // Exterior light with a visible fitting (the fitting is built by the caller); Mixed like the other lamps.
    private static Light ExteriorSpot(string name, Vector3 at, Vector3 aim, float angle, float range, float intensity, float kelvin, bool shadows = false)
    {
        Light light = SpotLight("Luz - " + name, at, angle, angle * 0.45f, range, intensity, kelvin, shadows);
        light.transform.rotation = Quaternion.LookRotation(aim.normalized, Mathf.Abs(aim.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
        light.transform.SetParent(exteriorLights, true);
        return light;
    }

    private static Light ExteriorPoint(string name, Vector3 at, float range, float intensity, float kelvin)
    {
        var obj = new GameObject("Luz - " + name);
        Undo.RegisterCreatedObjectUndo(obj, "Create light");
        obj.transform.SetParent(exteriorLights, false);
        obj.transform.position = at;
        var light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.useColorTemperature = true;
        light.colorTemperature = kelvin;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Mixed;
        return light;
    }

    private static void ProbeGrid(float x0, float z0, float x1, float z1, float step, params float[] heights)
    {
        for (float x = x0; x <= x1 + 1e-3f; x += step)
            for (float z = z0; z <= z1 + 1e-3f; z += step)
                foreach (float h in heights) exteriorProbes.Add(new Vector3(x, h, z));
    }

    private static void ExteriorProbeGroup()
    {
        ProbeGrid(-18.2f, -18.7f, 6.7f, -13.4f, 1.5f, 0.4f, 1.6f, 3.2f);     // front yard
        ProbeGrid(-18.2f, -13.0f, -15.1f, -8.3f, 1.5f, 0.4f, 1.6f, 3.2f);    // west strip up to the side gate
        ProbeGrid(3.8f, -12.5f, 6.7f, -3.9f, 1.5f, 0.4f, 1.6f, 3.2f);        // nook east of the archive
        ProbeGrid(-6.3f, -12.3f, 3.3f, -10.6f, 1.6f, 0.4f, 1.6f);            // bed in front of the archive
        ProbeGrid(7.5f, -18.5f, 20.5f, 4.5f, 3f, 0.4f, 2.0f, 4.5f);          // service yard and drive
        ProbeGrid(-18f, -2f, 12f, 17.5f, 3f, 0.4f, 2.0f, 4.5f);              // back yard (probes inside the building are harmless)
        ProbeGrid(-40f, -30.5f, 40f, -19.5f, 3f, 0.4f, 2.0f, 6f);            // street
        var obj = new GameObject(ExteriorProbesName);
        Undo.RegisterCreatedObjectUndo(obj, "Create exterior light probes");
        obj.transform.SetParent(lightsGroup, false);
        obj.AddComponent<LightProbeGroup>().probePositions = exteriorProbes.ToArray();
    }
}
