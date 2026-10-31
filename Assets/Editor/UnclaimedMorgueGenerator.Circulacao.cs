// Circulation corridors (zones "Circulacao" and "Vestibulo de servico"): finishes, wall protections, signage,
// fittings and lighting. Three legs share the zone but not the look, so the route reads as it turns:
//   leg 0  reception link (z < -3.875):        warm grey granilite, sage dado, cream paint, 4000 K
//   leg 1  staff rooms (z -3.875 .. 3.625):    cool grey granilite, blue-grey dado, 4500 K, stretcher rails
//   leg 2  service wing (z > 3.625, vestibule): green-grey granilite, clinical green dado, 4800-5200 K, rails
// Textures: Blender/Circulacao/scripts/texturas_circulacao.py (floor, decals, signs); fittings: Blender/Circulacao/scripts/gerar.py.
// Tools > Unclaimed > Circulacao > Update Corridors rebuilds all of this on the saved map without regenerating it.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class UnclaimedMorgueGenerator
{
    private const string CirculacaoFolder = BaseFolder + "/Circulacao";
    private const string CirculacaoMaterials = CirculacaoFolder + "/Materiais";
    private const string CirculacaoTextures = CirculacaoFolder + "/Texturas/";
    private const string CirculacaoRoom = "Circulacao", VestibuleRoom = "Vestibulo de servico";

    private static ZoneFinish[] corridorLegs;

    // Leg boundaries along Z (the legs change only along the north-south walls).
    private static readonly float[] LegBreaks = { -3.875f, 3.625f };

    private static int CorridorLeg(float x, float z) => x > 3.2f ? 2 : z < LegBreaks[0] ? 0 : z < LegBreaks[1] ? 1 : 2;

    private static ZoneFinish[] CorridorFinishes()
    {
        string t = RecepcaoTextures, a = AutopsiaTextures, c = CirculacaoTextures, f = CirculacaoMaterials;
        string[] floorKey = { "A", "B", "C" };
        Color[] dado = { new Color(0.58f, 0.66f, 0.60f), new Color(0.53f, 0.61f, 0.67f), new Color(0.62f, 0.74f, 0.70f) };
        Color[] upper = { new Color(0.94f, 0.92f, 0.85f), new Color(0.91f, 0.92f, 0.91f), new Color(0.95f, 0.96f, 0.94f) };
        float[] dadoHeight = { 1.2f, 1.2f, 1.5f };
        Material skirting = Mat(f, "Rodape granilite", new Color(0.30f, 0.30f, 0.29f), 0.40f);
        Material ceiling = Mat(AutopsiaFolder, "Forro mineral", Color.white, 0.05f, 0f, a + "Autopsia_Forro_Base.png", a + "Autopsia_Forro_Normal.png", 1.25f);
        var legs = new ZoneFinish[3];
        for (int i = 0; i < 3; i++)
        {
            string k = floorKey[i], n = "trecho " + (i + 1);
            legs[i] = new ZoneFinish
            {
                Floor = Mat(f, "Piso granilite " + k, Color.white, 1f, 0f, c + "Circulacao_Granilite_" + k + "_Base.png",
                    c + "Circulacao_Granilite_" + k + "_Normal.png", 2.4f, glossMap: c + "Circulacao_Granilite_" + k + "_MS.png"),
                Skirting = skirting,
                Wainscot = Mat(f, "Barrado circulacao " + n, dado[i], 0.46f, 0f, t + "Recepcao_Pintura_Base.png",
                    t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
                WainscotHeight = dadoHeight[i],
                UpperWall = Mat(f, "Pintura circulacao " + n, upper[i], 0.14f, 0f, t + "Recepcao_Pintura_Base.png",
                    t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
                Ceiling = ceiling,
                Friso = Mat(f, "Friso circulacao " + n, dado[i] * 0.84f, 0.5f),
                CeilingTrim = Mat(RecepcaoFolder, "Cantoneira do forro", new Color(0.93f, 0.93f, 0.91f), 0.35f),
            };
        }
        return legs;
    }

    private static ZoneFinish CorridorFinish(int leg)
    {
        if (corridorLegs == null) corridorLegs = CorridorFinishes();
        return corridorLegs[leg];
    }

    // Rect 0 of the corridor zone is leg 0, rects 1-2 are leg 1, rect 3 is leg 2.
    private static Material CorridorFloor(string zone, int rect, Material fallback)
    {
        if (zone == VestibuleRoom) return CorridorFinish(2).Floor;
        if (zone != CirculacaoRoom) return fallback;
        return CorridorFinish(rect == 0 ? 0 : rect < 3 ? 1 : 2).Floor;
    }

    // Wall finish of a corridor face: the face is cut at the leg boundaries and each piece gets its leg's finish.
    private static void CladCorridor(string room, bool vertical, float face, float sign, float a, float b, float fromY,
        bool wrapStart, bool wrapEnd)
    {
        var cuts = new List<float> { a };
        if (vertical && room == CirculacaoRoom)
            foreach (float z in LegBreaks)
                if (z > a + 0.01f && z < b - 0.01f) cuts.Add(z);
        cuts.Add(b);
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            float mid = (cuts[i] + cuts[i + 1]) / 2, into = face - sign * 0.3f;
            int leg = room == VestibuleRoom ? 2 : vertical ? CorridorLeg(into, mid) : CorridorLeg(mid, into);
            CladFaceCore(room, CorridorFinish(leg), vertical, face, sign, cuts[i], cuts[i + 1], fromY,
                wrapStart && i == 0, wrapEnd && i + 2 == cuts.Count);
        }
    }

    // ------------------------------------------------------------------ fittings, signs, protections

    private static Material CorridorSign(string file, bool lit = false)
    {
        string name = "Placa " + file.Replace(".png", "").Replace("Placa_", "");
        Material m = Mat(CirculacaoMaterials, lit ? name + " luminosa" : name, Color.white, lit ? 0.3f : 0.35f, 0f, CirculacaoTextures + file);
        if (lit && !m.IsKeywordEnabled("_EMISSION"))
        {
            m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
            m.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
        }
        return m;
    }

    private static GameObject Sign(string label, Transform parent, string model, Vector3 wallPoint, Vector3 normal) =>
        // On the finish surface (wall face + FinishOffset), so the sign back is never coplanar with the cladding.
        PlaceFitting(UnclaimedCirculacaoAssets.PlacaPrefab(model), parent, label.StartsWith("Saida") ? label : "Placa - " + label, wallPoint + normal * FinishOffset, normal);

    private static GameObject PlaceFitting(string prefabPath, Transform parent, string name, Vector3 point, Vector3 normal)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogWarning("Unclaimed: missing fitting prefab " + prefabPath + "; run Tools > Unclaimed > Circulacao > Build Fitting Prefabs."); return null; }
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-normal));
        return obj;
    }

    // Decal on a wall face: u runs along the wall (left to right seen from the front), v up.
    private static void WallDecal(string name, Transform parent, Vector3 point, Vector3 normal, float w, float h, Material m) =>
        Decal(name, parent, point + normal * 0.006f, normal, Vector3.Cross(Vector3.up, normal), Vector3.up, w, h, m);

    private struct Span { public float A, B; }

    // Door openings on a wall line (with a margin for the casings), so rails and guards stop short of them.
    private static List<Span> OpeningsOn(bool vertical, float line, float margin)
    {
        var list = new List<Span>();
        foreach (Door d in Doors)
            if (d.Vertical == vertical && Mathf.Abs(d.Line - line) < 0.01f)
                list.Add(new Span { A = d.Center - d.Width / 2 - margin, B = d.Center + d.Width / 2 + margin });
        return list;
    }

    // A horizontal wall rail (stretcher bumper) from a to b on one face, minus the openings and the fittings hung on it.
    private static void StretcherRail(Transform parent, string name, bool vertical, float line, float sign, float a, float b,
        params float[] fittings)
    {
        var cuts = new List<Span>(OpeningsOn(vertical, line, 0.08f));
        foreach (float f in fittings) cuts.Add(new Span { A = f - 0.40f, B = f + 0.40f });
        cuts.Sort((p, q) => p.A.CompareTo(q.A));
        float cursor = a;
        int part = 0;
        void Emit(float from, float to)
        {
            if (to - from < 0.35f) return;
            part++;
            float face = line + sign * WallThickness / 2;
            Material pvc = Mat(CirculacaoMaterials, "Defensa PVC", new Color(0.80f, 0.80f, 0.75f), 0.5f);
            Material rubber = Mat(CirculacaoMaterials, "Defensa borracha", new Color(0.20f, 0.20f, 0.20f), 0.35f);
            Transform g = Sub(parent, name + " " + part);
            FaceStrip("Defensa - corpo", g, vertical, face, sign, from, to, 0.84f, 0.96f, 0.018f, pvc);
            FaceStrip("Defensa - friso inferior", g, vertical, face, sign, from, to, 0.82f, 0.84f, 0.010f, rubber);
            FaceStrip("Defensa - friso superior", g, vertical, face, sign, from, to, 0.96f, 0.975f, 0.010f, rubber);
            foreach (float end in new[] { from, to })   // end caps
                FaceStrip("Defensa - ponteira", g, vertical, face, sign, end - 0.006f, end + 0.006f, 0.82f, 0.975f, 0.020f, pvc);
        }
        foreach (Span c in cuts)
        {
            if (c.B <= cursor) continue;
            Emit(cursor, Mathf.Min(c.A, b));
            cursor = Mathf.Max(cursor, c.B);
            if (cursor >= b) return;
        }
        Emit(cursor, b);
    }

    // Stainless corner guard on a convex wall corner. (cx, cz): the corner of the finished faces; (mx, mz): which
    // quadrant the wall mass lies in. Two thin legs that wrap the corner, 1.5 m tall.
    private static void CornerGuard(Transform parent, string name, float cx, float cz, float mx, float mz)
    {
        Material steel = Mat(CirculacaoMaterials, "Protetor de canto inox", new Color(0.66f, 0.66f, 0.64f), 0.55f, 0.85f);
        const float leg = 0.05f, t = 0.004f, y0 = 0.10f, y1 = 1.60f;
        Transform g = Sub(parent, name);
        // leg lying on the face whose normal is -mz (runs along x into the mass), wraps the corner
        Piece("Aba A", g, Mathf.Min(cx - mx * t, cx + mx * leg), y0, Mathf.Min(cz - mz * t, cz), Mathf.Max(cx - mx * t, cx + mx * leg), y1, Mathf.Max(cz - mz * t, cz), steel);
        // leg lying on the face whose normal is -mx (runs along z into the mass)
        Piece("Aba B", g, Mathf.Min(cx - mx * t, cx), y0, Mathf.Min(cz, cz + mz * leg), Mathf.Max(cx - mx * t, cx), y1, Mathf.Max(cz, cz + mz * leg), steel);
    }

    // The corridors' dressing, built once per map into Ambientes/Circulacao.
    private static void BuildCirculacao(Transform room)
    {
        Transform signs = Sub(room, "Sinalizacao"), fittings = Sub(room, "Equipamentos"), guards = Sub(room, "Protecoes"),
            wear = Sub(room, "Desgaste"), tech = Sub(room, "Instalacoes");
        Material pvc = Mat(RecepcaoFolder, "PVC branco", new Color(0.92f, 0.92f, 0.9f), 0.4f);
        Vector3 east = Vector3.right, west = Vector3.left, north = Vector3.forward, south = Vector3.back;
        const float face = WallThickness / 2;

        // Signage (docs/direcao-arte/comunicacao-visual.md section 2): room plates, orientation panels and safety signs
        // are separate families, each a single prefab mesh (Blender/Circulacao/scripts/placas.py), origin at the wall plane.
        Sign("diretorio", signs, "OR_Diretorio", new Vector3(-8.0f, 1.72f, -3.875f - face), south);
        Sign("arquivo", signs, "ID_Arquivo", new Vector3(-2f - face, 2.50f, -4.75f), west);
        Sign("descanso", signs, "ID_Descanso", new Vector3(-5.25f + face, 2.50f, -0.25f), east);
        Sign("deposito", signs, "ID_Deposito", new Vector3(-4.5f + face, 2.50f, 3.5f), east);
        Sign("autopsia", signs, "ID_Autopsia", new Vector3(-2f - face, 2.49f, 0f), west);
        Sign("sala tecnica", signs, "ID_SalaTecnica", new Vector3(-3.5f, 2.49f, 5.75f - face), south);
        Sign("risco eletrico", signs, "SEG_Risco", new Vector3(-2.45f, 1.62f, 5.75f - face), south);
        Sign("direcao recebimento", signs, "OR_Recebimento", new Vector3(3.25f - face, 2.66f, 4.5f), west);
        Sign("recebimento", signs, "ID_Recebimento", new Vector3(5.375f, 2.50f, 5.25f - face), south);
        Sign("camara fria (autopsia)", signs, "ID_CamaraFria", new Vector3(7.5f - face, 2.50f, 0.25f), west);
        Sign("camara fria (recebimento)", signs, "ID_CamaraFria", new Vector3(9.75f, 2.50f, 5.25f + face), north);
        // Exit signs: lit boxes (recessed print + soft emission), one over the circulation door, one on the way to the yard.
        Sign("Saida - porta da circulacao", signs, "SEG_Saida_Frente", new Vector3(-8.0f, 2.46f, -6f + face), north);
        Sign("Saida - rumo ao patio", signs, "SEG_Saida_Direita", new Vector3(-1.6f, 2.52f, 5.75f - face), south);

        // Fire fighting: three extinguishers with their sign above, two manual call points with theirs.
        string ext = UnclaimedCirculacaoAssets.PrefabPath(UnclaimedCirculacaoAssets.Extintor);
        string acionador = UnclaimedCirculacaoAssets.PrefabPath(UnclaimedCirculacaoAssets.Acionador);
        string bloco = UnclaimedCirculacaoAssets.PrefabPath(UnclaimedCirculacaoAssets.Bloco);
        PlaceFitting(ext, fittings, "Extintor - recuo da circulacao", new Vector3(-10.375f + face + FinishOffset, 0f, -4.95f), east);
        Sign("extintor (recuo)", signs, "SEG_Extintor", new Vector3(-10.375f + face, 1.80f, -4.95f), east);
        PlaceFitting(ext, fittings, "Extintor - corredor de servico", new Vector3(2.3f, 0f, 5.75f - face - FinishOffset), south);
        Sign("extintor (servico)", signs, "SEG_Extintor", new Vector3(2.3f, 1.80f, 5.75f - face), south);
        PlaceFitting(ext, fittings, "Extintor - vestibulo", new Vector3(7.5f - face - FinishOffset, 0f, 4.45f), west);
        Sign("extintor (vestibulo)", signs, "SEG_Extintor", new Vector3(7.5f - face, 1.80f, 4.45f), west);
        PlaceFitting(acionador, fittings, "Acionador - recuo da circulacao", new Vector3(-10.375f + face + FinishOffset, 1.30f, -5.55f), east);
        Sign("alarme (recuo)", signs, "SEG_Alarme", new Vector3(-10.375f + face, 1.53f, -5.55f), east);
        PlaceFitting(acionador, fittings, "Acionador - corredor de servico", new Vector3(1.3f, 1.30f, 5.75f - face - FinishOffset), south);
        Sign("alarme (servico)", signs, "SEG_Alarme", new Vector3(1.3f, 1.53f, 5.75f - face), south);

        // Autonomous emergency light blocks over the turns; off in normal operation (green charge LED).
        PlaceFitting(bloco, fittings, "Bloco de emergencia - recuo", new Vector3(-9.3f, 2.30f, -3.875f - face + FinishOffset), south);
        PlaceFitting(bloco, fittings, "Bloco de emergencia - trecho 2", new Vector3(-2f - face + FinishOffset, 2.30f, -2.3f), west);
        PlaceFitting(bloco, fittings, "Bloco de emergencia - trecho 3", new Vector3(-4.5f + face - FinishOffset, 2.30f, 1.8f), east);
        PlaceFitting(bloco, fittings, "Bloco de emergencia - corredor de servico", new Vector3(0.4f, 2.30f, 5.75f - face - FinishOffset), south);
        PlaceFitting(bloco, fittings, "Bloco de emergencia - vestibulo", new Vector3(5.6f, 2.30f, 3.625f + face + FinishOffset), north);

        // Stretcher rails along the walls the trolleys use (the route from the receiving room to the autopsy room).
        StretcherRail(guards, "Defensa - trecho 2 oeste", true, -5.25f, 1f, -3.70f, 0.80f, -2.6f);
        StretcherRail(guards, "Defensa - trecho 2 leste", true, -2f, -1f, -3.70f, 0.80f);
        StretcherRail(guards, "Defensa - trecho 3 leste", true, -2f, -1f, 0.80f, 3.50f);
        StretcherRail(guards, "Defensa - trecho 3 oeste", true, -4.5f, 1f, 0.95f, 5.65f, 1.8f);
        StretcherRail(guards, "Defensa - servico norte", false, 3.625f, 1f, -1.90f, 3.15f);
        StretcherRail(guards, "Defensa - servico sul", false, 5.75f, -1f, -4.40f, 3.15f, -1.6f, 0.4f, 1.3f, 2.3f);
        StretcherRail(guards, "Defensa - vestibulo norte", false, 3.625f, 1f, 3.45f, 7.40f, 5.6f);
        StretcherRail(guards, "Defensa - vestibulo sul", false, 5.25f, -1f, 3.35f, 7.40f);
        StretcherRail(guards, "Defensa - vestibulo leste", true, 7.5f, -1f, 3.73f, 5.15f, 4.45f);
        // Corner guards: convex corners of the route and the jambs of the free passage into the vestibule.
        CornerGuard(guards, "Protetor - degrau do deposito", -4.5f + face, 0.875f - face, -1f, 1f);
        CornerGuard(guards, "Protetor - quina da autopsia", -2f - face, 3.625f + face, 1f, -1f);
        CornerGuard(guards, "Protetor - quina do descanso", -5.25f + face, -3.875f - face, -1f, 1f);
        CornerGuard(guards, "Protetor - passagem 1 (corredor)", 3.25f - face, 3.875f, 1f, -1f);
        CornerGuard(guards, "Protetor - passagem 1 (vestibulo)", 3.25f + face, 3.875f, -1f, -1f);
        CornerGuard(guards, "Protetor - passagem 2 (corredor)", 3.25f - face, 5.125f, 1f, 1f);
        CornerGuard(guards, "Protetor - passagem 2 (vestibulo)", 3.25f + face, 5.125f, -1f, 1f);

        // Technical installations: surface conduit from the technical room, junction boxes, a light switch.
        Material galv = Mat(CirculacaoMaterials, "Eletroduto galvanizado", new Color(0.55f, 0.57f, 0.57f), 0.55f, 0.7f);
        Material boxMat = Mat(CirculacaoMaterials, "Caixa de passagem", new Color(0.50f, 0.52f, 0.52f), 0.5f, 0.6f);
        Material white = Mat(CirculacaoMaterials, "Espelho de tomada", new Color(0.88f, 0.88f, 0.85f), 0.4f);
        float cz = 5.75f - face - 0.020f;
        Cyl("Eletroduto - trecho horizontal", tech, new Vector3(0.0f, 2.95f, cz), 0.025f, 5.2f, galv, new Vector3(0f, 0f, 90f));
        Cyl("Eletroduto - subida", tech, new Vector3(-2.6f, 3.08f, cz), 0.025f, 0.26f, galv);
        foreach (float x in new[] { -2.6f, 1.55f })
            Piece("Caixa de passagem", tech, x - 0.06f, 2.89f, cz - 0.035f, x + 0.06f, 3.01f, cz + 0.03f, boxMat);
        for (float x = -2.0f; x < 2.5f; x += 0.9f)
            Piece("Abracadeira", tech, x - 0.012f, 2.93f, cz - 0.014f, x + 0.012f, 2.97f, 5.75f - face, galv);
        Piece("Interruptor - circulacao", tech, -7.0f - 0.04f, 1.21f, -6f + face, -7.0f + 0.04f, 1.29f, -6f + face + 0.014f, white);
        Piece("Interruptor - servico", tech, -2.0f - 0.04f, 1.21f, 5.75f - face - 0.014f, -2.0f + 0.04f, 1.29f, 5.75f - face, white);

        // Local wear: wheels on the turns and at the doors, cart scuffs at the corners, hand marks by the latches.
        Material wheels = Mat(CirculacaoMaterials, "Decal - rodas de maca", new Color(1f, 1f, 1f, 0.9f), 0.25f, 0f, CirculacaoTextures + "Decal_RodasMaca.png", transparent: true);
        Material scuffs = Mat(CirculacaoMaterials, "Decal - riscos de carrinho", new Color(1f, 1f, 1f, 0.9f), 0.25f, 0f, CirculacaoTextures + "Decal_RiscosParede.png", transparent: true);
        Material hands = Mat(CirculacaoMaterials, "Decal - marca de mao", new Color(1f, 1f, 1f, 0.9f), 0.25f, 0f, CirculacaoTextures + "Decal_MarcaMao.png", transparent: true);
        Material dust = Mat(CirculacaoMaterials, "Decal - poeira de rodape", new Color(1f, 1f, 1f, 0.9f), 0.25f, 0f, CirculacaoTextures + "Decal_PoeiraRodape.png", transparent: true);
        Vector3 diag = new Vector3(0.8f, 0f, 0.6f).normalized;
        Decal("Rodas - trecho 3", wear, new Vector3(-3.4f, 0.005f, 1.9f), Vector3.up, Vector3.forward, Vector3.right, 2.0f, 1.0f, wheels);
        Decal("Rodas - curva do trecho 3", wear, new Vector3(-3.1f, 0.005f, 4.05f), Vector3.up, diag, new Vector3(diag.z, 0f, -diag.x), 2.0f, 1.0f, wheels);
        Decal("Rodas - corredor de servico", wear, new Vector3(1.9f, 0.005f, 4.55f), Vector3.up, Vector3.right, Vector3.forward, 2.0f, 1.0f, wheels);
        Decal("Rodas - porta da autopsia", wear, new Vector3(-3.25f, 0.005f, 0.05f), Vector3.up, Vector3.right, Vector3.forward, 1.9f, 0.95f, wheels);
        Decal("Rodas - vestibulo", wear, new Vector3(5.4f, 0.005f, 4.2f), Vector3.up, Vector3.forward, Vector3.right, 1.6f, 0.8f, wheels);
        WallDecal("Riscos - degrau do deposito", wear, new Vector3(-4.5f + face, 0.90f, 1.25f), east, 0.9f, 0.225f, scuffs);
        WallDecal("Riscos - quina da autopsia", wear, new Vector3(-2f - face, 0.90f, 3.05f), west, 0.9f, 0.225f, scuffs);
        WallDecal("Riscos - quina da autopsia (norte)", wear, new Vector3(-1.55f, 0.90f, 3.625f + face), north, 0.9f, 0.225f, scuffs);
        WallDecal("Riscos - passagem", wear, new Vector3(2.45f, 0.90f, 3.625f + face), north, 0.9f, 0.225f, scuffs);
        WallDecal("Riscos - descanso", wear, new Vector3(-5.25f + face, 0.90f, -3.2f), east, 0.9f, 0.225f, scuffs);
        WallDecal("Mao - porta da autopsia", wear, new Vector3(-2f - face, 1.05f, 0.98f), west, 0.20f, 0.40f, hands);
        WallDecal("Mao - porta da sala tecnica", wear, new Vector3(-2.52f, 1.05f, 5.75f - face), south, 0.20f, 0.40f, hands);
        WallDecal("Poeira - recuo", wear, new Vector3(-9.6f, 0.07f, -3.875f - face), south, 1.6f, 0.2f, dust);
        WallDecal("Poeira - corredor de servico", wear, new Vector3(-1.0f, 0.07f, 5.75f - face), south, 1.6f, 0.2f, dust);
    }

    // Corridor ceiling lights: colour temperature and level change leg by leg; one more fixture in the long runs.
    private static void CorridorLights(Transform ceilingRoot)
    {
        var lights = new (string room, float x, float z, float intensity, float kelvin, bool alongZ)[]
        {
            (CirculacaoRoom, -9.55f, -4.925f, 3.4f, 4000f, false),
            (CirculacaoRoom, -7.25f, -4.925f, 3.8f, 4000f, false),
            (CirculacaoRoom, -3.9f, -4.925f, 3.8f, 4100f, false),
            (CirculacaoRoom, -3.625f, -1.5f, 3.6f, 4500f, true),
            (CirculacaoRoom, -3.25f, 2.25f, 3.6f, 4500f, true),
            (CirculacaoRoom, -3.1f, 4.7f, 3.4f, 4800f, false),
            (CirculacaoRoom, -0.625f, 4.7f, 3.5f, 4800f, false),
            (CirculacaoRoom, 1.9f, 4.7f, 3.4f, 4800f, false),
            (VestibuleRoom, 5.375f, 4.45f, 3.3f, 5200f, false),
        };
        var count = new Dictionary<string, int>();
        foreach (var (room, x, z, intensity, kelvin, alongZ) in lights)
        {
            count[room] = count.TryGetValue(room, out int n) ? n + 1 : 1;
            CeilingFixture(Sub(ceilingRoot, "Luminarias - " + room), "Luz - " + room + " - luminaria " + count[room],
                "Luminaria " + count[room], x, z, 0, intensity, kelvin, false, alongZ, model: false);
        }
    }

    // ------------------------------------------------------------------ patching the saved map

    /// <summary>Rebuilds the corridor finishes, dressing and lights on the open scene's map without regenerating it:
    /// the generator's own passes run into a scratch group and only the corridor pieces are moved across.</summary>
    [MenuItem("Tools/Unclaimed/Circulacao/Update Corridors")]
    public static void UpdateCorridors()
    {
        GameObject morgue = FindExistingMorgue(SceneManager.GetActiveScene());
        if (morgue == null || morgue.name != GroupName || Application.isPlaying)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update corridors");

        Transform real = morgue.transform;
        var scratch = new GameObject("_corredores_temp");
        Undo.RegisterCreatedObjectUndo(scratch, "Scratch");
        scratch.transform.SetParent(morgue.transform.parent, false);
        root = scratch.transform;
        floorsGroup = Group("Pisos"); wallsGroup = Group("Paredes"); claddingGroup = Group("Revestimentos"); skirtingGroup = Group("Rodapes");
        doorsGroup = Group("Portas"); ceilingsGroup = Group("Tetos"); envelopeGroup = Group("Fechamento externo");
        roomsGroup = real.Find("Ambientes");
        lightsGroup = real.Find("Luzes");
        PrepareMaterials();
        corridorLegs = null;
        finishes = new Dictionary<string, ZoneFinish> { ["Autopsia"] = AutopsiaFinish() };
        finishes["Recepcao"] = RecepcaoFinish();
        finishes["WC"] = WCFinish();
        finishes["Arquivo e escritorio"] = ArquivoFinish();
        finishes[CirculacaoRoom] = CorridorFinish(0);
        finishes[VestibuleRoom] = CorridorFinish(2);
        finishes[DescansoRoom] = DescansoFinish();
        finishes["Deposito"] = DepositoFinish();
        finishes["Sala tecnica"] = TecnicaFinish();

        List<Zone> zones = Zones();
        grid = Rasterize(zones);
        doorUsed = new bool[Doors.Length];
        Floors(zones);
        Walls(zones);
        Doorways(zones);
        StopStripsAtCasings();
        UnifyWallFinishes();
        Ceilings(zones);

        // Swap the corridor pieces: cladding groups, skirting strips, new ceiling panels.
        foreach (string room in new[] { CirculacaoRoom, VestibuleRoom })
        {
            Transform oldClad = real.Find("Revestimentos/" + room);
            if (oldClad != null) Undo.DestroyObjectImmediate(oldClad.gameObject);
            Transform newClad = claddingGroup.Find(room);
            if (newClad != null) Undo.SetTransformParent(newClad, real.Find("Revestimentos"), "Corridor cladding");
            foreach (Transform strip in Children(real.Find("Rodapes")))
                if (strip.name == "Rodape - " + room) Undo.DestroyObjectImmediate(strip.gameObject);
            foreach (Transform strip in Children(skirtingGroup))
                if (strip.name == "Rodape - " + room) Undo.SetTransformParent(strip, real.Find("Rodapes"), "Corridor skirting");
            foreach (Transform old in Children(real.Find("Tetos")))
                if (old.name.StartsWith("Forro - " + room)) Undo.DestroyObjectImmediate(old.gameObject);
            foreach (Transform panel in Children(ceilingsGroup))
                if (panel.name.StartsWith("Forro - " + room)) Undo.SetTransformParent(panel, real.Find("Tetos"), "Corridor ceiling");
        }
        Undo.DestroyObjectImmediate(scratch);

        // Dressing and lights, from the real groups.
        root = real;
        ceilingsGroup = real.Find("Tetos");
        claddingGroup = real.Find("Revestimentos");
        skirtingGroup = real.Find("Rodapes");
        doorsGroup = real.Find("Portas");
        Transform oldRoom = roomsGroup.Find(CirculacaoRoom);
        if (oldRoom != null) Undo.DestroyObjectImmediate(oldRoom.gameObject);
        BuildCirculacao(Sub(roomsGroup, CirculacaoRoom));
        foreach (string room in new[] { CirculacaoRoom, VestibuleRoom })
        {
            Transform oldLights = ceilingsGroup.Find("Luminarias - " + room);
            if (oldLights != null) Undo.DestroyObjectImmediate(oldLights.gameObject);
            foreach (Transform light in Children(lightsGroup))
                if (light.name.StartsWith("Luz - " + room + " - luminaria")) Undo.DestroyObjectImmediate(light.gameObject);
        }
        CorridorLights(ceilingsGroup);
        PrepareStaticLighting(zones);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(real.gameObject.scene);
        Debug.Log("Unclaimed: corridors updated. Re-bake the lighting (Tools > Unclaimed > Bake Prototype Lighting), then save.");
    }

    private static List<Transform> Children(Transform t)
    {
        var list = new List<Transform>();
        if (t != null) foreach (Transform c in t) list.Add(c);
        return list;
    }
}
