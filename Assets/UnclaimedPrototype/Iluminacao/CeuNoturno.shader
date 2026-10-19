// Night sky with moving clouds. The cloud deck is the one drawn by Blender/Ceu/ceu_noturno.py
// (compose_layers is the reference for this file): the same 3D value noise (integer hash), fBm, curved
// shells, coverage, warp, moon-facing relief, opening around the moon and colours, evaluated per pixel so
// the shells can drift with the wind. _Fundo is the cloudless sky + stars from the same script; the
// brightness curve (gain per altitude) comes from its json. At time zero and no wind the result matches
// Ceu_Noturno.png (stars and the moon veil are approximations, see the script).
// _NUVENS_TEXTURA: cheap fallback that rotates the baked cloud fields (_Nuvens) around the zenith.
// Wind and time come from the global _CeuEstado (CeuNoturno.cs): x, z = wind direction, y = time (s),
// w = 1 when published. Without it the clouds use _Time.y and _DirecaoPadrao.
Shader "Unclaimed/Ceu noturno"
{
    Properties
    {
        _Fundo ("Fundo: ceu sem nuvens + estrelas (sRGB)", 2D) = "black" {}
        [NoScaleOffset] _Nuvens ("Campos de nuvem (linear, modo textura)", 2D) = "black" {}
        [HideInInspector] _GainA ("Ganho base 1", Vector) = (1, 1, 1, 0)
        [HideInInspector] _GainB ("Ganho base 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _GainC ("Ganho base 3", Vector) = (0, 0, 0, 0)
        _LuaDir ("Direcao da lua", Vector) = (0.059, 0.342, -0.938, 0)
        _LuaDiametro ("Diametro da lua (graus)", Float) = 1.6
        _AberturaPiso ("Nuvens sobre a lua (0 = abertura limpa)", Range(0, 1)) = 0.035
        _VelocidadeBaixa ("Vento na camada de 2 km (m/s)", Float) = 7
        _VelocidadeFina ("Vento na camada de 1,3 km (m/s)", Float) = 9
        _VelocidadeAlta ("Vento na camada alta (m/s)", Float) = 3
        _Evolucao ("Evolucao das nuvens (unidades/min)", Float) = 0.6
        _DirecaoPadrao ("Direcao sem CeuNoturno (x, z)", Vector) = (1, 0, 0.3, 0)
        _GiroTextura ("Modo textura: voltas por hora", Float) = 1.5
        [Toggle(_NUVENS_TEXTURA)] _ModoTextura ("Nuvens por textura (barato)", Float) = 0
        [Gamma] _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _Exposure ("Exposure", Range(0, 8)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma shader_feature_local _NUVENS_TEXTURA
            #include "UnityCG.cginc"

            sampler2D _Fundo, _Nuvens;
            float4 _GainA, _GainB, _GainC, _LuaDir, _DirecaoPadrao, _CeuEstado;
            float _LuaDiametro, _AberturaPiso, _VelocidadeBaixa, _VelocidadeFina, _VelocidadeAlta, _Evolucao, _GiroTextura;
            half4 _Tint;
            half _Exposure;

            // ceu_noturno.py constants
            #define CLOUD_SCALE 1.1
            #define COVER_SCALE 9.0
            #define COVER_BASE 0.37
            #define COVER_SPREAD 0.45
            #define SOFTNESS 0.19
            #define WARP 0.35
            #define DETAIL_OCTAVES 3
            #define DETAIL_GAIN 3.0
            #define EARTH 6371.0
            static const float3 SHELL_OFFSET = float3(23.4, 11.7, -38.2);

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float smooth01(float a, float b, float x)
            {
                float t = saturate((x - a) / (b - a));
                return t * t * (3 - 2 * t);
            }

            // Integer lattice hash of the script's noise(): identical bits, so the deck is the same deck.
            float Hash(int3 c, uint seed)
            {
                uint h = (asuint(c.x) * 1597334677u) ^ (asuint(c.y) * 3812015801u) ^ (asuint(c.z) * 2798796415u) ^ seed;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                return (float)(h >> 8) / 16777215.0;
            }

            float Noise(float3 p, uint seed)
            {
                float3 cf = floor(p);
                int3 c = (int3)cf;
                float3 f = p - cf;
                f = f * f * f * (f * (f * 6 - 15) + 10);
                float x00 = lerp(Hash(c, seed), Hash(c + int3(1, 0, 0), seed), f.x);
                float x10 = lerp(Hash(c + int3(0, 1, 0), seed), Hash(c + int3(1, 1, 0), seed), f.x);
                float x01 = lerp(Hash(c + int3(0, 0, 1), seed), Hash(c + int3(1, 0, 1), seed), f.x);
                float x11 = lerp(Hash(c + int3(0, 1, 1), seed), Hash(c + int3(1, 1, 1), seed), f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            // fbm() of the script. foot > 0 adds the zero-mean detail octaves, each faded out below ~2 pixels.
            // low = the normalized sum of the first `lowOctaves` octaves (for the relief below).
            float Fbm(float3 p, uint seed, int octaves, float foot, int lowOctaves, out float low)
            {
                float total = 0, amplitude = 1, norm = 0;
                low = 0;
                int extra = foot > 0 ? DETAIL_OCTAVES : 0;
                [loop] for (int i = 0; i < octaves + extra; i++)
                {
                    if (i < octaves)
                    {
                        total += amplitude * Noise(p, seed + (uint)i * 101u);
                        norm += amplitude;
                        if (i == lowOctaves - 1) low = total / norm;
                    }
                    else
                    {
                        float fade = smooth01(1.5, 3.0, pow(2.03, -i) / max(foot, 1e-6));
                        if (fade > 0) total += DETAIL_GAIN * amplitude * (Noise(p, seed + (uint)i * 101u) - 0.5) * fade;
                    }
                    amplitude *= 0.48;
                    p = p * 2.03 + float3(13.2, 7.1, -9.7);
                }
                return total / norm;
            }

            float Fbm(float3 p, uint seed, int octaves, float foot)
            {
                float unused;
                return Fbm(p, seed, octaves, foot, 0, unused);
            }

            // Distance (km) from the ground to a concentric shell `height` km up, along d.
            float ShellDistance(float3 d, float height)
            {
                float ry = EARTH * d.y;
                return (2 * EARTH * height + height * height) / (sqrt(ry * ry + 2 * EARTH * height + height * height) + ry);
            }

            // Noise coordinates on a shell; `drift` (km) is how far the layer has moved with the wind.
            float3 Shell(float3 d, float height, float scale, float3 drift)
            {
                return (d * ShellDistance(d, height) - drift) / scale + SHELL_OFFSET;
            }

            // Noise units covered by one screen pixel on the shell (the script used one panorama texel).
            float Footprint(float3 d, float height, float scale, float pixel)
            {
                return ShellDistance(d, height) * pixel / (scale * max(d.y, 0.05));
            }

            float3 Drift(float speed, float t)
            {
                float2 w = _CeuEstado.w > 0.5 ? _CeuEstado.xz : _DirecaoPadrao.xy;
                w = dot(w, w) > 1e-6 ? normalize(w) : float2(1, 0);
                return float3(w.x, 0, w.y) * (speed * 0.001 * t);   // m/s -> km
            }

            float2 PanoramaUV(float3 d)
            {
                float u = 0.5 - atan2(d.z, d.x) / (2 * UNITY_PI);
                float v = 1 - (acos(clamp(d.y, -1, 1)) / UNITY_PI * 4095.0 + 0.5) / 4096.0;
                return float2(frac(u), v);
            }

            struct Clouds { float low, high, relief; };

            // cloud_fields() of the script, without the moon opening (applied by the caller).
            Clouds Fields(float3 d, float3 moon, float t, float pixel)
            {
                Clouds c;
            #if defined(_NUVENS_TEXTURA)
                float2 uv = PanoramaUV(d);
                float turn = t * _GiroTextura / 3600.0;
                float4 deck = tex2Dlod(_Nuvens, float4(uv.x + turn, uv.y, 0, 0));
                float layer = tex2Dlod(_Nuvens, float4(uv.x + turn * 1.25, uv.y, 0, 0)).a;
                c.high = tex2Dlod(_Nuvens, float4(uv.x + turn * 0.4, uv.y, 0, 0)).b;
                c.low = max(deck.r, 0.8 * layer);
                c.relief = (deck.g - 0.5) * 0.6;
            #else
                float3 drift = Drift(_VelocidadeBaixa, t), driftFine = Drift(_VelocidadeFina, t), driftHigh = Drift(_VelocidadeAlta, t);
                float3 evolve = float3(0, _Evolucao * t / 60.0, 0);
                float broad = Fbm(Shell(d, 2.0, COVER_SCALE, drift) + evolve * 0.3, 61, 3, 0);
                float3 p = Shell(d, 2.0, CLOUD_SCALE, drift) + evolve;
                float3 warp = float3(Fbm(p * 0.9, 31, 2, 0), Fbm(p * 0.9, 47, 2, 0), Fbm(p * 0.9, 79, 2, 0)) - 0.5;
                float3 q = p + warp * WARP;
                float foot = Footprint(d, 2.0, CLOUD_SCALE, pixel);
                float massLow;
                float mass = Fbm(q, 13, 6, foot, 4, massLow);
                float threshold = COVER_BASE + COVER_SPREAD * (broad - 0.5);
                float density = smooth01(threshold, threshold + SOFTNESS, mass);
                float3 p2 = Shell(d, 1.3, CLOUD_SCALE * 0.55, driftFine) + evolve * 1.4;
                float mass2 = Fbm(p2 + warp * WARP * 0.5, 211, 5, Footprint(d, 1.3, CLOUD_SCALE * 0.55, pixel));
                c.low = max(density, 0.8 * smooth01(threshold + 0.06, threshold + 0.06 + SOFTNESS, mass2));
                c.high = 0.36 + 0.28 * Fbm(Shell(d, 8.5, 14.0, driftHigh), 173, 3, 0);
                // Moon-facing relief: shift along the moon direction projected on the shell's tangent plane.
                float3 normal = normalize(d * ShellDistance(d, 2.0) + float3(0, EARTH, 0));
                float3 tangent = moon - normal * dot(normal, moon);
                // The script differences two full 6+3 octave fields; the relief lives in the low octaves, so
                // four are enough here and the unshifted four come from the deck's own sum.
                float shifted = Fbm(q + tangent * 0.18, 13, 4, 0) - massLow;
                c.relief = clamp(-shifted * 4.0, -0.3, 0.3);
            #endif
                return c;
            }

            float3 Gain(float alt)
            {
                return _GainA.rgb + _GainB.rgb * (1 - smooth01(15, 45, alt)) + _GainC.rgb * smooth01(40, 70, alt);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float pixel = max(length(ddx(d)), length(ddy(d)));
                float t = _CeuEstado.w > 0.5 ? _CeuEstado.y : _Time.y;
                float3 moon = normalize(_LuaDir.xyz);
                float alt = degrees(asin(clamp(d.y, -1, 1)));
                float angle = degrees(acos(clamp(dot(d, moon), -1, 1)));
                float3 p = tex2Dlod(_Fundo, float4(PanoramaUV(d), 0, 0)).rgb;   // cloudless sky + stars, linear

                if (alt > 0)
                {
                    Clouds c = Fields(d, moon, t, pixel);
                    float openRadius = 10.5 + 4.0 * (Noise(d * 19, 233) - 0.5);
                    float opening = smooth01(openRadius * 0.5, openRadius * 1.35, angle);
                    float low = c.low * (_AberturaPiso + (1 - _AberturaPiso) * opening);
                    float haze = smooth01(0, 12, alt);
                    float lowAlpha = (1 - exp(-low * 7)) * haze;
                    float highAlpha = (1 - exp(-c.high)) * haze;
                    float nearMoon = exp(-angle / 17);
                    float thick = smooth01(0.55, 1.0, low);
                    float3 cloud = (float3(0.029, 0.036, 0.056) + c.relief * float3(0.030, 0.034, 0.042)
                                    + nearMoon * float3(0.014, 0.017, 0.024)) * (1 - 0.22 * thick);
                    cloud += (1 - smooth01(10, 70, alt)) * float3(0.002, 0.003, 0.004);
                    p = p * (1 - highAlpha) + float3(0.025, 0.032, 0.052) * highAlpha;
                    p = p * (1 - lowAlpha) + cloud * lowAlpha;

                    // Moon behind the low deck and a thin streaky veil; tight corona plus a faint aureole.
                    float r = _LuaDiametro * 0.5;
                    float veil = 0.18 + 0.45 * 0.5;
                    if (angle < 35)
                    {
                    #if defined(_NUVENS_TEXTURA)
                        veil = 0.18 + 0.45 * smooth01(0.35, 0.75, (c.high - 0.36) / 0.28);
                    #else
                        veil = 0.18 + 0.45 * smooth01(0.35, 0.75, Fbm(Shell(d, 8.5, 1.6, Drift(_VelocidadeAlta, t)), 401, 4, 0));
                    #endif
                    }
                    if (angle < r)
                    {
                        float disc = 1 - smooth01(r * 0.88, r, angle);
                        float maria = Fbm(d * 180, 907, 3, 0);
                        float limb = pow(saturate(1 - (angle / r) * (angle / r)), 0.25);
                        float3 face = float3(1.0, 1.0, 0.95) * (0.69 + 0.31 * maria);
                        float seen = exp(-low * 10 - veil);
                        p = p * (1 - disc * seen) + face * (limb * disc * seen);
                    }
                    float outside = max(angle - r, 0);
                    float halo = (0.05 * exp(-outside / 1.4) + 0.012 * exp(-outside / 7)) * (0.6 + 0.8 * veil);
                    p += halo * exp(-low * 3) * float3(0.75, 0.85, 1.0);
                    p *= Gain(alt);
                }
                return half4(p * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
