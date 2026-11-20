using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Sons ligados em tempo de jogo pelo SonsDoMapa (Resources/Sons, gerados por montar_sons.py).</summary>
public class SonsTestes
{
    const string Cena = "Assets/Scenes/Testes/Jose.unity";

    static readonly string[] Slots =
    {
        "interno", "concreto", "asfalto_molhado", "grama_molhada", "metal",
        "comum_abrir", "comum_fechou", "tecnica_abrir", "tecnica_fechou", "frigorifica_abrir", "frigorifica_fechou",
        "vidro_abrir", "vidro_fechou", "vaivem_abrir", "vaivem_fechar", "destrancar", "ferrolho", "trancada", "batida_forte",
        "gaveta_mesa_abrir", "gaveta_mesa_fechar", "arquivo_abrir", "arquivo_fechar", "locker_abrir", "locker_fechou",
        "madeira_abrir", "madeira_fechou", "camara_porta_abrir", "camara_porta_fechou", "maca_abrir", "maca_fechar", "maca_fechou",
        "cadeado_disco", "cadeado_abrir", "cadeado_erro", "registro", "agua_loop", "descarga", "pegar_chave", "pegar_objeto",
        "encaixe", "disjuntor", "desarme", "apagao", "chave", "recusado", "zumbido_loop", "compressor_loop",
        "mensagem", "enviado", "puxar", "guardar", "lanterna", "obturador",
        "passos", "passos_corredor", "corrente", "trava", "esconderijo_entrar", "esconderijo_sair", "gravador_pegar",
        "copia_loop", "copia_pronto", "captura",
    };

    [Test]
    public void TodosOsSlotsTemSom()
    {
        foreach (string slot in Slots)
            Assert.That(SonsDoMapa.Variacoes(slot).Length, Is.GreaterThan(0), "Falta som para " + slot);
        Assert.That(SonsDoMapa.Variacoes("interno").Length, Is.GreaterThanOrEqualTo(8), "Passos precisam de variações.");
        Assert.That(SonsDoMapa.Variacoes("passos").Length, Is.GreaterThan(0));
        Assert.That(SonsDoMapa.Variacoes("passos")[0].name, Does.Not.Contain("corredor"), "Slots com prefixo parecido não se misturam.");
    }

    [UnityTest]
    public IEnumerator SonsSaoLigadosNoMapa()
    {
        if (!System.IO.File.Exists(Cena)) Assert.Ignore("Crie a cena com Tools > Unclaimed > José > Criar cena de testes.");
        EditorSceneManager.OpenScene(Cena);
        yield return new EnterPlayMode();
        for (int i = 0; i < 300 && SonsDoMapa.Instancia == null; i++) yield return null;
        yield return null;
        var sons = SonsDoMapa.Instancia;
        Assert.That(sons, Is.Not.Null, "O SonsDoMapa se instala sozinho no Play.");
        var sistemas = Object.FindAnyObjectByType<SistemasDaNoite>();
        if (sistemas != null) sistemas.salvarCheckpoints = false;

        // Móveis de mentira, criados depois do carregamento: Aplicar de novo os pega.
        var arquivo = new GameObject("Arquivo de aco").transform;
        var gaveta = Novo<PecaDeMovel>("Gaveta 1", arquivo);
        var tecnica = new GameObject("Porta tecnica").transform;
        var folha = Novo<FolhaDePorta>("Folha", tecnica);
        var manual = Novo<FolhaDePorta>("Porta com som próprio", null);
        var meu = AudioClip.Create("meu", 100, 1, 48000, false);
        manual.somAbrir = meu;
        sons.Aplicar();

        Assert.That(gaveta.somAbrir.name, Does.StartWith("arquivo_abrir"));
        Assert.That(gaveta.somFechar.name, Does.StartWith("arquivo_fechar"));
        Assert.That(gaveta.fonte, Is.Not.Null);
        Assert.That(folha.somAbrir.name, Does.StartWith("tecnica_abrir"));
        Assert.That(folha.somFechou.name, Does.StartWith("tecnica_fechou"));
        Assert.That(folha.somTrancada, Is.Not.Null);
        Assert.That(manual.somAbrir, Is.SameAs(meu), "Som posto à mão não é trocado.");

        // A porta da câmara fria da cena de testes é frigorífica e toca ao abrir, em 3D.
        FolhaDePorta porta = null;
        foreach (var p in Object.FindObjectsByType<FolhaDePorta>()) if (p.name == "Porta da camara fria") porta = p;
        Assert.That(porta, Is.Not.Null);
        Assert.That(porta.somAbrir.name, Does.StartWith("frigorifica_abrir"));
        Assert.That(porta.fonte.spatialBlend, Is.EqualTo(1f));
        porta.Abrir();
        yield return null;
        Assert.That(porta.fonte.isPlaying, Is.True);

        // Passos no jogador, com as superfícies de fora.
        var passos = Object.FindAnyObjectByType<PassosDoJogador>();
        Assert.That(passos, Is.Not.Null, "O jogador ganha o PassosDoJogador.");
        Assert.That(passos.padrao.sons.Length, Is.GreaterThanOrEqualTo(8));
        Assert.That(passos.superficies.Exists(s => System.Array.IndexOf(s.nomes, "Asfalto") >= 0), Is.True);
        Assert.That(passos.superficies.Exists(s => System.Array.IndexOf(s.nomes, "Grama") >= 0), Is.True);

        // Celular e câmera.
        var celular = Object.FindAnyObjectByType<Celular>();
        if (celular != null) Assert.That(celular.somMensagem, Is.Not.Null);

        // Apagão: tirar a luz geral toca o som de queda de energia.
        RedeEletrica.Padrao();
        yield return Esperar(0.2f);   // o SonsDoMapa precisa ver a luz ligada antes de ela cair
        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        yield return Esperar(0.2f);
        var global = sons.GetComponent<AudioSource>();
        Assert.That(global.isPlaying, Is.True, "Som do apagão.");

        yield return new ExitPlayMode();
        RedeEletrica.Padrao();
        EstadoDaNoite.Limpar();
    }

    static IEnumerator Esperar(float segundos)
    {
        float fim = Time.realtimeSinceStartup + segundos;
        while (Time.realtimeSinceStartup < fim) yield return null;
    }

    static T Novo<T>(string nome, Transform pai) where T : Component
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
        return go.AddComponent<T>();
    }
}
