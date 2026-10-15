// Painterly toon terrain. Vertex colour rgb = ground palette, alpha = road mask (0 grass, 1 dirt road).
// uv0 (when w = 1): x = distance to the nearest lane (m), y = stronghold plaza mask, z = corruption mask.
// Optional forest colour/normal textures over procedural grass, roads with wheel ruts,
// fine gravel and a ragged grass fringe, flagstone plazas with mossy grout, corrupted soil with glowing cracks
// near the enemy stronghold, layered rock on steep slopes, a noise bump for the lighting, and SSAO.
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
        _StrokeStrength ("Grass Brush Strokes", Range(0, 0.5)) = 0.16
        _BumpStrength ("Detail Bump", Range(0, 2)) = 0.8
        _RutStrength ("Wheel Ruts", Range(0, 1)) = 0.55
        _StoneSize ("Flagstone Size (m)", Range(0.4, 3)) = 1.1
        [HDR] _CorruptGlow ("Corruption Crack Glow", Color) = (2.2, 0.35, 0.8, 1)
        _CorruptSoil ("Corrupted Soil", Color) = (0.24, 0.19, 0.27, 1)
        _ForestGrassTex ("Forest Ground", 2D) = "white" {}
        _ForestSoilTex ("Forest Soil", 2D) = "white" {}
        [Normal] _ForestGrassNormal ("Forest Ground Normal", 2D) = "bump" {}
        [Normal] _ForestSoilNormal ("Forest Soil Normal", 2D) = "bump" {}
        _ForestNormalStrength ("Forest Normal Strength", Range(0, 2)) = 0.7
        [HDR] _ForestGroundTint ("Forest Ground Tint", Color) = (1, 1, 1, 1)
        _ForestTextureStrength ("Forest Texture Blend", Range(0, 1)) = 0
        _ForestTextureScale ("Forest Texture Repeats Per Metre", Float) = 0.25
        _Weathering ("Overgrown Ground and Worn Stone", Range(0, 1)) = 0
        _BeaconLight ("Beacon Position and Radiance", Vector) = (0, 0, 0, 0)
        _StoneTex ("Weathered Stone", 2D) = "white" {}
        [Normal] _StoneNormal ("Stone Normal", 2D) = "bump" {}
        _StoneMask ("Stone Mask (AO in G)", 2D) = "white" {}
        _StoneTextureStrength ("Natural Stone Surface", Range(0, 1)) = 0
        _StoneTextureScale ("Stone Repeats Per Metre", Float) = 0.38
        _NaturalLight ("Natural Direct and Ambient Lighting", Range(0, 1)) = 0
        _SoilBreakup ("Irregular Forest Floor", Range(0, 1)) = 0
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
            half _StrokeStrength;
            half _BumpStrength;
            half _RutStrength;
            float _StoneSize;
            half4 _CorruptGlow;
            half4 _CorruptSoil;
            half _ForestTextureStrength;
            float _ForestTextureScale;
            half _ForestNormalStrength;
            half4 _ForestGroundTint;
            half _Weathering;
            float4 _BeaconLight;
            half _StoneTextureStrength;
            float _StoneTextureScale;
            half _NaturalLight;
            half _SoilBreakup;
        CBUFFER_END
        TEXTURE2D(_ForestGrassTex); SAMPLER(sampler_ForestGrassTex);
        TEXTURE2D(_ForestSoilTex); SAMPLER(sampler_ForestSoilTex);
        TEXTURE2D(_ForestGrassNormal); SAMPLER(sampler_ForestGrassNormal);
        TEXTURE2D(_ForestSoilNormal); SAMPLER(sampler_ForestSoilNormal);
        TEXTURE2D(_StoneTex); SAMPLER(sampler_StoneTex);
        TEXTURE2D(_StoneNormal); SAMPLER(sampler_StoneNormal);
        TEXTURE2D(_StoneMask); SAMPLER(sampler_StoneMask);
        ENDHLSL

        Pass
        {
            Name "ForwardTerrain"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float4 masks : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float4 masks : TEXCOORD4;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 Hash2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.55 + ValueNoise(p * 2.07 + 5.1) * 0.3 + ValueNoise(p * 4.13 + 9.7) * 0.15;
            }

            // Voronoi: x = distance to the nearest cell centre, y = distance to the cell border, z = cell id hash.
            float3 Voronoi(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                float best = 8.0, second = 8.0;
                float id = 0.0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash2(cell + g);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < best) { second = best; best = d; id = Hash(cell + g); }
                        else if (d < second) second = d;
                    }
                return float3(sqrt(best), sqrt(second) - sqrt(best), id);
            }

            // Lane segments in world XZ (a.xy, b.xy), set by BattlefieldEnvironment; per-pixel distance keeps road
            // edges and ruts exact, where the coarse terrain mesh would bend them.
            float4 _LB_Lanes[16];
            float _LB_LaneCount;

            float LaneDistance(float2 p, float fallback)
            {
                int count = (int)_LB_LaneCount;
                if (count <= 0) return fallback;
                float best = 1e5;
                for (int i = 0; i < 16; i++)
                {
                    if (i >= count) break;
                    float2 a = _LB_Lanes[i].xy, b = _LB_Lanes[i].zw;
                    float2 ab = b - a;
                    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
                    best = min(best, length(p - (a + ab * t)));
                }
                return best;
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
                output.masks = input.masks;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Grass height field for the bump: soft clumps plus finer tufts.
            float GrassHeight(float2 p) { return ValueNoise(p * 1.7) * 0.6 + ValueNoise(p * 5.3 + 2.1) * 0.4; }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                bool hasMasks = input.masks.w > 0.5;
                // Detail fades with screen footprint so distant ground does not shimmer.
                float footprint = length(fwidth(p));
                half detail = 1.0h - smoothstep(0.08, 0.35, footprint);

                // Road: rebuilt from lane distance with a noisy, ragged edge (falls back to vertex alpha).
                float laneDistance = LaneDistance(p, input.masks.x);
                float edgeNoise = (Fbm(p * 0.9) - 0.5) * 0.9;
                half road = hasMasks ? 1.0h - smoothstep(1.7, 3.1, laneDistance + edgeNoise) : input.color.a;
                half plaza = hasMasks ? saturate(input.masks.y) : 0.0h;
                half corruption = hasMasks ? saturate(input.masks.z) : 0.0h;

                // Broad painterly patches, then brush strokes in two crossing directions.
                half broad = ValueNoise(p * 0.11);
                half fine = ValueNoise(p * 0.55 + 7.3);
                half3 albedo = input.color.rgb * (1.0h + (broad - 0.5h) * 2.0h * _PatchStrength) * lerp(0.96h, 1.04h, fine);
                // Broad worn patches with smaller edges, rather than a uniform green plane.
                half forestWear = smoothstep(0.42h, 0.72h, Fbm(p * 0.21 + 19.0));
                half soilMix = saturate(road + max(_Weathering * smoothstep(0.5h, 0.77h, broad) * 0.6h,
                    _SoilBreakup * forestWear * (0.45h + 0.35h * fine)));
                half3 groundUnderStone = albedo;
                if (_ForestTextureStrength > 0.001h)
                {
                    float2 forestUV = p * _ForestTextureScale;
                    half3 forestGrass = SAMPLE_TEXTURE2D(_ForestGrassTex, sampler_ForestGrassTex, forestUV).rgb;
                    half3 forestSoil = SAMPLE_TEXTURE2D(_ForestSoilTex, sampler_ForestSoilTex, forestUV).rgb;
                    // A second broad texture scale breaks up the visible tiled field at hero height.
                    forestGrass = lerp(forestGrass, SAMPLE_TEXTURE2D(_ForestGrassTex, sampler_ForestGrassTex, forestUV * 0.37 + 11.7).rgb, 0.22h);
                    forestSoil = lerp(forestSoil, SAMPLE_TEXTURE2D(_ForestSoilTex, sampler_ForestSoilTex, forestUV * 0.43 + 3.9).rgb, 0.18h);
                    half3 forestGround = lerp(forestGrass, forestSoil, soilMix) * _ForestGroundTint.rgb
                        * lerp(0.9h - _Weathering * 0.15h, 1.08h + _Weathering * 0.15h, broad);
                    half damp = smoothstep(0.4h, 0.75h, ValueNoise(p * 0.16 + 63.0));
                    forestGround *= lerp(half3(1.04h, 1.01h, 0.94h), half3(0.71h, 0.79h, 0.72h), damp * _SoilBreakup);
                    groundUnderStone = forestGround;
                    albedo = lerp(albedo, forestGround, _ForestTextureStrength * (1.0h - plaza * (1.0h - _Weathering)));
                }
                float2 strokeA = float2(p.x * 0.9 + p.y * 0.35, p.y * 4.5 - p.x * 1.2);
                float2 strokeB = float2(p.y * 0.9 - p.x * 0.3, p.x * 4.5 + p.y * 1.1);
                half strokes = (ValueNoise(strokeA) * 0.5 + ValueNoise(strokeB + 13.0) * 0.5 - 0.5) * 2.0;
                half grassMask = (1.0h - road) * (1.0h - plaza);
                albedo *= 1.0h + strokes * _StrokeStrength * grassMask * detail;
                // Darker clumps with sunlit tips: reads as tufts from the camera height.
                half clump = GrassHeight(p);
                albedo *= lerp(0.88h, 1.08h, clump * grassMask + (1.0h - grassMask) * 0.5h);

                // Wildflowers: sparse light dots clustered in patches, only on grass.
                half cluster = step(0.55h, ValueNoise(p * 0.18 + 31.0));
                half flower = step(0.9h, ValueNoise(p * 3.2 + 3.7)) * cluster * grassMask * _FlowerAmount * detail;
                half3 petal = lerp(half3(1.0h, 0.97h, 0.86h), half3(1.0h, 0.85h, 0.45h), step(0.5h, ValueNoise(p * 0.9)));
                petal = lerp(petal, half3(0.72h, 0.62h, 1.0h), step(0.8h, ValueNoise(p * 0.7 + 19.0)));
                albedo = lerp(albedo, petal, flower);

                // Road surface: packed earth, two wheel ruts and fine gravel variation.
                half rut = hasMasks ? exp(-pow((laneDistance - 0.8) / 0.22, 2.0)) * road : 0.0h;
                albedo *= 1.0h - rut * _RutStrength * 0.35h * (0.7h + 0.3h * ValueNoise(p * 2.0));
                half pebble = ValueNoise(p * 4.5 + 11.0);
                albedo *= 1.0h - road * _GravelAmount * (step(0.75h, pebble) * 0.18h - step(pebble, 0.12h) * 0.12h);
                // Ragged grass fringe along the road border.
                half fringe = smoothstep(0.15h, 0.5h, road) * (1.0h - smoothstep(0.5h, 0.85h, road));
                albedo = lerp(albedo, albedo * half3(0.86h, 0.95h, 0.82h), fringe * 0.5h);

                // Plaza flagstones: staggered rows of irregular slabs with grout and moss in the joints.
                float2 sp = p / _StoneSize;
                sp += _Weathering * float2(ValueNoise(p * 1.8), ValueNoise(p * 1.8 + 17.0)) * 0.13;
                float row = floor(sp.y);
                sp.x += Hash(float2(row, 3.0)) * 0.8 + row * 0.5;
                float2 cell = floor(sp);
                float2 local = frac(sp);
                float slabId = Hash(cell + 17.0);
                float2 edge = min(local, 1.0 - local);
                float joint = min(edge.x * (0.9 + slabId * 0.2), edge.y * 1.1);
                half grout = 1.0h - smoothstep(0.03, 0.07, joint + (ValueNoise(p * 6.0) - 0.5) * 0.02);
                half3 slab = input.color.rgb * lerp(0.86h, 1.1h, slabId) * lerp(0.93h, 1.04h, ValueNoise(p * 3.1 + slabId * 10.0));
                // Worn, slightly darker slab borders and a few cracked slabs.
                slab *= lerp(0.9h, 1.0h, smoothstep(0.03, 0.2, joint));
                half crack = step(0.82, slabId) * (1.0h - smoothstep(0.0, 0.025, abs(local.x - local.y * 0.7 - 0.15 - slabId * 0.2)));
                slab *= 1.0h - crack * 0.35h;
                half3 groutColor = lerp(half3(0.26h, 0.25h, 0.23h), half3(0.3h, 0.42h, 0.24h), step(0.45h, ValueNoise(p * 0.8 + 4.0)));
                half3 paved = lerp(slab, groutColor, grout);
                // Optional opening-glade treatment: chipped, earth-stained stone with moss spreading
                // out of the joints. Disabled on the other terrain materials.
                half erosion = ValueNoise(p * 2.7 + 43.0);
                half moss = (1.0h - smoothstep(0.04h, 0.24h, joint)) * smoothstep(0.35h, 0.7h, fine);
                half3 agedStone = slab * half3(0.62h, 0.65h, 0.59h) * lerp(0.75h, 1.12h, erosion);
                agedStone = lerp(agedStone, groundUnderStone * half3(0.73h, 0.86h, 0.62h), saturate(grout + moss * 0.8h));
                paved = lerp(paved, agedStone, _Weathering);
                // Slabs give way to grass at the plaza rim through noise.
                half pave = smoothstep(0.35h, 0.65h, plaza + (Fbm(p * 0.6 + 2.0) - 0.5) * 0.5h);
                pave *= 1.0h - _Weathering * step(0.89h, slabId) * (1.0h - smoothstep(0.65h, 1.0h, plaza));
                // Natural rock has its own fissures and normals. Do not draw the old black brick grid over it.
                half stoneAO = 1.0h;
                if (_StoneTextureStrength > 0.001h)
                {
                    float2 stoneUV = p * _StoneTextureScale;
                    half3 rock = SAMPLE_TEXTURE2D(_StoneTex, sampler_StoneTex, stoneUV).rgb;
                    rock *= half3(1.7h, 1.66h, 1.54h) * lerp(0.9h, 1.08h, broad);
                    stoneAO = lerp(0.72h, 1.0h, SAMPLE_TEXTURE2D(_StoneMask, sampler_StoneMask, stoneUV).g);
                    half soilInStone = smoothstep(0.52h, 0.76h, Fbm(p * 0.8 + 24.0)) * 0.5h;
                    half3 naturalStone = lerp(rock, groundUnderStone * 0.85h, soilInStone);
                    paved = lerp(paved, naturalStone, _StoneTextureStrength);
                    // An irregular, gradual soil rim instead of the hard circular sticker boundary.
                    half weatheredPave = smoothstep(0.18h, 0.8h, plaza + (Fbm(p * 1.2) - 0.5h) * 0.65h);
                    pave = lerp(pave, weatheredPave, _StoneTextureStrength);
                }
                // Edge noise must not create stone patches where no paving is requested.
                pave *= saturate(plaza * 100.0h);
                albedo = lerp(albedo, paved, pave);

                // Corruption: ashen purple soil with glowing veins that pulse, strongest by the enemy gate.
                half corrupt = smoothstep(0.1h, 0.7h, corruption + (Fbm(p * 0.35 + 8.0) - 0.5) * 0.4h);
                // The fringe only greys and dries the ground underneath (grass or plaza stone); the dark soil colour
                // is blended in gradually towards the core so grass, soil and plaza share one continuous gradient.
                half3 withered = albedo * half3(0.8h, 0.76h, 0.74h);
                half3 soil = _CorruptSoil.rgb * lerp(0.8h, 1.15h, broad);
                half3 corruptTarget = lerp(withered, soil, smoothstep(0.3h, 1.0h, corrupt) * 0.6h);
                albedo = lerp(albedo, corruptTarget, corrupt * (1.0h - pave * 0.3h));
                // The plaza stone in front of the enemy gate sinks into the same dark soil instead of staying a pale patch.
                albedo *= 1.0h - pave * smoothstep(0.25h, 0.8h, corrupt) * 0.5h;
                float3 veins = Voronoi(p * 0.55 + float2(3.1, 7.7));
                half vein = (1.0h - smoothstep(0.0, 0.06 + 0.04 * corruption, veins.y)) * smoothstep(0.35h, 0.9h, corrupt);
                half pulse = 0.65h + 0.35h * sin(_Time.y * 2.2 + veins.z * 6.28);

                // Steep hill faces: layered rock strata.
                float3 geometricNormal = normalize(input.normalWS);
                half steep = smoothstep(0.85, 0.6, geometricNormal.y);
                half strata = ValueNoise(float2(input.positionWS.y * 2.2 + ValueNoise(p * 0.3) * 2.0, 0.5));
                albedo *= 1.0h - steep * (0.12h - strata * 0.2h);

                // Detail bump from the grass/stone height so the low sun picks out the surface.
                float hx = GrassHeight(p + float2(0.08, 0)) * grassMask - GrassHeight(p) * grassMask;
                float hz = GrassHeight(p + float2(0, 0.08)) * grassMask - GrassHeight(p) * grassMask;
                float3 normalWS = normalize(geometricNormal + float3(-hx, 0, -hz) * (_BumpStrength * 6.0 * detail));
                normalWS = normalize(lerp(normalWS, geometricNormal, pave * 0.5));
                half forestSurface = _ForestTextureStrength * (1.0h - pave) * (1.0h - corrupt);
                if (forestSurface > 0.001h)
                {
                    float2 uv = p * _ForestTextureScale;
                    half normalStrength = _ForestNormalStrength * lerp(0.3h, 1.0h, detail);
                    half3 grassNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_ForestGrassNormal, sampler_ForestGrassNormal, uv), normalStrength);
                    half3 soilNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_ForestSoilNormal, sampler_ForestSoilNormal, uv), normalStrength);
                    half3 mapped = normalize(lerp(grassNormal, soilNormal, soilMix));
                    // UVs are world XZ: +U is X and +V is Z, including on the hills.
                    float3 tangent = normalize(float3(geometricNormal.y, -geometricNormal.x, 0));
                    float3 bitangent = normalize(cross(tangent, geometricNormal));
                    float3 mappedWS = normalize(tangent * mapped.x + bitangent * mapped.y + geometricNormal * mapped.z);
                    normalWS = normalize(lerp(normalWS, mappedWS, forestSurface));
                }
                if (_StoneTextureStrength > 0.001h && pave > 0.001h)
                {
                    half3 stoneNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_StoneNormal, sampler_StoneNormal, p * _StoneTextureScale), 0.85h);
                    float3 tangent = normalize(float3(geometricNormal.y, -geometricNormal.x, 0));
                    float3 bitangent = normalize(cross(tangent, geometricNormal));
                    float3 mappedWS = normalize(tangent * stoneNormal.x + bitangent * stoneNormal.y + geometricNormal * stoneNormal.z);
                    normalWS = normalize(lerp(normalWS, mappedWS, pave * _StoneTextureStrength));
                }

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(normalWS, light.direction);
                half lit = smoothstep(_RampThreshold - _RampSoftness, _RampThreshold + _RampSoftness, ndl);
                // Retain the stylized palette while letting small ground relief catch the light.
                half naturalSurface = saturate(forestSurface + pave * _StoneTextureStrength);
                lit = lerp(lit, saturate(ndl) * 0.85h + 0.15h, naturalSurface);
                lit *= lerp(1.0h, light.shadowAttenuation, 0.85h) * CloudShadow(input.positionWS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                half3 shade = lerp(_ShadowColor.rgb, half3(1, 1, 1), lit * ao.directAmbientOcclusion);
                half3 color = albedo * (shade * light.color + SampleSH(normalWS) * 0.35h * ao.indirectAmbientOcclusion);
                // Shadows remove direct sunlight; ambient sky still reveals the shaded ground.
                // The previous tinted ramp multiplied even full shadow by the sun's intensity.
                half visibility = light.shadowAttenuation * CloudShadow(input.positionWS);
                half3 direct = light.color * saturate(ndl) * visibility * ao.directAmbientOcclusion;
                half3 indirect = max(SampleSH(normalWS), 0.0h) * ao.indirectAmbientOcclusion;
                half3 naturalColor = albedo * (direct + indirect * 0.85h);
                color = lerp(color, naturalColor, _NaturalLight);
                color *= lerp(1.0h, stoneAO, pave * _StoneTextureStrength);
                float3 halfDirection = SafeNormalize(light.direction + GetWorldSpaceNormalizeViewDir(input.positionWS));
                half roughHighlight = pow(saturate(dot(normalWS, halfDirection)), 24.0h) * 0.045h;
                color += roughHighlight * light.color * saturate(ndl) * visibility * naturalSurface * ao.directAmbientOcclusion;
                color += _CorruptGlow.rgb * vein * pulse;
                float beaconFalloff = saturate(1.0 - distance(input.positionWS.xz, _BeaconLight.xz) / 18.0);
                color += albedo * half3(1.0, 0.62, 0.22) * beaconFalloff * beaconFalloff * _BeaconLight.w;
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
