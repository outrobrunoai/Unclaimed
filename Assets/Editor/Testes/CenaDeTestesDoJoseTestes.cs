using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Teste de ponta a ponta na cena de testes do José (Assets/Scenes/Testes/Jose.unity): entra em Play, espera a
/// Jogabilidade carregar e percorre a noite por fatos (Daniel sai da mesa, corrente, confronto, caça).
/// Não grava checkpoints (o save do jogador fica intacto).
/// </summary>
public class CenaDeTestesDoJoseTestes
{
    const string Cena = "Assets/Scenes/Testes/Jose.unity";

    [UnityTest]
    public IEnumerator NoiteInteiraPorFatosNaCenaDeTestes()
    {
        if (!System.IO.File.Exists(Cena)) Assert.Ignore("Crie a cena com Tools > Unclaimed > José > Criar cena de testes.");
        EditorSceneManager.OpenScene(Cena);
        yield return new EnterPlayMode();

        Movimento jogador = null;
        for (int i = 0; i < 300 && (jogador = Object.FindAnyObjectByType<Movimento>()) == null; i++) yield return null;
        Assert.That(jogador, Is.Not.Null, "A Jogabilidade não carregou o Player.");
        var sistemas = Object.FindAnyObjectByType<SistemasDaNoite>();
        var diretor = Object.FindAnyObjectByType<DiretorDeDaniel>();
        var confronto = Object.FindAnyObjectByType<Confronto>();
        Assert.That(sistemas, Is.Not.Null);
        Assert.That(diretor, Is.Not.Null);
        Assert.That(confronto, Is.Not.Null);
        Assert.That(DanielPerseguidor.Instancia, Is.Not.Null);
        sistemas.salvarCheckpoints = false;
        diretor.folgaDepoisDeLer = 0f;
        diretor.esperaDoBraco = 0f;
        confronto.espera = 0.2f;
        yield return null;

        EstadoDaNoite.Limpar();
        Assert.That(Sala.IdEm(jogador.transform.position), Is.EqualTo("Corredor"));
        Assert.That(Object.FindAnyObjectByType<CameraFotografica>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<Celular>(), Is.Not.Null);

        // Parte 2: com F01 e F02, Daniel larga a mesa (a autópsia não está à vista do corredor).
        EstadoDaNoite.Marcar("ev:F01");
        EstadoDaNoite.Marcar("ev:F02");
        for (int i = 0; i < 30 && !EstadoDaNoite.Tem("daniel_saiu"); i++) yield return null;
        Assert.That(EstadoDaNoite.Tem("daniel_saiu"), Is.True);
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Sombra));
        Assert.That(AlvoFotografico.Buscar("mesa_autopsia"), Is.Null, "A mesa devia estar vazia (P08).");

        // Parte 3: a troca confirmada leva à Obstrução, e a corrente vai para a entrada.
        EstadoDaNoite.Marcar("con:troca");
        for (int i = 0; i < 30 && !EstadoDaNoite.Tem("corrente"); i++) yield return null;
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Obstrucao));
        Assert.That(EstadoDaNoite.Tem("corrente"), Is.True);
        Assert.That(Object.FindAnyObjectByType<SaidaDoPredio>().Bloqueada || FindSaidaBloqueada(), Is.True);

        // Parte 4: o confronto aparece depois de pegar o gravador; ficar com ele começa a caça.
        EstadoDaNoite.Marcar("gravador_pego");
        float limite = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < limite && !confronto.EstaAberta) yield return null;
        Assert.That(confronto.EstaAberta, Is.True);
        var daniel = DanielPerseguidor.Instancia;
        var agente = daniel.GetComponent<NavMeshAgent>();
        Assert.That(agente.enabled && agente.isOnNavMesh, Is.True, "Daniel não apareceu sobre a NavMesh.");

        EstadoDaNoite.Marcar("confronto_feito");
        EstadoDaNoite.Marcar("caca");
        confronto.FecharInterface();
        daniel.ComecarCaca();
        float antes = Vector3.Distance(daniel.transform.position, jogador.transform.position);
        float fim = Time.time + 1.5f;
        while (Time.time < fim && !TelaDeFinal.Ativa) yield return null;
        Assert.That(EstadoDaNoite.Fase, Is.EqualTo(FaseDeDaniel.Caca));
        float depois = Vector3.Distance(daniel.transform.position, jogador.transform.position);
        Assert.That(depois, Is.LessThan(antes), "Daniel devia se aproximar do jogador na caça.");

        Time.timeScale = 1f;
        AudioListener.pause = false;
        yield return new ExitPlayMode();
        EstadoDaNoite.Limpar();
    }

    static bool FindSaidaBloqueada()
    {
        foreach (var s in Object.FindObjectsByType<SaidaDoPredio>()) if (s.Bloqueada) return true;
        return false;
    }
}
