using UnityEngine;

/// <summary>
/// Interface de tela aberta por uma tecla, sem mirar nada (celular, álbum, gravador). Usa o mesmo fluxo da
/// InterfaceDeTela: trava o jogador, solta o cursor, bloqueia as outras interações e pausa Daniel. A mesma tecla
/// (ou Esc) fecha.
/// </summary>
public abstract class PainelDoJogador : InterfaceDeTela
{
    public override float AlcanceDeInteracao => 0f;
    public override string Indicacao => "";
    public override void Interagir() => AbrirInterface();

    /// <summary>Tecla que abre e fecha (nome do Input System: Key).</summary>
    public abstract string TeclaDoPainel { get; }

    /// <summary>Falso quando o painel não pode abrir agora (ex.: sem câmera na mão).</summary>
    protected virtual bool PodeAbrir => true;

    protected override void Update()
    {
        base.Update();
        if (EstaAberta || InterfaceDeTela.AlgumaAberta || MiraDeInteracao.ControleCapturado || MenuPausa.Pausado) return;
        if (PodeAbrir && MiraDeInteracao.TeclaApertada(TeclaDoPainel)) AbrirInterface();
    }

    protected override void AtualizarAberta()
    {
        if (MiraDeInteracao.TeclaApertada(TeclaDoPainel)) PedirFechar();
    }

    // Sem indicação na mira: ninguém mira um painel do jogador.
    protected override void OnGUI()
    {
        useGUILayout = false;
        if (EstaAberta) Desenhar();
    }

    protected static float S => DesenhoUi.S;
    protected static void Caixa(Rect r, Color c) => DesenhoUi.Caixa(r, c);
    protected static GUIStyle Estilo(float tamanho, Color cor, bool negrito = false, TextAnchor ancora = TextAnchor.UpperLeft) =>
        DesenhoUi.Estilo(tamanho, cor, negrito, ancora);
    protected static bool Botao(Rect r, string texto, bool ativo = true, float tamanho = 16f) => DesenhoUi.Botao(r, texto, ativo, tamanho);
}

/// <summary>Atalhos para achar o jogador (Movimento) e a câmera dele.</summary>
public static class Jogador
{
    static Movimento cache;

    public static Movimento Movimento
    {
        get
        {
            if (cache == null) cache = Object.FindAnyObjectByType<Movimento>();
            return cache;
        }
    }

    public static Vector3 Posicao => Movimento != null ? Movimento.transform.position : Vector3.zero;

    public static Transform Olhos
    {
        get
        {
            Camera c = Camera.main;
            if (c != null) return c.transform;
            return Movimento != null ? Movimento.camera : null;
        }
    }

    /// <summary>O jogador está lendo, usando uma tela, no caderno ou no menu: Daniel não age.</summary>
    public static bool Ocupado =>
        (InterfaceDeTela.AlgumaAberta && !(InterfaceDeTela.Aberta is Celular)) || ObjetoInspecionavel.InspecionandoAlgo ||
        DiarioDePistas.Aberto || MenuPausa.Pausado;
}

/// <summary>Desenho simples em IMGUI usado pelos painéis de José (escala pela altura da tela, 1080p = 1).</summary>
public static class DesenhoUi
{
    static GUIStyle estiloBase;
    static Texture2D branco;

    /// <summary>Escala fixa enquanto um painel desenha dentro de uma área própria (a tela do celular). 0 = pela altura da tela.</summary>
    public static float EscalaForcada;
    public static float S => EscalaForcada > 0f ? EscalaForcada : Mathf.Max(0.5f, Screen.height / 1080f);

    public static void Caixa(Rect r, Color c)
    {
        if (branco == null)
        {
            branco = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            branco.SetPixel(0, 0, Color.white);
            branco.Apply();
        }
        Color antes = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, branco);
        GUI.color = antes;
    }

    public static GUIStyle Estilo(float tamanho, Color cor, bool negrito = false, TextAnchor ancora = TextAnchor.UpperLeft)
    {
        if (estiloBase == null) estiloBase = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
        var e = new GUIStyle(estiloBase)
        {
            fontSize = Mathf.RoundToInt(tamanho * S),
            fontStyle = negrito ? FontStyle.Bold : FontStyle.Normal,
            alignment = ancora,
        };
        e.normal.textColor = cor;
        return e;
    }

    public static bool Botao(Rect r, string texto, bool ativo = true, float tamanho = 16f)
    {
        bool sobre = ativo && r.Contains(Event.current.mousePosition);
        Caixa(r, !ativo ? new Color(0.25f, 0.25f, 0.25f, 0.8f) : sobre ? new Color(0.38f, 0.42f, 0.48f, 0.95f) : new Color(0.22f, 0.25f, 0.30f, 0.95f));
        GUI.Label(r, texto, Estilo(tamanho, ativo ? Color.white : new Color(1, 1, 1, 0.4f), true, TextAnchor.MiddleCenter));
        return ativo && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }
}
