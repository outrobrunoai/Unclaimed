using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Quadro de avisos da recepção. E com o quadro na mira: a câmera vai para a frente dele (vista do quadro);
/// o mouse destaca o papel sob o cursor e o clique abre a inspeção daquele papel (a mesma das pistas e do
/// cadeado: arrastar gira, roda aproxima, E ou Esc devolve à vista do quadro). Esc na vista do quadro volta ao
/// jogo. A posição dos papéis vem de QuadroAvisos.json (o mesmo que gerou o quadro 3D e as imagens).
///
/// Origem do objeto: centro do quadro, na face da parede; frente do quadro para -Z local, x à direita de quem olha.
/// </summary>
public class QuadroDeAvisos : InterfaceDeTela
{
    public TextAsset conteudo;
    [Tooltip("Inspeção de cada papel, na ordem dos documentos do JSON.")]
    public ObjetoInspecionavel[] papeis;
    public Font fonte, fonteNegrito;
    public float alcance = 2f;
    [Tooltip("Profundidade da face da cortiça (m, a partir da parede).")]
    public float faceDaCortica = 0.014f;

    public override float AlcanceDeInteracao => alcance;
    public override string Indicacao => "Ler quadro de avisos";
    public override void Interagir() => AbrirInterface();

    public enum Modo { Fechado, Indo, Quadro, Examinando, Voltando }
    public Modo Estado { get; private set; } = Modo.Fechado;
    public QuadroDados Dados => dados ??= QuadroDados.Carregar(conteudo.text);
    /// <summary>Papel sob o cursor (ou escolhido pelo teclado) na vista do quadro, ou em exame; -1 nenhum.</summary>
    public int Selecionado { get; private set; } = -1;
    /// <summary>Retângulos dos controles na tela (coordenadas do IMGUI), para os testes.</summary>
    public readonly Dictionary<string, Rect> Alvos = new Dictionary<string, Rect>();

    QuadroDados dados;
    int[] ordemDeLeitura;   // de cima para baixo, da esquerda para a direita

    // câmera
    Transform cam;
    Vector3 camPosLocal;
    Quaternion camRotLocal;
    Vector3 deP, ateP;
    Quaternion deR, ateR;
    float t0;
    const float Duracao = 0.45f;

    // entrada (Input System), lida no Update e usada no mesmo quadro
    Vector2 mouse, mouseAnterior;
    bool clicou, mouseMoveu;

    float s => Escala;

    void OrdenarLeitura()
    {
        var docs = Dados.documentos;
        var idx = new List<int>();
        for (int i = 0; i < docs.Length; i++) idx.Add(i);
        // faixas de 25 cm de altura, depois da esquerda para a direita
        idx.Sort((a, b) =>
        {
            int fa = Mathf.FloorToInt((0.45f - docs[a].posicao[1]) / 0.25f), fb = Mathf.FloorToInt((0.45f - docs[b].posicao[1]) / 0.25f);
            return fa != fb ? fa.CompareTo(fb) : docs[a].posicao[0].CompareTo(docs[b].posicao[0]);
        });
        ordemDeLeitura = idx.ToArray();
    }

    // ------------------------------------------------------------------ abrir, fechar e câmera

    protected override void AoAbrir()
    {
        if (ordemDeLeitura == null) OrdenarLeitura();
        cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) { FecharInterface(); return; }
        camPosLocal = cam.localPosition;
        camRotLocal = cam.localRotation;
        PoseDoQuadro(out ateP, out ateR);
        deP = cam.position; deR = cam.rotation;
        t0 = Time.unscaledTime;
        Estado = Modo.Indo;
        Selecionado = -1;
    }

    /// <summary>Câmera de frente para o quadro, a uma distância em que ele cabe na tela com folga.</summary>
    void PoseDoQuadro(out Vector3 pos, out Quaternion rot)
    {
        var q = Dados.quadro;
        var c = Camera.main;
        float vfov = c.fieldOfView * Mathf.Deg2Rad;
        float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov / 2f) * c.aspect);
        // margem para a barra de instruções embaixo
        float dist = Mathf.Max(q.altura * 1.16f / 2f / Mathf.Tan(vfov / 2f), q.largura * 1.06f / 2f / Mathf.Tan(hfov / 2f));
        Vector3 frente = -transform.forward;     // do quadro para quem olha
        pos = transform.position + frente * (dist + faceDaCortica) - transform.up * (q.altura * 0.035f);
        rot = Quaternion.LookRotation(-frente, transform.up);
    }

    protected override void PedirFechar()
    {
        // Em exame, Esc é da inspeção (ela fecha e devolve à vista do quadro).
        if (Estado == Modo.Examinando || Estado == Modo.Voltando) return;
        deP = cam != null ? cam.position : Vector3.zero;
        deR = cam != null ? cam.rotation : Quaternion.identity;
        t0 = Time.unscaledTime;
        Estado = Modo.Voltando;
        Selecionado = -1;
    }

    protected override void AoFechar()
    {
        if (cam != null) { cam.localPosition = camPosLocal; cam.localRotation = camRotLocal; }
        Estado = Modo.Fechado;
    }

    protected override void OnDisable()
    {
        if (Estado == Modo.Examinando && Selecionado >= 0 && Selecionado < papeis.Length && papeis[Selecionado] != null)
            papeis[Selecionado].EncerrarInspecao();
        base.OnDisable();
        if (Estado != Modo.Fechado && cam != null) { cam.localPosition = camPosLocal; cam.localRotation = camRotLocal; Estado = Modo.Fechado; }
    }

    protected override void LateUpdate()
    {
        if (EstaAberta && cam != null && (Estado == Modo.Indo || Estado == Modo.Voltando))
        {
            float k = Mathf.Clamp01((Time.unscaledTime - t0) / Duracao);
            float e = k * k * (3f - 2f * k);
            if (Estado == Modo.Indo)
            {
                cam.SetPositionAndRotation(Vector3.Lerp(deP, ateP, e), Quaternion.Slerp(deR, ateR, e));
                if (k >= 1f) Estado = Modo.Quadro;
            }
            else
            {
                Vector3 alvoP = cam.parent != null ? cam.parent.TransformPoint(camPosLocal) : camPosLocal;
                Quaternion alvoR = cam.parent != null ? cam.parent.rotation * camRotLocal : camRotLocal;
                cam.SetPositionAndRotation(Vector3.Lerp(deP, alvoP, e), Quaternion.Slerp(deR, alvoR, e));
                if (k >= 1f) FecharInterface();
            }
        }
        base.LateUpdate();
    }

    // ------------------------------------------------------------------ entrada

    protected override void AtualizarAberta()
    {
        if (Estado == Modo.Examinando)
        {
            // A inspeção cuida da entrada; quando ela fecha (E ou Esc), volta a vista do quadro.
            if (!ObjetoInspecionavel.InspecionandoAlgo) Estado = Modo.Quadro;
            return;
        }
        if (Estado != Modo.Quadro) return;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        var k = Keyboard.current;
        clicou = false;
        if (m != null)
        {
            Vector2 p = m.position.ReadValue();
            mouse = new Vector2(p.x, Screen.height - p.y);
            clicou = m.leftButton.wasPressedThisFrame;
        }
        bool esquerda = k != null && (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame);
        bool direita = k != null && (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame);
        bool enter = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame);
#else
        bool esquerda = false, direita = false, enter = false;
#endif
        mouseMoveu = mouse != mouseAnterior;
        mouseAnterior = mouse;

        if (clicou && BotaoSair().Contains(mouse)) { PedirFechar(); return; }
        int sob = DocumentoSob(mouse);
        if (sob >= 0 && mouseMoveu) Selecionado = sob;
        if (esquerda || direita)
        {
            int pos = System.Array.IndexOf(ordemDeLeitura, Selecionado);
            pos = pos < 0 ? 0 : (pos + (direita ? 1 : -1) + ordemDeLeitura.Length) % ordemDeLeitura.Length;
            Selecionado = ordemDeLeitura[pos];
        }
        if (clicou && sob >= 0) Examinar(sob);
        else if (enter && Selecionado >= 0) Examinar(Selecionado);
    }

    /// <summary>Abre a inspeção do papel i (o papel sai do quadro enquanto é examinado).</summary>
    public void Examinar(int i)
    {
        if (papeis == null || i < 0 || i >= papeis.Length || papeis[i] == null) return;
        Selecionado = i;
        if (papeis[i].IniciarInspecao()) Estado = Modo.Examinando;
    }

    // ------------------------------------------------------------------ geometria

    /// <summary>Papel mais à frente sob um ponto da tela (vista do quadro); -1 nenhum.</summary>
    public int DocumentoSob(Vector2 pontoGui)
    {
        var c = Camera.main;
        if (c == null) return -1;
        Ray r = c.ScreenPointToRay(new Vector3(pontoGui.x, Screen.height - pontoGui.y, 0f));
        Vector3 n = -transform.forward;
        var plano = new Plane(n, transform.position + n * faceDaCortica);
        if (!plano.Raycast(r, out float d)) return -1;
        Vector3 local = transform.InverseTransformPoint(r.GetPoint(d));
        var p = new Vector2(local.x, local.y);
        int melhor = -1, ordem = int.MinValue;
        var docs = Dados.documentos;
        for (int i = 0; i < docs.Length; i++)
            if (docs[i].ordem >= ordem && docs[i].Contem(p, out _)) { melhor = i; ordem = docs[i].ordem; }
        return melhor;
    }

    /// <summary>Ponto de um papel (uv do papel, origem no canto superior esquerdo) na tela, em coordenadas do IMGUI.</summary>
    public Vector2 NaTela(int doc, Vector2 uv)
    {
        Vector2 q = Dados.documentos[doc].NoQuadro(uv);
        Vector3 w = transform.TransformPoint(new Vector3(q.x, q.y, -faceDaCortica));
        Vector3 sp = Camera.main.WorldToScreenPoint(w);
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    float Barra => 54f * s;

    Rect BotaoSair()
    {
        float h = 36f * s;
        return new Rect(Screen.width - 190f * s, Screen.height - Barra + (Barra - h) / 2f, 174f * s, h);
    }

    // ------------------------------------------------------------------ desenho (só a vista do quadro; o exame é da inspeção)

    GUIStyle eTitulo, eBotao, eDica;
    Texture2D branco;

    void Estilos()
    {
        if (eTitulo != null) return;
        branco = Cor(Color.white);
        eTitulo = new GUIStyle { font = fonteNegrito ? fonteNegrito : fonte, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };
        eBotao = new GUIStyle { font = fonte, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };
        eDica = new GUIStyle(eBotao) { normal = { textColor = new Color(1f, 1f, 1f, 0.82f) } };
    }

    void Caixa(Rect r, Color c, float raio = 0f) => GUI.DrawTexture(r, branco, ScaleMode.StretchToFill, true, 0f, c, 0f, raio);

    protected override void Desenhar()
    {
        if (Event.current.type != EventType.Repaint) return;
        Alvos.Clear();
        if (Estado != Modo.Quadro) return;
        Estilos();
        if (Selecionado >= 0)
        {
            Destaque(Selecionado);
            var r = new Rect(0, 0, Screen.width, Barra);
            Caixa(r, new Color(0.07f, 0.08f, 0.09f, 0.72f));
            eTitulo.fontSize = Mathf.RoundToInt(20f * s);
            GUI.Label(r, Dados.documentos[Selecionado].titulo, eTitulo);
        }
        Caixa(new Rect(0, Screen.height - Barra, Screen.width, Barra), new Color(0.07f, 0.08f, 0.09f, 0.82f));
        eDica.fontSize = Mathf.RoundToInt(16f * s);
        GUI.Label(new Rect(0, Screen.height - Barra, Screen.width, Barra),
            "Clique num papel para examinar   ·   ← → escolher   ·   Enter examinar", eDica);
        Rect sair = BotaoSair();
        Alvos["sair"] = sair;
        Caixa(sair, sair.Contains(mouse) ? new Color(1f, 1f, 1f, 0.24f) : new Color(1f, 1f, 1f, 0.12f), 6f * s);
        eBotao.fontSize = Mathf.RoundToInt(16f * s);
        GUI.Label(sair, "Sair (Esc)", eBotao);
    }

    /// <summary>Contorno do papel sob o cursor (o quadro é visto de frente: o papel na tela é um retângulo girado).</summary>
    void Destaque(int i)
    {
        Vector2 a = NaTela(i, new Vector2(0, 0)), b = NaTela(i, new Vector2(1, 0)), d = NaTela(i, new Vector2(0, 1));
        float w = (b - a).magnitude, h = (d - a).magnitude;
        float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        Matrix4x4 antes = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, a);
        float e = 3f * s, f = 5f * s;
        var cor = new Color(1f, 0.86f, 0.35f, 0.95f);
        Caixa(new Rect(a.x - f, a.y - f, w + 2 * f, e), cor);
        Caixa(new Rect(a.x - f, a.y + h + f - e, w + 2 * f, e), cor);
        Caixa(new Rect(a.x - f, a.y - f, e, h + 2 * f), cor);
        Caixa(new Rect(a.x + w + f - e, a.y - f, e, h + 2 * f), cor);
        GUI.matrix = antes;
        Alvos["doc:" + Dados.documentos[i].id] = new Rect(a.x, a.y, w, h);
    }
}
