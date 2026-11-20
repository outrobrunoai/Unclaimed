using UnityEngine;

/// <summary>
/// A câmera do celular (o app "Câmera" do Celular usa este componente; fica no Player, cena Jogabilidade).
/// Tira a foto pelo visor do CelularNaMao, com o flash do celular: ilumina a sala por um instante e recarrega
/// em alguns segundos. O flash também serve de luz no apagão (RevelarComFlash, TravaDeSeguranca) e chama a
/// atenção de Daniel na caça.
/// A foto vai para o AlbumDeFotos (app Galeria); se um AlvoFotografico estiver no quadro, coleta a evidência.
/// </summary>
public class CameraFotografica : MonoBehaviour
{
    [Header("Flash")]
    [Tooltip("Luz do flash. Vazio = cria um spot na câmera principal.")]
    public Light flash;
    [Min(0f)] public float intensidade = 9f;
    [Min(1f)] public float alcance = 14f;
    [Min(0.02f)] public float duracao = 0.18f;
    [Min(0.2f)] public float recarga = 3f;

    [Header("Som")]
    public AudioSource fonte;
    public AudioClip somDisparo, somRecarregado;

    /// <summary>Hora (Time.time) do último flash e de onde ele saiu.</summary>
    public static float UltimoFlash { get; private set; } = -999f;
    public static Vector3 OrigemDoFlash { get; private set; }

    /// <summary>Disparado depois de cada foto (alvo pode ser null).</summary>
    public static event System.Action<AlvoFotografico> AoFotografar;

    float prontoEm, apagarEm = -1f;
    bool avisouRecarga = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        UltimoFlash = -999f;
        AoFotografar = null;
    }

    public bool FlashPronto => Time.time >= prontoEm;
    public float SegundosParaRecarregar => Mathf.Max(0f, prontoEm - Time.time);

    void Start()
    {
        AlbumDeFotos.SincronizarComFatos();
        GarantirFlash();
    }

    void GarantirFlash()
    {
        if (flash != null) { flash.enabled = false; return; }
        Transform olhos = Jogador.Olhos;
        if (olhos == null) return;
        var go = new GameObject("Flash do celular");
        go.transform.SetParent(olhos, false);
        go.transform.localPosition = new Vector3(0.03f, -0.06f, 0.15f);
        flash = go.AddComponent<Light>();
        flash.type = LightType.Spot;
        flash.spotAngle = 100f;
        flash.innerSpotAngle = 60f;
        flash.color = new Color(0.95f, 0.97f, 1f);
        flash.shadows = LightShadows.None;
        flash.enabled = false;
    }

    void Update()
    {
        if (!avisouRecarga && FlashPronto)
        {
            avisouRecarga = true;
            Tocar(somRecarregado);
        }
        if (flash != null && flash.enabled)
        {
            float e = 1f - Mathf.Clamp01((apagarEm - Time.time) / duracao);
            flash.intensity = Mathf.Lerp(intensidade, 0f, e);
            if (Time.time >= apagarEm) flash.enabled = false;
        }
    }

    /// <summary>Tira a foto pelo visor do celular. Devolve o alvo enquadrado (ou null).</summary>
    public AlvoFotografico Fotografar(CelularNaMao celular)
    {
        bool comFlash = FlashPronto;
        Camera visor = celular != null ? celular.Visor : Camera.main;
        AlvoFotografico alvo = MelhorAlvo(visor);
        if (comFlash) Flash();
        Tocar(somDisparo);

        // A luz do flash já está ligada neste quadro: a foto sai iluminada.
        Texture2D imagem = celular != null ? celular.Capturar() : null;
        bool nova = alvo != null && AlbumDeFotos.Registrar(alvo.id, alvo.Variante, alvo.legenda, alvo.idEvidencia, imagem);
        if (alvo == null) AlbumDeFotos.Registrar(null, 0, "Foto", null, imagem);
        MiraDeInteracao.Avisar(alvo == null ? "Foto salva na galeria." : nova ? "Foto: " + alvo.legenda + "." : "Outra foto: " + alvo.legenda + ".", 2f);
        AoFotografar?.Invoke(alvo);
        return alvo;
    }

    void Flash()
    {
        GarantirFlash();
        if (flash == null) return;
        flash.enabled = true;
        flash.range = alcance;
        flash.intensity = intensidade;
        apagarEm = Time.time + duracao;
        UltimoFlash = Time.time;
        OrigemDoFlash = flash.transform.position;
        prontoEm = Time.time + recarga;
        avisouRecarga = false;
    }

    static AlvoFotografico MelhorAlvo(Camera cam)
    {
        AlvoFotografico melhor = null;
        float nota = 0f;
        foreach (AlvoFotografico a in AlvoFotografico.Ativos)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            float n = a.Enquadramento(cam);
            if (n > nota) { nota = n; melhor = a; }
        }
        return melhor;
    }

    void Tocar(AudioClip clip)
    {
        if (clip == null) return;
        if (fonte != null) fonte.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 0.8f);
    }
}
