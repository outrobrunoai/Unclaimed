using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tela cheia que encerra a partida: o epílogo (ao sair do prédio) ou a captura por Daniel (com a opção de voltar
/// ao último checkpoint). Para o tempo, solta o cursor e desliga o jogador. Criada sob demanda, fora das cenas.
/// </summary>
public class TelaDeFinal : MonoBehaviour
{
    public static bool Ativa => instancia != null;
    static TelaDeFinal instancia;

    string titulo;
    List<string> paragrafos;
    bool captura;
    Vector2 rolagem;
    float abertaEm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => instancia = null;

    /// <summary>Mostra o final montado pelo que saiu do prédio.</summary>
    public static void MostrarEpilogo()
    {
        Epilogo.Textos textos = Epilogo.CarregarTextos();
        Mostrar(textos.titulo, Epilogo.Paragrafos(Epilogo.DaNoiteAtual(), textos), false);
    }

    /// <summary>Daniel alcançou o jogador: fim imediato, com volta ao último ponto salvo.</summary>
    public static void MostrarCaptura() =>
        Mostrar("Ele alcançou você.", new List<string> { "Daniel não queria uma quarta morte. Você não deixou outra saída para ele." }, true);

    static void Mostrar(string titulo, List<string> paragrafos, bool captura)
    {
        if (instancia != null) return;
        var go = new GameObject("Tela de final");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<TelaDeFinal>();
        instancia.titulo = titulo;
        instancia.paragrafos = paragrafos;
        instancia.captura = captura;
        instancia.abertaEm = Time.unscaledTime;
        foreach (Movimento m in FindObjectsByType<Movimento>()) m.enabled = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Uma interface que fecha no mesmo quadro devolveria o controle ao jogador: a tela o retoma a cada quadro.
    void LateUpdate()
    {
        foreach (Movimento m in FindObjectsByType<Movimento>()) if (m.enabled) m.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Fechar()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        instancia = null;
        Destroy(gameObject);
    }

    void OnGUI()
    {
        useGUILayout = false;
        GUI.depth = -1000;
        float s = DesenhoUi.S;
        float alfa = Mathf.Clamp01((Time.unscaledTime - abertaEm) / 1.5f);
        DesenhoUi.Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, alfa));
        if (alfa < 1f) return;

        float w = Mathf.Min(1100 * s, Screen.width - 80 * s);
        float x = (Screen.width - w) / 2f;
        GUI.Label(new Rect(x, 90 * s, w, 60 * s), titulo, DesenhoUi.Estilo(40, captura ? new Color(0.85f, 0.2f, 0.15f) : Color.white, true));

        var estilo = DesenhoUi.Estilo(21, new Color(0.88f, 0.88f, 0.86f));
        float total = 0f;
        foreach (string p in paragrafos) total += estilo.CalcHeight(new GUIContent(p), w - 30 * s) + 22 * s;
        Rect area = new Rect(x, 180 * s, w, Screen.height - 320 * s);
        rolagem = GUI.BeginScrollView(area, rolagem, new Rect(0, 0, w - 30 * s, total));
        float y = 0f;
        foreach (string p in paragrafos)
        {
            float h = estilo.CalcHeight(new GUIContent(p), w - 30 * s);
            GUI.Label(new Rect(0, y, w - 30 * s, h), p, estilo);
            y += h + 22 * s;
        }
        GUI.EndScrollView();

        float by = Screen.height - 110 * s;
        if (captura && Salvamento.ExisteSave)
        {
            if (DesenhoUi.Botao(new Rect(x, by, 380 * s, 54 * s), "Voltar ao último ponto salvo", true, 18))
            {
                Fechar();
                if (!Salvamento.Continuar()) SceneManager.LoadScene("main");
            }
        }
        if (DesenhoUi.Botao(new Rect(x + w - 300 * s, by, 300 * s, 54 * s), "Menu principal", true, 18))
        {
            Fechar();
            SceneManager.LoadScene("main");
        }
    }
}
