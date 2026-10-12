using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Base visual e de entrada da estação: desenho com IMGUI, uma passada por quadro (no Repaint), e entrada lida do
// Input System (mouse, roda e teclas), guardada até o quadro ser desenhado. Medidas em unidades de uma tela de
// 1080 de altura (s = pixels por unidade). Cores em sintonia com Blender/Recepcao/scripts/estacao_interface.py.
public partial class ComputadorRecepcao
{
    // ------------------------------------------------------------------ tema

    static readonly Color CTexto = Hex("#1F2933"), CSecundario = Hex("#5B6672"), CFraco = Hex("#8792A0");
    static readonly Color CJanela = Hex("#F2F4F6"), CConteudo = Hex("#FFFFFF"), CBorda = Hex("#C3CAD3"), CBordaJanela = Hex("#7F8B99");
    static readonly Color CTitulo = Hex("#41709F"), CTituloTopo = Hex("#5582B0"), CTituloInativo = Hex("#B5BFCA"), CTituloInativoTxt = Hex("#F4F6F8");
    static readonly Color CAcento = Hex("#3A6EA5"), CSelecao = Hex("#D3E2F2"), CSelecaoInativa = Hex("#E3E8EE"), CHover = Hex("#EAF0F7");
    static readonly Color CBarra = Hex("#283039"), CBarraTopo = Hex("#3B4550"), CBarraBotao = Hex("#3A4450"), CBarraHover = Hex("#46525F");
    static readonly Color CBarraTexto = Hex("#E8ECF0"), CFechar = Hex("#C24A3E"), CPapelFundo = Hex("#D7DCE2"), CFerramentas = Hex("#E9ECF0");

    // ------------------------------------------------------------------ entrada (Input System)

    Vector2 mouse;                       // pixels, origem no canto superior esquerdo (como o IMGUI)
    bool pendClique, pendSoltar, segurando;
    int pendCliques;
    float pendRolagem;
    readonly HashSet<string> pendTeclas = new HashSet<string>();
    float ultimoCliqueT = -10f;
    Vector2 ultimoCliqueP;

    // Entrada do quadro em desenho
    bool clicou, soltou, consumido;
    int cliques;
    float rolagem;
    readonly HashSet<string> teclas = new HashSet<string>();

    void LerEntrada()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m != null)
        {
            Vector2 p = m.position.ReadValue();
            mouse = new Vector2(p.x, Screen.height - p.y);
            if (m.leftButton.wasPressedThisFrame)
            {
                bool duplo = Time.unscaledTime - ultimoCliqueT < 0.45f && (mouse - ultimoCliqueP).magnitude < 8f * s && pendCliques < 2;
                pendCliques = duplo ? 2 : 1;
                ultimoCliqueT = duplo ? -10f : Time.unscaledTime;
                ultimoCliqueP = mouse;
                pendClique = true;
            }
            if (m.leftButton.wasReleasedThisFrame) pendSoltar = true;
            segurando = m.leftButton.isPressed;
            float r = m.scroll.ReadValue().y;
            if (Mathf.Abs(r) > 0.001f) pendRolagem += Mathf.Abs(r) >= 10f ? r / 120f : r;
        }
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame) pendTeclas.Add("cima");
            if (k.downArrowKey.wasPressedThisFrame) pendTeclas.Add("baixo");
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) pendTeclas.Add("enter");
            if (k.backspaceKey.wasPressedThisFrame) pendTeclas.Add("voltar");
        }
#endif
    }

    void TomarEntrada()
    {
        clicou = pendClique; soltou = pendSoltar; cliques = pendCliques; rolagem = pendRolagem;
        teclas.Clear(); teclas.UnionWith(pendTeclas);
        pendClique = pendSoltar = false; pendRolagem = 0f; pendTeclas.Clear();
        consumido = false;
    }

    bool TeclaUi(string t) => teclas.Contains(t);

    // ------------------------------------------------------------------ grupos, recorte e alvos

    float s = 1f;                                   // pixels por unidade
    float L => Screen.width / s;                    // largura da tela em unidades
    const float A = 1080f;                          // altura da tela em unidades
    Vector2 origem;                                 // canto do grupo atual, em pixels de tela
    Rect recorte;                                   // área visível do grupo atual, em pixels de tela
    bool interativo;                                // a região em desenho recebe o mouse
    readonly Stack<(Vector2, Rect)> pilha = new Stack<(Vector2, Rect)>();

    static float P(float u, float s) => u * s;
    Rect R(float x, float y, float w, float h) => new Rect(x * s, y * s, w * s, h * s);

    void Entrar(Rect r)
    {
        GUI.BeginGroup(r);
        pilha.Push((origem, recorte));
        origem += r.position;
        recorte = Intersecao(recorte, new Rect(origem, r.size));
    }

    void SairGrupo()
    {
        GUI.EndGroup();
        (origem, recorte) = pilha.Pop();
    }

    static Rect Intersecao(Rect a, Rect b)
    {
        float x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin), x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
        return new Rect(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
    }

    /// <summary>O mouse está sobre r (pixels no grupo atual) e a região recebe o mouse.</summary>
    bool Sobre(Rect r) => interativo && recorte.Contains(mouse) && new Rect(r.position + origem, r.size).Contains(mouse);

    /// <summary>Controles desenhados neste quadro, com a parte visível em pixels de tela (para testes e depuração).</summary>
    public readonly Dictionary<string, Rect> Alvos = new Dictionary<string, Rect>();

    void Marcar(string nome, Rect r)
    {
        Rect t = Intersecao(new Rect(r.position + origem, r.size), recorte);
        if (t.width > 1f && t.height > 1f) Alvos[nome] = t;
    }

    /// <summary>Posição de rolagem (pixels) de uma área rolável.</summary>
    public float RolagemDe(string id) => rolagens.TryGetValue(id, out var r) ? r.alvo : 0f;

    /// <summary>Clique (pressionar) em r; consome o clique do quadro.</summary>
    bool Clique(Rect r)
    {
        if (!clicou || consumido || !Sobre(r)) return false;
        consumido = true;
        return true;
    }

    // ------------------------------------------------------------------ desenho

    static void Caixa(Rect r, Color c, float raio = 0f)
    {
        if (c.a <= 0f) return;
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, raio);
    }

    static void Contorno(Rect r, Color c, float largura, float raio = 0f)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, largura, raio);
    }

    void Sombra(Rect r, float raio)
    {
        for (int i = 4; i >= 1; i--)
        {
            float e = i * 2.5f * s;
            Caixa(new Rect(r.x - e, r.y - e + 3f * s, r.width + 2 * e, r.height + 2 * e), new Color(0f, 0f, 0f, 0.045f), raio + e);
        }
    }

    void Linha(float x, float y, float w, Color c) => Caixa(new Rect(x, y, w, Mathf.Max(1f, s)), c);

    void Icone(Rect r, string nome, Color? tom = null)
    {
        Texture2D t = Tex(nome);
        if (t == null) return;
        Color antes = GUI.color;
        if (tom.HasValue) GUI.color = tom.Value;
        GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
        GUI.color = antes;
    }

    // ------------------------------------------------------------------ texto

    enum Peso { Normal, Negrito, Mono }
    readonly Dictionary<string, GUIStyle> estilos = new Dictionary<string, GUIStyle>();
    float escalaEstilos = -1f;

    GUIStyle Estilo(float tamanho, Color cor, Peso peso = Peso.Normal, bool quebra = false, TextAnchor ancora = TextAnchor.UpperLeft)
    {
        if (!Mathf.Approximately(escalaEstilos, s)) { estilos.Clear(); escalaEstilos = s; }
        string chave = $"{tamanho}|{ColorUtility.ToHtmlStringRGBA(cor)}|{peso}|{quebra}|{ancora}";
        if (estilos.TryGetValue(chave, out var e)) return e;
        Font f = peso == Peso.Negrito ? (fonteNegrito ?? fonte) : peso == Peso.Mono ? (fonteMono ?? fonte) : fonte;
        e = new GUIStyle
        {
            font = f, fontSize = Mathf.Max(8, Mathf.RoundToInt(tamanho * s)), wordWrap = quebra, alignment = ancora,
            richText = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0)
        };
        e.normal.textColor = cor;
        estilos[chave] = e;
        return e;
    }

    void Texto(Rect r, string t, GUIStyle e) => GUI.Label(r, t ?? "", e);

    /// <summary>Uma linha, cortada com reticências se não couber.</summary>
    void Rotulo(Rect r, string t, GUIStyle e)
    {
        GUI.Label(r, Cortar(t, e, r.width), e);
    }

    readonly Dictionary<string, string> cortes = new Dictionary<string, string>();

    string Cortar(string t, GUIStyle e, float largura)
    {
        if (string.IsNullOrEmpty(t)) return "";
        string chave = t + "|" + e.fontSize + "|" + e.font?.name + "|" + Mathf.RoundToInt(largura);
        if (cortes.TryGetValue(chave, out var c)) return c;
        c = t;
        if (e.CalcSize(new GUIContent(t)).x > largura)
        {
            int lo = 0, hi = t.Length;
            while (lo < hi)
            {
                int m = (lo + hi + 1) / 2;
                if (e.CalcSize(new GUIContent(t.Substring(0, m).TrimEnd() + "…")).x <= largura) lo = m; else hi = m - 1;
            }
            c = t.Substring(0, lo).TrimEnd() + "…";
        }
        if (cortes.Count > 4000) cortes.Clear();
        cortes[chave] = c;
        return c;
    }

    float Altura(string t, GUIStyle e, float largura) => e.CalcHeight(new GUIContent(t ?? ""), largura);

    // ------------------------------------------------------------------ botões

    /// <summary>Botão plano: fundo no hover/pressionado. Devolve true no clique.</summary>
    bool Botao(Rect r, Color hover, Color normal = default, float raio = 3f)
    {
        bool sobre = Sobre(r);
        Caixa(r, sobre ? (segurando ? hover * 0.94f : hover) : normal, raio * s);
        return Clique(r);
    }

    bool BotaoTexto(Rect r, string t, float tamanho = 15f, bool primario = false)
    {
        Color normal = primario ? CAcento : Hex("#E4E8ED"), hover = primario ? Hex("#4A80B8") : Hex("#D6DCE3");
        bool ok = Botao(r, hover, normal, 4f);
        Contorno(r, primario ? Hex("#2F5D8C") : Hex("#B5BEC8"), Mathf.Max(1f, s), 4f * s);
        Texto(r, t, Estilo(tamanho, primario ? Color.white : CTexto, Peso.Normal, false, TextAnchor.MiddleCenter));
        return ok;
    }

    // ------------------------------------------------------------------ rolagem

    class Rolagem { public float pos; public float alvo; }
    readonly Dictionary<string, Rolagem> rolagens = new Dictionary<string, Rolagem>();
    string barraArrastada;
    float barraInicioMouse, barraInicioPos;

    Rolagem Rol(string id)
    {
        if (!rolagens.TryGetValue(id, out var r)) rolagens[id] = r = new Rolagem();
        return r;
    }

    /// <summary>Área rolável (pixels no grupo atual). Desenhe o conteúdo entre Comecar e Terminar.</summary>
    float ComecarRolagem(string id, Rect vista, float alturaConteudo)
    {
        var r = Rol(id);
        float max = Mathf.Max(0f, alturaConteudo - vista.height);
        if (Sobre(vista) && Mathf.Abs(rolagem) > 0.001f && max > 0f)
        {
            r.alvo = Mathf.Clamp(r.alvo - rolagem * 64f * s, 0f, max);
            rolagem = 0f;   // a roda é usada pela área mais interna sob o mouse
        }
        r.alvo = Mathf.Clamp(r.alvo, 0f, max);
        r.pos = Mathf.Clamp(Mathf.Lerp(r.pos, r.alvo, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f)), 0f, max);
        if (Mathf.Abs(r.pos - r.alvo) < 0.5f) r.pos = r.alvo;

        // Barra
        if (max > 0f)
        {
            float bw = 10f * s;
            Rect trilho = new Rect(vista.xMax - bw - 2f * s, vista.y + 2f * s, bw, vista.height - 4f * s);
            float h = Mathf.Max(32f * s, trilho.height * vista.height / alturaConteudo);
            float y = trilho.y + (trilho.height - h) * (r.pos / max);
            Rect polegar = new Rect(trilho.x + 2f * s, y, bw - 4f * s, h);
            bool sobre = Sobre(new Rect(trilho.x - 4f * s, trilho.y, trilho.width + 8f * s, trilho.height));
            if (sobre || barraArrastada == id) Caixa(trilho, new Color(0f, 0f, 0f, 0.05f), bw / 2);
            Caixa(polegar, barraArrastada == id ? Hex("#7C8896") : sobre ? Hex("#99A4B1") : Hex("#B4BDC7"), polegar.width / 2);
            if (Clique(new Rect(trilho.x - 4f * s, trilho.y, trilho.width + 8f * s, trilho.height)))
            {
                if (!polegar.Contains(mouse - origem))   // clique no trilho: pula uma página
                    r.alvo = Mathf.Clamp(r.alvo + Mathf.Sign(mouse.y - origem.y - polegar.center.y) * vista.height * 0.9f, 0f, max);
                barraArrastada = id;
                barraInicioMouse = mouse.y;
                barraInicioPos = r.alvo;
            }
            if (barraArrastada == id)
            {
                if (segurando) r.alvo = r.pos = Mathf.Clamp(barraInicioPos + (mouse.y - barraInicioMouse) * max / Mathf.Max(1f, trilho.height - h), 0f, max);
                else barraArrastada = null;
            }
        }
        Entrar(vista);
        Entrar(new Rect(0f, -r.pos, vista.width, Mathf.Max(alturaConteudo, vista.height)));
        return max > 0f ? 14f * s : 0f;   // espaço reservado à barra
    }

    void TerminarRolagem()
    {
        SairGrupo();
        SairGrupo();
    }

    /// <summary>Rola o mínimo para mostrar [y0, y1] (pixels do conteúdo).</summary>
    void MostrarFaixa(string id, float y0, float y1, float alturaVista)
    {
        var r = Rol(id);
        if (y0 < r.alvo) r.alvo = y0;
        else if (y1 > r.alvo + alturaVista) r.alvo = y1 - alturaVista;
    }

    // ------------------------------------------------------------------ utilidades

    readonly Dictionary<string, Texture2D> texturas = new Dictionary<string, Texture2D>();

    Texture2D Tex(string nome)
    {
        if (texturas.Count == 0 && imagens != null)
            foreach (var t in imagens) if (t != null) texturas[t.name] = t;
        return nome != null && texturas.TryGetValue(nome, out var x) ? x : null;
    }

    static string Iniciais(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return "?";
        var p = nome.Split(' ');
        return p.Length > 1 ? $"{p[0][0]}{p[p.Length - 1][0]}".ToUpperInvariant() : nome.Substring(0, Mathf.Min(2, nome.Length)).ToUpperInvariant();
    }
}
