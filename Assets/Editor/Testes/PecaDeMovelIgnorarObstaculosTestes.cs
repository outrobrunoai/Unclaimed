using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição de PecaDeMovel.IrPara(alvo, ignorarObstaculos).</summary>
public class PecaDeMovelIgnorarObstaculosTestes
{
    GameObject go;
    PecaDeMovel peca;

    [SetUp]
    public void Preparar()
    {
        go = new GameObject("PortaDeTeste");
        peca = go.AddComponent<PecaDeMovel>();
        peca.tipo = PecaDeMovel.TipoDePeca.Porta;
        peca.curso = 90f;
        peca.trancada = false;
    }

    [TearDown]
    public void Limpar()
    {
        if (go != null) Object.DestroyImmediate(go);
        go = null;
        peca = null;
    }

    [Test]
    public void IrParaIgnorandoObstaculosQuandoTrancadaNaoAlteraDestinoAberta()
    {
        peca.trancada = true;

        peca.IrPara(30f, true);

        Assert.That(peca.DestinoAberta, Is.False);
        Assert.That(peca.EmMovimento, Is.False);
    }

    [Test]
    public void IrParaZeroQuandoAbertaETrancadaNaoAlteraDestinoAberta()
    {
        peca.Carregar("{\"abertura\":45,\"trancada\":true}");
        Assert.That(peca.DestinoAberta, Is.True);

        peca.IrPara(0f, true);

        Assert.That(peca.DestinoAberta, Is.True);
        Assert.That(peca.EmMovimento, Is.False);
    }

    [Test]
    public void IrParaPositivoQuandoDestrancadaDefineDestinoAbertaTrue()
    {
        peca.IrPara(8f, true);

        Assert.That(peca.DestinoAberta, Is.True);
        Assert.That(peca.EmMovimento, Is.True);
    }

    [Test]
    public void IrParaZeroOuNegativoQuandoDestrancadaDefineDestinoAbertaFalse()
    {
        peca.IrPara(60f, true);
        Assert.That(peca.DestinoAberta, Is.True);

        peca.IrPara(0f, true);
        Assert.That(peca.DestinoAberta, Is.False);

        peca.IrPara(45f, true);
        peca.IrPara(-10f, true);
        Assert.That(peca.DestinoAberta, Is.False);
    }
}
