using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Uma folha de porta que abre e fecha girando no próprio pivô. Coloque no objeto cujo pivô é o eixo
/// das dobradiças / do pivô, com o vidro, as ferragens móveis e os BoxColliders como filhos (ou nele mesmo).
/// Portas de duas folhas usam um componente em cada folha; elas funcionam de forma independente e uma
/// conta como obstáculo para a outra.
///
/// Interação (AlvoDeInteracao): olhar para a folha (raio pelo centro da tela, sem atravessar paredes), estar no
/// alcance e apertar a tecla. Apertar durante o movimento inverte a direção a partir do ângulo atual.
///
/// Obstrução: cada passo da folha é testado ao longo do arco, em sub-passos, com o volume dos colliders na
/// nova posição.
/// - Paredes, batentes, a outra folha e objetos fixos bloqueiam ao serem tocados.
/// - Personagens (CharacterController) são empurrados: a folha desloca o personagem com
///   CharacterController.Move, só no plano horizontal e só o necessário para manter a distância de contato,
///   então paredes e móveis continuam detendo o personagem e a velocidade vertical dele não muda. Em contato
///   a folha anda no máximo na velocidade de empurrão. Se o personagem não consegue sair (preso contra uma
///   parede), a folha para.
/// - Corpos dinâmicos (Rigidbody não cinemático) bloqueiam a uma pequena distância.
/// Bloqueada, a folha fica parada até nova interação (que inverte o sentido).
/// </summary>
[DisallowMultipleComponent]
public class FolhaDePorta : AlvoDeInteracao, ISalvavel
{
    [Header("Movimento (giro em torno de eixoLocal, no espaço do objeto pai)")]
    [Tooltip("Eixo de giro no espaço do pai. Para portas comuns: (0, 1, 0).")]
    public Vector3 eixoLocal = Vector3.up;
    [Tooltip("Ângulo (graus) da folha fechada em torno do eixo.")]
    public float anguloFechado = 0f;
    [Tooltip("+1 ou -1: para que lado a folha abre a partir do ângulo fechado.")]
    public int sentido = 1;
    [Tooltip("Abertura máxima (graus), limitada por paredes, batentes e prendedores.")]
    [Range(1f, 180f)] public float anguloMaximo = 90f;
    [Tooltip("Tempo (s) de um percurso completo, fechada → aberta.")]
    [Min(0.1f)] public float duracao = 1f;

    [Header("Estado inicial")]
    public bool iniciarAberta = false;
    [Tooltip("Fração do ângulo máximo quando começa aberta.")]
    [Range(0f, 1f)] public float aberturaInicial = 1f;

    [Header("Interação")]
    [Tooltip("Nome da tecla (Input System: Key; Input Manager: KeyCode).")]
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 2f;
    public string textoAbrir = "Abrir";
    public string textoFechar = "Fechar";

    [Header("Tranca (opcional)")]
    [Tooltip("Trancada: não abre. Com a chave no Chaveiro, a interação destranca (e a porta fica destrancada).")]
    public bool trancada = false;
    [Tooltip("Identificador da chave que destranca (Chaveiro).")]
    public string chaveNecessaria = "";
    public string textoTrancada = "Porta trancada";
    public string textoDestrancar = "Destrancar porta";
    public string avisoDestrancada = "Porta destrancada.";
    public string avisoSemChave = "Está trancada.";

    [Header("Obstrução")]
    [Tooltip("Layers testadas pela folha (jogador, móveis, a outra folha, paredes).")]
    public LayerMask obstaculos = ~(1 << 2);
    [Tooltip("Margem (m) descontada do volume dos colliders no teste contra geometria fixa (paredes, batentes, " +
             "a outra folha): evita bloquear por contato de borda.")]
    [Min(0f)] public float folga = 0.01f;
    [Tooltip("Distância (m) mantida de corpos dinâmicos (Rigidbody não cinemático), que bloqueiam a folha.")]
    [Min(0f)] public float margemCorpos = 0.03f;
    [Tooltip("Maior passo angular (graus) entre testes ao longo do arco.")]
    [Range(0.25f, 5f)] public float passoDeTeste = 1f;

    [Header("Personagens no caminho")]
    [Tooltip("Empurrar personagens (CharacterController) em vez de parar ao encostar neles.")]
    public bool empurrarPersonagens = true;
    [Tooltip("Distância (m) mantida entre a folha e a cápsula do personagem empurrado.")]
    [Min(0f)] public float contato = 0.02f;
    [Tooltip("Velocidade máxima (m/s) com que a folha desloca um personagem; em contato a folha desacelera.")]
    [Min(0.05f)] public float velocidadeEmpurrao = 1.2f;
    [Tooltip("Se o personagem andar menos que o pedido por mais que isto (m), está preso: a folha para.")]
    [Min(0.001f)] public float toleranciaPreso = 0.004f;

    [Header("Som (opcional)")]
    public AudioSource fonte;
    [Tooltip("Ao começar a abrir.")] public AudioClip somAbrir;
    [Tooltip("Ao começar a fechar.")] public AudioClip somFechar;
    [Tooltip("Quando chega ao ângulo fechado (batida).")] public AudioClip somFechou;
    [Tooltip("Quando para contra um obstáculo.")] public AudioClip somBloqueio;
    [Tooltip("Ao destrancar com a chave.")] public AudioClip somDestrancar;

    [Header("Eventos")]
    public UnityEvent aoComecarAbrir;
    public UnityEvent aoComecarFechar;
    public UnityEvent aoAbrirTotalmente;
    public UnityEvent aoFecharTotalmente;
    public UnityEvent aoBloquear;

    /// <summary>Abertura atual em graus, de 0 (fechada) ao ângulo máximo.</summary>
    public float Abertura => abertura;
    public bool EmMovimento => movendo;
    public bool Bloqueada => bloqueada;
    /// <summary>Para onde a folha vai (ou está): true = aberta.</summary>
    public bool DestinoAberta => destino > 0f;

    float abertura, origem, destino, tempo, duracaoSegmento;
    bool movendo, bloqueada, iniciada;
    Collider[] proprios = Array.Empty<Collider>();
    readonly Collider[] encontrados = new Collider[16];
    readonly HashSet<Collider> ignorar = new HashSet<Collider>();
    readonly List<CharacterController> personagens = new List<CharacterController>();

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao =>
        trancada ? (Chaveiro.Tem(chaveNecessaria) ? textoDestrancar : textoTrancada) : destino > 0f ? textoFechar : textoAbrir;

    public override void Interagir()
    {
        if (!trancada) { Alternar(); return; }
        if (!Chaveiro.Tem(chaveNecessaria)) { MiraDeInteracao.Avisar(avisoSemChave, 1.5f); return; }
        trancada = false;   // a chave fica com o jogador; a porta continua destrancada nesta sessão
        Tocar(somDestrancar);
        MiraDeInteracao.Avisar(avisoDestrancada);
    }

    // Save: abertura (para onde ia, se estava em movimento) e tranca.
    [Serializable] struct EstadoSalvo { public float abertura; public bool trancada; }

    public string Salvar() =>
        JsonUtility.ToJson(new EstadoSalvo { abertura = movendo ? destino : abertura, trancada = trancada });

    public void Carregar(string estado)
    {
        var e = JsonUtility.FromJson<EstadoSalvo>(estado);
        trancada = e.trancada;
        abertura = destino = Mathf.Clamp(e.abertura, 0f, anguloMaximo);
        movendo = bloqueada = false;
        iniciada = true;
        AplicarRotacao(abertura);
    }

    /// <summary>Destranca sem chave (ex.: ferrolho do outro lado). Não volta a trancar.</summary>
    public void Destravar()
    {
        if (!trancada) return;
        trancada = false;
        Tocar(somDestrancar);
    }

    // Volume de um BoxCollider da folha numa pose: centro, rotação e meias medidas no mundo.
    struct Caixa { public Vector3 centro; public Quaternion rotacao; public Vector3 meia; }

    void Awake()
    {
        proprios = GetComponentsInChildren<Collider>(true);
        ignorar.Clear();
        foreach (Collider c in proprios) ignorar.Add(c);
        // Collider em movimento: corpo cinemático, fora da simulação e do bake estático.
        if (!TryGetComponent(out Rigidbody rb)) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        AplicarEstadoInicial();
    }

    /// <summary>Coloca a folha no estado inicial configurado, sem animação (também no Editor).</summary>
    [ContextMenu("Aplicar estado inicial")]
    public void AplicarEstadoInicial()
    {
        abertura = destino = iniciarAberta ? anguloMaximo * aberturaInicial : 0f;
        movendo = bloqueada = false;
        iniciada = true;
        AplicarRotacao(abertura);
    }

    /// <summary>Abre se estiver indo para fechada e vice-versa; inverte a partir do ângulo atual.</summary>
    public void Alternar()
    {
        if (!iniciada) AplicarEstadoInicial();
        IrPara(destino > 0f ? 0f : anguloMaximo);
    }

    public void Abrir() => IrPara(anguloMaximo);
    public void Fechar() => IrPara(0f);

    void IrPara(float alvo)
    {
        if (Mathf.Approximately(alvo, abertura) && !movendo) { destino = alvo; return; }
        bool abrindo = alvo > abertura;
        origem = abertura;
        destino = alvo;
        tempo = 0f;
        // Percurso parcial leva tempo proporcional; o perfil suave parte do repouso a cada comando.
        duracaoSegmento = Mathf.Max(0.15f, duracao * Mathf.Abs(alvo - origem) / anguloMaximo);
        movendo = true;
        bloqueada = false;
        Tocar(abrindo ? somAbrir : somFechar);
        (abrindo ? aoComecarAbrir : aoComecarFechar)?.Invoke();
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
        float s = u * u * (3f - 2f * u);   // aceleração e desaceleração suaves
        // Se o empurrão atrasou a folha, ela retoma sem saltar: nunca mais rápida que o pico do perfil.
        float alvo = Mathf.MoveTowards(abertura, Mathf.Lerp(origem, destino, s), 1.6f * anguloMaximo / duracao * dt);

        float orcamento = velocidadeEmpurrao * dt;   // deslocamento de personagens permitido neste frame
        int passos = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(alvo - abertura) / passoDeTeste));
        float inicio = abertura;
        bool travou = false;
        for (int i = 1; i <= passos; i++)
        {
            float teste = Mathf.Lerp(inicio, alvo, i / (float)passos);
            if (ObstaculoFixo(teste) != null) { travou = true; break; }
            int r = AbrirCaminho(teste, ref orcamento);
            if (r < 0) { travou = true; break; }   // personagem preso
            if (r == 0) break;                      // orçamento de empurrão gasto: continua no próximo frame
            abertura = teste;
        }
        AplicarRotacao(abertura);
        Physics.SyncTransforms();   // quem andar neste frame já colide com a folha na posição nova

        if (travou)
        {
            movendo = false;
            bloqueada = true;
            Tocar(somBloqueio);
            aoBloquear?.Invoke();
            return;
        }
        if (u >= 1f && Mathf.Approximately(abertura, destino))
        {
            movendo = false;
            if (destino <= 0f) { Tocar(somFechou); aoFecharTotalmente?.Invoke(); }
            else aoAbrirTotalmente?.Invoke();
        }
    }

    Quaternion Rotacao(float graus) =>
        Quaternion.AngleAxis(anguloFechado + sentido * graus, eixoLocal.sqrMagnitude > 0f ? eixoLocal.normalized : Vector3.up);

    void AplicarRotacao(float graus) => transform.localRotation = Rotacao(graus);

    /// <summary>Volumes dos BoxColliders da folha na pose hipotética desta abertura.</summary>
    IEnumerable<Caixa> Caixas(float graus)
    {
        Matrix4x4 pai = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 nova = pai * Matrix4x4.TRS(transform.localPosition, Rotacao(graus), transform.localScale);
        Matrix4x4 atualInv = transform.worldToLocalMatrix;
        foreach (Collider c in proprios)
        {
            if (!c.enabled || c.isTrigger || !(c is BoxCollider box)) continue;
            Matrix4x4 m = nova * atualInv * c.transform.localToWorldMatrix;
            Vector3 e = c.transform.lossyScale;
            yield return new Caixa
            {
                centro = m.MultiplyPoint3x4(box.center),
                rotacao = m.rotation,
                meia = Vector3.Scale(box.size, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z))) * 0.5f,
            };
        }
    }

    /// <summary>Geometria fixa ou corpo dinâmico que a folha tocaria com esta abertura (personagens à parte).</summary>
    Collider ObstaculoFixo(float graus)
    {
        foreach (Caixa k in Caixas(graus))
        {
            Collider c = Sobreposto(k, -folga, false);
            if (c != null) return c;
            c = Sobreposto(k, margemCorpos, true);
            if (c != null) return c;
        }
        return null;
    }

    Collider Sobreposto(Caixa k, float margem, bool soCorpos)
    {
        Vector3 meia = k.meia + Vector3.one * margem;
        if (meia.x <= 0f || meia.y <= 0f || meia.z <= 0f) return null;
        int n = Physics.OverlapBoxNonAlloc(k.centro, meia, encontrados, k.rotacao, obstaculos, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = encontrados[i];
            if (ignorar.Contains(c)) continue;
            bool personagem = c is CharacterController;
            bool corpo = c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic && !personagem;
            if (personagem && empurrarPersonagens) continue;   // tratados em AbrirCaminho
            if (soCorpos ? corpo : !corpo) return c;
        }
        return null;
    }

    /// <summary>
    /// Afasta os personagens que esta abertura alcançaria. 1: caminho livre; 0: falta orçamento de empurrão
    /// neste frame (a folha espera); -1: um personagem está preso e não sai (a folha para).
    /// </summary>
    int AbrirCaminho(float graus, ref float orcamento)
    {
        if (!empurrarPersonagens) return PersonagemNoCaminho(graus) ? -1 : 1;
        personagens.Clear();
        foreach (Caixa k in Caixas(graus))
        {
            int n = Physics.OverlapBoxNonAlloc(k.centro, k.meia + Vector3.one * (contato + 0.6f), encontrados, k.rotacao,
                obstaculos, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (encontrados[i] is CharacterController cc && cc.enabled && !personagens.Contains(cc)) personagens.Add(cc);
        }
        foreach (CharacterController cc in personagens)
        {
            Vector3 empurrao = Vector3.zero;
            foreach (Caixa k in Caixas(graus)) empurrao += Necessario(cc, k, empurrao);
            float pedido = empurrao.magnitude;
            if (pedido < 1e-5f) continue;
            // Sem orçamento para o passo inteiro: afasta o que der agora e a folha avança no próximo frame,
            // acompanhando o personagem na velocidade de empurrão.
            bool parcial = pedido > orcamento;
            float passo = parcial ? orcamento : pedido;
            if (passo < 1e-5f) return 0;
            Vector3 antes = cc.transform.position;
            cc.Move(empurrao / pedido * passo);   // colide com paredes e móveis; não mexe na velocidade vertical do jogador
            Vector3 andou = cc.transform.position - antes;
            orcamento -= passo;
            if (Vector3.Dot(andou, empurrao / pedido) < passo - toleranciaPreso) return -1;
            if (parcial) return 0;
        }
        return 1;
    }

    bool PersonagemNoCaminho(float graus)
    {
        foreach (Caixa k in Caixas(graus))
        {
            int n = Physics.OverlapBoxNonAlloc(k.centro, k.meia + Vector3.one * contato, encontrados, k.rotacao, obstaculos,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (encontrados[i] is CharacterController) return true;
        }
        return false;
    }

    /// <summary>
    /// Deslocamento horizontal que tira a cápsula do personagem de dentro do volume da folha mais a distância
    /// de contato (já descontado o que outra caixa da folha pediu).
    /// </summary>
    Vector3 Necessario(CharacterController cc, Caixa k, Vector3 jaPedido)
    {
        Vector3 up = cc.transform.up;
        Vector3 centro = cc.transform.TransformPoint(cc.center) + jaPedido;
        float raio = cc.radius * Mathf.Max(Mathf.Abs(cc.transform.lossyScale.x), Mathf.Abs(cc.transform.lossyScale.z));
        float meioEixo = Mathf.Max(0f, cc.height * Mathf.Abs(cc.transform.lossyScale.y) * 0.5f - raio);
        Quaternion inv = Quaternion.Inverse(k.rotacao);
        float melhor = float.MaxValue;
        Vector3 dir = Vector3.zero;
        for (int i = 0; i <= 6; i++)   // pontos do eixo da cápsula
        {
            Vector3 p = centro + up * Mathf.Lerp(-meioEixo, meioEixo, i / 6f);
            Vector3 local = inv * (p - k.centro);
            Vector3 maisPerto = Vector3.Max(-k.meia, Vector3.Min(k.meia, local));
            Vector3 fora = local - maisPerto;
            float d = fora.magnitude;
            if (d < 1e-5f)   // eixo dentro da caixa: sai pela face mais próxima
            {
                Vector3 folgaFace = k.meia - new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                fora = folgaFace.x < folgaFace.z ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z));
                d = -Mathf.Min(folgaFace.x, folgaFace.z);
            }
            if (d < melhor) { melhor = d; dir = k.rotacao * fora.normalized; }
        }
        float falta = raio + contato - melhor;
        if (falta <= 0f) return Vector3.zero;
        dir = Vector3.ProjectOnPlane(dir, up);
        if (dir.sqrMagnitude < 1e-6f) return Vector3.zero;   // só por cima / por baixo: nada a empurrar
        return dir.normalized * falta;
    }

    void Tocar(AudioClip clip)
    {
        if (fonte == null || clip == null) return;
        fonte.Stop();   // um som por vez: interações repetidas não empilham
        fonte.clip = clip;
        fonte.Play();
    }

    void OnValidate()
    {
        sentido = sentido < 0 ? -1 : 1;
    }
}
