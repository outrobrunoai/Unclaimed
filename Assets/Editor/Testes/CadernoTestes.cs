using System.IO;
using System.Linq;
using NUnit.Framework;

public class CadernoTestes
{
    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
        Caderno.Catalogo = new CatalogoDeEvidencias
        {
            evidencias = new[]
            {
                new Evidencia { id = "A", titulo = "Primeira", resumo = "Resumo A" },
                new Evidencia { id = "B", titulo = "Segunda", resumo = "Resumo B" },
                new Evidencia { id = "C", titulo = "Terceira", resumo = "Resumo C" },
                new Evidencia { id = "D", titulo = "Quarta", resumo = "Resumo D" }
            },
            comparacoes = new[]
            {
                new Comparacao
                {
                    id = "AB", tipo = "conclusao", titulo = "Conclusão", texto = "Texto da conclusão.",
                    pares = new[] { new ParDeEvidencias { a = "A", b = "B" }, new ParDeEvidencias { a = "A", b = "C" } }
                },
                new Comparacao
                {
                    id = "BD", tipo = "hipotese", titulo = "Hipótese", texto = "Texto da hipótese.",
                    pares = new[] { new ParDeEvidencias { a = "B", b = "D" } }
                }
            }
        };
    }

    [TearDown]
    public void Limpar()
    {
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
    }

    [Test]
    public void ColetaMarcaFatoUmaVez()
    {
        Assert.That(Caderno.Coletou("A"), Is.False);
        Assert.That(Caderno.Coletar("A"), Is.True);
        Assert.That(Caderno.Coletar("A"), Is.False);
        Assert.That(Caderno.Coletou("A"), Is.True);
        Assert.That(EstadoDaNoite.Fatos, Is.EquivalentTo(new[] { "ev:A" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("desconhecida")]
    public void ColetaDesconhecidaNaoMarcaFatos(string id)
    {
        Assert.That(Caderno.Coletar(id), Is.False);
        Assert.That(Caderno.Coletou(id), Is.False);
        Assert.That(EstadoDaNoite.Fatos, Is.Empty);
    }

    [TestCase("A", "A")]
    [TestCase("desconhecida", "B")]
    [TestCase("A", "desconhecida")]
    [TestCase(null, "A")]
    [TestCase("A", "")]
    public void ComparacaoInvalidaNaoMarcaFatos(string a, string b)
    {
        Caderno.Coletar("A");
        Caderno.Coletar("B");
        string[] antes = EstadoDaNoite.Fatos.ToArray();
        Assert.That(Caderno.Comparar(a, b, out Comparacao comparacao), Is.EqualTo(ResultadoDaComparacao.Invalida));
        Assert.That(comparacao, Is.Null);
        Assert.That(EstadoDaNoite.Fatos, Is.EquivalentTo(antes));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void ComparacaoExigeAsDuasEvidencias(bool coletarA, bool coletarB)
    {
        if (coletarA) Caderno.Coletar("A");
        if (coletarB) Caderno.Coletar("B");
        string[] antes = EstadoDaNoite.Fatos.ToArray();
        Assert.That(Caderno.Comparar("A", "B", out Comparacao comparacao), Is.EqualTo(ResultadoDaComparacao.FaltaEvidencia));
        Assert.That(comparacao, Is.Null);
        Assert.That(EstadoDaNoite.Fatos, Is.EquivalentTo(antes));
    }

    [Test]
    public void EvidenciasSemRelacaoNaoGeramConclusao()
    {
        Caderno.Coletar("C");
        Caderno.Coletar("D");
        Assert.That(Caderno.Comparar("C", "D", out Comparacao comparacao), Is.EqualTo(ResultadoDaComparacao.SemRelacao));
        Assert.That(comparacao, Is.Null);
        Assert.That(EstadoDaNoite.Fatos, Is.EquivalentTo(new[] { "ev:C", "ev:D" }));
    }

    [TestCase("A", "B")]
    [TestCase("B", "A")]
    public void NovaComparacaoFuncionaNasDuasOrdensERepetidaJaEstaRegistrada(string a, string b)
    {
        Caderno.Coletar("A");
        Caderno.Coletar("B");
        Assert.That(Caderno.Comparar(a, b, out Comparacao nova), Is.EqualTo(ResultadoDaComparacao.Nova));
        Assert.That(nova, Is.SameAs(Caderno.Catalogo.comparacoes[0]));
        Assert.That(EstadoDaNoite.Tem("con:AB"), Is.True);
        Assert.That(Caderno.Comparar(b, a, out Comparacao repetida), Is.EqualTo(ResultadoDaComparacao.JaRegistrada));
        Assert.That(repetida, Is.SameAs(nova));
        Assert.That(Caderno.Conclusoes().Count(), Is.EqualTo(1));
    }

    [Test]
    public void ParesDiferentesGeramUmaUnicaConclusao()
    {
        Caderno.Coletar("A");
        Caderno.Coletar("B");
        Caderno.Coletar("C");
        Assert.That(Caderno.Comparar("A", "B", out Comparacao primeira), Is.EqualTo(ResultadoDaComparacao.Nova));
        Assert.That(Caderno.Comparar("C", "A", out Comparacao segunda), Is.EqualTo(ResultadoDaComparacao.JaRegistrada));
        Assert.That(segunda, Is.SameAs(primeira));
        Assert.That(Caderno.Conclusoes().Select(c => c.id), Is.EqualTo(new[] { "AB" }));
    }

    [Test]
    public void ListasSeguemOrdemDoCatalogoEIncluemHipoteses()
    {
        Assert.That(Caderno.Coletadas(), Is.Empty);
        Assert.That(Caderno.Conclusoes(), Is.Empty);
        Caderno.Coletar("D");
        Caderno.Coletar("B");
        Caderno.Coletar("A");
        Caderno.Comparar("D", "B", out _);
        Caderno.Comparar("A", "B", out _);
        Assert.That(Caderno.Coletadas().Select(e => e.id), Is.EqualTo(new[] { "A", "B", "D" }));
        Assert.That(Caderno.Conclusoes().Select(c => c.id), Is.EqualTo(new[] { "AB", "BD" }));
    }

    [Test]
    public void CatalogoRealNaoTemProblemasEEncontraTrocaNaOrdemInvertida()
    {
        string texto = File.ReadAllText("Assets/Resources/Historia/evidencias.json");
        CatalogoDeEvidencias catalogo = CatalogoDeEvidencias.Carregar(texto);
        Assert.That(catalogo.Problemas(), Is.Empty);
        Assert.That(catalogo.ComparacaoEntre("F02", "F01").id, Is.EqualTo("troca"));
    }

    [TestCase("idEvidencia")]
    [TestCase("idComparacao")]
    [TestCase("parDesconhecido")]
    [TestCase("parIgual")]
    [TestCase("tituloEvidencia")]
    [TestCase("resumo")]
    [TestCase("tituloComparacao")]
    [TestCase("texto")]
    public void ValidacaoDetectaProblemasDoCatalogo(string problema)
    {
        CatalogoDeEvidencias catalogo = Caderno.Catalogo;
        Assert.That(catalogo.Problemas(), Is.Empty);
        switch (problema)
        {
            case "idEvidencia": catalogo.evidencias[1].id = "A"; break;
            case "idComparacao": catalogo.comparacoes[1].id = "AB"; break;
            case "parDesconhecido": catalogo.comparacoes[0].pares[0].b = "desconhecida"; break;
            case "parIgual": catalogo.comparacoes[0].pares[0].b = "A"; break;
            case "tituloEvidencia": catalogo.evidencias[0].titulo = ""; break;
            case "resumo": catalogo.evidencias[0].resumo = null; break;
            case "tituloComparacao": catalogo.comparacoes[0].titulo = " "; break;
            case "texto": catalogo.comparacoes[0].texto = ""; break;
        }
        Assert.That(catalogo.Problemas(), Is.Not.Empty);
    }
}
