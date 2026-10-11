// Shared, low circular veil. Buff visibility is driven by AuraBlessingVisual, not model pivots.
Shader "Lightbringer/Aura Blessing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.7, 0.9, 2.0, 1)
        [HDR] _GoldColor ("Ankle Ribbon", Color) = (1.8, 1.2, 0.5, 1)
        _Intensity ("Veil Intensity", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "AuraBlessing"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GoldColor;
                half _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float h = input.uv.y;
                float angle = input.uv.x * 2.0 * PI;
                float flow = sin(angle * 3.0 - _Time.y * 1.1 + h * 6.0);
                float fine = sin(angle * 7.0 + _Time.y * 0.7 - h * 9.0);
                float fade = (1.0 - smoothstep(0.08, 1.0, h)) * smoothstep(0.0, 0.045, h);
                float ribbonHeight = 0.16 + flow * 0.045;
                float ribbon = exp2(-1700.0 * (h - ribbonHeight) * (h - ribbonHeight));
                float glow = fade * (0.10 + 0.05 * flow + 0.025 * fine);
                float arc = 0.35 + 0.65 * pow(saturate(sin(angle * 2.0 - _Time.y * 0.6)), 2.0);
                half3 color = (_Color.rgb * glow + _GoldColor.rgb * ribbon * arc * 0.45) * _Intensity;
                return half4(MixFogColor(color, half3(0, 0, 0), input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
