using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição da introdução de chegada e entrega pontual de mensagens.</summary>
public class IntroChegadaTestes
{
    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        CelularLogica.Limpar();
        CelularLogica.Dados = null;
        IntroChegada.DefinirAtivaParaTeste(false);
    }

    [TearDown]
    public void Limpar() => Preparar();

    [Test]
    public void DeveTocarRespondeAoFatoDaIntroducao()
    {
        Assert.That(Salvamento.VaiCarregar, Is.False);
        Assert.That(IntroChegada.DeveTocar(), Is.True);

        EstadoDaNoite.Marcar(IntroChegada.Fato);
        Assert.That(IntroChegada.DeveTocar(), Is.False);
    }

    [Test]
    public void EntregarEntregaUmaVezERejeitaInexistente()
    {
        int recebidas = 0;
        void Ouvir(DadosDoCelular.Mensagem m) => recebidas++;

        CelularLogica.AoReceber += Ouvir;
        try
        {
            Assert.That(CelularLogica.Entregar("celso_02"), Is.True);
            Assert.That(EstadoDaNoite.Tem("msg:celso_02"), Is.True);
            Assert.That(recebidas, Is.EqualTo(1));

            Assert.That(CelularLogica.Entregar("celso_02"), Is.False);
            Assert.That(recebidas, Is.EqualTo(1));

            // Mensagem do próprio jogador marca o fato mas não dispara AoReceber.
            Assert.That(CelularLogica.Entregar("intro_02"), Is.True);
            Assert.That(EstadoDaNoite.Tem("msg:intro_02"), Is.True);
            Assert.That(recebidas, Is.EqualTo(1));

            Assert.That(CelularLogica.Entregar("mensagem_inexistente"), Is.False);
        }
        finally
        {
            CelularLogica.AoReceber -= Ouvir;
        }
    }

    [Test]
    public void ConcluirSemJogadorMarcaFatoEntregaMensagensEIdempotente()
    {
        var go = new GameObject("IntroChegadaTeste");
        try
        {
            var intro = go.AddComponent<IntroChegada>();
            intro.mensagens = new[] { "intro_01", "intro_02" };
            IntroChegada.DefinirAtivaParaTeste(true);

            intro.Concluir();

            Assert.That(EstadoDaNoite.Tem(IntroChegada.Fato), Is.True);
            Assert.That(EstadoDaNoite.Tem("msg:intro_01"), Is.True);
            Assert.That(EstadoDaNoite.Tem("msg:intro_02"), Is.True);
            Assert.That(IntroChegada.Ativa, Is.False);

            Assert.DoesNotThrow(() => intro.Concluir());
            Assert.That(IntroChegada.Ativa, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SalvamentoNaoSalvaComIntroducaoAtiva()
    {
        IntroChegada.DefinirAtivaParaTeste(true);
        Assert.That(Salvamento.Salvar(), Is.False);
    }
}
