using System;
using UnityEngine;

/// <summary>
/// Disposição do quadro de avisos (QuadroAvisos.json): tamanho do quadro e, para cada documento afixado,
/// posição, rotação e ordem (metros, no plano do quadro, origem no centro, x à direita e y para cima), tamanho
/// e fixação. O mesmo arquivo traz o texto de cada papel, que só o gerador das imagens lê
/// (Blender/Recepcao/scripts/quadro_avisos.py); aqui ele serve ao quadro 3D (UnclaimedQuadroDeAvisos) e à
/// escolha do papel com o mouse (QuadroDeAvisos).
/// </summary>
[Serializable]
public class QuadroDados
{
    [Serializable] public class Quadro { public float largura, altura, moldura, profundidade; }
    [Serializable] public class Ponto { public float u, v; }
    [Serializable] public class Fixacao { public string tipo; public Ponto[] pontos; public string[] cores; }

    [Serializable]
    public class Documento
    {
        public string id, titulo, acabamento, papel;
        public float[] tamanho, posicao;
        public float rotacao;
        public int ordem;
        public Fixacao fixacao;

        public Vector2 Tamanho => new Vector2(tamanho[0], tamanho[1]);
        public Vector2 Posicao => new Vector2(posicao[0], posicao[1]);

        /// <summary>Ponto do quadro (m) → coordenadas do papel (0..1, origem no canto superior esquerdo); falso se não cair nele.</summary>
        public bool Contem(Vector2 pontoNoQuadro, out Vector2 uv)
        {
            Vector2 d = pontoNoQuadro - Posicao;
            float a = -rotacao * Mathf.Deg2Rad;
            var local = new Vector2(d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a), d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
            uv = new Vector2(local.x / tamanho[0] + 0.5f, 0.5f - local.y / tamanho[1]);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <summary>Ponto do papel (u, v, origem no canto superior esquerdo) → ponto do quadro (m).</summary>
        public Vector2 NoQuadro(Vector2 uv)
        {
            var local = new Vector2((uv.x - 0.5f) * tamanho[0], (0.5f - uv.y) * tamanho[1]);
            float a = rotacao * Mathf.Deg2Rad;
            return Posicao + new Vector2(local.x * Mathf.Cos(a) - local.y * Mathf.Sin(a), local.x * Mathf.Sin(a) + local.y * Mathf.Cos(a));
        }
    }

    public Quadro quadro;
    public Documento[] documentos;

    public static QuadroDados Carregar(string json) => JsonUtility.FromJson<QuadroDados>(json);
}
