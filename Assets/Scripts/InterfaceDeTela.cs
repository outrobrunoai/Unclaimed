using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Interação mirada que abre uma interface de tela (computador, cadeado): ao abrir, trava o movimento e o olhar
/// do jogador, solta o cursor e bloqueia as demais interações (MiraDeInteracao.ControleCapturado); ao fechar
/// (Esc ou botão), devolve os controles e o cursor como estavam. Só uma interface fica aberta por vez.
///
/// O fechamento acontece no LateUpdate, depois do Update do jogador, e a mira fica bloqueada no quadro seguinte:
/// a tecla que fecha a interface não chega ao Movimento nem dispara outra interação.
/// </summary>
public abstract class InterfaceDeTela : AlvoDeInteracao
{
    /// <summary>A interface aberta agora (null se nenhuma).</summary>
    public static InterfaceDeTela Aberta { get; private set; }
    public static bool AlgumaAberta => Aberta != null;

    public bool EstaAberta => Aberta == this;

    /// <summary>Escala da interface: 1 em 1080p, 1,33 em 1440p (medidas pensadas em pixels de 1080p).</summary>
    protected static float Escala => Mathf.Max(0.5f, Screen.height / 1080f);

    bool pedidoFechar;
    CursorLockMode travaAnterior;
    bool cursorVisivelAnterior;
    readonly List<Behaviour> desativados = new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Aberta = null;

    public void AbrirInterface()
    {
        if (Aberta != null) return;
        Aberta = this;
        pedidoFechar = false;
        desativados.Clear();
        foreach (var mov in FindObjectsByType<Movimento>())
        {
            if (!mov.enabled) continue;
            mov.enabled = false;   // movimento, olhar e o Esc/clique que travam e soltam o cursor
            desativados.Add(mov);
        }
        travaAnterior = Cursor.lockState;
        cursorVisivelAnterior = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AoAbrir();
    }

    /// <summary>Pede o fechamento; ele acontece no LateUpdate deste quadro.</summary>
    public void FecharInterface()
    {
        if (EstaAberta) pedidoFechar = true;
    }

    void Concluir()
    {
        pedidoFechar = false;
        Aberta = null;
        foreach (Behaviour b in desativados)
        {
            if (b == null) continue;
            if (b is Movimento mov) mov.olhar = Vector2.zero;   // sem giro acumulado enquanto a tela estava aberta
            b.enabled = true;
        }
        desativados.Clear();
        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;
        MiraDeInteracao.BloquearNoProximoQuadro();
        AoFechar();
    }

    protected override void Update()
    {
        if (EstaAberta)
        {
            if (TeclaEscape()) PedirFechar();
            else AtualizarAberta();
            return;
        }
        base.Update();
    }

    protected virtual void LateUpdate()
    {
        if (EstaAberta && pedidoFechar) Concluir();
    }

    protected override void OnGUI()
    {
        if (EstaAberta) Desenhar();
        else base.OnGUI();
    }

    protected virtual void OnDisable()
    {
        if (EstaAberta) Concluir();
    }

    /// <summary>Esc (e botões de sair) pedem o fechamento por aqui; o padrão fecha neste quadro.</summary>
    protected virtual void PedirFechar() => FecharInterface();

    /// <summary>Chamado ao abrir (estado da sessão é mantido entre aberturas).</summary>
    protected virtual void AoAbrir() { }
    protected virtual void AoFechar() { }
    /// <summary>Update enquanto aberta (teclado da interface).</summary>
    protected virtual void AtualizarAberta() { }
    /// <summary>OnGUI enquanto aberta.</summary>
    protected abstract void Desenhar();

    protected static bool TeclaEscape()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>Textura 1x1 de cor sólida para os estilos da interface.</summary>
    protected static Texture2D Cor(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    protected static Color Hex(string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        c.a = a;
        return c;
    }
}
