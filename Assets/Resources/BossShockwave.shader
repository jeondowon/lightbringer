// The Warlord's shockwave: a violet crest racing over the ground with an indigo wake, and a low curtain of dark
// light rising from the crest. Drawn on a terrain-following ring mesh rebuilt every frame by BossShockwaveVfx.
// Premultiplied blend: the wake dims the field while the crest adds light. Lives in Resources so standalone builds
// keep it (the material is created at runtime).
//   UV0.x  metres behind the leading edge (negative ahead of it)    UV0.y  curtain height fraction (0 at the ground)
//   UV0.z  0 ground band, 1 curtain                                  UV0.w  fade beyond the playable field
Shader "Lightbringer/Boss Shockwave"
{
    Properties
    {
        _Opacity ("Opacity", Float) = 1
        _Centre ("Centre (world)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "BossShockwave"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                float4 _Centre;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float4 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; float3 world : TEXCOORD1; half fog : TEXCOORD2; };

            V Vert(A v)
            {
                V o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float sum = 0, amplitude = .5;
                for (int k = 0; k < 4; k++) { sum += Noise(p) * amplitude; p = p * 2.03 + 17.1; amplitude *= .5; }
                return sum / .9375;
            }

            static const half3 Crest = half3(1.35, .95, 2.6);   // violet-white leading edge (HDR, blooms)
            static const half3 Violet = half3(.8, .22, 1.75);
            static const half3 Indigo = half3(.12, .1, .5);

            half4 Frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 xz = i.world.xz;
                float fade = saturate(i.uv.w) * _Opacity;
                if (fade <= .001) return half4(0, 0, 0, 0);
                half3 rgb;
                float alpha;
                [branch] if (i.uv.z < .5)
                {
                    // Ragged edge from world-space noise, radial streaks from the direction to the centre: both seamless.
                    float d = i.uv.x + (Fbm(xz * .22 + t * .6) - .5) * 1.6;
                    float ahead = max(-d, 0), behind = max(d, 0);
                    float2 direction = normalize(xz - _Centre.xz + 1e-4);
                    float streak = Fbm(direction * 8 + float2(i.uv.x * .05, t * .15));
                    float crest = exp(-ahead * ahead * 4) * exp(-behind * .9);
                    float band = exp(-ahead * ahead * 2) * exp(-behind * .22);
                    float wake = smoothstep(-.5, .5, d) * exp(-behind / 16);
                    rgb = Crest * crest * 1.6 + Violet * band * (.55 + .9 * streak) + Indigo * wake * (.6 + .6 * streak);
                    alpha = saturate(band * .35 + wake * .4);
                }
                else
                {
                    // Licks of dark light climbing off the crest, slanting as they rise.
                    float v = saturate(i.uv.y);
                    float n = Fbm(xz * .5 + float2(i.world.y * .6 - t * 2.2, t * .4));
                    float body = pow(1 - v, 1.8) * (.45 + 1.1 * n) * smoothstep(1, .7, v + (n - .5) * .5);
                    rgb = lerp(Violet, Crest, pow(1 - v, 4)) * body;
                    alpha = saturate(body * .12);
                }
                alpha *= fade;
                rgb = rgb * fade + half3(.015, .01, .05) * alpha;
                rgb = MixFogColor(rgb, half3(0, 0, 0), i.fog);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
