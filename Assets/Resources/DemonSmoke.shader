// The Demon King's smoke body (DemonKingSky). Procedural, no textures. Premultiplied blend so one shader both
// darkens (smoke) and adds light (eyes, violet currents). The shader lives in Resources so builds retain it.
//   _Mode 0 Smoke puff: domain-warped noise that rises and churns, lit violet on top, navy underneath, with faint
//          violet currents flowing inside; _Presence erodes it raggedly for gathering and dissipating.
//   _Mode 1 Smoke strand: a ribbon (uv.x across, uv.y along) whose edges fray into wisps as it flows.
//   _Mode 2 Eye slit: an almond of crimson light with a white-hot core (additive).
//   _Mode 3 Glow: soft additive dot.
// TEXCOORD1: x seed, y drift (metres), z rim (violet edge sheen 0..1), w glow (a strand's travelling pulse).
// _Billboard 1: each quad turns to the camera around TEXCOORD2 (xyz centre, w size), for smoke seen from any side.
Shader "Lightbringer/Demon Smoke"
{
    Properties
    {
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _RimColor ("Rim", Color) = (0.42, 0.2, 0.78, 1)
        [HDR] _GlowColor ("Current", Color) = (0.55, 0.22, 1.0, 1)
        _Mode ("Mode", Float) = 0
        _Presence ("Presence", Range(0, 1)) = 1
        _Glow ("Current Strength", Float) = 0.5
        _Charge ("Charge", Range(0, 1)) = 0
        _Flinch ("Flinch", Range(0, 1)) = 0
        _Focus ("Gather Point (object space)", Vector) = (0, 0, 0, 0)
        _Billboard ("Face Camera", Float) = 0
        _Push ("Push From Camera (metres)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "DemonSmoke"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _RimColor, _GlowColor;
                float _Mode, _Presence, _Glow, _Charge, _Flinch, _Billboard, _Push;
                float4 _Focus;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 data : TEXCOORD1; float4 centre : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 data : TEXCOORD1; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0, amplitude = 0.5;
                for (int i = 0; i < 4; i++) { sum += Noise(p) * amplitude; p = p * 2.03 + float2(1.7, 9.2); amplitude *= 0.5; }
                return sum / 0.9375;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position = input.positionOS.xyz;
                if (_Billboard > 0.5)
                {
                    float2 corner = input.uv * 2.0 - 1.0;
                    float3 right = mul((float3x3)GetWorldToObjectMatrix(), UNITY_MATRIX_V[0].xyz);
                    float3 up = mul((float3x3)GetWorldToObjectMatrix(), UNITY_MATRIX_V[1].xyz);
                    float3 camera = mul(GetWorldToObjectMatrix(), float4(_WorldSpaceCameraPos, 1)).xyz;
                    float3 centre = input.centre.xyz + normalize(input.centre.xyz - camera) * _Push;
                    position = centre + (right * corner.x + up * corner.y) * input.centre.w * 0.5;
                }
                float seed = input.data.x, drift = input.data.y, t = _Time.y;
                // Each lump drifts on its own slow orbit; charging draws the whole body in toward the eyes.
                position.x += sin(t * 0.13 + seed * 6.283) * drift;
                position.y += sin(t * 0.11 + seed * 9.17) * drift * 0.6;
                position = lerp(position, _Focus.xyz, _Charge * 0.045);
                position.x += sin(t * 47.0 + seed * 31.0) * _Flinch * 0.7;
                output.positionCS = TransformObjectToHClip(position);
                output.color = input.color;
                output.uv = input.uv;
                output.data = input.data.xzw;
                return output;
            }

            half4 Puff(Varyings input)
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p), seed = input.data.x, t = _Time.y;
                float2 q = p * 1.15 + seed * 13.7;
                float2 warp = float2(Fbm(q + float2(0, -t * 0.05)), Fbm(q + float2(5.2, 1.3) + float2(t * 0.04, 0)));
                float n = Fbm(q * 1.7 + warp * 1.6 + float2(0, -t * 0.09));
                float falloff = saturate(1.0 - r);
                falloff = falloff * falloff * (3.0 - 2.0 * falloff);
                float erosion = (1.0 - _Presence) * 0.9;
                float shape = saturate((falloff * (0.35 + n * 1.25) - 0.14 - erosion) * 1.9);
                // Billow shading: the top of each lump catches violet light, the underside sinks into navy.
                float lit = saturate(0.5 + p.y * 0.3 + (n - 0.5) * 1.1);
                half3 base = input.color.rgb * lerp(0.5, 1.45, lit);
                // Sheen only on the upper flank of a lump, never a full ring, so edges read as smoke and not bubbles.
                float edge = smoothstep(0.0, 0.3, shape) * (1.0 - smoothstep(0.3, 0.75, shape)) * 1.4 * input.data.y * saturate(p.y * 1.3 + (n - 0.5));
                // Thin violet currents, only in slowly wandering patches so the body never reads as lit marble.
                float ridge = 1.0 - abs(Fbm(q * 2.1 + warp * 2.6 + float2(t * 0.05, -t * 0.1)) * 2.0 - 1.0);
                float patches = smoothstep(0.45, 0.85, Noise(q * 0.45 + float2(t * 0.02, -t * 0.03) + seed * 3.1));
                float currents = pow(ridge, 16.0) * patches * smoothstep(0.4, 0.95, shape) * _Glow;
                float alpha = shape * input.color.a * _Color.a;
                half3 rgb = (base * _Color.rgb + _RimColor.rgb * edge) * alpha + _GlowColor.rgb * currents * input.color.a;
                return half4(rgb, alpha);
            }

            half4 Strand(Varyings input)
            {
                float across = abs(input.uv.x * 2.0 - 1.0), along = input.uv.y, seed = input.data.x, t = _Time.y;
                float n = Fbm(float2(input.uv.x * 2.4 + seed * 7.0, along * 7.0 - t * 0.35));
                float fine = Fbm(float2(input.uv.x * 7.0 + seed * 3.0, along * 19.0 - t * 0.7));
                float edge = across + (n - 0.5) * 0.95 + (fine - 0.5) * 0.4;
                float shape = smoothstep(0.0, 0.55, 1.0 - edge - (1.0 - _Presence) * 0.8);
                // A faint violet sheen on the upper flank only, so strands read as smoke, not glowing lines.
                float sheen = smoothstep(0.35, 0.8, across) * smoothstep(1.0, 0.75, across) * input.data.y * (0.5 + n);
                float ridge = 1.0 - abs(fine * 2.0 - 1.0);
                float currents = pow(ridge, 16.0) * smoothstep(0.4, 1.0, shape) * _Glow * 0.25;
                // A blow on the core runs up its stream as a violet surge along the strand's frayed body.
                float pulse = input.data.z * smoothstep(0.1, 0.7, shape) * (0.6 + ridge * 0.8) * 2.6;
                float alpha = shape * input.color.a * _Color.a;
                half3 rgb = (input.color.rgb * _Color.rgb * (0.6 + n * 0.8) + _RimColor.rgb * sheen * 0.5) * alpha
                    + _GlowColor.rgb * (currents + pulse) * input.color.a;
                return half4(rgb, alpha);
            }

            half4 Eye(Varyings input)
            {
                float2 p = input.uv * 2.0 - 1.0;
                float t = _Time.y;
                // Almond lid: widest just inside the middle, drawn to a fine point at both corners.
                float lid = 0.42 * (1.0 - p.x * p.x) * (1.0 - 0.18 * p.x);
                float d = abs(p.y - 0.06 * (1.0 - p.x * p.x)) / max(lid, 0.001);
                float body = saturate(1.0 - d) * smoothstep(1.0, 0.82, abs(p.x));
                float flicker = 0.9 + 0.1 * Noise(float2(t * 6.0, input.data.x * 17.0));
                // Crimson body with an ember-orange core; never white, so the eyes stay unmistakably red.
                float core = pow(body, 1.3), hot = pow(body, 4.0);
                half3 rgb = (_Color.rgb * core + half3(0.5, 0.12, 0.02) * _Color.r * 0.3 * hot) * flicker * input.color.a;
                return half4(rgb, 0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                int mode = (int)round(_Mode);
                if (mode == 0) return Puff(input);
                if (mode == 1) return Strand(input);
                if (mode == 2) return Eye(input);
                float r = length(input.uv * 2.0 - 1.0);
                float glow = pow(saturate(1.0 - r), 2.2) * input.color.a;
                return half4(_Color.rgb * input.color.rgb * glow, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
