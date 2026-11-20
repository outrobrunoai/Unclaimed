using UnityEngine;

/// <summary>
/// Verifica se o jogador está vendo uma porta (dentro do campo de visão e com linha de visão livre).
/// </summary>
public static class OlharDoJogador
{
    public const float AnguloPadrao = 60f;      // meio-ângulo em volta do centro da visão (cobre o campo de visão inteiro)
    public const float DistanciaPadrao = 30f;

    const int MascaraVisao = ~(1 << 2);
    const float ToleranciaBorda = 0.15f;

    /// <summary>Ponto dentro do cone do olhar e da distância máxima.</summary>
    public static bool DentroDoOlhar(Vector3 olhos, Vector3 frente, Vector3 ponto, float angulo, float distanciaMaxima)
    {
        if (angulo < 0f || distanciaMaxima <= 0f || frente.sqrMagnitude < 1e-8f) return false;
        Vector3 para = ponto - olhos;
        float distSq = para.sqrMagnitude;
        if (distSq > distanciaMaxima * distanciaMaxima) return false;
        if (distSq < 1e-8f) return true;
        return Vector3.Angle(frente, para) <= angulo;
    }

    /// <summary>Verdadeiro se algum ponto da folha está dentro do olhar e com linha de visão livre.</summary>
    public static bool Vendo(FolhaDePorta folha)
    {
        if (folha == null) return false;
        Transform olhos = Jogador.Olhos;
        if (olhos == null) return false;

        Vector3 origem = olhos.position;
        Vector3 frente = olhos.forward;

        Renderer[] renderers = folha.GetComponentsInChildren<Renderer>();
        bool temBounds = false;
        Bounds bounds = default;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!temBounds) { bounds = r.bounds; temBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!temBounds)
            return PontoVisivel(origem, frente, folha.transform.position, folha);

        Vector3 centro = bounds.center;
        Vector3 desloc = bounds.size.x >= bounds.size.z
            ? new Vector3(bounds.size.x * 0.35f, 0f, 0f)
            : new Vector3(0f, 0f, bounds.size.z * 0.35f);

        return PontoVisivel(origem, frente, centro, folha)
            || PontoVisivel(origem, frente, centro + desloc, folha)
            || PontoVisivel(origem, frente, centro - desloc, folha);
    }

    static bool PontoVisivel(Vector3 olhos, Vector3 frente, Vector3 ponto, FolhaDePorta folha)
    {
        return NaTela(olhos, frente, ponto) && LinhaDeVisaoLivre(olhos, ponto, folha);
    }

    // Enquadramento real da câmera do jogador (com uma folga pequena nas bordas); sem câmera, o cone.
    static bool NaTela(Vector3 olhos, Vector3 frente, Vector3 ponto)
    {
        if ((ponto - olhos).sqrMagnitude > DistanciaPadrao * DistanciaPadrao) return false;
        Camera cam = Jogador.Olhos != null ? Jogador.Olhos.GetComponentInChildren<Camera>() : null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return DentroDoOlhar(olhos, frente, ponto, AnguloPadrao, DistanciaPadrao);
        Vector3 v = cam.WorldToViewportPoint(ponto);
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }

    static bool LinhaDeVisaoLivre(Vector3 olhos, Vector3 ponto, FolhaDePorta folha)
    {
        Vector3 para = ponto - olhos;
        float distancia = para.magnitude;
        if (distancia < 1e-4f) return true;

        if (!Physics.Linecast(olhos, ponto, out RaycastHit acerto, MascaraVisao, QueryTriggerInteraction.Ignore))
            return true;

        Transform jogador = Jogador.Movimento != null ? Jogador.Movimento.transform : null;
        if (acerto.collider == null || jogador == null || !acerto.collider.transform.IsChildOf(jogador))
            return AcertoLivre(acerto, ponto, folha);

        // Primeiro acerto foi o próprio jogador: procura o obstáculo mais próximo além dele.
        RaycastHit[] acertos = Physics.RaycastAll(olhos, para / distancia, distancia, MascaraVisao, QueryTriggerInteraction.Ignore);
        bool achou = false;
        RaycastHit maisPerto = default;
        foreach (RaycastHit h in acertos)
        {
            if (h.collider == null || h.collider.transform.IsChildOf(jogador)) continue;
            if (!achou || h.distance < maisPerto.distance) { maisPerto = h; achou = true; }
        }
        return !achou || AcertoLivre(maisPerto, ponto, folha);
    }

    static bool AcertoLivre(RaycastHit acerto, Vector3 ponto, FolhaDePorta folha)
    {
        if (acerto.collider == null) return true;
        if (acerto.collider.GetComponentInParent<FolhaDePorta>() == folha) return true;
        return Vector3.Distance(acerto.point, ponto) < ToleranciaBorda;
    }
}
