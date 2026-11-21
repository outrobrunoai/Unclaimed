using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cadeado de quatro discos preso ao fecho de um armário. Enquanto fechado, as portas (PecaDeMovel) ficam trancadas.
///
/// Examinar o cadeado (ObjetoInspecionavel no mesmo objeto) mostra o próprio modelo na frente da câmera; este
/// componente manipula a cópia da inspeção e mantém o cadeado real igual a ela (o real fica oculto enquanto isso):
/// clicar num disco o seleciona, arrastar para cima/baixo ou usar o scroll sobre ele gira um dígito por vez, com
/// encaixe animado; ←/→ (A/D) escolhe o disco e ↑/↓ (W/S) gira. Clicar no arco (ou Espaço/Enter) puxa o arco:
/// combinação errada, ele cede um pouco e volta (sem limite de tentativas); certa, ele destrava e sobe. Arrastar
/// fora dos discos e do arco gira o cadeado inteiro. Os dígitos escolhidos ficam no cadeado entre inspeções.
///
/// Ao abrir, a inspeção fecha e o cadeado é retirado do fecho: sobe o bastante para a perna curta do arco passar
/// sobre o grampo com o corpo ainda por baixo dele, sai reto para a frente, sobe na frente do armário, passa
/// por cima do tampo e é deitado nele. Só então a aba do fecho gira para fora e as portas são liberadas.
/// </summary>
[RequireComponent(typeof(ObjetoInspecionavel))]
public class CadeadoCombinacao : MonoBehaviour, IManipulavelNaInspecao, ISalvavel
{
    [Header("Combinação")]
    [Tooltip("Quatro dígitos que abrem o cadeado.")]
    public string combinacao = "2317";
    [Tooltip("Dígitos mostrados no início da sessão.")]
    public string inicial = "0000";

    [Header("Peças")]
    [Tooltip("Raiz visual (corpo, arco e discos): é o modelo copiado na inspeção.")]
    public Transform modelo;
    [Tooltip("Os quatro discos, da esquerda para a direita (giram em torno do próprio X).")]
    public Transform[] discos = new Transform[4];
    [Tooltip("Graus por dígito (sinal = sentido em que o próximo dígito sobe para a frente).")]
    public float grausPorDigito = -36f;
    [Tooltip("Duração do encaixe de um dígito (s).")]
    [Min(0.02f)] public float duracaoDoEncaixe = 0.12f;
    public Transform arco;
    [Tooltip("Curso do arco aberto, no espaço do cadeado (a perna curta sai do corpo).")]
    public Vector3 cursoDoArco = new Vector3(0f, 0.015f, 0f);
    [Tooltip("Quanto o arco cede ao ser puxado com a combinação errada (m).")]
    public float folgaDoArco = 0.0012f;

    [Header("Retirada (espaço do pai do cadeado)")]
    [Tooltip("Subida que tira a perna curta de dentro do grampo, com o corpo ainda abaixo dele.")]
    public float subidaParaSoltar = 0.0125f;
    [Tooltip("Direção 'para fora' do armário, no espaço do pai.")]
    public Vector3 frente = Vector3.back;
    [Tooltip("Quanto o cadeado se afasta para a frente antes de subir.")]
    public float afastamento = 0.09f;
    [Tooltip("Altura de transporte acima do pouso (passa sobre o tampo sem tocá-lo).")]
    public float alturaSobreOPouso = 0.09f;
    [Tooltip("Pose final do cadeado aberto (sobre o armário).")]
    public Transform pousoAberto;
    [Tooltip("Aba articulada do fecho (gira em Y no próprio pivô depois que o cadeado saiu).")]
    public Transform abaDoFecho;
    public float giroDaAba = -170f;
    public PecaDeMovel[] portas;

    [Header("Som (opcional)")]
    public AudioSource fonte;
    public AudioClip somDisco, somErro, somAbrir;

    public bool Destrancado => destrancado;
    /// <summary>Verdadeiro do destrave até o cadeado pousar e as portas serem liberadas.</summary>
    public bool EmRetirada => retirando;
    public string Atual => new string(System.Array.ConvertAll(Digitos(), d => (char)('0' + d)));
    public int Selecionado => selecionado;
    /// <summary>Ângulo visual de cada disco, em dígitos (para testes: mostra o encaixe animado).</summary>
    public float Visual(int disco) => visual[disco];
    /// <summary>Abertura atual do arco (m ao longo de cursoDoArco).</summary>
    public float ArcoAtual => arcoAtual;

    const string Instr = "Clique num disco e arraste (ou roda do mouse) para girar  |  ←/→ escolhe, ↑/↓ gira  |  " +
                         "Clique no arco ou Espaço: puxar  |  Arraste fora dos discos: girar o cadeado";
    public string Instrucoes => destrancado ? "" : Instr;

    // Estado dos discos: contagem sem limite (o dígito é ela mod 10, e 9→0 continua girando no mesmo sentido).
    readonly int[] alvo = new int[4];
    readonly float[] visual = new float[4], inicioVisual = new float[4], inicioEncaixe = new float[4];
    Quaternion[] rotacaoBase;
    Vector3 arcoBase, modeloBase;
    float arcoAtual, tremor;
    int selecionado;
    bool destrancado, retirando, puxando;

    // Cópia da inspeção
    Transform copia, copiaArco;
    readonly Transform[] copiaDiscos = new Transform[4];
    Renderer[] copiaDiscosR = new Renderer[4];
    Vector3 copiaBase;
    MaterialPropertyBlock blocoSel;
    int discoArrastado = -1;
    bool capturouPonteiro, botaoAntes;
    float arrasto;
    Vector2 mouseAntes;
    ObjetoInspecionavel inspecao;

    void Awake()
    {
        inspecao = GetComponent<ObjetoInspecionavel>();
        if (modelo == null) modelo = transform;
        rotacaoBase = new Quaternion[discos.Length];
        for (int i = 0; i < discos.Length; i++) if (discos[i] != null) rotacaoBase[i] = discos[i].localRotation;
        for (int i = 0; i < 4; i++)
        {
            alvo[i] = inicial.Length > i && char.IsDigit(inicial[i]) ? inicial[i] - '0' : 0;
            visual[i] = inicioVisual[i] = alvo[i];
        }
        if (arco != null) arcoBase = arco.localPosition;
        modeloBase = modelo.localPosition;
        foreach (PecaDeMovel p in portas) if (p != null) p.trancada = true;
        Aplicar();
    }

    int[] Digitos()
    {
        var d = new int[4];
        for (int i = 0; i < 4; i++) d[i] = ((alvo[i] % 10) + 10) % 10;
        return d;
    }

    // ---------- API (também usada pelos testes) ----------

    /// <summary>Gira um disco um ou mais dígitos (positivo = próximo dígito), com encaixe animado.</summary>
    public void Girar(int disco, int passo)
    {
        if (destrancado || disco < 0 || disco > 3 || passo == 0) return;
        inicioVisual[disco] = visual[disco];
        inicioEncaixe[disco] = Time.time;
        alvo[disco] += passo;
        selecionado = disco;
        if (fonte != null && somDisco != null) fonte.PlayOneShot(somDisco);
    }

    /// <summary>Leva um disco ao dígito pedido pelo caminho mais curto.</summary>
    public void Definir(int disco, int valor)
    {
        if (disco < 0 || disco > 3) return;
        int atual = ((alvo[disco] % 10) + 10) % 10, d = ((Mathf.Clamp(valor, 0, 9) - atual) % 10 + 10) % 10;
        Girar(disco, d > 5 ? d - 10 : d);
    }

    public void Selecionar(int disco) => selecionado = Mathf.Clamp(disco, 0, 3);

    /// <summary>Puxa o arco: abre se a combinação está certa; senão ele cede um pouco e volta. True se abriu.</summary>
    public bool PuxarArco()
    {
        if (destrancado) return true;
        if (puxando) return false;
        if (Atual == combinacao)
        {
            destrancado = retirando = true;
            if (fonte != null && somAbrir != null) fonte.PlayOneShot(somAbrir);
            StartCoroutine(Abrir());
            return true;
        }
        if (fonte != null && somErro != null) fonte.PlayOneShot(somErro);
        StartCoroutine(Resistir());
        return false;
    }

    /// <summary>Compatibilidade: o mesmo que puxar o arco.</summary>
    public bool TentarAbrir() => PuxarArco();

    // ---------- Save (ISalvavel) ----------

    [System.Serializable] struct EstadoSalvo { public string digitos; public bool destrancado; }

    public string Salvar() => JsonUtility.ToJson(new EstadoSalvo { digitos = Atual, destrancado = destrancado });

    /// <summary>Dígitos como estavam; aberto vai direto para a pose final da retirada (sem animação).</summary>
    public void Carregar(string estado)
    {
        var e = JsonUtility.FromJson<EstadoSalvo>(estado);
        for (int i = 0; i < 4; i++)
            if (e.digitos != null && e.digitos.Length > i && char.IsDigit(e.digitos[i]))
            {
                alvo[i] = e.digitos[i] - '0';
                visual[i] = inicioVisual[i] = alvo[i];
            }
        if (e.destrancado && !destrancado) RestaurarAberto();
        Aplicar();
    }

    void RestaurarAberto()
    {
        destrancado = true;
        retirando = puxando = false;
        arcoAtual = cursoDoArco.magnitude;
        if (inspecao != null) inspecao.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        // Mesma pose final de Abrir().
        Vector3 p0 = transform.localPosition;
        Quaternion r0 = transform.localRotation;
        Vector3 pFora = p0 + Vector3.up * subidaParaSoltar + frente.normalized * afastamento;
        Vector3 pouso = pousoAberto != null ? transform.parent.InverseTransformPoint(pousoAberto.position) : pFora;
        Quaternion rPouso = pousoAberto != null ? Quaternion.Inverse(transform.parent.rotation) * pousoAberto.rotation : r0;
        transform.SetLocalPositionAndRotation(pouso, rPouso);
        if (abaDoFecho != null) abaDoFecho.localRotation *= Quaternion.AngleAxis(giroDaAba, Vector3.up);
        foreach (PecaDeMovel p in portas) if (p != null) p.trancada = false;
    }

    // ---------- Animação ----------

    void LateUpdate()
    {
        for (int i = 0; i < 4; i++)
        {
            float t = Mathf.Clamp01((Time.time - inicioEncaixe[i]) / duracaoDoEncaixe);
            visual[i] = Mathf.Lerp(inicioVisual[i], alvo[i], 1f - (1f - t) * (1f - t));   // sai rápido, encaixa suave
        }
        if (tremor > 0f) tremor -= Time.deltaTime;
        Aplicar();
    }

    void Aplicar()
    {
        Vector3 arcoPos = arcoBase + cursoDoArco.normalized * arcoAtual;
        Vector3 sacode = tremor > 0f ? new Vector3(Mathf.Sin(Time.time * 95f) * 0.0006f * (tremor / 0.25f), 0f, 0f) : Vector3.zero;
        for (int i = 0; i < 4 && i < discos.Length; i++)
        {
            Quaternion r = rotacaoBase[i] * Quaternion.AngleAxis(grausPorDigito * visual[i], Vector3.right);
            if (discos[i] != null) discos[i].localRotation = r;
            if (copiaDiscos[i] != null) copiaDiscos[i].localRotation = r;
        }
        if (arco != null) arco.localPosition = arcoPos;
        if (copiaArco != null) copiaArco.localPosition = arcoPos;
        if (modelo != transform && !retirando) modelo.localPosition = modeloBase + sacode;
        if (copia != null) copia.localPosition = copiaBase + copia.parent.InverseTransformVector(copia.TransformVector(sacode));
        if (copia != null) Destacar();
    }

    void Destacar()
    {
        blocoSel ??= new MaterialPropertyBlock();
        for (int i = 0; i < 4; i++)
        {
            Renderer r = copiaDiscosR[i];
            if (r == null) continue;
            if (i == selecionado && !destrancado)
            {
                blocoSel.SetColor("_BaseColor", new Color(1f, 0.70f, 0.28f));
                r.SetPropertyBlock(blocoSel);
            }
            else r.SetPropertyBlock(null);
        }
    }

    IEnumerator Resistir()
    {
        puxando = true;
        tremor = 0.25f;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.22f)
        {
            arcoAtual = folgaDoArco * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);   // sobe a folga e volta
            yield return null;
        }
        arcoAtual = 0f;
        puxando = false;
    }

    IEnumerator Abrir()
    {
        // 1. O arco destrava e sobe (visto na inspeção).
        float curso = cursoDoArco.magnitude;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.2f)
        {
            arcoAtual = curso * (1f - (1f - t) * (1f - t));
            yield return null;
        }
        arcoAtual = curso;
        yield return new WaitForSeconds(0.75f);
        inspecao.EncerrarInspecao();
        inspecao.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;   // fica só como objeto de cena
        yield return new WaitForSeconds(0.35f);

        // 2. Retirada pelo caminho livre (no espaço do pai: o armário).
        Vector3 f = frente.normalized, p0 = transform.localPosition;
        Quaternion r0 = transform.localRotation;
        Vector3 pSolto = p0 + Vector3.up * subidaParaSoltar;
        Vector3 pFora = pSolto + f * afastamento;
        Vector3 pouso = pousoAberto != null ? transform.parent.InverseTransformPoint(pousoAberto.position) : pFora;
        Quaternion rPouso = pousoAberto != null ? Quaternion.Inverse(transform.parent.rotation) * pousoAberto.rotation : r0;
        float altura = pouso.y + alturaSobreOPouso;
        Vector3 pAlto = new Vector3(pFora.x, altura, pFora.z);
        Vector3 pSobre = new Vector3(pouso.x, altura, pouso.z);
        yield return Mover(p0, pSolto, r0, r0, 0.45f);      // perna curta sobe acima do grampo, corpo abaixo dele
        yield return Mover(pSolto, pFora, r0, r0, 0.6f);    // reto para a frente: livre do fecho e das portas
        yield return Mover(pFora, pAlto, r0, r0, 0.5f);     // sobe na frente do armário
        yield return Mover(pAlto, pSobre, r0, rPouso, 0.75f);   // passa sobre o tampo e deita
        yield return Mover(pSobre, pouso, rPouso, rPouso, 0.4f);

        // 3. Só com o cadeado pousado a aba gira para fora e as portas são liberadas.
        if (abaDoFecho != null)
        {
            Quaternion a0 = abaDoFecho.localRotation;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.5f)
            {
                abaDoFecho.localRotation = a0 * Quaternion.AngleAxis(giroDaAba * Mathf.SmoothStep(0f, 1f, t), Vector3.up);
                yield return null;
            }
            abaDoFecho.localRotation = a0 * Quaternion.AngleAxis(giroDaAba, Vector3.up);
        }
        foreach (PecaDeMovel p in portas) if (p != null) p.trancada = false;
        retirando = false;
        MiraDeInteracao.Avisar("Cadeado aberto. As portas do armário estão livres.");
    }

    IEnumerator Mover(Vector3 a, Vector3 b, Quaternion ra, Quaternion rb, float dur)
    {
        for (float t = 0; t < 1f; t += Time.deltaTime / dur)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.SetLocalPositionAndRotation(Vector3.Lerp(a, b, k), Quaternion.Slerp(ra, rb, k));
            yield return null;
        }
        transform.SetLocalPositionAndRotation(b, rb);
    }

    // ---------- Inspeção (IManipulavelNaInspecao) ----------

    public void AoIniciarInspecao(Transform copiaRaiz, Camera camera)
    {
        copia = copiaRaiz;
        copiaBase = copia.localPosition;
        copiaArco = arco != null ? Achar(copia, arco.name) : null;
        for (int i = 0; i < 4; i++)
        {
            copiaDiscos[i] = discos.Length > i && discos[i] != null ? Achar(copia, discos[i].name) : null;
            copiaDiscosR[i] = copiaDiscos[i] != null ? copiaDiscos[i].GetComponent<Renderer>() : null;
        }
        discoArrastado = -1;
        capturouPonteiro = false;
        botaoAntes = true;   // o clique que porventura estiver pressionado não conta como clique novo
        Aplicar();
    }

    public void AoFinalizarInspecao()
    {
        copia = copiaArco = null;
        for (int i = 0; i < 4; i++) { copiaDiscos[i] = null; copiaDiscosR[i] = null; }
        discoArrastado = -1;
        capturouPonteiro = false;
    }

    static Transform Achar(Transform raiz, string nome)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t;
        return null;
    }

    public UsoDaEntrada ProcessarEntrada(Camera camera, Vector2 mouse, bool botao, float scroll)
    {
        if (copia == null) return UsoDaEntrada.Nenhum;
        if (destrancado) return UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;   // aberto: só observar até fechar
        UsoDaEntrada uso = UsoDaEntrada.Nenhum;
        int sob = Mirar(camera, mouse);   // 0..3 disco, 4 arco, 5 corpo, -1 nada
        bool clique = botao && !botaoAntes;

        if (clique)
        {
            if (sob >= 0 && sob <= 3) { discoArrastado = sob; selecionado = sob; arrasto = 0f; mouseAntes = mouse; capturouPonteiro = true; }
            else if (sob == 4) { capturouPonteiro = true; PuxarArco(); }
        }
        if (botao && capturouPonteiro)
        {
            uso |= UsoDaEntrada.Ponteiro;
            if (discoArrastado >= 0)
            {
                // Arrastar para baixo empurra a face do disco para baixo: entra o dígito de cima (o próximo).
                arrasto += mouse.y - mouseAntes.y;
                float passo = Mathf.Max(12f, Screen.height * 0.03f);
                while (arrasto <= -passo) { Girar(discoArrastado, 1); arrasto += passo; }
                while (arrasto >= passo) { Girar(discoArrastado, -1); arrasto -= passo; }
            }
        }
        if (!botao) { capturouPonteiro = false; discoArrastado = -1; }

        if (Mathf.Abs(scroll) > 0.01f && sob >= 0 && sob <= 3)
        {
            Girar(sob, scroll > 0f ? 1 : -1);
            uso |= UsoDaEntrada.Rolagem;
        }

#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) selecionado = (selecionado + 3) % 4;
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) selecionado = (selecionado + 1) % 4;
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Girar(selecionado, 1);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Girar(selecionado, -1);
            if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) PuxarArco();
        }
#else
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) selecionado = (selecionado + 3) % 4;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) selecionado = (selecionado + 1) % 4;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Girar(selecionado, 1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Girar(selecionado, -1);
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) PuxarArco();
#endif
        botaoAntes = botao;
        mouseAntes = mouse;
        return uso;
    }

    /// <summary>Peça da cópia sob o mouse: 0..3 disco, 4 arco, 5 corpo, -1 nada (caixas das malhas, a mais próxima).</summary>
    public int Mirar(Camera camera, Vector2 mouse)
    {
        if (copia == null || camera == null) return -1;
        Ray raio = camera.ScreenPointToRay(mouse);
        int melhor = -1;
        float dist = float.MaxValue;
        void Testar(Transform t, int id)
        {
            if (t == null) return;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var local = new Ray(t.InverseTransformPoint(raio.origin), t.InverseTransformDirection(raio.direction));
            if (!mf.sharedMesh.bounds.IntersectRay(local, out float d)) return;
            float mundo = Vector3.Distance(raio.origin, t.TransformPoint(local.GetPoint(d)));
            if (mundo < dist) { dist = mundo; melhor = id; }
        }
        for (int i = 0; i < 4; i++) Testar(copiaDiscos[i], i);
        Testar(copiaArco, 4);
        Testar(Achar(copia, "Corpo"), 5);
        return melhor;
    }

    /// <summary>Para testes: posição de tela do centro de uma peça da cópia (0..3 disco, 4 arco, 5 corpo).</summary>
    public Vector2 TelaDaPeca(Camera camera, int id)
    {
        Transform t = id <= 3 ? copiaDiscos[id] : id == 4 ? copiaArco : copia != null ? Achar(copia, "Corpo") : null;
        if (t == null || camera == null) return new Vector2(-1, -1);
        var mf = t.GetComponent<MeshFilter>();
        Vector3 c = mf != null && mf.sharedMesh != null ? t.TransformPoint(mf.sharedMesh.bounds.center) : t.position;
        return camera.WorldToScreenPoint(c);
    }
}
