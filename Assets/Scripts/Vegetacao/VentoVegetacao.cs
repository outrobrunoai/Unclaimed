using UnityEngine;

// Shader globals (not material properties):
// _VentoDirForca = (direction X, direction Z, base strength, wind time in seconds).
// _VentoRajada = (spatial scale in metres, speed in m/s, added strength, peak threshold).
// Gusts are sampled in world space by VegetacaoVento.hlsl, never as a global pulse.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class VentoVegetacao : MonoBehaviour
{
    public bool usarDirecaoDaChuva = true;
    public Vector2 direcao = new Vector2(1, 0);
    [Range(0, 1)] public float intensidade = 0.35f;
    [Range(0, 1)] public float rajadaIntensidade = 0.35f;
    [Min(0.1f)] public float rajadaEscala = 18f;
    [Min(0)] public float rajadaVelocidade = 3f;
    [Range(0, 1)] public float rajadaEspacamento = 0.65f;
    public bool tempoCongelado;
    public float tempoFixo;
    [Min(0)] public float escalaTempo = 1f;

    static readonly int DirForca = Shader.PropertyToID("_VentoDirForca");
    static readonly int Rajada = Shader.PropertyToID("_VentoRajada");
    static VentoVegetacao instancia;
    ChuvaLeve chuva;
    float tempo;
#if UNITY_EDITOR
    double ultimoTempoEditor;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        instancia = null;
        Shader.SetGlobalVector(DirForca, Vector4.zero);
        Shader.SetGlobalVector(Rajada, Vector4.zero);
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        ultimoTempoEditor = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.update += AtualizarEditor;
#endif
        Publicar();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!tempoCongelado) tempo += Time.deltaTime * Mathf.Max(0, escalaTempo);
        Publicar();
    }

#if UNITY_EDITOR
    void AtualizarEditor()
    {
        double agora = UnityEditor.EditorApplication.timeSinceStartup;
        if (!Application.isPlaying)
        {
            if (!tempoCongelado) tempo += (float)(agora - ultimoTempoEditor) * Mathf.Max(0, escalaTempo);
            Publicar();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
        ultimoTempoEditor = agora;
    }
#endif

    void Publicar()
    {
        if (!instancia || !instancia.isActiveAndEnabled) instancia = this;
        if (instancia != this) return; // Only one active publisher, even if accidentally duplicated.
        if (usarDirecaoDaChuva && !chuva) chuva = FindAnyObjectByType<ChuvaLeve>();
        Vector2 d = usarDirecaoDaChuva && chuva ? chuva.vento : direcao;
        d = d.sqrMagnitude > 0.000001f ? d.normalized : Vector2.right;
        Shader.SetGlobalVector(DirForca, new Vector4(d.x, d.y, Mathf.Clamp01(intensidade), tempoCongelado ? tempoFixo : tempo));
        Shader.SetGlobalVector(Rajada, new Vector4(Mathf.Max(0.1f, rajadaEscala), Mathf.Max(0, rajadaVelocidade),
            Mathf.Clamp01(rajadaIntensidade), Mathf.Clamp01(rajadaEspacamento)));
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= AtualizarEditor;
#endif
        if (instancia != this) return;
        instancia = null;
        Shader.SetGlobalVector(DirForca, Vector4.zero);
        Shader.SetGlobalVector(Rajada, Vector4.zero);
    }
}
