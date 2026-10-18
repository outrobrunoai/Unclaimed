Shader "Unclaimed/Chuva gotas"
{
    Properties
    {
        _Modo ("Modo: rastro, goticula, anel", Float) = 0
        _Opacidade ("Opacidade", Range(0,1)) = 0.35
        _Brilho ("Brilho", Range(0,8)) = 1
        _Cor ("Cor", Color) = (0.8,0.88,1,1)
        _AbrigoEpsilon ("Margem de abrigo", Float) = 0.005
        _SuavidadeProfundidade ("Distancia soft particles", Float) = 0.12
        _Ambiente ("Luz ambiente", Range(0,1)) = 0.15
        _FadePerto ("Fade perto da camera (inicio, fim)", Vector) = (0.5, 1.6, 0, 0)
        _AfinarPerto ("Afinar rastros perto (inicio, fim, largura, comprimento)", Vector) = (0.8, 3, 2.2, 0.55)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Cor;
                float _Modo, _Opacidade, _Brilho, _AbrigoEpsilon, _SuavidadeProfundidade, _Ambiente;
                float4 _FadePerto, _AfinarPerto;
            CBUFFER_END
            TEXTURE2D(_ChuvaAbrigoTex); SAMPLER(sampler_ChuvaAbrigoTex);
            float4 _ChuvaAbrigoUV, _ChuvaCentro, _ChuvaFade;
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 uv : TEXCOORD1;
                half4 color : COLOR;
                float fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.uv = i.uv; o.color = i.color; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half3 Scatter(Light l, float3 viewDir)
            {
                // Incoming light travels along -direction; forward scattering faces the light.
                float mu = dot(-l.direction, viewDir);
                float phase = 0.64 / (12.56637 * pow(max(0.04, 1.36 - 1.2 * mu), 1.5));
                return l.color * l.distanceAttenuation * l.shadowAttenuation * (0.06 + phase);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 mapUV = i.positionWS.xz * _ChuvaAbrigoUV.xy + _ChuvaAbrigoUV.zw;
                bool inside = all(mapUV >= 0) && all(mapUV <= 1) && _ChuvaAbrigoUV.x > 0;
                if (inside)
                {
                    float shelter = SAMPLE_TEXTURE2D_LOD(_ChuvaAbrigoTex, sampler_ChuvaAbrigoTex, mapUV, 0).r;
                    clip(i.positionWS.y - shelter - (_Modo > 1.5 ? -0.025 : _AbrigoEpsilon));
                }
                float2 p = i.uv.xy * 2 - 1;
                float age = saturate(i.uv.z);
                float mask;
                if (_Modo < 0.5)
                {
                    // Close streaks cover many pixels: draw them thinner, shorter and dimmer.
                    float camDist = distance(i.positionWS, GetCameraPositionWS());
                    float nearT = saturate((camDist - _AfinarPerto.x) / max(0.01, _AfinarPerto.y - _AfinarPerto.x));
                    float thin = lerp(_AfinarPerto.z, 1, nearT), len = lerp(_AfinarPerto.w, 1, nearT);
                    mask = exp(-p.x * p.x * 7 * thin * thin) * pow(saturate(1 - abs(p.y) / len), 0.65) * lerp(0.55, 1, nearT);
                }
                else if (_Modo < 1.5)
                    mask = exp(-dot(p,p) * 4) * (1 - smoothstep(0.65, 1, length(p)));
                else
                {
                    // Thin ring, eased expansion, soft fade-in and long fade-out.
                    float radius = lerp(0.12, 0.95, 1 - (1 - age) * (1 - age));
                    float thickness = max(fwidth(length(p)) * 0.75, 0.012);
                    mask = (1 - smoothstep(thickness * 0.5, thickness * 1.5, abs(length(p) - radius)))
                         * smoothstep(0, 0.15, age) * (1 - age) * (1 - age);
                }
                float distanceXZ = distance(i.positionWS.xz, _ChuvaCentro.xz);
                float fade = 1 - smoothstep(_ChuvaFade.x, _ChuvaFade.y, distanceXZ);
                fade *= smoothstep(_FadePerto.x, _FadePerto.y, distance(i.positionWS, GetCameraPositionWS()));
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float particleEye = -TransformWorldToView(i.positionWS).z;
                fade *= saturate((sceneEye - particleEye) / max(0.001, _SuavidadeProfundidade));
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 lighting = max(0, SampleSH(half3(0,1,0))) * _Ambiente;
                lighting += Scatter(GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1,1,1,1)), viewDir);
                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                    #if USE_CLUSTER_LIGHT_LOOP
                    UNITY_LOOP for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        lighting += Scatter(GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1)), viewDir);
                    }
                    #endif
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        lighting += Scatter(GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1)), viewDir);
                    LIGHT_LOOP_END
                #endif
                half alpha = saturate(mask * fade * _Opacidade * _Cor.a * i.color.a);
                half3 color = _Cor.rgb * i.color.rgb * lighting * _Brilho;
                return half4(MixFog(color, i.fog) * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
