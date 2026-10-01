// Stylized gradient skybox: horizon haze (matching the fog colour) rising to a clear zenith blue,
// with a soft warm glow around the sun direction. Below the horizon stays at the haze colour.
Shader "Lightbringer/Sky Gradient"
{
    Properties
    {
        _HorizonColor ("Horizon", Color) = (0.72, 0.76, 0.84, 1)
        _ZenithColor ("Zenith", Color) = (0.32, 0.5, 0.82, 1)
        _Exponent ("Gradient Exponent", Range(0.2, 4)) = 0.7
        _SunColor ("Sun Glow", Color) = (1, 0.92, 0.75, 1)
        _SunDirection ("Sun Direction (toward sun)", Vector) = (0.4, 0.5, -0.6, 0)
        _SunSize ("Sun Glow Size", Range(1, 64)) = 10
        _SunStrength ("Sun Glow Strength", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half _Exponent;
                half4 _SunColor;
                float4 _SunDirection;
                half _SunSize;
                half _SunStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half up = saturate(direction.y);
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _Exponent));
                half sun = pow(saturate(dot(direction, normalize(_SunDirection.xyz))), _SunSize);
                color += _SunColor.rgb * sun * _SunStrength;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
