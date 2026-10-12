// Ranged attack presentation for troops and enemies (ShotVfx): comet bolt heads, arrowhead glints, energy
// trails and spell impacts. Premultiplied blend: rgb adds light, alpha darkens what is behind, so corrupted
// shots can carry dark smoke and glowing fire in one draw. Allied shots are light and runes (blue-white with
// holy gold); enemy shots are crimson fire, violet edges and smoke. Lives in Resources for standalone builds.
//   0 Comet head (camera-facing, stretched along flight)   1 Glint (camera-facing star)
//   2 Impact sigil (ground quad)   3 Impact column (cylinder)   4 Trail ribbon (Trail/LineRenderer)
Shader "Lightbringer/Shot FX"
{
    Properties
    {
        _Mode ("Mode", Float) = 0
        _Allied ("Allied", Float) = 1
        _Kind ("0 Arrow, 1 Bolt", Float) = 1
        _Stretch ("Head Stretch", Float) = 1
        _Age ("Age", Float) = 0
        _Life ("Lifetime", Float) = 1
        _Seed ("Seed", Float) = 0
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+4" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Mode, _Allied, _Kind, _Stretch, _Age, _Life, _Seed, _Opacity;
            CBUFFER_END

            #define TAU 6.2831853
            static const float3 Arcane = float3(0.5, 0.95, 2.4);
            static const float3 Holy = float3(2.3, 1.55, 0.55);
            static const float3 WhiteHot = float3(2.8, 2.8, 3.0);
            static const float3 Crimson = float3(2.1, 0.2, 0.45);
            static const float3 Violet = float3(0.85, 0.14, 1.7);
            static const float3 Ember = float3(2.6, 0.95, 0.9);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 radialWS : TEXCOORD2;
                half4 color : COLOR;
                float stretch : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.uv = v.uv;
                o.color = v.color;
                o.stretch = 1;
                o.radialWS = TransformObjectToWorldDir(float3(v.positionOS.x, 0, v.positionOS.z), false);
                if (_Mode < 1.5)
                {
                    // Camera-facing quad, stretched behind the head along the screen-space flight direction.
                    float3 centreWS = TransformObjectToWorld(float3(0, 0, 0));
                    float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                    float3 forwardVS = TransformWorldToViewDir(normalize(float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22)), false);
                    float onScreen = length(forwardVS.xy);
                    float2 along = onScreen > 0.001 ? forwardVS.xy / onScreen : float2(1, 0);
                    float2 across = float2(-along.y, along.x);
                    float stretch = 1 + (max(_Stretch, 1) - 1) * saturate(onScreen * 1.4);
                    float2 local = v.positionOS.xy;
                    float2 offset = along * (local.x * stretch - (stretch - 1) * 0.5) * size + across * local.y * size;
                    float3 positionVS = TransformWorldToView(centreWS) + float3(offset, size * 0.25);
                    o.positionCS = TransformWViewToHClip(positionVS);
                    o.positionWS = centreWS;
                    o.stretch = stretch;
                }
                else
                {
                    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                }
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
                for (int k = 0; k < 3; k++) { value += amplitude * Noise(p); p = p * 2.07 + 13.3; amplitude *= 0.5; }
                return value;
            }
            float Ring(float r, float target, float width) { return 1 - smoothstep(width, width * 2.2, abs(r - target)); }
            float Cracks(float2 p, float seed)
            {
                float2 warp = float2(Fbm(p * 1.7 + seed), Fbm(p * 1.7 - seed + 9.3));
                float field = Fbm(p * 3.1 + warp * 1.6 + seed * 3.7);
                return 1 - smoothstep(0.0, 0.04, abs(field - 0.5));
            }
            float Facing(Varyings i)
            {
                return abs(dot(i.radialWS * rsqrt(max(dot(i.radialWS, i.radialWS), 1e-6)), GetWorldSpaceNormalizeViewDir(i.positionWS)));
            }

            // Bolt head: a white-hot core in a churning plasma shell with a flame tail licking behind it.
            // Light bolts carry a turning rune ring; corrupted bolts wear a shell of dark smoke.
            float4 Comet(Varyings i, bool allied)
            {
                float s = i.stretch;
                float time = _Time.y + _Seed * 7.1;
                float2 p = float2((i.uv.x - 1 + 0.5 / s) * s * 2, (i.uv.y - 0.5) * 2);
                float r = length(p);
                float a = atan2(p.y, p.x);
                float tailLength = max(2 * s - 1, 0.001);
                float behind = saturate(-p.x / tailLength);
                float width = lerp(0.62, 0.04, pow(behind, 0.7));
                float lick = (Noise(float2(p.x * 2.4 + time * 14, p.y * 3 + _Seed)) - 0.5) * 0.5 * behind;
                float tail = (1 - smoothstep(width * 0.35, width, abs(p.y + lick))) * step(p.x, 0) * (1 - behind);
                float churn = Fbm(float2(a * 1.6 + time * 5, r * 4 - time * 9) + _Seed);
                float shell = (1 - smoothstep(0.35, 0.95, r)) * (0.55 + churn * 0.9);
                float core = exp(-r * r * 9);
                float body = saturate(shell + tail * (0.5 + churn * 0.7));
                float3 rgb;
                float alpha = 0;
                if (allied)
                {
                    float spin = a + time * 4;
                    float runeRing = Ring(r, 0.8, 0.025) * (0.55 + 0.45 * pow(saturate(sin(spin * 6)), 4));
                    float nodes = pow(saturate(cos(spin * 3)), 40) * Ring(r, 0.8, 0.09);
                    float3 tint = lerp(Arcane, Holy, saturate(churn * 1.3 - 0.45));
                    rgb = tint * body * 0.9 + WhiteHot * core * 1.6 + Holy * (runeRing * 0.8 + nodes * 1.6) * (1 - behind);
                }
                else
                {
                    float smokeN = Fbm(float2(p.x * 1.8 + time * 6, p.y * 2.2 - time) + _Seed * 3);
                    float smoke = smoothstep(0.38, 0.75, smokeN + body * 0.4) * saturate(body * 1.6) * (1 - core);
                    float3 tint = lerp(Crimson, Violet, saturate(churn * 1.5 - 0.35));
                    rgb = tint * body * (1 - smoke * 0.75) + Ember * core * 1.4;
                    alpha = smoke * 0.75;
                }
                return float4(rgb, alpha);
            }

            // Small four-point star that flickers on an arrowhead or an orbiting spark.
            float4 Glint(Varyings i, bool allied)
            {
                float2 p = (i.uv - 0.5) * 2;
                float time = _Time.y * 26 + _Seed * 11;
                float2 q = float2(p.x * 0.92 - p.y * 0.38, p.x * 0.38 + p.y * 0.92);
                float2 b = abs(q);
                float rays = exp(-b.y * 26) * saturate(1 - b.x) + exp(-b.x * 26) * saturate(1 - b.y);
                float r = length(p);
                float core = exp(-r * r * 20);
                float halo = exp(-r * r * 4) * 0.35;
                float pulse = 0.75 + 0.25 * sin(time) * sin(time * 0.37 + 1.3);
                float3 tint = allied ? lerp(Holy, WhiteHot, 0.35) : Crimson;
                return float4((tint * (rays * 0.9 + halo) + WhiteHot * core * (allied ? 1 : 0.55)) * pulse, 0);
            }

            // Impact on the ground. Light: a rune circle with a hexagram, rays and a racing wave.
            // Corruption: a ragged splash of fire and thorns over cracked, scorched ground.
            float4 Sigil(Varyings i, bool allied)
            {
                float t = max(0, _Age);
                float2 p = (i.uv - 0.5) * 2;
                float r = length(p);
                float a = atan2(p.x, p.y);
                float fade = 1 - smoothstep(_Life * 0.35, _Life, t);
                float appear = smoothstep(0, 0.04, t);
                float waveR = saturate(t / 0.22) * 1.02;
                float wave = Ring(r, waveR, 0.03) * (1 - smoothstep(0.1, 0.32, t));
                float flash = exp(-r * r * 12) * (1 - smoothstep(0.02, 0.26, t));
                float edge = 1 - smoothstep(0.95, 1, r);
                float3 rgb;
                float alpha = 0;
                if (allied)
                {
                    float turn = a + t * 0.9;
                    float rings = Ring(r, 0.9, 0.008) + Ring(r, 0.74, 0.005) * 0.8 + Ring(r, 0.36, 0.006) * 0.7;
                    float tick = abs(frac(turn / TAU * 20 + 0.5) - 0.5);
                    float glyphs = (1 - smoothstep(0.05, 0.09, tick)) * step(0.76, r) * step(r, 0.88)
                        * step(0.35, Hash(float2(floor(turn / TAU * 20 + 0.5), 3.1)));
                    // Hexagram: six lines at distance 0.37 from the centre, clipped to the inner ring.
                    float lines = 0;
                    for (int k = 0; k < 6; k++)
                    {
                        float angle = k * TAU / 6 + t * 0.9;
                        float d = abs(dot(p, float2(cos(angle), sin(angle))) - 0.37);
                        lines += 1 - smoothstep(0.006, 0.016, d);
                    }
                    lines = saturate(lines) * step(r, 0.74);
                    float rays = pow(saturate(cos(a * 8 - t * 2)), 18) * (1 - smoothstep(0.1, 0.95, r)) * 0.6;
                    float reveal = smoothstep(saturate(t / 0.16) - 0.15, saturate(t / 0.16), 1 - r) + step(0.16, t);
                    rgb = Holy * (rings + glyphs * 0.9) + Arcane * (lines * 0.8 + rays) * saturate(reveal)
                        + WhiteHot * (wave * 1.3 + flash * 2.2);
                    rgb *= appear * fade * edge;
                }
                else
                {
                    float n = Fbm(p * 2.8 + _Seed);
                    float splashR = saturate(t / 0.14) * 0.85;
                    float ragged = r + (n - 0.5) * 0.4;
                    float splash = 1 - smoothstep(splashR - 0.1, splashR, ragged);
                    float thorn = abs(frac(a / TAU * 7 + _Seed) - 0.5);
                    float thornWidth = saturate((r - 0.3) / 0.6) * 0.09;
                    float thorns = (1 - smoothstep(thornWidth - 0.012, thornWidth, thorn)) * step(0.3, r) * step(r, 0.92)
                        * (1 - smoothstep(0.12, 0.5, t));
                    float heat = 1 - smoothstep(0.1, _Life * 0.75, t);
                    float cracks = Cracks(p * 1.3, _Seed) * splash * heat * (1 - smoothstep(0.6, 0.95, r));
                    float scorch = splash * (1 - smoothstep(0.5, 0.95, ragged));
                    rgb = Crimson * (cracks * 1.3 + thorns * 0.8 + wave * 0.9) + Violet * splash * (1 - r) * 0.35 * heat
                        + Ember * flash * 1.8;
                    rgb *= appear * fade * edge;
                    alpha = scorch * 0.6 * appear * fade;
                }
                return float4(rgb, alpha);
            }

            // Short rising burst. Light: streaks racing up a column. Corruption: torn flame tongues with smoky tips.
            float4 Column(Varyings i, bool allied)
            {
                float t = max(0, _Age);
                float y = i.uv.y;
                float an = i.uv.x * TAU;
                float fade = 1 - smoothstep(_Life * 0.3, _Life, t);
                float facing = smoothstep(0, 0.45, Facing(i));
                float3 rgb;
                float alpha = 0;
                if (allied)
                {
                    float streaks = pow(saturate(sin(an * 9 + _Seed * 3) * 0.5 + 0.5), 10);
                    float flow = pow(saturate(sin(y * 26 - t * 40 + sin(an * 4) * 1.5)), 5);
                    float top = 1 - smoothstep(0.25, 1, y);
                    rgb = (Arcane * (0.3 + streaks * 0.9) + Holy * flow * 0.6 + WhiteHot * exp(-y * 8) * 0.9) * top;
                }
                else
                {
                    float n = Fbm(float2(an * 1.6, y * 2.8 - t * 6) + _Seed);
                    float tongues = smoothstep(0.32, 0.6, n + (1 - y) * 0.55 - y * 0.35);
                    float smoke = smoothstep(0.4, 0.7, n) * smoothstep(0.35, 0.9, y) * (1 - tongues);
                    rgb = lerp(Crimson, Ember, (1 - y) * 0.6) * tongues * (1 - smoothstep(0.55, 1, y)) + Violet * smoke * 0.2;
                    alpha = smoke * 0.55 * (1 - smoothstep(0.85, 1, y)) * fade * facing;
                }
                rgb *= fade * facing * smoothstep(0, 0.04, y);
                return float4(rgb, alpha);
            }

            // Trail ribbon (uv.x 0 at the head to 1 at the tail, uv.y across). Arrows: a keen streak with
            // wind-cut wisps. Bolts: a twisting plasma ribbon (light) or burning smoke (corruption).
            float4 Ribbon(Varyings i, bool allied)
            {
                float time = _Time.y;
                float x = i.uv.x, y = i.uv.y;
                float across = 1 - abs(y * 2 - 1);
                float3 rgb;
                float alpha = 0;
                if (_Kind < 0.5)
                {
                    float core = pow(saturate(across), 7) * (1 - x * 0.6);
                    float wisp = smoothstep(0.55, 0.85, Noise(float2(x * 9 - time * 18, y * 3 + _Seed))) * pow(saturate(across), 2);
                    float3 tint = allied ? Holy : Crimson;
                    rgb = WhiteHot * core * (allied ? 0.9 : 0.4) + tint * (pow(saturate(across), 2.2) * 0.45 + wisp * 0.6);
                    if (!allied) alpha = smoothstep(0.45, 0.8, Noise(float2(x * 6 - time * 10, y * 2.5 + _Seed))) * across * x * 0.5;
                }
                else
                {
                    float n = Fbm(float2(x * 7 - time * 7, y * 2.6 + _Seed));
                    float twist = pow(saturate(sin(x * 24 - time * 30 + (y - 0.5) * 6 + _Seed)), 6) * pow(saturate(across), 1.5);
                    float body = smoothstep(0.3, 0.75, n + across * 0.55 - x * 0.35);
                    float core = pow(saturate(across), 6) * (1 - smoothstep(0, 0.55, x));
                    if (allied)
                        rgb = lerp(Arcane, Holy, saturate(n * 1.4 - 0.4)) * (body * 0.7 + twist * 0.6) + WhiteHot * core * 0.9;
                    else
                    {
                        float smoke = smoothstep(0.42, 0.72, n) * smoothstep(0.1, 0.6, x) * across;
                        rgb = lerp(Crimson, Violet, saturate(x * 1.4)) * (body * 0.8 + twist * 0.5) * (1 - smoke * 0.6) + Ember * core * 0.8;
                        alpha = smoke * 0.7;
                    }
                }
                return float4(rgb, alpha) * i.color.a * float4(i.color.rgb, 1);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                bool allied = _Allied > 0.5;
                // Branches, not a ternary chain: HLSL evaluates every arm of ?: and the modes are not cheap.
                float4 result;
                if (_Mode < 0.5) result = Comet(i, allied);
                else if (_Mode < 1.5) result = Glint(i, allied);
                else if (_Mode < 2.5) result = Sigil(i, allied);
                else if (_Mode < 3.5) result = Column(i, allied);
                else result = Ribbon(i, allied);
                result *= _Opacity;
                return half4(result.rgb, saturate(result.a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
