using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A interface do celular. Uma câmera própria desenha o Canvas numa textura, e a textura vai na tela 3D do modelo
/// (como o visor): fica recortada nos cantos arredondados da tela, aparece já durante a animação de puxar e gira
/// junto quando o celular deita (câmera e foto aberta). O mouse é levado da tela do jogo para a tela do celular
/// (raio até o plano da tela) e os cliques e a rolagem vão para os botões e listas da interface.
///
/// Telas em pé: início (relógio e apps), Mensagens, conversa, Redação (envio de provas), Fotos, e o painel rápido
/// (toque na barra de cima) com a lanterna. Deitado: o visor da câmera e a foto aberta.
/// Criado pelo Celular ao começar; as imagens vêm de CelularNaMao.imagens (Blender/Sistemas/Celular/icones.py).
/// </summary>
public class CelularTela : MonoBehaviour
{
    const float L = 360f;   // largura lógica da tela em pé (unidades do Canvas)
    float A;                // altura lógica, pela proporção da tela do modelo
    const float BarraStatus = 26f, BarraHome = 22f;

    enum Tela { Inicio, Mensagens, Conversa, Redacao, Galeria }
    Tela tela = Tela.Inicio;
    string contato;
    int fotoAberta = -1;
    bool emFoto, painelRapidoAberto;
    string chaveConstruida;

    Celular dono;
    CelularNaMao na;
    int camada;
    Canvas canvas;
    GraphicRaycaster raycaster;
    Image fundoMascara;
    Camera camUi;
    RenderTexture rtInterface;
    RawImage visorImg;
    Vector3 telaMin;
    float telaLargura, telaAltura;
    readonly List<RaycastResult> acertos = new List<RaycastResult>();
    RectTransform retrato, paisagem, conteudo, camHud, fotoView, painelRapido;
    RawImage papel;
    TextMeshProUGUI horaStatus;
    Image[] barras = new Image[4];
    Image clarao;
    Sprite arredondado;

    static readonly Color Escuro = new Color(0.05f, 0.055f, 0.065f), Cartao = new Color(0.12f, 0.125f, 0.14f),
        TextoFraco = new Color(1f, 1f, 1f, 0.55f), Azul = new Color(0.16f, 0.48f, 0.98f), Verde = new Color(0.2f, 0.62f, 0.33f);
    static readonly Color[] CoresDeContato = { new Color(0.85f, 0.45f, 0.2f), new Color(0.25f, 0.5f, 0.85f), new Color(0.6f, 0.35f, 0.75f), new Color(0.2f, 0.6f, 0.5f) };

    // ================================================================== montagem

    public void Construir(Celular dono, CelularNaMao na)
    {
        this.dono = dono;
        this.na = na;
        camada = LayerMask.NameToLayer("Inspecao");
        if (camada < 0) camada = 0;

        // Retângulo da tela do modelo, no espaço do celular (a tela olha para -Z).
        Mesh malha = na.tela.GetComponent<MeshFilter>().sharedMesh;
        Bounds b = malha.bounds;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = na.transform.InverseTransformPoint(na.tela.transform.TransformPoint(c));
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        float largura = max.x - min.x, altura = max.y - min.y;
        A = L * altura / largura;

        telaMin = min;
        telaLargura = largura;
        telaAltura = altura;

        // Câmera da interface: longe de tudo, só a camada UI, desenhando numa textura do tamanho da tela.
        int camadaUi = LayerMask.NameToLayer("UI");
        camada = camadaUi >= 0 ? camadaUi : 5;
        rtInterface = new RenderTexture(720, Mathf.RoundToInt(720f * A / L), 24) { name = "Interface do celular" };
        rtInterface.Create();
        var camGo = new GameObject("Camera da interface do celular");
        camGo.transform.position = new Vector3(0f, -5000f, 0f);
        camUi = camGo.AddComponent<Camera>();
        camUi.orthographic = true;
        camUi.cullingMask = 1 << camada;
        camUi.clearFlags = CameraClearFlags.SolidColor;
        camUi.backgroundColor = Color.black;
        camUi.nearClipPlane = 0.1f;
        camUi.farClipPlane = 10f;
        camUi.targetTexture = rtInterface;
        camUi.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        CelularNaMao.CamerasSemVHS.Add(camUi);

        var go = new GameObject("Interface do celular", typeof(RectTransform));
        go.layer = camada;
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camUi;
        canvas.planeDistance = 1f;
        var escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(L, A);
        escala.matchWidthOrHeight = 0f;
        raycaster = go.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;   // fora do EventSystem: os toques chegam por Tocar(), já convertidos
        na.TexturaDaInterface = rtInterface;

        arredondado = Arredondado();
        fundoMascara = Caixa(go.transform, Color.black);
        Esticar(fundoMascara.rectTransform);
        Transform raiz = fundoMascara.transform;
        visorImg = Novo("Visor ao vivo", raiz).gameObject.AddComponent<RawImage>();
        visorImg.raycastTarget = false;
        Esticar(visorImg.rectTransform);

        retrato = Novo("Em pe", raiz);
        Esticar(retrato);
        papel = Figura(retrato, "papel_de_parede", Color.white);
        Esticar(papel.rectTransform);
        conteudo = Novo("Conteudo", retrato);
        Esticar(conteudo, 0, BarraHome, 0, BarraStatus);
        MontarBarraDeStatus();
        MontarBarraHome();
        MontarPainelRapido();

        paisagem = Novo("Deitado", raiz);
        paisagem.anchorMin = paisagem.anchorMax = new Vector2(0.5f, 0.5f);
        paisagem.sizeDelta = new Vector2(A, L);
        paisagem.localRotation = Quaternion.Euler(0f, 0f, -90f);   // o celular deita girando +90: a interface fica em pé
        MontarHudDaCamera();
        fotoView = Novo("Foto aberta", paisagem);
        Esticar(fotoView);

        chaveConstruida = null;
    }

    Sprite Arredondado()
    {
        Texture2D t = na.Imagem("arredondado");
        if (t == null) return null;
        float borda = t.width * 0.375f;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(borda, borda, borda, borda));
    }

    RectTransform Novo(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = camada;
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        return rt;
    }

    static void Esticar(RectTransform rt, float esq = 0, float baixo = 0, float dir = 0, float cima = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(esq, baixo);
        rt.offsetMax = new Vector2(-dir, -cima);
    }

    /// <summary>Posiciona pelo canto de cima à esquerda do pai (x para a direita, y para baixo).</summary>
    static RectTransform Em(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    Image Caixa(Transform pai, Color cor, float raio = 0f)
    {
        var img = Novo("Caixa", pai).gameObject.AddComponent<Image>();
        img.color = cor;
        if (raio > 0f && arredondado != null)
        {
            img.sprite = arredondado;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = arredondado.border.x / raio;
        }
        return img;
    }

    RawImage Figura(Transform pai, string nome, Color cor)
    {
        var img = Novo(nome, pai).gameObject.AddComponent<RawImage>();
        img.texture = na.Imagem(nome);
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Texto(Transform pai, string t, float tamanho, Color cor, TextAlignmentOptions alinhamento = TextAlignmentOptions.Left,
                          FontStyles estilo = FontStyles.Normal)
    {
        var tmp = Novo("Texto", pai).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = t;
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.fontStyle = estilo;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    void Clicavel(Graphic alvo, UnityAction acao)
    {
        alvo.raycastTarget = true;
        var b = alvo.gameObject.AddComponent<Button>();
        b.targetGraphic = alvo;
        var cores = b.colors;
        cores.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        cores.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cores;
        b.onClick.AddListener(acao);
    }

    Image Toque(RectTransform rt, UnityAction acao)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        Clicavel(img, acao);
        return img;
    }

    // ================================================================== partes fixas

    void MontarBarraDeStatus()
    {
        RectTransform barra = Em(Novo("Barra de status", retrato), 0, 0, L, BarraStatus);
        horaStatus = Texto(barra, "23:12", 12.5f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        Em(horaStatus.rectTransform, 22, 5, 80, 18);
        for (int i = 0; i < 4; i++)
        {
            barras[i] = Caixa(barra, Color.white);
            float h = 4 + i * 2.6f;
            Em(barras[i].rectTransform, L - 86 + i * 5, 17 - h, 3.2f, h);
        }
        // Bateria: contorno, miolo e a carga (34%).
        Image contorno = Caixa(barra, new Color(1, 1, 1, 0.7f), 3f);
        Em(contorno.rectTransform, L - 56, 7, 24, 12);
        Image miolo = Caixa(contorno.transform, Color.black, 2f);
        Esticar(miolo.rectTransform, 1.2f, 1.2f, 1.2f, 1.2f);
        Image carga = Caixa(miolo.transform, Color.white, 1.5f);
        Em(carga.rectTransform, 1, 1, 21.6f * 0.34f - 2f, 7.6f);
        Image ponta = Caixa(barra, new Color(1, 1, 1, 0.6f));
        Em(ponta.rectTransform, L - 31.5f, 10.5f, 1.6f, 5);
        // Tocar na barra de cima abre o painel rápido, como num celular de verdade.
        Toque(barra, () => painelRapidoAberto = !painelRapidoAberto);
    }

    void MontarBarraHome()
    {
        RectTransform faixa = Novo("Barra home", retrato);
        faixa.anchorMin = new Vector2(0, 0);
        faixa.anchorMax = new Vector2(1, 0);
        faixa.pivot = new Vector2(0.5f, 0);
        faixa.sizeDelta = new Vector2(0, BarraHome);
        Image pilula = Caixa(faixa, new Color(1, 1, 1, 0.75f), 2f);
        pilula.rectTransform.anchorMin = pilula.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        pilula.rectTransform.sizeDelta = new Vector2(120, 4.5f);
        Toque(faixa, IrParaInicio);
    }

    void MontarPainelRapido()
    {
        painelRapido = Novo("Painel rapido", retrato);
        Esticar(painelRapido);
        Image fundo = painelRapido.gameObject.AddComponent<Image>();
        fundo.color = new Color(0.03f, 0.035f, 0.045f, 0.97f);
        Clicavel(fundo, () => painelRapidoAberto = false);
        painelRapido.gameObject.SetActive(false);
    }

    void ConstruirPainelRapido()
    {
        for (int i = painelRapido.childCount - 1; i >= 0; i--) Destroy(painelRapido.GetChild(i).gameObject);
        Em(Texto(painelRapido, Relogio(), 34, Color.white, TextAlignmentOptions.Left, FontStyles.Normal).rectTransform, 24, 40, 200, 44);
        Em(Texto(painelRapido, "qua., 18 de nov.", 12, TextoFraco).rectTransform, 26, 84, 200, 18);
        string[] nomes = { "Lanterna", "Wi-Fi", "Dados", "Avião" };
        float lado = 66f, esp = (L - 4 * lado) / 5f;
        for (int i = 0; i < nomes.Length; i++)
        {
            bool ligado = i == 0 ? dono.Lanterna : i == 2;
            Image tile = Caixa(painelRapido, ligado ? new Color(0.95f, 0.95f, 0.97f) : new Color(1, 1, 1, 0.14f), 18f);
            Em(tile.rectTransform, esp + i * (lado + esp), 120, lado, lado);
            if (i == 0)
            {
                RawImage g = Figura(tile.transform, "glifo_lanterna", ligado ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(30, 30);
                Clicavel(tile, () => dono.AlternarLanterna());
            }
            else
            {
                var t = Texto(tile.transform, i == 1 ? "Wi-Fi" : i == 2 ? "4G" : "Off", 13, ligado ? new Color(0.1f, 0.1f, 0.12f) : Color.white,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                Esticar(t.rectTransform);
            }
            Em(Texto(painelRapido, nomes[i], 10.5f, TextoFraco, TextAlignmentOptions.Center).rectTransform, esp + i * (lado + esp) - 10, 190, lado + 20, 16);
        }
    }

    void MontarHudDaCamera()
    {
        camHud = Novo("Visor", paisagem);
        Esticar(camHud);
        // Grade de enquadramento.
        for (int i = 1; i <= 2; i++)
        {
            Image v = Caixa(camHud, new Color(1, 1, 1, 0.22f));
            v.raycastTarget = false;
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = new Vector2(i / 3f, 0.5f);
            v.rectTransform.sizeDelta = new Vector2(0.8f, L);
            Image h = Caixa(camHud, new Color(1, 1, 1, 0.22f));
            h.raycastTarget = false;
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0.5f, i / 3f);
            h.rectTransform.sizeDelta = new Vector2(A, 0.8f);
        }
        // Obturador à direita (decorativo: a foto sai pelo clique ou F).
        RectTransform ob = Novo("Obturador", camHud);
        ob.anchorMin = ob.anchorMax = new Vector2(1f, 0.5f);
        ob.anchoredPosition = new Vector2(-48f, 0f);
        ob.sizeDelta = new Vector2(62, 62);
        RawImage anel = Figura(ob, "anel", Color.white);
        Esticar(anel.rectTransform);
        RawImage miolo = Figura(ob, "circulo", Color.white);
        Esticar(miolo.rectTransform, 7, 7, 7, 7);
        var modo = Texto(camHud, "FOTO", 11, new Color(1f, 0.82f, 0.2f), TextAlignmentOptions.Center, FontStyles.Bold);
        modo.rectTransform.anchorMin = modo.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        modo.rectTransform.anchoredPosition = new Vector2(-48f, -50f);
        modo.rectTransform.sizeDelta = new Vector2(80, 16);
        clarao = Caixa(camHud, new Color(1, 1, 1, 0));
        clarao.raycastTarget = false;
        Esticar(clarao.rectTransform);
    }

    // ================================================================== navegação

    public void IrParaInicio()
    {
        tela = Tela.Inicio;
        emFoto = false;
        painelRapidoAberto = false;
        chaveConstruida = null;
    }

    public void AbrirConversa(string contatoNovo)
    {
        emFoto = false;
        painelRapidoAberto = false;
        Ir(Tela.Conversa, contatoNovo);
    }

    void Ir(Tela t, string contatoNovo = null)
    {
        tela = t;
        contato = contatoNovo;
        chaveConstruida = null;
    }

    void OnEnable() => CelularLogica.AoReceber += AoReceberMensagem;

    void OnDisable() => CelularLogica.AoReceber -= AoReceberMensagem;

    void AoReceberMensagem(DadosDoCelular.Mensagem m)
    {
        chaveConstruida = null;
        if (dono != null && tela == Tela.Conversa && m != null && m.contato == contato)
            dono.lidas.Add(m.id);
    }

    public void AbrirFoto(int indice)
    {
        tela = Tela.Galeria;
        fotoAberta = indice;
        emFoto = true;
        ConstruirFoto();
    }

    void FecharFoto()
    {
        emFoto = false;
        chaveConstruida = null;
        dono.FecharFoto();
    }

    void OnDestroy()
    {
        if (camUi != null) { CelularNaMao.CamerasSemVHS.Remove(camUi); Destroy(camUi.gameObject); }
        if (canvas != null) Destroy(canvas.gameObject);
        if (rtInterface != null) { rtInterface.Release(); Destroy(rtInterface); }
    }

    // ================================================================== toque (mouse -> tela do celular)

    void Update()
    {
        if (dono == null || !dono.EstaAberta || IntroChegada.Ativa || na.Animando || na.CameraSobreposta == null) return;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        bool clique = mouse.leftButton.wasPressedThisFrame;
        float rolagem = mouse.scroll.ReadValue().y;
        if (!clique && Mathf.Abs(rolagem) < 0.01f) return;
        if (!PontoNaTela(mouse.position.ReadValue(), out Vector2 ponto)) return;
        Tocar(ponto, clique, rolagem);
#endif
    }

    /// <summary>Converte a posição do mouse na tela do jogo para pixels da interface (raio até o plano da tela do celular).</summary>
    public bool PontoNaTela(Vector2 mouse, out Vector2 pixel)
    {
        pixel = Vector2.zero;
        Camera cam = na.CameraSobreposta;
        if (cam == null) return false;
        Ray raio = cam.ScreenPointToRay(mouse);
        var plano = new Plane(na.transform.TransformDirection(Vector3.back), na.transform.TransformPoint(new Vector3(0f, 0f, telaMin.z)));
        if (!plano.Raycast(raio, out float d)) return false;
        Vector3 local = na.transform.InverseTransformPoint(raio.GetPoint(d));
        float u = (local.x - telaMin.x) / telaLargura, v = (local.y - telaMin.y) / telaAltura;
        if (na.sinalUV.x < 0f) u = 1f - u;
        if (na.sinalUV.y < 0f) v = 1f - v;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
        pixel = new Vector2(u * rtInterface.width, v * rtInterface.height);
        return true;
    }

    /// <summary>Um toque (clique) ou rolagem num ponto da interface, em pixels dela.</summary>
    public void Tocar(Vector2 pixel, bool clique, float rolagem = 0f)
    {
        var dados = new PointerEventData(EventSystem.current) { position = pixel, button = PointerEventData.InputButton.Left };
        acertos.Clear();
        raycaster.Raycast(dados, acertos);
        if (acertos.Count == 0) return;
        acertos.Sort((x, y) => y.depth.CompareTo(x.depth));
        GameObject alvo = acertos[0].gameObject;
        dados.pointerCurrentRaycast = acertos[0];
        if (clique)
        {
            GameObject quem = ExecuteEvents.GetEventHandler<IPointerClickHandler>(alvo);
            if (quem != null) ExecuteEvents.Execute(quem, dados, ExecuteEvents.pointerClickHandler);
        }
        if (Mathf.Abs(rolagem) > 0.01f)
        {
            dados.scrollDelta = new Vector2(0f, Mathf.Sign(rolagem));
            GameObject quem = ExecuteEvents.GetEventHandler<IScrollHandler>(alvo);
            if (quem != null) ExecuteEvents.Execute(quem, dados, ExecuteEvents.scrollHandler);
        }
    }

    // ================================================================== atualização

    void LateUpdate()
    {
        if (canvas == null || dono == null) return;
        camUi.enabled = na.Visivel;
        if (Camera.main != null) camUi.depth = Camera.main.depth - 1f;   // desenha antes da câmera do jogador
        if (!dono.EstaAberta && !dono.ModoCamera) emFoto = false;

        bool deitado = na.Destino == CelularNaMao.Pose.Camera || na.Destino == CelularNaMao.Pose.Foto;
        retrato.gameObject.SetActive(!deitado);
        paisagem.gameObject.SetActive(deitado);
        camHud.gameObject.SetActive(dono.ModoCamera);
        fotoView.gameObject.SetActive(!dono.ModoCamera && emFoto);
        // No modo câmera, a imagem ao vivo do visor fica embaixo dos controles.
        visorImg.gameObject.SetActive(dono.ModoCamera);
        if (dono.ModoCamera) visorImg.texture = na.ImagemDoVisor;
        if (!deitado) painelRapido.gameObject.SetActive(painelRapidoAberto);

        horaStatus.text = Relogio();
        int sinal = Sinal.Barras;
        for (int i = 0; i < barras.Length; i++) barras[i].color = new Color(1, 1, 1, i < sinal ? 1f : 0.28f);

        if (dono.ModoCamera) AtualizarVisor();
        else if (!deitado)
        {
            string chave = Chave();
            if (chave != chaveConstruida)
            {
                chaveConstruida = chave;
                Construir();
                if (painelRapidoAberto) ConstruirPainelRapido();
            }
        }
    }

    void AtualizarVisor()
    {
        float desde = Time.unscaledTime - dono.DisparoEm;
        clarao.color = new Color(1, 1, 1, desde < 0.25f ? 0.9f * (1f - desde / 0.25f) : 0f);
    }

    string Chave()
    {
        var sb = new StringBuilder();
        sb.Append(tela).Append('|').Append(contato).Append('|').Append(painelRapidoAberto).Append(dono.Lanterna).Append('|')
          .Append(EstadoDaNoite.Fatos.Count).Append('|').Append(AlbumDeFotos.Fotos.Count).Append('|').Append(Sinal.Tem).Append('|')
          .Append(Relogio()).Append('|').Append(dono.lidas.Count);
        foreach (var item in CelularLogica.Fila) sb.Append(item.id).Append(Mathf.RoundToInt(item.Progresso * 20));
        return sb.ToString();
    }

    static string Relogio()
    {
        int minutos = 23 * 60 + 12 + (int)(Time.timeSinceLevelLoad / 60f);
        return $"{(minutos / 60) % 24:00}:{minutos % 60:00}";
    }

    void Limpar(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    void Construir()
    {
        Limpar(conteudo);
        papel.gameObject.SetActive(tela == Tela.Inicio);
        if (tela != Tela.Inicio) Esticar(Caixa(conteudo, Escuro).rectTransform, 0, -BarraHome, 0, -BarraStatus);
        switch (tela)
        {
            case Tela.Inicio: ConstruirInicio(); break;
            case Tela.Mensagens: ConstruirMensagens(); break;
            case Tela.Conversa: ConstruirConversa(); break;
            case Tela.Redacao: ConstruirRedacao(); break;
            case Tela.Galeria: ConstruirGaleria(); break;
        }
    }

    float AlturaDoConteudo => A - BarraStatus - BarraHome;

    // ================================================================== telas

    void ConstruirInicio()
    {
        // Tracinho discreto embaixo da barra de status: dá para puxar o painel rápido.
        Image puxador = Caixa(conteudo, new Color(1, 1, 1, 0.22f), 1.5f);
        Em(puxador.rectTransform, L / 2f - 16, 2, 32, 3);

        Em(Texto(conteudo, Relogio(), 62, Color.white, TextAlignmentOptions.Center).rectTransform, 0, 40, L, 76);
        Em(Texto(conteudo, "quarta-feira, 18 de novembro", 13, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.Center).rectTransform, 0, 116, L, 20);

        var apps = new (string nome, string icone, UnityAction acao)[]
        {
            ("Mensagens", "app_mensagens", () => Ir(Tela.Mensagens)),
            ("Câmera", "app_camera", () => dono.AbrirCamera()),
            ("Fotos", "app_galeria", () => Ir(Tela.Galeria)),
            ("Redação", "app_provas", () => Ir(Tela.Redacao)),
        };
        float lado = 58f, esp = (L - 4 * lado) / 5f, y = AlturaDoConteudo - 120f;
        Image doca = Caixa(conteudo, new Color(1, 1, 1, 0.12f), 26f);
        Em(doca.rectTransform, 10, y - 12, L - 20, lado + 40);
        int naoLidas = NaoLidasTotal();
        for (int i = 0; i < apps.Length; i++)
        {
            float x = esp + i * (lado + esp);
            RawImage icone = Figura(conteudo, apps[i].icone, Color.white);
            Em(icone.rectTransform, x, y, lado, lado);
            Clicavel(icone, apps[i].acao);
            Em(Texto(conteudo, apps[i].nome, 10.5f, Color.white, TextAlignmentOptions.Center).rectTransform, x - 12, y + lado + 4, lado + 24, 14);
            if (i == 0 && naoLidas > 0)
            {
                RawImage badge = Figura(conteudo, "circulo", new Color(0.93f, 0.22f, 0.2f));
                Em(badge.rectTransform, x + lado - 14, y - 6, 20, 20);
                Esticar(Texto(badge.transform, naoLidas.ToString(), 11, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
            }
        }
    }

    RectTransform Cabecalho(string titulo, UnityAction voltar)
    {
        RectTransform cab = Em(Novo("Cabecalho", conteudo), 0, 0, L, 46);
        var seta = Texto(cab, "‹", 30, Azul, TextAlignmentOptions.Center);
        Em(seta.rectTransform, 4, 2, 34, 40);
        Clicavel(seta, voltar);
        Em(Texto(cab, titulo, 18, Color.white, TextAlignmentOptions.Left, FontStyles.Bold).rectTransform, 40, 11, L - 60, 26);
        Image linha = Caixa(cab, new Color(1, 1, 1, 0.08f));
        Em(linha.rectTransform, 0, 45, L, 0.8f);
        return cab;
    }

    /// <summary>Lista rolável abaixo do cabeçalho; devolve o conteúdo (posicionar filhos com Em, y para baixo).</summary>
    RectTransform Lista(float topo, float rodape, float alturaTotal, bool comecarNoFim = false)
    {
        RectTransform vista = Em(Novo("Lista", conteudo), 0, topo, L, AlturaDoConteudo - topo - rodape);
        vista.gameObject.AddComponent<RectMask2D>();
        Image alvo = vista.gameObject.AddComponent<Image>();
        alvo.color = new Color(0, 0, 0, 0);
        RectTransform dentro = Novo("Dentro", vista);
        dentro.anchorMin = new Vector2(0, 1);
        dentro.anchorMax = new Vector2(1, 1);
        dentro.pivot = new Vector2(0.5f, 1);
        dentro.sizeDelta = new Vector2(0, alturaTotal);
        var rolagem = vista.gameObject.AddComponent<ScrollRect>();
        rolagem.content = dentro;
        rolagem.viewport = vista;
        rolagem.horizontal = false;
        rolagem.movementType = ScrollRect.MovementType.Clamped;
        rolagem.scrollSensitivity = 26f;
        float sobra = alturaTotal - vista.sizeDelta.y;
        if (comecarNoFim && sobra > 0f) dentro.anchoredPosition = new Vector2(0, sobra);
        return dentro;
    }

    int NaoLidas(string c)
    {
        int n = 0;
        foreach (DadosDoCelular.Mensagem m in CelularLogica.Conversa(c)) if (m.de != "eu" && !dono.lidas.Contains(m.id)) n++;
        return n;
    }

    int NaoLidasTotal()
    {
        int n = 0;
        foreach (DadosDoCelular.Contato c in CelularLogica.Dados.contatos) n += NaoLidas(c.id);
        return n;
    }

    Color CorDoContato(string id)
    {
        int i = 0;
        foreach (DadosDoCelular.Contato c in CelularLogica.Dados.contatos) { if (c.id == id) break; i++; }
        return CoresDeContato[i % CoresDeContato.Length];
    }

    void Avatar(Transform pai, string id, string nome, float x, float y, float lado)
    {
        RawImage a = Figura(pai, "circulo", CorDoContato(id));
        Em(a.rectTransform, x, y, lado, lado);
        Esticar(Texto(a.transform, string.IsNullOrEmpty(nome) ? "?" : nome.Substring(0, 1), lado * 0.45f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
    }

    void ConstruirMensagens()
    {
        Cabecalho("Mensagens", IrParaInicio);
        var contatos = new List<(DadosDoCelular.Contato c, DadosDoCelular.Mensagem ultima)>();
        foreach (DadosDoCelular.Contato c in CelularLogica.Dados.contatos)
        {
            DadosDoCelular.Mensagem ultima = null;
            foreach (var m in CelularLogica.Conversa(c.id)) ultima = m;
            if (ultima != null) contatos.Add((c, ultima));
        }
        const float linha = 66f;
        RectTransform lista = Lista(48, 0, contatos.Count * linha);
        for (int i = 0; i < contatos.Count; i++)
        {
            var (c, ultima) = contatos[i];
            RectTransform item = Em(Novo("Contato", lista), 0, i * linha, L, linha);
            string id = c.id;
            Toque(item, () => Ir(Tela.Conversa, id));
            Avatar(item, c.id, c.nome, 14, 12, 42);
            int n = NaoLidas(c.id);
            Em(Texto(item, c.nome, 14, Color.white, TextAlignmentOptions.Left, n > 0 ? FontStyles.Bold : FontStyles.Normal).rectTransform, 66, 12, L - 100, 20);
            var previa = Texto(item, (ultima.de == "eu" ? "Você: " : "") + ultima.texto, 12, TextoFraco);
            previa.textWrappingMode = TextWrappingModes.NoWrap;
            previa.overflowMode = TextOverflowModes.Ellipsis;
            Em(previa.rectTransform, 66, 34, L - 100, 18);
            if (n > 0) Em(Figura(item, "circulo", Azul).rectTransform, L - 26, 28, 10, 10);
            Em(Caixa(item, new Color(1, 1, 1, 0.07f)).rectTransform, 66, linha - 1, L - 66, 0.8f);
        }
    }

    void ConstruirConversa()
    {
        DadosDoCelular.Contato c = CelularLogica.Dados.BuscarContato(contato);
        RectTransform cab = Cabecalho(c != null ? c.nome : contato, () => Ir(Tela.Mensagens));

        var respostas = new List<DadosDoCelular.Resposta>(CelularLogica.RespostasDisponiveis(contato));
        float rodape = respostas.Count > 0 ? respostas.Count * 42f + 10f : 0f;
        var mensagens = new List<DadosDoCelular.Mensagem>(CelularLogica.Conversa(contato));

        // Mede as bolhas antes de montar a lista.
        float maxW = L * 0.72f, y = 10f;
        var medidas = new List<(float w, float h)>();
        var medidor = Texto(conteudo, "", 13.5f, Color.white);
        foreach (var m in mensagens)
        {
            Vector2 p = medidor.GetPreferredValues(m.texto, maxW - 24f, 0f);
            float w = Mathf.Min(p.x, maxW - 24f);
            Vector2 q = medidor.GetPreferredValues(m.texto, w + 0.5f, 0f);
            medidas.Add((w, q.y));
            y += q.y + 18f + 8f;
        }
        Destroy(medidor.gameObject);

        RectTransform lista = Lista(48, rodape, y, true);
        y = 10f;
        for (int i = 0; i < mensagens.Count; i++)
        {
            var m = mensagens[i];
            dono.lidas.Add(m.id);
            bool minha = m.de == "eu";
            var (w, h) = medidas[i];
            Image bolha = Caixa(lista, minha ? Verde : new Color(0.17f, 0.175f, 0.19f), 14f);
            float bw = w + 24f, bh = h + 18f;
            Em(bolha.rectTransform, minha ? L - bw - 12f : 12f, y, bw, bh);
            Esticar(Texto(bolha.transform, m.texto, 13.5f, Color.white).rectTransform, 12, 9, 12, 9);
            y += bh + 8f;
        }

        float yb = AlturaDoConteudo - rodape + 4f;
        foreach (var r in respostas)
        {
            Image b = Caixa(conteudo, Sinal.Tem ? Azul : new Color(1, 1, 1, 0.12f), 17f);
            Em(b.rectTransform, 16, yb, L - 32, 34);
            Esticar(Texto(b.transform, r.rotulo, 13, Sinal.Tem ? Color.white : TextoFraco, TextAlignmentOptions.Center).rectTransform);
            var resp = r;
            if (Sinal.Tem) Clicavel(b, () => { CelularLogica.Responder(resp, Sinal.Tem); CelularLogica.ReceberPendentes(Sinal.Tem); });
            yb += 42f;
        }
    }

    void ConstruirRedacao()
    {
        Cabecalho("Redação", IrParaInicio);
        var coletadas = new List<Evidencia>(Caderno.Coletadas());
        const float linha = 58f;
        float rodape = 34f;
        RectTransform lista = Lista(48, rodape, Mathf.Max(1, coletadas.Count) * linha + 8f);
        if (coletadas.Count == 0)
            Em(Texto(lista, "Nenhum arquivo para enviar.", 13, TextoFraco, TextAlignmentOptions.Center).rectTransform, 0, 30, L, 20);
        for (int i = 0; i < coletadas.Count; i++)
        {
            Evidencia e = coletadas[i];
            Image cartao = Caixa(lista, Cartao, 12f);
            Em(cartao.rectTransform, 12, 6 + i * linha, L - 24, linha - 8);
            var titulo = Texto(cartao.transform, e.titulo, 13, Color.white);
            titulo.textWrappingMode = TextWrappingModes.NoWrap;
            titulo.overflowMode = TextOverflowModes.Ellipsis;
            Em(titulo.rectTransform, 12, 7, L - 140, 18);
            Em(Texto(cartao.transform, e.tipo == "foto" ? "Foto" : e.tipo == "audio" ? "Áudio" : "Documento", 11, TextoFraco).rectTransform, 12, 27, 120, 16);

            var item = ItemDaFila(e.id);
            if (CelularLogica.Enviada(e.id))
                Em(Texto(cartao.transform, "Enviado", 12, new Color(0.45f, 0.85f, 0.55f), TextAlignmentOptions.Right).rectTransform, L - 140, 15, 100, 18);
            else if (item != null)
            {
                Image trilho = Caixa(cartao.transform, new Color(1, 1, 1, 0.12f), 2f);
                Em(trilho.rectTransform, L - 124, 22, 84, 4);
                Image cheio = Caixa(trilho.transform, Azul, 2f);
                Em(cheio.rectTransform, 0, 0, 84 * item.Progresso, 4);
            }
            else if (CelularLogica.PodeEnviar(e.id, out _))
            {
                Image b = Caixa(cartao.transform, Azul, 13f);
                Em(b.rectTransform, L - 112, 12, 76, 26);
                Esticar(Texto(b.transform, "Enviar", 12, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
                string id = e.id;
                Clicavel(b, () => CelularLogica.Enfileirar(id));
            }
            else
                Em(Texto(cartao.transform, "Indisponível", 11, TextoFraco, TextAlignmentOptions.Right).rectTransform, L - 140, 16, 100, 18);
        }
        if (CelularLogica.Fila.Count > 0)
            Em(Texto(conteudo, Sinal.Tem ? "Enviando…" : "Aguardando conexão…", 12, TextoFraco, TextAlignmentOptions.Center).rectTransform,
                0, AlturaDoConteudo - rodape + 8, L, 18);
    }

    static CelularLogica.ItemNaFila ItemDaFila(string id)
    {
        foreach (var i in CelularLogica.Fila) if (i.id == id) return i;
        return null;
    }

    void ConstruirGaleria()
    {
        Cabecalho("Fotos", IrParaInicio);
        var fotos = AlbumDeFotos.Fotos;
        if (fotos.Count == 0)
        {
            Em(Texto(conteudo, "Nenhuma foto", 14, TextoFraco, TextAlignmentOptions.Center).rectTransform, 0, AlturaDoConteudo / 2f - 10, L, 20);
            return;
        }
        const float esp = 2f;
        float lado = (L - esp * 2f) / 3f;
        int linhas = Mathf.CeilToInt(fotos.Count / 3f);
        RectTransform lista = Lista(48, 0, linhas * (lado + esp));
        for (int i = fotos.Count - 1, k = 0; i >= 0; i--, k++)   // mais recentes primeiro
        {
            AlbumDeFotos.Foto f = fotos[i];
            RawImage mini = Novo("Foto", lista).gameObject.AddComponent<RawImage>();
            Em(mini.rectTransform, (k % 3) * (lado + esp), (k / 3) * (lado + esp), lado, lado);
            if (f.imagem != null)
            {
                mini.texture = f.imagem;
                float aspecto = f.imagem.width / (float)f.imagem.height;
                mini.uvRect = aspecto >= 1f ? new Rect((1f - 1f / aspecto) / 2f, 0f, 1f / aspecto, 1f) : new Rect(0f, (1f - aspecto) / 2f, 1f, aspecto);
            }
            else mini.color = new Color(0.2f, 0.2f, 0.22f);
            int indice = i;
            Clicavel(mini, () => dono.AbrirFoto(indice));
        }
    }

    void ConstruirFoto()
    {
        Limpar(fotoView);
        var fotos = AlbumDeFotos.Fotos;
        if (fotoAberta < 0 || fotoAberta >= fotos.Count) return;
        AlbumDeFotos.Foto f = fotos[fotoAberta];
        Esticar(Caixa(fotoView, Color.black).rectTransform);
        RawImage img = Novo("Imagem", fotoView).gameObject.AddComponent<RawImage>();
        img.raycastTarget = false;
        Esticar(img.rectTransform);
        if (f.imagem != null)
        {
            img.texture = f.imagem;
            var ajuste = img.gameObject.AddComponent<AspectRatioFitter>();
            ajuste.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ajuste.aspectRatio = f.imagem.width / (float)f.imagem.height;
        }
        else img.color = new Color(0.12f, 0.12f, 0.13f);

        // Barra de cima: voltar e posição.
        Image topo = Caixa(fotoView, new Color(0, 0, 0, 0.45f));
        Em(topo.rectTransform, 0, 0, A, 34);
        var voltar = Texto(topo.transform, "‹  Fotos", 15, Color.white, TextAlignmentOptions.Left);
        Em(voltar.rectTransform, 30, 6, 110, 24);
        Clicavel(voltar, FecharFoto);
        Em(Texto(topo.transform, $"{fotoAberta + 1} de {fotos.Count}", 12, TextoFraco, TextAlignmentOptions.Right).rectTransform, A - 140, 9, 110, 18);

        // Laterais: passar de foto (as mais antigas à esquerda).
        if (fotoAberta > 0)
        {
            RectTransform esq = Em(Novo("Anterior", fotoView), 0, 34, 60, L - 34);
            Toque(esq, () => { fotoAberta--; ConstruirFoto(); });
        }
        if (fotoAberta < fotos.Count - 1)
        {
            RectTransform dir = Em(Novo("Proxima", fotoView), A - 60, 34, 60, L - 34);
            Toque(dir, () => { fotoAberta++; ConstruirFoto(); });
        }
    }
}
