using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Testes de modo de edição para a frase de saída e as condições dos parágrafos do epílogo.</summary>
public class FinaisTestes
{
    static readonly string[] Provas784 = { "D11", "D12", "D13", "D15" };

    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
    }

    [TearDown]
    public void Limpar() => Preparar();

    [Test]
    public void FraseDeSaidaSegueONivelMaisAltoAlcancado()
    {
        var t = Epilogo.CarregarTextos();

        Assert.That(Epilogo.FraseDeSaida(EstadoDaNoite.Tem, t), Is.EqualTo(t.saidaNada));

        EstadoDaNoite.Marcar("ev:D02");
        Assert.That(Epilogo.FraseDeSaida(EstadoDaNoite.Tem, t), Is.EqualTo(t.saidaAutopsia));

        EstadoDaNoite.Marcar("ev:F01");
        Assert.That(Epilogo.FraseDeSaida(EstadoDaNoite.Tem, t), Is.EqualTo(t.saidaMesa));

        EstadoDaNoite.Marcar("con:troca");
        Assert.That(Epilogo.FraseDeSaida(EstadoDaNoite.Tem, t), Is.EqualTo(t.saidaTroca));

        EstadoDaNoite.Limpar();
        EstadoDaNoite.Marcar("enviou_algo");
        Assert.That(Epilogo.FraseDeSaida(EstadoDaNoite.Tem, t), Is.EqualTo(t.saidaProvas));

        EstadoDaNoite.Limpar();
        EstadoDaNoite.Marcar("gravador_pego");
        Assert.That(Epilogo.FraseDeSaida(EstadoDaNoite.Tem, t), Is.EqualTo(t.saidaProvas));
    }

    [Test]
    public void GravadorAbandonadoSoApareceQuandoJogadorConheceOGravador()
    {
        var t = Epilogo.CarregarTextos();

        var semConhecer = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Obstrucao, Provas784);
        Assert.That(semConhecer.gravador, Is.EqualTo(DestinoDoGravador.Abandonado));
        Assert.That(semConhecer.conheceGravador, Is.False);
        List<string> pSem = Epilogo.Paragrafos(semConhecer, t);
        Assert.That(pSem, Has.No.Member(t.gravadorAbandonado));

        EstadoDaNoite.Marcar("ev:D09");
        var conhecendo = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Obstrucao, Provas784);
        Assert.That(conhecendo.gravador, Is.EqualTo(DestinoDoGravador.Abandonado));
        Assert.That(conhecendo.conheceGravador, Is.True);
        List<string> pCom = Epilogo.Paragrafos(conhecendo, t);
        Assert.That(pCom, Has.Member(t.gravadorAbandonado));
    }

    [TestCase("ev:D10")]
    [TestCase("ev:D13")]
    public void EvidenciasDoGravadorMarcamConheceGravador(string fato)
    {
        var t = Epilogo.CarregarTextos();
        EstadoDaNoite.Marcar(fato);
        var r = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Obstrucao, Provas784);
        Assert.That(r.conheceGravador, Is.True);
        Assert.That(Epilogo.Paragrafos(r, t), Has.Member(t.gravadorAbandonado));
    }

    [Test]
    public void SamuelNaoLocalizadoSoApareceComAlgumaEvidenciaDo041()
    {
        var t = Epilogo.CarregarTextos();

        var sem041 = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Obstrucao, Provas784);
        Assert.That(sem041.samuel, Is.EqualTo(CasoSamuel.NaoLocalizado));
        Assert.That(sem041.conhece041, Is.False);
        Assert.That(Epilogo.Paragrafos(sem041, t), Has.No.Member(t.samuelNaoLocalizado));

        for (int i = 1; i <= 8; i++)
        {
            EstadoDaNoite.Limpar();
            EstadoDaNoite.Marcar("ev:H0" + i);
            var com041 = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Obstrucao, Provas784);
            Assert.That(com041.conhece041, Is.True, "ev:H0" + i);
            Assert.That(Epilogo.Paragrafos(com041, t), Has.Member(t.samuelNaoLocalizado), "ev:H0" + i);
        }
    }
}
