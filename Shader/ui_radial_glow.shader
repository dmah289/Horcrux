Shader "Horcrux/ui_radial_glow"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0.082, 0.180, 0.369, 1)
        _CenterColor ("Center Color", Color) = (0.141, 0.290, 0.588, 1)
        _Center ("Center (UV)", Vector) = (0.5, 0.57, 0, 0)
        _Radius ("Radius (x rect height)", Float) = 0.38
        _Falloff ("Falloff", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            half4 _EdgeColor, _CenterColor;
            float4 _Center;
            half _Radius, _Falloff;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Rect aspect from pixel derivatives: radius stays in rect-height units on any screen.
                float aspect = abs(ddy(i.uv.y)) / abs(ddx(i.uv.x));
                float2 offset = i.uv - _Center.xy;
                offset.x *= aspect;
                half t = saturate(1 - length(offset) / _Radius);
                return lerp(_EdgeColor, _CenterColor, pow(t, _Falloff)) * i.color;
            }
            ENDCG
        }
    }
}