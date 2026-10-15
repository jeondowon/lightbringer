// The abyss core's obsidian crystal (AbyssCoreVisual). Opaque, flat-faceted: a glossy navy-black body lit by the main
// light, a violet rim, crimson cracks that crawl upward and pulse with the heartbeat, and the heart's glow seen through
// the faces turned toward the camera. Lives in Resources so builds retain it.
Shader "Lightbringer/Abyss Core"
{
    Properties
    {
        _Body ("Body", Color) = (0.02, 0.02, 0.05, 1)
        [HDR] _Rim ("Rim", Color) = (0.32, 0.14, 0.65, 1)
        [HDR] _Vein ("Cracks", Color) = (2.0, 0.1, 0.06, 1)
        _Veins ("Crack Amount", Float) = 1
        _Heart ("Heartbeat", Range(0, 1)) = 0
        _Flash ("Hit Flash", Range(0, 1)) = 0
        _Charge ("Charge", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "AbyssCore"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Body, _Rim, _Vein;
                float _Veins, _Heart, _Flash, _Charge, _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2; half fogFactor : TEXCOORD3;
            };

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
                for (int i = 0; i < 4; i++) { sum += Noise(p) * amplitude; p = p * 2.07 + float2(3.1, 7.7); amplitude *= 0.5; }
                return sum / 0.9375;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight();
                float facing = saturate(dot(n, v));
                // Obsidian: dark wrapped diffuse, a tight glossy highlight on each facet and a violet rim.
                float diffuse = saturate(dot(n, light.direction)) * 0.6 + 0.25;
                float3 h = normalize(light.direction + v);
                float gloss = pow(saturate(dot(n, h)), 70.0) * 0.9;
                float rim = pow(1.0 - facing, 3.0);
                // Cracks: ridged noise around the crystal that creeps upward.
                float3 p = input.positionOS * 1.6;
                float t = _Time.y;
                float ridge = 1.0 - abs(Fbm(float2(p.x * 1.1 + p.z * 0.8, p.y * 0.9 - t * 0.18)) * 2.0 - 1.0);
                float cracks = pow(ridge, 12.0) * _Veins;
                float heart = _Heart * 0.85 + 0.15 + _Charge * 0.6;
                // The heart shines through the faces that look at the camera, strongest at the crystal's waist.
                float inner = pow(facing, 2.5) * exp(-pow(input.positionOS.y * 0.55 - 1.4, 2.0)) * heart * 0.22 * _Veins;
                half3 rgb = _Body.rgb * diffuse * light.color + light.color * gloss * 0.35 + _Rim.rgb * rim * (1.0 + _Charge * 1.5);
                rgb += _Vein.rgb * (cracks * (0.35 + heart * 1.3) + inner) * (1.0 + _Flash * 2.5);
                rgb += _Rim.rgb * _Flash * 0.9;
                rgb *= _Fade;
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
