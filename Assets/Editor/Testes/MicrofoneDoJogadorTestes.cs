using NUnit.Framework;
using UnityEngine;

/// <summary>Testes unitários das funções puras de MicrofoneDoJogador e reação ao barulho de DanielPerseguidor.</summary>
public class MicrofoneDoJogadorTestes
{
    [Test]
    public void NivelDbSilencioRetornaMenos80()
    {
        float[] silencio = new float[512];
        Assert.That(MicrofoneDoJogador.NivelDb(silencio, 0, silencio.Length), Is.EqualTo(-80f));
    }

    [Test]
    public void NivelDbSenoAmplitudeUmAproximadamenteMenos3Db()
    {
        float[] seno = new float[512];
        for (int i = 0; i < seno.Length; i++)
            seno[i] = Mathf.Sin(2f * Mathf.PI * 8f * i / seno.Length);

        float db = MicrofoneDoJogador.NivelDb(seno, 0, seno.Length);
        Assert.That(db, Is.EqualTo(-3f).Within(0.5f));
    }

    [Test]
    public void NivelDbLeituraCircularIgualAoBlocoContiguo()
    {
        float[] bloco = new float[64];
        for (int i = 0; i < bloco.Length; i++)
            bloco[i] = Mathf.Sin(i * 0.37f) * 0.5f;

        float[] circular = new float[100];
        int inicio = 80;
        for (int i = 0; i < bloco.Length; i++)
            circular[(inicio + i) % circular.Length] = bloco[i];

        float dbContiguo = MicrofoneDoJogador.NivelDb(bloco, 0, bloco.Length);
        float dbCircular = MicrofoneDoJogador.NivelDb(circular, inicio, bloco.Length);
        Assert.That(dbCircular, Is.EqualTo(dbContiguo).Within(1e-5f));
    }

    [Test]
    public void AcimaDoLimiarAcimaDePisoMaisMargemRetornaTrue()
    {
        Assert.That(MicrofoneDoJogador.AcimaDoLimiar(-30f, -50f, 18f, -42f), Is.True);
    }

    [Test]
    public void AcimaDoLimiarAbaixoDePisoMaisMargemRetornaFalse()
    {
        Assert.That(MicrofoneDoJogador.AcimaDoLimiar(-35f, -50f, 18f, -42f), Is.False);
    }

    [Test]
    public void AcimaDoLimiarAbaixoDoMinimoAbsolutoRetornaFalse()
    {
        Assert.That(MicrofoneDoJogador.AcimaDoLimiar(-45f, -70f, 18f, -42f), Is.False);
    }

    [Test]
    public void ReagirAoBarulhoForaDoRaioRetornaNada()
    {
        Assert.That(DanielPerseguidor.ReagirAoBarulho(20f, false, 6f, 14f), Is.EqualTo(DanielPerseguidor.ReacaoAoBarulho.Nada));
    }

    [Test]
    public void ReagirAoBarulhoExpostoDentroDoRaioRetornaInvestigar()
    {
        Assert.That(DanielPerseguidor.ReagirAoBarulho(10f, false, 6f, 14f), Is.EqualTo(DanielPerseguidor.ReacaoAoBarulho.Investigar));
    }

    [Test]
    public void ReagirAoBarulhoEscondidoPertoRetornaDescobrir()
    {
        Assert.That(DanielPerseguidor.ReagirAoBarulho(5f, true, 6f, 14f), Is.EqualTo(DanielPerseguidor.ReacaoAoBarulho.Descobrir));
    }

    [Test]
    public void ReagirAoBarulhoEscondidoLongeRetornaInvestigar()
    {
        Assert.That(DanielPerseguidor.ReagirAoBarulho(10f, true, 6f, 14f), Is.EqualTo(DanielPerseguidor.ReacaoAoBarulho.Investigar));
    }

    [Test]
    public void ReagirAoBarulhoExpostoPertoRetornaInvestigar()
    {
        Assert.That(DanielPerseguidor.ReagirAoBarulho(5f, false, 6f, 14f), Is.EqualTo(DanielPerseguidor.ReacaoAoBarulho.Investigar));
    }
}
