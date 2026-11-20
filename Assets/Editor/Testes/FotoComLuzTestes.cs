using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição da exigência de luz para fotografar evidências (P02 / F01).</summary>
public class FotoComLuzTestes
{
    static readonly MethodInfo MetodoOnEnable =
        typeof(AlvoFotografico).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo MetodoOnDisable =
        typeof(AlvoFotografico).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly List<GameObject> objetos = new List<GameObject>();
    readonly List<Camera> camerasDesabilitadas = new List<Camera>();
    Action<AlvoFotografico> aoFotografar;

    [SetUp]
    public void Preparar()
    {
        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
        RedeEletrica.Padrao();
        AlbumDeFotos.Limpar();
    }

    [TearDown]
    public void Limpar()
    {
        if (aoFotografar != null) CameraFotografica.AoFotografar -= aoFotografar;
        aoFotografar = null;

        foreach (GameObject go in objetos)
        {
            if (go == null) continue;
            foreach (AlvoFotografico a in go.GetComponentsInChildren<AlvoFotografico>(true))
                MetodoOnDisable?.Invoke(a, null);
            UnityEngine.Object.DestroyImmediate(go);
        }
        objetos.Clear();

        foreach (Camera cam in camerasDesabilitadas)
            if (cam != null) cam.enabled = true;
        camerasDesabilitadas.Clear();

        EstadoDaNoite.Limpar();
        Caderno.Catalogo = null;
        RedeEletrica.Padrao();
        AlbumDeFotos.Limpar();
    }

    GameObject CriarObjeto(string nome)
    {
        var go = new GameObject(nome);
        objetos.Add(go);
        return go;
    }

    [Test]
    public void TemLuzDependeDoCircuitoConfigurado()
    {
        GameObject go = CriarObjeto("AlvoMesa");
        AlvoFotografico alvo = go.AddComponent<AlvoFotografico>();

        alvo.requerCircuito = "";
        Assert.That(alvo.TemLuz, Is.True);
        Assert.That(CameraFotografica.ValeComoEvidencia(alvo), Is.True);
        Assert.That(CameraFotografica.ValeComoEvidencia(null), Is.False);

        alvo.requerCircuito = RedeEletrica.Exame;
        Assert.That(alvo.TemLuz, Is.False);
        Assert.That(CameraFotografica.ValeComoEvidencia(alvo), Is.False);

        Assert.That(RedeEletrica.Definir(RedeEletrica.Geral, false), Is.EqualTo(ResultadoDoCircuito.Ok));
        Assert.That(RedeEletrica.Definir(RedeEletrica.Exame, true), Is.EqualTo(ResultadoDoCircuito.Ok));
        Assert.That(alvo.TemLuz, Is.True);
        Assert.That(CameraFotografica.ValeComoEvidencia(alvo), Is.True);
    }

    [Test]
    public void FotografarSemLuzSalvaFotoComumENaoColetaEvidenciaAteAcenderExame()
    {
        // Isola Camera.main de qualquer câmera já aberta na cena do Editor.
        while (Camera.main != null)
        {
            Camera existente = Camera.main;
            existente.enabled = false;
            camerasDesabilitadas.Add(existente);
        }

        Vector3 origem = new Vector3(5000f, 5000f, 5000f);

        GameObject camGo = CriarObjeto("CameraTeste");
        camGo.tag = "MainCamera";
        camGo.transform.position = origem;
        camGo.transform.rotation = Quaternion.identity;
        camGo.AddComponent<Camera>();

        GameObject alvoGo = CriarObjeto("AlvoMesa");
        alvoGo.transform.position = origem + Vector3.forward * 2f;
        GameObject centroGo = CriarObjeto("CentroMesa");
        centroGo.transform.SetParent(alvoGo.transform, false);
        AlvoFotografico alvo = alvoGo.AddComponent<AlvoFotografico>();
        alvo.id = "mesa_autopsia";
        alvo.idEvidencia = "F01";
        alvo.centro = centroGo.transform;
        alvo.requerCircuito = RedeEletrica.Exame;
        MetodoOnEnable?.Invoke(alvo, null);

        GameObject fotoGo = CriarObjeto("CameraFotograficaTeste");
        CameraFotografica cameraFoto = fotoGo.AddComponent<CameraFotografica>();

        int chamadas = 0;
        AlvoFotografico ultimoEvento = alvo;
        aoFotografar = a => { chamadas++; ultimoEvento = a; };
        CameraFotografica.AoFotografar += aoFotografar;

        // Com a luz de exame desligada: foto comum no álbum, sem evidência e retorno null.
        AlvoFotografico resultadoEscuro = cameraFoto.Fotografar(null);
        Assert.That(resultadoEscuro, Is.Null);
        Assert.That(chamadas, Is.EqualTo(1));
        Assert.That(ultimoEvento, Is.Null);
        Assert.That(Caderno.Coletou("F01"), Is.False);
        Assert.That(AlbumDeFotos.Fotos.Count, Is.EqualTo(1));
        Assert.That(AlbumDeFotos.Fotos[0].alvo, Is.Empty);
        Assert.That(AlbumDeFotos.Fotos[0].legenda, Is.EqualTo("Foto escura"));

        // Desliga a geral e liga o exame: a foto passa a valer e coleta F01.
        Assert.That(RedeEletrica.Definir(RedeEletrica.Geral, false), Is.EqualTo(ResultadoDoCircuito.Ok));
        Assert.That(RedeEletrica.Definir(RedeEletrica.Exame, true), Is.EqualTo(ResultadoDoCircuito.Ok));

        AlvoFotografico resultadoClaro = cameraFoto.Fotografar(null);
        Assert.That(resultadoClaro, Is.SameAs(alvo));
        Assert.That(chamadas, Is.EqualTo(2));
        Assert.That(ultimoEvento, Is.SameAs(alvo));
        Assert.That(Caderno.Coletou("F01"), Is.True);
        Assert.That(AlbumDeFotos.Fotos.Count, Is.EqualTo(2));
        Assert.That(AlbumDeFotos.Fotos[1].alvo, Is.EqualTo("mesa_autopsia"));
    }
}
