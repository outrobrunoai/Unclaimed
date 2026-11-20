using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Daniel na parte 4 (caça). Fica escondido até o confronto; depois persegue com NavMesh, pela visão e pelo som.
///   Visão: cone à frente, sem parede no caminho; no apagão enxerga menos (a lanterna denuncia o jogador).
///   Som: cada passo do jogador faz barulho num raio: correndo (Shift) longe, andando perto, agachado nada.
///   Escondido: o jogador fica invisível, a menos que Daniel o tenha visto entrar.
///   Captura: perto o bastante e vendo o jogador = fim imediato (volta ao CP04).
/// Fica no objeto de Daniel (modelo do Bruno) junto de um NavMeshAgent. Sem modelo, o prefab provisório de
/// José usa uma cápsula. Animator opcional: parâmetro float "Velocidade".
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DanielPerseguidor : MonoBehaviour
{
    public enum Estado { Inativo, Parado, Perseguindo, Investigando, Procurando, Saindo }

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
    public float distanciaDeCaptura = 1.3f;
    [Tooltip("Segundos sem ver o jogador até desistir da última posição e procurar em volta.")]
    public float esquecerEm = 6f;
    public float tempoProcurando = 20f;

    [Header("Som")]
    public AudioSource fonte;
    public AudioClip falaConfronto, somCaptura;

    public Estado Atual { get; private set; } = Estado.Inativo;
    public float UltimaVezQueViu { get; private set; } = -999f;
    public static DanielPerseguidor Instancia { get; private set; }

    NavMeshAgent agente;
    Vector3 ultimaPosicao;
    float procurandoDesde, proximoDestino;
    Movimento ouvindo;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        Instancia = this;
        Mostrar(false);
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
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
        Atual = Estado.Parado;
        if (fonte != null && falaConfronto != null) fonte.PlayOneShot(falaConfronto);
    }

    /// <summary>O jogador ficou com o gravador: a caça começa.</summary>
    public void ComecarCaca()
    {
        if (Atual == Estado.Inativo) Mostrar(true);
        ultimaPosicao = Jogador.Posicao;
        UltimaVezQueViu = Time.time;
        Atual = Estado.Perseguindo;
    }

    /// <summary>O jogador entregou o gravador: Daniel o pega e vai embora pelo caminho de onde veio.</summary>
    public void IrEmbora()
    {
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
        if (animator != null) animator.SetFloat("Velocidade", agente.enabled ? agente.velocity.magnitude : 0f);
        if (Atual == Estado.Inativo || Atual == Estado.Parado || Atual == Estado.Saindo || TelaDeFinal.Ativa) return;
        if (!agente.enabled || !agente.isOnNavMesh) return;

        bool vendo = VeOJogador();
        if (vendo)
        {
            UltimaVezQueViu = Time.time;
            ultimaPosicao = Jogador.Posicao;
            Atual = Estado.Perseguindo;
        }

        // Escondido depois de ser visto entrando: ele vai até o esconderijo.
        Esconderijo esc = Esconderijo.Ocupado;
        bool sabeDoEsconderijo = esc != null && UltimaVezQueViu >= Esconderijo.EntrouEm - 1f;
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
        Atual = Estado.Parado;
        if (agente.enabled) agente.isStopped = true;
        if (somCaptura != null) AudioSource.PlayClipAtPoint(somCaptura, transform.position);
        TelaDeFinal.MostrarCaptura();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, alcanceDaVisao);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioPassoCorrendo);
    }
}
