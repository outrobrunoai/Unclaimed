using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Daniel na parte 4 (caça). Fica escondido até o confronto; depois persegue com NavMesh, pela visão e pelo som.
///   Visão: cone à frente, sem parede no caminho; no apagão enxerga menos (a lanterna denuncia o jogador).
///   Som: cada passo do jogador faz barulho num raio: correndo (Shift) longe, andando perto, agachado nada.
///   Escondido: o jogador fica invisível, a menos que Daniel o tenha visto entrar.
///   Captura: perto o bastante e vendo o jogador = fim imediato (volta ao CP04).
/// Fica no objeto de Daniel (modelo do Bruno) junto de um NavMeshAgent. Sem modelo, o prefab provisório de
/// Animator (Assets/Modelos/Daniel/Daniel.controller): float "Velocidade", bool "Confronto", trigger "Agarrar".
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DanielPerseguidor : MonoBehaviour
{
    public enum Estado { Inativo, Parado, Perseguindo, Investigando, Procurando, Saindo }
    public enum ReacaoAoBarulho { Nada, Investigar, Descobrir }

    [Header("Corpo")]
    [Tooltip("Objetos visíveis de Daniel (ligados só quando ele aparece).")]
    public GameObject corpo;
    public Animator animator;
    [Tooltip("Altura dos olhos acima do pé.")]
    public float alturaDosOlhos = 1.65f;

    [Header("Velocidades (o jogador anda a 4 e corre a 6,2)")]
    public float velocidadePerseguindo = 4.6f;
    public float velocidadeInvestigando = 3.2f;
    public float velocidadeProcurando = 2.2f;

    [Header("Percepção")]
    public float alcanceDaVisao = 18f;
    public float alcanceNoEscuro = 8f;
    [Range(10f, 180f)] public float anguloDaVisao = 120f;
    public float raioPassoCorrendo = 14f, raioPassoAndando = 5f, raioPassoAgachado = 0f;
    public float raioBarulhoDescobre = 6f, raioBarulhoOuve = 14f;
    public float distanciaDeCaptura = 1.3f;
    [Tooltip("Segundos de animação de agarrar antes da tela de captura.")]
    public float tempoDoAgarrar = 0.8f;
    [Tooltip("Segundos sem ver o jogador até desistir da última posição e procurar em volta.")]
    public float esquecerEm = 6f;
    public float tempoProcurando = 20f;

    [Header("Som")]
    public AudioSource fonte;
    public AudioClip falaConfronto, somCaptura;

    public Estado Atual { get; private set; } = Estado.Inativo;
    public float UltimaVezQueViu { get; private set; } = -999f;
    public static DanielPerseguidor Instancia { get; private set; }
    public static bool Capturando { get; private set; }

    NavMeshAgent agente;
    Vector3 ultimaPosicao;
    float procurandoDesde, proximoDestino;
    Movimento ouvindo;
    Esconderijo descoberto;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Instancia = null;
        Capturando = false;
    }

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        Instancia = this;
        Capturando = false;
        Mostrar(false);
    }

    void OnDisable()
    {
        if (Instancia == this) Capturando = false;
    }

    void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
            Capturando = false;
        }
        if (ouvindo != null) ouvindo.AoDarPasso -= AoPasso;
    }

    void Mostrar(bool sim)
    {
        if (corpo != null) corpo.SetActive(sim);
        if (agente != null) agente.enabled = sim;
    }

    /// <summary>Daniel aparece num ponto (porta do arquivo) e fica parado olhando o jogador.</summary>
    public void Aparecer(Vector3 posicao, Quaternion rotacao)
    {
        transform.SetPositionAndRotation(posicao, rotacao);
        Mostrar(true);
        if (NavMesh.SamplePosition(posicao, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agente.Warp(hit.position);
        descoberto = null;
        Atual = Estado.Parado;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool("Confronto", true);
        if (fonte != null && falaConfronto != null) fonte.PlayOneShot(falaConfronto);
    }

    /// <summary>O jogador ficou com o gravador: a caça começa.</summary>
    public void ComecarCaca()
    {
        if (Atual == Estado.Inativo) Mostrar(true);
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool("Confronto", false);
        descoberto = null;
        ultimaPosicao = Jogador.Posicao;
        UltimaVezQueViu = Time.time;
        Atual = Estado.Perseguindo;
    }

    /// <summary>O jogador entregou o gravador: Daniel o pega e vai embora pelo caminho de onde veio.</summary>
    public void IrEmbora()
    {
        descoberto = null;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool("Confronto", false);
        Atual = Estado.Saindo;
        Mostrar(false);
    }

    void Update()
    {
        if (ouvindo == null && Jogador.Movimento != null)
        {
            ouvindo = Jogador.Movimento;
            ouvindo.AoDarPasso += AoPasso;
        }
        if (animator != null && animator.isActiveAndEnabled) animator.SetFloat("Velocidade", agente.enabled ? agente.velocity.magnitude : 0f);
        if (Atual == Estado.Inativo || Atual == Estado.Parado || Atual == Estado.Saindo || TelaDeFinal.Ativa) return;
        if (!agente.enabled || !agente.isOnNavMesh) return;

        bool vendo = VeOJogador();
        if (vendo)
        {
            UltimaVezQueViu = Time.time;
            ultimaPosicao = Jogador.Posicao;
            Atual = Estado.Perseguindo;
        }

        // Escondido depois de ser visto entrando ou denunciado por barulho no microfone: ele vai até o esconderijo.
        Esconderijo esc = Esconderijo.Ocupado;
        if (descoberto != null && esc != descoberto) descoberto = null;
        bool sabeDoEsconderijo = esc != null && (UltimaVezQueViu >= Esconderijo.EntrouEm - 1f || esc == descoberto);
        if (sabeDoEsconderijo) { ultimaPosicao = Jogador.Posicao; Atual = Estado.Perseguindo; }

        float distancia = Vector3.Distance(transform.position, Jogador.Posicao);
        if (distancia <= distanciaDeCaptura && (vendo || sabeDoEsconderijo)) { Capturar(); return; }

        switch (Atual)
        {
            case Estado.Perseguindo:
                agente.speed = velocidadePerseguindo;
                agente.SetDestination(ultimaPosicao);
                if (!vendo && !sabeDoEsconderijo && Time.time - UltimaVezQueViu > esquecerEm) Investigar(ultimaPosicao);
                break;
            case Estado.Investigando:
                agente.speed = velocidadeInvestigando;
                if (!agente.pathPending && agente.remainingDistance < 0.6f) { Atual = Estado.Procurando; procurandoDesde = Time.time; }
                break;
            case Estado.Procurando:
                agente.speed = velocidadeProcurando;
                if (Time.time >= proximoDestino || (!agente.pathPending && agente.remainingDistance < 0.5f))
                {
                    // Primeiro em volta de onde perdeu o jogador; depois, ele "conhece o prédio" e chega mais perto.
                    Vector3 centro = Time.time - procurandoDesde < tempoProcurando ? ultimaPosicao : Jogador.Posicao;
                    float raio = Time.time - procurandoDesde < tempoProcurando ? 8f : 14f;
                    Vector3 alvo = centro + Random.insideUnitSphere * raio;
                    if (NavMesh.SamplePosition(alvo, out NavMeshHit hit, 4f, NavMesh.AllAreas)) agente.SetDestination(hit.position);
                    proximoDestino = Time.time + 7f;
                }
                break;
        }
    }

    void Investigar(Vector3 ponto)
    {
        if (Atual == Estado.Perseguindo && Time.time - UltimaVezQueViu < 1f) return;
        ultimaPosicao = ponto;
        Atual = Estado.Investigando;
        agente.SetDestination(ponto);
    }

    public static ReacaoAoBarulho ReagirAoBarulho(float distancia, bool escondido, float raioDescobre, float raioOuve)
    {
        if (distancia > raioOuve) return ReacaoAoBarulho.Nada;
        if (escondido && distancia <= raioDescobre) return ReacaoAoBarulho.Descobrir;
        return ReacaoAoBarulho.Investigar;
    }

    public void OuvirBarulho(Vector3 ponto)
    {
        if (Atual == Estado.Inativo || Atual == Estado.Parado || Atual == Estado.Saindo || TelaDeFinal.Ativa) return;
        float distancia = Vector3.Distance(transform.position, ponto);
        ReacaoAoBarulho reacao = ReagirAoBarulho(distancia, Esconderijo.JogadorEscondido, raioBarulhoDescobre, raioBarulhoOuve);
        if (reacao == ReacaoAoBarulho.Nada) return;
        if (reacao == ReacaoAoBarulho.Descobrir)
        {
            descoberto = Esconderijo.Ocupado;
            ultimaPosicao = ponto;
            Atual = Estado.Perseguindo;
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.SetDestination(ponto);
            return;
        }
        if (descoberto != null && descoberto == Esconderijo.Ocupado) return;
        if (agente != null && agente.enabled && agente.isOnNavMesh) Investigar(ponto);
    }

    void AoPasso(bool agachado)
    {
        if (Atual == Estado.Inativo || Atual == Estado.Parado || Atual == Estado.Saindo || ouvindo == null) return;
        float raio = agachado ? raioPassoAgachado : ouvindo.Correndo ? raioPassoCorrendo : raioPassoAndando;
        if (raio <= 0f) return;
        if ((Jogador.Posicao - transform.position).sqrMagnitude <= raio * raio) Investigar(Jogador.Posicao);
    }

    Vector3 Olhos => transform.position + Vector3.up * alturaDosOlhos;

    public bool VeOJogador()
    {
        if (Esconderijo.JogadorEscondido) return false;
        Transform alvo = Jogador.Olhos;
        if (alvo == null) return false;
        Vector3 para = alvo.position - Olhos;
        // No escuro ele enxerga pouco, a não ser que a lanterna do celular esteja acesa.
        float alcance = RedeEletrica.Ligado(RedeEletrica.Geral) || Celular.LanternaLigada ? alcanceDaVisao : alcanceNoEscuro;
        if (para.magnitude > alcance) return false;
        if (Vector3.Angle(transform.forward, para) > anguloDaVisao * 0.5f) return false;
        if (Physics.Raycast(Olhos, para.normalized, out RaycastHit acerto, para.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
        {
            Movimento m = acerto.collider.GetComponentInParent<Movimento>();
            if (m == null) return false;
        }
        return true;
    }

    void Capturar()
    {
        descoberto = null;
        Atual = Estado.Parado;
        if (agente.enabled) agente.isStopped = true;
        if (somCaptura != null) AudioSource.PlayClipAtPoint(somCaptura, transform.position);
        if (animator != null && animator.isActiveAndEnabled)
        {
            // Agarra o jogador e só então a tela final (que pausa o jogo) entra.
            Capturando = true;
            Vector3 olhar = Jogador.Posicao - transform.position; olhar.y = 0f;
            if (olhar.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(olhar);
            animator.SetTrigger("Agarrar");
            if (Jogador.Movimento != null) Jogador.Movimento.enabled = false;
            StartCoroutine(CapturaDepois(tempoDoAgarrar));
        }
        else TelaDeFinal.MostrarCaptura();
    }

    System.Collections.IEnumerator CapturaDepois(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        TelaDeFinal.MostrarCaptura();
        Capturando = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, alcanceDaVisao);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioPassoCorrendo);
    }
}
