// Boss attack telegraphs (BossVfx warnings for the Warlord and the Demon King). The floor is drawn twice: an
// alpha-blended corruption shadow that darkens the ground, and an additive layer with a runic double rim, inward
// thorns, drifting wisps, fissures that heat up and a charge front that sweeps out with the wind-up. The curtain,
// sky lance, vortex, falling orb and its trail are the volumetric parts. Lives in Resources for standalone builds.
Shader "Lightbringer/Boss Telegraph"
{
    Properties
    {
        _Age ("Age", Float) = 0
        _Progress ("Wind-up", Float) = 0
        _Mode ("0 Shadow, 1 Glow, 2 Curtain, 3 Lance, 4 Vortex, 5 Orb, 6 Trail", Float) = 1
        _Shape ("0 Circle, 1 Cone, 2 Lane", Float) = 0
        _Radius ("Radius or Length (m)", Float) = 3
        _Arc ("Cone Half Arc (radians)", Float) = 0.96
        _Width ("Lane Width (m)", Float) = 4.8
        _Seed ("Seed", Float) = 0
        _Opacity ("Opacity", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+4" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Age, _Progress, _Mode, _Shape, _Radius, _Arc, _Width, _Seed, _Opacity, _SrcBlend, _DstBlend;
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
                float3 facingWS : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                // Curtains carry outward normals; the lance and trail cylinders face out radially; the orb is a sphere.
                float3 facing = _Mode > 2.5 && _Mode < 3.5 || _Mode > 5.5 ? float3(v.positionOS.x, 0, v.positionOS.z)
                    : _Mode > 4.5 ? v.positionOS.xyz : v.normalOS;
                o.facingWS = TransformObjectToWorldDir(facing + 1e-5);
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
            // Branching fissures: thin valleys of a domain-warped noise field.
            float Cracks(float2 p, float seed)
            {
                float2 warp = float2(Fbm(p * 1.7 + seed), Fbm(p * 1.7 - seed + 9.3));
                float field = Fbm(p * 3.1 + warp * 1.6 + seed * 3.7);
                return 1 - smoothstep(0.0, 0.035, abs(field - 0.5));
            }
            float Line(float d, float width) { return exp(-d * d / (width * width)); }
            float Facing(Varyings i) { return abs(dot(normalize(i.facingWS), GetWorldSpaceNormalizeViewDir(i.positionWS))); }

            // Floor coordinates in metres: position, signed distance inside the boundary, distance along that
            // boundary, the wind-up coordinate (0 at the caster or centre, 1 at the far edge) and the flow direction.
            struct Floor { float2 p; float inside; float along; float reach; float2 flow; };

            Floor Field(float2 uv)
            {
                Floor f;
                if (_Shape > 1.5)
                {
                    f.p = float2((uv.x - 0.5) * _Width, uv.y * _Radius);
                    float side = _Width * 0.5 - abs(f.p.x), cap = _Radius - f.p.y;
                    f.inside = min(side, cap);
                    f.along = side < cap ? f.p.y : f.p.x + 50;
                    f.reach = uv.y;
                    f.flow = float2(0, 1);
                    return f;
                }
                f.p = (uv * 2 - 1) * _Radius;
                float r = length(f.p);
                float a = atan2(f.p.x, f.p.y);
                f.inside = _Radius - r;
                f.along = a * _Radius;
                if (_Shape > 0.5)
                {
                    float side = r * sin(_Arc - abs(a));
                    if (side < f.inside) { f.inside = side; f.along = r + 50; }
                }
                f.reach = r / _Radius;
                f.flow = r > 0.001 ? f.p / r : float2(0, 1);
                return f;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = max(0, _Age);
                float k = saturate(_Progress);
                // The last third of the wind-up pulses faster and faster: the moment to leave.
                float urgency = smoothstep(0.65, 1, k);
                float pulse = 0.5 + 0.5 * sin(t * lerp(5, 26, urgency));
                float3 rgb = 0;
                float alpha = 1;
                if (_Mode < 1.5)
                {
                    Floor f = Field(i.uv);
                    float d = f.inside;
                    float inArea = smoothstep(-0.04, 0.01, d);
                    // The area unrolls from the caster (or centre) in a fifth of a second.
                    float unroll = t / 0.2;
                    float reveal = 1 - smoothstep(unroll - 0.12, unroll, f.reach);
                    if (_Mode < 0.5)
                    {
                        float n = Fbm(f.p * 0.35 + _Seed + float2(t * 0.12, -t * 0.08));
                        rgb = lerp(float3(0.025, 0.004, 0.045), float3(0.1, 0.012, 0.07), n);
                        alpha = smoothstep(-0.02, 0.4, d) * saturate(0.3 + 0.3 * k + (n - 0.5) * 0.3) * reveal;
                    }
                    else
                    {
                        // Rim: a crisp hot line on the boundary with a soft bloom skirt, and a thin inner line.
                        float rim = Line(d, 0.055) + Line(d, 0.24) * 0.3;
                        float innerLine = Line(d - 0.5, 0.035);
                        // A band of procedural runes crawling along the edge between the two lines.
                        float u = f.along / 0.62 + t * 0.35;
                        float id = floor(u), x = frac(u), y = saturate((d - 0.1) / 0.32);
                        float h1 = Hash(float2(id, _Seed)), h2 = Hash(float2(id, _Seed + 3.1)), h3 = Hash(float2(id, _Seed + 7.7));
                        float glyph = Line(x - 0.5, 0.07) * step(0.25, h1)
                            + Line(y - lerp(0.25, 0.75, h2), 0.09) * step(abs(x - 0.5), 0.28) * step(0.45, h2)
                            + Line(x - 0.5 - (y - 0.5) * (h3 > 0.5 ? 0.7 : -0.7), 0.07) * step(0.3, h3) * step(abs(y - 0.5), 0.42);
                        glyph *= step(0.1, d) * step(d, 0.42) * step(abs(x - 0.5), 0.36) * (0.65 + 0.35 * sin(t * 3 + id * 1.7));
                        // Thorns reaching inwards from the inner line; they lengthen as the attack charges.
                        float thornId = floor(f.along / 1.1);
                        float thornX = abs(frac(f.along / 1.1) - 0.5) * 1.1;
                        float thornLength = min(lerp(0.55, 1.05, Hash(float2(thornId, _Seed + 1.3))), (_Shape > 1.5 ? _Width * 0.5 : _Radius) * 0.2) * lerp(0.45, 1, k);
                        float depth = (d - 0.52) / thornLength;
                        float thorns = step(0, depth) * step(depth, 1) * (1 - smoothstep(0, 0.035, thornX - (1 - depth) * 0.16));
                        // Wisps gather towards a circle's centre and stream away from the caster in cones and lanes.
                        float direction = _Shape < 0.5 ? 1 : -1;
                        float wisps = smoothstep(0.52, 0.86, Fbm(f.p * 0.45 + f.flow * t * 0.7 * direction + _Seed));
                        float cracks = Cracks(f.p * 0.32, _Seed) * smoothstep(0.3, 1.2, d);
                        // The charge front: filled area behind a ragged white-hot edge sweeping to the far side.
                        float front = f.reach + (Noise(f.p * 1.3 + t * 2) - 0.5) * 0.06 - k;
                        float filled = step(front, 0) * step(0.01, k);
                        float frontLine = Line(front * _Radius, 0.16) * smoothstep(0, 0.05, k);
                        float detail = 0;
                        if (_Shape < 0.5)
                        {
                            // A rotating seven-point sigil that brightens with the wind-up.
                            float2 sp = f.p / _Radius;
                            float cs = cos(t * 0.45), sn = sin(t * 0.45);
                            sp = float2(cs * sp.x - sn * sp.y, sn * sp.x + cs * sp.y);
                            float rr = length(sp);
                            const float star = 0.5;
                            float lines = 0;
                            [unroll] for (int s = 0; s < 7; s++)
                            {
                                float a0 = s * TAU / 7, a1 = (s + 3) * TAU / 7;
                                float2 A = float2(sin(a0), cos(a0)) * star, B = float2(sin(a1), cos(a1)) * star;
                                float2 across = normalize(float2(A.y - B.y, B.x - A.x));
                                lines += Line(dot(sp - A, across) * _Radius, 0.03);
                            }
                            detail = (lines * step(rr, star) + Line((rr - star) * _Radius, 0.035) + Line((rr - 0.2) * _Radius, 0.03) * 0.8)
                                * lerp(0.2, 1.1, k) * 0.9;
                        }
                        else if (_Shape < 1.5)
                        {
                            // Radial claw streaks racing outwards across the cone.
                            float a = atan2(f.p.x, f.p.y);
                            float lanes = pow(saturate(sin(a * 22 + Noise(float2(a * 6, _Seed)) * 2)), 14);
                            detail = lanes * pow(saturate(sin(length(f.p) * 2.2 - t * 9)), 3) * smoothstep(0.4, 1.2, d) * 0.8;
                        }
                        else
                        {
                            // Chevrons running down the lane in the direction of travel.
                            float m = frac((f.p.y - t * 6.5) / 2.6) * 2.6;
                            float target = 1.3 - abs(f.p.x) * 0.55;
                            detail = Line(m - target, 0.12) * step(abs(f.p.x), _Width * 0.5 - 0.6) * (0.7 + 0.8 * k);
                        }
                        rgb = Crimson * rim * (1.3 + urgency * pulse * 1.3)
                            + Core * Line(d, 0.03) * (0.5 + urgency * pulse)
                            + Violet * innerLine * 1.1
                            + lerp(Violet, Crimson, 0.4) * glyph * 1.4
                            + Violet * thorns * (0.5 + 0.7 * k)
                            + Violet * wisps * 0.22
                            + Crimson * cracks * (0.1 + 1.1 * k * k)
                            + lerp(Violet, Crimson, 0.5) * detail
                            + lerp(Violet, Crimson, k) * filled * (0.1 + 0.12 * pulse * urgency)
                            + Core * frontLine * 1.1
                            + Crimson * 0.2 * urgency * pulse * step(0, d);
                        rgb *= inArea * reveal;
                    }
                }
                else if (_Mode < 2.5)
                {
                    // Curtain: corrupted flames licking up from the boundary, taller as the attack charges.
                    float y = i.uv.y;
                    float height = lerp(0.35, 1, k);
                    float n = Fbm(float2(i.uv.x * 0.9, y * 1.8 - t * 2.4) + _Seed);
                    float body = saturate(1 - y / height);
                    float flames = smoothstep(0.3, 0.8, n * 0.9 + body * 0.55);
                    float3 tint = lerp(Violet, Crimson, saturate(n * 1.5 - 0.2));
                    rgb = (tint * flames * pow(body, 1.4) * (0.5 + urgency * pulse * 0.9) + Core * exp(-y * 10) * 0.45)
                        * smoothstep(0, 0.3, t) * smoothstep(0.05, 0.5, Facing(i));
                }
                else if (_Mode < 3.5)
                {
                    // Sky lance: a column of falling corruption over the target that thickens before the strike.
                    float y = i.uv.y, an = i.uv.x * TAU;
                    float fall = Noise(float2(an * 2, y * 18 + t * 10) + _Seed);
                    float threads = pow(saturate(sin(an * 6 + y * 9 + fall * 3)), 8);
                    float body = smoothstep(0, 0.06, y) * (1 - smoothstep(0.7, 1, y));
                    float power = 0.08 + 0.92 * k * k;
                    rgb = (Violet * (0.22 + threads) + Crimson * fall * 0.45 + Core * pow(fall, 4) * k)
                        * body * power * (0.75 + 0.25 * pulse) * smoothstep(0, 0.5, Facing(i)) * smoothstep(0, 0.3, t);
                }
                else if (_Mode < 4.5)
                {
                    // Vortex: a storm eye spiralling open in the sky above the target.
                    float2 p = (i.uv - 0.5) * 2;
                    float r = length(p), a = atan2(p.y, p.x);
                    float arms = pow(saturate(0.5 + 0.5 * sin(a * 3 + log(r + 0.04) * 6 - t * 3.5)), 3);
                    float n = Fbm(p * 2.5 + float2(t * 0.3, 0) + _Seed);
                    float ring = (1 - smoothstep(0.55, 1, r)) * smoothstep(0.08, 0.3, r);
                    float eye = exp(-r * r * 30) * k * k;
                    rgb = (lerp(Violet, Crimson, n) * arms * ring * (0.25 + 0.75 * k) * (0.6 + n) + Core * eye * 2) * smoothstep(0, 0.4, t);
                }
                else if (_Mode < 5.5)
                {
                    // Abyss orb (alpha blended): a near-black core with churning crimson currents and a violet rim.
                    float fresnel = 1 - saturate(Facing(i));
                    float swirl = Fbm(i.uv * float2(7, 3.5) + float2(t * 1.5, -t * 0.8) + _Seed);
                    rgb = lerp(float3(0.015, 0.002, 0.03), Violet * 1.4, pow(fresnel, 2.2))
                        + Crimson * pow(saturate((swirl - 0.45) * 2), 3) * 1.4 + Core * pow(fresnel, 6) * 0.8;
                    alpha = saturate(0.9 + fresnel);
                }
                else
                {
                    // Orb trail: a streak of corruption fading upwards behind the falling orb.
                    float y = i.uv.y, an = i.uv.x * TAU;
                    float n = Noise(float2(an * 2.5, y * 10 + t * 12) + _Seed);
                    rgb = lerp(Violet, Crimson, n) * pow(saturate(1 - y), 2.2) * (0.35 + n * 0.9) * smoothstep(0, 0.5, Facing(i));
                }
                bool additive = _DstBlend > 0.5 && _DstBlend < 1.5;
                return additive ? half4(rgb * _Opacity, 1) : half4(rgb, saturate(alpha * _Opacity));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
