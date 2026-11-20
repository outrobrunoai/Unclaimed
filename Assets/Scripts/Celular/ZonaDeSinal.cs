using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área com sinal de celular (por causa da chuva, só a recepção e o pátio têm sinal). Bruno espalha no mapa:
/// um objeto com um Collider (caixa, esfera ou cápsula; pode ser trigger) cobrindo a área. Sem Collider, vale
/// uma esfera de raio "raio" em volta do objeto.
/// O celular pergunta Sinal.Barras: a maior força entre as zonas em que o jogador está.
/// </summary>
public class ZonaDeSinal : MonoBehaviour
{
    [Range(1, 4)] public int barras = 3;
    [Tooltip("Usado só quando o objeto não tem Collider.")]
    [Min(0.5f)] public float raio = 4f;

    static readonly List<ZonaDeSinal> zonas = new List<ZonaDeSinal>();
    public static IReadOnlyList<ZonaDeSinal> Zonas => zonas;
    Collider area;

    void OnEnable()
    {
        area = GetComponent<Collider>();
        if (!zonas.Contains(this)) zonas.Add(this);
    }

    void OnDisable() => zonas.Remove(this);

    public bool Contem(Vector3 p)
    {
        if (area != null && area.enabled)
            return (area.ClosestPoint(p) - p).sqrMagnitude < 1e-6f;
        return (transform.position - p).sqrMagnitude <= raio * raio;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        var c = GetComponent<Collider>();
        if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        else Gizmos.DrawWireSphere(transform.position, raio);
    }
}

/// <summary>Força do sinal onde o jogador está (0 = sem sinal).</summary>
public static class Sinal
{
    /// <summary>Para testes e depuração: força um valor (-1 = usar as zonas).</summary>
    public static int Forcado = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Forcado = -1;

    public static int BarrasEm(Vector3 p)
    {
        if (Forcado >= 0) return Forcado;
        int melhor = 0;
        foreach (ZonaDeSinal z in ZonaDeSinal.Zonas)
            if (z != null && z.barras > melhor && z.Contem(p)) melhor = z.barras;
        return melhor;
    }

    public static int Barras => BarrasEm(Jogador.Posicao);
    public static bool Tem => Barras > 0;
}
