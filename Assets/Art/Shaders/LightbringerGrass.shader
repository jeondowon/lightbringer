// Stylized grass blades baked into chunk meshes. Vertex colour = ground colour at the blade root,
// uv.y = 0 at the root and 1 at the tip. Wind sways the tips; blades near the hero (the aura centre,
// a global set by AuraRuneVisual) bend away. Receives sun shadows, casts none.
Shader "Lightbringer/Grass"
{
    Properties
    {
        _TipTint ("Tip Tint", Color) = (1.1, 1.2, 1.0, 1)
        _RootShade ("Root Shade", Range(0, 1)) = 0.72
        _ShadowColor ("Shadow Tint", Color) = (0.6, 0.68, 0.84, 1)
        _WindStrength ("Wind Strength", Range(0, 0.6)) = 0.16
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.4
        _WindScale ("Wind Wave Scale", Range(0.01, 0.5)) = 0.07
        _PushRadius ("Hero Push Radius", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardGrass"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TipTint;
                half _RootShade;
                half4 _ShadowColor;
                half _WindStrength;
                half _WindSpeed;
                half _WindScale;
                half _PushRadius;
            CBUFFER_END

            float4 _LB_AuraSphere;
            float4 _LB_CloudParams;
            float GrassHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float GrassNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(GrassHash(i), GrassHash(i + float2(1, 0)), u.x), lerp(GrassHash(i + float2(0, 1)), GrassHash(i + float2(1, 1)), u.x), u.y);
            }
            half CloudShadow(float3 positionWS)
            {
                if (_LB_CloudParams.x <= 0.0) return 1.0h;
                float2 c = (positionWS.xz + _Time.y * _LB_CloudParams.zw) * _LB_CloudParams.y;
                float n = GrassNoise(c) * 0.65 + GrassNoise(c * 2.3 + 5.2) * 0.35;
                return 1.0h - _LB_CloudParams.x * smoothstep(0.48, 0.68, n);
            }

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : TEXCOORD1;
                half height : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half h = input.uv.y;
                // Two crossing waves plus a slow gust make the field ripple rather than sway in unison.
                float phase = _Time.y * _WindSpeed;
                float wave = sin(phase + positionWS.x * _WindScale * 6.0 + positionWS.z * _WindScale * 4.0)
                    + 0.5 * sin(phase * 1.7 + positionWS.z * _WindScale * 9.0);
                float gust = 0.6 + 0.4 * sin(phase * 0.23 + positionWS.x * 0.02);
                float3 offset = float3(wave, 0, wave * 0.6) * _WindStrength * gust * h * h;
                // Bend away from the hero.
                if (_LB_AuraSphere.w > 0.0)
                {
                    float2 away = positionWS.xz - _LB_AuraSphere.xz;
                    float reach = length(away);
                    float push = saturate(1.0 - reach / max(_PushRadius, 0.01)) * h;
                    offset.xz += (away / max(reach, 0.001)) * push * 0.45;
                    offset.y -= push * 0.25;
                }
                positionWS += offset;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.height = h;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lit = lerp(1.0h, light.shadowAttenuation, 0.85h) * CloudShadow(input.positionWS);
                half3 shade = lerp(_ShadowColor.rgb, half3(1, 1, 1), lit) * light.color * 0.9h + SampleSH(half3(0, 1, 0)) * 0.3h;
                half3 albedo = lerp(input.color.rgb * _RootShade, input.color.rgb * _TipTint.rgb, input.height);
                half3 color = albedo * shade;
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
