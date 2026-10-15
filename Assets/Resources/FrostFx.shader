Shader "Lightbringer/Frost FX"
{
    // Ice spell visuals, selected by _Mode: 0 bolt crystal, 1 camera-facing glow, 2 ice shell over a frozen unit,
    // 3 ground frost, 4 trail ribbon. Premultiplied alpha: colour adds light, alpha lets ice tint what it covers.
    Properties
    {
        _Mode ("Mode", Float) = 0
        _Age ("Age", Float) = 0
        _Fade ("Fade", Float) = 1
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Mode, _Age, _Fade, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 os : TEXCOORD1;
                float3 positionWS : TEXCOORD2; float3 normalWS : TEXCOORD3; float4 color : COLOR;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.os = v.positionOS.xyz;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }
            float hash(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            // Thin bright lines where a cell pattern changes: reads as cracks / frost veins.
            float veins(float2 p)
            {
                float n = vnoise(p) + 0.5 * vnoise(p * 2.1 + 7.3);
                return pow(1 - abs(frac(n * 3) - 0.5) * 2, 10);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 deep = float3(0.10, 0.42, 1.0), ice = float3(0.45, 0.85, 1.3), white = float3(1.6, 1.9, 2.2);
                float t = _Age;
                float3 n = normalize(i.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float facing = saturate(abs(dot(n, view)));
                float fresnel = pow(1 - facing, 2.2);
                float3 color = 0;
                float alpha = 0;
                int mode = (int)(_Mode + 0.5);
                if (mode == 0)
                {
                    // Bolt crystal: white-hot centre facets with a cyan rim and a travelling glint.
                    float glint = pow(saturate(sin(dot(i.os, float3(9, 7, 5)) + t * 18 + _Seed)), 12);
                    color = lerp(white, ice, fresnel) * (1.1 + fresnel * 1.2) + white * glint;
                    alpha = 0.9;
                }
                else if (mode == 1)
                {
                    // Drifting cold mist; the radial mask reaches zero before the quad boundary.
                    float2 p = (i.uv - 0.5) * 2;
                    float r = length(p);
                    float edge = 1 - smoothstep(0.55, 0.98, r);
                    float mist = vnoise(p * 4.5 + float2(t * 1.8, -t * 2.3) + _Seed);
                    float wisps = vnoise(p * 9 + float2(-t, t * 3) + _Seed * 2);
                    float halo = exp(-r * r * 5) * (0.25 + mist * 0.8) * edge;
                    color = ice * halo + white * exp(-r * r * 28) * wisps * edge * 0.45;
                    alpha = 0;
                }
                else if (mode == 2)
                {
                    // Ice shell: a pale blue body that tints the frozen unit, bright facet rims, cracks and sparkle.
                    float2 q = i.positionWS.xz * 3.2 + i.positionWS.y * 2.1 + _Seed;
                    float crack = veins(q);
                    float sparkle = pow(hash(floor(i.positionWS.xy * 40 + i.positionWS.z * 13)), 40) * (0.5 + 0.5 * sin(t * 9 + _Seed * 3));
                    float form = smoothstep(0, 0.25, t);
                    color = deep * 0.18 + ice * (fresnel * 0.9 + 0.12) + white * (crack * 0.35 + sparkle * 2.5);
                    alpha = (0.42 + fresnel * 0.4) * form;
                    color *= form;
                }
                else if (mode == 3)
                {
                    // Ground frost: a crystalline star of ice veins fading out from the frozen unit's feet.
                    float2 p = (i.uv - 0.5) * 2;
                    float r = length(p);
                    float a = atan2(p.y, p.x);
                    float star = pow(saturate(cos(a * 6 + _Seed) * 0.5 + 0.5), 6) * (1 - r);
                    float vein = veins(p * 4 + _Seed);
                    float body = saturate(1 - r * 1.1);
                    float grow = smoothstep(0, 0.3, t);
                    float mask = body * smoothstep(r - 0.1, r + 0.1, grow);
                    color = (ice * 0.35 + white * (vein * 0.5 + star * 0.6)) * mask;
                    alpha = (0.35 + vein * 0.25) * mask;
                }
                else
                {
                    // Trail ribbon: uv.x runs along the trail (0 at the head); vertex alpha fades it.
                    float along = i.uv.x;
                    float across = abs(i.uv.y - 0.5) * 2;
                    float core = exp(-across * across * 6) * (1 - smoothstep(0.55, 1, across));
                    float flake = vnoise(float2(along * 14 - t * 20, i.uv.y * 3 + _Seed));
                    color = lerp(white, ice, saturate(along * 1.4)) * core * (0.6 + flake * 0.8) * i.color.a;
                    alpha = 0;
                }
                return half4(color * _Fade, alpha * _Fade);
            }
            ENDHLSL
        }
    }
}
