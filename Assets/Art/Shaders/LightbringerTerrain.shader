// Painterly toon terrain. Vertex colour rgb = ground palette, alpha = road mask (0 grass, 1 dirt road).
// World-space noise adds brush-like patches, tiny wildflowers on grass and gravel on roads.
Shader "Lightbringer/Terrain"
{
    Properties
    {
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.68, 0.84, 1)
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.1
        _RampSoftness ("Ramp Softness", Range(0.001, 1)) = 0.35
        _PatchStrength ("Patch Variation", Range(0, 0.5)) = 0.14
        _FlowerAmount ("Wildflowers", Range(0, 1)) = 0.6
        _GravelAmount ("Road Gravel", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ShadowColor;
            half _RampThreshold;
            half _RampSoftness;
            half _PatchStrength;
            half _FlowerAmount;
            half _GravelAmount;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardTerrain"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float4 _LB_CloudParams;
            half CloudShadow(float3 positionWS)
            {
                if (_LB_CloudParams.x <= 0.0) return 1.0h;
                float2 c = (positionWS.xz + _Time.y * _LB_CloudParams.zw) * _LB_CloudParams.y;
                float n = ValueNoise(c) * 0.65 + ValueNoise(c * 2.3 + 5.2) * 0.35;
                return 1.0h - _LB_CloudParams.x * smoothstep(0.48, 0.68, n);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                half road = input.color.a;
                // Broad painterly patches, then finer strokes.
                half broad = ValueNoise(p * 0.11);
                half fine = ValueNoise(p * 0.55 + 7.3);
                half3 albedo = input.color.rgb * (1.0h + (broad - 0.5h) * 2.0h * _PatchStrength) * lerp(0.96h, 1.04h, fine);
                // Wildflowers: sparse light dots clustered in patches, only on grass.
                half cluster = step(0.55h, ValueNoise(p * 0.18 + 31.0));
                half flower = step(0.9h, ValueNoise(p * 3.2 + 3.7)) * cluster * (1.0h - road) * _FlowerAmount;
                half3 petal = lerp(half3(1.0h, 0.97h, 0.86h), half3(1.0h, 0.85h, 0.45h), step(0.5h, ValueNoise(p * 0.9)));
                albedo = lerp(albedo, petal, flower);
                // Gravel on roads: darker and lighter pebbles.
                half pebble = ValueNoise(p * 4.5 + 11.0);
                albedo *= 1.0h - road * _GravelAmount * (step(0.75h, pebble) * 0.18h - step(pebble, 0.12h) * 0.12h);

                float3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(normalWS, light.direction);
                half lit = smoothstep(_RampThreshold - _RampSoftness, _RampThreshold + _RampSoftness, ndl);
                lit *= lerp(1.0h, light.shadowAttenuation, 0.85h) * CloudShadow(input.positionWS);
                half3 shade = lerp(_ShadowColor.rgb, half3(1, 1, 1), lit);
                half3 color = albedo * (shade * light.color + SampleSH(normalWS) * 0.35h);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 NormalsFrag(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
