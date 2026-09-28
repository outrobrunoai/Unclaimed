using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Diário de pistas. Coloque UM destes em um GameObject vazio da cena.
/// Cada ObjetoInspecionavel se registra aqui na primeira vez que é examinado.
/// Tab abre/fecha o diário. Interface feita com IMGUI (sem Canvas).
/// </summary>
public class DiarioDePistas : MonoBehaviour
{
    [Serializable]
    public class Entrada
    {
        public string nome;
        public string descricao;
        public bool lida;
    }

    public static DiarioDePistas Instancia { get; private set; }
    public static bool Aberto => Instancia != null && Instancia.aberto;
    public static int Total => Instancia != null ? Instancia.entradas.Count : 0;

    [Header("Controles")]
    public string teclaDiario = "Tab";
    [Tooltip("Se marcado, o jogo congela enquanto o diário está aberto.")]
    public bool pausarJogoAoAbrir = false;
    [Tooltip("Movimento do jogador, olhar com o mouse, lanterna etc.")]
    public Behaviour[] desativarComDiarioAberto;

    [Header("Eventos")]
    [Tooltip("Ex.: tocar um som de anotação.")]
    public UnityEvent aoRegistrarPista;
    public UnityEvent aoAbrirDiario;
    public UnityEvent aoFecharDiario;

    readonly List<Entrada> entradas = new List<Entrada>();
    bool aberto;
    int selecionado = -1;
    Vector2 scrollLista, scrollTexto;
    string avisoTexto;
    float avisoAte;

    CursorLockMode travaAnterior;
    bool cursorVisivelAnterior;
    bool pausou;
    float timeScaleAnterior = 1f;
    readonly List<Behaviour> desativados = new List<Behaviour>();

    GUIStyle sTitulo, sTexto, sBotao, sBotaoSel, sAviso, sRodape;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // ---------- API pública ----------

    /// <summary>Chamado pelas pistas. Ignora se a pista já está no diário.</summary>
    public static void Registrar(string nome, string descricao)
    {
        if (Instancia == null)
        {
            Debug.LogWarning("DiarioDePistas: não há nenhum DiarioDePistas na cena.");
            return;
        }
        Instancia.Adicionar(nome, descricao);
    }

    /// <summary>Útil para o futuro sistema de dedução: o jogador já achou esta pista?</summary>
    public static bool Tem(string nome)
    {
        return Instancia != null && Instancia.entradas.Exists(e => e.nome == nome);
    }

    void Adicionar(string nome, string descricao)
    {
        if (string.IsNullOrWhiteSpace(nome)) nome = "Pista sem nome";
        if (entradas.Exists(e => e.nome == nome)) return;

        entradas.Add(new Entrada { nome = nome, descricao = descricao });
        avisoTexto = $"Nova pista no diário: {nome}";
        avisoAte = Time.unscaledTime + 4f;
        aoRegistrarPista?.Invoke();
    }

    // ---------- Abrir / fechar ----------

    void Update()
    {
        if (TeclaApertada(teclaDiario))
        {
            if (aberto) Fechar();
            else if (!ObjetoInspecionavel.InspecionandoAlgo) Abrir();
        }
        else if (aberto && TeclaApertada("Escape"))
        {
            Fechar();
        }
    }

    void Abrir()
    {
        aberto = true;

        desativados.Clear();
        foreach (var comp in desativarComDiarioAberto)
        {
            if (comp != null && comp.enabled)
            {
                comp.enabled = false;
                desativados.Add(comp);
            }
        }

        travaAnterior = Cursor.lockState;
        cursorVisivelAnterior = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        pausou = pausarJogoAoAbrir;
        if (pausou)
        {
            timeScaleAnterior = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (selecionado < 0 && entradas.Count > 0) Selecionar(entradas.Count - 1);

        aoAbrirDiario?.Invoke();
    }

    void Fechar()
    {
        aberto = false;

        foreach (var comp in desativados)
            if (comp != null) comp.enabled = true;
        desativados.Clear();

        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;

        if (pausou)
        {
            Time.timeScale = timeScaleAnterior;
            pausou = false;
        }

        aoFecharDiario?.Invoke();
    }

    void OnDisable()
    {
        if (aberto) Fechar();
    }

    void Selecionar(int i)
    {
        selecionado = i;
        entradas[i].lida = true;
        scrollTexto = Vector2.zero;
    }

    // ---------- Interface (IMGUI) ----------

    void IniciarEstilos()
    {
        if (sTitulo != null) return;

        sTitulo = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true };
        sTitulo.normal.textColor = new Color(0.95f, 0.85f, 0.6f);

        sTexto = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
        sTexto.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

        sBotao = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            padding = new RectOffset(12, 12, 10, 10)
        };

        sBotaoSel = new GUIStyle(sBotao) { fontStyle = FontStyle.Bold };
        sBotaoSel.normal.textColor = new Color(1f, 0.85f, 0.4f);
        sBotaoSel.hover.textColor = new Color(1f, 0.85f, 0.4f);

        sAviso = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleRight, wordWrap = true };
        sAviso.normal.textColor = Color.white;

        sRodape = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        sRodape.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
    }

    void OnGUI()
    {
        IniciarEstilos();

        // Aviso de "nova pista" no canto superior direito.
        if (!aberto && Time.unscaledTime < avisoAte)
            GUI.Label(new Rect(Screen.width - 520, 20, 500, 70), avisoTexto, sAviso);

        if (!aberto) return;

        float w = Mathf.Min(Screen.width * 0.85f, 1100f);
        float h = Mathf.Min(Screen.height * 0.8f, 700f);
        var painel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        // Escurece o fundo e desenha o painel.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.07f, 0.07f, 0.08f, 0.97f);
        GUI.DrawTexture(painel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(painel.x + 24, painel.y + 16, w - 48, h - 32));
        GUILayout.Label($"DIÁRIO DE PISTAS ({entradas.Count})", sTitulo);
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();

        // Lista de pistas (esquerda)
        GUILayout.BeginVertical(GUILayout.Width(w * 0.32f));
        scrollLista = GUILayout.BeginScrollView(scrollLista, GUILayout.Height(h - 130));
        for (int i = 0; i < entradas.Count; i++)
        {
            string rotulo = (entradas[i].lida ? "" : "(novo) ") + entradas[i].nome;
            if (GUILayout.Button(rotulo, i == selecionado ? sBotaoSel : sBotao))
                Selecionar(i);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(20);

        // Detalhes (direita)
        GUILayout.BeginVertical();
        if (selecionado >= 0 && selecionado < entradas.Count)
        {
            var e = entradas[selecionado];
            GUILayout.Label(e.nome, sTitulo);
            scrollTexto = GUILayout.BeginScrollView(scrollTexto, GUILayout.Height(h - 190));
            GUILayout.Label(string.IsNullOrEmpty(e.descricao) ? "(sem descrição)" : e.descricao, sTexto);
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("Nenhuma pista registrada ainda.\nExamine objetos pela sala para encontrá-las.", sTexto);
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.Label($"[{teclaDiario.ToUpper()}] ou [Esc] para fechar", sRodape);
        GUILayout.EndArea();
    }

    // ---------- Teste rápido ----------

    [ContextMenu("Testar: adicionar pista de teste")]
    void AdicionarPistaDeTeste()
    {
        int n = entradas.Count + 1;
        Adicionar($"Pista de teste {n}", "Texto de exemplo para testar o diário. Troque por uma descrição real depois.");
    }

    // ---------- Entrada (Input System novo e antigo) ----------

    static bool TeclaApertada(string nomeTecla)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        if (!Enum.TryParse(nomeTecla, true, out Key k)) return false;
        return Keyboard.current[k].wasPressedThisFrame;
#else
        if (!Enum.TryParse(nomeTecla, true, out KeyCode k)) return false;
        return Input.GetKeyDown(k);
#endif
    }
}