// Three-channel authored sigil plus analytic HDR rings; the second mode is a low floating ribbon.
Shader "Lightbringer/Aura Runes"
{
    Properties
    {
        [NoScaleOffset] _SigilTex ("Sigil mask (runes / gold / filigree)", 2D) = "black" {}
        [HDR] _Color ("Moonlight", Color) = (0.65, 1.05, 2.4, 1)
        [HDR] _GoldColor ("Warm Gold", Color) = (2.7, 1.65, 0.65, 1)
        _Intensity ("Light Intensity", Range(0, 5)) = 1
        _FillAlpha ("Ground Mist", Range(0, 1)) = 0.04
        _RotationSpeed ("Rune Rotation (radians/sec)", Float) = 0.28
        _LineWidth ("Ring Width", Range(0.0005, 0.02)) = 0.0025
        _Pulse ("Breathing", Range(0, 1)) = 0.08
        [HideInInspector] _Layer ("Ground / Ribbon", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "AuraRunes"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SigilTex); SAMPLER(sampler_SigilTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _GoldColor;
                float _Intensity, _FillAlpha, _RotationSpeed, _LineWidth, _Pulse, _Layer;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + 1.0), f.x), f.y);
            }
            float Band(float v, float centre, float width)
            {
                float aa = max(fwidth(v), 0.0001);
                return (1.0 - smoothstep(width, width + aa, abs(v - centre))) * min(1.0, width * 2.0 / aa);
            }
            float2 Rotate(float2 p, float a)
            {
                float s, c; sincos(a, s, c);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                half3 color;
                if (_Layer > 0.5)
                {
                    // Broken arcs, with different heights, never an opaque cylindrical wall.
                    float angle = input.uv.x * 2.0 * PI;
                    float h = input.uv.y;
                    float path = 0.28 + 0.14 * sin(angle * 3.0 - t * 0.45);
                    float arc = pow(saturate(0.5 + 0.5 * sin(angle * 2.0 + t * 0.32)), 4.0);
                    float ribbonLine = Band(h, path, 0.012);
                    float halo = exp2(-1200.0 * (h - path) * (h - path));
                    float secondPath = 0.60 + 0.15 * sin(angle * 2.0 + t * 0.32);
                    float secondArc = pow(saturate(0.5 + 0.5 * sin(angle * 3.0 - t * 0.25)), 8.0);
                    color = _GoldColor.rgb * (ribbonLine + halo * 0.16) * arc * 0.65;
                    color += _Color.rgb * Band(h, secondPath, 0.009) * secondArc * 0.7;
                }
                else
                {
                    float2 p = input.uv * 2.0 - 1.0;
                    float r = length(p);
                    clip(1.0 - r);
                    float angle = atan2(p.y, p.x);
                    float2 spun = Rotate(p, t * _RotationSpeed);
                    float2 inner = Rotate(p, -t * _RotationSpeed * 0.6);
                    half3 mask = SAMPLE_TEXTURE2D(_SigilTex, sampler_SigilTex, spun * 0.5 + 0.5).rgb;
                    float filigree = SAMPLE_TEXTURE2D(_SigilTex, sampler_SigilTex, inner * 0.5 + 0.5).b;
                    float w = _LineWidth;
                    float blue = mask.r * 1.15 + filigree * 0.7;
                    blue += Band(r, 0.81, w * 0.65) * 0.8 + Band(r, 0.918, w * 0.65) * 0.9;
                    blue += Band(r, 0.785, w * 0.35) * 0.45;
                    float gold = mask.g * 0.9 + Band(r, 0.981, w) * 1.25;
                    gold += Band(r, 0.936, w * 0.45) * 0.6;
                    float halo = exp2(-8500.0 * (r - 0.979) * (r - 0.979));
                    gold += halo * 0.16;
                    float mist = Noise(p * 7.0 + float2(t * 0.045, -t * 0.03));
                    mist *= Noise(p * 15.0 - t * 0.04);
                    float edgeFade = (1.0 - smoothstep(0.9, 0.99, r)) * smoothstep(0.15, 0.6, r);
                    blue += mist * _FillAlpha * edgeFade;
                    // Slowly travelling pinpoints along the outer ring.
                    float spark = pow(saturate(cos(angle * 8.0 - t * 0.4)), 64.0);
                    gold += spark * Band(r, 0.962, w * 2.2) * 1.4;
                    color = _Color.rgb * blue + _GoldColor.rgb * gold;
                }
                color *= _Intensity * (1.0 - _Pulse * (0.5 + 0.5 * sin(t * 1.6)));
                return half4(MixFogColor(color, half3(0, 0, 0), input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
