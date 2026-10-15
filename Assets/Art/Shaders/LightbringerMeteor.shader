Shader "Lightbringer/Meteor"
{
    // Meteor spell visuals, one additive shader selected by _Mode (objects use their own object-space position):
    // 0 ground aurora warning (unit ground quad), 1 aurora curtain (cylinder, pivot at base),
    // 2 fireball body (sphere), 3 fireball tail (cone, base at the head), 4 corona (sphere), 5 impact dome (sphere).
    // Palette stays red / orange / yellow.
    Properties
    {
        _Mode ("Mode", Float) = 0
        _Age ("Age", Float) = 0
        _Progress ("Warning progress 0..1", Float) = 0
        _Fade ("Overall fade", Float) = 1
        _Burst ("Seconds since impact (<0 before)", Float) = -1
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
                float _Mode, _Age, _Progress, _Fade, _Burst;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 os : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.os = v.positionOS.xyz;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                return o;
            }
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * vnoise(p); p = p * 2.03 + 11.7; a *= 0.5; }
                return v;
            }
            // 0 = deep red, 0.5 = orange, 1 = yellow-white.
            float3 fire(float t)
            {
                float3 red = float3(1.5, 0.12, 0.03), orange = float3(2.1, 0.75, 0.1), yellow = float3(2.6, 2.0, 0.6);
                t = saturate(t);
                return t < 0.5 ? lerp(red, orange, t * 2) : lerp(orange, yellow, t * 2 - 1);
            }
            float ring(float r, float target, float width)
            {
                return 1 - smoothstep(width, width * 2, abs(r - target));
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Age;
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 result = 0;
                int mode = (int)(_Mode + 0.5);
                if (mode == 0)
                {
                    // Ground aurora: slow ribbons of fire rolling outward from a target mark.
                    float2 p = i.os.xz * 2;
                    float r = length(p) / 0.89;
                    float2 c = p / max(length(p), 0.0001);
                    float warp = fbm(c * 1.6 + float2(t * 0.35, -t * 0.25) + r * 0.8) * 2 - 1;
                    float bands = sin(r * 16 - t * 3.2 + warp * 6.5) * 0.5 + 0.5;
                    bands = pow(bands, 2.2);
                    float ribbons = pow(saturate(sin(atan2(p.y, p.x) * 6 + warp * 4 + t * 0.9) * 0.5 + 0.5), 3);
                    float body = (0.20 + bands * 0.75 + ribbons * 0.35) * (1 - smoothstep(0.55, 1.0, r) * 0.55);
                    body *= 0.35 + 0.65 * _Progress;
                    float3 tint = fire(0.2 + (1 - r) * 0.55 + bands * 0.25 + _Progress * 0.15);
                    float disc = 1 - smoothstep(0.93, 1.0, r);
                    float edge = ring(r, 0.97, 0.018) * (0.7 + 0.8 * _Progress);
                    float edgeTicks = ring(r, 0.90, 0.012) * pow(saturate(sin(atan2(p.y, p.x) * 36 - t * 1.5) * 0.5 + 0.5), 4) * 0.8;
                    float closing = ring(r, lerp(1.9, 0.06, _Progress * _Progress), 0.022) * smoothstep(0.0, 0.12, _Progress) * (r < 1.9 ? 1 : 0);
                    float core = exp(-r * r * 9) * (0.15 + 0.85 * _Progress * _Progress);
                    result = tint * body * disc * 0.8
                        + fire(0.85) * (edge + edgeTicks) * 0.8
                        + float3(2.6, 2.3, 1.4) * closing * 0.9
                        + fire(0.9) * core * 1.1;
                    float after = _Burst < 0 ? 1 : saturate(1 - _Burst / 0.8);
                    result *= after + (_Burst >= 0 ? exp(-_Burst * 7) * 1.5 : 0);
                }
                else if (mode == 1)
                {
                    // Aurora curtain standing on the warning edge: hanging folds that sway and shimmer.
                    float y = saturate(i.os.y);
                    float2 c = normalize(i.os.xz + float2(0.0001, 0));
                    float a = atan2(i.os.z, i.os.x);
                    float sway = vnoise(c * 1.8 + float2(t * 0.45, y * 1.2 - t * 0.2)) * 2 - 1;
                    float folds = sin(a * 9 + sway * 3.2 + t * 0.9) * 0.5 + 0.5;
                    float rays = pow(folds, 2.6) + 0.45 * pow(sin(a * 22 - t * 1.4 + y * 4 + sway * 5) * 0.5 + 0.5, 5);
                    float heightWave = 0.55 + 0.3 * sin(a * 5 + t * 1.6) + 0.15 * sin(a * 11 - t * 2.3);
                    float envelope = pow(saturate(1 - y / max(heightWave, 0.05)), 1.4);
                    float hem = exp(-y * 28) * 0.9;
                    float shimmer = 0.75 + 0.25 * sin(y * 40 - t * 7 + a * 3);
                    float3 tint = fire(1 - y * 1.25 + sway * 0.12);
                    float strength = pow(_Progress, 1.4);
                    result = tint * (rays * envelope * shimmer * 1.15 + hem) * strength;
                    if (_Burst >= 0)
                    {
                        // On impact the curtain flares white-hot and burns off.
                        float flare = exp(-_Burst * 3.4);
                        result = result * 0.3 + fire(0.9 - y * 0.6) * (rays * 0.6 + 0.5) * envelope * flare * 2.2
                            + float3(2.6, 2.2, 1.2) * hem * flare;
                    }
                    float facing = abs(dot(normalize(float3(i.os.x, 0, i.os.z)), mul((float3x3)UNITY_MATRIX_I_M, view)));
                    result *= smoothstep(0.0, 0.5, facing) * 0.85 + 0.15;
                }
                else if (mode == 2)
                {
                    // Molten rock: churning crust with white-hot cracks, brighter toward the rim.
                    float3 n = normalize(i.os);
                    float2 uv = float2(atan2(n.z, n.x) * 0.9, n.y * 2.2);
                    float crust = fbm(uv * 2.4 + float2(t * 0.6, t * 0.4));
                    float crack = pow(1 - abs(fbm(uv * 3.6 - t * 0.5) * 2 - 1), 5);
                    float3 world = TransformObjectToWorldDir(n);
                    float rim = pow(1 - saturate(abs(dot(world, view))), 1.6);
                    float heat = saturate(crust * 0.8 + crack * 0.9 + rim * 0.5);
                    result = fire(0.25 + heat * 0.75) * (0.7 + heat * 1.2) + float3(2.8, 2.4, 1.6) * crack * 0.7;
                }
                else if (mode == 3)
                {
                    // Tail: streaks of flame behind the head, red at the tip. y = 0 head, 1 tip.
                    float y = saturate(i.os.y);
                    float a = atan2(i.os.z, i.os.x);
                    float2 c = normalize(i.os.xz + float2(0.0001, 0));
                    float streak = fbm(c * 3.0 + float2(y * 4.5 - t * 9, y * 2));
                    float strands = pow(saturate(sin(a * 7 + y * 6 - t * 10) * 0.5 + 0.5), 3);
                    float shape = pow(1 - y, 1.3);
                    float v = shape * (0.35 + streak * 0.9 + strands * 0.5);
                    result = fire(0.95 - y * 0.85 + streak * 0.2) * v * 1.5;
                }
                else if (mode == 4)
                {
                    // Corona: a soft halo, strongest where the sphere faces the camera.
                    float3 world = TransformObjectToWorldDir(normalize(i.os));
                    float facing = saturate(abs(dot(world, view)));
                    float glow = pow(facing, 2.2);
                    result = fire(0.55 + glow * 0.4) * glow * (0.85 + 0.15 * sin(t * 28));
                }
                else
                {
                    // Impact dome: an expanding shell of fire that thins out as it grows. _Burst = seconds since impact.
                    float3 n = normalize(i.os);
                    float2 uv = float2(atan2(n.z, n.x) * 1.4, n.y * 2.4);
                    float turbulence = fbm(uv * 2.2 + float2(0, -_Burst * 3.2));
                    float3 world = TransformObjectToWorldDir(n);
                    float rim = pow(1 - saturate(abs(dot(world, view))), 0.9);
                    float life = saturate(_Burst / 0.75);
                    float fade = (1 - life) * (1 - life);
                    float lick = saturate(turbulence * 1.5 - life * 0.5);
                    result = fire(0.35 + (1 - life) * 0.55 + turbulence * 0.25) * (rim * 1.2 + 0.25) * lick * fade * 1.9;
                }
                return half4(result * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
