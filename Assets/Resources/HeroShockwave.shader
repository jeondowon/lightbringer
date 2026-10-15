Shader "Lightbringer/Hero Shockwave"
{
    // The hero's staff slam, sized in the script to the aura radius. _Mode 0: ground wave on a unit ground quad
    // (r = 1 is the aura edge). _Mode 1: low light wall on a unit cylinder that rides the wave front.
    // _Wave is the front's radius as a fraction of the aura (0..1), _Age seconds since the slam.
    Properties
    {
        _Mode ("Mode", Float) = 0
        _Age ("Age", Float) = 0
        _Wave ("Wave front 0..1", Float) = 0
        _Fade ("Fade", Float) = 1
        _Radius ("Aura radius (m)", Float) = 4
        _EchoStrength ("Secondary rings", Range(0, 1)) = 1
        [HDR] _WaveColor ("Wave color", Color) = (2.2, 1.55, 0.6, 1)
        [HDR] _CoreColor ("Core color", Color) = (2.6, 2.4, 1.9, 1)
        [HDR] _AccentColor ("Accent color", Color) = (0.45, 0.8, 1.8, 1)
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
                float _Mode, _Age, _Wave, _Fade, _Radius;
                float _EchoStrength;
                float4 _WaveColor, _CoreColor, _AccentColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 os : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.os = v.positionOS.xyz;
                return o;
            }
            float hash(float n) { return frac(sin(n) * 43758.5453); }
            float vnoise(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(hash(i), hash(i + 1), f * f * (3 - 2 * f));
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 gold = _WaveColor.rgb, white = _CoreColor.rgb, blue = _AccentColor.rgb;
                float t = _Age;
                float3 result;
                if (_Mode < 0.5)
                {
                    float2 p = i.os.xz * 2.2; // the quad is 2.2 aura radii wide
                    float r = length(p);
                    float a = atan2(p.y, p.x);
                    // Ragged front: the wave bulges a little unevenly around the circle.
                    float w = clamp(0.2 / max(_Radius, 0.1), 0.015, 0.12);
                    float rag = (sin(a * 5 + 1.7) * 0.012 + sin(a * 13 - t * 4) * 0.006) * w * 8;
                    float d = r - (_Wave + rag);
                    float front = exp(-d * d / (w * w)) * 1.6;
                    float wake = d < 0 ? exp(d / (w * 6)) * 0.55 : 0;
                    // Ripples trailing inside the front.
                    float ripples = d < 0 ? pow(saturate(sin(d / w * 2.2 + t * 10) * 0.5 + 0.5), 6) * exp(d / (w * 12)) * 0.4 : 0;
                    // The aura edge flashes as the wave reaches it, marking how far the slam reached.
                    float arrive = smoothstep(0.85, 1.0, _Wave);
                    float edge = exp(-pow((r - 1) / (w * 0.45), 2)) * arrive * (1.4 - smoothstep(0.45, 0.9, t));
                    float ticks = pow(saturate(cos(a * 32) * 0.5 + 0.5), 12) * exp(-pow((r - 1 + w * 1.6) / (w * 0.7), 2)) * arrive;
                    float core = exp(-r * r * 30) * exp(-t * 9) * 2.5;
                    // Healing keeps the main front, but omits trailing ripples and the separate edge ring.
                    result = white * front + gold * wake + gold * (ripples + edge + ticks * 0.8) * _EchoStrength + white * core;
                    result += blue * front * 0.2;
                    result *= 1 - smoothstep(1.02, 1.06, r);
                }
                else
                {
                    // Wall: light streaks rising off the wave front, brightest at the ground.
                    float y = saturate(i.os.y);
                    float a = atan2(i.os.z, i.os.x);
                    float streaks = 0.45 + 0.55 * pow(saturate(sin(a * 23 + sin(a * 4) * 2.5) * 0.5 + 0.5), 3);
                    float fall = pow(1 - y, 2.4);
                    result = lerp(white, gold, y) * streaks * fall * 0.9;
                }
                return half4(result * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
