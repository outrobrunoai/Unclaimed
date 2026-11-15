using UnityEngine;

/// <summary>
/// Controla o filtro de fita VHS (shader Unclaimed/FiltroVHS, aplicado pelo Full Screen Pass do PC_Renderer).
/// Coloque UM destes na cena. Fora do Play Mode o filtro fica desligado, para não atrapalhar a edição.
///
/// O visual (quanto de cada efeito) fica no material. Aqui ficam o liga/desliga, o tempo da animação
/// e as falhas: pequenas interferências espontâneas e os picos chamados pelos sustos, via FiltroVHS.Falha().
/// </summary>
public class FiltroVHS : MonoBehaviour
{
    static readonly int idAtivo = Shader.PropertyToID("_VHSAtivo");
    static readonly int idTempo = Shader.PropertyToID("_VHSTempo");
    static readonly int idFalha = Shader.PropertyToID("_VHSFalha");

    static FiltroVHS instancia;

    [Tooltip("Liga ou desliga o filtro durante o jogo.")]
    public bool ativo = true;

    [Header("Falhas espontâneas")]
    [Tooltip("Intervalo (s) entre interferências pequenas e aleatórias. Zero nos dois = sem falhas espontâneas.")]
    public Vector2 intervaloEntreFalhas = new Vector2(8f, 22f);
    [Tooltip("Força (0 a 1) das falhas espontâneas.")]
    public Vector2 forcaDasFalhas = new Vector2(0.15f, 0.45f);
    [Tooltip("Duração (s) das falhas espontâneas.")]
    public Vector2 duracaoDasFalhas = new Vector2(0.08f, 0.3f);

    float proximaFalha;
    float falhaForca, falhaInicio, falhaFim;

    /// <summary>
    /// Pico de interferência na imagem (ex.: Daniel aparece, porta bate). forca de 0 a 1; duracao em segundos.
    /// Um pico mais forte substitui o que estiver em andamento.
    /// </summary>
    public static void Falha(float forca, float duracao)
    {
        if (instancia == null) return;
        instancia.Disparar(forca, duracao);
    }

    void Disparar(float forca, float duracao)
    {
        float agora = Time.unscaledTime;
        if (agora < falhaFim && AtualDaFalha(agora) > forca) return;
        falhaForca = Mathf.Clamp01(forca);
        falhaInicio = agora;
        falhaFim = agora + Mathf.Max(0.02f, duracao);
    }

    float AtualDaFalha(float agora)
    {
        if (agora >= falhaFim) return 0f;
        float p = Mathf.InverseLerp(falhaInicio, falhaFim, agora);
        return falhaForca * (1f - p * p); // entra no máximo e some no fim
    }

    void OnEnable()
    {
        instancia = this;
        AgendarFalha();
    }

    void OnDisable()
    {
        if (instancia == this) instancia = null;
        Shader.SetGlobalFloat(idAtivo, 0f);
        Shader.SetGlobalFloat(idFalha, 0f);
    }

    void AgendarFalha()
    {
        if (intervaloEntreFalhas.y <= 0f) { proximaFalha = float.PositiveInfinity; return; }
        proximaFalha = Time.unscaledTime + Random.Range(intervaloEntreFalhas.x, intervaloEntreFalhas.y);
    }

    void Update()
    {
        float agora = Time.unscaledTime;
        if (agora >= proximaFalha)
        {
            Disparar(Random.Range(forcaDasFalhas.x, forcaDasFalhas.y), Random.Range(duracaoDasFalhas.x, duracaoDasFalhas.y));
            AgendarFalha();
        }

        Shader.SetGlobalFloat(idAtivo, ativo ? 1f : 0f);
        Shader.SetGlobalFloat(idTempo, agora % 1000f);
        Shader.SetGlobalFloat(idFalha, ativo ? AtualDaFalha(agora) : 0f);
    }
}
