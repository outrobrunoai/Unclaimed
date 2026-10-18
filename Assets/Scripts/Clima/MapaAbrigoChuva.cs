using UnityEngine;

public enum TipoSuperficie : byte { Nenhum, Duro, Terra, Vegetacao, Outro }

[CreateAssetMenu(menuName = "Unclaimed/Mapa de abrigo da chuva")]
public sealed class MapaAbrigoChuva : ScriptableObject
{
    public Vector2 origem;
    [Min(0.001f)] public float celula = 0.125f;
    public int largura, altura;
    public float[] alturas;
    public byte[] tipos;
    public byte[] exposicao;
    [Tooltip("Distância percorrida por dentro desde a abertura para o exterior (m); 0 fora do prédio.")]
    public float[] profundidadeInterna;
    public Texture2D textura;

    public Vector4 ParametrosUV => new Vector4(1f / Mathf.Max(celula * largura, 0.001f),
        1f / Mathf.Max(celula * altura, 0.001f), -origem.x / Mathf.Max(celula * largura, 0.001f),
        -origem.y / Mathf.Max(celula * altura, 0.001f));

    public bool Celula(Vector3 p, out int x, out int z)
    {
        x = z = -1;
        if (celula <= 0 || largura <= 0 || altura <= 0) return false;
        x = Mathf.FloorToInt((p.x - origem.x) / celula);
        z = Mathf.FloorToInt((p.z - origem.y) / celula);
        return x >= 0 && z >= 0 && x < largura && z < altura;
    }

    public Vector3 CentroCelula(int x, int z, float y) =>
        new Vector3(origem.x + (x + 0.5f) * celula, y, origem.y + (z + 0.5f) * celula);

    public float ProfundidadeNaCelula(int x, int z)
    {
        if (x < 0 || z < 0 || x >= largura || z >= altura || profundidadeInterna == null) return 0f;
        int i = z * largura + x;
        return i < profundidadeInterna.Length ? profundidadeInterna[i] : 0f;
    }

    bool Indice(Vector3 p, out int i)
    {
        i = -1;
        if (celula <= 0 || largura <= 0 || altura <= 0) return false;
        int x = Mathf.FloorToInt((p.x - origem.x) / celula);
        int z = Mathf.FloorToInt((p.z - origem.y) / celula);
        if (x < 0 || z < 0 || x >= largura || z >= altura) return false;
        i = z * largura + x;
        return alturas != null && tipos != null && i < alturas.Length && i < tipos.Length;
    }

    public bool Amostrar(Vector3 p, out float alturaSuperficie, out TipoSuperficie tipo)
    {
        alturaSuperficie = -10f;
        tipo = TipoSuperficie.Nenhum;
        if (!Indice(p, out int i)) return false;
        alturaSuperficie = alturas[i];
        tipo = (TipoSuperficie)tipos[i];
        return tipo != TipoSuperficie.Nenhum;
    }

    // Height, rather than ground classification, determines whether a head is under a roof.
    public float Exposicao(Vector3 p)
    {
        if (!Indice(p, out int i)) return 0f;
        return alturas[i] <= p.y + 0.05f ? 1f : 0f;
    }

    // Inside the building only: walking distance from the doorway plane, ignoring roofs over the yard.
    public float ProfundidadeInterna(Vector3 p)
    {
        if (!Indice(p, out int i) || profundidadeInterna == null || i >= profundidadeInterna.Length) return 0f;
        return profundidadeInterna[i];
    }

    public float ExposicaoSuavizada(Vector3 p)
    {
        if (!Indice(p, out int i) || exposicao == null || i >= exposicao.Length) return 0f;
        return exposicao[i] / 255f;
    }
}
