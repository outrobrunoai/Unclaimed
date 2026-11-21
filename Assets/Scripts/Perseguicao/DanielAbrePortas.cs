using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abre a porta fechada à frente de Daniel durante a caça para que o NavMeshAgent não atravesse folhas fechadas.
/// Não fecha portas depois e ignora portas trancadas.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DanielAbrePortas : MonoBehaviour
{
    [Min(0.2f)] public float alcance = 1.6f;          // distância horizontal até o centro da folha
    [Range(0f, 180f)] public float anguloAFrente = 75f; // só portas dentro deste meio-ângulo da direção em que ele anda
    [Min(0.05f)] public float intervalo = 0.15f;      // segundos entre buscas

    const float VelocidadeMinima = 0.2f;
    const float AberturaMaximaParaAbrir = 30f;

    NavMeshAgent agente;
    FolhaDePorta[] portas;
    float proximaBusca;
    readonly List<Renderer> renderers = new List<Renderer>();

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        AtualizarPortas();
    }

    void Update()
    {
        if (Time.time < proximaBusca) return;
        if (agente == null || !agente.enabled || !agente.isOnNavMesh) return;

        Vector3 direcao = Horizontal(agente.velocity);
        if (direcao.sqrMagnitude <= VelocidadeMinima * VelocidadeMinima)
            direcao = Horizontal(agente.desiredVelocity);
        if (direcao.sqrMagnitude <= VelocidadeMinima * VelocidadeMinima) return;

        proximaBusca = Time.time + intervalo;
        AbrirMaisProxima(direcao);
    }

    void AtualizarPortas()
    {
        portas = FindObjectsByType<FolhaDePorta>();
    }

    void GarantirListaDePortas()
    {
        if (portas == null)
        {
            AtualizarPortas();
            return;
        }
        for (int i = 0; i < portas.Length; i++)
        {
            if (portas[i] == null)
            {
                AtualizarPortas();
                return;
            }
        }
    }

    void AbrirMaisProxima(Vector3 direcao)
    {
        GarantirListaDePortas();

        Vector3 origem = transform.position;
        FolhaDePorta maisProxima = null;
        float menorDistSqr = float.PositiveInfinity;

        for (int i = 0; i < portas.Length; i++)
        {
            FolhaDePorta folha = portas[i];
            if (folha == null || !folha.isActiveAndEnabled) continue;

            Vector3 centro = CentroDaFolha(folha);
            if (!DeveAbrir(origem, direcao, centro, alcance, anguloAFrente,
                    folha.Abertura, folha.trancada, folha.DestinoAberta))
                continue;

            float distSqr = DistanciaHorizontalSqr(origem, centro);
            if (distSqr < menorDistSqr)
            {
                menorDistSqr = distSqr;
                maisProxima = folha;
            }
        }

        if (maisProxima != null) maisProxima.Abrir();
    }

    Vector3 CentroDaFolha(FolhaDePorta folha)
    {
        renderers.Clear();
        folha.GetComponentsInChildren<Renderer>(false, renderers);
        bool temBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled) continue;
            if (!temBounds)
            {
                bounds = r.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return temBounds ? bounds.center : folha.transform.position;
    }

    /// <summary>Indica se Daniel deve abrir uma porta com esta posição e estado.</summary>
    public static bool DeveAbrir(
        Vector3 daniel,
        Vector3 direcao,
        Vector3 porta,
        float alcance,
        float angulo,
        float abertura,
        bool trancada,
        bool destinoAberta)
    {
        if (trancada || destinoAberta || abertura >= AberturaMaximaParaAbrir) return false;
        if (alcance < 0f || angulo < 0f) return false;

        Vector2 dir = new Vector2(direcao.x, direcao.z);
        if (dir.sqrMagnitude <= 1e-8f) return false;

        Vector2 para = new Vector2(porta.x - daniel.x, porta.z - daniel.z);
        if (para.sqrMagnitude > alcance * alcance) return false;

        if (para.sqrMagnitude <= 1e-8f) return true;
        return Vector2.Angle(dir, para) <= angulo;
    }

    static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static float DistanciaHorizontalSqr(Vector3 a, Vector3 b)
    {
        float dx = b.x - a.x;
        float dz = b.z - a.z;
        return dx * dx + dz * dz;
    }
}
