using UnityEngine;

/// <summary>
/// Troca materiais de renderers conforme um circuito da RedeEletrica está ligado ou desligado
/// (ex.: difusores emissivos das luminárias trocam para material apagado no apagão e voltam ao original
/// quando a luz retorna). Marque "inverter" para trocar quando o circuito está ligado.
/// </summary>
public class MaterialDoCircuito : MonoBehaviour
{
    public string circuito = RedeEletrica.Geral;
    [Tooltip("Troca quando o circuito está LIGADO (ex.: LED de emergência).")]
    public bool inverter;
    [System.Serializable] public class Troca { public Material ligado, desligado; }
    public Troca[] trocas = new Troca[0];
    [Tooltip("Vazio = todos os Renderer deste objeto e dos filhos (inclusive inativos).")]
    public Renderer[] renderers = new Renderer[0];

    Material[][] originais;

    void Awake() => Inicializar();

    void OnEnable()
    {
        RedeEletrica.AoMudar -= AoMudar;
        RedeEletrica.AoMudar += AoMudar;
        Aplicar();
    }

    void OnDisable() => RedeEletrica.AoMudar -= AoMudar;

    void AoMudar(string id)
    {
        if (id == null || id == circuito) Aplicar();
    }

    void Inicializar()
    {
        if (originais != null) return;
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        originais = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            if (mats == null) continue;
            var copia = new Material[mats.Length];
            for (int j = 0; j < mats.Length; j++) copia[j] = mats[j];
            originais[i] = copia;
        }
    }

    public void Aplicar()
    {
        Inicializar();
        if (renderers == null) return;
        bool ligado = RedeEletrica.Ligado(circuito) != inverter;

        for (int i = 0; i < originais.Length && i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Material[] orig = originais[i];
            if (r == null || orig == null) continue;

            if (ligado || trocas == null || trocas.Length == 0)
            {
                r.sharedMaterials = orig;
                continue;
            }

            var atual = new Material[orig.Length];
            for (int j = 0; j < orig.Length; j++)
            {
                Material m = orig[j];
                if (m != null)
                {
                    for (int k = 0; k < trocas.Length; k++)
                    {
                        Troca t = trocas[k];
                        if (t != null && t.ligado != null && m == t.ligado)
                        {
                            m = t.desligado;
                            break;
                        }
                    }
                }
                atual[j] = m;
            }
            r.sharedMaterials = atual;
        }
    }
}
