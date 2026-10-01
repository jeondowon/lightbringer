// Premium-stylized toon surface for Lightbringer characters, troops and props.
// Soft two-tone ramp, shadow tint, rim light, optional emission and inverted-hull outline.
// Allied materials (_AuraReact = 1) glow when inside the hero aura. The aura position is a
// global set once per frame, so no per-unit CPU work or per-renderer property blocks are needed.
Shader "Lightbringer/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.55, 0.6, 0.8, 1)
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.1
        _RampSoftness ("Ramp Softness", Range(0.001, 1)) = 0.08
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.9, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.35
        _Specular ("Specular Highlight", Range(0, 1)) = 0
        [Toggle] _UseVertexColor ("Use Baked Vertex Palette", Float) = 0
        _EmissionStrength ("Vertex Emission Strength", Range(0, 8)) = 2.5
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _AuraReact ("Aura Reaction (allies)", Range(0, 1)) = 0
        _OutlineColor ("Outline Color", Color) = (0.08, 0.07, 0.1, 1)
        _OutlineWidth ("Outline Width (world m)", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _RampThreshold;
            half _RampSoftness;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _Specular;
            half _UseVertexColor;
            half _EmissionStrength;
            half4 _EmissionColor;
            half _AuraReact;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        // xyz = aura centre (world), w = radius. Set by AuraRuneVisual.
        float4 _LB_AuraSphere;
        half4 _LB_AuraColor;
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 surface : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : TEXCOORD3;
                half specular : TEXCOORD4;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                // Baked palette: rgb = albedo, a = emission amount, uv1.x = metallic highlight.
                output.color = _UseVertexColor > 0.5h ? input.color : half4(1, 1, 1, 0);
                output.specular = _UseVertexColor > 0.5h ? input.surface.x : 0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Soft two-tone ramp; cast shadows fall into the same painted shadow tone.
                half ndl = dot(normalWS, light.direction);
                half lit = smoothstep(_RampThreshold - _RampSoftness, _RampThreshold + _RampSoftness, ndl);
                lit *= lerp(1.0h, light.shadowAttenuation, 0.85h);
                half3 shade = lerp(_ShadowColor.rgb, half3(1, 1, 1), lit);
                half3 albedo = _BaseColor.rgb * input.color.rgb;
                half3 color = albedo * (shade * light.color + SampleSH(normalWS) * 0.35h);

                // Small painted highlight for metal and gold trim.
                half3 halfway = normalize(light.direction + viewWS);
                half spec = smoothstep(0.93h, 0.96h, saturate(dot(normalWS, halfway))) * lit * max(_Specular, input.specular);
                color += spec * light.color;

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);
                color += fresnel * _RimStrength * _RimColor.rgb * (0.4h + 0.6h * lit);

                // Hero aura reaction: allies inside the radius gain a light rim and lift.
                float2 offset = input.positionWS.xz - _LB_AuraSphere.xz;
                float radius = max(_LB_AuraSphere.w, 0.001);
                half inside = _AuraReact * (1.0h - smoothstep(radius * 0.92, radius, length(offset))) * step(0.001, _LB_AuraSphere.w);
                color += inside * _LB_AuraColor.rgb * (0.12h + fresnel * 1.4h);

                color += _EmissionColor.rgb + input.color.rgb * input.color.a * _EmissionStrength;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Inverted-hull outline. URP renders SRPDefaultUnlit passes in the opaque queue.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(_OutlineWidth - 0.0001);
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
