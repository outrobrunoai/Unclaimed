using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EstadoDaNoiteTestes
{
    Action<string> aoMarcar;

    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
    }

    [TearDown]
    public void Limpar()
    {
        if (aoMarcar != null) EstadoDaNoite.AoMarcar -= aoMarcar;
        aoMarcar = null;
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
    }

    [Test]
    public void FatoNovoDisparaEventoUmaVezERepetidoNaoDispara()
    {
        int chamadas = 0;
        string recebido = null;
        aoMarcar = fato => { chamadas++; recebido = fato; };
        EstadoDaNoite.AoMarcar += aoMarcar;

        Assert.That(EstadoDaNoite.Marcar("ev:A"), Is.True);
        Assert.That(EstadoDaNoite.Marcar("ev:A"), Is.False);
        Assert.That(chamadas, Is.EqualTo(1));
        Assert.That(recebido, Is.EqualTo("ev:A"));
        Assert.That(EstadoDaNoite.Tem("ev:A"), Is.True);
        Assert.That(EstadoDaNoite.Fatos, Is.EquivalentTo(new[] { "ev:A" }));
    }

    [TestCase(null)]
    [TestCase("")]
    public void FatoVazioNaoMarcaNemDisparaEvento(string fato)
    {
        int chamadas = 0;
        aoMarcar = _ => chamadas++;
        EstadoDaNoite.AoMarcar += aoMarcar;
        Assert.That(EstadoDaNoite.Marcar(fato), Is.False);
        Assert.That(EstadoDaNoite.Tem(fato), Is.False);
        Assert.That(EstadoDaNoite.Fatos, Is.Empty);
        Assert.That(chamadas, Is.Zero);
    }

    [Test]
    public void ExportarEImportarRestauramFatosEFaseSemAnunciarDescobertas()
    {
        EstadoDaNoite.Marcar("ev:A");
        EstadoDaNoite.Marcar("con:AB");
        EstadoDaNoite.Fase = FaseDeDaniel.Caca;
        string json = EstadoDaNoite.Exportar();
        EstadoDaNoite.Limpar();
        Assert.That(EstadoDaNoite.Fatos, Is.Empty);
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Mesa));
        int chamadas = 0;
        aoMarcar = _ => chamadas++;
        EstadoDaNoite.AoMarcar += aoMarcar;

        EstadoDaNoite.Importar(json);

        Assert.That(EstadoDaNoite.Fatos, Is.EquivalentTo(new[] { "ev:A", "con:AB" }));
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Caca));
        Assert.That(chamadas, Is.Zero);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("{lixo")]
    [TestCase("{\"fase\":99,\"fatos\":[]}")]
    public void ImportacaoAusenteOuInvalidaLimpaEstado(string json)
    {
        EstadoDaNoite.Marcar("ev:A");
        EstadoDaNoite.Fase = FaseDeDaniel.Obstrucao;
        if (!string.IsNullOrEmpty(json))
            LogAssert.Expect(LogType.Warning, "EstadoDaNoite: estado salvo inválido; a noite foi reiniciada.");

        Assert.DoesNotThrow(() => EstadoDaNoite.Importar(json));

        Assert.That(EstadoDaNoite.Fatos, Is.Empty);
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Mesa));
    }

    [TestCase("{\"versao\":1,\"cena\":\"Teste\"}", false)]
    [TestCase("{\"versao\":1,\"cena\":\"Teste\",\"noite\":\"{lixo\"}", true)]
    public void CampoNoiteDoSaveAntigoOuCorrompidoCarregaVazio(string json, bool corrompido)
    {
        // Usa o contêiner real do save sem carregar cenas nem escrever no save do jogador.
        Type tipo = typeof(Salvamento).GetNestedType("Dados", BindingFlags.NonPublic);
        Assert.That(tipo, Is.Not.Null);
        object dados = JsonUtility.FromJson(json, tipo);
        FieldInfo campo = tipo.GetField("noite");
        Assert.That(campo, Is.Not.Null);
        string noite = (string)campo.GetValue(dados);
        if (!corrompido) Assert.That(noite, Is.Null.Or.Empty);
        EstadoDaNoite.Marcar("ev:A");
        EstadoDaNoite.Fase = FaseDeDaniel.Sombra;
        if (corrompido)
            LogAssert.Expect(LogType.Warning, "EstadoDaNoite: estado salvo inválido; a noite foi reiniciada.");

        Assert.DoesNotThrow(() => EstadoDaNoite.Importar(noite));

        Assert.That(EstadoDaNoite.Fatos, Is.Empty);
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Mesa));
    }
}
