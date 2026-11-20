using UnityEngine;

/// <summary>
/// Objeto "Sistemas" da cena Jogabilidade. Ao abrir a cena, zera o estado que vive só na memória (energia,
/// fila de envio, fotos de outra partida); o save restaura o resto logo depois. Grava os checkpoints:
///   CP01 ao pegar a chave da circulação, CP02 ao confirmar a troca ("con:troca"),
///   CP03 ao ler onde o gravador foi guardado (D09), CP04 ao pegar o gravador.
/// O checkpoint usa o save único (Salvamento) e espera o jogador sair de qualquer tela antes de gravar.
/// </summary>
public class SistemasDaNoite : MonoBehaviour
{
    public bool salvarCheckpoints = true;

    static readonly (string id, string fato)[] Checkpoints =
    {
        ("CP01", "chave:chave_circulacao"),
        ("CP02", "con:troca"),
        ("CP03", "ev:D09"),
        ("CP04", "gravador_pego"),
    };

    void Awake()
    {
        RedeEletrica.Padrao();
        CelularLogica.Limpar();
    }

    void Start() => AlbumDeFotos.SincronizarComFatos(true);

    void Update()
    {
        if (!salvarCheckpoints || TelaDeFinal.Ativa || Jogador.Ocupado || Jogador.Movimento == null) return;
        if (Esconderijo.JogadorEscondido) return;
        foreach (var (id, fato) in Checkpoints)
        {
            if (EstadoDaNoite.Tem("cp:" + id) || !Tem(fato)) continue;
            EstadoDaNoite.Marcar("cp:" + id);
            if (Salvamento.Salvar()) MiraDeInteracao.Avisar("Progresso salvo.", 1.5f);
            return;
        }
    }

    static bool Tem(string fato) =>
        fato.StartsWith("chave:") ? Chaveiro.Tem(fato.Substring(6)) : EstadoDaNoite.Tem(fato);
}
