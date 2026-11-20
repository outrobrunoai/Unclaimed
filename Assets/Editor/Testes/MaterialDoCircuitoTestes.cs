using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Testes de modo de edição do componente MaterialDoCircuito.</summary>
public class MaterialDoCircuitoTestes
{
    static readonly MethodInfo MetodoOnEnable =
        typeof(MaterialDoCircuito).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo MetodoOnDisable =
        typeof(MaterialDoCircuito).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly List<GameObject> objetos = new List<GameObject>();
    readonly List<Material> materiais = new List<Material>();
    Material aceso, apagado, outro;

    [SetUp]
    public void Preparar()
    {
        RedeEletrica.Padrao();
        EstadoDaNoite.Limpar();
        aceso = CriarMaterial("Aceso");
        apagado = CriarMaterial("Apagado");
        outro = CriarMaterial("Outro");
    }

    [TearDown]
    public void Limpar()
    {
        foreach (GameObject go in objetos)
        {
            if (go == null) continue;
            foreach (MaterialDoCircuito mc in go.GetComponentsInChildren<MaterialDoCircuito>(true))
                MetodoOnDisable?.Invoke(mc, null);
            UnityEngine.Object.DestroyImmediate(go);
        }
        objetos.Clear();
        foreach (Material mat in materiais)
            if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
        materiais.Clear();
        RedeEletrica.Padrao();
        EstadoDaNoite.Limpar();
    }

    Material CriarMaterial(string nome)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
        var mat = new Material(shader) { name = nome };
        materiais.Add(mat);
        return mat;
    }

    GameObject CriarObjeto(string nome, bool ativo = true)
    {
        var go = new GameObject(nome);
        go.SetActive(ativo);
        objetos.Add(go);
        return go;
    }

    void Ativar(GameObject go, MaterialDoCircuito mc)
    {
        go.SetActive(true);
        MetodoOnEnable?.Invoke(mc, null);
    }

    MaterialDoCircuito.Troca[] TrocaPadrao() =>
        new[] { new MaterialDoCircuito.Troca { ligado = aceso, desligado = apagado } };

    [Test]
    public void ApagaoTrocaApenasMaterialConfiguradoEVoltaComFusivel()
    {
        GameObject go = CriarObjeto("Luminaria", false);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { aceso, outro };
        MaterialDoCircuito mc = go.AddComponent<MaterialDoCircuito>();
        mc.circuito = RedeEletrica.Geral;
        mc.trocas = TrocaPadrao();
        Ativar(go, mc);

        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { aceso, outro }));

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));

        RedeEletrica.ColocarFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { aceso, outro }));
    }

    [Test]
    public void SlotQueJaComecaApagadoContinuaApagadoQuandoALuzVolta()
    {
        GameObject go = CriarObjeto("LuminariaComSlotApagado", false);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { apagado, aceso };
        MaterialDoCircuito mc = go.AddComponent<MaterialDoCircuito>();
        mc.circuito = RedeEletrica.Geral;
        mc.trocas = TrocaPadrao();
        Ativar(go, mc);

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, apagado }));

        RedeEletrica.ColocarFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, aceso }));
    }

    [Test]
    public void InverterAplicaTrocaComLuzLigadaEVoltaAoOriginalNoApagao()
    {
        GameObject go = CriarObjeto("LedEmergencia", false);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { aceso, outro };
        MaterialDoCircuito mc = go.AddComponent<MaterialDoCircuito>();
        mc.circuito = RedeEletrica.Geral;
        mc.inverter = true;
        mc.trocas = TrocaPadrao();
        Ativar(go, mc);

        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { aceso, outro }));

        RedeEletrica.ColocarFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));
    }

    [Test]
    public void MudancaEmOutroCircuitoNaoAlteraComponenteDoCircuitoGeral()
    {
        GameObject go = CriarObjeto("LuminariaGeral", false);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { aceso, outro };
        MaterialDoCircuito mc = go.AddComponent<MaterialDoCircuito>();
        mc.circuito = RedeEletrica.Geral;
        mc.trocas = TrocaPadrao();
        Ativar(go, mc);

        r.sharedMaterials = new[] { outro, outro };
        RedeEletrica.RemoverFusivel(RedeEletrica.Exame);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { outro, outro }));
        RedeEletrica.ColocarFusivel(RedeEletrica.Exame);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { outro, outro }));

        mc.Aplicar();
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { aceso, outro }));

        RedeEletrica.Definir(RedeEletrica.Geral, false);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));

        RedeEletrica.Definir(RedeEletrica.Exame, true);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));

        RedeEletrica.Definir(RedeEletrica.Exame, false);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));
    }

    [Test]
    public void RenderersVazioIncluiFilhosInclusiveInativo()
    {
        GameObject pai = CriarObjeto("LuminariaComFilhos", false);

        GameObject filhoAtivo = new GameObject("FilhoAtivo");
        filhoAtivo.transform.SetParent(pai.transform);
        MeshRenderer rAtivo = filhoAtivo.AddComponent<MeshRenderer>();
        rAtivo.sharedMaterials = new[] { aceso };

        GameObject filhoInativo = new GameObject("FilhoInativo");
        filhoInativo.transform.SetParent(pai.transform);
        MeshRenderer rInativo = filhoInativo.AddComponent<MeshRenderer>();
        rInativo.sharedMaterials = new[] { aceso, outro };
        filhoInativo.SetActive(false);

        MaterialDoCircuito mc = pai.AddComponent<MaterialDoCircuito>();
        mc.circuito = RedeEletrica.Geral;
        mc.trocas = TrocaPadrao();
        Ativar(pai, mc);

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        Assert.That(rAtivo.sharedMaterials, Is.EqualTo(new[] { apagado }));
        Assert.That(rInativo.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));

        RedeEletrica.ColocarFusivel(RedeEletrica.Geral);
        Assert.That(rAtivo.sharedMaterials, Is.EqualTo(new[] { aceso }));
        Assert.That(rInativo.sharedMaterials, Is.EqualTo(new[] { aceso, outro }));
    }

    [Test]
    public void IgnoraRenderersNulosETrocasComLigadoNulo()
    {
        GameObject go = CriarObjeto("LuminariaComNulos", false);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { aceso, outro };
        MaterialDoCircuito mc = go.AddComponent<MaterialDoCircuito>();
        mc.circuito = RedeEletrica.Geral;
        mc.renderers = new Renderer[] { null, r };
        mc.trocas = new[]
        {
            null,
            new MaterialDoCircuito.Troca { ligado = null, desligado = outro },
            new MaterialDoCircuito.Troca { ligado = aceso, desligado = apagado },
        };
        Ativar(go, mc);

        RedeEletrica.RemoverFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { apagado, outro }));

        RedeEletrica.ColocarFusivel(RedeEletrica.Geral);
        Assert.That(r.sharedMaterials, Is.EqualTo(new[] { aceso, outro }));
    }
}
