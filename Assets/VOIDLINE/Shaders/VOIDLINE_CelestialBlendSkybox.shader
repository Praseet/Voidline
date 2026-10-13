Shader "Skybox/VOIDLINE Celestial Blend"
{
    Properties
    {
        _TexA ("Current Space Panorama", 2D) = "black" {}
        _TexB ("Next Space Panorama", 2D) = "black" {}
        _Blend ("Sector Transition", Range(0, 1)) = 0
        _Tint ("Sky Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _TexA;
            sampler2D _TexB;
            half _Blend;
            half4 _Tint;
            half _Exposure;
            half _Rotation;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            float2 DirectionToEquirectangular(float3 direction)
            {
                direction = normalize(direction);
                float2 uv;
                uv.x = atan2(direction.x, direction.z) * 0.15915494309 + 0.5 + _Rotation / 360.0;
                uv.y = asin(clamp(direction.y, -1.0, 1.0)) * 0.31830988618 + 0.5;
                uv.x = frac(uv.x);
                return uv;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = DirectionToEquirectangular(i.direction);
                half3 a = tex2D(_TexA, uv).rgb;
                half3 b = tex2D(_TexB, uv).rgb;
                half3 nebula = lerp(a, b, saturate(_Blend));
                nebula *= _Tint.rgb * _Exposure;
                return half4(nebula, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
