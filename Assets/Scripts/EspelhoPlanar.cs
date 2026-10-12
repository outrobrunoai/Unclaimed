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

    static readonly int TexturaId = Shader.PropertyToID("_ReflexoTex"), AtivoId = Shader.PropertyToID("_ReflexoAtivo");

    Camera camReflexo;
    RenderTexture textura;
    MaterialPropertyBlock bloco;
    readonly Plane[] planos = new Plane[6];
    bool renderizando;

    /// <summary>Verdadeiro se o reflexo foi renderizado no último frame (para testes e depuração).</summary>
    public bool Ativo => camReflexo != null && camReflexo.enabled;

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
        Preparar(principal);
        Posicionar(principal);
        camReflexo.enabled = true;
        espelho.GetPropertyBlock(bloco, submalha);
        bloco.SetTexture(TexturaId, textura);
        bloco.SetFloat(AtivoId, 1f);
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

    void Posicionar(Camera principal)
    {
        Vector3 o = Ponto, n = Normal;
        Transform c = principal.transform;
        Vector3 p = c.position - 2f * Vector3.Dot(c.position - o, n) * n;
        Vector3 frente = Vector3.Reflect(c.forward, n), cima = Vector3.Reflect(c.up, n);
        camReflexo.transform.SetPositionAndRotation(p, Quaternion.LookRotation(frente, cima));

        camReflexo.ResetProjectionMatrix();
        camReflexo.fieldOfView = principal.fieldOfView;
        camReflexo.aspect = principal.aspect;
        camReflexo.nearClipPlane = principal.nearClipPlane;
        camReflexo.farClipPlane = principal.farClipPlane;
        Matrix4x4 projecao = camReflexo.projectionMatrix;
        // Plano de corte no espelho (um milímetro para dentro da sala), em espaço de câmera, lado visível positivo.
        Matrix4x4 w2c = camReflexo.worldToCameraMatrix;
        Vector3 cp = w2c.MultiplyPoint(o + n * 0.001f);
        Vector3 cn = w2c.MultiplyVector(n).normalized;
        camReflexo.projectionMatrix = camReflexo.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
        camReflexo.cullingMatrix = projecao * w2c;   // o frustum oblíquo deformaria o culling
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
