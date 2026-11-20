using NUnit.Framework;
using UnityEngine;

/// <summary>Testes unitários das funções puras de OlharDoJogador.</summary>
public class OlharDoJogadorTestes
{
    static readonly Vector3 Olhos = Vector3.zero;
    static readonly Vector3 Frente = Vector3.forward;

    [Test]
    public void PontoAFrenteEstaDentroDoOlhar()
    {
        Assert.That(OlharDoJogador.DentroDoOlhar(Olhos, Frente, new Vector3(0f, 0f, 5f), 60f, 30f), Is.True);
    }

    [Test]
    public void PontoA70GrausComAngulo60FicaFora()
    {
        Vector3 ponto = Quaternion.Euler(0f, 70f, 0f) * Vector3.forward * 5f;
        Assert.That(OlharDoJogador.DentroDoOlhar(Olhos, Frente, ponto, 60f, 30f), Is.False);
    }

    [Test]
    public void PontoA50GrausComAngulo60FicaDentro()
    {
        Vector3 ponto = Quaternion.Euler(0f, 50f, 0f) * Vector3.forward * 5f;
        Assert.That(OlharDoJogador.DentroDoOlhar(Olhos, Frente, ponto, 60f, 30f), Is.True);
    }

    [Test]
    public void PontoAtrasFicaFora()
    {
        Assert.That(OlharDoJogador.DentroDoOlhar(Olhos, Frente, new Vector3(0f, 0f, -5f), 60f, 30f), Is.False);
    }

    [Test]
    public void PontoAlemDaDistanciaMaximaFicaFora()
    {
        Assert.That(OlharDoJogador.DentroDoOlhar(Olhos, Frente, new Vector3(0f, 0f, 35f), 60f, 30f), Is.False);
    }

    [Test]
    public void OlhosSobreOPontoFicamDentro()
    {
        Assert.That(OlharDoJogador.DentroDoOlhar(Olhos, Frente, Olhos, 60f, 30f), Is.True);
    }
}
