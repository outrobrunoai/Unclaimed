// Filtro de fita VHS / câmera de mão, aplicado à imagem final pelo Full Screen Pass Renderer Feature do URP
// (Assets/Settings/PC_Renderer.asset). A interface IMGUI (caderno, mira) é desenhada depois e não passa por ele.
//
// O material guarda o "visual" (quanto de cada efeito). O componente FiltroVHS na cena controla, por variáveis
// globais: _VHSAtivo (0 fora do Play Mode, para não atrapalhar a edição), _VHSTempo (tempo real, anima até com o
// jogo pausado) e _VHSFalha (0..1, picos de interferência nos sustos).
Shader "Unclaimed/FiltroVHS"
{
    Properties
    {
        _Intensidade ("Intensidade geral", Range(0, 1)) = 1
        _Curvatura ("Curvatura da lente", Range(0, 0.3)) = 0.035
        _Aberracao ("Aberração cromática nas bordas (px)", Range(0, 6)) = 1.8
        _BorraoCor ("Borrão da cor na horizontal (px)", Range(0, 12)) = 5
        _Ondulacao ("Ondulação da fita (px)", Range(0, 4)) = 0.9
        _Granulado ("Granulado", Range(0, 0.3)) = 0.06
        _Linhas ("Linhas de varredura", Range(0, 0.5)) = 0.10
        _Vinheta ("Vinheta", Range(0, 1.5)) = 0.8
        _Saturacao ("Saturação", Range(0, 1.5)) = 0.88
        _Preto ("Preto lavado", Range(0, 0.15)) = 0.03
        _Comutacao ("Ruído de troca de cabeça: altura (fração da tela)", Range(0, 0.06)) = 0.016
        _ComutacaoDesloc ("Ruído de troca de cabeça: deslocamento (px)", Range(0, 120)) = 45
        _Dropouts ("Dropouts (riscos brancos curtos)", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "FiltroVHS"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensidade, _Curvatura, _Aberracao, _BorraoCor, _Ondulacao, _Granulado;
            float _Linhas, _Vinheta, _Saturacao, _Preto, _Comutacao, _ComutacaoDesloc, _Dropouts;
            float _VHSAtivo, _VHSTempo, _VHSFalha; // globais (FiltroVHS.cs)

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 Amostra(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float2 ParaIQ(float3 c)
            {
                return float2(dot(c, float3(0.596, -0.274, -0.322)), dot(c, float3(0.211, -0.523, 0.312)));
            }

            half4 Frag(Varyings entrada) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(entrada);
                float k = saturate(_Intensidade * _VHSAtivo);
                float falha = saturate(_VHSFalha) * _VHSAtivo;
                float2 tela = _ScreenParams.xy;
                float2 px = 1.0 / tela;
                float t = _VHSTempo;

                // Lente da câmera: barril leve; os cantos saem da imagem e ficam pretos.
                float2 c = entrada.texcoord * 2.0 - 1.0;
                float r2 = dot(c, c);
                float curv = _Curvatura * k;
                float2 uv = c * (1.0 + curv * r2) / (1.0 + curv) * 0.5 + 0.5;

                // Fita: ondulação lenta e tremor fino por linha (instabilidade do mecanismo).
                float linha = floor(uv.y * tela.y * 0.5);
                float quadro = floor(t * 30.0);
                float onda = sin(uv.y * 40.0 + t * 2.3) * 0.5 + sin(uv.y * 173.0 - t * 7.1) * 0.25;
                float tremor = Hash(float2(linha, floor(t * 24.0))) - 0.5;
                float deslocamento = (onda + tremor * 0.6) * _Ondulacao * k;

                // Ruído de troca de cabeça: faixa fina e torta no pé da imagem, sempre presente em fita sem
                // mascaramento. Nas falhas ela cresce e "pula" para cima, como em fita com servo instável.
                float pulo = falha * (0.5 + 0.5 * sin(t * 23.0)) * (0.6 + 0.4 * Hash(float2(quadro, 3.1)));
                float hComut = _Comutacao * k * (1.0 + pulo * 9.0);
                float naComut = hComut > 0.0 ? saturate(1.0 - uv.y / hComut) : 0.0; // 1 na borda de baixo
                float comut = step(0.001, naComut);
                deslocamento += comut * (pow(naComut, 0.7) * _ComutacaoDesloc + (Hash(float2(linha, quadro)) - 0.5) * 10.0);

                // Rastreamento perdido (só nas falhas): uma faixa da imagem rasga para o lado, em posição aleatória.
                float posRast = Hash(float2(floor(t * 6.0), 9.7));
                float rast = smoothstep(0.07, 0.0, abs(uv.y - posRast)) * falha;
                deslocamento += rast * ((Hash(float2(linha, quadro)) - 0.25) * 40.0);
                uv.x += deslocamento * px.x;

                // Aberração cromática, mais forte nas bordas (lente barata).
                float2 ab = c * (0.35 + r2) * (_Aberracao * k + falha * 4.0) * px;
                float3 cor = float3(Amostra(uv + ab).r, Amostra(uv).g, Amostra(uv - ab).b);

                // VHS: o brilho fica nítido, a cor borra e "atrasa" para a direita.
                float lum = dot(cor, float3(0.299, 0.587, 0.114));
                float2 iq = 0;
                float passo = _BorraoCor * k * px.x;
                [unroll] for (int s = -2; s <= 2; s++)
                    iq += ParaIQ(Amostra(uv + float2((s * 0.5 + 0.6) * passo, 0)));
                iq *= 0.2 * lerp(1.0, _Saturacao, k);
                cor = float3(lum + 0.956 * iq.x + 0.621 * iq.y,
                             lum - 0.272 * iq.x - 0.647 * iq.y,
                             lum - 1.106 * iq.x + 1.703 * iq.y);

                // Preto lavado e esverdeado, como em fita gravada várias vezes.
                float preto = _Preto * k;
                cor = cor * (1.0 - preto) + preto * float3(0.75, 1.0, 0.9);

                // Ruído na faixa de troca de cabeça e no rasgo de rastreamento.
                float chuvisco = Hash(float2(entrada.texcoord.x * tela.x, linha + quadro));
                cor = lerp(cor, cor * 0.75 + chuvisco * 0.3, saturate(comut * 0.45 + rast * 0.5)); // a imagem deslocada continua visível sob o ruído

                // Dropouts (perda de óxido na fita): um traço fino e claro que começa de repente e se apaga
                // numa cauda para a direita, como o sinal voltando aos poucos. Dura uns dois quadros, é translúcido
                // e cintila; nunca um bloco branco liso.
                float quadroDrop = floor(t * 15.0);
                float temDropout = step(1.0 - 0.12 * _Dropouts * k - 0.5 * falha, Hash(float2(quadroDrop, 1.7)));
                float sorteio = Hash(float2(linha, quadroDrop + 0.5));
                float inicio = Hash(float2(quadroDrop * 1.37 + 17.3, linha * 1.618 + 0.37));
                float cauda = 0.008 + 0.025 * Hash(float2(quadroDrop * 0.73 + 5.1, linha * 2.31 + 1.9)); // fração da largura
                float dx = entrada.texcoord.x - inicio;
                float risco = temDropout * step(1.0 - 0.004 - 0.015 * falha, sorteio)
                            * smoothstep(0.0, 0.002, dx) * exp(-max(dx, 0.0) / cauda) * step(0.0, dx);
                // O traço sai quebrado em pedaços de ~3 px (sinal falhando), não como uma linha lisa desenhada.
                float pedaco = Hash(float2(floor(entrada.texcoord.x * tela.x / 3.0), linha + quadro * 7.0));
                float cintila = 0.25 + 0.75 * pedaco * pedaco;
                cor = lerp(cor, max(cor, float3(0.78, 0.8, 0.82)), saturate(risco * cintila * 0.65));

                // Linhas de varredura e granulado.
                float varredura = 0.5 + 0.5 * cos(entrada.texcoord.y * tela.y * PI);
                cor *= 1.0 - _Linhas * k * (1.0 - varredura);
                cor += (Hash(entrada.texcoord * tela + frac(t) * 97.0) - 0.5) * (_Granulado * k + falha * 0.12);

                // Vinheta.
                float vinheta = smoothstep(1.45, 0.25, length(c));
                cor *= lerp(1.0, vinheta, saturate(_Vinheta * k));

                // Fora da imagem (cantos da lente): preto.
                float dentro = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return half4(max(cor, 0.0) * dentro, 1.0);
            }
            ENDHLSL
        }
    }
}
