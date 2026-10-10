using UnityEngine;

/// <summary>
/// O que o jogador está mirando: um raio pelo centro da câmera, calculado uma vez por frame e
/// compartilhado pelas interações que usam a mesma tecla. A primeira superfície atingida decide,
/// então nada é acionado através de paredes. Interações miradas (portas) têm prioridade sobre as
/// pistas do ObjetoInspecionavel, que só são escolhidas por proximidade quando nenhuma porta está na mira.
/// </summary>
public static class MiraDeInteracao
{
    /// <summary>Distância máxima do raio; cada interação ainda aplica o próprio alcance.</summary>
    public const float DistanciaMaxima = 6f;

    static int frame = -1;
    static FolhaDePorta porta;
    static RaycastHit acerto;

    /// <summary>Folha de porta mirada e ao alcance neste frame (null se nenhuma).</summary>
    public static FolhaDePorta PortaMirada { get { Atualizar(); return porta; } }

    /// <summary>
    /// Verdadeiro quando outro sistema captura o controle: inspeção de pista, diário aberto ou cursor
    /// solto (menu, Esc). Interações do mundo não devem responder nesse estado.
    /// </summary>
    public static bool ControleCapturado =>
        ObjetoInspecionavel.InspecionandoAlgo || DiarioDePistas.Aberto || Cursor.lockState != CursorLockMode.Locked;

    static void Atualizar()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        porta = null;
        if (ControleCapturado) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        // A câmera fica dentro da cápsula do jogador; raios não atingem o collider em que começam.
        var raio = new Ray(cam.transform.position, cam.transform.forward);
        int mascara = ~(1 << 2);   // ignora a layer "Ignore Raycast"
        if (!Physics.Raycast(raio, out acerto, DistanciaMaxima, mascara, QueryTriggerInteraction.Ignore)) return;
        FolhaDePorta folha = acerto.collider.GetComponentInParent<FolhaDePorta>();
        if (folha != null && folha.isActiveAndEnabled && acerto.distance <= folha.alcance) porta = folha;
    }
}
