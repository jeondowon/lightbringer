Shader "Lightbringer/Boss Rift"
{
    Properties
    {
        _Color ("Tint", Color) = (0.48,0.08,0.8,1)
        _Age ("Age", Float) = 0
        _Progress ("Telegraph", Float) = 0
        _Mode ("Surface", Float) = 0
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Age, _Progress, _Mode, _Opacity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            V Vert(A v)
            {
                V o;
                if (_Mode > 0.5 && _Mode < 1.5)
                    v.positionOS.y += sin(v.uv.x * 45 + _Age * 4) * v.uv.y * 0.7;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float2 uv = i.uv;
                float pulse = 0.6 + 0.4 * sin(_Age * 8);
                half3 rgb; float alpha;
                if (_Mode < 0.5)
                {
                    float r = length(uv * 2 - 1);
                    float ring = smoothstep(0.9,0.95,r) * (1-smoothstep(0.97,1,r));
                    float fill = step(r, saturate(_Progress)) * 0.18;
                    float spokes = pow(abs(sin(atan2(uv.y-.5,uv.x-.5) * 12)), 28) * 0.12;
                    rgb = lerp(_Color.rgb, half3(1,0.48,0.95), ring);
                    alpha = saturate((ring * (0.75 + pulse * .25) + fill + spokes) * (1-step(1.01,r)));
                }
                else if (_Mode < 1.5)
                {
                    float strands = pow(saturate(sin(uv.x*170 + sin(uv.x*37+_Age)*3 - _Age*3)), 3);
                    float drift = sin(uv.x*28-_Age*2 + uv.y*4) * .5 + .5;
                    rgb = lerp(half3(0.18,0.01,0.3),half3(0.12,1.1,0.8),drift);
                    rgb = lerp(rgb,half3(0.8,0.2,1.3),strands*.8);
                    alpha = (0.2+strands*.65) * pow(saturate(1-uv.y),1.3) * smoothstep(0,.06,uv.y);
                }
                else if (_Mode < 2.5)
                {
                    float edge = exp(-uv.y * 14);
                    float flow = sin(uv.x*65-uv.y*18-_Age*14) * sin(uv.x*29+uv.y*11+_Age*7);
                    float veins = pow(saturate(flow), 5) * (1-uv.y);
                    rgb = lerp(half3(.035,.008,.065),half3(1.6,.65,2.4),edge) + half3(.6,.08,1.2)*veins;
                    alpha = pow(saturate(sin(uv.x*3.14159)),.6) * pow(saturate(1-uv.y),.65);
                }
                else if (_Mode < 3.5)
                {
                    rgb = half3(.09,.025,.14) + _Color.rgb * pow(saturate(sin(uv.x*29+uv.y*19)),24)*.5;
                    alpha = 1;
                }
                else if (_Mode < 4.5)
                {
                    float border = step(.46,abs(uv.x-.5)) + step(.48,abs(uv.y-.5));
                    float bands = pow(saturate(sin(uv.y*60-_Age*6)),12);
                    rgb = lerp(half3(.22,.02,.34),half3(1,.25,1.1),saturate(border));
                    alpha = saturate(.2 + border*.65 + bands*.12 + _Progress*.1);
                }
                else if (_Mode < 5.5)
                {
                    float noise = frac(sin(dot(floor(uv*90),float2(12.9898,78.233))) * 43758.5453);
                    float dissolve = uv.y*.65 + noise*.35 - _Progress;
                    float edge = 1-smoothstep(0,.09,dissolve);
                    rgb = lerp(half3(.018,.005,.035),half3(.65,.12,1.1),edge);
                    alpha = step(0,dissolve)*.97;
                }
                else if (_Mode < 6.5)
                {
                    // Camera-facing energy ribbon: narrow white-violet core and a soft coloured skirt.
                    float across = abs(uv.y * 2 - 1);
                    float core = exp(-across * across * 80);
                    float halo = pow(saturate(1-across), 2);
                    float flicker = .87 + .13 * sin(_Age * 93 + uv.x * 24);
                    rgb = i.color.rgb * (halo + core * 1.7) * flicker;
                    alpha = saturate(halo + core) * i.color.a;
                }
                else if (_Mode < 7.5)
                {
                    // A dark, translucent pool with slow violet currents over a midnight-blue bed.
                    float2 p = uv * 2 - 1;
                    float r = length(p);
                    float angle = atan2(p.y, p.x);
                    float flow = .5 + .5 * sin(angle * 5 + r * 18 - _Age * 1.2 + sin(angle * 3 + _Age * .4));
                    float rim = exp(-pow((r - .87) * 50, 2));
                    float inner = exp(-pow((r - .68) * 70, 2));
                    float edge = 1-smoothstep(.82, 1, r);
                    rgb = lerp(half3(.006,.009,.045), half3(.16,.025,.25), flow * .7);
                    rgb += half3(.25,.045,.38) * (rim + inner * .4);
                    alpha = edge * (.3 + flow * .13) + rim * .5;
                }
                else
                {
                    float angle = uv.x * 6.283185;
                    float path = .25 + .13 * sin(angle * 3 - _Age * .6);
                    float ribbon = exp(-pow((uv.y-path) * 38, 2));
                    float wisps = pow(saturate(sin(angle * 13 + uv.y * 5 - _Age)), 4);
                    float fade = smoothstep(0,.1,uv.y) * pow(saturate(1-uv.y), 2);
                    float blend = .5 + .5 * sin(angle * 2 + _Age * .35);
                    rgb = lerp(half3(.008,.012,.065),half3(.23,.035,.34),blend);
                    rgb += half3(.14,.025,.22) * ribbon;
                    alpha = saturate((wisps * .4 + ribbon * .75) * fade);
                }
                return half4(rgb, saturate(alpha * _Opacity));
            }
            ENDHLSL
        }
    }
}
