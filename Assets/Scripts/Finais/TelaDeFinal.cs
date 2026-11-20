using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tela cheia que encerra a partida: o epílogo (ao sair do prédio) ou a captura por Daniel (com a opção de voltar
/// ao último checkpoint). Para o tempo, solta o cursor e desliga o jogador. Criada sob demanda, fora das cenas.
/// </summary>
public class TelaDeFinal : MonoBehaviour
{
    public static bool Ativa => instancia != null;
    static TelaDeFinal instancia;

    const float DuracaoFadePreto = 1.5f;
    const float DuracaoTitulo = 1.0f;
    const float AtrasoSubtitulo = 0.5f;
    const float DuracaoSubtitulo = 0.7f;
    const float AtrasoLinha = 0.5f;
    const float DuracaoLinha = 0.8f;
    const float DuracaoParagrafo = 0.9f;
    const float AtrasoBotoes = 0.8f;
    const float DuracaoBotoes = 0.6f;

    string titulo;
    string subtitulo;
    List<string> paragrafos;
    bool captura;
    Vector2 rolagem;
    float abertaEm;
    float inicioConteudo;
    float proximoInicio;
    float inicioBotoes;
    float ultimoRepaint;
    int atual;
    float[] inicioParagrafo;
    bool[] completo;
    bool autoRolar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => instancia = null;

    /// <summary>Mostra o final montado pelo que saiu do prédio.</summary>
    public static void MostrarEpilogo()
    {
        Epilogo.Textos textos = Epilogo.CarregarTextos();
        Mostrar(textos.titulo, textos.subtitulo, Epilogo.Paragrafos(Epilogo.DaNoiteAtual(), textos), false);
    }

    /// <summary>Daniel alcançou o jogador: fim imediato, com volta ao último ponto salvo.</summary>
    public static void MostrarCaptura()
    {
        Epilogo.Textos textos = Epilogo.CarregarTextos();
        var lista = new List<string>();
        if (!string.IsNullOrWhiteSpace(textos.capturaTexto)) lista.Add(textos.capturaTexto);
        Mostrar(textos.capturaTitulo, null, lista, true);
    }

    static void Mostrar(string titulo, string subtitulo, List<string> paragrafos, bool captura)
    {
        if (instancia != null) return;
        var go = new GameObject("Tela de final");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<TelaDeFinal>();
        instancia.titulo = titulo ?? "";
        instancia.subtitulo = subtitulo;
        instancia.paragrafos = paragrafos ?? new List<string>();
        instancia.captura = captura;
        instancia.abertaEm = Time.unscaledTime;
        instancia.inicioConteudo = instancia.abertaEm + DuracaoFadePreto;
        instancia.ultimoRepaint = instancia.abertaEm;
        instancia.atual = -1;
        int qtd = instancia.paragrafos.Count;
        instancia.inicioParagrafo = new float[qtd];
        instancia.completo = new bool[qtd];
        bool temSub = !string.IsNullOrWhiteSpace(subtitulo);
        float inicioPrimeiro = instancia.inicioConteudo + (temSub ? 1.4f : 1.1f);
        instancia.proximoInicio = qtd > 0 ? inicioPrimeiro : float.PositiveInfinity;
        instancia.inicioBotoes = qtd == 0 ? inicioPrimeiro : float.PositiveInfinity;
        foreach (Movimento m in FindObjectsByType<Movimento>()) m.enabled = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        float agora = Time.unscaledTime;
        if (agora < inicioConteudo) return;

        if (atual + 1 < paragrafos.Count && agora >= proximoInicio)
            IniciarProximoParagrafo(agora);

        if (AvancarApertado())
            Avancar(agora);
    }

    void IniciarProximoParagrafo(float agora)
    {
        atual++;
        inicioParagrafo[atual] = agora;
        completo[atual] = false;
        if (atual + 1 < paragrafos.Count)
        {
            int chars = paragrafos[atual] != null ? paragrafos[atual].Length : 0;
            float espera = Mathf.Min(6f, 1.2f + 0.035f * chars);
            proximoInicio = agora + espera;
        }
        else
        {
            proximoInicio = float.PositiveInfinity;
            inicioBotoes = agora + DuracaoParagrafo + AtrasoBotoes;
        }
        autoRolar = true;
    }

    void Avancar(float agora)
    {
        if (atual < 0)
        {
            if (paragrafos.Count > 0) IniciarProximoParagrafo(agora);
            else inicioBotoes = Mathf.Min(inicioBotoes, agora);
            return;
        }

        bool entrando = !completo[atual] && (agora - inicioParagrafo[atual] < DuracaoParagrafo);
        if (entrando)
        {
            completo[atual] = true;
            if (atual + 1 < paragrafos.Count)
                proximoInicio = Mathf.Min(proximoInicio, agora + 0.15f);
            else
                inicioBotoes = Mathf.Min(inicioBotoes, agora + AtrasoBotoes);
        }
        else if (atual + 1 < paragrafos.Count)
        {
            IniciarProximoParagrafo(agora);
        }
    }

    static bool AvancarApertado()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        bool tecla = k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
        Mouse m = Mouse.current;
        bool clique = m != null && m.leftButton.wasPressedThisFrame;
        return tecla || clique;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
#endif
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
        float agora = Time.unscaledTime;
        float s = DesenhoUi.S;
        float alfa = Mathf.Clamp01((agora - abertaEm) / DuracaoFadePreto);
        DesenhoUi.Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, alfa));
        if (alfa < 1f) return;

        float t = agora - inicioConteudo;
        float w = Mathf.Min(780 * s, Screen.width - 80 * s);   // coluna estreita, como uma página
        float x = (Screen.width - w) / 2f;

        float eTitulo = Suave(t / DuracaoTitulo);
        Color corTitulo = captura ? new Color(0.85f, 0.2f, 0.15f, eTitulo) : new Color(1f, 1f, 1f, eTitulo);
        float yTitulo = 84 * s + (1f - eTitulo) * 16 * s;
        GUI.Label(new Rect(x, yTitulo, w, 60 * s), titulo, DesenhoUi.EstiloMaquina(42, corTitulo, TextAnchor.UpperCenter));

        bool temSub = !string.IsNullOrWhiteSpace(subtitulo);
        if (temSub)
        {
            float eSub = Suave((t - AtrasoSubtitulo) / DuracaoSubtitulo);
            if (eSub > 0.001f)
            {
                GUI.Label(new Rect(x, 146 * s, w, 30 * s), subtitulo,
                    DesenhoUi.EstiloMaquina(17, new Color(0.62f, 0.62f, 0.6f, eSub), TextAnchor.UpperCenter));
            }
        }

        float yLinha = temSub ? 186 * s : 156 * s;
        float eLinha = Suave((t - AtrasoLinha) / DuracaoLinha);
        if (eLinha > 0.001f)
        {
            float larguraLinha = w * eLinha;
            float xLinha = x + (w - larguraLinha) * 0.5f;
            Color corLinha = captura ? new Color(0.75f, 0.2f, 0.15f, 0.55f * eLinha) : new Color(0.75f, 0.75f, 0.72f, 0.35f * eLinha);
            DesenhoUi.Caixa(new Rect(xLinha, yLinha, larguraLinha, Mathf.Max(1f, 1.5f * s)), corLinha);
        }

        float yArea = yLinha + 26 * s;
        Rect area = new Rect(x, yArea, w, Mathf.Max(80 * s, Screen.height - yArea - 130 * s));

        if (Event.current.type == EventType.ScrollWheel) autoRolar = false;

        if (atual >= 0)
        {
            var estiloMedida = DesenhoUi.EstiloMaquina(21, Color.white);
            float larguraTexto = w - 30 * s;
            float total = 0f;
            for (int i = 0; i <= atual; i++)
                total += estiloMedida.CalcHeight(new GUIContent(paragrafos[i]), larguraTexto) + 22 * s;
            total += 12 * s;

            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Clamp(agora - ultimoRepaint, 0f, 0.1f);
                ultimoRepaint = agora;
                if (autoRolar)
                {
                    float alvo = Mathf.Max(0f, total - area.height);
                    rolagem.y = Mathf.Lerp(rolagem.y, alvo, 1f - Mathf.Exp(-8f * dt));
                    if (Mathf.Abs(rolagem.y - alvo) < 0.5f)
                    {
                        rolagem.y = alvo;
                        autoRolar = false;
                    }
                }
            }

            Vector2 antes = rolagem;
            rolagem = GUI.BeginScrollView(area, rolagem, new Rect(0, 0, larguraTexto, total));
            if (Event.current.type != EventType.Repaint && Mathf.Abs(rolagem.y - antes.y) > 0.5f) autoRolar = false;

            float y = 0f;
            for (int i = 0; i <= atual; i++)
            {
                float p = completo[i] ? 1f : Mathf.Clamp01((agora - inicioParagrafo[i]) / DuracaoParagrafo);
                float e = Suave(p);
                var estiloP = DesenhoUi.EstiloMaquina(21, new Color(0.88f, 0.88f, 0.86f, e));
                float h = estiloP.CalcHeight(new GUIContent(paragrafos[i]), larguraTexto);
                GUI.Label(new Rect(0, y + (1f - e) * 10 * s, larguraTexto, h), paragrafos[i], estiloP);
                y += h + 22 * s;
            }
            GUI.EndScrollView();
        }
        else if (Event.current.type == EventType.Repaint)
        {
            ultimoRepaint = agora;
        }

        if (agora < inicioBotoes) return;

        float alfaBotoes = Suave((agora - inicioBotoes) / DuracaoBotoes);
        float by = Screen.height - 110 * s;
        if (captura && Salvamento.ExisteSave)
        {
            if (BotaoComFade(new Rect(x, by, 380 * s, 54 * s), "Voltar ao último ponto salvo", alfaBotoes, 18))
            {
                Fechar();
                if (!Salvamento.Continuar()) SceneManager.LoadScene("main");
            }
        }
        if (BotaoComFade(new Rect(x + w - 300 * s, by, 300 * s, 54 * s), "Menu principal", alfaBotoes, 18))
        {
            Fechar();
            SceneManager.LoadScene("main");
        }
    }

    // Só texto, como as escolhas da saída: mais claro e sublinhado com o mouse em cima.
    static bool BotaoComFade(Rect r, string texto, float alfa, float tamanho = 18f)
    {
        bool sobre = alfa > 0.1f && r.Contains(Event.current.mousePosition);
        GUIStyle estilo = DesenhoUi.EstiloMaquina(tamanho + 3f, sobre ? new Color(1f, 1f, 1f, alfa) : new Color(0.7f, 0.7f, 0.68f, alfa), TextAnchor.MiddleCenter);
        GUI.Label(r, texto, estilo);
        if (sobre)
        {
            float lt = estilo.CalcSize(new GUIContent(texto)).x;
            DesenhoUi.Caixa(new Rect(r.center.x - lt / 2f, r.center.y + estilo.fontSize * 0.7f, lt, Mathf.Max(1f, DesenhoUi.S * 1.5f)), new Color(1f, 1f, 1f, 0.8f * alfa));
        }
        return alfa > 0.1f && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    static float Suave(float x)
    {
        float c = Mathf.Clamp01(x);
        return 1f - (1f - c) * (1f - c) * (1f - c);
    }
}
