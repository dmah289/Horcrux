Shader "Horcrux/ui_radial_glow"
{
    Properties
    {
        // UGUI Graphic assigns its texture here every canvas rebuild; declared only to keep that call valid.
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _EdgeColor ("Edge Color", Color) = (0.082, 0.180, 0.369, 1)
        _CenterColor ("Center Color", Color) = (0.141, 0.290, 0.588, 1)
        _Center ("Center (UV)", Vector) = (0.5, 0.57, 0, 0)
        _Radius ("Radius (x rect height)", Float) = 0.38
        _Falloff ("Falloff", Float) = 2

        // Mask writes these per material instance; without them the glow ignores Mask and logs a missing-property warning.
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // RectMask2D enables this keyword and feeds _ClipRect in canvas space.
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float4 canvasPosition : TEXCOORD1; };

            half4 _EdgeColor, _CenterColor;
            float4 _Center;
            half _Radius, _Falloff;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.canvasPosition = v.vertex;
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
                half4 color = lerp(_EdgeColor, _CenterColor, pow(t, _Falloff)) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.canvasPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
