using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição para o comportamento sem flash (lanterna e RevelarComFlash).</summary>
public class SemFlashTestes
{
    readonly List<GameObject> criados = new List<GameObject>();

    [SetUp]
    public void Preparar() => EstadoDaNoite.Limpar();

    [TearDown]
    public void Limpar()
    {
        foreach (GameObject go in criados)
            if (go != null) Object.DestroyImmediate(go);
        criados.Clear();
        EstadoDaNoite.Limpar();
    }

    GameObject Criar(string nome)
    {
        var go = new GameObject(nome);
        criados.Add(go);
        return go;
    }

    [Test]
    public void LanternaIluminaSemInstanciaDevolveFalse()
    {
        Assert.That(Celular.LanternaLigada, Is.False);
        Assert.That(Celular.LanternaIlumina(Vector3.zero, 6f), Is.False);
        Assert.That(Celular.LanternaIlumina(Vector3.forward * 2f, 10f), Is.False);
    }

    [Test]
    public void RevelarComFlashSemLanternaDesativaObjeto()
    {
        GameObject controlador = Criar("ControladorRevelar");
        GameObject escondido = Criar("ObjetoEscondido");
        escondido.SetActive(true);

        RevelarComFlash revelar = controlador.AddComponent<RevelarComFlash>();
        revelar.objetos = new[] { escondido };
        revelar.fatoAoRevelar = "teste_revelar_sem_lanterna";

        revelar.Avaliar();

        Assert.That(escondido.activeSelf, Is.False);
        Assert.That(EstadoDaNoite.Tem("teste_revelar_sem_lanterna"), Is.False);
    }
}
