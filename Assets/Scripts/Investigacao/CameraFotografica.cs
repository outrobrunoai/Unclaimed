using UnityEngine;

/// <summary>
/// A câmera do celular (o app "Câmera" do Celular usa este componente; fica no Player, cena Jogabilidade).
/// Tira a foto pelo visor do CelularNaMao, sem flash: a foto usa apenas a luz do lugar.
/// A foto vai para o AlbumDeFotos (app Galeria); se um AlvoFotografico estiver no quadro, coleta a evidência.
/// </summary>
public class CameraFotografica : MonoBehaviour
{
    [Header("Som")]
    public AudioSource fonte;
    public AudioClip somDisparo;

    /// <summary>Disparado depois de cada foto (alvo pode ser null).</summary>
    public static event System.Action<AlvoFotografico> AoFotografar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => AoFotografar = null;

    void Start() => AlbumDeFotos.SincronizarComFatos();

    /// <summary>Verdadeiro quando o alvo tem a luz exigida para valer como evidência.</summary>
    public static bool ValeComoEvidencia(AlvoFotografico a) => a != null && a.TemLuz;

    /// <summary>Tira a foto pelo visor do celular. Devolve o alvo enquadrado (ou null).</summary>
    public AlvoFotografico Fotografar(CelularNaMao celular)
    {
        Camera visor = celular != null ? celular.Visor : Camera.main;
        AlvoFotografico alvo = MelhorAlvo(visor);
        Tocar(somDisparo);

        Texture2D imagem = celular != null ? celular.Capturar() : null;
        if (alvo != null && !ValeComoEvidencia(alvo))
        {
            AlbumDeFotos.Registrar(null, 0, "Foto escura", null, imagem);
            MiraDeInteracao.Avisar(alvo.avisoSemLuz, 3.5f);
            AoFotografar?.Invoke(null);
            return null;
        }
        bool nova = alvo != null && AlbumDeFotos.Registrar(alvo.id, alvo.Variante, alvo.legenda, alvo.idEvidencia, imagem);
        if (alvo == null) AlbumDeFotos.Registrar(null, 0, "Foto", null, imagem);
        MiraDeInteracao.Avisar(alvo == null ? "Foto salva na galeria." : nova ? "Foto: " + alvo.legenda + "." : "Outra foto: " + alvo.legenda + ".", 2f);
        AoFotografar?.Invoke(alvo);
        return alvo;
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
