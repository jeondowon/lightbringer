// Shallow, silty river under an overcast sky. uv.x runs across the river (0 = one bank, 1 = the other);
// vertex alpha fades the ends. From the bank inward: dark wet mud, a thin bright waterline, see-through shallows,
// then murky water. The surface is mostly a reflection of the grey sky with soft drifting cloud shapes, broken by
// wind-roughened patches and fine ripples; no sun glint.
Shader "Lightbringer/River Water"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.15, 0.16, 0.14, 1)
        _ShallowColor ("Shallow Water", Color) = (0.27, 0.25, 0.19, 1)
        _MudColor ("Wet Bank Mud", Color) = (0.13, 0.11, 0.09, 1)
        _HorizonColor ("Sky Horizon", Color) = (0.4, 0.44, 0.48, 1)
        _ZenithColor ("Sky Zenith", Color) = (0.27, 0.3, 0.35, 1)
        _Reflection ("Reflection Strength", Range(0, 1)) = 0.6
        _SkyBoost ("Sky Reflection Brightness", Range(0.5, 2.5)) = 1.35
        _FlowDirection ("Flow Direction (xz)", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Flow Speed", Range(0, 2)) = 0.25
        _RippleScale ("Ripple Scale", Range(0.05, 3)) = 1.1
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.22
        _MudWidth ("Bank Mud Width", Range(0.01, 0.5)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RiverWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _MudColor;
                half4 _HorizonColor;
                half4 _ZenithColor;
                half _Reflection;
                half _SkyBoost;
                float4 _FlowDirection;
                half _FlowSpeed;
                half _RippleScale;
                half _RippleStrength;
                half _MudWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }

            // Gradient noise in [-1, 1] with its analytic derivative (xy), so ripple normals need one evaluation.
            float3 Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);
                float2 ga = Hash2(i), gb = Hash2(i + float2(1, 0)), gc = Hash2(i + float2(0, 1)), gd = Hash2(i + float2(1, 1));
                float va = dot(ga, f), vb = dot(gb, f - float2(1, 0)), vc = dot(gc, f - float2(0, 1)), vd = dot(gd, f - float2(1, 1));
                float value = va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * (va - vb - vc + vd);
                float2 slope = ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd)
                             + du * (u.yx * (va - vb - vc + vd) + float2(vb, vc) - va);
                return float3(slope, value);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flow = normalize(_FlowDirection.xz + float2(1e-4, 0));
                float2 across = float2(-flow.y, flow.x);
                float2 p = input.positionWS.xz;
                // River-aligned coordinates, stretched along the current.
                float2 q = float2(dot(p, flow), dot(p, across));
                float t = _Time.y * _FlowSpeed;

                // Wind patches: calm, mirror-like stretches alternate with roughened water.
                half rough = smoothstep(-0.35h, 0.45h, Noise(q * 0.07 + float2(-t * 0.05, 0)).z);
                float2 slope = 0;
                float3 n1 = Noise(float2(q.x * 0.45, q.y) * _RippleScale - float2(t * 0.8, 0));
                float3 n2 = Noise(float2(q.x * 0.6, q.y) * _RippleScale * 2.7 + float2(-t * 1.6, 5.3));
                float3 n3 = Noise(q * _RippleScale * 7.0 + float2(-t * 2.4, 1.7));
                slope += n1.xy * float2(0.45, 1) * 0.5;
                slope += n2.xy * float2(0.6, 1) * 0.3 * 2.7;
                slope += n3.xy * 0.12 * 7.0 * rough;
                slope *= _RippleScale * _RippleStrength * lerp(0.35h, 1.0h, rough);
                float2 slopeWS = slope.x * flow + slope.y * across;
                float3 normalWS = normalize(float3(-slopeWS.x, 1, -slopeWS.y));

                // 0 at either bank, 1 mid-channel; the waterline wanders with small coves and mud spits.
                half middle = 1.0h - abs(input.uv.x * 2.0h - 1.0h);
                half wander = Noise(p * 0.35 + 7.3).z * 0.09h + Noise(p * 1.3 + 2.9).z * 0.04h;
                middle = saturate(middle + wander * smoothstep(0.0h, _MudWidth * 0.6h, middle));
                half water = smoothstep(_MudWidth * 0.9h, _MudWidth * 1.1h, middle);
                half depth = smoothstep(_MudWidth, 0.9h, middle);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.6h, 1.0h, light.shadowAttenuation);
                half3 ambient = max(SampleSH(half3(0, 1, 0)), 0.0h);
                half3 lighting = ambient * 0.9h + light.color * saturate(light.direction.y) * 0.4h * shadow;

                // Silt clouds drift in the current underneath the surface.
                half silt = Noise(q * 0.22 - float2(t * 0.3, 0)).z * 0.5h + 0.5h;
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depth * (0.8h + silt * 0.4h))) * lighting;

                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 reflected = reflect(-viewWS, normalWS);
                // Overcast sky with soft cloud masses, seen in the reflection.
                float2 skyUV = reflected.xz / max(reflected.y, 0.15) * 1.6 + float2(t * 0.02, 0);
                half clouds = Noise(skyUV).z * 0.6h + Noise(skyUV * 2.3 + 4.1).z * 0.3h;
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, saturate(reflected.y)) * _SkyBoost * (1.0h + clouds * 0.35h);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), 4.0h);
                half sheen = saturate(_Reflection * lerp(0.75h, 0.45h, rough) + fresnel * 0.5h);
                half3 color = lerp(body, sky * shadow, sheen);

                // Thin bright waterline where the water meets the mud.
                half waterline = saturate(1.0h - abs(middle - _MudWidth) / (_MudWidth * 0.12h));
                color = lerp(color, sky * 1.15h * shadow, waterline * 0.45h * water);

                // Wet, dark mud along the banks fading out onto the dry ground.
                half3 mud = _MudColor.rgb * lighting;
                color = lerp(mud, color, water);
                // Shallows let the riverbed show through; mid-channel is opaque silt.
                half shallowAlpha = lerp(0.62h, 0.95h, smoothstep(0.0h, 0.35h, depth));
                half mudAlpha = smoothstep(0.0h, _MudWidth * 0.8h, middle) * 0.62h;
                half alpha = lerp(mudAlpha, shallowAlpha, water) * input.color.a;
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
