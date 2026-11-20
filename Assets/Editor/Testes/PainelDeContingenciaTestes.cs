using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição do painel físico de contingência, cartão e encaixe de fusível.</summary>
public class PainelDeContingenciaTestes
{
    static readonly MethodInfo MetodoOnEnable =
        typeof(PainelDeContingencia).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo MetodoOnDisable =
        typeof(PainelDeContingencia).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly List<GameObject> objetos = new List<GameObject>();
    readonly List<Material> materiais = new List<Material>();

    Material lenteAcesa, lenteApagada;
    Material mat6, mat9, mat10, mat13;

    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
        RedeEletrica.Padrao();
        Chaveiro.Limpar();

        lenteAcesa = CriarMaterial("LenteAcesa");
        lenteApagada = CriarMaterial("LenteApagada");
        mat6 = CriarMaterial("Visor6");
        mat9 = CriarMaterial("Visor9");
        mat10 = CriarMaterial("Visor10");
        mat13 = CriarMaterial("Visor13");
    }

    [TearDown]
    public void Limpar()
    {
        foreach (GameObject go in objetos)
        {
            if (go == null) continue;
            foreach (PainelDeContingencia p in go.GetComponentsInChildren<PainelDeContingencia>(true))
                MetodoOnDisable?.Invoke(p, null);
            UnityEngine.Object.DestroyImmediate(go);
        }
        objetos.Clear();

        foreach (Material mat in materiais)
            if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
        materiais.Clear();

        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
        RedeEletrica.Padrao();
        Chaveiro.Limpar();
    }

    Material CriarMaterial(string nome)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
        var mat = new Material(shader) { name = nome };
        materiais.Add(mat);
        return mat;
    }

    GameObject CriarObjeto(string nome)
    {
        var go = new GameObject(nome);
        objetos.Add(go);
        return go;
    }

    Renderer CriarQuad(string nome, Transform pai)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = nome;
        quad.transform.SetParent(pai);
        objetos.Add(quad);
        return quad.GetComponent<Renderer>();
    }

    PainelDeContingencia CriarPainel(
        out Transform selRefrig, out Transform selGeral, out Transform selExame,
        out Renderer lenRefrig, out Renderer lenGeral, out Renderer lenExame,
        out Renderer visor,
        bool ativarEvento = false)
    {
        GameObject raiz = CriarObjeto("PainelDeContingencia");
        var painel = raiz.AddComponent<PainelDeContingencia>();
        painel.lenteAcesa = lenteAcesa;
        painel.lenteApagada = lenteApagada;

        selRefrig = CriarObjeto("SeletorRefrigeracao").transform;
        selRefrig.SetParent(raiz.transform);
        selGeral = CriarObjeto("SeletorGeral").transform;
        selGeral.SetParent(raiz.transform);
        selExame = CriarObjeto("SeletorExame").transform;
        selExame.SetParent(raiz.transform);

        lenRefrig = CriarQuad("LenteRefrigeracao", raiz.transform);
        lenGeral = CriarQuad("LenteGeral", raiz.transform);
        lenExame = CriarQuad("LenteExame", raiz.transform);
        visor = CriarQuad("Visor", raiz.transform);

        painel.colunas = new[]
        {
            new PainelDeContingencia.Coluna { circuito = RedeEletrica.Refrigeracao, seletor = selRefrig, lente = lenRefrig },
            new PainelDeContingencia.Coluna { circuito = RedeEletrica.Geral, seletor = selGeral, lente = lenGeral },
            new PainelDeContingencia.Coluna { circuito = RedeEletrica.Exame, seletor = selExame, lente = lenExame },
        };
        painel.visor = visor;
        painel.leituras = new[]
        {
            new PainelDeContingencia.Leitura { total = 6, material = mat6 },
            new PainelDeContingencia.Leitura { total = 9, material = mat9 },
            new PainelDeContingencia.Leitura { total = 10, material = mat10 },
            new PainelDeContingencia.Leitura { total = 13, material = mat13 },
        };

        if (ativarEvento) MetodoOnEnable?.Invoke(painel, null);
        return painel;
    }

    static void ConferirAnguloZ(Transform t, float esperado) =>
        Assert.That(Mathf.DeltaAngle(t.localEulerAngles.z, esperado), Is.EqualTo(0f).Within(0.01f));

    [Test]
    public void EstadoInicialAplicaAngulosLentesEVisorPreservandoXeY()
    {
        PainelDeContingencia painel = CriarPainel(
            out Transform selRefrig, out Transform selGeral, out Transform selExame,
            out Renderer lenRefrig, out Renderer lenGeral, out Renderer lenExame,
            out Renderer visor);

        selRefrig.localEulerAngles = new Vector3(12f, 24f, 0f);
        selGeral.localEulerAngles = new Vector3(0f, 18f, 0f);
        selExame.localEulerAngles = new Vector3(10f, 0f, 0f);

        painel.Atualizar();

        ConferirAnguloZ(selRefrig, 315f);
        ConferirAnguloZ(selGeral, 315f);
        ConferirAnguloZ(selExame, 45f);
        Assert.That(Mathf.DeltaAngle(selRefrig.localEulerAngles.x, 12f), Is.EqualTo(0f).Within(0.01f));
        Assert.That(Mathf.DeltaAngle(selRefrig.localEulerAngles.y, 24f), Is.EqualTo(0f).Within(0.01f));

        Assert.That(lenRefrig.sharedMaterial, Is.EqualTo(lenteAcesa));
        Assert.That(lenGeral.sharedMaterial, Is.EqualTo(lenteAcesa));
        Assert.That(lenExame.sharedMaterial, Is.EqualTo(lenteApagada));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat9));
    }

    [Test]
    public void AcionarExameComGeralLigadaRecusaSemMudarEstadoEColetaD04()
    {
        PainelDeContingencia painel = CriarPainel(
            out _, out _, out Transform selExame,
            out _, out _, out Renderer lenExame,
            out Renderer visor,
            ativarEvento: true);

        painel.Acionar(RedeEletrica.Exame);

        Assert.That(RedeEletrica.Ligado(RedeEletrica.Exame), Is.False);
        ConferirAnguloZ(selExame, 45f);
        Assert.That(lenExame.sharedMaterial, Is.EqualTo(lenteApagada));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat9));
        Assert.That(Caderno.Coletou("D04"), Is.True);
    }

    [Test]
    public void DesligarGeralPermiteLigarExameEAtualizaAngulosLentesEVisor()
    {
        PainelDeContingencia painel = CriarPainel(
            out _, out Transform selGeral, out Transform selExame,
            out _, out Renderer lenGeral, out Renderer lenExame,
            out Renderer visor,
            ativarEvento: true);

        painel.Acionar(RedeEletrica.Geral);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);
        ConferirAnguloZ(selGeral, 45f);
        Assert.That(lenGeral.sharedMaterial, Is.EqualTo(lenteApagada));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat6));

        painel.Acionar(RedeEletrica.Exame);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Exame), Is.True);
        ConferirAnguloZ(selExame, 315f);
        Assert.That(lenExame.sharedMaterial, Is.EqualTo(lenteAcesa));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat10));
    }

    [Test]
    public void AcionarRefrigeracaoMantemCircuitoLigado()
    {
        PainelDeContingencia painel = CriarPainel(
            out Transform selRefrig, out _, out _,
            out Renderer lenRefrig, out _, out _,
            out Renderer visor,
            ativarEvento: true);

        painel.Acionar(RedeEletrica.Refrigeracao);

        Assert.That(RedeEletrica.Ligado(RedeEletrica.Refrigeracao), Is.True);
        ConferirAnguloZ(selRefrig, 315f);
        Assert.That(lenRefrig.sharedMaterial, Is.EqualTo(lenteAcesa));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat9));
    }

    [Test]
    public void IndicacaoDoPainelEDaChaveSeguemEstadoDoCircuito()
    {
        PainelDeContingencia painel = CriarPainel(
            out _, out _, out _,
            out _, out _, out _,
            out _,
            ativarEvento: true);

        Assert.That(painel.Indicacao(RedeEletrica.Geral), Is.EqualTo("Desligar: Luz geral"));
        Assert.That(painel.Indicacao(RedeEletrica.Exame), Is.EqualTo("Ligar: Luz de exame (autópsia)"));
        Assert.That(painel.Indicacao(RedeEletrica.Refrigeracao), Is.EqualTo("Chave da refrigeração (travada)"));
        Assert.That(painel.Indicacao("inexistente"), Is.EqualTo(""));

        GameObject goChave = CriarObjeto("ChaveGeral");
        var chave = goChave.AddComponent<ChaveDoPainel>();
        chave.circuito = RedeEletrica.Geral;
        Assert.That(chave.Indicacao, Is.EqualTo(""));

        chave.painel = painel;
        Assert.That(chave.Indicacao, Is.EqualTo("Desligar: Luz geral"));
        chave.Interagir();
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);
        Assert.That(chave.Indicacao, Is.EqualTo("Ligar: Luz geral"));
    }

    [Test]
    public void RemoverFusivelApagaLenteEAtualizaVisorPeloEvento()
    {
        PainelDeContingencia painel = CriarPainel(
            out _, out Transform selGeral, out _,
            out _, out Renderer lenGeral, out _,
            out Renderer visor,
            ativarEvento: true);

        Assert.That(lenGeral.sharedMaterial, Is.EqualTo(lenteAcesa));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat9));

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);

        Assert.That(lenGeral.sharedMaterial, Is.EqualTo(lenteApagada));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat6));

        // Desliga a chave e tenta religar sem fusível: a alavanca não gira para ligado.
        painel.Acionar(RedeEletrica.Geral);
        ConferirAnguloZ(selGeral, 45f);
        painel.Acionar(RedeEletrica.Geral);
        Assert.That(RedeEletrica.Buscar(RedeEletrica.Geral).ligado, Is.False);
        ConferirAnguloZ(selGeral, 45f);
    }

    [Test]
    public void EncaixeDeFusivelExigeItemParaRecolocarERestauraLuzGeral()
    {
        GameObject go = CriarObjeto("QuadroGeral");
        var encaixe = go.AddComponent<EncaixeDeFusivel>();

        Assert.That(encaixe.Indicacao, Is.EqualTo(""));

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);
        Assert.That(encaixe.Indicacao, Is.EqualTo("Ver o encaixe do fusível"));

        encaixe.Interagir();
        Assert.That(RedeEletrica.Buscar(RedeEletrica.Geral).fusivel, Is.False);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);

        Chaveiro.Adicionar("fusivel_reserva");
        Assert.That(encaixe.Indicacao, Is.EqualTo("Recolocar o fusível da luz geral"));

        encaixe.Interagir();
        Assert.That(RedeEletrica.Buscar(RedeEletrica.Geral).fusivel, Is.True);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.True);
        Assert.That(encaixe.Indicacao, Is.EqualTo(""));
    }

    [Test]
    public void CartaoDoPainelColetaEvidenciaD04()
    {
        GameObject go = CriarObjeto("CartaoD04");
        var cartao = go.AddComponent<CartaoDoPainel>();

        Assert.That(cartao.Indicacao, Is.EqualTo("Ler o cartão do painel"));
        Assert.That(Caderno.Coletou("D04"), Is.False);

        cartao.Interagir();

        Assert.That(Caderno.Coletou("D04"), Is.True);
    }

    [Test]
    public void SalvarECarregarRestauramEstadoDaRedeEDoPainel()
    {
        PainelDeContingencia painel = CriarPainel(
            out _, out Transform selGeral, out Transform selExame,
            out _, out Renderer lenGeral, out Renderer lenExame,
            out Renderer visor,
            ativarEvento: true);

        painel.Acionar(RedeEletrica.Geral);
        painel.Acionar(RedeEletrica.Exame);
        string json = painel.Salvar();

        RedeEletrica.Padrao();
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.True);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Exame), Is.False);
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat9));

        painel.Carregar(json);

        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Exame), Is.True);
        ConferirAnguloZ(selGeral, 45f);
        ConferirAnguloZ(selExame, 315f);
        Assert.That(lenGeral.sharedMaterial, Is.EqualTo(lenteApagada));
        Assert.That(lenExame.sharedMaterial, Is.EqualTo(lenteAcesa));
        Assert.That(visor.sharedMaterial, Is.EqualTo(mat10));
    }
}
