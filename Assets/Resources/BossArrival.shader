// Grounded surfaces for the Warlord's arrival: lit volumetric smoke billboards and a molten fissure decal.
// Premultiplied blend so smoke can darken the scene while fissures add light. Lives in Resources so
// standalone builds keep it (materials are created at runtime by BossArrivalVfx).
//   _Mode 0  Smoke particle. Streams: UV + StableRandom.xy (TEXCOORD0), AgePercent (TEXCOORD1.x).
//   _Mode 1  Fissure decal on a ground quad: charred crater with glowing branching cracks.
Shader "Lightbringer/Boss Arrival"
{
    Properties
    {
        [HDR] _Glow ("Underglow", Color) = (2.2, 0.3, 0.1, 1)
        _GlowAmount ("Underglow Amount", Float) = 0
        _Mode ("Mode", Float) = 0
        _Progress ("Crack Spread", Float) = 1
        _Heat ("Crack Heat", Float) = 1
        _Opacity ("Opacity", Float) = 1
        _Seed ("Seed", Float) = 0
        _SoftFade ("Soft Particle Distance", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "BossArrival"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_local _ _SOFT_PARTICLES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Glow;
                float _GlowAmount, _Mode, _Progress, _Heat, _Opacity, _Seed, _SoftFade;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; float4 misc : TEXCOORD1; };
            struct V
            {
                float4 positionCS : SV_POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; float4 misc : TEXCOORD1;
                float4 screen : TEXCOORD2; float eyeDepth : TEXCOORD3; half fog : TEXCOORD4;
            };

            V Vert(A v)
            {
                V o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.color = v.color; o.uv = v.uv; o.misc = v.misc;
                o.screen = ComputeScreenPos(o.positionCS);
                o.eyeDepth = -TransformWorldToView(world).z;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
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
            // Distance to the nearest Voronoi cell border (branching crack network).
            float CellEdge(float2 x)
            {
                float2 n = floor(x), f = frac(x), nearest = 0, offset = 0;
                float best = 8;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j), r = g + Hash2(n + g) - f;
                    float d = dot(r, r);
                    if (d < best) { best = d; nearest = r; offset = g; }
                }
                best = 8;
                for (int jj = -2; jj <= 2; jj++)
                for (int ii = -2; ii <= 2; ii++)
                {
                    float2 g = offset + float2(ii, jj), r = g + Hash2(n + g) - f;
                    if (dot(nearest - r, nearest - r) > 1e-5) best = min(best, dot(.5 * (nearest + r), normalize(r - nearest)));
                }
                return best;
            }

            half4 Smoke(V i)
            {
                float2 p = i.uv.xy * 2 - 1;
                float2 seed = i.uv.zw;
                float age = saturate(i.misc.x);
                float spin = seed.x * 6.2831;
                float2 q = float2(p.x * cos(spin) - p.y * sin(spin), p.x * sin(spin) + p.y * cos(spin));
                // No round outline may survive: the falloff is sampled through a strong noise warp, so each puff is
                // a ragged, lopsided wisp whose edge is eaten by noise rather than a disc with a soft rim.
                float2 warp = float2(Fbm(q * 1.2 + seed * 31 + age * .3), Fbm(q * 1.2 + seed.yx * 17 - age * .25)) - .5;
                float2 pw = p + warp * 1.1;
                float r = length(pw);
                // Billowing lobes: noise churns slowly with age so the puff rolls instead of sliding.
                float n = Fbm(q * 2.1 + warp * 1.6 + seed * 41 + float2(age * .4, -age * .6));
                float detail = Fbm(q * 5.2 + seed.yx * 23 - age * .9);
                float body = saturate(1 - r * r);
                float density = body * (n * 1.35 + detail * .35) - .42 - age * age * .5;
                density = saturate(density * 1.7);
                density *= density * (3 - 2 * density);
                // Hard guarantee that nothing reaches the quad border, without drawing a circle of its own.
                density *= 1 - smoothstep(.55, 1, length(p));
                float alpha = density * i.color.a * _Opacity;
                if (alpha <= .002) return half4(0, 0, 0, 0);

                // Lumps from the warp field rather than a sphere: a sphere normal lights the puff as a round disc.
                float3 normalVS = normalize(float3(pw * .35 + warp * 1.2 + (float2(n, detail) - .5) * .6, .85));
                float3 normalWS = TransformViewToWorldDir(normalVS, true);
                Light sun = GetMainLight();
                float wrap = saturate(dot(normalWS, sun.direction) * .55 + .45);
                half3 ambient = SampleSH(normalWS);
                half3 rgb = i.color.rgb * (sun.color * wrap * .9 + ambient);
                rgb *= lerp(1, .5, density * body);                       // thick cores self-shadow
                float under = saturate(.45 - normalWS.y * .6);
                float heat = _GlowAmount * saturate(1 - age * 1.5) * (.35 + body * .65);
                rgb += _Glow.rgb * under * under * heat;                    // fire light from below, cooling as it rises
                rgb = MixFog(rgb, i.fog);
                #if defined(_SOFT_PARTICLES)
                    float2 screenUV = i.screen.xy / i.screen.w;
                    float scene = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    alpha *= saturate((scene - i.eyeDepth) / max(_SoftFade, .01));
                #endif
                return half4(rgb * alpha, alpha);
            }

            half4 Fissure(V i)
            {
                float2 p = i.uv.xy * 2 - 1;
                float r = length(p);
                float2 warp = (float2(Noise(p * 3 + _Seed), Noise(p * 3 + _Seed + 9.3)) - .5) * .55;
                // Stretch cells tangentially so the cracks run outward from the centre like a split crust.
                float angle = atan2(p.y, p.x);
                float2 polar = float2(angle * 1.6, pow(r, .8) * 4.5) + warp * 1.5;
                float edge = min(CellEdge(polar + _Seed * 3.1), CellEdge(p * 5.5 + warp + _Seed));
                float reach = 1 - smoothstep(_Progress - .1, _Progress, r);
                float width = lerp(.075, .015, saturate(r));
                float crack = (1 - smoothstep(width * .35, width, edge)) * reach;
                float core = (1 - smoothstep(0, width * .3, edge)) * reach;
                float pit = exp(-r * r * 22) * saturate(_Progress * 3);
                float flicker = .85 + .15 * Noise(float2(_Seed, _Time.y * 6));
                float heat = _Heat * flicker * (1 - smoothstep(.4, 1, r) * .6);
                half3 hot = lerp(half3(1.1, .07, .03), half3(3.2, 1.3, .4), saturate(core * heat + pit * heat));
                half3 glow = hot * (crack * .9 + core * 1.3 + pit * 1.6) * heat;

                float mottled = .7 + .3 * Fbm(p * 7 + _Seed);
                float charred = (1 - smoothstep(.3, .98, r + (mottled - .7) * .4)) * saturate(_Progress * 1.5) * .92;
                float alpha = saturate(charred + crack * .5) * _Opacity;
                half3 rgb = half3(.03, .022, .02) * mottled * alpha + glow * _Opacity;
                rgb = MixFogColor(rgb, half3(0, 0, 0), i.fog);
                return half4(rgb, alpha);
            }

            half4 Frag(V i) : SV_Target
            {
                // A branch, not a ternary: HLSL evaluates both sides of ?: and the Voronoi pass is costly.
                [branch] if (_Mode < .5) return Smoke(i);
                return Fissure(i);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
