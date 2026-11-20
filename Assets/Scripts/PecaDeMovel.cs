using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gaveta (desliza pelos trilhos) ou porta de armário (gira nas dobradiças). Fica no objeto que se move, com
/// os BoxColliders da peça nele ou nos filhos; o que estiver dentro (papéis, objetos) vai como filho e anda junto.
/// Posição fechada = a pose do objeto na cena/prefab.
///
/// Interação (AlvoDeInteracao): mirar a peça e apertar a tecla. Apertar durante o movimento inverte a partir da
/// posição atual, sem salto: cada comando recomeça um perfil suave (acelera e desacelera), e a velocidade nunca
/// passa do pico do perfil.
///
/// Obstrução: antes de cada passo, o volume dos BoxColliders na nova pose é testado. O corpo do próprio móvel
/// (colliders da raiz e dos filhos que não são outras peças móveis) é ignorado; paredes, outros móveis, outras
/// gavetas/portas e o jogador (CharacterController, tratado como cilindro até o chão) param a peça no último ponto
/// livre. Não empurra ninguém.
/// </summary>
[DisallowMultipleComponent]
public class PecaDeMovel : AlvoDeInteracao, ISalvavel
{
    public enum TipoDePeca { Gaveta, Porta }

    [Header("Movimento (no espaço do objeto pai)")]
    public TipoDePeca tipo = TipoDePeca.Gaveta;
    [Tooltip("Gaveta: direção em que ela sai. Porta: eixo das dobradiças (o objeto gira em torno do próprio pivô).")]
    public Vector3 eixo = Vector3.forward;
    [Tooltip("Gaveta: curso (m) até aberta. Porta: abertura máxima (graus; o sinal escolhe o lado).")]
    public float curso = 0.4f;
    [Tooltip("Tempo (s) de um percurso completo.")]
    [Min(0.1f)] public float duracao = 0.6f;

    [Header("Interação")]
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.8f;
    public string textoAbrir = "Abrir gaveta";
    public string textoFechar = "Fechar gaveta";
    [Tooltip("Trancada (ex.: cadeado): não se move; a indicação mostra textoTrancada.")]
    public bool trancada = false;
    public string textoTrancada = "Trancado";
    public string avisoTrancada = "";
    [Tooltip("Raiz do móvel: seus colliders (corpo, tampo) não bloqueiam a peça. Vazio = o objeto pai.")]
    public Transform movel;

    [Header("Obstrução")]
    public LayerMask obstaculos = ~(1 << 2);
    [Tooltip("Margem (m) descontada do volume no teste: evita bloquear por contato de borda.")]
    [Min(0f)] public float folga = 0.004f;
    [Tooltip("Maior passo entre testes (m para gavetas, graus para portas).")]
    [Min(0.001f)] public float passoDeTeste = 0.01f;

    [Header("Som (opcional)")]
    public AudioSource fonte;
    public AudioClip somAbrir, somFechar, somFechou, somBloqueio;

    /// <summary>Abertura atual: metros (gaveta) ou graus (porta), de 0 a |curso|.</summary>
    public float Abertura => abertura;
    public bool EmMovimento => movendo;
    public bool Bloqueada => bloqueada;
    public bool DestinoAberta => destino > 0f;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => trancada ? textoTrancada : destino > 0f ? textoFechar : textoAbrir;

    public override void Interagir()
    {
        if (trancada) { MiraDeInteracao.Avisar(avisoTrancada, 1.5f); return; }
        Alternar();
    }

    float abertura, origem, destino, tempo, duracaoSegmento;
    bool movendo, bloqueada, iniciada;
    Vector3 posicaoFechada;
    Quaternion rotacaoFechada;
    Collider[] proprios = Array.Empty<Collider>();
    readonly HashSet<Collider> ignorar = new HashSet<Collider>();
    readonly Collider[] encontrados = new Collider[16];
    // Busca do personagem é larga e pega o corpo do móvel inteiro; com buffer pequeno o CharacterController sobrava.
    static readonly Collider[] arredores = new Collider[128];

    float Maximo => Mathf.Abs(curso);

    void Awake()
    {
        Iniciar();
        // Collider em movimento: corpo cinemático, fora da simulação.
        if (!TryGetComponent(out Rigidbody rb)) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Iniciar()
    {
        if (iniciada) return;
        iniciada = true;
        posicaoFechada = transform.localPosition;
        rotacaoFechada = transform.localRotation;
        proprios = GetComponentsInChildren<Collider>(true);
        ignorar.Clear();
        foreach (Collider c in proprios) ignorar.Add(c);
        Transform raiz = movel != null ? movel : transform.parent;
        if (raiz != null)
            foreach (Collider c in raiz.GetComponentsInChildren<Collider>(true))
            {
                PecaDeMovel dona = c.GetComponentInParent<PecaDeMovel>();
                if (dona == null || dona == this) ignorar.Add(c);   // corpo do móvel, não outra gaveta/porta
            }
    }

    public void Alternar() { if (!trancada) IrPara(destino > 0f ? 0f : Maximo); }
    public void Abrir() { if (!trancada) IrPara(Maximo); }
    public void Fechar() => IrPara(0f);

    // Save: abertura (para onde ia, se estava em movimento) e tranca.
    [Serializable] struct EstadoSalvo { public float abertura; public bool trancada; }

    public string Salvar() =>
        JsonUtility.ToJson(new EstadoSalvo { abertura = movendo ? destino : abertura, trancada = trancada });

    public void Carregar(string estado)
    {
        Iniciar();
        var e = JsonUtility.FromJson<EstadoSalvo>(estado);
        trancada = e.trancada;
        abertura = destino = Mathf.Clamp(e.abertura, 0f, Maximo);
        movendo = bloqueada = false;
        Aplicar(abertura);
    }

    /// <summary>Fecha na hora, sem animação (estado de partida).</summary>
    public void FecharImediatamente()
    {
        Iniciar();
        abertura = destino = 0f;
        movendo = bloqueada = false;
        Aplicar(0f);
    }

    void IrPara(float alvo)
    {
        Iniciar();
        if (Mathf.Approximately(alvo, abertura) && !movendo) { destino = alvo; return; }
        bool abrindo = alvo > abertura;
        origem = abertura;
        destino = alvo;
        tempo = 0f;
        duracaoSegmento = Mathf.Max(0.12f, duracao * Mathf.Abs(alvo - origem) / Maximo);
        movendo = true;
        bloqueada = false;
        Tocar(abrindo ? somAbrir : somFechar);
    }

    protected override void Update()
    {
        base.Update();
        if (movendo) Mover(Time.deltaTime);
    }

    void Mover(float dt)
    {
        tempo += dt;
        float u = Mathf.Clamp01(tempo / duracaoSegmento);
        float s = u * u * (3f - 2f * u);
        float alvo = Mathf.MoveTowards(abertura, Mathf.Lerp(origem, destino, s), 1.6f * Maximo / duracao * dt);

        int passos = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(alvo - abertura) / passoDeTeste));
        float inicio = abertura;
        bool travou = false;
        for (int i = 1; i <= passos; i++)
        {
            float teste = Mathf.Lerp(inicio, alvo, i / (float)passos);
            if (Obstruida(teste)) { travou = true; break; }
            abertura = teste;
        }
        Aplicar(abertura);
        Physics.SyncTransforms();

        if (travou)
        {
            movendo = false;
            bloqueada = true;
            Tocar(somBloqueio);
            return;
        }
        if (u >= 1f && Mathf.Approximately(abertura, destino))
        {
            movendo = false;
            if (destino <= 0f) Tocar(somFechou);
            else Esvair(somAbrir);   // abriu: o rangido não continua depois que a peça parou
        }
    }

    Vector3 Posicao(float a) => tipo == TipoDePeca.Gaveta ? posicaoFechada + Direcao * a : posicaoFechada;
    Quaternion Rotacao(float a) =>
        tipo == TipoDePeca.Porta ? Quaternion.AngleAxis(Mathf.Sign(curso) * a, Direcao) * rotacaoFechada : rotacaoFechada;
    Vector3 Direcao => eixo.sqrMagnitude > 0f ? eixo.normalized : Vector3.forward;

    void Aplicar(float a)
    {
        transform.localPosition = Posicao(a);
        transform.localRotation = Rotacao(a);
    }

    /// <summary>Algum collider de fora tocaria os BoxColliders da peça nesta abertura?</summary>
    bool Obstruida(float a)
    {
        Matrix4x4 pai = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 nova = pai * Matrix4x4.TRS(Posicao(a), Rotacao(a), transform.localScale);
        Matrix4x4 atualInv = transform.worldToLocalMatrix;
        foreach (Collider c in proprios)
        {
            if (!c.enabled || c.isTrigger || !(c is BoxCollider box)) continue;
            Matrix4x4 m = nova * atualInv * c.transform.localToWorldMatrix;
            Vector3 e = c.transform.lossyScale;
            Vector3 meia = Vector3.Scale(box.size, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z))) * 0.5f
                           - Vector3.one * folga;
            if (meia.x <= 0f || meia.y <= 0f || meia.z <= 0f) continue;
            Vector3 centro = m.MultiplyPoint3x4(box.center);
            int n = Physics.OverlapBoxNonAlloc(centro, meia, encontrados, m.rotation, obstaculos, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!ignorar.Contains(encontrados[i])) return true;
            if (PersonagemNoVolume(centro, meia, m.rotation)) return true;
        }
        return false;
    }

    /// <summary>
    /// A cápsula do personagem é arredondada embaixo, mas ali estão as pernas e os pés: uma gaveta baixa não pode
    /// passar por esse vão. O personagem conta como cilindro vertical do raio da cápsula, até o chão.
    /// </summary>
    bool PersonagemNoVolume(Vector3 centro, Vector3 meia, Quaternion rotacao)
    {
        // Margem horizontal: a cápsula se afasta dentro do skinWidth antes de tocar a caixa, então a busca precisa
        // achá-la já nessa faixa; o teste de cilindro abaixo decide com o raio exato.
        int n = Physics.OverlapBoxNonAlloc(centro, meia + new Vector3(0.25f, 0.6f, 0.25f), arredores, rotacao, obstaculos,
            QueryTriggerInteraction.Ignore);
        Quaternion inv = Quaternion.Inverse(rotacao);
        for (int i = 0; i < n; i++)
        {
            if (!(arredores[i] is CharacterController cc) || !cc.enabled) continue;
            Vector3 eixoCc = cc.transform.TransformPoint(cc.center);
            // Raio + skinWidth: o controller se afasta de qualquer coisa dentro da pele.
            float raio = cc.radius * Mathf.Max(Mathf.Abs(cc.transform.lossyScale.x), Mathf.Abs(cc.transform.lossyScale.z)) + cc.skinWidth;
            float baseCc = cc.bounds.min.y;
            if (baseCc > centro.y + meia.y) continue;   // o personagem está acima da peça
            Vector3 local = inv * (new Vector3(eixoCc.x, centro.y, eixoCc.z) - centro);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - meia.x), dz = Mathf.Max(0f, Mathf.Abs(local.z) - meia.z);
            if (dx * dx + dz * dz < raio * raio) return true;
        }
        return false;
    }

    void Tocar(AudioClip clip)
    {
        if (fonte == null || clip == null) return;
        if (esvaindo != null) { StopCoroutine(esvaindo); esvaindo = null; fonte.volume = volumeDaFonte; }
        fonte.Stop();   // um som por vez: interações repetidas não empilham
        fonte.clip = clip;
        fonte.Play();
    }

    [Tooltip("Ao terminar de abrir, o som de abrir que ainda estiver tocando some neste tempo (s).")]
    [Min(0f)] public float esvairAoParar = 0.12f;
    Coroutine esvaindo;
    float volumeDaFonte = 1f;

    /// <summary>Some com o som em curso (se ainda for este clipe) em vez de deixá-lo tocar com a peça parada.</summary>
    void Esvair(AudioClip clip)
    {
        if (fonte == null || clip == null || fonte.clip != clip || !fonte.isPlaying || !isActiveAndEnabled) return;
        if (esvaindo != null) return;
        volumeDaFonte = fonte.volume;
        esvaindo = StartCoroutine(EsvairAgora());
    }

    System.Collections.IEnumerator EsvairAgora()
    {
        for (float t = 0f; t < esvairAoParar; t += Time.unscaledDeltaTime)
        {
            fonte.volume = volumeDaFonte * (1f - t / esvairAoParar);
            yield return null;
        }
        fonte.Stop();
        fonte.volume = volumeDaFonte;
        esvaindo = null;
    }
}
