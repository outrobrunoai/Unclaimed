using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Algo que vale fotografar (a pessoa na mesa, o corpo em C-02). A CameraFotografica escolhe o alvo mais
/// central que está no quadro, dentro da distância e sem parede no caminho. A foto coleta a evidência do alvo
/// (F01, F02...) e registra a variante atual.
///
/// Variantes: cada uma pode mostrar um conjunto de objetos (poses[variante]). Daniel troca a variante da mesa
/// (o braço em outra posição) quando o jogador não está olhando; duas fotos com variantes diferentes marcam
/// "mudou:&lt;id&gt;".
///
/// Quem coloca: Bruno, no mapa (ex.: id "mesa_autopsia" com F01; id "gaveta_c02" com F02).
/// </summary>
public class AlvoFotografico : MonoBehaviour
{
    [Tooltip("Nome combinado do alvo (ex.: mesa_autopsia, gaveta_c02).")]
    public string id = "mesa_autopsia";
    [Tooltip("Evidência coletada ao fotografar (ex.: F01). Vazio = só fica no álbum.")]
    public string idEvidencia = "F01";
    [Tooltip("Legenda da foto no álbum.")]
    public string legenda = "A pessoa na mesa de autópsia";
    [Tooltip("Ponto que precisa estar visível. Vazio = o próprio objeto.")]
    public Transform centro;
    [Min(0.5f)] public float distanciaMaxima = 7f;
    [Tooltip("Fração da tela, a partir do centro, em que o alvo precisa estar (1 = tela inteira).")]
    [Range(0.2f, 1f)] public float enquadramento = 0.8f;
    [Tooltip("Fato que precisa existir para o alvo valer (vazio = sempre).")]
    public string requerFato = "";

    [Header("Variantes")]
    [Tooltip("Um objeto por variante; só o da variante atual fica ativo. Pode ficar vazio.")]
    public GameObject[] poses = new GameObject[0];
    [SerializeField] int variante;

    static readonly List<AlvoFotografico> ativos = new List<AlvoFotografico>();
    public static IReadOnlyList<AlvoFotografico> Ativos => ativos;

    public Vector3 Ponto => centro != null ? centro.position : transform.position;

    public int Variante
    {
        get => variante;
        set
        {
            variante = Mathf.Max(0, value);
            AplicarPose();
        }
    }

    public static AlvoFotografico Buscar(string id)
    {
        foreach (AlvoFotografico a in ativos) if (a != null && a.id == id) return a;
        return null;
    }

    void OnEnable()
    {
        if (!ativos.Contains(this)) ativos.Add(this);
        AplicarPose();
    }

    void OnDisable() => ativos.Remove(this);

    void AplicarPose()
    {
        if (poses == null) return;
        for (int i = 0; i < poses.Length; i++)
            if (poses[i] != null) poses[i].SetActive(i == variante);
    }

    public bool Valido => string.IsNullOrEmpty(requerFato) || EstadoDaNoite.Tem(requerFato);

    /// <summary>Quão bem o alvo está enquadrado (0 = fora; 1 = no centro). Considera distância e paredes.</summary>
    public float Enquadramento(Camera cam)
    {
        if (cam == null || !Valido) return 0f;
        Vector3 p = Ponto;
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0.05f || v.z > distanciaMaxima) return 0f;
        float dx = Mathf.Abs(v.x - 0.5f) * 2f, dy = Mathf.Abs(v.y - 0.5f) * 2f;
        if (dx > enquadramento || dy > enquadramento) return 0f;
        // Nada entre a câmera e o alvo (o próprio alvo e seus filhos não contam).
        Vector3 origem = cam.transform.position;
        Vector3 dir = p - origem;
        if (Physics.Raycast(origem, dir.normalized, out RaycastHit acerto, dir.magnitude - 0.05f, ~(1 << 2), QueryTriggerInteraction.Ignore)
            && !acerto.transform.IsChildOf(transform))
            return 0f;
        return 1f - Mathf.Max(dx, dy) * 0.5f - v.z / distanciaMaxima * 0.25f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(Ponto, 0.12f);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(Ponto, distanciaMaxima);
    }
}
