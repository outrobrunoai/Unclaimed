using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Diretor de Daniel nas partes 1 a 3 (sem navegação: Daniel "ocupa" o prédio pelos PontoDeAcao).
/// Fica na cena Jogabilidade (objeto "Sistemas"); acha o mapa pelos nomes combinados.
///
/// Mesa:      imóvel sob o lençol. Depois da primeira foto da mesa, troca a pose do braço quando o jogador sai
///            da autópsia (duas fotos diferentes = "mudou:mesa_autopsia"). Larga a mesa (P08) quando
///            DecisoesDeDaniel.PodeSairDaMesa e a autópsia não está à vista: mesa vazia, torneira aberta.
/// Sombra:    presença (S4) e portas trancadas (S2), uma a cada ~90 s.
/// Obstrução: corrente na entrada (S5) logo no começo, apagão (S1) uma vez, armadilha da câmara fria (S3) uma
///            vez quando o jogador entra lá depois de achar Léo, e S2/S4 a cada ~60 s.
/// Regras: nunca age na sala do jogador nem nas vizinhas à vista; nunca age enquanto o jogador lê, usa uma tela
/// ou está no caderno (e espera alguns segundos depois).
/// </summary>
public class DiretorDeDaniel : MonoBehaviour, ISalvavel
{
    [Header("Nomes combinados (lista de salas e pontos)")]
    public string salaMesa = "Autopsia";
    public string salaCamaraFria = "CamaraFria";
    public string salaTecnica = "Tecnica";
    public string alvoMesa = "mesa_autopsia";
    public string pontoTorneiraMesa = "Autopsia/Torneira";

    [Header("Ritmo")]
    [Tooltip("Multiplica os intervalos de 90 s (Sombra) e 60 s (Obstrução). Ajustar em playtest.")]
    [Min(0.05f)] public float escalaDosIntervalos = 1f;
    [Tooltip("Segundos de folga depois que o jogador fecha um documento ou tela.")]
    [Min(0f)] public float folgaDepoisDeLer = 4f;
    [Tooltip("Segundos com o jogador fora da autópsia antes de mexer o braço.")]
    [Min(0f)] public float esperaDoBraco = 6f;
    [Tooltip("Segundos com o jogador dentro da câmara fria antes da armadilha.")]
    [Min(0f)] public float esperaDaArmadilha = 3f;

    public bool depurar;

    float proximaAcao = -1f, livreDesde, foraDaMesaDesde = -1f, naCamaraDesde = -1f;
    FaseDeDaniel faseAnterior;
    readonly List<PontoDeAcao> candidatosPontos = new List<PontoDeAcao>();
    readonly List<DecisoesDeDaniel.Candidato> candidatos = new List<DecisoesDeDaniel.Candidato>();

    public static DiretorDeDaniel Instancia { get; private set; }

    void OnEnable() => Instancia = this;
    void OnDisable() { if (Instancia == this) Instancia = null; }

    void Start()
    {
        faseAnterior = EstadoDaNoite.Fase;
        // Depois de carregar um save: a mesa e o braço voltam como estavam.
        if (EstadoDaNoite.Tem("daniel_saiu")) MostrarMesaVazia();
        if (EstadoDaNoite.Tem("mesa_braco_mudou")) TrocarBraco();
    }

    void Update()
    {
        if (TelaDeFinal.Ativa) return;
        FaseDeDaniel fase = DecisoesDeDaniel.ProximaFase(EstadoDaNoite.Fase, EstadoDaNoite.Tem);
        if (fase != EstadoDaNoite.Fase) EstadoDaNoite.Fase = fase;
        if (fase != faseAnterior) AoMudarDeFase(faseAnterior, fase);
        faseAnterior = fase;

        if (Jogador.Ocupado || Jogador.Movimento == null) { livreDesde = Time.time; return; }
        if (Time.time - livreDesde < folgaDepoisDeLer) return;
        if (EstadoDaNoite.Tem("gravador_entregue")) return;   // ele pegou o que queria e foi embora

        Vector3 jogador = Jogador.Posicao;
        HashSet<string> protegidas = Sala.Protegidas(jogador);
        switch (fase)
        {
            case FaseDeDaniel.Mesa: AtualizarMesa(protegidas); break;
            case FaseDeDaniel.Sombra:
            case FaseDeDaniel.Obstrucao: AtualizarSabotagem(fase, protegidas, jogador); break;
        }
    }

    void Log(string t) { if (depurar) Debug.Log("[Daniel] " + t, this); }

    void AoMudarDeFase(FaseDeDaniel de, FaseDeDaniel para)
    {
        Log($"fase {de} → {para}");
        proximaAcao = Time.time + DecisoesDeDaniel.Intervalo(para) * escalaDosIntervalos * 0.5f;
        if (para == FaseDeDaniel.Caca)
            foreach (PontoDeAcao p in PontoDeAcao.Todos)
                if (p != null && p.tipo == TipoDeAcao.TrancarPorta) p.Desfazer();   // na fuga, só a corrente segura
    }

    // ------------------------------------------------------------------ Mesa

    void AtualizarMesa(HashSet<string> protegidas)
    {
        bool mesaAVista = protegidas.Contains(salaMesa);
        if (mesaAVista) foraDaMesaDesde = -1f;
        else if (foraDaMesaDesde < 0f) foraDaMesaDesde = Time.time;
        bool foraHaTempo = foraDaMesaDesde >= 0f && Time.time - foraDaMesaDesde >= esperaDoBraco;

        if (!EstadoDaNoite.Tem("mesa_braco_mudou") && AlbumDeFotos.Fotografou(alvoMesa) && foraHaTempo)
        {
            EstadoDaNoite.Marcar("mesa_braco_mudou");
            TrocarBraco();
            Log("braço em outra posição");
        }

        if (DecisoesDeDaniel.PodeSairDaMesa(EstadoDaNoite.Tem) && !mesaAVista && foraHaTempo)
            SairDaMesa();
    }

    void TrocarBraco()
    {
        AlvoFotografico mesa = AlvoFotografico.Buscar(alvoMesa);
        if (mesa != null) mesa.Variante = 1;
    }

    /// <summary>P08: Daniel larga a mesa. Mesa vazia, lençol no chão, torneira aberta.</summary>
    public void SairDaMesa()
    {
        if (!EstadoDaNoite.Marcar("daniel_saiu")) return;
        EstadoDaNoite.Fase = DecisoesDeDaniel.ProximaFase(EstadoDaNoite.Fase, EstadoDaNoite.Tem);
        MostrarMesaVazia();
        PontoDeAcao torneira = PontoDeAcao.Buscar(pontoTorneiraMesa);
        if (torneira != null) torneira.Executar();
        Log("saiu da mesa");
    }

    static void MostrarMesaVazia()
    {
        EstadoMesaAutopsia mesa = FindAnyObjectByType<EstadoMesaAutopsia>();
        if (mesa != null) mesa.MostrarP08(true);
    }

    // ------------------------------------------------------------------ Sombra e Obstrução

    void AtualizarSabotagem(FaseDeDaniel fase, HashSet<string> protegidas, Vector3 jogador)
    {
        if (fase == FaseDeDaniel.Obstrucao)
        {
            if (TentarUnica(TipoDeAcao.Corrente, protegidas)) return;
            if (TentarArmadilha()) return;
        }

        if (proximaAcao < 0f) proximaAcao = Time.time + DecisoesDeDaniel.Intervalo(fase) * escalaDosIntervalos;
        if (Time.time < proximaAcao) return;

        bool agiu = false;
        // O apagão vem uma vez na Obstrução, com o jogador longe da sala técnica.
        if (fase == FaseDeDaniel.Obstrucao && !EstadoDaNoite.Tem("apagao") && !protegidas.Contains(salaTecnica))
            agiu = TentarUnica(TipoDeAcao.Apagao, protegidas);
        if (!agiu) agiu = AcaoComum(fase, protegidas, jogador);
        // Sem ponto bom agora: tenta de novo em alguns segundos, sem perder a vez.
        proximaAcao = Time.time + (agiu ? DecisoesDeDaniel.Intervalo(fase) * escalaDosIntervalos : 5f);
    }

    bool TentarUnica(TipoDeAcao tipo, HashSet<string> protegidas)
    {
        foreach (PontoDeAcao p in PontoDeAcao.Todos)
        {
            if (p == null || p.tipo != tipo || p.Vezes > 0 || protegidas.Contains(p.Sala)) continue;
            p.Executar();
            Log("executou " + p.id);
            return true;
        }
        return false;
    }

    bool TentarArmadilha()
    {
        if (!EstadoDaNoite.Tem("ev:F02") || EstadoDaNoite.Tem("preso_camara_fria")) return false;
        if (Sala.IdEm(Jogador.Posicao) != salaCamaraFria) { naCamaraDesde = -1f; return false; }
        if (naCamaraDesde < 0f) naCamaraDesde = Time.time;
        if (Time.time - naCamaraDesde < esperaDaArmadilha) return false;
        foreach (PontoDeAcao p in PontoDeAcao.Todos)
        {
            if (p == null || p.tipo != TipoDeAcao.PrenderCamaraFria || p.Vezes > 0) continue;
            p.Executar();
            Log("prendeu o jogador na câmara fria");
            return true;
        }
        return false;
    }

    bool AcaoComum(FaseDeDaniel fase, HashSet<string> protegidas, Vector3 jogador)
    {
        candidatos.Clear();
        candidatosPontos.Clear();
        var gruposTrancados = new HashSet<string>();
        foreach (PontoDeAcao p in PontoDeAcao.Todos)
        {
            if (p == null) continue;
            if (p.tipo == TipoDeAcao.TrancarPorta && p.Ativo && !string.IsNullOrEmpty(p.grupo)) gruposTrancados.Add(p.grupo);
            candidatosPontos.Add(p);
            candidatos.Add(new DecisoesDeDaniel.Candidato
            {
                id = p.id, sala = p.Sala, grupo = p.grupo, tipo = p.tipo, posicao = p.transform.position,
                disponivel = p.Disponivel && p.isActiveAndEnabled, valeNaFase = p.ValeNa(fase),
            });
        }
        int i = DecisoesDeDaniel.Escolher(candidatos, protegidas, jogador, gruposTrancados, () => Random.value);
        if (i < 0) return false;
        candidatosPontos[i].Executar();
        Log("executou " + candidatosPontos[i].id);
        return true;
    }

    // ------------------------------------------------------------------ save

    [System.Serializable] struct Estado { public float faltaParaAcao; }

    public string Salvar() => JsonUtility.ToJson(new Estado { faltaParaAcao = proximaAcao < 0f ? -1f : Mathf.Max(0f, proximaAcao - Time.time) });

    public void Carregar(string estado)
    {
        var e = JsonUtility.FromJson<Estado>(estado);
        proximaAcao = e.faltaParaAcao < 0f ? -1f : Time.time + e.faltaParaAcao;
    }
}
