using UnityEngine;

/// <summary>
/// Mostra objetos enquanto a lanterna do celular os ilumina por perto e por alguns segundos depois
/// (ex.: a trava de segurança dentro da câmara fria, no escuro). Fora disso eles ficam escondidos.
/// Uma vez revelado, pode ficar visível para sempre (manterDepoisDeVer).
/// O nome da classe foi mantido por compatibilidade com prefabs existentes.
/// </summary>
public class RevelarComFlash : MonoBehaviour
{
    [Tooltip("Objetos ligados enquanto a lanterna revela.")]
    public GameObject[] objetos = new GameObject[0];
    [Min(0.5f)] public float distancia = 6f;
    [Min(0.2f)] public float segundosVisivel = 4f;
    public bool manterDepoisDeVer = true;
    [Tooltip("Fato marcado na primeira vez (opcional).")]
    public string fatoAoRevelar = "";

    bool visto;
    float visivelAte = float.NegativeInfinity;

    void OnEnable() => Aplicar(visto && manterDepoisDeVer);

    void Update() => Avaliar();

    /// <summary>Atualiza a visibilidade dos objetos de acordo com a lanterna do celular.</summary>
    public void Avaliar()
    {
        if (visto && manterDepoisDeVer) return;
        bool iluminado = Celular.LanternaIlumina(transform.position, distancia);
        if (iluminado) visivelAte = Time.time + segundosVisivel;
        bool revela = iluminado || Time.time <= visivelAte;
        if (revela && !visto)
        {
            visto = true;
            if (!string.IsNullOrEmpty(fatoAoRevelar)) EstadoDaNoite.Marcar(fatoAoRevelar);
        }
        Aplicar(revela || (visto && manterDepoisDeVer));
    }

    void Aplicar(bool mostrar)
    {
        foreach (GameObject o in objetos) if (o != null && o.activeSelf != mostrar) o.SetActive(mostrar);
    }
}
