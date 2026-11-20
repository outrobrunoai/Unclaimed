using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum TipoDeAcao
{
    Presenca,          // S4: torneira, descarga, porta batendo, passos, vulto atrás do vidro
    TrancarPorta,      // S2: tranca uma porta pelo outro lado (destrava sozinha depois de um tempo)
    Apagao,            // S1: tira o fusível da luz geral
    PrenderCamaraFria, // S3: fecha a porta da câmara fria com o jogador dentro e apaga a luz
    Corrente,          // S5: corrente e cadeado na entrada principal
    Aparicao,          // onde Daniel aparece no confronto e começa a caça
}

/// <summary>
/// Lugar do mapa onde Daniel pode agir, com nome fixo "Sala/Coisa" (ex.: Tecnica/Fusivel, Autopsia/Torneira,
/// WC/Descarga, Corredor/Passos, Recepcao/Corrente, Arquivo/Porta). Bruno marca na cena e liga as referências;
/// o DiretorDeDaniel escolhe e executa. A sala é o texto antes da barra.
/// </summary>
public class PontoDeAcao : MonoBehaviour, ISalvavel
{
    public string id = "Autopsia/Torneira";
    public TipoDeAcao tipo = TipoDeAcao.Presenca;
    [Tooltip("Pode repetir (só Presença faz sentido repetir).")]
    public bool repetivel = true;
    [Tooltip("Fases em que vale (Mesa não tem ações).")]
    public bool naSombra = true, naObstrucao = true;
    [Tooltip("Portas do mesmo grupo nunca ficam trancadas juntas (as duas pontas do circuito).")]
    public string grupo = "";

    [Header("Referências (use as que servirem)")]
    public FolhaDePorta porta;
    [Tooltip("Outras folhas trancadas junto (porta dupla, outra porta da sala).")]
    public FolhaDePorta[] outrasPortas = new FolhaDePorta[0];
    public Torneira torneira;
    public Descarga descarga;
    public AudioSource som;
    public AudioClip clip;
    [Tooltip("Ligado por alguns segundos (vulto) ou para sempre (corrente).")]
    public GameObject mostrar;
    [Min(0.2f)] public float segundosVisivel = 2.5f;
    [Tooltip("Luzes apagadas pela armadilha da câmara fria.")]
    public Light[] luzes = new Light[0];
    [Tooltip("Trancar porta: segundos até destravar sozinha (0 = só destrava quando outra porta do grupo tranca).")]
    [Min(0f)] public float segundosTrancada = 150f;
    public UnityEvent aoExecutar;

    static readonly List<PontoDeAcao> pontos = new List<PontoDeAcao>();
    public static IReadOnlyList<PontoDeAcao> Todos => pontos;

    public string Sala => id.Contains("/") ? id.Substring(0, id.IndexOf('/')) : id;
    public int Vezes { get; private set; }
    public bool Ativo { get; private set; }   // efeito em curso (porta trancada, armadilha, corrente)
    float destravarEm = -1f;

    void OnEnable() { if (!pontos.Contains(this)) pontos.Add(this); }
    void OnDisable() => pontos.Remove(this);

    public static PontoDeAcao Buscar(string id)
    {
        foreach (PontoDeAcao p in pontos) if (p != null && p.id == id) return p;
        return null;
    }

    public bool ValeNa(FaseDeDaniel fase) =>
        (fase == FaseDeDaniel.Sombra && naSombra) || (fase == FaseDeDaniel.Obstrucao && naObstrucao);

    public bool Disponivel => !Ativo && (repetivel || Vezes == 0);

    /// <summary>Verdadeiro se este ponto está ativo segurando a folha informada.</summary>
    public bool Segura(FolhaDePorta f)
    {
        if (!Ativo || f == null) return false;
        if (porta == f) return true;
        if (outrasPortas != null)
            foreach (FolhaDePorta outra in outrasPortas)
                if (outra == f) return true;
        return false;
    }

    /// <summary>Verdadeiro se esta ação fecha (ou bate) alguma porta.</summary>
    public bool FechaPorta =>
        tipo == TipoDeAcao.TrancarPorta || tipo == TipoDeAcao.PrenderCamaraFria || tipo == TipoDeAcao.Corrente
        || (tipo == TipoDeAcao.Presenca && porta != null && porta.Abertura > 1f);

    /// <summary>Verdadeiro se o jogador está vendo alguma das portas deste ponto.</summary>
    public bool PortasAVista()
    {
        if (porta != null && OlharDoJogador.Vendo(porta)) return true;
        if (outrasPortas != null)
            foreach (FolhaDePorta outra in outrasPortas)
                if (outra != null && OlharDoJogador.Vendo(outra)) return true;
        return false;
    }

    public void Executar()
    {
        if (!pontos.Contains(this)) pontos.Add(this);   // quem age sempre entra na lista (testes sem OnEnable)
        Vezes++;
        switch (tipo)
        {
            case TipoDeAcao.Presenca:
                if (torneira != null && !torneira.Aberta) torneira.Alternar();
                if (descarga != null && !descarga.EmCiclo) descarga.Acionar();
                if (porta != null && porta.Abertura > 1f) porta.Fechar();
                if (mostrar != null) StartCoroutine(MostrarPor(segundosVisivel));
                break;
            case TipoDeAcao.TrancarPorta:
                if (FecharETrancarPortas())
                {
                    Ativo = true;
                    destravarEm = segundosTrancada > 0f ? Time.time + segundosTrancada : -1f;
                }
                break;
            case TipoDeAcao.Apagao:
                RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
                break;
            case TipoDeAcao.PrenderCamaraFria:
                FecharETrancarPortas();
                foreach (Light l in luzes) if (l != null) l.enabled = false;
                Ativo = true;
                EstadoDaNoite.Marcar("preso_camara_fria");
                break;
            case TipoDeAcao.Corrente:
                FecharETrancarPortas();
                if (mostrar != null) mostrar.SetActive(true);
                Ativo = true;
                EstadoDaNoite.Marcar("corrente");
                break;
            case TipoDeAcao.Aparicao:
                break;
        }
        if (som != null) { if (clip != null) som.PlayOneShot(clip); else som.Play(); }
        aoExecutar?.Invoke();
    }

    /// <summary>Desfaz o efeito (porta destravada, luz de volta). A corrente não sai.</summary>
    public void Desfazer()
    {
        if (!Ativo || tipo == TipoDeAcao.Corrente) return;
        Ativo = false;
        destravarEm = -1f;
        DestravarSeLivre(porta);
        if (outrasPortas != null)
            foreach (FolhaDePorta f in outrasPortas)
                DestravarSeLivre(f);
        foreach (Light l in luzes) if (l != null) l.enabled = true;
    }

    bool FecharETrancarPortas()
    {
        bool alguma = false;
        if (porta != null) { porta.Fechar(); porta.trancada = true; alguma = true; }
        if (outrasPortas != null)
            foreach (FolhaDePorta f in outrasPortas)
                if (f != null) { f.Fechar(); f.trancada = true; alguma = true; }
        return alguma;
    }

    void MarcarPortasTrancadas()
    {
        if (porta != null) porta.trancada = true;
        if (outrasPortas != null)
            foreach (FolhaDePorta f in outrasPortas)
                if (f != null) f.trancada = true;
    }

    void DestravarSeLivre(FolhaDePorta f)
    {
        if (f == null) return;
        // Tempo de uma porta trancada não pode soltar uma folha presa por armadilha ou corrente.
        if (tipo == TipoDeAcao.TrancarPorta && SeguradaPorOutroCritico(f)) return;
        f.Destravar();
    }

    bool SeguradaPorOutroCritico(FolhaDePorta f)
    {
        foreach (PontoDeAcao p in pontos)
        {
            if (p == null || p == this) continue;
            if ((p.tipo == TipoDeAcao.PrenderCamaraFria || p.tipo == TipoDeAcao.Corrente) && p.Segura(f))
                return true;
        }
        return false;
    }

    void Update()
    {
        if (Ativo && destravarEm > 0f && Time.time >= destravarEm) Desfazer();
    }

    IEnumerator MostrarPor(float segundos)
    {
        mostrar.SetActive(true);
        yield return new WaitForSeconds(segundos);
        mostrar.SetActive(false);
    }

    [System.Serializable] struct Estado { public int vezes; public bool ativo; }

    public string Salvar() => JsonUtility.ToJson(new Estado { vezes = Vezes, ativo = Ativo });

    public void Carregar(string estado)
    {
        var e = JsonUtility.FromJson<Estado>(estado);
        Vezes = e.vezes;
        Ativo = e.ativo;
        if (Ativo && (tipo == TipoDeAcao.TrancarPorta || tipo == TipoDeAcao.PrenderCamaraFria || tipo == TipoDeAcao.Corrente))
            MarcarPortasTrancadas();
        if (Ativo && tipo == TipoDeAcao.TrancarPorta && segundosTrancada > 0f) destravarEm = Time.time + segundosTrancada;
        if (Ativo && tipo == TipoDeAcao.Corrente && mostrar != null) mostrar.SetActive(true);
        if (Ativo && tipo == TipoDeAcao.PrenderCamaraFria) foreach (Light l in luzes) if (l != null) l.enabled = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = tipo == TipoDeAcao.Aparicao ? new Color(1f, 0.2f, 0.2f, 0.8f) : new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.25f);
    }
}
