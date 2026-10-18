Shader "Unclaimed/Vegetacao"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Cor", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Recorte alfa", Float) = 1
        _Cutoff ("Limiar alfa", Range(0,1)) = 0.5
        [Toggle(_NORMALMAP)] _UsarNormal ("Usar normal map", Float) = 0
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Forca normal", Range(0,2)) = 1
        _Smoothness ("Suavidade", Range(0,1)) = 0.15
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 0
        _Translucidez ("Translucidez (fraca)", Range(0,1)) = 0.2
        [Toggle] _InverterVerso ("Inverter normal no verso", Float) = 0
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights ("Brilho especular", Float) = 0
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections ("Reflexos", Float) = 0
        [HideInInspector] _ReceiveShadows ("Receber sombras", Float) = 1
        [Toggle] _VentoSemCor ("Malha sem cor de vertice", Float) = 1
        _VentoFolhaSemCor ("Folhas sem cor", Range(0,1)) = 1
        _VentoTronco ("Flexao principal (m)", Float) = 0.04
        _VentoRamos ("Flexao ramos (m)", Float) = 0.10
        _VentoFolhas ("Tremor folhas (m)", Float) = 0.025
        _VentoFrequencia ("Frequencia (Hz)", Float) = 0.35
        _VentoAltura ("Altura de referencia (m)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull [_Cull]
        ZWrite On
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VegetacaoVertex
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma fragment VegetacaoForward
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #include "VegetacaoPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VegetacaoVertex
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma fragment VegetacaoDepth
            #define VEGETACAO_SHADOW 1
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "VegetacaoPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VegetacaoVertex
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma fragment VegetacaoDepth
            #include "VegetacaoPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VegetacaoVertex
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma fragment VegetacaoDepthNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "VegetacaoPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
