// Additive lightning ribbon for LineRenderers (texture mode Stretch: uv.x along, uv.y across).
// Bright white-hot core fading softly to the edges; vertex alpha fades the whole bolt.
Shader "Lightbringer/Lightning"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.2, 1.6, 3.0, 1)
        _Softness ("Edge Softness", Range(0.5, 6)) = 1.8
        _CoreBoost ("White Core Boost", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Lightning"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
                half _CoreBoost;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half across = 1.0h - abs(input.uv.y * 2.0h - 1.0h);
                half glow = pow(saturate(across), _Softness);
                half core = pow(saturate(across), 8.0h) * _CoreBoost;
                half3 color = (_Color.rgb * glow + core) * input.color.rgb * input.color.a;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
