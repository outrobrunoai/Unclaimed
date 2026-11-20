using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Liga os sons do jogo sem mexer em prefab nem em cena: a cada cena carregada, preenche os campos de som
/// que ainda estão VAZIOS nos componentes do mapa (portas, móveis, cadeado, torneira, painel, celular, Daniel...)
/// e cria a fonte de áudio 3D de cada um. Som posto à mão no Inspector tem prioridade.
///
/// Os sons ficam em Resources/Sons/&lt;pasta&gt;/&lt;slot&gt;_NN.wav (gerados por Blender/Sistemas/Sons/montar_sons.py).
/// Cada slot pode ter várias variações (_01, _02...): uma é sorteada por objeto, para duas portas iguais lado
/// a lado não soarem idênticas. Para trocar um som, substitua o .wav; para variar mais, acrescente outro número.
///
/// Se instala sozinho no Play (não precisa estar em nenhuma cena). Os créditos (todos CC0) ficam em
/// Resources/Sons/creditos.json.
/// </summary>
public class SonsDoMapa : MonoBehaviour
{
    public static SonsDoMapa Instancia { get; private set; }

    [Header("Volumes")]
    [Range(0f, 1f)] public float volumePortas = 0.85f;
    [Range(0f, 1f)] public float volumeMoveis = 0.75f;
    [Range(0f, 1f)] public float volumeObjetos = 0.8f;
    [Range(0f, 1f)] public float volumeZumbido = 0.18f;
    [Range(0f, 1f)] public float volumeCompressor = 0.35f;
    [Range(0f, 1f)] public float volumeApagao = 0.9f;
    [Tooltip("Luzes do circuito geral que ganham zumbido de reator (as mais próximas umas das outras são puladas).")]
    [Min(0)] public int maximoDeZumbidos = 24;

    [Header("Fonte 3D")]
    [Min(0.1f)] public float distanciaMinima = 1.2f;
    [Min(1f)] public float distanciaMaxima = 18f;
    [Range(0f, 0.2f)] public float variacaoDeTom = 0.05f;

    /// <summary>Quantos componentes já receberam algum som desde o Play (diagnóstico e testes).</summary>
    public int Preenchidos { get; private set; }

    static readonly Dictionary<string, AudioClip[]> catalogo = new Dictionary<string, AudioClip[]>(StringComparer.OrdinalIgnoreCase);
    bool geralLigado = true, primeiraLeitura = true;
    bool? refrigeracaoLigada;
    readonly List<AudioSource> compressores = new List<AudioSource>();
    AudioSource fonteGlobal;

    // ------------------------------------------------------------------ instalação

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Instalar()
    {
        if (Instancia != null || FindAnyObjectByType<SonsDoMapa>() != null) return;
        var go = new GameObject("Sons do mapa");
        DontDestroyOnLoad(go);
        go.AddComponent<SonsDoMapa>();
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
        CarregarCatalogo();
        fonteGlobal = gameObject.AddComponent<AudioSource>();
        fonteGlobal.playOnAwake = false;
        fonteGlobal.spatialBlend = 0f;
    }

    void OnEnable() => SceneManager.sceneLoaded += AoCarregar;
    void OnDisable() => SceneManager.sceneLoaded -= AoCarregar;
    void OnDestroy() { if (Instancia == this) Instancia = null; }
    void Start() => Aplicar();
    void AoCarregar(Scene cena, LoadSceneMode modo) => Aplicar();

    static void CarregarCatalogo()
    {
        if (catalogo.Count > 0) return;
        var grupos = new Dictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);
        foreach (AudioClip c in Resources.LoadAll<AudioClip>("Sons"))
        {
            string slot = Regex.Replace(c.name, @"_\d+$", "");
            if (!grupos.TryGetValue(slot, out var l)) grupos[slot] = l = new List<AudioClip>();
            l.Add(c);
        }
        foreach (var g in grupos)
        {
            g.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            catalogo[g.Key] = g.Value.ToArray();
        }
    }

    /// <summary>Todas as variações de um slot (ex.: "tecnica_fechou"); vazio se não houver.</summary>
    public static AudioClip[] Variacoes(string slot)
    {
        CarregarCatalogo();
        return catalogo.TryGetValue(slot, out var v) ? v : Array.Empty<AudioClip>();
    }

    /// <summary>Uma variação sorteada do slot (null se não houver).</summary>
    public static AudioClip Um(string slot)
    {
        var v = Variacoes(slot);
        return v.Length == 0 ? null : v[UnityEngine.Random.Range(0, v.Length)];
    }

    // ------------------------------------------------------------------ energia: apagão, zumbido, compressor

    void Update()
    {
        bool ligado = RedeEletrica.Ligado(RedeEletrica.Geral);
        if (!primeiraLeitura && geralLigado && !ligado)
        {
            AudioClip c = Um("apagao");
            if (c != null) fonteGlobal.PlayOneShot(c, volumeApagao);
        }
        geralLigado = ligado;
        primeiraLeitura = false;

        bool frio = RedeEletrica.Ligado(RedeEletrica.Refrigeracao);
        if (refrigeracaoLigada != frio)
        {
            refrigeracaoLigada = frio;
            foreach (AudioSource a in compressores)
                if (a != null) { if (frio && !a.isPlaying) a.Play(); else if (!frio) a.Stop(); }
        }
    }

    // ------------------------------------------------------------------ aplicação

    /// <summary>Preenche os sons de tudo que está carregado (pode ser chamado de novo: só mexe em campos vazios).</summary>
    public void Aplicar()
    {
        foreach (FolhaDePorta p in Todos<FolhaDePorta>())
        {
            string tipo = TipoDePorta(p.transform);
            bool mudou = Preencher(ref p.somAbrir, tipo + "_abrir") | Preencher(ref p.somFechar, tipo + "_fechar") | Preencher(ref p.somFechou, tipo + "_fechou")
                       | Preencher(ref p.somDestrancar, "destrancar") | Preencher(ref p.somTrancada, "trancada");
            if (p.fonte == null && (p.somAbrir != null || p.somFechar != null || p.somFechou != null)) p.fonte = Fonte(p.gameObject, volumePortas);
            Contar(mudou);
        }

        foreach (PecaDeMovel m in Todos<PecaDeMovel>())
        {
            bool mudou;
            if (Tem(m.transform, "CamaraMortuaria"))
                mudou = m.tipo == PecaDeMovel.TipoDePeca.Porta
                    ? Preencher(ref m.somAbrir, "camara_porta_abrir") | Preencher(ref m.somFechou, "camara_porta_fechou")
                    : Preencher(ref m.somAbrir, "maca_abrir") | Preencher(ref m.somFechar, "maca_fechar") | Preencher(ref m.somFechou, "maca_fechou");
            else if (EArquivoDeAco(m.transform))
                mudou = Preencher(ref m.somAbrir, "arquivo_abrir") | Preencher(ref m.somFechar, "arquivo_fechar");
            else if (Tem(m.transform, "Armario de pertences"))
                mudou = Preencher(ref m.somAbrir, "locker_abrir") | Preencher(ref m.somFechou, "locker_fechou");
            else if (m.tipo == PecaDeMovel.TipoDePeca.Porta)   // roupeiro, armário de apoio e outras portinhas de madeira
                mudou = Preencher(ref m.somAbrir, "madeira_abrir") | Preencher(ref m.somFechou, "madeira_fechou");
            else                                               // gavetas de mesa (recepção, mesa do servidor...)
                mudou = Preencher(ref m.somAbrir, "gaveta_mesa_abrir") | Preencher(ref m.somFechar, "gaveta_mesa_fechar");
            if (m.fonte == null) m.fonte = Fonte(m.gameObject, volumeMoveis);
            Contar(mudou);
        }

        foreach (FerrolhoDePorta f in Todos<FerrolhoDePorta>())
        {
            Contar(Preencher(ref f.somAbrir, "ferrolho"));
            if (f.fonte == null) f.fonte = Fonte(f.gameObject, volumePortas);
        }

        foreach (CadeadoCombinacao c in Todos<CadeadoCombinacao>())
        {
            Contar(Preencher(ref c.somDisco, "cadeado_disco") | Preencher(ref c.somErro, "cadeado_erro") | Preencher(ref c.somAbrir, "cadeado_abrir"));
            if (c.fonte == null) c.fonte = Fonte(c.gameObject, volumeObjetos, 6f);
        }

        foreach (Torneira t in Todos<Torneira>())
        {
            Contar(Preencher(ref t.somBotao, "registro") | Preencher(ref t.somAgua, "agua_loop"));
            if (t.fonte == null) t.fonte = Fonte(t.gameObject, volumeObjetos, 8f);
        }

        foreach (Descarga d in Todos<Descarga>())
        {
            Contar(Preencher(ref d.somDescarga, "descarga"));
            if (d.fonte == null) d.fonte = Fonte(d.gameObject, volumeObjetos, 12f);
        }

        foreach (ItemColetavel i in Todos<ItemColetavel>())
            Contar(Preencher(ref i.somColeta, i.name.IndexOf("chave", StringComparison.OrdinalIgnoreCase) >= 0 ? "pegar_chave" : "pegar_objeto"));

        foreach (EncaixeDeFusivel e in Todos<EncaixeDeFusivel>())
            Contar(Preencher(ref e.somEncaixe, "encaixe"));

        foreach (PainelDeEnergia p in Todos<PainelDeEnergia>())
        {
            Contar(Preencher(ref p.somDisjuntor, "disjuntor") | Preencher(ref p.somRecusado, "desarme"));
            if (p.fonte == null) p.fonte = Fonte(p.gameObject, volumeObjetos, 10f);
        }

        foreach (PainelDeContingencia p in Todos<PainelDeContingencia>())
        {
            Contar(Preencher(ref p.somChave, "chave") | Preencher(ref p.somRecusado, "recusado"));
            if (p.fonte == null) p.fonte = Fonte(p.gameObject, volumeObjetos, 10f);
        }

        foreach (Celular c in Todos<Celular>())
            Contar(Preencher(ref c.somMensagem, "mensagem") | Preencher(ref c.somEnviado, "enviado") | Preencher(ref c.somPuxar, "puxar")
                 | Preencher(ref c.somGuardar, "guardar") | Preencher(ref c.somLanterna, "lanterna"));

        foreach (CameraFotografica c in Todos<CameraFotografica>())
            Contar(Preencher(ref c.somDisparo, "obturador"));

        // Daniel e perseguição.
        foreach (PontoDeAcao p in Todos<PontoDeAcao>())
        {
            string slot = SomDaAcao(p);
            if (slot == null) continue;
            Contar(Preencher(ref p.clip, slot));
            if (p.som == null) p.som = Fonte(p.gameObject, 1f, 25f);
        }
        foreach (TravaDeSeguranca t in Todos<TravaDeSeguranca>()) Contar(Preencher(ref t.somTrava, "trava"));
        foreach (Esconderijo e in Todos<Esconderijo>())
            Contar(Preencher(ref e.somEntrar, "esconderijo_entrar") | Preencher(ref e.somSair, "esconderijo_sair"));
        foreach (Gravador g in Todos<Gravador>()) Contar(Preencher(ref g.somPegar, "gravador_pegar"));
        foreach (CopiaDoAudio c in Todos<CopiaDoAudio>())
        {
            Contar(Preencher(ref c.somTrabalhando, "copia_loop") | Preencher(ref c.somPronto, "copia_pronto"));
            if (c.fonte == null) c.fonte = Fonte(c.gameObject, 0.5f, 6f);
        }
        foreach (DanielPerseguidor d in Todos<DanielPerseguidor>()) Contar(Preencher(ref d.somCaptura, "captura"));

        foreach (Movimento m in Todos<Movimento>()) Passos(m);
        Zumbidos();
        Compressores();
    }

    /// <summary>Tipo de porta pelo nome dela ou de um pai (os nomes vêm dos prefabs do Bruno).</summary>
    static string TipoDePorta(Transform t)
    {
        if (Tem(t, "vidro")) return "vidro";
        if (Tem(t, "frigorifica") || Tem(t, "camara fria")) return "frigorifica";
        if (Tem(t, "tecnica")) return "tecnica";
        if (Tem(t, "ServicoDupla") || Tem(t, "clinica dupla") || Tem(t, "vai e vem")) return "vaivem";
        return "comum";
    }

    /// <summary>Gaveta de arquivo de aço: o móvel (pai) é o prefab "Arquivo de aco" ou uma cópia renomeada "Arquivo 1", "Arquivo 2"...
    /// (a sala "Arquivo e escritorio" não conta, senão a mesa do servidor viraria arquivo).</summary>
    static bool EArquivoDeAco(Transform t)
    {
        string movel = t.parent != null ? t.parent.name : "";
        return movel.StartsWith("Arquivo de aco", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(movel, @"^Arquivo\s*\d+");
    }

    static string SomDaAcao(PontoDeAcao p)
    {
        switch (p.tipo)
        {
            case TipoDeAcao.Presenca:
                if (p.torneira != null || p.descarga != null) return null;   // a torneira e a descarga têm som próprio
                return p.porta != null ? "batida_forte" : "passos_corredor";
            case TipoDeAcao.TrancarPorta: return "ferrolho";
            case TipoDeAcao.PrenderCamaraFria: return "trava";
            case TipoDeAcao.Corrente: return "corrente";
            default: return null;   // o apagão toca pelo Update (vale também para o fusível tirado por outro caminho)
        }
    }

    void Passos(Movimento m)
    {
        var passos = m.GetComponent<PassosDoJogador>();
        if (passos == null) passos = m.gameObject.AddComponent<PassosDoJogador>();
        if (passos.padrao.sons.Length > 0 || passos.superficies.Count > 0) return;   // configurado à mão
        passos.padrao.sons = Variacoes("interno");
        passos.volumeGeral = 0.55f;
        Superficie(passos, "concreto", 0.9f, "Concreto", "Cimento");
        Superficie(passos, "asfalto_molhado", 0.85f, "Asfalto", "Calcada", "Calçada", "Pavimento");
        Superficie(passos, "grama_molhada", 0.8f, "Grama", "Gramado", "Capim", "Terra");
        Superficie(passos, "metal", 0.85f, "Metal galvanizado", "Grade", "Chapa");
        Contar(passos.padrao.sons.Length > 0);
    }

    static void Superficie(PassosDoJogador p, string slot, float volume, params string[] nomes)
    {
        var sons = Variacoes(slot);
        if (sons.Length == 0) return;
        p.superficies.Add(new PassosDoJogador.ConjuntoDePassos { nomes = nomes, sons = sons, volume = volume });
    }

    /// <summary>Zumbido de reator nas luzes do circuito geral: liga e apaga junto com elas (ConsumidorDeEnergia.sons).</summary>
    void Zumbidos()
    {
        AudioClip zumbido = Um("zumbido_loop");
        if (zumbido == null || maximoDeZumbidos == 0) return;
        var postos = new List<Vector3>();
        foreach (ConsumidorDeEnergia c in Todos<ConsumidorDeEnergia>())
        {
            if (c.inverter || c.circuito != RedeEletrica.Geral) continue;
            if (Array.Exists(c.sons, a => a != null && a.clip == zumbido)) continue;   // já tem
            var novos = new List<AudioSource>(c.sons);
            foreach (Light l in c.luzes.Length > 0 ? c.luzes : c.GetComponentsInChildren<Light>(true))
            {
                if (l == null || postos.Count >= maximoDeZumbidos) continue;
                Vector3 pos = l.transform.position;
                if (postos.Exists(q => (q - pos).sqrMagnitude < 4f * 4f)) continue;   // uma por ambiente, não por lâmpada
                postos.Add(pos);
                AudioSource a = Fonte(l.gameObject, volumeZumbido, 6f);
                a.clip = zumbido;
                a.loop = true;
                a.minDistance = 0.8f;
                a.priority = 200;   // som de fundo: cede canal para os efeitos
                a.time = UnityEngine.Random.Range(0f, zumbido.length * 0.9f);
                novos.Add(a);
            }
            c.sons = novos.ToArray();
            c.Aplicar();
        }
    }

    /// <summary>Compressor da câmara mortuária: toca enquanto o circuito de refrigeração está ligado (ver Update).</summary>
    void Compressores()
    {
        AudioClip loop = Um("compressor_loop");
        compressores.RemoveAll(a => a == null);
        if (loop == null) return;
        foreach (Transform t in Raizes("CamaraMortuaria"))
        {
            if (compressores.Exists(a => a.transform == t)) continue;
            AudioSource a = Fonte(t.gameObject, volumeCompressor, 10f);
            a.clip = loop;
            a.loop = true;
            a.priority = 190;   // som de fundo: cede canal para os efeitos
            a.time = UnityEngine.Random.Range(0f, loop.length * 0.9f);
            compressores.Add(a);
            Contar(true);
        }
        refrigeracaoLigada = null;   // o Update liga ou desliga conforme a rede
    }

    // ------------------------------------------------------------------ utilitários

    static T[] Todos<T>() where T : UnityEngine.Object => FindObjectsByType<T>(FindObjectsInactive.Include);

    static IEnumerable<Transform> Raizes(string trecho)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name.StartsWith(trecho, StringComparison.OrdinalIgnoreCase) && (t.parent == null || !Tem(t.parent, trecho)))
                yield return t;
    }

    void Contar(bool mudou) { if (mudou) Preenchidos++; }

    static bool Preencher(ref AudioClip campo, string slot)
    {
        if (campo != null) return false;
        campo = Um(slot);
        return campo != null;
    }

    AudioSource Fonte(GameObject go, float volume, float maxima = -1f)
    {
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = distanciaMinima;
        a.maxDistance = maxima > 0f ? maxima : distanciaMaxima;
        a.dopplerLevel = 0f;
        a.volume = volume;
        a.pitch = 1f + UnityEngine.Random.Range(-variacaoDeTom, variacaoDeTom);
        return a;
    }

    static bool Tem(Transform t, string trecho)
    {
        for (; t != null; t = t.parent)
            if (t.name.IndexOf(trecho, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
