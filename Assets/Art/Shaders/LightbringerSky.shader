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
        _Panorama ("360 Panorama (2:1)", 2D) = "gray" {}
        _PanoramaEnabled ("Use Panorama", Float) = 0
        _PanoramaRotation ("Panorama Rotation", Range(0, 360)) = 0
        _PanoramaExposure ("Panorama Exposure", Range(0.2, 2)) = 1
        _HorizonOffset ("Panorama Horizon Offset", Range(-0.2, 0.2)) = 0
        _CloudMotion ("Slow Cloud Drift", Range(0, 0.1)) = 0.012
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
                float _PanoramaEnabled, _PanoramaRotation, _PanoramaExposure, _HorizonOffset, _CloudMotion;
            CBUFFER_END
            TEXTURE2D(_Panorama);
            SAMPLER(sampler_Panorama);

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
                if (_PanoramaEnabled > 0.5)
                {
                    float u = frac(atan2(direction.z, direction.x) / (2.0 * PI) + 0.5 + _PanoramaRotation / 360.0);
                    float v = saturate(0.5 + asin(clamp(direction.y, -1.0, 1.0)) / PI + _HorizonOffset);
                    // Only the upper cloud layer drifts; the mountains and castle remain anchored.
                    u = frac(u + sin(_Time.y * 0.025) * _CloudMotion * smoothstep(0.25, 0.7, direction.y));
                    half3 panorama = SAMPLE_TEXTURE2D(_Panorama, sampler_Panorama, float2(u, v)).rgb;
                    // Softly join the two painted edges, including images with slightly different edge values.
                    float seam = 0.5 * (1.0 - smoothstep(0.0, 0.035, min(u, 1.0 - u)));
                    half3 opposite = SAMPLE_TEXTURE2D(_Panorama, sampler_Panorama, float2(1.0 - u, v)).rgb;
                    panorama = lerp(panorama, opposite, seam) * _PanoramaExposure;
                    panorama = lerp(_HorizonColor.rgb, panorama, smoothstep(-0.12, 0.28, direction.y));
                    // Avoid a pinched cloud pattern at the zenith of the spherical projection.
                    panorama = lerp(panorama, _ZenithColor.rgb * 0.65, smoothstep(0.94, 1.0, direction.y));
                    return half4(panorama, 1);
                }
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
