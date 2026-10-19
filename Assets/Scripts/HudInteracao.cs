using UnityEngine;

/// <summary>
/// Indicações de tela do jogo, deliberadamente mínimas: um texto discreto, sem painel, e sem dizer o que o
/// objeto é nem o que a ação fará.
/// </summary>
public static class HudInteracao
{
    static GUIStyle estilo, sombra;
    static int tamanho = -1;

    static float Escala => Mathf.Max(0.6f, Screen.height / 1080f);

    static void Estilos()
    {
        int t = Mathf.RoundToInt(17 * Escala);
        if (estilo != null && t == tamanho) return;
        tamanho = t;
        estilo = new GUIStyle { fontSize = t, alignment = TextAnchor.MiddleCenter };
        estilo.normal.textColor = new Color(1f, 1f, 1f, 0.78f);
        sombra = new GUIStyle(estilo);
        sombra.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
    }

    /// <summary>Texto centralizado com sombra leve, a <paramref name="altura"/> (0..1) da tela.</summary>
    public static void Texto(string texto, float altura, float alfa = 1f)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(texto)) return;
        Estilos();
        var r = new Rect(0, Screen.height * altura, Screen.width, 30 * Escala);
        var antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfa);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), texto, sombra);
        GUI.Label(r, texto, estilo);
        GUI.color = antes;
    }

    /// <summary>"Aperte E para interagir", logo abaixo da mira.</summary>
    public static void Interagir(string tecla) => Texto("Aperte " + tecla.ToUpper() + " para interagir", 0.5f + 0.045f);
}
