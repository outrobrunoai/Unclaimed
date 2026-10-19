using UnityEngine;

/// <summary>
/// Ferrolho no lado de dentro de uma porta trancada: ao interagir, recolhe o trinco e destrava a folha
/// de vez (ela nunca volta a trancar). Do lado de fora, quem responde é a própria FolhaDePorta trancada.
/// </summary>
public class FerrolhoDePorta : AlvoDeInteracao
{
    public FolhaDePorta porta;
    [Tooltip("Parte móvel do ferrolho.")] public Transform trinco;
    [Tooltip("Deslocamento local do trinco ao recolher.")] public Vector3 deslocamento = new Vector3(0f, 0f, -0.1f);
    [Min(0.05f)] public float duracao = 0.35f;
    [Min(0.2f)] public float alcance = 1.8f;
    public string tecla = "E";
    public string textoAbrir = "Abrir ferrolho";
    public string avisoAberto = "Você abriu o ferrolho.";
    public AudioSource fonte;
    public AudioClip somAbrir;

    bool liberado;
    float progresso;
    Vector3 origem;

    public bool Liberado => liberado;
    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => liberado ? "" : textoAbrir;

    void Awake()
    {
        if (trinco != null) origem = trinco.localPosition;
    }

    public override void Interagir()
    {
        if (liberado) return;
        liberado = true;
        if (porta != null) porta.Destravar();
        if (fonte != null && somAbrir != null) fonte.PlayOneShot(somAbrir);
        MiraDeInteracao.Avisar(avisoAberto);
        // Sem collider, a mira volta a achar a folha por trás e a abre/fecha normalmente.
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    protected override void Update()
    {
        base.Update();
        if (!liberado || trinco == null || progresso >= 1f) return;
        progresso = Mathf.Min(1f, progresso + Time.deltaTime / duracao);
        trinco.localPosition = origem + deslocamento * Mathf.SmoothStep(0f, 1f, progresso);
    }
}
