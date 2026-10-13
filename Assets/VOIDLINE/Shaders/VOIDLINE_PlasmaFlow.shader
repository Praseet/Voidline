Shader "VOIDLINE/PlasmaFlow"
{
    Properties
    {
        [HDR] _BaseColor ("Plasma Tint", Color) = (0.02, 0.5, 1, 0.7)
        _Glow ("Glow Energy", Range(0, 4)) = 1
        _Boost ("Boost Blend", Range(0, 1)) = 0
        _FlowMin ("Flow Start", Float) = 0
        _FlowInvLength ("Inverse Flow Length", Float) = 1
        _FlowWidth ("Plume Width", Float) = 0.3
        _IsSheet ("Is Energy Sheet", Float) = 0
        _ScrollSpeed ("Plasma Flow Speed", Range(0, 8)) = 1.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "PlasmaForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Glow;
                float _Boost;
                float _FlowMin;
                float _FlowInvLength;
                float _FlowWidth;
                float _IsSheet;
                float _ScrollSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate((input.positionOS.y - _FlowMin) * max(0.0001, _FlowInvLength));
                float tailFade = 1.0 - smoothstep(0.80, 1.0, t);
                float nozzleHeat = 0.74 + 0.26 * (1.0 - smoothstep(0.02, 0.58, t));

                // Two travelling, incommensurate waves make narrow luminous filaments move
                // through the plasma instead of making the whole surface pulse uniformly.
                float phaseA = t * 82.0 - _Time.y * _ScrollSpeed * (5.2 + _Boost * 6.0)
                    + sin(input.positionOS.x * 22.0 + input.positionOS.z * 17.0
                    + _Time.y * 2.4) * 2.4;
                float phaseB = t * 39.0 + input.positionOS.x * 13.0
                    - input.positionOS.z * 19.0 - _Time.y * _ScrollSpeed * (2.8 + _Boost * 4.4);
                float streakA = pow(saturate(0.5 + 0.5 * sin(phaseA)), 7.0);
                float streakB = pow(saturate(0.5 + 0.5 * sin(phaseB)), 5.0);

                float warp = sin(input.positionOS.x * 11.0 + input.positionOS.z * 15.0
                    + t * 31.0 - _Time.y * _ScrollSpeed * (1.5 + _Boost * 3.0));
                float grain = sin(input.positionOS.x * 31.0 - input.positionOS.z * 27.0
                    + t * 73.0 + _Time.y * _ScrollSpeed * (2.2 + _Boost * 3.8));
                float fluid = saturate(0.79 + 0.12 * warp + 0.09 * grain);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), 2.0);

                float sheetVein = _IsSheet * (0.20 + 0.62 * streakA + 0.31 * streakB);
                float tubeVein = (1.0 - _IsSheet) * (0.60 + 0.32 * streakA + 0.12 * streakB);
                float energy = (0.59 + 0.25 * fluid + 0.25 * rim + sheetVein + tubeVein);
                energy *= nozzleHeat * tailFade * (0.92 + 0.08 * sin(_Time.y * _ScrollSpeed * (2.8 + _Boost * 3.8) + t * 14.0));

                // Pressure cells travel through the 3D sheath from the nozzle toward the tail.
                // Keep the broad membranes smoother; compression flicker is strongest in the tubes.
                float shockCell = 0.84 + 0.16 * cos(t * 24.0 - _Time.y * (3.8 + _Boost * 5.2)) * exp(-t * 0.72);
                energy *= lerp(shockCell, 1.0, _IsSheet);
                energy *= _Glow * (0.66 + _Boost * 0.92);

                half3 rgb = _BaseColor.rgb * (half)max(0.0, energy);
                half alpha = (half)(_BaseColor.a * tailFade * (0.62 + 0.24 * fluid + 0.24 * streakA)
                    * (0.80 + _Boost * 0.44));
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
