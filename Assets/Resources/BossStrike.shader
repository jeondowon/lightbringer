// Boss special-attack impacts (BossStrikeVfx). HDR additive by default so bloom picks up the violet/crimson
// energy; the scorch mode is drawn by a second, alpha-blended material instance to darken the ground under it,
// and the thorn mode by an opaque, depth-writing instance for the obsidian spikes that erupt from the ground.
// Lives in Resources so standalone builds retain it.
Shader "Lightbringer/Boss Strike"
{
    Properties
    {
        _Age ("Age", Float) = 0
        _Mode ("0 Sigil, 1 Pillar, 2 Slash, 3 Wall, 4 Scorch, 5 Thorn", Float) = 0
        _Arc ("Half Arc (radians)", Float) = 3.1416
        _Life ("Lifetime", Float) = 1
        _Descent ("Pillar Descent", Float) = 0.08
        _Sweep ("Slash Sweep", Float) = 0.14
        _Seed ("Seed", Float) = 0
        _Opacity ("Opacity", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        _ZWrite ("Z Write", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+6" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Age, _Mode, _Arc, _Life, _Descent, _Sweep, _Seed, _Opacity, _SrcBlend, _DstBlend, _ZWrite;
            CBUFFER_END

            #define TAU 6.2831853
            static const float3 Violet = float3(0.85, 0.14, 1.8);
            static const float3 Crimson = float3(1.9, 0.2, 0.5);
            static const float3 Core = float3(2.6, 1.45, 3.0);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 radialWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.radialWS = TransformObjectToWorldDir(float3(v.positionOS.x, 0, v.positionOS.z));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS + 1e-5);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float value = 0, amplitude = 0.5;
                for (int k = 0; k < 4; k++) { value += amplitude * Noise(p); p = p * 2.03 + 17.1; amplitude *= 0.5; }
                return value;
            }
            float Ring(float r, float target, float width) { return 1 - smoothstep(width, width * 2.2, abs(r - target)); }
            // Branching glowing fissures: thin valleys of a domain-warped noise field.
            float Cracks(float2 p, float seed)
            {
                float2 warp = float2(Fbm(p * 1.7 + seed), Fbm(p * 1.7 - seed + 9.3));
                float field = Fbm(p * 3.1 + warp * 1.6 + seed * 3.7);
                return 1 - smoothstep(0.0, 0.035, abs(field - 0.5));
            }
            float Cone(float2 p, float arc)
            {
                float a = abs(atan2(p.x, p.y));
                return arc > 3.1 ? 1 : 1 - smoothstep(arc - 0.05, arc + 0.03, a);
            }
            float Facing(Varyings i)
            {
                return abs(dot(normalize(i.radialWS), GetWorldSpaceNormalizeViewDir(i.positionWS)));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = max(0, _Age);
                float3 rgb = 0;
                float alpha = 1;
                if (_Mode < 0.5)
                {
                    // Ground sigil: thorned double ring with rune ticks, a fast shockwave, a white-hot flash
                    // and crimson fissures racing outwards from the centre.
                    float2 p = (i.uv - 0.5) * 2;
                    float r = length(p);
                    float a = atan2(p.x, p.y) + t * 0.35;
                    float appear = smoothstep(0, 0.05, t);
                    float fade = 1 - smoothstep(_Life * 0.4, _Life, t);
                    float rings = Ring(r, 0.93, 0.006) + Ring(r, 0.8, 0.004) * 0.8;
                    float tick = abs(frac(a / TAU * 24 + 0.5) - 0.5);
                    float runes = (1 - smoothstep(0.03, 0.06, tick)) * step(0.8, r) * step(r, 0.93) * 0.8;
                    float thornAngle = abs(frac(a / TAU * 9 + _Seed) - 0.5);
                    float thornWidth = saturate((r - 0.42) / 0.38) * 0.1;
                    float thorns = (1 - smoothstep(thornWidth - 0.012, thornWidth, thornAngle)) * step(0.42, r) * step(r, 0.8);
                    float reveal = 1 - smoothstep(saturate(t / 0.22) - 0.08, saturate(t / 0.22), r);
                    float cracks = Cracks(p * 1.4, _Seed) * reveal * (1 - smoothstep(0.15, _Life * 0.8, t)) * (1 - smoothstep(0.7, 1, r));
                    float waveR = saturate(t / 0.3) * 1.02;
                    float wave = (Ring(r, waveR, 0.025) + exp(-max(0, waveR - r) * 9) * step(r, waveR) * 0.35)
                        * (1 - smoothstep(0.12, 0.42, t));
                    float flash = exp(-r * r * 14) * (1 - smoothstep(0.02, 0.32, t));
                    float pool = (1 - r) * 0.16 * (0.75 + 0.25 * sin(t * 9));
                    rgb = Violet * (rings + runes + thorns * 0.7 + pool) + Crimson * cracks * 1.3 + Core * wave * 1.25 + Core * flash * 2.6;
                    rgb *= Cone(p, _Arc) * appear * fade * (1 - smoothstep(0.96, 1, r));
                }
                else if (_Mode < 1.5)
                {
                    // Pillar of corrupted lightning that slams down from the sky, then frays and fades.
                    float y = i.uv.y;
                    float an = i.uv.x * TAU;
                    float arrival = smoothstep(1 - t / _Descent - 0.04, 1 - t / _Descent + 0.04, y);
                    float fade = 1 - smoothstep(0.1, _Life, t);
                    float threads = pow(saturate(sin(an * 17 - y * 10 + t * 14)), 12);
                    float twist = pow(saturate(sin(an * 4 + y * 22 - t * 18)), 8);
                    float flow = pow(saturate(sin(y * 70 - t * 60 + sin(an * 5) * 2)), 6);
                    float fray = smoothstep(0.35, 0.65, Noise(float2(an * 2.5, y * 9 - t * 6) + _Seed) + (1 - fade) * 0.6);
                    float3 tint = lerp(Violet, Crimson, 0.5 + 0.5 * sin(an * 3 - y * 6 + t * 4));
                    rgb = (Violet * 0.28 + tint * (threads * 0.75 + twist * 0.6) + Core * flow * 0.18 + Core * exp(-y * 9) * 0.9)
                        * arrival * fade * (1 - fray * 0.85);
                    rgb *= (1 - smoothstep(0.82, 1, y)) * smoothstep(0, 0.5, Facing(i));
                }
                else if (_Mode < 2.5)
                {
                    // Cleave: a broad crescent that sweeps across the arc with a white-hot leading edge.
                    float x = i.uv.x, y = i.uv.y;
                    float head = t / max(0.01, _Sweep) * 1.2;
                    float behind = head - x;
                    float revealed = step(0, behind);
                    float trail = exp(-max(behind, 0) * 2.6) * revealed;
                    float leading = exp(-behind * behind * 220) * (1 - smoothstep(1.0, 1.2, head));
                    float band = exp(-pow((y - 0.76) * 6.5, 2));
                    float rim = exp(-pow((y - 0.94) * 28, 2));
                    float strands = pow(saturate(sin(y * 46 - x * 14 + t * 22 + Fbm(float2(x * 7, y * 3) + _Seed) * 5)), 6);
                    float fade = 1 - smoothstep(_Life * 0.2, _Life * 0.75, t);
                    float tips = smoothstep(0, 0.07, x) * (1 - smoothstep(0.93, 1, x)) * smoothstep(0.12, 0.42, y);
                    rgb = (Violet * band * 1.3 + Core * rim * 1.7 + Crimson * strands * band * 0.9) * trail
                        + Core * leading * (band + rim) * 2.4;
                    rgb *= fade * tips;
                }
                else if (_Mode < 3.5)
                {
                    // Shock wall: a short ring of torn violet smoke and crimson embers expanding outwards.
                    float y = i.uv.y;
                    float an = i.uv.x * TAU;
                    float n = Fbm(float2(an * 2.2, y * 2.4 - t * 3.5) + _Seed);
                    float body = pow(saturate(1 - y), 1.5) * smoothstep(0, 0.04, y);
                    float wisps = smoothstep(0.38, 0.72, n + (1 - y) * 0.35);
                    float fade = 1 - smoothstep(0.08, _Life, t);
                    float3 tint = lerp(Violet, Crimson, saturate(n * 1.6 - 0.3));
                    rgb = (tint * wisps * 1.2 + Core * exp(-y * 12) * 1.3) * body * fade;
                    rgb *= smoothstep(0, 0.3, Facing(i));
                }
                else if (_Mode < 4.5)
                {
                    // Scorch (alpha blended): ragged burnt ground with fissures that cool from crimson to black.
                    float2 p = (i.uv - 0.5) * 2;
                    float r = length(p);
                    float n = Fbm(p * 2.6 + _Seed);
                    float edge = r + (n - 0.5) * 0.45;
                    float mask = 1 - smoothstep(0.5, 0.95, edge);
                    float appear = smoothstep(0, 0.08, t);
                    float fade = 1 - smoothstep(_Life * 0.45, _Life, t);
                    float heat = 1 - smoothstep(0.1, _Life * 0.7, t);
                    float cracks = Cracks(p * 1.4, _Seed) * (1 - smoothstep(0.55, 0.9, r));
                    rgb = lerp(float3(0.02, 0.006, 0.03), Crimson * 0.7, cracks * heat);
                    alpha = saturate(mask * 0.78 + cracks * heat * 0.4) * Cone(p, _Arc) * appear * fade;
                }
                else
                {
                    // Thorn (opaque): faceted obsidian lit from above, a violet rim and crimson veins that cool
                    // from a white-hot base as the spike stands.
                    float3 n = normalize(i.normalWS);
                    float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    float lambert = saturate(dot(n, normalize(float3(0.3, 0.9, 0.25))));
                    float rim = pow(1 - saturate(abs(dot(n, view))), 3);
                    float heat = 1 - smoothstep(0.1, _Life * 0.8, t);
                    float veins = Cracks(i.uv * float2(2.2, 3.6), _Seed);
                    float root = exp(-i.uv.y * 5);
                    rgb = float3(0.03, 0.018, 0.05) * (0.35 + lambert * 1.4) + Violet * rim * 0.55
                        + Crimson * veins * (0.2 + heat * 1.1) + lerp(Crimson, Core, heat * 0.5) * root * heat * 0.9
                        + Violet * pow(saturate(i.uv.y), 6) * 0.6;
                    alpha = 1;
                }
                bool additive = _DstBlend > 0.5 && _DstBlend < 1.5;
                return additive ? half4(rgb * _Opacity, 1) : half4(rgb, saturate(alpha * _Opacity));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
