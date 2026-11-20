using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição do estado e serialização do EnvelopeDoGravador.</summary>
public class EnvelopeDoGravadorTestes
{
    static readonly Quaternion RotInicialAba = Quaternion.Euler(0f, 15f, 0f);
    static readonly Vector3 PosInicialGravador = new Vector3(0.01f, -0.02f, 0.03f);

    GameObject raiz;
    EnvelopeDoGravador envelope;
    Transform aba;
    Gravador gravador;
    MeshRenderer rendererGravador;
    BoxCollider colliderGravador;

    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        Chaveiro.Limpar();

        raiz = new GameObject("EnvelopeDeGuardaTeste");
        envelope = raiz.AddComponent<EnvelopeDoGravador>();

        var abaGo = new GameObject("Aba");
        abaGo.transform.SetParent(raiz.transform, false);
        abaGo.transform.localRotation = RotInicialAba;
        aba = abaGo.transform;

        var gravGo = new GameObject("Gravador de Leo");
        gravGo.transform.SetParent(raiz.transform, false);
        gravGo.transform.localPosition = PosInicialGravador;
        gravador = gravGo.AddComponent<Gravador>();
        rendererGravador = gravGo.AddComponent<MeshRenderer>();
        colliderGravador = gravGo.AddComponent<BoxCollider>();

        envelope.aba = aba;
        envelope.gravador = gravador;
    }

    [TearDown]
    public void Limpar()
    {
        if (raiz != null) Object.DestroyImmediate(raiz);
        raiz = null;
        EstadoDaNoite.Limpar();
        Chaveiro.Limpar();
    }

    [Test]
    public void CarregarLacradoMantemAbaFechadaEGravadorOcultoDesativado()
    {
        envelope.Inicializar();
        envelope.Carregar("0");

        Assert.That(rendererGravador.enabled, Is.False);
        Assert.That(colliderGravador.enabled, Is.False);
        Assert.That(gravador.enabled, Is.False);
        Assert.That(gravador.gameObject.activeSelf, Is.True);
        Assert.That(Quaternion.Angle(aba.localRotation, RotInicialAba), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(gravador.transform.localPosition, PosInicialGravador), Is.LessThan(1e-5f));
        Assert.That(envelope.Salvar(), Is.EqualTo("0"));
        Assert.That(envelope.Instrucoes, Is.EqualTo("Clique: abrir o envelope"));
    }

    [Test]
    public void CarregarAbertoGiraAbaDeslizaELiberaGravador()
    {
        envelope.Carregar("1");

        Quaternion rotEsperada = RotInicialAba * Quaternion.Euler(envelope.anguloAberta, 0f, 0f);
        Vector3 posEsperada = PosInicialGravador + envelope.deslize;

        Assert.That(rendererGravador.enabled, Is.True);
        Assert.That(colliderGravador.enabled, Is.True);
        Assert.That(gravador.enabled, Is.True);
        Assert.That(gravador.gameObject.activeSelf, Is.True);
        Assert.That(Quaternion.Angle(aba.localRotation, rotEsperada), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(gravador.transform.localPosition, posEsperada), Is.LessThan(1e-5f));
        Assert.That(envelope.Salvar(), Is.EqualTo("1"));
        Assert.That(envelope.Instrucoes, Is.EqualTo("Clique: tirar o gravador"));
    }

    [Test]
    public void CarregarVazioAbreAbaEDesativaGravador()
    {
        envelope.Carregar("2");

        Quaternion rotEsperada = RotInicialAba * Quaternion.Euler(envelope.anguloAberta, 0f, 0f);

        Assert.That(gravador.gameObject.activeSelf, Is.False);
        Assert.That(rendererGravador.enabled, Is.False);
        Assert.That(colliderGravador.enabled, Is.False);
        Assert.That(gravador.enabled, Is.False);
        Assert.That(Quaternion.Angle(aba.localRotation, rotEsperada), Is.LessThan(0.01f));
        Assert.That(envelope.Salvar(), Is.EqualTo("2"));
        Assert.That(envelope.Instrucoes, Is.EqualTo(""));
    }

    [Test]
    public void FatoGravadorPegoForcaEstadoVazioMesmoAoCarregarLacradoOuAberto()
    {
        EstadoDaNoite.Marcar("gravador_pego");
        envelope.Inicializar();

        Quaternion rotEsperada = RotInicialAba * Quaternion.Euler(envelope.anguloAberta, 0f, 0f);

        Assert.That(gravador.gameObject.activeSelf, Is.False);
        Assert.That(Quaternion.Angle(aba.localRotation, rotEsperada), Is.LessThan(0.01f));
        Assert.That(envelope.Salvar(), Is.EqualTo("2"));

        envelope.Carregar("0");
        Assert.That(gravador.gameObject.activeSelf, Is.False);
        Assert.That(envelope.Salvar(), Is.EqualTo("2"));

        envelope.Carregar("1");
        Assert.That(gravador.gameObject.activeSelf, Is.False);
        Assert.That(envelope.Salvar(), Is.EqualTo("2"));
    }

    [Test]
    public void AlternarEntreEstadosPreservaPoseInicialSemDeriva()
    {
        envelope.Carregar("1");
        envelope.Carregar("2");
        envelope.Carregar("0");

        Assert.That(gravador.gameObject.activeSelf, Is.True);
        Assert.That(rendererGravador.enabled, Is.False);
        Assert.That(colliderGravador.enabled, Is.False);
        Assert.That(gravador.enabled, Is.False);
        Assert.That(Quaternion.Angle(aba.localRotation, RotInicialAba), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(gravador.transform.localPosition, PosInicialGravador), Is.LessThan(1e-5f));
        Assert.That(envelope.Salvar(), Is.EqualTo("0"));

        envelope.Carregar("1");
        Quaternion rotAberta = RotInicialAba * Quaternion.Euler(envelope.anguloAberta, 0f, 0f);
        Assert.That(rendererGravador.enabled, Is.True);
        Assert.That(colliderGravador.enabled, Is.True);
        Assert.That(gravador.enabled, Is.True);
        Assert.That(Quaternion.Angle(aba.localRotation, rotAberta), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(gravador.transform.localPosition, PosInicialGravador + envelope.deslize), Is.LessThan(1e-5f));
        Assert.That(envelope.Salvar(), Is.EqualTo("1"));
    }
}
