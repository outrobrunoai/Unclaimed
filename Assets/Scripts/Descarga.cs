using UnityEngine;

/// <summary>
/// Descarga de caixa acoplada: E aperta o botão (afunda, segura um instante e volta) e roda um ciclo curto
/// (esvaziar e encher a caixa). Durante o ciclo não há indicação nem nova ação, então apertar E várias vezes
/// não acumula ciclos. A bacia fica tampada no modelo, por isso não há água visível para animar.
/// Fica na raiz do vaso: mirar o vaso ou a caixa mostra "Dar descarga".
/// </summary>
[DisallowMultipleComponent]
public class Descarga : AlvoDeInteracao
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.8f;
    public string texto = "Dar descarga";

    [Header("Botão")]
    public Transform botao;
    public Vector3 cursoBotao = new Vector3(0f, -0.004f, 0f);

    [Header("Ciclo")]
    [Tooltip("Tempo (s) até poder acionar de novo: esvaziar e reencher a caixa.")]
    [Min(0.5f)] public float duracaoCiclo = 6f;

    [Header("Som (opcional)")]
    public AudioSource fonte;
    public AudioClip somBotao, somDescarga;

    public bool EmCiclo => tempo < duracaoCiclo;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => EmCiclo ? "" : texto;
    public override void Interagir() => Acionar();

    float tempo = float.MaxValue;
    Vector3 botaoRepouso;

    void Awake()
    {
        if (botao != null) botaoRepouso = botao.localPosition;
    }

    public void Acionar()
    {
        if (EmCiclo) return;
        tempo = 0f;
        if (fonte != null)
        {
            if (somBotao != null) fonte.PlayOneShot(somBotao);
            if (somDescarga != null) { fonte.clip = somDescarga; fonte.Play(); }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!EmCiclo) return;
        tempo += Time.deltaTime;
        if (botao == null) return;
        // 0,1 s afundando, 0,25 s segurado, 0,35 s voltando.
        float k = tempo < 0.1f ? Mathf.SmoothStep(0f, 1f, tempo / 0.1f)
                : tempo < 0.35f ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (tempo - 0.35f) / 0.35f);
        botao.localPosition = botaoRepouso + cursoBotao * k;
    }
}
