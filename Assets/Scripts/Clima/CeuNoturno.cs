using UnityEngine;

// Publishes the global _CeuEstado read by the skybox shader "Unclaimed/Ceu noturno":
// (wind direction X, time in seconds, wind direction Z, 1 = published).
// The wind direction follows ChuvaLeve.vento (ground wind) so clouds and rain drift the same way;
// the cloud speeds per layer are on the sky material. Runs in the Editor too, like VentoVegetacao.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class CeuNoturno : MonoBehaviour
{
    public bool usarDirecaoDaChuva = true;
    public Vector2 direcao = new Vector2(1f, 0.3f);
    [Min(0)] public float escalaTempo = 1f;
    [Tooltip("Tempo inicial (s): onde as nuvens começam.")] public float tempoInicial;
    public bool tempoCongelado;

    const float IntervaloBuscaChuva = 2f;
    static readonly int Estado = Shader.PropertyToID("_CeuEstado");
    ChuvaLeve chuva;
    float proximaBuscaChuva = float.NegativeInfinity;
    float tempo;
#if UNITY_EDITOR
    double ultimoTempoEditor;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Shader.SetGlobalVector(Estado, Vector4.zero);

    void OnEnable()
    {
        tempo = tempoInicial;
        proximaBuscaChuva = float.NegativeInfinity;
#if UNITY_EDITOR
        ultimoTempoEditor = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.update += AtualizarEditor;
#endif
        Publicar();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!tempoCongelado) tempo += Time.deltaTime * escalaTempo;
        Publicar();
    }

#if UNITY_EDITOR
    void AtualizarEditor()
    {
        double agora = UnityEditor.EditorApplication.timeSinceStartup;
        if (!Application.isPlaying && !tempoCongelado)
        {
            tempo += (float)(agora - ultimoTempoEditor) * escalaTempo;
            Publicar();
        }
        ultimoTempoEditor = agora;
    }
#endif

    /// <summary>Coloca as nuvens num instante (s) — usado em capturas e testes.</summary>
    public void DefinirTempo(float segundos)
    {
        tempo = segundos;
        Publicar();
    }

    void Publicar()
    {
        if (usarDirecaoDaChuva && !chuva && Time.realtimeSinceStartup >= proximaBuscaChuva)
        {
            chuva = FindAnyObjectByType<ChuvaLeve>();
            proximaBuscaChuva = Time.realtimeSinceStartup + IntervaloBuscaChuva;
        }
        Vector2 d = usarDirecaoDaChuva && chuva ? chuva.vento : direcao;
        d = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
        Shader.SetGlobalVector(Estado, new Vector4(d.x, tempo, d.y, 1f));
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= AtualizarEditor;
#endif
        Shader.SetGlobalVector(Estado, Vector4.zero);
    }
}
