using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// O celular 3D na cena de testes: sai do bolso, a tela aparece na frente do jogador, o app Câmera tira uma foto
/// pelo visor (com imagem) e o celular volta para o bolso.
/// </summary>
public class CelularNaCenaTestes
{
    const string Cena = "Assets/Scenes/Testes/Jose.unity";

    static IEnumerator Esperar(float segundos)
    {
        float fim = Time.realtimeSinceStartup + segundos;
        while (Time.realtimeSinceStartup < fim) yield return null;
    }

    [UnityTest]
    public IEnumerator PuxarFotografarEGuardar()
    {
        if (!System.IO.File.Exists(Cena)) Assert.Ignore("Crie a cena com Tools > Unclaimed > José > Criar cena de testes.");
        EditorSceneManager.OpenScene(Cena);
        yield return new EnterPlayMode();

        Celular celular = null;
        for (int i = 0; i < 300 && (celular = Object.FindAnyObjectByType<Celular>()) == null; i++) yield return null;
        Assert.That(celular, Is.Not.Null);
        Object.FindAnyObjectByType<SistemasDaNoite>().salvarCheckpoints = false;
        yield return null;
        Assert.That(celular.NaMao, Is.Not.Null, "O Celular está sem o prefab Celular na mao.");
        Assert.That(celular.NaMao.Visivel, Is.False);

        celular.AbrirInterface();
        yield return Esperar(0.6f);
        Assert.That(celular.EstaAberta, Is.True);
        Assert.That(celular.NaMao.Atual, Is.EqualTo(CelularNaMao.Pose.Mao));
        // O celular não pausa: o jogador continua andando e Daniel continua agindo.
        var movimento = Object.FindAnyObjectByType<Movimento>();
        Assert.That(movimento.enabled, Is.True);
        Assert.That(movimento.ComCelularNaMao, Is.True);
        Assert.That(Jogador.Ocupado, Is.False);
        Assert.That(celular.NaMao.TexturaDaInterface, Is.Not.Null, "A interface devia estar na tela 3D.");
        Rect r = celular.NaMao.RetanguloNaTela();
        // O mouse no meio da tela do celular cai no meio da interface (Rect de GUI tem y para baixo).
        var tela = celular.NaMao.GetComponent<CelularTela>();
        Assert.That(tela.PontoNaTela(new Vector2(r.center.x, Screen.height - r.center.y), out Vector2 px), Is.True);
        Assert.That(px.x / celular.NaMao.TexturaDaInterface.width, Is.EqualTo(0.5f).Within(0.05f));
        Assert.That(px.y / celular.NaMao.TexturaDaInterface.height, Is.EqualTo(0.5f).Within(0.05f));
        Assert.That(r.height, Is.GreaterThan(Screen.height * 0.5f), $"Tela do celular pequena demais: {r}");
        Assert.That(r.center.x, Is.GreaterThan(Screen.width * 0.5f), "O celular devia ficar à direita.");
        Assert.That(r.height, Is.GreaterThan(r.width), "Na mão o celular fica em pé.");

        celular.AbrirCamera();
        yield return Esperar(0.6f);
        Assert.That(celular.EstaAberta, Is.False);
        Assert.That(celular.ModoCamera, Is.True);
        Rect visor = celular.NaMao.RetanguloNaTela();
        Assert.That(visor.width, Is.GreaterThan(visor.height), "No modo câmera o celular fica deitado.");
        Assert.That(visor.width, Is.InRange(Screen.width * 0.6f, Screen.width * 0.8f), "No modo câmera o celular ocupa uns 70% da tela.");
        int antes = AlbumDeFotos.Fotos.Count;
        Assert.That(celular.TirarFoto(), Is.True);
        Assert.That(AlbumDeFotos.Fotos.Count, Is.EqualTo(antes + 1));
        Assert.That(AlbumDeFotos.Fotos[AlbumDeFotos.Fotos.Count - 1].imagem, Is.Not.Null);
        Assert.That(CameraFotografica.UltimoFlash, Is.GreaterThan(0f));

        // Galeria: a foto aberta deita o celular grande, e voltar o põe em pé de novo.
        celular.AbrirInterface();
        yield return Esperar(0.6f);
        Assert.That(celular.ModoCamera, Is.False);
        celular.AbrirFoto(AlbumDeFotos.Fotos.Count - 1);
        yield return Esperar(0.6f);
        Assert.That(celular.NaMao.Atual, Is.EqualTo(CelularNaMao.Pose.Foto));
        Assert.That(celular.NaMao.RetanguloNaTela().width, Is.GreaterThan(Screen.width * 0.6f));

        // Guardar pelo caminho normal (T/Esc) e puxar de novo: era aqui que o jogo travava.
        celular.Guardar();
        yield return Esperar(0.8f);
        Assert.That(celular.EstaAberta, Is.False);
        Assert.That(InterfaceDeTela.AlgumaAberta, Is.False);
        Assert.That(Object.FindAnyObjectByType<Movimento>().enabled, Is.True, "O jogador devia voltar a andar.");
        celular.AbrirInterface();
        yield return Esperar(0.6f);
        Assert.That(celular.EstaAberta, Is.True);
        Assert.That(celular.NaMao.Atual, Is.EqualTo(CelularNaMao.Pose.Mao));
        celular.FecharInterface();
        yield return Esperar(0.6f);
        Assert.That(celular.NaMao.Visivel, Is.False, "Fechado, o celular volta para o bolso.");

        yield return new ExitPlayMode();
        EstadoDaNoite.Limpar();
    }

    [UnityTest]
    public IEnumerator LanternaAcendeNaMaoEApagaNoBolso()
    {
        if (!System.IO.File.Exists(Cena)) Assert.Ignore("Crie a cena com Tools > Unclaimed > José > Criar cena de testes.");
        EditorSceneManager.OpenScene(Cena);
        yield return new EnterPlayMode();
        Celular celular = null;
        for (int i = 0; i < 300 && (celular = Object.FindAnyObjectByType<Celular>()) == null; i++) yield return null;
        Object.FindAnyObjectByType<SistemasDaNoite>().salvarCheckpoints = false;
        yield return null;

        celular.intensidadeLanterna = 7.5f;
        celular.DefinirLanterna(true);
        yield return Esperar(0.6f);
        Assert.That(Celular.LanternaLigada, Is.True);
        Assert.That(celular.NaMao.Atual, Is.EqualTo(CelularNaMao.Pose.Lanterna));
        Light luz = celular.NaMao.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "Lanterna do celular");
        Assert.That(luz, Is.Not.Null);
        Assert.That(luz.enabled, Is.True);
        Assert.That(luz.intensity, Is.EqualTo(7.5f));
        Assert.That(Vector3.Dot(luz.transform.forward, Camera.main.transform.forward), Is.GreaterThan(0.8f), "O facho devia ir para a frente.");

        // Abrir e guardar o celular com a lanterna acesa volta para a pose de lanterna, não para o bolso.
        celular.AbrirInterface();
        yield return Esperar(0.6f);
        celular.FecharInterface();
        yield return Esperar(0.6f);
        Assert.That(celular.NaMao.Atual, Is.EqualTo(CelularNaMao.Pose.Lanterna));

        celular.DefinirLanterna(false);
        yield return Esperar(0.6f);
        Assert.That(Celular.LanternaLigada, Is.False);
        Assert.That(celular.NaMao.Visivel, Is.False);

        yield return new ExitPlayMode();
        EstadoDaNoite.Limpar();
    }

    /// <summary>Brilho médio do lado esquerdo da imagem (longe do celular), em várias renderizações (o VHS tem granulado).</summary>
    static float BrilhoLongeDoCelular()
    {
        Camera cam = Camera.main;
        var rt = new RenderTexture(640, 360, 24);
        var t = new Texture2D(640, 360, TextureFormat.RGB24, false);
        float soma = 0f;
        int n = 0;
        for (int k = 0; k < 6; k++)
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            t.Apply();
            RenderTexture.active = null;
            for (int x = 60; x < 280; x += 4)
                for (int y = 80; y < 280; y += 4) { soma += t.GetPixel(x, y).grayscale; n++; }
        }
        Object.Destroy(rt);
        Object.Destroy(t);
        return soma / n;
    }

    [UnityTest]
    public IEnumerator PuxarOCelularNaoMudaOBrilhoDaCena()
    {
        if (!System.IO.File.Exists(Cena)) Assert.Ignore("Crie a cena com Tools > Unclaimed > José > Criar cena de testes.");
        EditorSceneManager.OpenScene(Cena);
        yield return new EnterPlayMode();
        Celular celular = null;
        for (int i = 0; i < 300 && (celular = Object.FindAnyObjectByType<Celular>()) == null; i++) yield return null;
        Object.FindAnyObjectByType<SistemasDaNoite>().salvarCheckpoints = false;
        yield return Esperar(1f);

        float guardado = BrilhoLongeDoCelular();
        celular.AbrirInterface();
        yield return Esperar(0.8f);
        float naMao = BrilhoLongeDoCelular();
        Assert.That(naMao, Is.EqualTo(guardado).Within(guardado * 0.015f + 0.002f),
            $"O brilho da cena mudou ao puxar o celular: {guardado:F3} -> {naMao:F3}.");

        yield return new ExitPlayMode();
        EstadoDaNoite.Limpar();
    }
}
