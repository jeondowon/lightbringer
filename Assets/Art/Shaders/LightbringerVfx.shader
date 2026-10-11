// Procedural VFX sprite for particles and effect quads. One shader, several shapes drawn from UV, so no
// textures are needed. Premultiplied blend: _Additive = 1 glows (adds light), 0 is an alpha-blended puff
// (dust, smoke) that can darken. Vertex colour tints and fades (alpha = strength).
//   0 Soft dot     1 Star flare     2 Shock ring     3 Slash crescent
//   4 Rune ring    5 Flame tongue   6 Soft puff (dust/smoke)
Shader "Lightbringer/VFX Shape"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        [Enum(Dot,0,Star,1,Ring,2,Slash,3,RuneRing,4,Flame,5,Puff,6)] _Shape ("Shape", Float) = 0
        _Additive ("Additive (1) / Alpha Blend (0)", Range(0, 1)) = 1
        _Softness ("Softness", Range(0.5, 6)) = 1.6
        _RingWidth ("Ring Width", Range(0.01, 0.4)) = 0.08
        _GlyphCount ("Rune Glyph Count", Float) = 12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "VfxShape"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Shape;
                half _Additive;
                half _Softness;
                half _RingWidth;
                float _GlyphCount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv * 2.0 - 1.0;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(dot(i, float2(1, 57))), b = Hash(dot(i + float2(1, 0), float2(1, 57)));
                float c = Hash(dot(i + float2(0, 1), float2(1, 57))), d = Hash(dot(i + float2(1, 1), float2(1, 57)));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Anti-aliased band of half-width `width` around `centre`.
            float Band(float value, float centre, float width)
            {
                float aa = max(fwidth(value), 1e-4);
                return 1.0 - smoothstep(width, width + aa * 1.5, abs(value - centre));
            }

            float Shape(float2 p, float r)
            {
                int shape = (int)round(_Shape);
                if (shape == 1)
                {
                    // Star flare: hot core plus two thin crossing rays that taper to the edge.
                    float2 a = abs(p);
                    float core = pow(saturate(1.0 - r * 1.6), 3.0);
                    float rays = saturate(1.0 - a.x) * saturate(1.0 - a.y * 16.0) + saturate(1.0 - a.y) * saturate(1.0 - a.x * 16.0);
                    float diag = saturate(1.0 - r * 1.8) * (saturate(1.0 - abs(a.x - a.y) * 10.0));
                    return saturate(core + rays * rays * 0.9 + diag * 0.35);
                }
                if (shape == 2)
                {
                    // Shock ring: a bright rim with a faint fill and a soft trailing edge inside.
                    float rim = exp(-pow((r - 0.82) / max(_RingWidth, 0.01), 2.0));
                    float inner = smoothstep(0.2, 0.82, r) * step(r, 0.82) * 0.18;
                    return saturate(rim + inner) * step(r, 1.0);
                }
                if (shape == 3)
                {
                    // Slash crescent: the gap between two circles, brightest along the leading curve, tapered tips.
                    float outer = 1.0 - smoothstep(0.86, 0.96, r);
                    float inner = smoothstep(0.62, 0.8, length(p - float2(0.0, -0.32)));
                    float crescent = outer * inner * step(-0.2, p.y);
                    float edge = smoothstep(0.55, 0.92, r);
                    float tips = saturate(1.0 - abs(p.x) * 1.05);
                    return saturate(crescent * (0.35 + edge) * tips * 1.6);
                }
                if (shape == 4)
                {
                    // Rune ring: double circle with glyph ticks between, for blessing marks and casting circles.
                    float outerRing = Band(r, 0.9, 0.03);
                    float innerRing = Band(r, 0.68, 0.018);
                    float angle = atan2(p.y, p.x) / (2.0 * PI) + 0.5;
                    float cell = angle * max(_GlyphCount, 3.0);
                    float id = floor(cell);
                    float local = frac(cell);
                    float glyph = Band(local, 0.5, 0.06 + Hash(id) * 0.08) * Band(r, 0.79, 0.06 + Hash(id + 3.1) * 0.03);
                    float pip = Band(local, 0.5, 0.12) * Band(r, 0.79, 0.015) * step(0.5, Hash(id + 7.7));
                    float fill = smoothstep(0.0, 0.9, r) * step(r, 0.9) * 0.12;
                    return saturate(outerRing + innerRing * 0.8 + max(glyph, pip) * 0.9 + fill);
                }
                if (shape == 5)
                {
                    // Flame tongue: a teardrop that narrows upward, licked by scrolling noise.
                    float y = p.y * 0.5 + 0.5;
                    float width = lerp(0.85, 0.05, pow(y, 0.8));
                    float wobble = (Noise(float2(p.x * 3.0, y * 4.0 - _Time.y * 3.0)) - 0.5) * 0.5 * y;
                    float body = 1.0 - smoothstep(width * 0.55, width, abs(p.x + wobble));
                    float base = smoothstep(-1.0, -0.6, p.y);
                    return saturate(body * base * (1.15 - y * 0.6));
                }
                if (shape == 6)
                {
                    // Soft puff: lumpy round cloud for dust and smoke.
                    float n = Noise(p * 2.5 + 3.7) * 0.6 + Noise(p * 5.0 + 1.3) * 0.4;
                    return saturate(pow(saturate(1.0 - r), _Softness) * (0.55 + n * 0.9));
                }
                return pow(saturate(1.0 - r), _Softness);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv;
                float r = length(p);
                float value = Shape(p, r) * input.color.a;
                half3 rgb = _Color.rgb * input.color.rgb * value;
                // Fog fades additive light out (never toward the fog colour, which would light the haze).
                rgb = MixFogColor(rgb, half3(0, 0, 0), input.fogFactor);
                return half4(rgb, value * (1.0h - _Additive) * _Color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
