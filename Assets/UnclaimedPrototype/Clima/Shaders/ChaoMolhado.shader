Shader "Unclaimed/Chao molhado"
{
    Properties
    {
        _Intensidade ("Intensidade", Range(0,1)) = 1
        _EscurecimentoMin ("Escurecimento minimo", Range(0,1)) = 0.12
        _EscurecimentoMax ("Escurecimento maximo", Range(0,1)) = 0.22
        _SuavidadeMin ("Suavidade minima", Range(0,1)) = 0.35
        _SuavidadeMax ("Suavidade maxima", Range(0,1)) = 0.62
        _SuavidadePoca ("Suavidade da poca", Range(0,1)) = 0.93
        _EscalaRuido ("Escala do ruido", Float) = 1
        _Reflexo ("Reflexo do ambiente", Range(0,1)) = 0.5
        _EscurecimentoPoca ("Escurecimento da poca", Range(0,1)) = 0.45
        _ReflexoPoca ("Reflexo do ambiente na poca", Range(0,1)) = 0.6
        _BrilhoLua ("Brilho especular da luz principal", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensidade, _EscurecimentoMin, _EscurecimentoMax;
                float _SuavidadeMin, _SuavidadeMax, _SuavidadePoca, _EscalaRuido, _Reflexo, _EscurecimentoPoca, _ReflexoPoca, _BrilhoLua;
            CBUFFER_END
            TEXTURE2D(_ChuvaAbrigoTex); SAMPLER(sampler_ChuvaAbrigoTex);
            float4 _ChuvaAbrigoUV;
            float _ChuvaUmidade;
            float4 _ChuvaPocas[8], _ChuvaPocasRot[8];
            int _ChuvaPocasQtd;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
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
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell), Hash(cell+float2(1,0)), f.x),
                    lerp(Hash(cell+float2(0,1)), Hash(cell+1), f.x), f.y);
            }
            half3 Specular(Light l, float3 n, float3 v, float roughness)
            {
                float3 h = SafeNormalize(l.direction + v);
                float nl = saturate(dot(n,l.direction)), nv = max(0.001, saturate(dot(n,v)));
                float nh = saturate(dot(n,h)), vh = saturate(dot(v,h));
                float a = max(0.004, roughness * roughness), a2 = a*a;
                float denominator = nh*nh*(a2-1)+1;
                float distribution = a2 / max(1e-7, PI*denominator*denominator);
                float visibility = 0.5 / max(0.0001, nl*sqrt(nv*nv*(1-a2)+a2) + nv*sqrt(nl*nl*(1-a2)+a2));
                float fresnel = 0.02 + 0.98*pow(1-vh,5);
                return l.color * (l.distanceAttenuation*l.shadowAttenuation*nl*distribution*visibility*fresnel);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.positionWS.xz * _ChuvaAbrigoUV.xy + _ChuvaAbrigoUV.zw;
                clip(_ChuvaAbrigoUV.x - 0.000001);
                clip(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                float exposure = SAMPLE_TEXTURE2D_LOD(_ChuvaAbrigoTex, sampler_ChuvaAbrigoTex, uv, 0).g;
                float w = saturate(_ChuvaUmidade * _Intensidade * exposure);
                float2 p = i.positionWS.xz * max(0.001, _EscalaRuido);
                float noise = Noise(p/0.6)*0.35 + Noise(p/3)*0.65;
                float puddle = 0;
                for (int j = 0; j < min(_ChuvaPocasQtd,8); j++)
                {
                    float2 d = i.positionWS.xz - _ChuvaPocas[j].xy;
                    float2 r = _ChuvaPocasRot[j].xy;
                    float2 q = float2(r.x*d.x+r.y*d.y, -r.y*d.x+r.x*d.y) / max(_ChuvaPocas[j].zw,0.01);
                    // One coherent shallow puddle with a gently wavy, fairly crisp waterline (no detached blobs).
                    float edge = length(q) + (Noise(p*0.9 + j*13.1)-0.5)*0.2 + (Noise(p*5)-0.5)*0.05;
                    puddle = max(puddle, 1-smoothstep(0.9,0.97,edge));
                }
                float smoothness = lerp(lerp(_SuavidadeMin,_SuavidadeMax,noise),_SuavidadePoca,puddle);
                float3 n = normalize(i.normalWS + float3(Noise(p*7)-0.5,0,Noise(p*7+19)-0.5)*lerp(0.07,0.015,puddle));
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float roughness = max(0.04,1-smoothness);
                // The moon glint broken by canopy shadows read as painted blotches: keep it faint.
                half3 color = Specular(GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1,1,1,1)),n,v,roughness) * _BrilhoLua;
                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                    #if USE_CLUSTER_LIGHT_LOOP
                    UNITY_LOOP for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        color += Specular(GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)),n,v,roughness);
                    }
                    #endif
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        color += Specular(GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)),n,v,roughness);
                    LIGHT_LOOP_END
                #endif
                float fresnel = 0.02 + 0.98*pow(1-saturate(dot(n,v)),5);
                color += GlossyEnvironmentReflection(reflect(-v,n),i.positionWS,roughness,1,inputData.normalizedScreenSpaceUV)
                    * fresnel * lerp(0.35 * _Reflexo, _ReflexoPoca, puddle);
                half alpha = w * lerp(lerp(_EscurecimentoMin,_EscurecimentoMax,noise),_EscurecimentoPoca,puddle);
                // Black absorption plus additive reflected radiance: premultiplied water layer.
                float fogVisibility = MixFogColor(float3(1,1,1), float3(0,0,0), i.fog).r;
                return half4(MixFog(color*w, i.fog) - MixFog(half3(0,0,0), i.fog), alpha*fogVisibility);
            }
            ENDHLSL
        }
    }
}
