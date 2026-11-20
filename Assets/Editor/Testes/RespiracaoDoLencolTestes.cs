using NUnit.Framework;
using UnityEngine;

/// <summary>Testes unitários das funções puras de RespiracaoDoLencol.</summary>
public class RespiracaoDoLencolTestes
{
    [Test]
    public void DeslocamentoRespeitaCicloAmplitudeEPeso()
    {
        float ciclo = 4.2f;
        float amplitude = 0.012f;
        float peso = 0.75f;

        Assert.That(RespiracaoDoLencol.Deslocamento(0f, ciclo, amplitude, peso), Is.EqualTo(0f).Within(1e-5f));
        Assert.That(RespiracaoDoLencol.Deslocamento(ciclo * 0.5f, ciclo, amplitude, peso), Is.EqualTo(amplitude * peso).Within(1e-5f));
        Assert.That(RespiracaoDoLencol.Deslocamento(ciclo * 0.5f, ciclo, amplitude, 0f), Is.EqualTo(0f).Within(1e-5f));
    }

    [Test]
    public void QuedaSuaveDoCentroAteORaio()
    {
        float raio = 0.35f;

        Assert.That(RespiracaoDoLencol.Queda(0f, raio), Is.EqualTo(1f).Within(1e-5f));
        Assert.That(RespiracaoDoLencol.Queda(raio, raio), Is.EqualTo(0f).Within(1e-5f));
        Assert.That(RespiracaoDoLencol.Queda(raio * 1.5f, raio), Is.EqualTo(0f).Within(1e-5f));

        float meio = RespiracaoDoLencol.Queda(raio * 0.5f, raio);
        Assert.That(meio, Is.GreaterThan(0f).And.LessThan(1f));
    }

    [Test]
    public void OlhandoConsideraAnguloEDistanciaMaxima()
    {
        Vector3 olhos = Vector3.zero;
        Vector3 frente = Vector3.forward;
        float angulo = 14f;
        float distMax = 9f;

        Assert.That(RespiracaoDoLencol.Olhando(olhos, frente, new Vector3(0f, 0f, 3f), angulo, distMax), Is.True);

        Vector3 a30Graus = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward * 3f;
        Assert.That(RespiracaoDoLencol.Olhando(olhos, frente, a30Graus, angulo, distMax), Is.False);

        Assert.That(RespiracaoDoLencol.Olhando(olhos, frente, new Vector3(0f, 0f, -3f), angulo, distMax), Is.False);
        Assert.That(RespiracaoDoLencol.Olhando(olhos, frente, new Vector3(0f, 0f, 12f), angulo, distMax), Is.False);
    }
}
