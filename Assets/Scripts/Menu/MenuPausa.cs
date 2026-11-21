using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu de pausa da gameplay. Coloque em qualquer objeto da cena do jogo (ex.: um vazio "Menu de pausa"):
/// ao iniciar, cria o próprio Canvas, o painel (Continuar, Salvar jogo, Opções, Sair para o menu) e a tela de
/// opções (MenuOpcoes).
///
/// Esc pausa: congela o tempo (timeScale 0) e o áudio, desliga o Movimento e solta o cursor. Esc de novo fecha
/// o que estiver por cima (confirmação, opções) e, por último, volta ao jogo. Enquanto uma pista, o diário ou
/// uma interface de tela (computador, quadro, cadeado) estiver aberta, o Esc é deles e a pausa não abre,
/// nem no quadro em que eles fecham.
/// </summary>
public class MenuPausa : MonoBehaviour
{
    [Tooltip("Cena do menu principal (precisa estar na lista de cenas do build).")]
    public string cenaDoMenu = "main";

    [Header("Aparência")]
    public TMP_FontAsset fonte;
    public Color corTexto = new Color(0.82f, 0.82f, 0.82f);
    public Color corDestaque = new Color(0.62f, 0.06f, 0.05f);
    public Color corPainel = new Color(0.03f, 0.05f, 0.05f, 0.92f);

    public static bool Pausado { get; private set; }

    UiMenu ui;
    Canvas canvas;
    RectTransform tela, painel;
    MenuOpcoes opcoes;
    TextMeshProUGUI aviso;
    float avisoAte;
    bool capturadoNoQuadroAnterior;
    float escalaDeTempoAnterior = 1f;
    readonly List<Behaviour> desativados = new List<Behaviour>();
    Vector2 areaAnterior;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Pausado = false;

#if UNITY_EDITOR
    // Ao adicionar o componente no Editor, já vem com a fonte do menu principal.
    void Reset()
    {
        fonte = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Materials/JMH Typewriter-Thin SDF.asset");
    }
#endif

    // Outro sistema está usando o Esc (e os controles) agora.
    static bool OutroSistemaCapturando =>
        ObjetoInspecionavel.InspecionandoAlgo || DiarioDePistas.Aberto || InterfaceDeTela.AlgumaAberta ||
        IntroChegada.Ativa || TelaDeFinal.Ativa || DanielPerseguidor.Capturando;

    void Start()
    {
        UiMenu.GarantirEventSystem();
        ui = new UiMenu(fonte, corTexto, corDestaque);

        var go = new GameObject("Menu de pausa - Canvas", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // acima de qualquer HUD de uGUI
        go.AddComponent<CanvasScaler>();   // Constant Pixel Size: a escala do painel é ajustada em Update
        go.AddComponent<GraphicRaycaster>();

        Montar();
        tela.gameObject.SetActive(false);

        opcoes = gameObject.AddComponent<MenuOpcoes>();
        opcoes.canvas = canvas;
        opcoes.fonte = fonte;
        opcoes.corTexto = corTexto;
        opcoes.corDestaque = corDestaque;
        opcoes.menuPrincipal = painel.gameObject;
        opcoes.botaoOpcoes = UiMenu.ProcurarFilho(painel, "Opcoes").GetComponent<Button>();
        opcoes.fecharComEsc = false;   // o Esc passa por aqui (ordem: confirmação, opções, pausa)
    }

    void OnDestroy()
    {
        if (Pausado) RestaurarJogo();
    }

    void Update()
    {
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (Pausado)
        {
            if (esc)
            {
                if (DialogoConfirmacao.Aberto != null) DialogoConfirmacao.Aberto.Cancelar();
                else if (opcoes.Aberto) opcoes.Fechar();
                else Continuar();
            }
            AjustarEscala();
            if (aviso.alpha > 0f) aviso.alpha = Mathf.Clamp01((avisoAte - Time.unscaledTime) / 0.5f);
        }
        else if (esc && !capturadoNoQuadroAnterior && !OutroSistemaCapturando)
        {
            Pausar();
        }
    }

    // O Esc que fecha uma pista ou interface é lido por ela no Update; olhar o estado do quadro anterior
    // impede que o mesmo Esc também abra a pausa (a ordem dos Updates não importa).
    void LateUpdate() => capturadoNoQuadroAnterior = OutroSistemaCapturando;

    public void Pausar()
    {
        if (Pausado || IntroChegada.Ativa || TelaDeFinal.Ativa || DanielPerseguidor.Capturando) return;
        Pausado = true;

        escalaDeTempoAnterior = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        desativados.Clear();
        foreach (Movimento mov in FindObjectsByType<Movimento>())
        {
            if (!mov.enabled) continue;
            mov.enabled = false;
            desativados.Add(mov);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        aviso.alpha = 0f;
        painel.gameObject.SetActive(true);
        tela.gameObject.SetActive(true);
        areaAnterior = Vector2.zero;
        AjustarEscala();
    }

    public void Continuar()
    {
        if (!Pausado) return;
        if (opcoes.Aberto) opcoes.Fechar();
        tela.gameObject.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
        RestaurarJogo();
        MiraDeInteracao.BloquearNoProximoQuadro();
    }

    void RestaurarJogo()
    {
        Pausado = false;
        Time.timeScale = escalaDeTempoAnterior > 0f ? escalaDeTempoAnterior : 1f;
        AudioListener.pause = false;
        foreach (Behaviour b in desativados)
        {
            if (b == null) continue;
            if (b is Movimento mov) mov.olhar = Vector2.zero;
            b.enabled = true;
        }
        desativados.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void SalvarJogo()
    {
        string motivo = Salvamento.PorQueNaoPodeSalvar();
        bool ok = motivo == null && Salvamento.Salvar();
        aviso.text = ok ? "Jogo salvo." : motivo ?? "Não foi possível salvar.";
        aviso.color = ok ? corTexto : corDestaque;
        aviso.alpha = 1f;
        avisoAte = Time.unscaledTime + 2.5f;
    }

    void SairParaOMenu()
    {
        DialogoConfirmacao.Mostrar(canvas.transform, ui, "SAIR PARA O MENU",
            "O progresso desde o último salvamento será perdido.", "SAIR", () =>
            {
                RestaurarJogo();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene(cenaDoMenu);
            });
    }

    void AjustarEscala()
    {
        Vector2 area = tela.rect.size;
        if (area == areaAnterior) return;
        areaAnterior = area;
        painel.localScale = Vector3.one * UiMenu.EscalaPara(area);
    }

    void Montar()
    {
        tela = UiMenu.Rect("Pausa - tela", canvas.transform);
        UiMenu.Esticar(tela);
        UiMenu.Imagem(tela.gameObject, new Color(0f, 0f, 0f, 0.65f));

        var tamanho = new Vector2(620f, 640f);
        painel = ui.Painel("Painel", tela, tamanho, corPainel);

        ui.Texto("Titulo", painel, "PAUSADO", 56, TextAlignmentOptions.Center, new Vector2(0f, 240f), new Vector2(tamanho.x, 80f));

        var botoes = new (string nome, string texto, UnityEngine.Events.UnityAction acao)[]
        {
            ("Continuar", "CONTINUAR", Continuar),
            ("Salvar", "SALVAR JOGO", SalvarJogo),
            ("Opcoes", "OPÇÕES", null),   // ligado pelo MenuOpcoes
            ("Sair", "SAIR PARA O MENU", SairParaOMenu),
        };
        float y = 120f;
        foreach (var b in botoes)
        {
            ui.BotaoTexto(b.nome, painel, b.texto, 40, new Vector2(0f, y), new Vector2(460f, 64f), b.acao ?? (() => { }));
            y -= 82f;
        }

        aviso = ui.Texto("Aviso", painel, "", 28, TextAlignmentOptions.Center, new Vector2(0f, -265f), new Vector2(tamanho.x, 50f));
        aviso.alpha = 0f;
    }
}
