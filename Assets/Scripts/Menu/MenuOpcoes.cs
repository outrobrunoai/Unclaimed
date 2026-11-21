using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela de opções. Coloque no Canvas do menu: ao iniciar, monta o painel (abas Vídeo, Áudio e Controles; cada
/// linha com o nome à esquerda e o controle à direita) e liga o clique do botão "Opcoes".
/// Abrir esconde o menuPrincipal; Voltar ou Esc fecham e salvam. Cada mudança vale na hora (Configuracoes).
///
/// Referências vazias são procuradas pelo nome dentro do Canvas ("Opcoes", "ContainerMenu"), e a fonte vem do
/// texto do botão, então basta adicionar o componente. O MenuPausa cria um destes com as referências preenchidas.
/// </summary>
public class MenuOpcoes : MonoBehaviour
{
    [Header("Menu")]
    [Tooltip("Botão que abre as opções. Vazio = procura um objeto chamado \"Opcoes\" no Canvas.")]
    public Button botaoOpcoes;
    [Tooltip("O que some enquanto as opções estão abertas. Vazio = procura \"ContainerMenu\".")]
    public GameObject menuPrincipal;
    [Tooltip("Onde o painel é criado. Vazio = o Canvas em que este objeto está.")]
    public Canvas canvas;
    [Tooltip("Esc fecha as opções. Desligado quando outro script (MenuPausa) cuida do Esc.")]
    public bool fecharComEsc = true;

    [Header("Aparência")]
    [Tooltip("Vazio = a mesma fonte do botão de opções.")]
    public TMP_FontAsset fonte;
    public Color corTexto = new Color(0.82f, 0.82f, 0.82f);
    public Color corDestaque = new Color(0.62f, 0.06f, 0.05f);
    public Color corPainel = new Color(0.03f, 0.05f, 0.05f, 0.92f);
    public Color corFundo = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("Tamanho do painel numa tela 1920x1080; acompanha a altura da tela.")]
    public Vector2 tamanhoPainel = new Vector2(1180f, 760f);

    /// <summary>Disparado ao fechar (Voltar ou Esc), depois de salvar.</summary>
    public event Action Fechado;
    public bool Aberto => tela != null && tela.gameObject.activeSelf;

    const float AlturaLinha = 72f, LarguraControle = 460f;
    static readonly string[] NomesAbas = { "VÍDEO", "ÁUDIO", "CONTROLES" };

    UiMenu ui;
    RectTransform tela, painel;
    readonly List<TextMeshProUGUI> textosAbas = new List<TextMeshProUGUI>();
    readonly List<Image> marcasAbas = new List<Image>();
    readonly List<GameObject> paginas = new List<GameObject>();
    // Cada controle se redesenha a partir das Configuracoes (abrir, restaurar padrões).
    readonly List<Action> atualizacoes = new List<Action>();
    Vector2 tamanhoTelaAnterior;

    void Start()
    {
        Configuracoes.Carregar();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        Transform raiz = canvas.rootCanvas.transform;

        if (botaoOpcoes == null) botaoOpcoes = UiMenu.ProcurarFilho(raiz, "Opcoes")?.GetComponent<Button>();
        if (menuPrincipal == null) menuPrincipal = UiMenu.ProcurarFilho(raiz, "ContainerMenu")?.gameObject;
        if (fonte == null && botaoOpcoes != null) fonte = botaoOpcoes.GetComponentInChildren<TMP_Text>(true)?.font;
        ui = new UiMenu(fonte, corTexto, corDestaque);

        Montar(raiz);
        tela.gameObject.SetActive(false);

        if (botaoOpcoes != null) botaoOpcoes.onClick.AddListener(Abrir);
        else Debug.LogWarning("MenuOpcoes: botão \"Opcoes\" não encontrado; arraste-o no Inspector.", this);
    }

    void OnDestroy()
    {
        if (botaoOpcoes != null) botaoOpcoes.onClick.RemoveListener(Abrir);
    }

    void Update()
    {
        if (!Aberto) return;
        AjustarEscala();
        if (fecharComEsc && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Fechar();
    }

    public void Abrir()
    {
        foreach (Action atualizar in atualizacoes) atualizar();
        MostrarAba(0);
        if (menuPrincipal != null) menuPrincipal.SetActive(false);
        tela.gameObject.SetActive(true);
        tela.SetAsLastSibling();
        tamanhoTelaAnterior = Vector2.zero;
        AjustarEscala();
    }

    public void Fechar()
    {
        if (!Aberto) return;
        Configuracoes.Salvar();
        tela.gameObject.SetActive(false);
        if (menuPrincipal != null) menuPrincipal.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(null);
        Fechado?.Invoke();
    }

    // O painel é desenhado para 1080 de altura e escalado para a tela atual.
    void AjustarEscala()
    {
        Vector2 tamanho = tela.rect.size;
        if (tamanho == tamanhoTelaAnterior) return;
        tamanhoTelaAnterior = tamanho;
        painel.localScale = Vector3.one * UiMenu.EscalaPara(tamanho);
    }

    void MostrarAba(int indice)
    {
        for (int i = 0; i < paginas.Count; i++)
        {
            bool ativa = i == indice;
            paginas[i].SetActive(ativa);
            textosAbas[i].color = ativa ? Color.white : corTexto * 0.7f;
            marcasAbas[i].enabled = ativa;
        }
    }

    // ---------------------------------------------------------------- montagem

    void Montar(Transform raiz)
    {
        // Fundo escuro na tela inteira (também bloqueia cliques no que estiver atrás).
        tela = UiMenu.Rect("Opcoes - tela", raiz);
        UiMenu.Esticar(tela);
        UiMenu.Imagem(tela.gameObject, corFundo);

        painel = ui.Painel("Painel", tela, tamanhoPainel, corPainel);

        float largura = tamanhoPainel.x, altura = tamanhoPainel.y;
        float topo = altura / 2f;

        ui.Texto("Titulo", painel, "OPÇÕES", 52, TextAlignmentOptions.Center,
            new Vector2(0f, topo - 60f), new Vector2(largura, 70f));

        // Abas
        float larguraAba = (largura - 120f) / NomesAbas.Length;
        for (int i = 0; i < NomesAbas.Length; i++)
        {
            int indice = i;
            float x = -largura / 2f + 60f + larguraAba * (i + 0.5f);
            Button aba = ui.BotaoTexto("Aba " + NomesAbas[i], painel, NomesAbas[i], 36,
                new Vector2(x, topo - 140f), new Vector2(larguraAba - 20f, 56f), () => MostrarAba(indice));
            textosAbas.Add(aba.GetComponentInChildren<TextMeshProUGUI>());
            RectTransform marca = UiMenu.Rect("Marca", aba.transform);
            marca.anchorMin = new Vector2(0.15f, 0f); marca.anchorMax = new Vector2(0.85f, 0f);
            marca.sizeDelta = new Vector2(0f, 4f); marca.anchoredPosition = new Vector2(0f, -2f);
            marcasAbas.Add(UiMenu.Imagem(marca.gameObject, corDestaque));
        }
        RectTransform linhaAbas = UiMenu.Rect("Divisoria", painel);
        linhaAbas.sizeDelta = new Vector2(largura - 120f, 1f);
        linhaAbas.anchoredPosition = new Vector2(0f, topo - 172f);
        UiMenu.Imagem(linhaAbas.gameObject, new Color(corTexto.r, corTexto.g, corTexto.b, 0.2f));

        // Páginas
        float inicioLinhas = topo - 230f;
        paginas.Add(PaginaVideo(inicioLinhas));
        paginas.Add(PaginaAudio(inicioLinhas));
        paginas.Add(PaginaControles(inicioLinhas));

        // Rodapé
        ui.BotaoTexto("Restaurar padroes", painel, "RESTAURAR PADRÕES", 30,
            new Vector2(-largura / 2f + 220f, -topo + 55f), new Vector2(380f, 56f), () =>
            {
                Configuracoes.RestaurarPadroes();
                foreach (Action atualizar in atualizacoes) atualizar();
            });
        ui.BotaoTexto("Voltar", painel, "VOLTAR", 36,
            new Vector2(largura / 2f - 130f, -topo + 55f), new Vector2(200f, 56f), Fechar);
    }

    GameObject PaginaVideo(float y)
    {
        RectTransform pagina = NovaPagina("Pagina Video");

        List<Vector2Int> resolucoes = Configuracoes.ResolucoesDisponiveis();
        string[] nomesResolucao = resolucoes.ConvertAll(r => r.x + " x " + r.y).ToArray();
        Seletor(pagina, ref y, "Resolução", nomesResolucao,
            () => Mathf.Max(0, resolucoes.IndexOf(new Vector2Int(Configuracoes.ResolucaoLargura, Configuracoes.ResolucaoAltura))),
            i => Configuracoes.DefinirResolucao(resolucoes[i].x, resolucoes[i].y));

        FullScreenMode[] modos = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        Seletor(pagina, ref y, "Modo de tela", new[] { "Tela cheia", "Tela cheia (janela)", "Janela" },
            () => Mathf.Max(0, Array.IndexOf(modos, Configuracoes.ModoDeTela)),
            i => Configuracoes.DefinirModoDeTela(modos[i]));

        string[] qualidades = Array.ConvertAll(QualitySettings.names, NomeDaQualidade);
        Seletor(pagina, ref y, "Qualidade gráfica", qualidades,
            () => Configuracoes.Qualidade, Configuracoes.DefinirQualidade);

        SimNao(pagina, ref y, "Sincronização vertical", () => Configuracoes.VSync, Configuracoes.DefinirVSync);

        string[] nomesFps = Array.ConvertAll(Configuracoes.LimitesDeFps, f => f > 0 ? f + " FPS" : "Sem limite");
        Seletor(pagina, ref y, "Limite de FPS", nomesFps,
            () => Mathf.Max(0, Array.IndexOf(Configuracoes.LimitesDeFps, Configuracoes.LimiteDeFps)),
            i => Configuracoes.DefinirLimiteDeFps(Configuracoes.LimitesDeFps[i]),
            ativo: () => !Configuracoes.VSync);

        Deslizante(pagina, ref y, "Brilho", 0f, 1f, () => Configuracoes.Brilho, Configuracoes.DefinirBrilho,
            v => Mathf.RoundToInt(v * 100f) + "%");
        return pagina.gameObject;
    }

    GameObject PaginaAudio(float y)
    {
        RectTransform pagina = NovaPagina("Pagina Audio");
        Deslizante(pagina, ref y, "Volume geral", 0f, 1f, () => Configuracoes.VolumeGeral, Configuracoes.DefinirVolumeGeral,
            v => Mathf.RoundToInt(v * 100f) + "%");
        SimNao(pagina, ref y, "Som em segundo plano", () => Configuracoes.SomEmSegundoPlano, Configuracoes.DefinirSomEmSegundoPlano);
        SimNao(pagina, ref y, "Usar microfone", () => Configuracoes.UsarMicrofone, Configuracoes.DefinirUsarMicrofone);
        Deslizante(pagina, ref y, "Sensibilidade do microfone", 0f, 1f, () => Configuracoes.SensibilidadeMicrofone,
            Configuracoes.DefinirSensibilidadeMicrofone, v => Mathf.RoundToInt(v * 100f) + "%");
        return pagina.gameObject;
    }

    GameObject PaginaControles(float y)
    {
        RectTransform pagina = NovaPagina("Pagina Controles");
        Deslizante(pagina, ref y, "Sensibilidade do mouse", Configuracoes.SensibilidadeMin, Configuracoes.SensibilidadeMax,
            () => Configuracoes.Sensibilidade, Configuracoes.DefinirSensibilidade, v => v.ToString("0.0") + "x");
        SimNao(pagina, ref y, "Inverter eixo Y", () => Configuracoes.InverterY, Configuracoes.DefinirInverterY);
        Deslizante(pagina, ref y, "Campo de visão", Configuracoes.CampoDeVisaoMin, Configuracoes.CampoDeVisaoMax,
            () => Configuracoes.CampoDeVisao, v => Configuracoes.DefinirCampoDeVisao(Mathf.Round(v)), v => Mathf.RoundToInt(v) + "°");
        Deslizante(pagina, ref y, "Balanço da câmera", 0f, 1f, () => Configuracoes.IntensidadeBalanco,
            v => Configuracoes.DefinirIntensidadeBalanco(Mathf.Round(v * 20f) / 20f),   // passos de 5%
            v => v <= 0f ? "Desl." : Mathf.RoundToInt(v * 100f) + "%");
        return pagina.gameObject;
    }

    static string NomeDaQualidade(string nome)
    {
        switch (nome)
        {
            case "Mobile": return "Baixa";
            case "PC": return "Alta";
            default: return nome;
        }
    }

    RectTransform NovaPagina(string nome)
    {
        RectTransform pagina = UiMenu.Rect(nome, painel);
        UiMenu.Esticar(pagina);
        return pagina;
    }

    // ---------------------------------------------------------------- controles

    // Nome à esquerda, controle à direita; desce y para a próxima linha.
    RectTransform Linha(RectTransform pagina, ref float y, string rotulo)
    {
        float largura = tamanhoPainel.x - 160f;
        RectTransform linha = UiMenu.Rect(rotulo, pagina);
        linha.sizeDelta = new Vector2(largura, AlturaLinha);
        linha.anchoredPosition = new Vector2(0f, y);
        ui.Texto("Rotulo", linha, rotulo, 32, TextAlignmentOptions.Left,
            new Vector2(-largura / 2f + (largura - LarguraControle) / 2f, 0f), new Vector2(largura - LarguraControle, AlturaLinha));
        y -= AlturaLinha;
        return linha;
    }

    Vector2 PosicaoControle => new Vector2((tamanhoPainel.x - 160f) / 2f - LarguraControle / 2f, 0f);

    // "<  valor  >": as setas percorrem as opções em círculo.
    void Seletor(RectTransform pagina, ref float y, string rotulo, string[] opcoes,
        Func<int> ler, Action<int> escrever, Func<bool> ativo = null)
    {
        RectTransform linha = Linha(pagina, ref y, rotulo);
        RectTransform controle = UiMenu.Rect("Seletor", linha);
        controle.sizeDelta = new Vector2(LarguraControle, AlturaLinha);
        controle.anchoredPosition = PosicaoControle;

        TextMeshProUGUI valor = ui.Texto("Valor", controle, "", 30, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(LarguraControle - 120f, AlturaLinha));
        TextMeshProUGUI nome = linha.Find("Rotulo").GetComponent<TextMeshProUGUI>();
        Button anterior = null, proximo = null;

        void Redesenhar()
        {
            int i = Mathf.Clamp(ler(), 0, opcoes.Length - 1);
            valor.text = opcoes.Length > 0 ? opcoes[i] : "-";
            bool habilitado = ativo == null || ativo();
            anterior.interactable = proximo.interactable = habilitado;
            valor.alpha = nome.alpha = habilitado ? 1f : 0.35f;
        }

        void Passo(int direcao)
        {
            if (opcoes.Length == 0) return;
            escrever((ler() + direcao + opcoes.Length) % opcoes.Length);
            foreach (Action atualizar in atualizacoes) atualizar();   // ex.: VSync habilita/desabilita o limite de FPS
        }

        anterior = ui.BotaoTexto("Anterior", controle, "<", 40, new Vector2(-LarguraControle / 2f + 30f, 0f),
            new Vector2(60f, AlturaLinha), () => Passo(-1));
        proximo = ui.BotaoTexto("Proximo", controle, ">", 40, new Vector2(LarguraControle / 2f - 30f, 0f),
            new Vector2(60f, AlturaLinha), () => Passo(1));
        atualizacoes.Add(Redesenhar);
    }

    void SimNao(RectTransform pagina, ref float y, string rotulo, Func<bool> ler, Action<bool> escrever)
    {
        Seletor(pagina, ref y, rotulo, new[] { "Desligado", "Ligado" }, () => ler() ? 1 : 0, i => escrever(i == 1));
    }

    void Deslizante(RectTransform pagina, ref float y, string rotulo, float min, float max,
        Func<float> ler, Action<float> escrever, Func<float, string> formatar)
    {
        RectTransform linha = Linha(pagina, ref y, rotulo);
        float larguraBarra = LarguraControle - 110f;

        RectTransform barra = UiMenu.Rect("Deslizante", linha);
        barra.sizeDelta = new Vector2(larguraBarra, 30f);
        barra.anchoredPosition = PosicaoControle - new Vector2(55f, 0f);

        RectTransform trilho = UiMenu.Rect("Trilho", barra);
        trilho.anchorMin = new Vector2(0f, 0.5f); trilho.anchorMax = new Vector2(1f, 0.5f);
        trilho.sizeDelta = new Vector2(0f, 4f);
        UiMenu.Imagem(trilho.gameObject, new Color(corTexto.r, corTexto.g, corTexto.b, 0.3f));

        RectTransform areaPreenchida = UiMenu.Rect("Area preenchida", barra);
        areaPreenchida.anchorMin = new Vector2(0f, 0.5f); areaPreenchida.anchorMax = new Vector2(1f, 0.5f);
        areaPreenchida.sizeDelta = new Vector2(0f, 4f);
        RectTransform preenchido = UiMenu.Rect("Preenchido", areaPreenchida);
        preenchido.sizeDelta = Vector2.zero;
        UiMenu.Imagem(preenchido.gameObject, corDestaque);

        RectTransform areaPegador = UiMenu.Rect("Area do pegador", barra);
        UiMenu.Esticar(areaPegador);
        RectTransform pegador = UiMenu.Rect("Pegador", areaPegador);
        pegador.sizeDelta = new Vector2(16f, 30f);
        Image imagemPegador = UiMenu.Imagem(pegador.gameObject, corTexto);

        var slider = barra.gameObject.AddComponent<Slider>();
        slider.fillRect = preenchido;
        slider.handleRect = pegador;
        slider.targetGraphic = imagemPegador;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.colors = UiMenu.Cores(corTexto, Color.white);

        TextMeshProUGUI valor = ui.Texto("Valor", linha, "", 30, TextAlignmentOptions.Right,
            PosicaoControle + new Vector2(LarguraControle / 2f - 45f, 0f), new Vector2(90f, AlturaLinha));

        slider.onValueChanged.AddListener(v =>
        {
            escrever(v);
            valor.text = formatar(ler());
        });
        atualizacoes.Add(() =>
        {
            slider.SetValueWithoutNotify(ler());
            valor.text = formatar(ler());
        });
    }
}
