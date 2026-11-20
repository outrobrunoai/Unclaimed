using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// O celular 3D na mão do jornalista (modelo de Blender/Sistemas/Celular). Fica preso à câmera do jogador e é
/// desenhado por uma câmera de sobreposição (layer "Inspecao"), então não atravessa paredes.
///
/// Poses: Guardado (fora da tela, embaixo), Mao (em pé, à direita, tela para o jogador: a interface do Celular é
/// desenhada por cima da tela) e Camera (deitado, mais alto: a tela mostra o visor ao vivo). Troca de pose é uma
/// animação curta: o celular sobe do bolso girando, ou deita para fotografar.
///
/// O visor é uma segunda câmera que renderiza o mundo numa RenderTexture mostrada na tela do modelo.
/// </summary>
public class CelularNaMao : MonoBehaviour
{
    public enum Pose { Guardado, Mao, Camera, Lanterna, Foto }

    [Header("Modelo")]
    [Tooltip("Renderer da tela (objeto \"Tela\" do FBX).")]
    public Renderer tela;
    [Tooltip("Sinais do eixo U e V da UV da tela no espaço do celular (preenchidos pelo editor ao montar o prefab).")]
    public Vector2 sinalUV = Vector2.one;
    public Light brilhoDaTela;
    [Tooltip("Ícones, papel de parede e formas da interface (Assets/Prefabs/Sistemas/Celular/Icones), achados pelo nome.")]
    public Texture2D[] imagens = new Texture2D[0];

    public Texture2D Imagem(string nome)
    {
        foreach (Texture2D t in imagens) if (t != null && t.name == nome) return t;
        return null;
    }

    /// <summary>A câmera que desenha o celular (a interface da tela é clicada por ela). Null com o celular guardado.</summary>
    public Camera CameraSobreposta => sobreposta;

    /// <summary>Imagem da interface (CelularTela desenha nela). Quando existe, é ela que aparece na tela do modelo.</summary>
    public Texture TexturaDaInterface { get; set; }

    /// <summary>Imagem ao vivo do visor (a interface a coloca embaixo dos controles da câmera).</summary>
    public RenderTexture ImagemDoVisor => rtVisor;

    [Header("Poses (relativas à câmera do jogador)")]
    public Vector3 posGuardado = new Vector3(0.16f, -0.32f, 0.18f);
    public Vector3 rotGuardado = new Vector3(-70f, -25f, 30f);
    public Vector3 posMao = new Vector3(0.085f, -0.004f, 0.205f);
    public Vector3 rotMao = Vector3.zero;
    [Tooltip("Câmera e foto da galeria: o celular deitado ocupa esta fração da largura da tela.")]
    [Range(0.3f, 0.95f)] public float fracaoDeitado = 0.7f;
    [Tooltip("Deslocamento do celular deitado, em frações da tela (x: direita, y: cima).")]
    public Vector2 deslocamentoDeitado = new Vector2(0f, -0.02f);
    [Tooltip("Comprimento do modelo (m), para calcular a distância do celular deitado.")]
    public float comprimentoDoCelular = 0.152f;
    public Vector3 rotCamera = new Vector3(0f, 0f, 90f);
    [Tooltip("Segurando como lanterna: embaixo à direita, com a traseira (LED) para a frente.")]
    public Vector3 posLanterna = new Vector3(0.14f, -0.15f, 0.24f);
    public Vector3 rotLanterna = new Vector3(12f, -6f, -10f);
    [Min(0.05f)] public float duracao = 0.38f;
    [Tooltip("Campo de visão para o qual as poses foram ajustadas; com outro FOV nas opções, a distância se adapta.")]
    public float campoDeVisao = 50f;

    [Header("Visor")]
    public Vector2Int resolucaoDoVisor = new Vector2Int(320, 700);
    [Tooltip("O visor mostra o mesmo pedaço do mundo que o celular cobre na tela (parece uma janela). Desligado: usa campoDoVisor.")]
    public bool visorComoJanela = true;
    [Tooltip("Ângulo horizontal do mundo no visor quando visorComoJanela está desligado.")]
    public float campoDoVisor = 68f;
    public Color corDaTelaApagada = new Color(0.07f, 0.09f, 0.13f);

    public Pose Atual { get; private set; } = Pose.Guardado;
    public Pose Destino { get; private set; } = Pose.Guardado;
    public bool Animando => t < 1f;
    public bool Visivel => !(Atual == Pose.Guardado && !Animando);
    public Camera Visor => visor;

    Vector3 dePos, paraPos;
    Quaternion deRot, paraRot;
    float t = 1f;
    int camada = -1;
    Camera camJogador, sobreposta, visor;
    RenderTexture rtVisor;
    MaterialPropertyBlock bloco;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseMapST = Shader.PropertyToID("_BaseMap_ST"),
        BaseColor = Shader.PropertyToID("_BaseColor");

    /// <summary>LED do flash no modelo (vazio "PontoFlash" do FBX); a lanterna sai daqui.</summary>
    public Transform PontoDaLuz { get; private set; }

    // ------------------------------------------------------------------ VHS só na câmera do jogador

    /// <summary>
    /// Câmeras do celular (visor, interface): o filtro VHS fica desligado enquanto elas desenham. O filtro da câmera
    /// do jogador passa por cima de tudo depois, então o VHS continua, mas sem bordas curvas dentro da tela do celular.
    /// </summary>
    public static readonly HashSet<Camera> CamerasSemVHS = new HashSet<Camera>();
    static readonly int VHSAtivo = Shader.PropertyToID("_VHSAtivo");
    static float vhsAntes;
    static bool vhsDesligado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LigarFiltroPorCamera()
    {
        RenderPipelineManager.beginCameraRendering -= AntesDaCamera;
        RenderPipelineManager.endCameraRendering -= DepoisDaCamera;
        RenderPipelineManager.beginCameraRendering += AntesDaCamera;
        RenderPipelineManager.endCameraRendering += DepoisDaCamera;
    }

    static void AntesDaCamera(ScriptableRenderContext contexto, Camera cam)
    {
        if (!CamerasSemVHS.Contains(cam)) return;
        vhsAntes = Shader.GetGlobalFloat(VHSAtivo);
        Shader.SetGlobalFloat(VHSAtivo, 0f);
        vhsDesligado = true;
    }

    static void DepoisDaCamera(ScriptableRenderContext contexto, Camera cam)
    {
        if (!vhsDesligado || !CamerasSemVHS.Contains(cam)) return;
        Shader.SetGlobalFloat(VHSAtivo, vhsAntes);
        vhsDesligado = false;
    }

    void Awake()
    {
        foreach (Transform f in GetComponentsInChildren<Transform>(true)) if (f.name == "PontoFlash") PontoDaLuz = f;
        if (PontoDaLuz == null) PontoDaLuz = transform;
        camada = LayerMask.NameToLayer("Inspecao");
        if (camada >= 0) foreach (Transform f in GetComponentsInChildren<Transform>(true)) f.gameObject.layer = camada;
        // O brilho da tela só acende o próprio celular: a cena não muda de luz ao puxá-lo.
        if (brilhoDaTela != null && camada >= 0) brilhoDaTela.cullingMask = 1 << camada;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AplicarPose(Pose.Guardado);
        Mostrar(false);
    }

    void OnDestroy()
    {
        CamerasSemVHS.Remove(visor);
        DesligarSobreposta();
        if (rtVisor != null) { rtVisor.Release(); Destroy(rtVisor); }
    }

    /// <summary>Anima até a pose.</summary>
    public void IrPara(Pose pose)
    {
        if (pose == Destino && !Animando) return;
        if (pose != Pose.Guardado) { PrenderNaCamera(); Mostrar(true); }
        dePos = transform.localPosition;
        deRot = transform.localRotation;
        (paraPos, paraRot) = PoseDe(pose);
        Destino = pose;
        t = 0f;
        Atual = pose == Pose.Guardado ? Atual : pose;
        if (pose == Pose.Camera) LigarVisor(true);
    }

    /// <summary>Some na hora (inspeção, final de jogo).</summary>
    public void GuardarJa()
    {
        Destino = Atual = Pose.Guardado;
        t = 1f;
        AplicarPose(Pose.Guardado);
        LigarVisor(false);
        Mostrar(false);
    }

    /// <summary>Ângulo horizontal do mundo mostrado no visor.</summary>
    float CampoHorizontalDoVisor()
    {
        if (!visorComoJanela || camJogador == null) return campoDoVisor;
        float horizontal = Camera.VerticalToHorizontalFieldOfView(camJogador.fieldOfView, camJogador.aspect);
        // A tela é um pouco menor que o corpo do celular (0,1455 de 0,152).
        float fracao = fracaoDeitado * 0.957f;
        return 2f * Mathf.Atan(fracao * Mathf.Tan(horizontal * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
    }

    float FovDoJogador => camJogador != null ? camJogador.fieldOfView : campoDeVisao;

    /// <summary>
    /// As poses foram ajustadas para um campo de visão de "campoDeVisao" (50°). Com o FOV das opções do jogador,
    /// a distância muda na mesma proporção, para o celular ocupar o mesmo pedaço da tela em qualquer FOV.
    /// </summary>
    float FatorDoFov => Mathf.Tan(campoDeVisao * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(FovDoJogador * 0.5f * Mathf.Deg2Rad);

    (Vector3, Quaternion) PoseDe(Pose p) => p switch
    {
        Pose.Mao => (posMao * FatorDoFov, Quaternion.Euler(rotMao)),
        Pose.Camera or Pose.Foto => (PosicaoDeitado(), Quaternion.Euler(rotCamera)),
        Pose.Lanterna => (posLanterna * FatorDoFov, Quaternion.Euler(rotLanterna)),
        _ => (posGuardado * FatorDoFov, Quaternion.Euler(rotGuardado)),
    };

    /// <summary>Distância que faz o celular deitado ocupar "fracaoDeitado" da largura da tela, pela proporção atual.</summary>
    Vector3 PosicaoDeitado()
    {
        float aspecto = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
        float meiaAltura = Mathf.Tan(FovDoJogador * 0.5f * Mathf.Deg2Rad);          // por metro de distância
        float d = comprimentoDoCelular / (fracaoDeitado * 2f * meiaAltura * aspecto);
        return new Vector3(deslocamentoDeitado.x * 2f * meiaAltura * aspecto * d, deslocamentoDeitado.y * 2f * meiaAltura * d, d);
    }

    void AplicarPose(Pose p)
    {
        (Vector3 pos, Quaternion rot) = PoseDe(p);
        transform.SetLocalPositionAndRotation(pos, rot);
    }

    void LateUpdate()
    {
        if (!Visivel) return;
        if (camJogador == null || camJogador != Camera.main) PrenderNaCamera();
        if (Animando)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duracao);
            // Sai do bolso com um pouco de sobra no fim; guarda acelerando.
            float e = Destino == Pose.Guardado ? t * t : 1f - Mathf.Pow(1f - t, 3f) + Mathf.Sin(t * Mathf.PI) * 0.06f;
            transform.SetLocalPositionAndRotation(Vector3.LerpUnclamped(dePos, paraPos, e), Quaternion.SlerpUnclamped(deRot, paraRot, e));
            if (t >= 1f && Destino == Pose.Guardado)
            {
                Atual = Pose.Guardado;
                LigarVisor(false);
                Mostrar(false);
                return;
            }
        }
        else
        {
            // Mão viva: balanço leve na pose da câmera (na pose da mão fica parado, a interface é desenhada por cima).
            (Vector3 pos, Quaternion rot) = PoseDe(Destino);
            if (Destino == Pose.Camera || Destino == Pose.Lanterna)
            {
                float tempo = Time.time;
                pos += new Vector3(Mathf.Sin(tempo * 1.1f) * 0.0015f, Mathf.Sin(tempo * 1.7f) * 0.0012f, 0f);
                rot *= Quaternion.Euler(Mathf.Sin(tempo * 0.9f) * 0.4f, Mathf.Sin(tempo * 1.3f) * 0.5f, 0f);
            }
            transform.SetLocalPositionAndRotation(pos, rot);
        }
        AtualizarTela();
    }

    void Mostrar(bool sim)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = sim;
        if (brilhoDaTela != null) brilhoDaTela.enabled = sim;
        if (sim) LigarSobreposta(); else DesligarSobreposta();
    }

    void PrenderNaCamera()
    {
        Camera c = Camera.main;
        if (c == null || transform.parent == c.transform) { camJogador = c; return; }
        DesligarSobreposta();
        camJogador = c;
        transform.SetParent(c.transform, false);
        if (Visivel) LigarSobreposta();
    }

    // ------------------------------------------------------------------ quem desenha o celular

    // A própria câmera do jogador desenha o celular. Uma câmera empilhada fazia o filtro VHS e o pós-processamento
    // rodarem duas vezes (a cena escurecia ao puxar o celular). A cápsula do jogador mantém as paredes longe o
    // bastante para o celular (a uns 15 a 20 cm) não atravessá-las.
    void LigarSobreposta()
    {
        if (camJogador == null) PrenderNaCamera();
        sobreposta = camJogador;
    }

    void DesligarSobreposta() => sobreposta = null;

    // ------------------------------------------------------------------ visor e tela

    void LigarVisor(bool ligar)
    {
        if (!ligar)
        {
            if (visor != null) visor.enabled = false;
            return;
        }
        if (camJogador == null) PrenderNaCamera();
        if (camJogador == null) return;
        if (rtVisor == null)
        {
            rtVisor = new RenderTexture(resolucaoDoVisor.x, resolucaoDoVisor.y, 24) { name = "Visor do celular" };
            rtVisor.Create();
        }
        if (visor == null)
        {
            var go = new GameObject("Visor do celular");
            go.transform.SetParent(camJogador.transform, false);
            // Rolado como o celular deitado: a imagem aparece em pé na tela.
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotCamera.z);
            visor = go.AddComponent<Camera>();
            CamerasSemVHS.Add(visor);
            visor.nearClipPlane = 0.05f;
            visor.farClipPlane = camJogador.farClipPlane;
            visor.targetTexture = rtVisor;
            var dados = visor.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = false;
        }
        visor.depth = camJogador.depth - 2f;   // antes da interface e da câmera do jogador
        visor.cullingMask = camJogador.cullingMask & ~(camada >= 0 ? 1 << camada : 0);
        // O lado comprido da tela (vertical da RT) cobre o ângulo horizontal pedido.
        visor.fieldOfView = CampoHorizontalDoVisor();
        visor.enabled = true;
    }

    void AtualizarTela()
    {
        if (tela == null) return;
        if (bloco == null) bloco = new MaterialPropertyBlock();
        tela.GetPropertyBlock(bloco);
        bool mostrarVisor = Destino == Pose.Camera && visor != null && visor.enabled;
        if (TexturaDaInterface != null)
        {
            if (mostrarVisor) visor.fieldOfView = CampoHorizontalDoVisor();
            bloco.SetTexture(BaseMap, TexturaDaInterface);
            bloco.SetColor(BaseColor, Color.white);
        }
        else if (mostrarVisor)
        {
            visor.fieldOfView = CampoHorizontalDoVisor();   // acompanha o FOV das opções
            bloco.SetTexture(BaseMap, rtVisor);
            bloco.SetColor(BaseColor, Color.white);
        }
        else
        {
            bloco.SetTexture(BaseMap, Texture2D.whiteTexture);
            bloco.SetColor(BaseColor, corDaTelaApagada);
        }
        // UV espelhada no FBX: corrige pela escala/deslocamento.
        bloco.SetVector(BaseMapST, new Vector4(sinalUV.x, sinalUV.y, sinalUV.x < 0 ? 1f : 0f, sinalUV.y < 0 ? 1f : 0f));
        tela.SetPropertyBlock(bloco);
    }

    /// <summary>Retângulo da tela do celular em coordenadas de GUI (origem no alto à esquerda). Vazio se guardado.</summary>
    public Rect RetanguloNaTela()
    {
        if (tela == null || sobreposta == null || !Visivel) return Rect.zero;
        Bounds b = tela.GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 canto = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = sobreposta.WorldToScreenPoint(tela.transform.TransformPoint(canto));
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
    }

    /// <summary>Foto na horizontal, sem a interface: renderiza o visor desenrolado numa textura nova.</summary>
    public Texture2D Capturar(int largura = 640, int altura = 360)
    {
        if (visor == null) LigarVisor(true);
        if (visor == null) return null;
        Quaternion antes = visor.transform.localRotation;
        RenderTexture alvoAntes = visor.targetTexture;
        float fovAntes = visor.fieldOfView;
        var rt = RenderTexture.GetTemporary(largura, altura, 24);
        visor.transform.localRotation = Quaternion.identity;
        visor.targetTexture = rt;
        // Mesmo enquadramento do visor: o ângulo horizontal vira vertical pela proporção.
        visor.fieldOfView = Camera.HorizontalToVerticalFieldOfView(CampoHorizontalDoVisor(), largura / (float)altura);
        visor.Render();
        RenderTexture ativa = RenderTexture.active;
        RenderTexture.active = rt;
        var foto = new Texture2D(largura, altura, TextureFormat.RGB24, false);
        foto.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
        foto.Apply();
        RenderTexture.active = ativa;
        RenderTexture.ReleaseTemporary(rt);
        visor.transform.localRotation = antes;
        visor.targetTexture = alvoAntes;
        visor.fieldOfView = fovAntes;
        return foto;
    }
}
