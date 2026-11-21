using System.Collections.Generic;
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
        if (EstaAberta || InterfaceDeTela.AlgumaAberta || MiraDeInteracao.ControleCapturado || MenuPausa.Pausado || IntroChegada.Ativa) return;
        if (PodeAbrir && MiraDeInteracao.TeclaApertada(TeclaDoPainel)) AbrirInterface();
    }

    protected override void AtualizarAberta()
    {
        if (!IntroChegada.Ativa && MiraDeInteracao.TeclaApertada(TeclaDoPainel)) PedirFechar();
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
        DiarioDePistas.Aberto || MenuPausa.Pausado || IntroChegada.Ativa;
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

    /// <summary>Curva suave 0..1 (smoothstep) para fades e entradas.</summary>
    public static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static Texture2D vinheta, degrade;

    /// <summary>Escurece as bordas da tela (centro livre). alfa 0..1.</summary>
    public static void Vinheta(float alfa)
    {
        if (alfa <= 0.001f) return;
        if (vinheta == null)
        {
            const int tam = 128;
            vinheta = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[tam * tam];
            for (int j = 0; j < tam; j++)
                for (int i = 0; i < tam; i++)
                {
                    float u = i / (tam - 1f) * 2f - 1f, v = j / (tam - 1f) * 2f - 1f;
                    px[j * tam + i] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.3f, Mathf.Sqrt(u * u + v * v))) * 0.85f);
                }
            vinheta.SetPixels(px);
            vinheta.Apply();
        }
        Color antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfa);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vinheta, ScaleMode.StretchToFill);
        GUI.color = antes;
    }

    /// <summary>Faixa preta em degradê: opaca de um lado (em cima ou embaixo) e transparente do outro.</summary>
    public static void Degrade(Rect r, float alfa, bool opacaEmCima)
    {
        if (degrade == null)
        {
            degrade = new Texture2D(1, 64, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int j = 0; j < 64; j++) degrade.SetPixel(0, j, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, j / 63f)));
            degrade.Apply();
        }
        Color antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfa);
        // A textura tem o opaco em v = 1, que o IMGUI desenha em cima.
        if (opacaEmCima) GUI.DrawTexture(r, degrade, ScaleMode.StretchToFill);
        else GUI.DrawTextureWithTexCoords(r, degrade, new Rect(0f, 1f, 1f, -1f));
        GUI.color = antes;
    }

    static Font maquina;
    static bool maquinaCarregada;
    static readonly Dictionary<ChaveEstilo, ParEstilo> cacheEstilos = new Dictionary<ChaveEstilo, ParEstilo>();

    readonly struct ChaveEstilo : System.IEquatable<ChaveEstilo>
    {
        public readonly int tamanhoPx;
        public readonly bool negrito;
        public readonly TextAnchor ancora;
        public readonly bool maquina;
        public readonly bool textoRico;

        public ChaveEstilo(int tamanhoPx, bool negrito, TextAnchor ancora, bool maquina, bool textoRico)
        {
            this.tamanhoPx = tamanhoPx;
            this.negrito = negrito;
            this.ancora = ancora;
            this.maquina = maquina;
            this.textoRico = textoRico;
        }

        public bool Equals(ChaveEstilo outro) =>
            tamanhoPx == outro.tamanhoPx && negrito == outro.negrito && ancora == outro.ancora &&
            maquina == outro.maquina && textoRico == outro.textoRico;

        public override bool Equals(object obj) => obj is ChaveEstilo outro && Equals(outro);

        public override int GetHashCode() =>
            ((((tamanhoPx * 397) ^ (int)ancora) * 2 + (negrito ? 1 : 0)) * 2 + (maquina ? 1 : 0)) * 2 + (textoRico ? 1 : 0);
    }

    // Dois estilos por chave evitam sobrescrever a cor quando texto e sombra são pedidos antes de desenhar.
    sealed class ParEstilo
    {
        readonly GUIStyle primeiro, segundo;
        bool usarSegundo;

        public ParEstilo(GUIStyle primeiro, GUIStyle segundo)
        {
            this.primeiro = primeiro;
            this.segundo = segundo;
        }

        public GUIStyle Proximo()
        {
            GUIStyle escolhido = usarSegundo ? segundo : primeiro;
            usarSegundo = !usarSegundo;
            return escolhido;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarEstilos()
    {
        estiloBase = null;
        maquina = null;
        maquinaCarregada = false;
        cacheEstilos.Clear();
    }

    /// <summary>Como Estilo, na fonte de máquina de escrever (voz do jornalista: pergunta de saída e finais).</summary>
    public static GUIStyle EstiloMaquina(float tamanho, Color cor, TextAnchor ancora = TextAnchor.UpperLeft)
    {
        if (!maquinaCarregada) { maquina = Resources.Load<Font>("Fontes/Maquina de escrever"); maquinaCarregada = true; }
        return ObterEstilo(tamanho, cor, false, ancora, maquina, true, false);
    }

    public static GUIStyle Estilo(float tamanho, Color cor, bool negrito = false, TextAnchor ancora = TextAnchor.UpperLeft, bool textoRico = false)
    {
        return ObterEstilo(tamanho, cor, negrito, ancora, null, false, textoRico);
    }

    static GUIStyle ObterEstilo(float tamanho, Color cor, bool negrito, TextAnchor ancora, Font fonte, bool usarMaquina, bool textoRico)
    {
        if (estiloBase == null) estiloBase = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
        var chave = new ChaveEstilo(Mathf.RoundToInt(tamanho * S), negrito, ancora, usarMaquina, textoRico);
        if (!cacheEstilos.TryGetValue(chave, out ParEstilo par))
        {
            par = new ParEstilo(CriarEstilo(chave, fonte), CriarEstilo(chave, fonte));
            cacheEstilos[chave] = par;
        }
        GUIStyle e = par.Proximo();
        e.normal.textColor = cor;
        return e;
    }

    static GUIStyle CriarEstilo(ChaveEstilo chave, Font fonte)
    {
        var e = new GUIStyle(estiloBase)
        {
            fontSize = chave.tamanhoPx,
            fontStyle = chave.negrito ? FontStyle.Bold : FontStyle.Normal,
            alignment = chave.ancora,
            richText = chave.textoRico,
        };
        if (fonte != null) e.font = fonte;
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
