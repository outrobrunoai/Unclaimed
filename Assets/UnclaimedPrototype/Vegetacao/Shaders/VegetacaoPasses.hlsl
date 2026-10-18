#ifndef UNCLAIMED_VEGETACAO_PASSES_INCLUDED
#define UNCLAIMED_VEGETACAO_PASSES_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST, _BaseColor;
    float _Cutoff, _AlphaClip, _UsarNormal, _BumpScale, _Smoothness, _Cull, _Translucidez, _InverterVerso;
    float _SpecularHighlights, _EnvironmentReflections, _ReceiveShadows;
    float _VentoSemCor, _VentoFolhaSemCor, _VentoTronco, _VentoRamos, _VentoFolhas, _VentoFrequencia, _VentoAltura;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
#include "VegetacaoVento.hlsl"
float3 _LightDirection, _LightPosition;
struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half fog : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings VegetacaoVertex(Attributes i)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 p = DeformarVegetacao(i.positionOS.xyz, i.normalOS, i.color);
    VertexPositionInputs positions = GetVertexPositionInputs(p);
    VertexNormalInputs normals = GetVertexNormalInputs(i.normalOS, i.tangentOS);
    o.positionWS = positions.positionWS;
    o.positionCS = positions.positionCS;
    o.normalWS = normals.normalWS;
    o.tangentWS = half4(normals.tangentWS, i.tangentOS.w * GetOddNegativeScale());
    o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
    o.fog = ComputeFogFactor(o.positionCS.z);
#if defined(VEGETACAO_SHADOW)
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDir = normalize(_LightPosition - o.positionWS);
    #else
        float3 lightDir = _LightDirection;
    #endif
    o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, lightDir)));
#endif
    return o;
}
half4 VegetacaoBase(Varyings i)
{
    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
#if defined(_ALPHATEST_ON)
    clip(base.a - _Cutoff);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(i.positionCS);
#endif
    return base;
}
half3 VegetacaoNormal(Varyings i, FRONT_FACE_TYPE face)
{
    half3 n = NormalizeNormalPerPixel(i.normalWS);
#if defined(_NORMALMAP)
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
    half3 tangent = normalize(i.tangentWS.xyz);
    n = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
        half3x3(tangent, i.tangentWS.w * cross(n, tangent), n)));
#endif
    // Kit meshes carry crown/tuft "volume" normals that are valid on both faces; flip only for plain cards.
    return _InverterVerso > 0.5 ? n * IS_FRONT_VFACE(face, 1.0h, -1.0h) : n;
}
half3 VegetacaoTransmissao(Light light, half3 normal, half3 albedo)
{
    return albedo * light.color * (light.distanceAttenuation * light.shadowAttenuation)
        * saturate(-dot(normal, light.direction)) * (_Translucidez * 0.15h);
}
half4 VegetacaoForward(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 base = VegetacaoBase(i);
    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.positionCS = i.positionCS;
    inputData.normalWS = VegetacaoNormal(i, face);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
    // Per-pixel shadow coordinates preserve cascade selection across large plant meshes.
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
    #else
        inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    #endif
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.shadowMask = half4(1,1,1,1);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1), i.fog);
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = base.rgb;
    surface.alpha = base.a;
    surface.normalTS = half3(0,0,1);
    surface.smoothness = _Smoothness;
    surface.occlusion = 1;
    half4 result = UniversalFragmentPBR(inputData, surface);
    half3 transmission = VegetacaoTransmissao(GetMainLight(inputData.shadowCoord, i.positionWS, inputData.shadowMask), inputData.normalWS, base.rgb);
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    #if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        transmission += VegetacaoTransmissao(GetAdditionalLight(lightIndex, i.positionWS, inputData.shadowMask), inputData.normalWS, base.rgb);
    }
    #endif
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        transmission += VegetacaoTransmissao(GetAdditionalLight(lightIndex, i.positionWS, inputData.shadowMask), inputData.normalWS, base.rgb);
    LIGHT_LOOP_END
#endif
    result.rgb = MixFog(result.rgb + transmission, inputData.fogCoord);
    return result;
}
half4 VegetacaoDepth(Varyings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    VegetacaoBase(i);
    return half4(i.positionCS.zzz, 0);
}
half4 VegetacaoDepthNormals(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    VegetacaoBase(i);
    half3 normal = VegetacaoNormal(i, face);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normal, 0);
#endif
}
#endif
