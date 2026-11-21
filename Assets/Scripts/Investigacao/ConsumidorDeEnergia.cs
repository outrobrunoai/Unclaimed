using UnityEngine;

/// <summary>
/// Liga e desliga coisas do mapa conforme um circuito da RedeEletrica (Bruno coloca nas luzes):
///   circuito "geral": luzes do prédio; "exame": luminária da autópsia; "refrigeracao": compressores, sons.
///   Marque "inverter" nas luzes de emergência (acendem quando a luz geral apaga).
/// Luzes baked não apagam por código: as do circuito precisam ser Realtime ou Mixed.
/// </summary>
public class ConsumidorDeEnergia : MonoBehaviour
{
    public string circuito = RedeEletrica.Geral;
    [Tooltip("Liga quando o circuito está DESLIGADO (luz de emergência).")]
    public bool inverter;
    [Tooltip("Vazio = todas as Light deste objeto e dos filhos.")]
    public Light[] luzes = new Light[0];
    public LuminariaExame[] luminarias = new LuminariaExame[0];
    public GameObject[] objetos = new GameObject[0];
    public AudioSource[] sons = new AudioSource[0];

    void Awake()
    {
        if (luzes == null || luzes.Length == 0) luzes = GetComponentsInChildren<Light>(true);
    }

    void OnEnable()
    {
        RedeEletrica.AoMudar += AoMudar;
        Aplicar();
    }

    void OnDisable() => RedeEletrica.AoMudar -= AoMudar;

    void AoMudar(string id)
    {
        if (id == null || id == circuito) Aplicar();
    }

    public void Aplicar()
    {
        bool ligado = RedeEletrica.Ligado(circuito) != inverter;
        foreach (Light l in luzes) if (l != null) l.enabled = ligado;
        foreach (LuminariaExame lum in luminarias) if (lum != null) lum.Ligar(ligado);
        foreach (GameObject o in objetos) if (o != null) o.SetActive(ligado);
        foreach (AudioSource a in sons)
        {
            if (a == null) continue;
            if (ligado && !a.isPlaying) a.Play();
            else if (!ligado && a.isPlaying) a.Stop();
        }
    }

    /// <summary>Verdadeiro se a luz deve ficar acesa conforme o circuito do consumidor que a controla (ou a luz geral).</summary>
    public static bool DeveEstarLigada(Light luz)
    {
        if (luz == null) return false;
        foreach (ConsumidorDeEnergia c in FindObjectsByType<ConsumidorDeEnergia>())
            if (c.Controla(luz)) return RedeEletrica.Ligado(c.circuito) != c.inverter;
        return RedeEletrica.Ligado(RedeEletrica.Geral);
    }

    bool Controla(Light luz)
    {
        if (luzes != null && luzes.Length > 0)
        {
            foreach (Light l in luzes) if (l == luz) return true;
            return false;
        }
        return luz.transform.IsChildOf(transform);
    }
}
