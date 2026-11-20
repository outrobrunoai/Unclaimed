using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Volume de uma sala do mapa, com o nome combinado da lista de salas (Recepcao, Autopsia, CamaraFria, Tecnica,
/// Deposito, Arquivo, Descanso, WC, Corredor, CorredorFundos, Recebimento, Patio). Bruno coloca um por sala, com
/// um BoxCollider (trigger) cobrindo o espaço. O diretor de Daniel usa para saber onde o jogador está e para
/// não agir na sala dele nem nas vizinhas com porta aberta.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Sala : MonoBehaviour
{
    public string id = "Autopsia";
    [Tooltip("Salas ligadas a esta. Com a porta aberta (ou sem porta), a vizinha também fica protegida.")]
    public Vizinha[] vizinhas = new Vizinha[0];

    [System.Serializable]
    public class Vizinha
    {
        public string sala;
        [Tooltip("Porta entre as duas. Vazio = passagem aberta.")]
        public FolhaDePorta porta;
    }

    static readonly List<Sala> salas = new List<Sala>();
    public static IReadOnlyList<Sala> Todas => salas;
    Collider area;

    void OnEnable()
    {
        area = GetComponent<Collider>();
        if (!salas.Contains(this)) salas.Add(this);
    }

    void OnDisable() => salas.Remove(this);

    public bool Contem(Vector3 p) => area != null && (area.ClosestPoint(p) - p).sqrMagnitude < 1e-6f;

    public static Sala Buscar(string id)
    {
        foreach (Sala s in salas) if (s != null && s.id == id) return s;
        return null;
    }

    public static Sala Em(Vector3 p)
    {
        foreach (Sala s in salas) if (s != null && s.Contem(p)) return s;
        return null;
    }

    public static string IdEm(Vector3 p)
    {
        Sala s = Em(p);
        return s != null ? s.id : "";
    }

    /// <summary>Salas que o jogador está vendo agora: a dele e as vizinhas sem porta ou com a porta aberta.</summary>
    public static HashSet<string> Protegidas(Vector3 jogador)
    {
        var resultado = new HashSet<string>();
        Sala atual = Em(jogador);
        if (atual == null) return resultado;
        resultado.Add(atual.id);
        foreach (Vizinha v in atual.vizinhas)
            if (v != null && !string.IsNullOrEmpty(v.sala) && (v.porta == null || v.porta.Abertura > 5f))
                resultado.Add(v.sala);
        return resultado;
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;
        Gizmos.color = new Color(0.9f, 0.4f, 0.9f, 0.18f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
