// Procedural rune circle for the hero aura, drawn on a ground quad (UV 0..1 spans the diameter).
// Additive light: outer ring, rotating glyph band, inner star and a soft fill toward the edge.
Shader "Lightbringer/Aura Runes"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.55, 0.75, 1.6, 1)
        _FillAlpha ("Fill Strength", Range(0, 1)) = 0.12
        _GlyphCount ("Glyph Count", Float) = 32
        _RotationSpeed ("Rotation Speed", Float) = 0.08
        _LineWidth ("Line Width (radius fraction)", Range(0.002, 0.05)) = 0.012
        _Pulse ("Pulse", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AuraRunes"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FillAlpha;
                float _GlyphCount;
                float _RotationSpeed;
                float _LineWidth;
                half _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            // Anti-aliased band centred on `centre` with half-width `width`.
            float Band(float value, float centre, float width)
            {
                float aa = max(fwidth(value), 1e-4);
                return 1.0 - smoothstep(width, width + aa * 1.5, abs(value - centre));
            }

            float Glyph(float2 cell, float seed, float width)
            {
                // Each rune is a few strokes chosen from the seed inside a unit cell.
                float g = 0;
                // Branch-free so fwidth stays valid across neighbouring glyph cells.
                g = max(g, Band(cell.x, 0.5, width) * step(0.15, cell.y) * step(cell.y, 0.85) * step(0.3, Hash(seed)));
                g = max(g, Band(cell.y, 0.5 + (Hash(seed + 3.1) - 0.5) * 0.4, width) * step(0.2, cell.x) * step(cell.x, 0.8) * step(0.5, Hash(seed + 1.7)));
                g = max(g, Band(cell.x - cell.y * 0.8, 0.1, width * 0.8) * step(0.15, cell.y) * step(cell.y, 0.85) * step(0.55, Hash(seed + 2.3)));
                g = max(g, Band(length(cell - float2(0.5, 0.75)), 0.12, width * 0.7) * step(0.7, Hash(seed + 4.9)));
                return g;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);
                clip(1.02 - r);
                float angle = atan2(input.uv.y, input.uv.x) / (2.0 * PI) + 0.5;
                float t = _Time.y;
                float w = _LineWidth;

                float light = 0;
                light += Band(r, 0.985, w * 1.2);
                light += Band(r, 0.93, w * 0.6) * 0.8;
                light += Band(r, 0.80, w * 0.6) * 0.8;

                // Rotating glyph band between the two inner rings.
                float spun = frac(angle + t * _RotationSpeed);
                float count = max(8.0, floor(_GlyphCount));
                float index = floor(spun * count);
                float2 cell = float2(frac(spun * count), saturate((r - 0.815) / 0.1));
                float inBand = step(0.815, r) * step(r, 0.915);
                light += Glyph(cell, index, 0.06) * inBand;

                // Counter-rotating inner star and ring.
                float inner = frac(angle - t * _RotationSpeed * 1.5);
                float star = abs(frac(inner * 8.0) - 0.5) * 2.0;
                light += Band(r, lerp(0.42, 0.6, star), w * 0.5) * step(r, 0.62) * 0.7;
                light += Band(r, 0.3, w * 0.5) * 0.6;
                float spokes = abs(frac(inner * 4.0 + 0.125) - 0.5);
                light += Band(spokes * r * 6.0, 0.0, w * 3.0) * step(0.3, r) * step(r, 0.78) * 0.35;

                light += _FillAlpha * smoothstep(0.2, 1.0, r) * smoothstep(1.0, 0.95, r);
                light *= 1.0 - _Pulse + _Pulse * (0.5 + 0.5 * sin(t * 2.2));
                half3 color = _Color.rgb * light;
                // Additive: fade toward black in fog rather than toward the fog colour.
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
