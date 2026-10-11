// Additive shell drawn over allied troop models (URP Lit or any shader) while they stand in the hero aura:
// a pulsing light rim, a soft lift and a band of light sweeping up the body. Appended as an extra material,
// it reads the same global aura sphere as the toon shader, so no per-unit CPU work is needed. The test uses
// the renderer's pivot, so a whole unit lights up together instead of being cut at the aura edge.
// Outside the aura the vertex shader collapses the mesh, so the extra pass costs almost nothing.
Shader "Lightbringer/Aura Blessing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.7, 0.9, 2.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.3
        _Lift ("Body Lift", Range(0, 1)) = 0.12
        _SweepStrength ("Sweep Strength", Range(0, 2)) = 0.6
        _SweepSpeed ("Sweep Speed (m/s)", Float) = 1.4
        _Shell ("Shell Offset (m)", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AuraBlessing"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
                half _RimStrength;
                half _Lift;
                half _SweepStrength;
                float _SweepSpeed;
                float _Shell;
            CBUFFER_END

            // xyz = aura centre (world), w = radius. Set by AuraRuneVisual.
            float4 _LB_AuraSphere;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half inside : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float pivotY : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                float radius = _LB_AuraSphere.w;
                float distance = length(pivot.xz - _LB_AuraSphere.xz);
                half inside = radius > 0.001 ? 1.0 - smoothstep(radius - 0.35, radius + 0.15, distance) : 0.0;
                output.inside = inside;
                if (inside <= 0.001)
                {
                    // Degenerate triangle: nothing rasterises.
                    output.positionCS = float4(0, 0, 0, 0);
                    return output;
                }
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * _Shell;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.pivotY = _LB_AuraSphere.y;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);
                half pulse = 0.8h + 0.2h * sin(_Time.y * 4.0);
                // A thin band of light climbing from the feet, repeating every ~2.4 m of travel.
                float height = input.positionWS.y - input.pivotY;
                float sweep = frac(height * 0.42 - _Time.y * _SweepSpeed * 0.42);
                half band = smoothstep(0.86, 0.96, sweep) * (1.0 - smoothstep(0.96, 1.0, sweep)) * saturate(2.4 - height);
                half3 color = _Color.rgb * (fresnel * _RimStrength * pulse + _Lift + band * _SweepStrength);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color * input.inside, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
