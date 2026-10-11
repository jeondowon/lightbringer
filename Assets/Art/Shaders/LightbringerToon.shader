// Premium-stylized toon surface for Lightbringer characters, troops and props.
// Soft two-tone ramp, shadow tint, rim light, optional emission and inverted-hull outline.
// Aura feedback is rendered separately at the feet, preserving character surface colours.
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
        _WindSway ("Wind Sway (props, uses uv1.y weights)", Range(0, 0.5)) = 0
        _PatternStrength ("Surface Pattern Strength (uv2.x ids)", Range(0, 1)) = 1
        [HDR] _PatternGlow ("Corrupted Mortar Glow", Color) = (1.6, 0.25, 0.6, 1)
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
            float _WindSway;
            half _PatternStrength;
            half4 _PatternGlow;
        CBUFFER_END

        // xyz = aura centre (world), w = radius. Set by AuraRuneVisual.
        float4 _LB_AuraSphere;
        half4 _LB_AuraColor;

        // Drifting cloud shadows (x strength, y scale, zw drift); a global set by BattlefieldStyling, 0 = off.
        float4 _LB_CloudParams;
        float LBHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float LBNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(LBHash(i), LBHash(i + float2(1, 0)), u.x), lerp(LBHash(i + float2(0, 1)), LBHash(i + float2(1, 1)), u.x), u.y);
        }
        half CloudShadow(float3 positionWS)
        {
            if (_LB_CloudParams.x <= 0.0) return 1.0h;
            float2 p = (positionWS.xz + _Time.y * _LB_CloudParams.zw) * _LB_CloudParams.y;
            float n = LBNoise(p) * 0.65 + LBNoise(p * 2.3 + 5.2) * 0.35;
            return 1.0h - _LB_CloudParams.x * smoothstep(0.48, 0.68, n);
        }

        // Wind for props: uv1.y holds a per-vertex sway weight (0 for characters, so they never move).
        float3 ApplySway(float3 positionWS, float weight)
        {
            float phase = _Time.y * 1.3 + positionWS.x * 0.15 + positionWS.z * 0.11;
            float2 sway = float2(sin(phase), sin(phase * 0.83 + 1.7)) * (_WindSway * weight);
            positionWS.xz += sway;
            return positionWS;
        }
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 surface : TEXCOORD1;
                float2 pattern : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : TEXCOORD3;
                half specular : TEXCOORD4;
                half pattern : TEXCOORD5;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Procedural surface detail for baked props and buildings (uv2.x): 1 masonry, 2 roof tiles,
            // 3 wood planks, 4 corrupted masonry (glowing mortar), 5 rock grain. World-space, so it is meant for
            // static meshes; troops leave uv2 empty and stay clean.
            float AA(float value) { return max(fwidth(value), 1e-4); }

            half3 ApplyPattern(half3 albedo, half id, float3 positionWS, float3 normalWS, out half3 glow)
            {
                glow = 0;
                int pattern = (int)round(id);
                if (pattern <= 0 || _PatternStrength <= 0.0h) return albedo;
                float3 n = abs(normalWS);
                bool top = n.y > max(n.x, n.z);
                // Face coordinates: along the wall and up, or the ground plane for top faces.
                float2 uv = top ? positionWS.xz : float2(n.x > n.z ? positionWS.z : positionWS.x, positionWS.y);
                half3 result = albedo;
                if (pattern == 1 || pattern == 4)
                {
                    float2 size = pattern == 4 ? float2(0.7, 0.36) : float2(0.6, 0.3);
                    float2 b = uv / size;
                    float row = floor(b.y);
                    b.x += frac(row * 0.5) + LBHash(float2(row, 1.7)) * 0.3;
                    float2 cell = floor(b), local = frac(b);
                    float id2 = LBHash(cell + 3.3);
                    float2 edge = min(local, 1.0 - local) * size;
                    float joint = min(edge.x, edge.y);
                    float mortar = 1.0 - smoothstep(0.018, 0.018 + AA(joint) * 1.5 + 0.012, joint);
                    half3 stone = albedo * lerp(0.86h, 1.1h, id2) * lerp(0.94h, 1.04h, LBNoise(uv * 7.0 + id2 * 9.0));
                    // Bevel: lit upper edge, shaded lower edge of each block.
                    stone *= 1.0h + 0.07h * (1.0h - smoothstep(0.0, 0.08, (1.0 - local.y) * size.y)) - 0.1h * (1.0h - smoothstep(0.0, 0.06, local.y * size.y));
                    if (pattern == 4) stone *= lerp(0.8h, 1.05h, LBNoise(uv * 2.3));
                    half3 joints = albedo * (pattern == 4 ? 0.3h : 0.55h);
                    result = lerp(stone, joints, mortar);
                    if (pattern == 4) glow = _PatternGlow.rgb * mortar * (0.55h + 0.45h * sin(_Time.y * 2.0 + id2 * 6.28)) * step(0.35, LBNoise(uv * 0.9 + 4.0));
                }
                else if (pattern == 2)
                {
                    // Overlapping roof tiles with scalloped lower edges.
                    float2 t = float2(uv.x / 0.32, uv.y / 0.2);
                    float row = floor(t.y);
                    t.x += frac(row * 0.5);
                    float2 local = frac(t);
                    float tileId = LBHash(floor(t) + 5.1);
                    float scallop = sqrt(saturate(1.0 - pow(local.x * 2.0 - 1.0, 2.0))) * 0.35;
                    float lower = smoothstep(0.0, 0.12 + AA(local.y), local.y - (0.35 - scallop) * 0.4);
                    result = albedo * lerp(0.62h, 1.0h, lower) * lerp(0.88h, 1.08h, tileId) * lerp(1.06h, 0.94h, local.y);
                }
                else if (pattern == 3)
                {
                    // Vertical planks with grain and dark seams.
                    float plank = uv.x / 0.26;
                    float local = frac(plank);
                    float plankId = LBHash(float2(floor(plank), 2.2));
                    float grain = LBNoise(float2(plank * 9.0, uv.y * 0.9 + plankId * 20.0));
                    float seam = 1.0 - smoothstep(0.03, 0.03 + AA(local) * 1.5, min(local, 1.0 - local));
                    result = albedo * lerp(0.85h, 1.1h, plankId) * lerp(0.9h, 1.06h, grain);
                    result = lerp(result, albedo * 0.45h, seam);
                }
                else if (pattern == 5)
                {
                    // Rock grain: mottled speckles, faint strata and darker cracks.
                    float speck = LBNoise(uv * 9.0) * 0.5 + LBNoise(uv * 23.0 + 3.0) * 0.5;
                    float strata = LBNoise(float2(positionWS.y * 3.0 + LBNoise(uv * 0.8) * 2.0, 0.5));
                    float crack = 1.0 - smoothstep(0.0, 0.04, abs(LBNoise(uv * 1.6 + 9.0) - 0.5));
                    result = albedo * lerp(0.86h, 1.1h, speck) * lerp(0.93h, 1.05h, strata) * (1.0h - crack * 0.3h);
                }
                return lerp(albedo, result, _PatternStrength);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = ApplySway(TransformObjectToWorld(input.positionOS.xyz), input.surface.y);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                // Baked palette: rgb = albedo, a = emission amount, uv1.x = metallic highlight.
                output.color = _UseVertexColor > 0.5h ? input.color : half4(1, 1, 1, 0);
                output.specular = _UseVertexColor > 0.5h ? input.surface.x : 0;
                output.pattern = _UseVertexColor > 0.5h ? input.pattern.x : 0;
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
                lit *= lerp(1.0h, light.shadowAttenuation, 0.85h) * CloudShadow(input.positionWS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                half3 shade = lerp(_ShadowColor.rgb, half3(1, 1, 1), lit * ao.directAmbientOcclusion);
                half3 patternGlow;
                half3 albedo = ApplyPattern(_BaseColor.rgb * input.color.rgb, input.pattern, input.positionWS, normalWS, patternGlow);
                half3 color = albedo * (shade * light.color + SampleSH(normalWS) * 0.35h * ao.indirectAmbientOcclusion);
                color += patternGlow;

                // Small painted highlight for metal and gold trim.
                half3 halfway = normalize(light.direction + viewWS);
                half spec = smoothstep(0.93h, 0.96h, saturate(dot(normalWS, halfway))) * lit * max(_Specular, input.specular);
                color += spec * light.color;

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);
                color += fresnel * _RimStrength * _RimColor.rgb * (0.4h + 0.6h * lit);

                // Hero aura reaction: allies inside the radius gain a light rim and lift.
                // Buff feedback is owned by AuraBlessingVisual's foot rune and leg veil.

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
                float2 surface : TEXCOORD1;
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
                float3 positionWS = ApplySway(TransformObjectToWorld(input.positionOS.xyz), input.surface.y);
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
                float2 surface : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = ApplySway(TransformObjectToWorld(input.positionOS.xyz), input.surface.y);
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
                float2 surface : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformWorldToHClip(ApplySway(TransformObjectToWorld(input.positionOS.xyz), input.surface.y));
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
                float2 surface : TEXCOORD1;
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
                output.positionCS = TransformWorldToHClip(ApplySway(TransformObjectToWorld(input.positionOS.xyz), input.surface.y));
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
