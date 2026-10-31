using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reflexo planar de um espelho (URP): uma câmera auxiliar, refletida no plano do espelho a partir da câmera
/// principal, renderiza numa RenderTexture que o shader "Unclaimed/Espelho planar" lê em espaço de tela.
///
/// - Pose: posição e direções da câmera principal refletidas no plano. Para não inverter o sentido dos
///   triângulos (culling), a câmera auxiliar usa a rotação própria mais próxima, que gera a imagem espelhada na
///   horizontal; o shader compensa lendo a textura com u invertido.
/// - Recorte: plano de corte oblíquo no plano do espelho, então nada atrás da parede aparece no reflexo.
/// - Recursão: o próprio espelho (renderer inteiro) não é desenhado pela câmera auxiliar.
/// - Custo: a câmera só fica ativa quando o espelho está no frustum da câmera principal, de frente para ela,
///   perto o bastante e não totalmente encoberto (linhas até os cantos e o centro); a textura usa uma fração da
///   resolução da tela, com teto.
/// - Janela: o shader só lê o reflexo onde o espelho aparece na tela, então a câmera renderiza só essa janela
///   (frustum e viewport recortados, mesma densidade de pixels) e o shader recebe a origem dela em
///   _ReflexoJanela. Medido em 2026-10-19: com o espelho do WC visível a 7,6 m o quadro inteiro refletido dobrava
///   casters (12 mil) e triângulos (15,8 M) da cena, e o Editor caía de 165 para ~75 FPS.
/// Fora do Play Mode (ou sem câmera principal) o shader cai na sonda de reflexo da sala.
/// </summary>
[DisallowMultipleComponent]
public class EspelhoPlanar : MonoBehaviour
{
    [Tooltip("Renderer do espelho e índice da submalha da face espelhada (recebe a textura do reflexo).")]
    public Renderer espelho;
    public int submalha;
    [Tooltip("Ponto e normal (para fora, para a sala) da face espelhada, no espaço deste objeto.")]
    public Vector3 pontoLocal;
    public Vector3 normalLocal = Vector3.back;

    [Header("Custo")]
    [Range(0.2f, 1f)] public float escalaDeResolucao = 0.6f;
    [Min(128)] public int larguraMaxima = 1280;
    [Min(1f)] public float distanciaMaxima = 9f;
    [Tooltip("Layers que a câmera do reflexo não desenha (além das já ocultas na câmera principal).")]
    public LayerMask ocultarNoReflexo = 0;
    [Tooltip("Renderiza só a janela do espelho na tela, não o quadro inteiro. Desligado volta ao comportamento antigo.")]
    public bool recortarNaJanela = true;
    [Tooltip("Folga (fração da tela) em volta do espelho na janela recortada.")]
    [Range(0f, 0.1f)] public float folgaDaJanela = 0.015f;
    [Tooltip("Até onde, além do plano do espelho, a câmera do reflexo desenha (0 = até o far da câmera principal). " +
             "Também limita a distância das sombras dela.")]
    [Min(0f)] public float alcanceDoReflexo = 0f;
    [Tooltip("Fração da área da tela que o espelho ocupa abaixo da qual só a sonda de reflexo da sala é usada " +
             "(o planar entra misturado até o dobro desse valor). Medido em 2026-10-19: com o espelho do WC a ~7 m, " +
             "em ângulo rasante (0,2% da tela), planar e sonda diferiam em 0,0% dos pixels e o planar custava ~7 ms no Editor.")]
    [Range(0f, 0.2f)] public float areaMinimaNaTela = 0.01f;
    public bool sombrasNoReflexo = true;

    static readonly int TexturaId = Shader.PropertyToID("_ReflexoTex"), AtivoId = Shader.PropertyToID("_ReflexoAtivo"),
        JanelaId = Shader.PropertyToID("_ReflexoJanela");

    Camera camReflexo;
    RenderTexture textura;
    MaterialPropertyBlock bloco;
    readonly Plane[] planos = new Plane[6];
    bool renderizando;
    Vector4 janela = new Vector4(0f, 0f, 1f, 1f);   // origem e tamanho na imagem do reflexo (u já invertido)

    /// <summary>Verdadeiro se o reflexo foi renderizado no último frame (para testes e depuração).</summary>
    public bool Ativo => camReflexo != null && camReflexo.enabled;
    /// <summary>Câmera do reflexo neste frame (frustum já recortado na janela do espelho); null se o espelho está inativo.</summary>
    public Camera CameraDoReflexo => Ativo ? camReflexo : null;

    void OnEnable()
    {
        bloco ??= new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += AntesDeRenderizar;
        RenderPipelineManager.endCameraRendering += DepoisDeRenderizar;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= AntesDeRenderizar;
        RenderPipelineManager.endCameraRendering -= DepoisDeRenderizar;
        Desligar();
        if (camReflexo != null) Destroy(camReflexo.gameObject);
        if (textura != null) { textura.Release(); Destroy(textura); }
        camReflexo = null;
        textura = null;
    }

    // Depois do Update do jogador (que gira a câmera): a pose do reflexo é a deste frame.
    void LateUpdate()
    {
        Camera principal = Camera.main;
        if (espelho == null || principal == null || !Visivel(principal)) { Desligar(); return; }
        bool temJanela = JanelaDoEspelho(principal, out Vector4 janelaNaTela, out float area);
        float mistura = areaMinimaNaTela <= 0f || !temJanela
            ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(areaMinimaNaTela, areaMinimaNaTela * 2f, area));
        if (mistura <= 0f) { Desligar(); return; }
        Preparar(principal);
        Posicionar(principal, recortarNaJanela && temJanela ? janelaNaTela : new Vector4(0f, 0f, 1f, 1f));
        camReflexo.enabled = true;
        espelho.GetPropertyBlock(bloco, submalha);
        bloco.SetTexture(TexturaId, textura);
        bloco.SetFloat(AtivoId, mistura);
        bloco.SetVector(JanelaId, janela);
        espelho.SetPropertyBlock(bloco, submalha);
    }

    void Desligar()
    {
        if (camReflexo != null) camReflexo.enabled = false;
        if (espelho != null && bloco != null)
        {
            espelho.GetPropertyBlock(bloco, submalha);
            bloco.SetFloat(AtivoId, 0f);
            espelho.SetPropertyBlock(bloco, submalha);
        }
    }

    Vector3 Ponto => transform.TransformPoint(pontoLocal);
    Vector3 Normal => transform.TransformDirection(normalLocal).normalized;

    bool Visivel(Camera c)
    {
        Vector3 o = Ponto, n = Normal, p = c.transform.position;
        float lado = Vector3.Dot(p - o, n);
        if (lado < 0.01f || lado > distanciaMaxima) return false;   // atrás do espelho ou longe demais
        GeometryUtility.CalculateFrustumPlanes(c, planos);
        Bounds b = espelho.bounds;
        if (!GeometryUtility.TestPlanesAABB(planos, b)) return false;
        // Encoberto por paredes ou pela porta? Basta uma linha livre até o centro ou um canto.
        Vector3 e = b.extents, centro = b.center + n * 0.02f;
        Vector3 u = Vector3.Cross(n, Vector3.up).normalized;
        float largura = Mathf.Abs(Vector3.Dot(e, new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)))) * 0.9f;
        float altura = e.y * 0.9f;
        for (int i = 0; i < 5; i++)
        {
            Vector3 alvo = i == 0 ? centro : centro + u * (i % 2 == 0 ? largura : -largura) + Vector3.up * (i < 3 ? altura : -altura);
            if (!Physics.Linecast(p, alvo, ~(1 << 2), QueryTriggerInteraction.Ignore)) return true;
        }
        return false;
    }

    void Preparar(Camera principal)
    {
        int largura = Mathf.Min(larguraMaxima, Mathf.Max(64, Mathf.RoundToInt(principal.pixelWidth * escalaDeResolucao)));
        int altura = Mathf.Max(64, Mathf.RoundToInt(largura * principal.pixelHeight / (float)Mathf.Max(1, principal.pixelWidth)));
        if (textura == null || textura.width != largura || textura.height != altura)
        {
            if (textura != null) { textura.Release(); Destroy(textura); }
            textura = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Reflexo - " + name, antiAliasing = 1, useMipMap = false, wrapMode = TextureWrapMode.Clamp,
            };
        }
        if (camReflexo == null)
        {
            var go = new GameObject("Camera do reflexo - " + name) { hideFlags = HideFlags.HideAndDontSave };
            camReflexo = go.AddComponent<Camera>();
            camReflexo.enabled = false;
            var dados = camReflexo.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = false;
            dados.requiresColorOption = CameraOverrideOption.Off;
            dados.requiresDepthOption = CameraOverrideOption.Off;
            dados.antialiasing = AntialiasingMode.None;
            dados.renderShadows = true;
        }
        camReflexo.targetTexture = textura;
        camReflexo.depth = principal.depth - 1;   // antes da câmera principal no mesmo frame
        camReflexo.clearFlags = principal.clearFlags;
        camReflexo.backgroundColor = principal.backgroundColor;
        camReflexo.cullingMask = principal.cullingMask & ~ocultarNoReflexo.value;
        camReflexo.allowHDR = principal.allowHDR;
        camReflexo.allowMSAA = false;
        camReflexo.useOcclusionCulling = false;
    }

    void Posicionar(Camera principal, Vector4 janelaNaTela)
    {
        Vector3 o = Ponto, n = Normal;
        Transform c = principal.transform;
        Vector3 p = c.position - 2f * Vector3.Dot(c.position - o, n) * n;
        Vector3 frente = Vector3.Reflect(c.forward, n), cima = Vector3.Reflect(c.up, n);
        camReflexo.transform.SetPositionAndRotation(p, Quaternion.LookRotation(frente, cima));

        janela = janelaNaTela;
        Matrix4x4 recorte = Matrix4x4.identity;
        if (janela.z < 1f || janela.w < 1f)
        {
            // NDC da janela na câmera do reflexo; o recorte leva essa faixa para [-1, 1] (em coordenadas de corte).
            float x0 = 2f * janela.x - 1f, x1 = 2f * (janela.x + janela.z) - 1f;
            float y0 = 2f * janela.y - 1f, y1 = 2f * (janela.y + janela.w) - 1f;
            float meioX = (x1 - x0) * 0.5f, meioY = (y1 - y0) * 0.5f;
            recorte.m00 = 1f / meioX; recorte.m03 = -(x0 + x1) * 0.5f / meioX;
            recorte.m11 = 1f / meioY; recorte.m13 = -(y0 + y1) * 0.5f / meioY;
        }
        camReflexo.rect = new Rect(0f, 0f, janela.z, janela.w);   // mesma densidade de pixels que o quadro inteiro

        camReflexo.ResetProjectionMatrix();
        camReflexo.fieldOfView = principal.fieldOfView;
        camReflexo.aspect = principal.aspect;
        camReflexo.nearClipPlane = principal.nearClipPlane;
        float alcance = alcanceDoReflexo > 0f ? Vector3.Dot(c.position - o, n) + alcanceDoReflexo : principal.farClipPlane;
        camReflexo.farClipPlane = Mathf.Min(principal.farClipPlane, Mathf.Max(principal.nearClipPlane + 1f, alcance));
        camReflexo.GetUniversalAdditionalCameraData().renderShadows = sombrasNoReflexo;
        Matrix4x4 projecao = recorte * camReflexo.projectionMatrix;
        // Plano de corte no espelho (um milímetro para dentro da sala), em espaço de câmera, lado visível positivo.
        Matrix4x4 w2c = camReflexo.worldToCameraMatrix;
        Vector3 cp = w2c.MultiplyPoint(o + n * 0.001f);
        Vector3 cn = w2c.MultiplyVector(n).normalized;
        camReflexo.projectionMatrix = recorte * camReflexo.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
        camReflexo.cullingMatrix = projecao * w2c;   // o frustum oblíquo deformaria o culling
    }

    // Janela do espelho na tela da câmera principal (viewport 0..1), já no sentido da imagem do reflexo (u invertido).
    // Falso se algum canto está atrás da câmera ou perto demais: aí vale o quadro inteiro. area é a fração da tela.
    bool JanelaDoEspelho(Camera c, out Vector4 recorteNaTela, out float area)
    {
        recorteNaTela = new Vector4(0f, 0f, 1f, 1f);
        area = 1f;
        Bounds b = espelho.bounds;
        float x0 = 1f, y0 = 1f, x1 = 0f, y1 = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 canto = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 v = c.WorldToViewportPoint(canto);
            if (v.z <= 0.05f) return false;
            x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x);
            y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
        }
        x0 = Mathf.Clamp01(x0 - folgaDaJanela); x1 = Mathf.Clamp01(x1 + folgaDaJanela);
        y0 = Mathf.Clamp01(y0 - folgaDaJanela); y1 = Mathf.Clamp01(y1 + folgaDaJanela);
        area = Mathf.Max(0f, x1 - x0) * Mathf.Max(0f, y1 - y0);
        // Janela mínima para o recorte não degenerar (a área já foi medida acima).
        if (x1 - x0 < 0.02f) { float m = (x0 + x1) * 0.5f; x0 = Mathf.Clamp(m - 0.01f, 0f, 0.98f); x1 = x0 + 0.02f; }
        if (y1 - y0 < 0.02f) { float m = (y0 + y1) * 0.5f; y0 = Mathf.Clamp(m - 0.01f, 0f, 0.98f); y1 = y0 + 0.02f; }
        recorteNaTela = new Vector4(1f - x1, y0, x1 - x0, y1 - y0);
        return true;
    }

    void AntesDeRenderizar(ScriptableRenderContext contexto, Camera c)
    {
        if (c != camReflexo || espelho == null) return;
        renderizando = true;
        espelho.forceRenderingOff = true;   // sem recursão: o espelho não aparece no próprio reflexo
    }

    void DepoisDeRenderizar(ScriptableRenderContext contexto, Camera c)
    {
        if (c != camReflexo || !renderizando) return;
        renderizando = false;
        if (espelho != null) espelho.forceRenderingOff = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(Ponto, Ponto + Normal * 0.2f);
    }
}
