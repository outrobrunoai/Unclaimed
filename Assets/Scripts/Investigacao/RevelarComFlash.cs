using UnityEngine;

/// <summary>
/// Mostra objetos só por alguns segundos depois de um flash da câmera por perto (ex.: a trava de segurança
/// dentro da câmara fria, no escuro). Fora disso eles ficam escondidos. Uma vez revelado, pode ficar visível
/// para sempre (manterDepoisDeVer).
/// </summary>
public class RevelarComFlash : MonoBehaviour
{
    [Tooltip("Objetos ligados enquanto o flash revela.")]
    public GameObject[] objetos = new GameObject[0];
    [Min(0.5f)] public float distancia = 6f;
    [Min(0.2f)] public float segundosVisivel = 4f;
    public bool manterDepoisDeVer = true;
    [Tooltip("Fato marcado na primeira vez (opcional).")]
    public string fatoAoRevelar = "";

    bool visto;

    void OnEnable() => Aplicar(visto && manterDepoisDeVer);

    void Update()
    {
        if (visto && manterDepoisDeVer) return;
        float desde = Time.time - CameraFotografica.UltimoFlash;
        bool perto = (CameraFotografica.OrigemDoFlash - transform.position).sqrMagnitude <= distancia * distancia;
        bool revela = desde >= 0f && desde <= segundosVisivel && perto;
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
