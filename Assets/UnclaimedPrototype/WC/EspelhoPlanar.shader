// Face of the bathroom mirror. With EspelhoPlanar running (_ReflexoAtivo = 1) it shows the planar reflection
// rendered by the mirror's reflection camera, read in screen space with u flipped (that camera uses a proper
// rotation, so its image is the mirror image flipped horizontally). Otherwise (Edit Mode, mirror off screen)
// it falls back to the room's reflection probe. Wipe streaks and dried splashes (alpha of _ManchasTex, the
// mirror's smoothness map) dull and blur the reflection locally.
Shader "Unclaimed/Espelho planar"
{
    Properties
    {
        _BaseColor ("Tinta do vidro", Color) = (0.9, 0.92, 0.92, 1)
        _ManchasTex ("Manchas (A = lisura)", 2D) = "white" {}
        _Manchas ("Intensidade das manchas", Range(0, 1)) = 0.6
        [NoScaleOffset] _ReflexoTex ("Reflexo (definido pelo script)", 2D) = "black" {}
        _ReflexoAtivo ("Reflexo planar ativo", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ManchasTex); SAMPLER(sampler_ManchasTex);
            TEXTURE2D(_ReflexoTex); SAMPLER(sampler_ReflexoTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _ManchasTex_ST;
                half _Manchas;
                half _ReflexoAtivo;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _ManchasTex);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 tela = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 r = reflect(-v, n);

                half lisura = SAMPLE_TEXTURE2D(_ManchasTex, sampler_ManchasTex, i.uv).a;
                half mancha = saturate((0.975 - lisura) * 7) * _Manchas;

                half3 sonda = GlossyEnvironmentReflection(r, i.positionWS, 0.02, 1.0, tela);
                half3 planar = SAMPLE_TEXTURE2D(_ReflexoTex, sampler_ReflexoTex, float2(1.0 - tela.x, tela.y)).rgb;
                half3 reflexo = lerp(sonda, planar, saturate(_ReflexoAtivo));

                // Manchas: reflexo mais fosco (sonda borrada) e um véu claro e fraco por cima.
                half3 fosco = GlossyEnvironmentReflection(r, i.positionWS, 0.45, 1.0, tela);
                reflexo = lerp(reflexo, fosco, mancha * 0.6) + mancha * 0.025;

                half3 cor = reflexo * _BaseColor.rgb;
                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
            ENDHLSL
        }

        // Depth for the depth prepass / shadows of the mirror (it casts like any opaque surface).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex V
            #pragma fragment F
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 V(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half F() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
