using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição dos sistemas das etapas 2 a 5 (regras puras, sem cena).</summary>
public class SistemasDoJoseTestes
{
    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
        RedeEletrica.Padrao();
        CelularLogica.Limpar();
        CelularLogica.Dados = null;
        AlbumDeFotos.Limpar();
    }

    [TearDown]
    public void Limpar() => Preparar();

    // ------------------------------------------------------------------ painel de energia (P02, S1)

    [Test]
    public void PainelComecaComRefrigeracaoEGeralLigadas()
    {
        Assert.That(RedeEletrica.Consumo, Is.EqualTo(9));
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Refrigeracao), Is.True);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Exame), Is.False);
    }

    [Test]
    public void LuzDeExameSoLigaDepoisDeDesligarAGeral()
    {
        Assert.That(RedeEletrica.Definir(RedeEletrica.Exame, true), Is.EqualTo(ResultadoDoCircuito.Sobrecarga));
        Assert.That(EstadoDaNoite.Tem("luz_exame"), Is.False);
        Assert.That(RedeEletrica.Definir(RedeEletrica.Geral, false), Is.EqualTo(ResultadoDoCircuito.Ok));
        Assert.That(RedeEletrica.Definir(RedeEletrica.Exame, true), Is.EqualTo(ResultadoDoCircuito.Ok));
        Assert.That(RedeEletrica.Consumo, Is.EqualTo(10));
        Assert.That(EstadoDaNoite.Tem("luz_exame"), Is.True);
    }

    [Test]
    public void RefrigeracaoNaoDesliga()
    {
        Assert.That(RedeEletrica.Definir(RedeEletrica.Refrigeracao, false), Is.EqualTo(ResultadoDoCircuito.Essencial));
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Refrigeracao), Is.True);
    }

    [Test]
    public void ApagaoEFusivelReserva()
    {
        Assert.That(RedeEletrica.RemoverFusivel(RedeEletrica.Geral), Is.True);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);
        Assert.That(EstadoDaNoite.Tem("apagao"), Is.True);
        Assert.That(RedeEletrica.Definir(RedeEletrica.Geral, false), Is.EqualTo(ResultadoDoCircuito.Ok));
        Assert.That(RedeEletrica.Definir(RedeEletrica.Geral, true), Is.EqualTo(ResultadoDoCircuito.SemFusivel));
        Assert.That(RedeEletrica.ColocarFusivel(RedeEletrica.Geral), Is.True);
        Assert.That(RedeEletrica.Definir(RedeEletrica.Geral, true), Is.EqualTo(ResultadoDoCircuito.Ok));
    }

    [Test]
    public void FusivelDeVoltaComExameLigadoNaoPassaDoLimite()
    {
        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);          // geral apaga, mas a chave fica "ligada"
        Assert.That(RedeEletrica.Definir(RedeEletrica.Exame, true), Is.EqualTo(ResultadoDoCircuito.Ok));
        RedeEletrica.ColocarFusivel(RedeEletrica.Geral);
        Assert.That(RedeEletrica.Consumo, Is.LessThanOrEqualTo(RedeEletrica.Limite));
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Geral), Is.False);
    }

    [Test]
    public void RedeVaiEVoltaDoSave()
    {
        RedeEletrica.Definir(RedeEletrica.Geral, false);
        RedeEletrica.Definir(RedeEletrica.Exame, true);
        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        string salvo = RedeEletrica.Exportar();
        RedeEletrica.Padrao();
        RedeEletrica.Importar(salvo);
        Assert.That(RedeEletrica.Ligado(RedeEletrica.Exame), Is.True);
        Assert.That(RedeEletrica.Buscar(RedeEletrica.Geral).fusivel, Is.False);
    }

    // ------------------------------------------------------------------ álbum (F01, braço que mudou)

    [Test]
    public void FotoDeAlvoColetaEvidenciaEMarcaMudanca()
    {
        Assert.That(AlbumDeFotos.Registrar("mesa_autopsia", 0, "Mesa", "F01", null), Is.True);
        Assert.That(Caderno.Coletou("F01"), Is.True);
        Assert.That(EstadoDaNoite.Tem("mudou:mesa_autopsia"), Is.False);
        Assert.That(AlbumDeFotos.Registrar("mesa_autopsia", 0, "Mesa", "F01", null), Is.False);
        AlbumDeFotos.Registrar("mesa_autopsia", 1, "Mesa", "F01", null);
        Assert.That(EstadoDaNoite.Tem("mudou:mesa_autopsia"), Is.True);
    }

    [Test]
    public void AlbumVoltaPelosFatosEDescartaOutraPartida()
    {
        AlbumDeFotos.Registrar("gaveta_c02", 0, "Gaveta 2", "F02", null);
        AlbumDeFotos.Registrar(null, 0, "Foto", null, null);
        string salvo = EstadoDaNoite.Exportar();
        AlbumDeFotos.Limpar();
        EstadoDaNoite.Importar(salvo);
        AlbumDeFotos.SincronizarComFatos(true);
        Assert.That(AlbumDeFotos.Fotos.Count, Is.EqualTo(1));
        Assert.That(AlbumDeFotos.Fotos[0].alvo, Is.EqualTo("gaveta_c02"));

        EstadoDaNoite.Limpar();   // novo jogo
        AlbumDeFotos.SincronizarComFatos(true);
        Assert.That(AlbumDeFotos.Fotos, Is.Empty);
    }

    // ------------------------------------------------------------------ celular e envio

    [Test]
    public void ConteudoDoCelularSemProblemas()
    {
        Assert.That(DadosDoCelular.Padrao.Problemas(), Is.Empty);
        foreach (string id in DadosDoCelular.Padrao.envio.provasDa784)
            Assert.That(Caderno.Catalogo.Buscar(id), Is.Not.Null, id);
    }

    [Test]
    public void MensagemSoChegaComSinalEComOFato()
    {
        CelularLogica.ReceberIniciais();
        Assert.That(EstadoDaNoite.Tem("msg:celso_01"), Is.True);
        Assert.That(CelularLogica.ReceberPendentes(true), Is.EqualTo(0));
        Caderno.Coletar("D02");
        Assert.That(CelularLogica.ReceberPendentes(false), Is.EqualTo(0));
        Assert.That(CelularLogica.ReceberPendentes(true), Is.EqualTo(1));
        Assert.That(EstadoDaNoite.Tem("msg:celso_02"), Is.True);
    }

    [Test]
    public void EnvioSoAndaComSinal()
    {
        Caderno.Coletar("D11");
        Assert.That(CelularLogica.Enfileirar("D11"), Is.True);
        Assert.That(CelularLogica.Enfileirar("D11"), Is.False);
        CelularLogica.Avancar(100f, false);
        Assert.That(CelularLogica.Enviada("D11"), Is.False);
        CelularLogica.Avancar(100f, true);
        Assert.That(CelularLogica.Enviada("D11"), Is.True);
        Assert.That(EstadoDaNoite.Tem("enviou_algo"), Is.True);
    }

    [Test]
    public void AudioSoEnviaDepoisDaCopia()
    {
        Caderno.Coletar(CelularLogica.IdAudio);
        Assert.That(CelularLogica.PodeEnviar(CelularLogica.IdAudio, out _), Is.False);
        EstadoDaNoite.Marcar(CelularLogica.FatoAudioCopiado);
        Assert.That(CelularLogica.PodeEnviar(CelularLogica.IdAudio, out _), Is.True);
    }

    [Test]
    public void SamuelPrimeiroQuemRecebeuPrimeiro()
    {
        EstadoDaNoite.Marcar("con:samuel");
        var irene = CelularLogica.RespostasDisponiveis("irene").Single();
        var jornal = CelularLogica.RespostasDisponiveis("redacao").Single(r => r.marca == "samuel:jornal");
        Assert.That(CelularLogica.Responder(irene, false), Is.False);
        Assert.That(CelularLogica.Responder(irene, true), Is.True);
        Assert.That(CelularLogica.Responder(jornal, true), Is.True);
        Assert.That(EstadoDaNoite.Tem("samuel:irene:primeiro"), Is.True);
        Assert.That(EstadoDaNoite.Tem("samuel:jornal:primeiro"), Is.False);
        Assert.That(CelularLogica.RespostasDisponiveis("irene"), Is.Empty);
    }

    // ------------------------------------------------------------------ finais

    static readonly string[] Provas784 = { "D11", "D12", "D13", "D15" };

    [Test]
    public void SairCedoDaFinalIncompleto()
    {
        var r = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Sombra, Provas784);
        Assert.That(r.incompleto, Is.True);
        Assert.That(r.provas, Is.EqualTo(ProvasEnviadas.Nenhuma));
        Assert.That(r.gravador, Is.EqualTo(DestinoDoGravador.Abandonado));
    }

    [Test]
    public void TodasAsProvasECopiaSemOriginal()
    {
        foreach (string id in Provas784) EstadoDaNoite.Marcar("env:" + id);
        EstadoDaNoite.Marcar("enviou_algo");
        EstadoDaNoite.Marcar("gravador_pego");
        EstadoDaNoite.Marcar("gravador_entregue");
        EstadoDaNoite.Marcar("samuel:jornal:primeiro");
        var r = Epilogo.Calcular(EstadoDaNoite.Tem, FaseDeDaniel.Obstrucao, Provas784);
        Assert.That(r.incompleto, Is.False);
        Assert.That(r.provas, Is.EqualTo(ProvasEnviadas.Todas));
        Assert.That(r.gravador, Is.EqualTo(DestinoDoGravador.Entregue));
        Assert.That(r.copiaEnviada, Is.True);
        Assert.That(r.samuel, Is.EqualTo(CasoSamuel.JornalPrimeiro));

        var textos = Epilogo.CarregarTextos();
        List<string> p = Epilogo.Paragrafos(r, textos);
        Assert.That(p, Has.Member(textos.provasTodas));
        Assert.That(p, Has.Member(textos.copiaEnviada));
        Assert.That(p, Has.Member(textos.samuelJornal));
    }

    [Test]
    public void TextosDosFinaisPreenchidos()
    {
        var t = Epilogo.CarregarTextos();
        foreach (string s in new[] { t.incompleto, t.provasNenhuma, t.provasAlgumas, t.provasTodas, t.gravadorComJogador,
                     t.gravadorEntregue, t.gravadorAbandonado, t.copiaEnviada, t.samuelNaoLocalizado, t.samuelIrene, t.samuelJornal })
            Assert.That(s, Is.Not.Null.And.Not.Empty);
    }

    // ------------------------------------------------------------------ Daniel

    [Test]
    public void FasesAvancamPorFatosENuncaVoltam()
    {
        Assert.That(DecisoesDeDaniel.ProximaFase(FaseDeDaniel.Mesa, EstadoDaNoite.Tem), Is.EqualTo(FaseDeDaniel.Mesa));
        EstadoDaNoite.Marcar("daniel_saiu");
        Assert.That(DecisoesDeDaniel.ProximaFase(FaseDeDaniel.Mesa, EstadoDaNoite.Tem), Is.EqualTo(FaseDeDaniel.Sombra));
        EstadoDaNoite.Marcar("con:troca");
        Assert.That(DecisoesDeDaniel.ProximaFase(FaseDeDaniel.Sombra, EstadoDaNoite.Tem), Is.EqualTo(FaseDeDaniel.Obstrucao));
        EstadoDaNoite.Limpar();
        Assert.That(DecisoesDeDaniel.ProximaFase(FaseDeDaniel.Obstrucao, EstadoDaNoite.Tem), Is.EqualTo(FaseDeDaniel.Obstrucao));
    }

    [Test]
    public void SaiDaMesaSoComAsProvas()
    {
        Assert.That(DecisoesDeDaniel.PodeSairDaMesa(EstadoDaNoite.Tem), Is.False);
        EstadoDaNoite.Marcar("ev:F01");
        Assert.That(DecisoesDeDaniel.PodeSairDaMesa(EstadoDaNoite.Tem), Is.False);
        EstadoDaNoite.Marcar("luz_exame");
        Assert.That(DecisoesDeDaniel.PodeSairDaMesa(EstadoDaNoite.Tem), Is.False);   // a foto da mesa sozinha não basta
        EstadoDaNoite.Limpar();
        EstadoDaNoite.Marcar("ev:F02");
        Assert.That(DecisoesDeDaniel.PodeSairDaMesa(EstadoDaNoite.Tem), Is.True);
    }

    static DecisoesDeDaniel.Candidato C(string sala, TipoDeAcao tipo, float x, string grupo = "") => new DecisoesDeDaniel.Candidato
    {
        id = sala + "/x", sala = sala, tipo = tipo, posicao = new Vector3(x, 0, 0), grupo = grupo, disponivel = true, valeNaFase = true,
    };

    [Test]
    public void NuncaEscolheSalaProtegidaNemAsDuasPontasDoCircuito()
    {
        var lista = new List<DecisoesDeDaniel.Candidato>
        {
            C("Autopsia", TipoDeAcao.Presenca, 10),
            C("WC", TipoDeAcao.TrancarPorta, 12, "circuito"),
            C("Tecnica", TipoDeAcao.Apagao, 12),
        };
        var protegidas = new HashSet<string> { "Autopsia" };
        var trancados = new HashSet<string> { "circuito" };
        for (int i = 0; i < 20; i++)
            Assert.That(DecisoesDeDaniel.Escolher(lista, protegidas, Vector3.zero, trancados, () => i / 20f), Is.EqualTo(-1));
        trancados.Clear();
        Assert.That(DecisoesDeDaniel.Escolher(lista, protegidas, Vector3.zero, trancados, () => 0.5f), Is.EqualTo(1));
    }

    // ------------------------------------------------------------------ dados combinados

    [Test]
    public void RegistroDoSensorMostraDanielSemSaida()
    {
        TextAsset t = Resources.Load<TextAsset>("Historia/registro_sensor");
        Assert.That(t, Is.Not.Null);
        var d = JsonUtility.FromJson<ComputadorRecepcao.DadosDeAcesso>(t.text);
        var daniel = d.registros.Where(r => r.cracha == "LV-12").ToList();
        Assert.That(daniel.Count, Is.EqualTo(1));
        Assert.That(daniel[0].hora, Is.EqualTo("23:04"));
        Assert.That(daniel[0].evento, Is.EqualTo("Entrada"));
    }

    [Test]
    public void GravacaoTemFalasEmOrdem()
    {
        Gravacao g = Gravacao.Carregar();
        Assert.That(g.falas.Length, Is.GreaterThan(0));
        for (int i = 1; i < g.falas.Length; i++) Assert.That(g.falas[i].inicio, Is.GreaterThanOrEqualTo(g.falas[i - 1].fim));
    }

    [Test]
    public void EvidenciasDosPrefabsExistemNoCatalogo()
    {
        var problemas = new List<string>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string caminho = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (go == null) continue;
            foreach (var o in go.GetComponentsInChildren<ObjetoInspecionavel>(true))
                if (!string.IsNullOrEmpty(o.idEvidencia) && Caderno.Catalogo.Buscar(o.idEvidencia) == null)
                    problemas.Add(caminho + ": " + o.idEvidencia);
            foreach (var a in go.GetComponentsInChildren<AlvoFotografico>(true))
                if (!string.IsNullOrEmpty(a.idEvidencia) && Caderno.Catalogo.Buscar(a.idEvidencia) == null)
                    problemas.Add(caminho + ": " + a.idEvidencia);
        }
        Assert.That(problemas, Is.Empty);
    }
}
