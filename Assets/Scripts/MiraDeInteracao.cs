using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// O que o jogador está mirando: um raio pelo centro da câmera, calculado uma vez por frame e
/// compartilhado pelas interações que usam a mesma tecla. A primeira superfície atingida decide,
/// então nada é acionado através de paredes. Interações miradas (AlvoDeInteracao: portas, gavetas,
/// armários, torneira, descarga) têm prioridade sobre as pistas do ObjetoInspecionavel, que só são
/// escolhidas por proximidade quando nenhum alvo está na mira.
/// </summary>
public static class MiraDeInteracao
{
    /// <summary>Distância máxima do raio; cada interação ainda aplica o próprio alcance.</summary>
    public const float DistanciaMaxima = 6f;

    static int frame = -1, bloqueadoAte = -1;
    static AlvoDeInteracao alvo;
    static RaycastHit acerto;

    /// <summary>Alvo mirado e ao alcance neste frame (null se nenhum).</summary>
    public static AlvoDeInteracao Alvo { get { Atualizar(); return alvo; } }

    /// <summary>Folha de porta mirada e ao alcance neste frame (null se nenhuma).</summary>
    public static FolhaDePorta PortaMirada => Alvo as FolhaDePorta;

    /// <summary>
    /// Verdadeiro quando outro sistema captura o controle: inspeção de pista, diário aberto, interface de tela
    /// (computador, cadeado), cursor solto (menu, Esc) ou o quadro logo depois de uma interface fechar.
    /// Interações do mundo não devem responder nesse estado.
    /// </summary>
    public static bool ControleCapturado =>
        ObjetoInspecionavel.InspecionandoAlgo || DiarioDePistas.Aberto || InterfaceDeTela.AlgumaAberta ||
        Cursor.lockState != CursorLockMode.Locked || Time.frameCount <= bloqueadoAte;

    /// <summary>Nenhum alvo neste quadro nem no próximo (a tecla que fechou uma interface não aciona outra).</summary>
    public static void BloquearNoProximoQuadro()
    {
        bloqueadoAte = Time.frameCount + 1;
        frame = -1;
    }

    /// <summary>Aviso curto na parte de baixo da tela ("Porta destrancada.").</summary>
    public static void Avisar(string texto, float segundos = 2.5f) => AvisoNaTela.Mostrar(texto, segundos);

    static void Atualizar()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        alvo = null;
        if (ControleCapturado) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        // A câmera fica dentro da cápsula do jogador; raios não atingem o collider em que começam.
        var raio = new Ray(cam.transform.position, cam.transform.forward);
        int mascara = ~(1 << 2);   // ignora a layer "Ignore Raycast"
        if (!Physics.Raycast(raio, out acerto, DistanciaMaxima, mascara, QueryTriggerInteraction.Ignore)) return;
        AlvoDeInteracao a = acerto.collider.GetComponentInParent<AlvoDeInteracao>();
        // Uma pista guardada dentro de uma gaveta ou porta (papel na gaveta, legenda colada na porta) é dela:
        // mirando o próprio objeto, a inspeção vence a peça que o carrega.
        var pista = acerto.collider.GetComponentInParent<ObjetoInspecionavel>();
        if (a != null && pista != null && pista.isActiveAndEnabled && pista.transform.IsChildOf(a.transform) && pista.transform != a.transform)
            a = null;
        if (a != null && a.isActiveAndEnabled && acerto.distance <= a.AlcanceDeInteracao) alvo = a;
    }

    /// <summary>Indicação sob a mira: só "Aperte E para interagir" (o texto da ação não aparece).</summary>
    public static void Indicar(string tecla, string texto)
    {
        if (!string.IsNullOrEmpty(texto)) HudInteracao.Interagir(tecla);
    }

    public static bool TeclaApertada(string nomeTecla)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null || !Enum.TryParse(nomeTecla, true, out Key k)) return false;
        return Keyboard.current[k].wasPressedThisFrame;
#else
        return Enum.TryParse(nomeTecla, true, out KeyCode k) && Input.GetKeyDown(k);
#endif
    }
}

/// <summary>Desenha o aviso de MiraDeInteracao.Avisar (objeto criado sob demanda, fora da cena salva).</summary>
public class AvisoNaTela : MonoBehaviour
{
    static AvisoNaTela instancia;
    string texto;
    float ate;
    public static string Atual => instancia != null && Time.unscaledTime < instancia.ate ? instancia.texto : "";

    public static void Mostrar(string texto, float segundos)
    {
        if (string.IsNullOrEmpty(texto) || !Application.isPlaying) return;
        if (instancia == null)
        {
            var go = new GameObject("Avisos na tela");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<AvisoNaTela>();
        }
        instancia.texto = texto;
        instancia.ate = Time.unscaledTime + segundos;
    }

    void OnGUI()
    {
        if (Time.unscaledTime >= ate || string.IsNullOrEmpty(texto)) return;
        HudInteracao.Texto(texto, 0.72f, Mathf.Clamp01((ate - Time.unscaledTime) / 0.4f));
    }
}
