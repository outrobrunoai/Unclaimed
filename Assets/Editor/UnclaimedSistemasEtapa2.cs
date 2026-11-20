// Places José's stage-2 (investigation) systems in the morgue map: photo targets, the power panel, the
// circuit consumers (lights, diffusers, emergency lights, blackout volume) and the spare fuse.
// Idempotent: running it again updates the same objects instead of duplicating them.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class UnclaimedSistemasEtapa2
{
    const string Raiz = "Unclaimed - Necroterio (planta)";
    const string Pasta = "Assets/UnclaimedPrototype/Sistemas";
    const string GrupoEnergia = "Sistemas - energia";

    [MenuItem("Tools/Unclaimed/Sistemas/Colocar etapa 2 (investigacao)")]
    public static void Colocar()
    {
        GameObject g = GameObject.Find(Raiz);
        if (g == null) { Debug.LogWarning($"Unclaimed: '{Raiz}' not found in the active scene."); return; }
        Transform raiz = g.transform;
        Undo.SetCurrentGroupName("Place stage 2 systems");

        AlvosFotograficos(raiz);
        Painel(raiz);
        AutopsiaSemLuz(raiz);
        Energia(raiz);
        CamadaDeLuzDaAutopsia(raiz);   // after Energia, which (re)builds the emergency lights
        Fusivel(raiz);
        SalasDaMesa(raiz);

        EditorSceneManager.MarkSceneDirty(g.scene);
        Debug.Log("Unclaimed: stage 2 systems placed.");
    }

    // ------------------------------------------------------------------ photo targets (F01, F02)

    static void AlvosFotograficos(Transform raiz)
    {
        // F01: the person on the autopsy table. The target lives on the "occupied" object, so it disappears with it (P08).
        Transform ocupado = Achar(raiz, "Ambientes/Autopsia/Mobiliario/Estado da mesa de autopsia/Corpo - autopsia");
        var mesa = Garantir<AlvoFotografico>(ocupado.gameObject);
        // P02: the photo only counts with the exam light on (the general light leaves the table in shadow).
        mesa.requerCircuito = RedeEletrica.Exame;
        // Pose A at the start; Daniel switches to pose B (left arm moved) when the player leaves the room.
        Transform poseA = ocupado.Find("Pose A"), poseB = ocupado.Find("Pose B");
        if (poseA != null && poseB != null) mesa.poses = new[] { poseA.gameObject, poseB.gameObject };
        else Debug.LogWarning("Unclaimed: run Tools > Unclaimed > Autopsia > Add Body Pose B for the table's pose B.");
        mesa.id = "mesa_autopsia";
        mesa.idEvidencia = "F01";
        mesa.legenda = "A pessoa na mesa de autópsia";
        mesa.centro = Ponto(ocupado, "Centro da foto", new Vector3(2.30f, 1.08f, -0.45f));

        // F02: the body in drawer C-02 (Leo, with the splint). On the tray, so the tray collider does not block the photo
        // and the closed door does.
        Transform maca = Achar(raiz, "Ambientes/Camara fria/Camara fria - equipamentos/CamaraMortuaria/C-02/Maca");
        var c02 = Garantir<AlvoFotografico>(maca.gameObject);
        c02.id = "gaveta_c02";
        c02.idEvidencia = "F02";
        c02.legenda = "O corpo na gaveta C-02, com a tala";
        // The middle of the body stays inside the cabinet even with the tray out, so the point sits on the part that
        // slides out (0.55 m toward the opening, measured with the drawer closed).
        Transform corpo = maca.Find("Ocupante/Corpo");
        var peca = maca.GetComponent<PecaDeMovel>();
        Vector3 abre = peca != null ? maca.parent.TransformDirection(peca.eixo).normalized * Mathf.Sign(peca.curso) : Vector3.zero;
        c02.centro = Ponto(maca, "Centro da foto", Centro(corpo) + abre * 0.55f + Vector3.up * 0.05f);
        c02.distanciaMaxima = 4f;
    }

    // ------------------------------------------------------------------ power panel (D04, P02, S1)

    static void Painel(Transform raiz)
    {
        // The contingency panel model is the interface: aim at a selector and press E (no screen overlay).
        Transform equipamentos = Achar(raiz, "Ambientes/Sala tecnica/Equipamentos");
        Transform quadro = Achar(equipamentos, "Quadro geral");
        var antigo = quadro.GetComponent<PainelDeEnergia>();
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);
        var encaixe = Garantir<EncaixeDeFusivel>(quadro.gameObject);
        encaixe.circuito = RedeEletrica.Geral;
        encaixe.itemFusivel = "fusivel_reserva";

        Transform pc = Achar(equipamentos, "Painel de contingencia");
        var painel = Garantir<PainelDeContingencia>(pc.gameObject);
        const string mats = "Assets/Modelos/Tecnica/Materiais/";
        painel.lenteAcesa = Mat(mats + "Lente - verde acesa.mat");
        painel.lenteApagada = Mat(mats + "Lente - verde apagada.mat");
        painel.anguloLigado = 315f; painel.anguloDesligado = 45f;
        var colunas = new List<PainelDeContingencia.Coluna>();
        foreach (var (circuito, nome) in new[] { (RedeEletrica.Refrigeracao, "refrigeracao"), (RedeEletrica.Geral, "luz_geral"),
                                                 (RedeEletrica.Exame, "exame") })
        {
            Transform seletor = Achar(pc, "Seletor - " + nome);
            Transform lente = Achar(pc, $"Sinaleiro - {nome}/Sinaleiro - {nome} - lente");
            colunas.Add(new PainelDeContingencia.Coluna { circuito = circuito, seletor = seletor, lente = lente.GetComponent<Renderer>() });
            Alvo(seletor);
            var chave = Garantir<ChaveDoPainel>(seletor.gameObject);
            chave.painel = painel; chave.circuito = circuito;
        }
        painel.colunas = colunas.ToArray();
        painel.visor = Achar(pc, "Visor - contingencia").GetComponent<Renderer>();
        painel.leituras = new[] { 6, 9, 10, 13 }.Select(total => new PainelDeContingencia.Leitura
        {
            total = total, material = UnclaimedTecnicaAssets.DisplayMaterial($"Visor_Contingencia_{total:00}.png")
        }).ToArray();
        painel.idEvidencia = "D04";
        AssetDatabase.SaveAssets();

        Transform cartao = Achar(pc, "Etiquetas/Etiqueta - pc_d04");
        Alvo(cartao);
        Garantir<CartaoDoPainel>(cartao.gameObject).idEvidencia = "D04";
        foreach (var c in new Component[] { painel, encaixe, cartao.GetComponent<CartaoDoPainel>() }) EditorUtility.SetDirty(c);
        painel.Atualizar();
    }

    // Aim target for a small part on the panel face: its mesh bounds grown 1 cm, so the ray hits it before the cabinet.
    static void Alvo(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var box = Garantir<BoxCollider>(t.gameObject);
        Bounds b = mf.sharedMesh.bounds;
        box.center = b.center;
        box.size = b.size + Vector3.one * 0.02f;
    }

    // ------------------------------------------------------------------ autopsy without ceiling light (P02)

    // The rain got into the autopsy ceiling and burnt its fixtures: the room only has its emergency block and the
    // exam light, so the player has to solve the panel to see the table (and to take F01).
    const string LuzesDaAutopsia = "Luz - autopsia - luminaria";
    static readonly Rect AreaDaAutopsia = Rect.MinMaxRect(-2f, -3.5f, 7.5f, 3.625f);   // UnclaimedMorgueGenerator.Zones

    static bool NaAutopsia(Vector3 p) => AreaDaAutopsia.Contains(new Vector2(p.x, p.z));

    static void AutopsiaSemLuz(Transform raiz)
    {
        foreach (Light l in Achar(raiz, "Luzes").GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith(LuzesDaAutopsia)))
        {
            var flicker = l.GetComponent<FlickerFluorescente>();
            if (flicker != null) Undo.DestroyObjectImmediate(flicker);
            Undo.RecordObject(l.gameObject, "Burnt light");
            l.gameObject.SetActive(false);
        }
        Material aceso = Mat("Assets/UnclaimedPrototype/Autopsia/Difusor aceso.mat");
        Material gasto = Mat("Assets/UnclaimedPrototype/Autopsia/Difusor gasto.mat");
        Material apagado = Mat("Assets/UnclaimedPrototype/Autopsia/Difusor apagado.mat");
        Material prismaAceso = Mat("Assets/Modelos/Recepcao/Materiais/Difusor prismatico aceso.mat");
        Material prismaApagado = Mat("Assets/Modelos/Recepcao/Materiais/Difusor prismatico apagado.mat");
        foreach (Transform fixture in Achar(raiz, "Tetos/Luminarias - Autopsia"))
        {
            if (!fixture.name.StartsWith("Luminaria ") || fixture.name == "Luminaria de exame") continue;
            foreach (var r in fixture.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == aceso || mats[i] == gasto) mats[i] = apagado;
                    else if (mats[i] == prismaAceso) mats[i] = prismaApagado;
                }
                Undo.RecordObject(r, "Burnt diffuser"); r.sharedMaterials = mats;
            }
        }
    }

    // The neighbouring rooms' lights are realtime spots without shadows (range 8 m): their light crosses the walls
    // and showed in the dark autopsy's corners. The autopsy's own surfaces and objects go to rendering layer 1
    // only; every other light keeps the Default layer, so it no longer reaches them. The lights that belong in
    // the room (exam light, its emergency block; the phone flashlight in Celular) light both layers. Doors stay on
    // Default (seen from both sides). Baked lighting is not affected (the lightmapper ignores rendering layers).
    const uint CamadaPadrao = 1u, CamadaAutopsia = 1u << 1;

    static bool DaAutopsia(Transform t, Transform raiz)
    {
        bool da = false;
        for (; t != null && t != raiz; t = t.parent)
        {
            string n = t.name;
            if (n.Contains(" / ") || n.StartsWith("Parede")) return false;   // shared wall cores and exterior walls
            if (n == "Autopsia" || n.EndsWith(" - Autopsia") || n.EndsWith("(autopsia)")) da = true;
        }
        return da;
    }

    static void CamadaDeLuzDaAutopsia(Transform raiz)
    {
        int n = 0;
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (!DaAutopsia(r.transform, raiz)) continue;
            Undo.RecordObject(r, "Autopsy light layer");
            r.renderingLayerMask = CamadaAutopsia;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            n++;
        }
        var daSala = new List<Light>(Object.FindObjectsByType<LuminariaExame>(FindObjectsInactive.Include)
            .Where(l => l.transform.IsChildOf(raiz)).SelectMany(l => l.GetComponentsInChildren<Light>(true)));
        daSala.AddRange(raiz.GetComponentsInChildren<Light>(true).Where(l => l.name == "Luz de emergencia" && NaAutopsia(l.transform.position)));
        foreach (var l in daSala)
        {
            Undo.RecordObject(l, "Autopsy light layer");
            l.renderingLayerMask = (int)(CamadaPadrao | CamadaAutopsia);
            // URP adds this component at runtime with layer 1 only when it is missing, so create it here.
            var urp = l.GetComponent<UniversalAdditionalLightData>();
            if (urp == null) urp = Undo.AddComponent<UniversalAdditionalLightData>(l.gameObject);
            if (urp != null)
            {
                Undo.RecordObject(urp, "Autopsy light layer");
                urp.renderingLayers = CamadaPadrao | CamadaAutopsia;
                EditorUtility.SetDirty(urp);
                PrefabUtility.RecordPrefabInstancePropertyModifications(urp);
            }
            EditorUtility.SetDirty(l);
            PrefabUtility.RecordPrefabInstancePropertyModifications(l);
        }
        // The room's emergency block is only a faint pool by the west door: the table stays unreadable without the exam light.
        foreach (var l in daSala.Where(l => l.name == "Luz de emergencia")) { l.intensity = 1.2f; l.range = 4.5f; }
        Debug.Log($"Unclaimed: {n} autopsy renderers on light layer 1; {daSala.Count} lights reach them.");
    }

    // ------------------------------------------------------------------ circuits

    static void Energia(Transform raiz)
    {
        Transform grupo = Grupo(raiz, GrupoEnergia);

        // General light: every room light (the exterior lighting is street power and stays on).
        Transform luzes = Achar(raiz, "Luzes");
        Transform externa = luzes.Find("Iluminacao externa");
        var geral = Garantir<ConsumidorDeEnergia>(Grupo(grupo, "Luz geral").gameObject);
        geral.circuito = RedeEletrica.Geral;
        geral.inverter = false;
        geral.luzes = luzes.GetComponentsInChildren<Light>(true)
            .Where(l => (externa == null || !l.transform.IsChildOf(externa)) && !l.name.StartsWith(LuzesDaAutopsia)).ToArray();

        // Exam light: the autopsy lamp, off at the start of the night.
        var exame = Garantir<ConsumidorDeEnergia>(Grupo(grupo, "Luz de exame").gameObject);
        exame.circuito = RedeEletrica.Exame;
        exame.inverter = false;
        exame.luzes = new Light[0];
        exame.luminarias = Object.FindObjectsByType<LuminariaExame>(FindObjectsInactive.Include)
            .Where(l => l.transform.IsChildOf(raiz)).ToArray();   // LuminariaExame.Ligar drives its own light and lens

        // Diffusers: lit material -> off material while the general light is out.
        Material aceso = Mat("Assets/UnclaimedPrototype/Autopsia/Difusor aceso.mat");
        Material gasto = Mat("Assets/UnclaimedPrototype/Autopsia/Difusor gasto.mat");
        Material apagado = Mat("Assets/UnclaimedPrototype/Autopsia/Difusor apagado.mat");
        Material prismaAceso = Mat("Assets/Modelos/Recepcao/Materiais/Difusor prismatico aceso.mat");
        Material prismaApagado = Mat("Assets/Modelos/Recepcao/Materiais/Difusor prismatico apagado.mat");
        foreach (Transform t in Achar(raiz, "Tetos"))
        {
            if (!t.name.StartsWith("Luminarias - ")) continue;
            var troca = Garantir<MaterialDoCircuito>(t.gameObject);
            troca.circuito = RedeEletrica.Geral;
            troca.inverter = false;
            troca.renderers = new Renderer[0];
            troca.trocas = new[]
            {
                new MaterialDoCircuito.Troca { ligado = aceso, desligado = apagado },
                new MaterialDoCircuito.Troca { ligado = gasto, desligado = apagado },
                new MaterialDoCircuito.Troca { ligado = prismaAceso, desligado = prismaApagado },
            };
        }

        // Emergency lights: one small downlight under each emergency block, lit only during a blackout.
        // The autopsy's own block stays lit all night: its ceiling lights are out (AutopsiaSemLuz).
        var emergencia = new List<Light>();
        foreach (Transform bloco in raiz.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Bloco de emergencia")).ToArray())
        {
            Light l = LuzDeEmergencia(bloco);
            if (NaAutopsia(l.transform.position)) l.enabled = true;
            else emergencia.Add(l);
        }
        var inv = Garantir<ConsumidorDeEnergia>(Grupo(grupo, "Emergencia").gameObject);
        inv.circuito = RedeEletrica.Geral;
        inv.inverter = true;
        inv.luzes = emergencia.ToArray();
        inv.objetos = new[] { VolumeDoApagao(grupo) };
    }

    static Light LuzDeEmergencia(Transform bloco)
    {
        Transform t = bloco.Find("Luz de emergencia");
        if (t == null)
        {
            t = new GameObject("Luz de emergencia").transform;
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Emergency light");
            t.SetParent(bloco, false);
        }
        // Wall-mounted block: aim down and away from the wall, so it lights the floor instead of a hot spot on the wall.
        Bounds b = Limites(bloco);
        Vector3 longe = Vector3.zero;
        Vector3 lado = b.size.x > b.size.z ? Vector3.forward : Vector3.right;   // the wall runs along the long side
        foreach (Vector3 d in new[] { lado, -lado })
            if (Physics.Raycast(b.center, d, out RaycastHit h, 0.5f, ~0, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(bloco))
            { longe = -d; break; }
        t.position = new Vector3(b.center.x, b.min.y - 0.02f, b.center.z) + longe * 0.05f;
        t.rotation = Quaternion.LookRotation((Vector3.down + longe * 0.7f).normalized, longe == Vector3.zero ? Vector3.forward : longe);
        var l = Garantir<Light>(t.gameObject);
        l.type = LightType.Spot;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        l.color = new Color(1f, 0.96f, 0.9f);
        l.intensity = 5f;
        l.range = 7f;
        l.spotAngle = 120f;
        l.innerSpotAngle = 40f;
        l.shadows = LightShadows.None;
        l.enabled = false;   // the consumer turns it on in play only when the general light is out
        return l;
    }

    static GameObject VolumeDoApagao(Transform grupo)
    {
        string caminho = Pasta + "/Apagao.asset";
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(caminho);
        if (perfil == null)
        {
            System.IO.Directory.CreateDirectory(Pasta);
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, caminho);
        }
        // The baked indirect light stays in the lightmaps when the lights go out; this darkens what is left. Mostly
        // shadows and midtones, so what a real light still reaches (exam lamp, emergency lights) keeps its brightness.
        if (!perfil.TryGet(out ColorAdjustments ajuste)) ajuste = perfil.Add<ColorAdjustments>(true);
        ajuste.postExposure.Override(-1f);
        ajuste.saturation.Override(-20f);
        if (!perfil.TryGet(out ShadowsMidtonesHighlights tons)) tons = perfil.Add<ShadowsMidtonesHighlights>(true);
        tons.shadows.Override(new Vector4(1f, 1f, 1f, -0.7f));
        tons.midtones.Override(new Vector4(1f, 1f, 1f, -0.55f));
        tons.highlights.Override(new Vector4(1f, 1f, 1f, 0f));
        tons.shadowsStart.Override(0f);
        tons.shadowsEnd.Override(0.3f);
        tons.highlightsStart.Override(0.55f);
        tons.highlightsEnd.Override(1f);
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();

        Transform t = Grupo(grupo, "Volume do apagao");
        var v = Garantir<Volume>(t.gameObject);
        v.isGlobal = true;
        v.priority = 50f;
        v.weight = 1f;
        v.sharedProfile = perfil;
        t.gameObject.SetActive(false);
        return t.gameObject;
    }

    // ------------------------------------------------------------------ spare fuse (S1)

    static void Fusivel(Transform raiz)
    {
        // In the maintenance shelf's "electrical" bin (Estante L1, bin 1).
        Transform estante = Achar(raiz, "Ambientes/Deposito/Estante L1");
        Transform bin = estante.Find("Estante L1 - bin 1");
        Transform f = estante.Find("Fusivel reserva");
        if (f == null)
        {
            f = new GameObject("Fusivel reserva").transform;
            Undo.RegisterCreatedObjectUndo(f.gameObject, "Spare fuse");
            f.SetParent(estante, false);
        }
        Bounds b = Limites(bin);
        f.position = new Vector3(b.center.x - 0.02f, b.min.y + 0.026f, b.center.z);   // lying on the bin floor
        f.rotation = Quaternion.Euler(0f, 20f, 90f);   // lying on its side

        Material porcelana = MatNovo("Fusivel - porcelana", new Color(0.92f, 0.91f, 0.87f), 0.55f, 0f);
        Material latao = MatNovo("Fusivel - latao", new Color(0.78f, 0.62f, 0.32f), 0.6f, 1f);
        Material indicador = MatNovo("Fusivel - indicador", new Color(0.75f, 0.12f, 0.08f), 0.4f, 0f);
        Peca(f, "Corpo", new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.019f, 0.022f), porcelana);
        Peca(f, "Contato", new Vector3(0f, -0.022f, 0f), new Vector3(0.012f, 0.004f, 0.012f), latao);
        Peca(f, "Tampa", new Vector3(0f, 0.021f, 0f), new Vector3(0.016f, 0.003f, 0.016f), latao);
        Peca(f, "Indicador", new Vector3(0f, 0.0245f, 0f), new Vector3(0.007f, 0.0015f, 0.007f), indicador);

        var col = Garantir<BoxCollider>(f.gameObject);
        col.size = new Vector3(0.06f, 0.07f, 0.06f);   // larger than the fuse so it is easy to aim at
        col.center = Vector3.zero;

        var item = Garantir<ItemColetavel>(f.gameObject);
        item.id = "fusivel_reserva";
        item.textoAcao = "Pegar fusível reserva";
        item.aviso = "Fusível reserva guardado.";
    }

    static void Peca(Transform pai, string nome, Vector3 local, Vector3 escala, Material m)
    {
        Transform t = pai.Find(nome);
        if (t == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = nome;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(go, "Fuse part");
            t = go.transform;
            t.SetParent(pai, false);
        }
        t.localPosition = local;
        t.localRotation = Quaternion.identity;
        t.localScale = escala;
        var r = t.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    // ------------------------------------------------------------------ room volumes around the table (stage 4 seed)

    // DiretorDeDaniel only touches the table (pose B, P08) while the autopsy is out of the player's sight, judged by
    // Sala volumes. Without them he believes the room is never seen and acts in front of the player. These three
    // cover the table: the autopsy, the cold room and the corridor leg at the autopsy door; the rest of the list
    // comes with stage 4. Rectangles from UnclaimedMorgueGenerator.Zones (floor to ceiling).
    static void SalasDaMesa(Transform raiz)
    {
        Transform grupo = Grupo(raiz, "Sistemas - salas");
        FolhaDePorta[] Folhas(string porta) => Object.FindObjectsByType<FolhaDePorta>(FindObjectsInactive.Include)
            .Where(f => f.transform.IsChildOf(raiz) && f.GetComponentsInParent<Transform>(true).Any(t => t.name == porta)).ToArray();
        var circulacao = Folhas("Porta autopsia - circulacao");
        var camara = Folhas("Porta autopsia - camara fria");
        Sala.Vizinha[] Por(string sala, FolhaDePorta[] folhas) =>
            folhas.Select(f => new Sala.Vizinha { sala = sala, porta = f }).ToArray();   // one entry per leaf: either open counts

        SalaVolume(grupo, "Autopsia", -2f, -3.5f, 7.5f, 3.625f, Por("Corredor", circulacao).Concat(Por("CamaraFria", camara)).ToArray());
        SalaVolume(grupo, "CamaraFria", 7.5f, -3.5f, 15f, 5.25f, Por("Autopsia", camara));
        SalaVolume(grupo, "Corredor", -4.5f, -3.875f, -2f, 3.625f, Por("Autopsia", circulacao));
        if (circulacao.Length == 0 || camara.Length == 0) Debug.LogWarning("Unclaimed: autopsy door leaves not found for the Sala volumes.");
    }

    static void SalaVolume(Transform grupo, string id, float x0, float z0, float x1, float z1, Sala.Vizinha[] vizinhas)
    {
        Transform t = Grupo(grupo, "Sala - " + id);
        t.gameObject.layer = 2;   // Ignore Raycast: aiming and photos pass through
        t.position = new Vector3((x0 + x1) / 2f, 1.6f, (z0 + z1) / 2f);
        var box = Garantir<BoxCollider>(t.gameObject);
        box.isTrigger = true; box.center = Vector3.zero; box.size = new Vector3(x1 - x0, 3.2f, z1 - z0);
        var sala = Garantir<Sala>(t.gameObject);
        sala.id = id; sala.vizinhas = vizinhas;
        EditorUtility.SetDirty(sala);
    }

    // ------------------------------------------------------------------ helpers

    static Transform Achar(Transform raiz, string caminho)
    {
        Transform t = raiz.Find(caminho);
        if (t == null) throw new System.InvalidOperationException($"Unclaimed: '{caminho}' not found under '{raiz.name}'.");
        return t;
    }

    static Transform Grupo(Transform pai, string nome)
    {
        Transform t = pai.Find(nome);
        if (t != null) return t;
        t = new GameObject(nome).transform;
        Undo.RegisterCreatedObjectUndo(t.gameObject, "Create group");
        t.SetParent(pai, false);
        return t;
    }

    static Transform Ponto(Transform pai, string nome, Vector3 mundo)
    {
        Transform t = Grupo(pai, nome);
        t.position = mundo;
        return t;
    }

    static T Garantir<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 Centro(Transform t) => Limites(t).center;

    static Material Mat(string caminho)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) Debug.LogWarning($"Unclaimed: material '{caminho}' not found.");
        return m;
    }

    static Material MatNovo(string nome, Color cor, float lisura, float metal)
    {
        string dir = Pasta + "/Materiais";
        string caminho = $"{dir}/{nome}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null)
        {
            System.IO.Directory.CreateDirectory(dir);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, caminho);
        }
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", lisura);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }
}
