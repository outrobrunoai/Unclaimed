using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Peças de uGUI montadas por código no visual dos menus (fonte do menu, texto cinza, destaque vermelho-sangue):
/// retângulos, textos TMP e botões de texto. Usado pelas opções, pela pausa e pelo diálogo de confirmação.
/// </summary>
public class UiMenu
{
    public readonly TMP_FontAsset fonte;
    public readonly Color corTexto, corDestaque;

    public UiMenu(TMP_FontAsset fonte, Color corTexto, Color corDestaque)
    {
        this.fonte = fonte;
        this.corTexto = corTexto;
        this.corDestaque = corDestaque;
    }

    public static RectTransform Rect(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = 5;   // UI
        var rect = (RectTransform)go.transform;
        rect.SetParent(pai, false);
        return rect;
    }

    public static void Esticar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static Image Imagem(GameObject go, Color cor)
    {
        var imagem = go.AddComponent<Image>();
        imagem.color = cor;
        return imagem;
    }

    public TextMeshProUGUI Texto(string nome, Transform pai, string texto, float tamanho, TextAlignmentOptions alinhamento,
        Vector2 posicao, Vector2 dimensao)
    {
        RectTransform rect = Rect(nome, pai);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = dimensao;
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (fonte != null) tmp.font = fonte;
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.color = corTexto;
        tmp.alignment = alinhamento;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    public Button BotaoTexto(string nome, Transform pai, string texto, float tamanho, Vector2 posicao, Vector2 dimensao,
        UnityEngine.Events.UnityAction aoClicar)
    {
        RectTransform rect = Rect(nome, pai);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = dimensao;
        Imagem(rect.gameObject, Color.clear);   // área de clique

        TextMeshProUGUI tmp = Texto("Texto", rect, texto, tamanho, TextAlignmentOptions.Center, Vector2.zero, dimensao);
        tmp.color = Color.white;   // a cor final vem do tint do botão

        var botao = rect.gameObject.AddComponent<Button>();
        botao.targetGraphic = tmp;
        botao.colors = Cores(corTexto, corDestaque);
        botao.navigation = new Navigation { mode = Navigation.Mode.None };
        botao.onClick.AddListener(aoClicar);
        return botao;
    }

    /// <summary>Painel escuro de fundo com borda fina, centrado, no tamanho dado (pixels de 1080p).</summary>
    public RectTransform Painel(string nome, Transform pai, Vector2 tamanho, Color cor)
    {
        RectTransform painel = Rect(nome, pai);
        painel.sizeDelta = tamanho;
        Imagem(painel.gameObject, cor);
        var borda = painel.gameObject.AddComponent<Outline>();
        borda.effectColor = new Color(corTexto.r, corTexto.g, corTexto.b, 0.25f);
        borda.effectDistance = new Vector2(2f, -2f);
        return painel;
    }

    public static ColorBlock Cores(Color normal, Color destaque)
    {
        ColorBlock cores = ColorBlock.defaultColorBlock;
        cores.normalColor = normal;
        cores.selectedColor = normal;   // clicar não deixa o botão "aceso"
        cores.highlightedColor = destaque;
        cores.pressedColor = destaque * 0.8f;
        cores.disabledColor = new Color(normal.r, normal.g, normal.b, 0.3f);
        cores.fadeDuration = 0.08f;
        return cores;
    }

    /// <summary>Escala que leva um painel desenhado para 1920x1080 a caber na área dada.</summary>
    public static float EscalaPara(Vector2 area) => Mathf.Min(area.y / 1080f, area.x / 1920f);

    /// <summary>
    /// Cria um EventSystem (Input System) na cena ativa se não houver um; sem ele a UI não recebe cliques.
    /// Fica na cena (some com ela): a próxima cena usa o próprio, sem dois EventSystems disputando a entrada.
    /// </summary>
    public static void GarantirEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static Transform ProcurarFilho(Transform raiz, string nome)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nome) return t;
        return null;
    }
}

/// <summary>
/// Caixa "tem certeza?" sobre a tela inteira (bloqueia o que está atrás). Confirmar chama a ação e fecha;
/// Cancelar só fecha. Quem abre trata o Esc chamando Cancelar (Aberto diz se há uma na tela).
/// </summary>
public class DialogoConfirmacao : MonoBehaviour
{
    public static DialogoConfirmacao Aberto { get; private set; }

    RectTransform painel;
    Vector2 areaAnterior;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Aberto = null;

    public static DialogoConfirmacao Mostrar(Transform canvas, UiMenu ui, string titulo, string mensagem,
        string textoConfirmar, Action aoConfirmar, string textoCancelar = "CANCELAR")
    {
        if (Aberto != null) Aberto.Cancelar();

        RectTransform tela = UiMenu.Rect("Confirmacao", canvas);
        UiMenu.Esticar(tela);
        tela.SetAsLastSibling();
        UiMenu.Imagem(tela.gameObject, new Color(0f, 0f, 0f, 0.7f));
        var dialogo = tela.gameObject.AddComponent<DialogoConfirmacao>();

        var tamanho = new Vector2(900f, 380f);
        RectTransform painel = ui.Painel("Painel", tela, tamanho, new Color(0.03f, 0.05f, 0.05f, 0.97f));
        dialogo.painel = painel;

        ui.Texto("Titulo", painel, titulo, 44, TextAlignmentOptions.Center, new Vector2(0f, 120f), new Vector2(tamanho.x - 80f, 60f));
        TextMeshProUGUI corpo = ui.Texto("Mensagem", painel, mensagem, 30, TextAlignmentOptions.Center,
            new Vector2(0f, 20f), new Vector2(tamanho.x - 120f, 130f));
        corpo.textWrappingMode = TextWrappingModes.Normal;

        ui.BotaoTexto("Cancelar", painel, textoCancelar, 34, new Vector2(-200f, -125f), new Vector2(300f, 60f), dialogo.Cancelar);
        ui.BotaoTexto("Confirmar", painel, textoConfirmar, 34, new Vector2(200f, -125f), new Vector2(300f, 60f), () =>
        {
            dialogo.Fechar();
            aoConfirmar?.Invoke();
        });

        Aberto = dialogo;
        dialogo.Update();
        return dialogo;
    }

    public void Cancelar() => Fechar();

    void Fechar()
    {
        if (Aberto == this) Aberto = null;
        EventSystem.current?.SetSelectedGameObject(null);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Aberto == this) Aberto = null;
    }

    void Update()
    {
        Vector2 area = ((RectTransform)transform).rect.size;
        if (area == areaAnterior) return;
        areaAnterior = area;
        painel.localScale = Vector3.one * UiMenu.EscalaPara(area);
    }
}
