using UnityEngine;

/// <summary>Controla juntas a luz de exame e a emissão da lente, sem criar materiais.</summary>
public class LuminariaExame : MonoBehaviour
{
    [SerializeField] Light luz;
    [SerializeField] Renderer lente;
    [SerializeField, ColorUsage(false, true)] Color emissaoLigada = new Color(2.4f, 2.25f, 2f);
    [SerializeField] bool ligadaAoIniciar = false;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock propriedades;

    public bool Ligada { get; private set; }

    public void Ligar(bool ligar)
    {
        Ligada = ligar;
        if (luz != null) luz.enabled = ligar;
        if (lente == null) return;
        if (propriedades == null) propriedades = new MaterialPropertyBlock();
        lente.GetPropertyBlock(propriedades);
        propriedades.SetColor(EmissionColor, ligar ? emissaoLigada : Color.black);
        lente.SetPropertyBlock(propriedades);
    }

    void Awake() => Ligar(ligadaAoIniciar);
    void OnValidate() => Ligar(ligadaAoIniciar);
}
