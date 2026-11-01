using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Botões do menu principal. Coloque no Canvas do menu; se o Canvas não tiver um MenuOpcoes, ele é criado aqui.
///
/// Sem save: o botão "Comecar" inicia um jogo novo. Com save: ele vira CONTINUAR (carrega o save) e logo abaixo
/// aparece NOVO JOGO (uma cópia dele, com o mesmo efeito de sangue), que pede confirmação antes de apagar o save;
/// os botões de baixo descem uma linha. "Sair" fecha o jogo.
/// Referências vazias são procuradas pelo nome dentro do Canvas.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Tooltip("Cena da gameplay (precisa estar na lista de cenas do build).")]
    public string cenaDoJogo = "PrototipoNecroterio";
    [Tooltip("Vazio = procura \"Comecar\" no Canvas.")]
    public Button botaoComecar;
    [Tooltip("Vazio = procura \"Sair\" no Canvas.")]
    public Button botaoSair;

    [Header("Aparência do aviso de novo jogo")]
    public Color corTexto = new Color(0.82f, 0.82f, 0.82f);
    public Color corDestaque = new Color(0.62f, 0.06f, 0.05f);

    UiMenu ui;
    Transform raiz;
    bool carregando;

    void Start()
    {
        // Voltando da gameplay: tempo, áudio e cursor livres.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        raiz = GetComponentInParent<Canvas>().rootCanvas.transform;
        // A tela de opções não depende de alguém lembrar de pôr o MenuOpcoes na cena (e salvá-la).
        if (raiz.GetComponentInChildren<MenuOpcoes>(true) == null) gameObject.AddComponent<MenuOpcoes>();
        if (botaoComecar == null) botaoComecar = UiMenu.ProcurarFilho(raiz, "Comecar")?.GetComponent<Button>();
        if (botaoSair == null) botaoSair = UiMenu.ProcurarFilho(raiz, "Sair")?.GetComponent<Button>();
        if (botaoComecar == null)
        {
            Debug.LogWarning("MenuPrincipal: botão \"Comecar\" não encontrado; arraste-o no Inspector.", this);
            return;
        }

        TMP_Text textoComecar = botaoComecar.GetComponentInChildren<TMP_Text>(true);
        ui = new UiMenu(textoComecar != null ? textoComecar.font : null, corTexto, corDestaque);

        if (Salvamento.ExisteSave)
        {
            Button novoJogo = CriarBotaoAbaixo(botaoComecar, "Novo jogo", "NOVO JOGO");
            DefinirTexto(botaoComecar, "CONTINUAR");
            botaoComecar.onClick.AddListener(Continuar);
            novoJogo.onClick.AddListener(ConfirmarNovoJogo);
        }
        else
        {
            botaoComecar.onClick.AddListener(NovoJogo);
        }

        if (botaoSair != null) botaoSair.onClick.AddListener(Sair);
    }

    void Update()
    {
        if (DialogoConfirmacao.Aberto != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            DialogoConfirmacao.Aberto.Cancelar();
    }

    void Continuar()
    {
        if (carregando) return;
        carregando = true;
        if (!Salvamento.Continuar())
        {
            // Save ilegível (versão antiga, arquivo corrompido): avisa e oferece começar do zero.
            carregando = false;
            DialogoConfirmacao.Mostrar(raiz, ui, "SAVE INDISPONÍVEL",
                "Não foi possível carregar o jogo salvo. Deseja iniciar um novo jogo?", "NOVO JOGO", NovoJogo);
        }
    }

    void ConfirmarNovoJogo()
    {
        DialogoConfirmacao.Mostrar(raiz, ui, "NOVO JOGO",
            "Iniciar um novo jogo apagará o seu progresso salvo. Deseja continuar?", "CONFIRMAR", NovoJogo);
    }

    void NovoJogo()
    {
        if (carregando) return;
        carregando = true;
        Salvamento.NovoJogo(cenaDoJogo);
    }

    void Sair()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Copia o botão (com o hover de sangue) logo abaixo dele e desce uma linha os que estavam abaixo.
    static Button CriarBotaoAbaixo(Button modelo, string nome, string texto)
    {
        var rect = (RectTransform)modelo.transform;
        Transform pai = rect.parent;
        float espacamento = 28f;
        float maisPerto = float.MaxValue;
        foreach (Transform irmao in pai)
        {
            if (irmao == rect || !(irmao is RectTransform r)) continue;
            float d = rect.anchoredPosition.y - r.anchoredPosition.y;
            if (d > 0.01f && d < maisPerto) maisPerto = d;
        }
        if (maisPerto < float.MaxValue) espacamento = maisPerto;

        foreach (Transform irmao in pai)
            if (irmao is RectTransform r && r.anchoredPosition.y < rect.anchoredPosition.y - 0.01f)
                r.anchoredPosition -= new Vector2(0f, espacamento);

        Button copia = Instantiate(modelo, pai);
        copia.name = nome;
        copia.onClick.RemoveAllListeners();
        var rectCopia = (RectTransform)copia.transform;
        rectCopia.SetSiblingIndex(rect.GetSiblingIndex() + 1);
        rectCopia.anchoredPosition = rect.anchoredPosition - new Vector2(0f, espacamento);
        DefinirTexto(copia, texto);
        return copia;
    }

    // Troca o texto e alarga a área de clique se o texto novo for maior.
    static void DefinirTexto(Button botao, string texto)
    {
        TMP_Text tmp = botao.GetComponentInChildren<TMP_Text>(true);
        if (tmp == null) return;
        tmp.text = texto;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        var rect = (RectTransform)botao.transform;
        float necessario = tmp.GetPreferredValues(texto).x + 8f;
        if (necessario > rect.sizeDelta.x) rect.sizeDelta = new Vector2(necessario, rect.sizeDelta.y);
    }
}
