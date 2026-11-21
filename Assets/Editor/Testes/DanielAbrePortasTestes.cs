using NUnit.Framework;
using UnityEngine;

/// <summary>Testes unitários da função pura de DanielAbrePortas.</summary>
public class DanielAbrePortasTestes
{
    static readonly Vector3 Origem = Vector3.zero;
    static readonly Vector3 Frente = Vector3.forward;
    const float Alcance = 1.6f;
    const float Angulo = 75f;

    [Test]
    public void PortaAFrentePertoEFechadaRetornaTrue()
    {
        Vector3 porta = new Vector3(0f, 0f, 1.2f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, false), Is.True);
    }

    [Test]
    public void PortaAtrasRetornaFalse()
    {
        Vector3 porta = new Vector3(0f, 0f, -1f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, false), Is.False);
    }

    [Test]
    public void PortaLongeRetornaFalse()
    {
        Vector3 porta = new Vector3(0f, 0f, 2.5f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, false), Is.False);
    }

    [Test]
    public void PortaTrancadaRetornaFalse()
    {
        Vector3 porta = new Vector3(0f, 0f, 1.2f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, true, false), Is.False);
    }

    [Test]
    public void PortaJaAbertaRetornaFalse()
    {
        Vector3 porta = new Vector3(0f, 0f, 1.2f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 60f, false, false), Is.False);
    }

    [Test]
    public void PortaComDestinoAbertoRetornaFalse()
    {
        Vector3 porta = new Vector3(0f, 0f, 1.2f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, true), Is.False);
    }

    [Test]
    public void DirecaoZeroRetornaFalse()
    {
        Vector3 porta = new Vector3(0f, 0f, 1.2f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Vector3.zero, porta, Alcance, Angulo, 0f, false, false), Is.False);
    }

    [Test]
    public void PortaA60GrausComAngulo75RetornaTrue()
    {
        Vector3 porta = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 1.2f;
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, false), Is.True);
    }

    [Test]
    public void PortaA80GrausComAngulo75RetornaFalse()
    {
        Vector3 porta = Quaternion.Euler(0f, 80f, 0f) * Vector3.forward * 1.2f;
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, false), Is.False);
    }

    [Test]
    public void DistanciaIgnoraAlturaVertical()
    {
        Vector3 porta = new Vector3(0f, 5f, 1.2f);
        Assert.That(DanielAbrePortas.DeveAbrir(Origem, Frente, porta, Alcance, Angulo, 0f, false, false), Is.True);
    }
}
