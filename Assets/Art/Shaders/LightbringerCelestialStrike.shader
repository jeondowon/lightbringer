Shader "Lightbringer/Celestial Strike"
{
    Properties
    {
        _Age ("Age", Float) = 0
        _Mode ("0 Column, 1 Ground", Float) = 0
        _Descent ("Descent Duration", Float) = 0.075
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Age, _Mode, _Descent;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 radialWS : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.radialWS = TransformObjectToWorldDir(float3(v.positionOS.x, 0, v.positionOS.z));
                return o;
            }
            float ring(float r, float target, float width)
            {
                return 1 - smoothstep(width, width * 2, abs(r - target));
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Age;
                float3 gold = float3(1.8, 1.15, 0.42);
                float3 blue = float3(0.24, 0.65, 1.8);
                float3 result;
                if (_Mode < 0.5)
                {
                    float y = i.uv.y;
                    // Continuous angular harmonics close cleanly at the cylindrical seam.
                    float a = i.uv.x * 6.2831853;
                    float threads = pow(saturate(sin(a * 19 + y * 8 + t * 11)), 14);
                    float flow = pow(saturate(sin(y * 90 + t * 55 + sin(a * 7))), 8);
                    float braid = pow(saturate(sin(a * 5 - y * 18 - t * 8)), 10);
                    float arrival = smoothstep(1 - t / _Descent - 0.035, 1 - t / _Descent + 0.035, y);
                    float fade = 1 - smoothstep(0.30, 0.70, t);
                    float baseGlow = exp(-y * 18) * 0.45;
                    float3 tint = lerp(blue, gold, 0.5 + 0.5 * sin(a * 3 + y * 5));
                    result = (float3(0.65, 0.77, 1) * 0.32 + tint * (threads * 0.65 + braid * 0.55)
                        + flow * 0.10 + baseGlow) * arrival * fade;
                    result *= (0.75 + 0.25 * sin(y * 23 + t * 20)) * (1 - smoothstep(0.85, 1, y));
                    float facing = abs(dot(normalize(i.radialWS), GetWorldSpaceNormalizeViewDir(i.positionWS)));
                    result *= smoothstep(0, 0.55, facing);
                }
                else
                {
                    float2 p = (i.uv - 0.5) * 2;
                    float r = length(p);
                    float a = atan2(p.y, p.x);
                    float hit = max(0, t - _Descent);
                    float on = smoothstep(_Descent - 0.015, _Descent + 0.015, t);
                    float fade = 1 - smoothstep(0.48, 1.25, t);
                    float sigil = ring(r, 0.48, 0.004) + ring(r, 0.56, 0.006);
                    float sector = abs(frac(a / 6.2831853 * 24 + 0.5) - 0.5);
                    sigil += (1 - smoothstep(0.025, 0.055, sector)) * step(0.48, r) * step(r, 0.56);
                    float star = 0.35 + 0.055 * cos(a * 8 + hit * 0.4);
                    sigil += ring(r, star, 0.004) * 0.7;
                    float waveR = 0.18 + 0.76 * saturate(hit / 0.55);
                    float wave = ring(r, waveR, 0.012) * (1 - smoothstep(0.08, 0.7, hit));
                    float glow = exp(-r * r * 65) * (1 - smoothstep(0.06, 0.40, hit));
                    result = (gold * sigil * 0.70 * fade + blue * wave + float3(3, 2.6, 1.8) * glow) * on;
                    result *= 1 - smoothstep(0.93, 1, r);
                }
                return half4(result, 1);
            }
            ENDHLSL
        }
    }
}
