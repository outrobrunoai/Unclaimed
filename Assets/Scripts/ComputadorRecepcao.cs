using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estação da recepção (REC-01, "Ipê Desktop"), com a sessão da atendente aberta: área de trabalho, barra de
/// tarefas com menu, relógio e sessão, e janelas de verdade (abrir, fechar, minimizar, maximizar, alternar pela
/// barra, arrastar sem sair da tela). Aplicativos: Correio, Arquivos, Leitor de documentos, Notas, Imagens,
/// Agenda e "Sobre esta estação" (ComputadorRecepcao.Apps.cs).
///
/// Mirar o monitor e apertar E abre a interface (InterfaceDeTela: trava o jogador e as outras interações, solta o
/// cursor); Esc ou "Sair" fecham e devolvem cursor e controles. O estado da sessão (janelas, lidas, pastas,
/// rolagens) fica guardado até o fim da partida. Ao sair, a última imagem da interface vai para a tela do monitor
/// no cenário, então o computador visto na sala é o mesmo que estava aberto.
///
/// O conteúdo (pessoas, conversas, arquivos, agenda) vem de EstacaoRecepcao.json (EstacaoDados), separado da
/// lógica. Desenho com IMGUI e entrada pelo Input System (ComputadorRecepcao.Ui.cs).
/// </summary>
public partial class ComputadorRecepcao : InterfaceDeTela
{
    [Header("Interação")]
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.5f;
    public string textoUsar = "Usar computador";

    [Header("Conteúdo e aparência")]
    [Tooltip("EstacaoRecepcao.json: estação, pessoas, conversas, pastas, arquivos e agenda.")]
    public TextAsset conteudo;
    public Texture2D papelDeParede;
    [Tooltip("Ícones, glifos e imagens da estação, achados pelo nome da textura (ex.: doc_64, g_close, foto_armario_apoio).")]
    public Texture2D[] imagens;
    public Font fonte, fonteNegrito, fonteMono;

    [Header("Monitor no cenário")]
    [Tooltip("Tela do monitor (Unlit, _BaseMap): recebe a imagem da interface ao sair.")]
    public Renderer telaDoMonitor;
    [Tooltip("Largura/altura da tela física do monitor. Até ~5% de diferença a captura é só ajustada à tela (sem " +
             "cortar a barra de tarefas nem os ícones); acima disso, as laterais são cortadas.")]
    public float aspectoDoMonitor = 0.506f / 0.293f;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => textoUsar;
    public override void Interagir() => AbrirInterface();

    public EstacaoDados Dados => dados;

    // ------------------------------------------------------------------ janelas

    public enum App { Correio, Arquivos, Leitor, Notas, Imagens, Agenda, Sobre }

    public class Janela
    {
        public App app;
        public Rect area, areaNormal;   // unidades
        public bool aberta, minimizada, maximizada, posicionada;
        public Vector2 tamanho;
        public string icone;
    }

    const float BarraAltura = 44f, TituloAltura = 34f;

    EstacaoDados dados;
    readonly Dictionary<App, Janela> janelas = new Dictionary<App, Janela>();
    readonly List<Janela> ordem = new List<Janela>();     // de trás para a frente
    readonly List<Janela> tarefas = new List<Janela>();   // ordem na barra
    Janela arrastando;
    Vector2 arrasteMouse0, arrasteArea0;
    Janela frentePendente;
    bool menuAberto;
    int iconeSelecionado = -1;
    float horaBase;
    int quadroDesenhado = -1;
    bool fechando;
    Texture2D capturaMonitor;
    MaterialPropertyBlock blocoMonitor;

    void Awake() => Carregar();

    void Carregar()
    {
        if (dados != null || conteudo == null) return;
        dados = EstacaoDados.Carregar(conteudo.text);
        string[] hm = (dados.estacao.horaInicial ?? "00:00").Split(':');
        horaBase = int.Parse(hm[0]) * 60 + int.Parse(hm[1]);
        foreach (var c in dados.conversas) foreach (var m in c.mensagens) if (m.lida) lidas.Add(m.id);
        void J(App a, float x, float y, float w, float h, string icone) =>
            janelas[a] = new Janela { app = a, area = new Rect(x, y, w, h), tamanho = new Vector2(w, h), icone = icone };
        J(App.Correio, 150, 56, 1220, 740, "correio");
        J(App.Arquivos, 240, 110, 1000, 640, "pasta");
        J(App.Leitor, 560, 36, 900, 780, "doc");
        J(App.Notas, 330, 170, 800, 540, "nota");
        J(App.Imagens, 470, 120, 940, 660, "imagem");
        J(App.Agenda, 380, 130, 900, 600, "agenda");
        J(App.Sobre, 0, 0, 540, 420, "estacao");
    }

    /// <summary>Minutos desde 00:00 do dia da sessão, andando com o tempo de jogo.</summary>
    float Minutos => horaBase + Time.timeSinceLevelLoad / 60f;

    public string Relogio
    {
        get { int m = (int)Minutos; return $"{(m / 60) % 24:00}:{m % 60:00}"; }
    }

    string DataAtual => Minutos >= 1440f ? dados.estacao.dataSeguinte : dados.estacao.data;
    string DiaAtual => Minutos >= 1440f ? dados.estacao.diaSeguinte : dados.estacao.diaDaSemana;

    protected override void AoAbrir()
    {
        Carregar();
        menuAberto = false;
        arrastando = null;
        barraArrastada = null;
        fechando = false;
        pendClique = pendSoltar = false;
        pendTeclas.Clear();
    }

    protected override void AtualizarAberta() => LerEntrada();

    // Esc e "Sair": a imagem do último quadro vai para o monitor antes de fechar.
    protected override void PedirFechar()
    {
        if (!fechando) StartCoroutine(CapturarEFechar());
    }

    IEnumerator CapturarEFechar()
    {
        fechando = true;
        yield return new WaitForEndOfFrame();
        AplicarNoMonitor(ScreenCapture.CaptureScreenshotAsTexture());
        FecharInterface();
    }

    /// <summary>Põe a imagem da interface na tela do monitor.</summary>
    public void AplicarNoMonitor(Texture2D captura)
    {
        if (captura == null) return;
        if (telaDoMonitor == null) { Destroy(captura); return; }
        if (capturaMonitor != null) Destroy(capturaMonitor);
        capturaMonitor = captura;
        capturaMonitor.wrapMode = TextureWrapMode.Clamp;
        float sx = Mathf.Min(1f, aspectoDoMonitor / ((float)captura.width / captura.height));
        if (sx > 0.95f) sx = 1f;
        blocoMonitor ??= new MaterialPropertyBlock();
        telaDoMonitor.GetPropertyBlock(blocoMonitor);
        blocoMonitor.SetTexture("_BaseMap", capturaMonitor);
        blocoMonitor.SetVector("_BaseMap_ST", new Vector4(sx, 1f, (1f - sx) / 2f, 0f));
        telaDoMonitor.SetPropertyBlock(blocoMonitor);
    }

    // ------------------------------------------------------------------ API de janelas (também usada pelos testes)

    public Janela JanelaDe(App a) => janelas.TryGetValue(a, out var j) ? j : null;
    public bool AppAberta(App a) => JanelaDe(a) is { aberta: true };
    public bool Visivel(App a) => JanelaDe(a) is { aberta: true, minimizada: false };
    public App? Foco
    {
        get
        {
            for (int i = ordem.Count - 1; i >= 0; i--) if (!ordem[i].minimizada) return ordem[i].app;
            return null;
        }
    }
    public bool MenuAberto => menuAberto;
    public IReadOnlyList<Janela> Ordem => ordem;

    public void AbrirApp(App a)
    {
        Carregar();
        var j = janelas[a];
        if (!j.aberta)
        {
            j.aberta = true;
            j.maximizada = false;
            if (!j.posicionada)
            {
                if (a == App.Sobre) j.area = new Rect((L - j.tamanho.x) / 2f, (A - BarraAltura - j.tamanho.y) / 2f, j.tamanho.x, j.tamanho.y);
                j.posicionada = true;
            }
            tarefas.Remove(j);
            tarefas.Add(j);
        }
        j.minimizada = false;
        Frente(j);
        frentePendente = null;   // aberta por um clique noutra janela: a nova fica na frente
    }

    public void FecharApp(App a)
    {
        var j = janelas[a];
        j.aberta = false;
        j.minimizada = false;
        if (j.maximizada) { j.area = j.areaNormal; j.maximizada = false; }
        ordem.Remove(j);
        tarefas.Remove(j);
        if (arrastando == j) arrastando = null;
    }

    public void Minimizar(App a)
    {
        var j = janelas[a];
        if (!j.aberta) return;
        j.minimizada = true;
        if (arrastando == j) arrastando = null;
    }

    public void AlternarMaximizar(App a)
    {
        var j = janelas[a];
        if (!j.aberta) return;
        if (j.maximizada) { j.area = j.areaNormal; j.maximizada = false; }
        else { j.areaNormal = j.area; j.area = new Rect(0, 0, L, A - BarraAltura); j.maximizada = true; }
        Frente(j);
    }

    /// <summary>Clique no botão da barra de tarefas: mostra, traz para a frente ou minimiza (se já é a da frente).</summary>
    public void BotaoDaTarefa(App a)
    {
        var j = janelas[a];
        if (j.minimizada) { j.minimizada = false; Frente(j); }
        else if (Foco == a) j.minimizada = true;
        else Frente(j);
    }

    void Frente(Janela j)
    {
        ordem.Remove(j);
        ordem.Add(j);
    }

    /// <summary>Move uma janela (unidades), mantendo a barra de título alcançável dentro da área de trabalho.</summary>
    public void Mover(App a, Vector2 posicao)
    {
        var j = janelas[a];
        j.area.position = Limitar(j, posicao);
    }

    Vector2 Limitar(Janela j, Vector2 p)
    {
        float visivel = 160f;
        return new Vector2(Mathf.Clamp(p.x, visivel - j.area.width, L - visivel), Mathf.Clamp(p.y, 0f, A - BarraAltura - TituloAltura));
    }

    // ------------------------------------------------------------------ desenho (uma passada por quadro)

    protected override void Desenhar()
    {
        if (dados == null || Event.current.type != EventType.Repaint) return;
        s = Escala;
        if (quadroDesenhado != Time.frameCount) { quadroDesenhado = Time.frameCount; TomarEntrada(); }
        else { clicou = soltou = false; rolagem = 0f; teclas.Clear(); }
        GUI.depth = -100;
        pilha.Clear();
        Alvos.Clear();
        origem = Vector2.zero;
        recorte = new Rect(0, 0, Screen.width, Screen.height);
        foreach (var j in janelas.Values) if (j.maximizada) j.area = new Rect(0, 0, L, A - BarraAltura);

        // Arraste da janela (independe de onde está o mouse).
        if (arrastando != null)
        {
            if (segurando) arrastando.area.position = Limitar(arrastando, arrasteArea0 + (mouse - arrasteMouse0) / s);
            else arrastando = null;
        }

        // Quem recebe o mouse: menu, barra, janela de cima ou área de trabalho.
        Rect menu = RetanguloMenu();
        Janela sob = null;
        string alvo = "area";
        if (menuAberto && R(menu.x, menu.y, menu.width, menu.height).Contains(mouse)) alvo = "menu";
        else if (mouse.y >= (A - BarraAltura) * s) alvo = "barra";
        else
        {
            for (int i = ordem.Count - 1; i >= 0; i--)
            {
                var j = ordem[i];
                if (j.minimizada) continue;
                if (R(j.area.x, j.area.y, j.area.width, j.area.height).Contains(mouse)) { sob = j; alvo = "janela"; break; }
            }
        }
        bool fecharMenu = menuAberto && clicou && alvo != "menu";

        interativo = alvo == "area";
        AreaDeTrabalho();

        frentePendente = null;
        foreach (var j in new List<Janela>(ordem))
        {
            if (j.minimizada || !j.aberta) continue;
            interativo = alvo == "janela" && j == sob;
            if (interativo && clicou) frentePendente = j;
            DesenharJanela(j);
        }

        interativo = alvo == "barra";
        BarraDeTarefas();
        if (menuAberto)
        {
            interativo = alvo == "menu";
            Menu(menu);
        }
        if (frentePendente != null && frentePendente.aberta) Frente(frentePendente);
        if (fecharMenu && !menuClicadoAgora) menuAberto = false;
        menuClicadoAgora = false;
        Teclado();
    }

    bool menuClicadoAgora;

    void Teclado()
    {
        if (teclas.Count == 0) return;
        App? f = Foco;
        if (f == null)
        {
            if (TeclaUi("baixo")) iconeSelecionado = Mathf.Min(IconesDaArea.Length - 1, iconeSelecionado + 1);
            if (TeclaUi("cima")) iconeSelecionado = Mathf.Max(0, iconeSelecionado - 1);
            if (TeclaUi("enter") && iconeSelecionado >= 0) AbrirApp(IconesDaArea[iconeSelecionado].app);
            return;
        }
        switch (f.Value)
        {
            case App.Correio: TecladoCorreio(); break;
            case App.Arquivos: TecladoArquivos(); break;
            case App.Notas: TecladoNotas(); break;
        }
    }

    // ------------------------------------------------------------------ área de trabalho

    static readonly (App app, string icone, string rotulo)[] IconesDaArea =
    {
        (App.Correio, "correio", "Correio"), (App.Arquivos, "pasta", "Arquivos"), (App.Notas, "nota", "Notas"), (App.Agenda, "agenda", "Agenda"),
    };

    void AreaDeTrabalho()
    {
        if (papelDeParede != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), papelDeParede, ScaleMode.ScaleAndCrop);
        else Caixa(new Rect(0, 0, Screen.width, Screen.height), Hex("#3E5470"));
        bool clicouEmIcone = false;
        for (int i = 0; i < IconesDaArea.Length; i++)
        {
            var (app, icone, rotulo) = IconesDaArea[i];
            Rect r = R(20, 20 + i * 104, 96, 96);
            Marcar("icone:" + app, r);
            bool sel = iconeSelecionado == i, sobre = Sobre(r);
            if (sel) Caixa(r, new Color(0.62f, 0.76f, 0.92f, 0.38f), 4f * s);
            else if (sobre) Caixa(r, new Color(1f, 1f, 1f, 0.14f), 4f * s);
            if (sel) Contorno(r, new Color(0.8f, 0.9f, 1f, 0.6f), Mathf.Max(1f, s), 4f * s);
            Icone(R(20 + 24, 20 + i * 104 + 8, 48, 48), icone + "_64");
            Rect rr = R(20 + 2, 20 + i * 104 + 60, 92, 24);
            Texto(new Rect(rr.x + s, rr.y + s, rr.width, rr.height), rotulo, Estilo(15, new Color(0, 0, 0, 0.55f), Peso.Normal, false, TextAnchor.UpperCenter));
            Texto(rr, rotulo, Estilo(15, Color.white, Peso.Normal, false, TextAnchor.UpperCenter));
            if (Clique(r))
            {
                clicouEmIcone = true;
                iconeSelecionado = i;
                if (cliques >= 2) AbrirApp(app);
            }
        }
        if (clicou && interativo && !clicouEmIcone && !consumido) iconeSelecionado = -1;
    }

    // ------------------------------------------------------------------ janela

    void DesenharJanela(Janela j)
    {
        Rect r = R(j.area.x, j.area.y, j.area.width, j.area.height);
        bool ativa = Foco == j.app;
        float raio = j.maximizada ? 0f : 6f * s;
        if (!j.maximizada) Sombra(r, raio);
        Caixa(r, CJanela, raio);
        Entrar(r);
        float w = r.width, th = TituloAltura * s;

        // Barra de título
        Rect barra = new Rect(0, 0, w, th);
        Caixa(barra, ativa ? CTitulo : CTituloInativo, raio);
        Caixa(new Rect(0, th * 0.5f, w, th * 0.5f), ativa ? CTitulo : CTituloInativo);
        if (ativa) Caixa(new Rect(raio, 0, w - 2 * raio, Mathf.Max(1f, s)), CTituloTopo);
        Icone(new Rect(10f * s, 7f * s, 20f * s, 20f * s), j.icone + "_24");
        float bw = 40f * s;
        Rotulo(new Rect(38f * s, 0, w - 38f * s - 3 * bw - 8f * s, th), TituloDe(j.app), Estilo(15, ativa ? Color.white : CTituloInativoTxt, Peso.Negrito, false, TextAnchor.MiddleLeft));

        bool podeMax = j.app != App.Sobre;
        Rect bFechar = new Rect(w - bw - 4f * s, 4f * s, bw, th - 8f * s);
        Rect bMax = new Rect(bFechar.x - bw - 2f * s, bFechar.y, bw, bFechar.height);
        Rect bMin = new Rect((podeMax ? bMax.x : bFechar.x) - bw - 2f * s, bFechar.y, bw, bFechar.height);
        Color hoverTitulo = new Color(1f, 1f, 1f, 0.18f);
        Color glifo = ativa ? Color.white : CTituloInativoTxt;
        bool acao = false;
        Marcar("titulo:" + j.app, new Rect(0, 0, bMin.x, th));
        Marcar("min:" + j.app, bMin);
        if (podeMax) Marcar("max:" + j.app, bMax);
        Marcar("fechar:" + j.app, bFechar);
        if (Botao(bMin, hoverTitulo)) { Minimizar(j.app); acao = true; }
        Icone(Centro(bMin, 18f * s), "g_min", glifo);
        if (podeMax)
        {
            if (Botao(bMax, hoverTitulo)) { AlternarMaximizar(j.app); acao = true; }
            Icone(Centro(bMax, 18f * s), j.maximizada ? "g_restore" : "g_max", glifo);
        }
        if (Botao(bFechar, CFechar)) { FecharApp(j.app); acao = true; }
        Icone(Centro(bFechar, 18f * s), "g_close", glifo);
        if (acao) frentePendente = j.aberta && !j.minimizada ? j : null;

        if (!acao && Clique(new Rect(0, 0, bMin.x, th)))
        {
            if (cliques >= 2 && podeMax) AlternarMaximizar(j.app);
            else if (!j.maximizada)
            {
                arrastando = j;
                arrasteMouse0 = mouse;
                arrasteArea0 = j.area.position;
            }
        }

        // Conteúdo
        if (j.aberta && !j.minimizada)
        {
            Rect corpo = new Rect(1f * s, th, w - 2f * s, r.height - th - 1f * s);
            Entrar(corpo);
            ConteudoDe(j.app, corpo.width, corpo.height);
            SairGrupo();
        }
        SairGrupo();
        if (!j.maximizada) Contorno(r, ativa ? CBordaJanela : Hex("#9AA5B2"), Mathf.Max(1f, s), raio);
    }

    static Rect Centro(Rect r, float lado) => new Rect(r.center.x - lado / 2, r.center.y - lado / 2, lado, lado);

    string TituloDe(App a)
    {
        switch (a)
        {
            case App.Correio: return $"Correio — {dados.estacao.usuario}";
            case App.Arquivos: return "Arquivos — " + CaminhoTexto(pastaAtual);
            case App.Leitor: return (leitorArquivo != null ? leitorArquivo.nome + " — " : "") + "Leitor de documentos";
            case App.Notas: return (notaAtual != null ? notaAtual.nome + " — " : "") + "Notas";
            case App.Imagens: return (imagemAtual != null ? imagemAtual.nome + " — " : "") + "Imagens";
            case App.Agenda: return "Agenda — recepção";
            default: return "Sobre esta estação";
        }
    }

    void ConteudoDe(App a, float w, float h)
    {
        switch (a)
        {
            case App.Correio: AppCorreio(w, h); break;
            case App.Arquivos: AppArquivos(w, h); break;
            case App.Leitor: AppLeitor(w, h); break;
            case App.Notas: AppNotas(w, h); break;
            case App.Imagens: AppImagens(w, h); break;
            case App.Agenda: AppAgenda(w, h); break;
            case App.Sobre: AppSobre(w, h); break;
        }
    }

    // ------------------------------------------------------------------ barra de tarefas e menu

    void BarraDeTarefas()
    {
        float y = A - BarraAltura;
        Rect barra = R(0, y, L, BarraAltura);
        Caixa(barra, CBarra);
        Linha(barra.x, barra.y, barra.width, CBarraTopo);

        // Menu
        Rect bMenu = R(6, y + 5, 112, BarraAltura - 10);
        Marcar("menu", bMenu);
        if (Botao(bMenu, CBarraHover, menuAberto ? CBarraHover : Color.clear, 4f)) { menuAberto = !menuAberto; menuClicadoAgora = true; }
        Icone(R(14, y + 10, 24, 24), "logo_24");
        Texto(R(44, y, 70, BarraAltura), "Menu", Estilo(15, CBarraTexto, Peso.Negrito, false, TextAnchor.MiddleLeft));

        // Janelas abertas
        float bx = 130, larg = Mathf.Min(220f, (L - 130 - 520) / Mathf.Max(1, tarefas.Count) - 6);
        foreach (var j in new List<Janela>(tarefas))
        {
            Rect b = R(bx, y + 5, larg, BarraAltura - 10);
            bool frente = Foco == j.app;
            Marcar("tarefa:" + j.app, b);
            if (Botao(b, CBarraHover, frente ? Hex("#4D5A68") : j.minimizada ? Color.clear : CBarraBotao, 4f)) BotaoDaTarefa(j.app);
            if (frente) Caixa(new Rect(b.x + 6f * s, b.yMax - 3f * s, b.width - 12f * s, 2f * s), Hex("#8FB4DC"), s);
            Icone(new Rect(b.x + 8f * s, b.y + 7f * s, 20f * s, 20f * s), j.icone + "_24", j.minimizada ? new Color(1, 1, 1, 0.65f) : Color.white);
            Rotulo(new Rect(b.x + 34f * s, b.y, b.width - 40f * s, b.height), NomeCurto(j.app),
                Estilo(14, j.minimizada ? Hex("#AEB8C3") : CBarraTexto, Peso.Normal, false, TextAnchor.MiddleLeft));
            bx += larg + 6;
        }

        // Área de notificação: correio, sessão, relógio, sair
        float x = L - 8;
        Rect bSair = R(x - 104, y + 6, 104, BarraAltura - 12);
        Marcar("sair", bSair);
        if (Botao(bSair, Hex("#7A3A34"), Hex("#4A3A3C"), 4f)) PedirFechar();
        Icone(new Rect(bSair.x + 8f * s, bSair.y + 7f * s, 18f * s, 18f * s), "g_power");
        Texto(new Rect(bSair.x + 28f * s, bSair.y, bSair.width - 30f * s, bSair.height), "Sair (Esc)", Estilo(14, CBarraTexto, Peso.Normal, false, TextAnchor.MiddleCenter));
        x -= 104 + 14;
        Texto(R(x - 92, y + 3, 92, 22), Relogio, Estilo(16, CBarraTexto, Peso.Negrito, false, TextAnchor.MiddleRight));
        Texto(R(x - 150, y + 23, 150, 18), $"{Abrev(DiaAtual)} {DataAtual}", Estilo(12, Hex("#B8C1CB"), Peso.Normal, false, TextAnchor.MiddleRight));
        x -= 166;
        Texto(R(x - 130, y, 130, BarraAltura), dados.estacao.usuario, Estilo(14, Hex("#C9D1DA"), Peso.Normal, false, TextAnchor.MiddleRight));
        x -= 140;
        int nl = NaoLidas();
        Rect bMail = R(x - 52, y + 6, 52, BarraAltura - 12);
        if (Botao(bMail, CBarraHover, Color.clear, 4f)) AbrirApp(App.Correio);
        Icone(new Rect(bMail.x + 6f * s, bMail.y + 7f * s, 20f * s, 20f * s), "correio_24");
        if (nl > 0)
        {
            Rect badge = new Rect(bMail.x + 26f * s, bMail.y + 4f * s, 20f * s, 16f * s);
            Caixa(badge, Hex("#D9534F"), 8f * s);
            Texto(badge, nl.ToString(), Estilo(11, Color.white, Peso.Negrito, false, TextAnchor.MiddleCenter));
        }
    }

    static string Abrev(string dia) => string.IsNullOrEmpty(dia) ? "" : dia.Substring(0, 3);

    string NomeCurto(App a)
    {
        switch (a)
        {
            case App.Correio: return NaoLidas() > 0 ? $"Correio ({NaoLidas()})" : "Correio";
            case App.Arquivos: return "Arquivos";
            case App.Leitor: return leitorArquivo != null ? leitorArquivo.nome : "Leitor de documentos";
            case App.Notas: return notaAtual != null ? notaAtual.nome : "Notas";
            case App.Imagens: return imagemAtual != null ? imagemAtual.nome : "Imagens";
            case App.Agenda: return "Agenda";
            default: return "Sobre esta estação";
        }
    }

    static readonly (App? app, string icone, string rotulo)[] ItensDoMenu =
    {
        (App.Correio, "correio", "Correio"), (App.Arquivos, "pasta", "Arquivos"), (App.Notas, "nota", "Notas"), (App.Agenda, "agenda", "Agenda"),
        (null, null, null), (App.Sobre, "estacao", "Sobre esta estação"),
    };

    Rect RetanguloMenu()
    {
        float h = 78 + 40 * 5 + 14 + 48;
        return new Rect(6, A - BarraAltura - h - 4, 320, h);
    }

    void Menu(Rect m)
    {
        Rect r = R(m.x, m.y, m.width, m.height);
        Sombra(r, 6f * s);
        Caixa(r, Hex("#F7F8FA"), 6f * s);
        Contorno(r, CBordaJanela, Mathf.Max(1f, s), 6f * s);
        // Cabeçalho da sessão
        Rect cab = new Rect(r.x, r.y, r.width, 70f * s);
        Caixa(cab, Hex("#41709F"), 6f * s);
        Caixa(new Rect(cab.x, cab.y + 35f * s, cab.width, 35f * s), Hex("#41709F"));
        Rect av = new Rect(r.x + 14f * s, r.y + 13f * s, 44f * s, 44f * s);
        Caixa(av, CorDe("nina"), 22f * s);
        Texto(av, Iniciais(dados.estacao.nomeUsuario), Estilo(17, Color.white, Peso.Negrito, false, TextAnchor.MiddleCenter));
        Texto(new Rect(r.x + 70f * s, r.y + 14f * s, r.width - 80f * s, 24f * s), dados.estacao.nomeUsuario, Estilo(16, Color.white, Peso.Negrito));
        Texto(new Rect(r.x + 70f * s, r.y + 38f * s, r.width - 80f * s, 20f * s), dados.estacao.setor, Estilo(13, Hex("#DCE6F0")));
        float y = r.y + 78f * s;
        foreach (var (app, icone, rotulo) in ItensDoMenu)
        {
            if (app == null)
            {
                Linha(r.x + 12f * s, y + 6f * s, r.width - 24f * s, CBorda);
                y += 14f * s;
                continue;
            }
            Rect it = new Rect(r.x + 6f * s, y, r.width - 12f * s, 38f * s);
            Marcar("menu:" + app.Value, it);
            if (Botao(it, CHover)) { AbrirApp(app.Value); menuAberto = false; }
            Icone(new Rect(it.x + 10f * s, it.y + 7f * s, 24f * s, 24f * s), icone + "_24");
            Texto(new Rect(it.x + 44f * s, it.y, it.width - 50f * s, it.height), rotulo, Estilo(15, CTexto, Peso.Normal, false, TextAnchor.MiddleLeft));
            y += 40f * s;
        }
        Rect sair = new Rect(r.x + 6f * s, r.yMax - 46f * s, r.width - 12f * s, 40f * s);
        Linha(r.x + 12f * s, sair.y - 4f * s, r.width - 24f * s, CBorda);
        if (Botao(sair, Hex("#F3DEDB"))) { menuAberto = false; PedirFechar(); }
        Icone(new Rect(sair.x + 12f * s, sair.y + 10f * s, 20f * s, 20f * s), "g_power", Hex("#A0473D"));
        Texto(new Rect(sair.x + 44f * s, sair.y, sair.width - 50f * s, sair.height), "Sair da estação (Esc)", Estilo(15, Hex("#8A3B33"), Peso.Normal, false, TextAnchor.MiddleLeft));
    }

    static Color CorDe(string pessoa)
    {
        switch (pessoa)
        {
            case "helena": return Hex("#7A5C99");
            case "mauro": return Hex("#8A6B3D");
            case "nina": return Hex("#3A7D74");
            case "celso": return Hex("#4F6D9A");
            default: return Hex("#6B7480");
        }
    }
}
