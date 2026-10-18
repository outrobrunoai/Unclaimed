// Finish passes run after the walls and doorways are built (Generate), and on demand on the saved map
// (Tools > Unclaimed > Update Wall Finishes):
// - skirting and dado rails stop against the door casings instead of running under them, where their faces
//   coincided with the casing's and flickered;
// - coplanar finish panels and collinear trim strips of the same material join into one mesh each, with one
//   lightmap chart: separate quads (the paint above a door and the paint beside it) each got their own lightmap
//   island and texel density, and the bake showed every piece as a faint rectangle.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class UnclaimedMorgueGenerator
{
    // Strips that run along a wall up to a doorway; they butt against its casing.
    private static readonly string[] StripsStoppedByCasings = { "Rodape - ", "Friso - " };

    /// <summary>Applies the finish passes and rebuilds the wall signs on the open scene's map, without
    /// regenerating it. Re-bake the lighting afterwards (Tools > Unclaimed > Bake Prototype Lighting).</summary>
    [MenuItem("Tools/Unclaimed/Update Wall Finishes")]
    public static void UpdateWallFinishes()
    {
        GameObject morgue = FindExistingMorgue(SceneManager.GetActiveScene());
        if (morgue == null || morgue.name != GroupName)
        {
            Debug.LogWarning("Unclaimed: no '" + GroupName + "' in the open scene.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update wall finishes");
        root = morgue.transform;
        claddingGroup = root.Find("Revestimentos");
        skirtingGroup = root.Find("Rodapes");
        envelopeGroup = root.Find("Fechamento externo");
        doorsGroup = root.Find("Portas");
        roomsGroup = root.Find("Ambientes");

        FitFrameLinings();
        StopStripsAtCasings();
        UnifyWallFinishes();
        Transform recepcao = roomsGroup.Find("Recepcao/Parede - sinalizacao e quadros"), wc = roomsGroup.Find("WC/Sinalizacao");
        if (recepcao != null) RecepcaoSigns(recepcao);
        if (wc != null) WCSigns(wc);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(morgue.scene);
        Debug.Log("Unclaimed: wall finishes updated. Re-bake with Tools > Unclaimed > Bake Prototype Lighting, then save.");
    }

    // Generated frames: the head lining and the threshold fit between the side linings (they used to overlap
    // them at the corners, two boxes sharing the same faces). New maps get this from Doorways directly.
    private static void FitFrameLinings()
    {
        foreach (Door d in Doors)
        {
            Transform g = doorsGroup.Find(d.Name);
            if (g == null) continue;
            float a = d.Center - d.Width / 2 + FrameLining, b = d.Center + d.Width / 2 - FrameLining;
            foreach (Transform t in g)
                if (t.name == "Batente superior" || t.name == "Soleira")
                    ResizeAlong(t, d.Vertical, a, b);
        }
    }

    // Skirting and dado rails reach the doorway's opening edge; where a casing covers that edge they stop at
    // the casing's outer side. The casing's reach is read from the frame actually built (generated boxes or
    // the modelled prefab), at floor level, separately for each face and each side of the opening.
    private static void StopStripsAtCasings()
    {
        var strips = new List<Transform>();
        foreach (Transform group in new[] { skirtingGroup, claddingGroup })
            if (group != null)
                foreach (MeshRenderer r in group.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (string prefix in StripsStoppedByCasings)
                        if (r.name.StartsWith(prefix) && IsBox(r)) strips.Add(r.transform);

        const float half = WallThickness / 2, eps = 0.001f;
        foreach (Door d in Doors)
        {
            if (!d.Header) continue;
            Transform g = doorsGroup.Find(d.Name);
            if (g == null) continue;
            float a = d.Center - d.Width / 2, b = d.Center + d.Width / 2;
            foreach (float sign in new[] { -1f, 1f })
            {
                CasingReach(g, d, sign, out float reachA, out float reachB);
                foreach (Transform s in strips)
                {
                    Vector3 p = s.position, size = s.localScale;
                    if (d.Vertical ? size.z < size.x : size.x < size.z) continue;   // runs along another wall
                    float across = (d.Vertical ? p.x : p.z) - d.Line, acrossSize = d.Vertical ? size.x : size.z;
                    if (across * sign < half - eps || Mathf.Abs(across) - acrossSize / 2 > half + 0.05f) continue;
                    if (p.y - size.y / 2 > DoorHeight) continue;
                    float along = d.Vertical ? p.z : p.x, length = d.Vertical ? size.z : size.x;
                    float s0 = along - length / 2, s1 = along + length / 2;
                    if (reachA > 0f && Mathf.Abs(s1 - a) < eps) ResizeAlong(s, d.Vertical, s0, a - reachA);
                    else if (reachB > 0f && Mathf.Abs(s0 - b) < eps) ResizeAlong(s, d.Vertical, b + reachB, s1);
                }
            }
        }
    }

    // How far the casing on one face (`sign`: -1 low side, +1 high side) runs past each edge of the opening.
    // Door leaves are left out: they swing and are not part of the frame.
    private static void CasingReach(Transform doorGroup, Door d, float sign, out float reachA, out float reachB)
    {
        const float half = WallThickness / 2;
        float a = d.Center - d.Width / 2, b = d.Center + d.Width / 2;
        reachA = reachB = 0f;
        foreach (MeshFilter f in doorGroup.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null || f.GetComponentInParent<FolhaDePorta>() != null) continue;
            if (f.name == "Verga") continue;
            Matrix4x4 m = f.transform.localToWorldMatrix;
            foreach (Vector3 v in f.sharedMesh.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                float across = ((d.Vertical ? w.x : w.z) - d.Line) * sign, along = d.Vertical ? w.z : w.x;
                if (w.y > 0.05f || across <= half + 0.0005f || across > half + 0.06f) continue;
                reachA = Mathf.Max(reachA, a - along);
                reachB = Mathf.Max(reachB, along - b);
            }
        }
    }

    // Joins coplanar panels (same name, parent and material) into one mesh per connected patch, and collinear
    // strips with the same section into one box. Each result has a single lightmap chart in proportion to its
    // size, so the bake runs continuously across what used to be separate pieces.
    private static void UnifyWallFinishes()
    {
        foreach (Transform group in new[] { claddingGroup, skirtingGroup, envelopeGroup })
        {
            if (group == null) continue;
            var panels = new Dictionary<string, List<MeshRenderer>>();
            var strips = new Dictionary<string, List<MeshRenderer>>();
            foreach (MeshRenderer r in group.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<Collider>() != null || r.sharedMaterials.Length != 1) continue;
                if (PanelKey(r, out string pk)) Add(panels, pk, r);
                else if (StripKey(r, out string sk)) Add(strips, sk, r);
            }
            foreach (List<MeshRenderer> list in panels.Values)
                foreach (List<MeshRenderer> patch in Connected(list, PanelsTouch))
                    if (patch.Count > 1) MergePanels(patch);
            foreach (List<MeshRenderer> list in strips.Values)
                foreach (List<MeshRenderer> run in Connected(list, StripsTouch))
                    if (run.Count > 1) MergeStrips(run);
        }
    }

    private static void Add(Dictionary<string, List<MeshRenderer>> map, string key, MeshRenderer r)
    {
        if (!map.TryGetValue(key, out List<MeshRenderer> list)) map[key] = list = new List<MeshRenderer>();
        list.Add(r);
    }

    // A wall panel from Panel(): a quad facing along X or Z, with UVs in metres along its own edges.
    private static bool PanelKey(MeshRenderer r, out string key)
    {
        key = null;
        Mesh mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || mesh.vertexCount != 4 || r.transform.localToWorldMatrix != Matrix4x4.identity) return false;
        Vector3 n = mesh.normals[0];
        if (Mathf.Abs(n.y) > 0.001f || (Mathf.Abs(n.x) < 0.999f && Mathf.Abs(n.z) < 0.999f)) return false;
        Vector3[] v = mesh.vertices;
        Vector2[] uv = mesh.uv;
        Vector3 axis = PanelAxis(n);
        for (int i = 0; i < 4; i++)
            if (Mathf.Abs(uv[i].x - Vector3.Dot(v[i], axis)) > 0.0005f || Mathf.Abs(uv[i].y - v[i].y) > 0.0005f) return false;
        float plane = Vector3.Dot(v[0], n);
        key = r.transform.parent.GetEntityId() + "|" + r.name + "|" + r.sharedMaterial.GetEntityId() + "|" +
              Vector3Int.RoundToInt(n) + "|" + Mathf.RoundToInt(plane * 10000f);
        return true;
    }

    // FacePanel's along-the-wall axis (texture u): +Z on walls facing X, +X on walls facing Z.
    private static Vector3 PanelAxis(Vector3 normal) => Mathf.Abs(normal.x) > 0.5f ? Vector3.forward : Vector3.right;

    private static Rect PanelRect(MeshRenderer r)
    {
        Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
        Vector3 axis = PanelAxis(mesh.normals[0]);
        float u0 = float.MaxValue, u1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        foreach (Vector3 v in mesh.vertices)
        {
            float u = Vector3.Dot(v, axis);
            u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
            y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
        }
        return Rect.MinMaxRect(u0, y0, u1, y1);
    }

    // Share an edge or overlap (corners touching only do not count).
    private static bool PanelsTouch(MeshRenderer p, MeshRenderer q)
    {
        Rect a = PanelRect(p), b = PanelRect(q);
        const float e = 0.0005f;
        float ou = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), ov = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return ou > -e && ov > -e && (ou > e || ov > e);
    }

    private static bool IsBox(MeshRenderer r)
    {
        Mesh mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
        return mesh != null && (mesh.name == "Cube" || mesh.name == "Cube (lightmap UV)") && r.transform.rotation == Quaternion.identity;
    }

    // A trim strip from FaceStrip(): an axis-aligned box, long along X or Z.
    private static bool StripKey(MeshRenderer r, out string key)
    {
        key = null;
        if (!IsBox(r)) return false;
        Vector3 p = r.transform.position, s = r.transform.localScale;
        bool alongX = s.x >= s.z;
        float q(float f) => Mathf.Round(f * 10000f);
        key = r.transform.parent.GetEntityId() + "|" + r.name + "|" + r.sharedMaterial.GetEntityId() + "|" + (alongX ? "x" : "z") + "|" +
              q(p.y - s.y / 2) + "|" + q(p.y + s.y / 2) + "|" +
              (alongX ? q(p.z - s.z / 2) + "|" + q(p.z + s.z / 2) : q(p.x - s.x / 2) + "|" + q(p.x + s.x / 2));
        return true;
    }

    private static Vector2 StripSpan(MeshRenderer r)
    {
        Vector3 p = r.transform.position, s = r.transform.localScale;
        return s.x >= s.z ? new Vector2(p.x - s.x / 2, p.x + s.x / 2) : new Vector2(p.z - s.z / 2, p.z + s.z / 2);
    }

    private static bool StripsTouch(MeshRenderer p, MeshRenderer q)
    {
        Vector2 a = StripSpan(p), b = StripSpan(q);
        return Mathf.Min(a.y, b.y) - Mathf.Max(a.x, b.x) > -0.0005f;
    }

    private static List<List<MeshRenderer>> Connected(List<MeshRenderer> items, System.Func<MeshRenderer, MeshRenderer, bool> touch)
    {
        var result = new List<List<MeshRenderer>>();
        var done = new bool[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            if (done[i]) continue;
            var patch = new List<MeshRenderer> { items[i] };
            done[i] = true;
            for (int k = 0; k < patch.Count; k++)
                for (int j = 0; j < items.Count; j++)
                    if (!done[j] && touch(patch[k], items[j]))
                    {
                        done[j] = true;
                        patch.Add(items[j]);
                    }
            result.Add(patch);
        }
        return result;
    }

    // One mesh over the union of the panels' rectangles: a grid on all their edges, so neighbouring cells share
    // vertices (no T-junctions) and the lightmap is continuous across the old seams.
    private static void MergePanels(List<MeshRenderer> patch)
    {
        MeshRenderer first = patch[0];
        Mesh src = first.GetComponent<MeshFilter>().sharedMesh;
        Vector3 n = src.normals[0], axis = PanelAxis(n);
        Vector4 tangent = src.tangents[0];
        float plane = Vector3.Dot(src.vertices[0], n);
        var rects = patch.ConvertAll(PanelRect);
        var us = new SortedSet<float>();
        var ys = new SortedSet<float>();
        foreach (Rect r in rects)
        {
            us.Add(Snap(r.xMin)); us.Add(Snap(r.xMax));
            ys.Add(Snap(r.yMin)); ys.Add(Snap(r.yMax));
        }
        var u = new List<float>(us);
        var y = new List<float>(ys);
        float uMin = u[0], yMin = y[0], extent = Mathf.Max(u[u.Count - 1] - uMin, y[y.Count - 1] - yMin);

        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var tris = new List<int>();
        var index = new Dictionary<Vector2Int, int>();
        Vector3 across = n * plane;
        int Vertex(int i, int j)
        {
            var key = new Vector2Int(i, j);
            if (index.TryGetValue(key, out int at)) return at;
            index[key] = verts.Count;
            verts.Add(across + axis * u[i] + Vector3.up * y[j]);
            uv.Add(new Vector2(u[i], y[j]));
            uv2.Add(new Vector2((u[i] - uMin) / extent, (y[j] - yMin) / extent));
            return verts.Count - 1;
        }
        bool frontCcw = Vector3.Dot(Vector3.Cross(axis, Vector3.up), n) > 0;
        for (int i = 0; i + 1 < u.Count; i++)
            for (int j = 0; j + 1 < y.Count; j++)
            {
                var c = new Vector2((u[i] + u[i + 1]) / 2, (y[j] + y[j + 1]) / 2);
                if (!rects.Exists(r => r.Contains(c))) continue;
                int a = Vertex(i, j), b = Vertex(i + 1, j), cc = Vertex(i + 1, j + 1), d = Vertex(i, j + 1);
                tris.AddRange(frontCcw ? new[] { a, b, cc, a, cc, d } : new[] { a, cc, b, a, d, cc });
            }

        var normals = new Vector3[verts.Count];
        var tangents = new Vector4[verts.Count];
        for (int i = 0; i < verts.Count; i++) { normals[i] = n; tangents[i] = tangent; }
        var mesh = new Mesh { name = first.name, vertices = verts.ToArray(), uv = uv.ToArray(), uv2 = uv2.ToArray(), triangles = tris.ToArray(), normals = normals, tangents = tangents };
        mesh.RecalculateBounds();
        ReplaceWith(patch, mesh, Vector3.zero, Vector3.one);
    }

    private static float Snap(float f) => Mathf.Round(f * 10000f) / 10000f;

    private static void MergeStrips(List<MeshRenderer> run)
    {
        Bounds b = run[0].bounds;
        foreach (MeshRenderer r in run) b.Encapsulate(r.bounds);
        Mesh mesh = run[0].GetComponent<MeshFilter>().sharedMesh.name == "Cube (lightmap UV)"
            ? LightmapCube(b.size) : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        ReplaceWith(run, mesh, b.center, b.size);
    }

    // A new object in place of the first piece (same name, parent, sibling slot, material, shadow and lightmap
    // settings), then the pieces are removed.
    private static void ReplaceWith(List<MeshRenderer> pieces, Mesh mesh, Vector3 position, Vector3 scale)
    {
        MeshRenderer first = pieces[0];
        var obj = new GameObject(first.name);
        Undo.RegisterCreatedObjectUndo(obj, "Merge " + first.name);
        obj.transform.SetParent(first.transform.parent, false);
        obj.transform.SetSiblingIndex(first.transform.GetSiblingIndex());
        obj.transform.SetPositionAndRotation(position, Quaternion.identity);
        obj.transform.localScale = scale;
        obj.layer = first.gameObject.layer;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterial = first.sharedMaterial;
        r.shadowCastingMode = first.shadowCastingMode;
        r.receiveShadows = first.receiveShadows;
        GameObjectUtility.SetStaticEditorFlags(obj, GameObjectUtility.GetStaticEditorFlags(first.gameObject));
        r.receiveGI = first.receiveGI;
        var from = new SerializedObject(first);
        var to = new SerializedObject(r);
        to.FindProperty("m_ScaleInLightmap").floatValue = from.FindProperty("m_ScaleInLightmap").floatValue;
        to.ApplyModifiedPropertiesWithoutUndo();
        foreach (MeshRenderer p in pieces) Undo.DestroyObjectImmediate(p.gameObject);
    }

    // Sets a box's extent along the wall (X for lines along X, Z for vertical lines), keeping the rest.
    private static void ResizeAlong(Transform t, bool vertical, float from, float to)
    {
        Undo.RecordObject(t, "Resize " + t.name);
        Vector3 p = t.position, s = t.localScale;
        if (vertical) { p.z = (from + to) / 2; s.z = to - from; }
        else { p.x = (from + to) / 2; s.x = to - from; }
        t.position = p;
        t.localScale = s;
        var filter = t.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Cube (lightmap UV)")
        {
            Undo.RecordObject(filter, "Resize " + t.name);
            filter.sharedMesh = LightmapCube(s);
        }
    }
}
