Shader "UI/SharedCursorBorder"
{
    // Hollow, rounded-rectangle UI cursor border. Draws only a frame at the rect edge (transparent
    // centre) so it can render on top of any selectable without covering it. Thickness is proportional
    // to the on-screen size of the quad, so it reads the same on small and large buttons. The frame is
    // tinted by a diagonal, slowly rotating split between two player colours; set both colours equal
    // for a single-player cursor (the split becomes invisible).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Player0Color ("Player 0 Colour", Color) = (0, 0.8, 0.7647, 1)
        _Player1Color ("Player 1 Colour", Color) = (0.8, 0, 0, 1)
        _Thickness ("Colour Split Softness", Range(0, 1)) = 0.3
        _BorderFraction ("Border Width (pixels)", Range(1, 20)) = 3
        _CornerRadius ("Corner Radius (fraction of size)", Range(0, 1)) = 0.25
        _CutSize ("Cut Corner Size (TL/BR, fraction)", Range(0, 1)) = 0.3
        _RotateSpeed ("Split Rotate Speed", Float) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Player0Color;
            fixed4 _Player1Color;
            float _Thickness;
            float _BorderFraction;
            float _CornerRadius;
            float _CutSize;
            float _RotateSpeed;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            float2 RotateUV(float2 uv, float angle)
            {
                float2 c = uv - 0.5;
                float s = sin(angle);
                float co = cos(angle);
                return float2(c.x * co - c.y * s, c.x * s + c.y * co) + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // On-screen pixel size of the quad, from UV derivatives.
                float2 sizePx = 1.0 / max(fwidth(uv), 1e-5);   // quad size in pixels (width, height)
                float minDim = min(sizePx.x, sizePx.y);

                // Fixed pixel thickness regardless of button size.
                float thickPx = _BorderFraction;

                // Pixel coordinates from the centre.
                float2 p = (uv - 0.5) * sizePx;

                // Round only the top-right and bottom-left corners (same sign of x and y); keep the
                // top-left and bottom-right square so the chamfer below cuts them cleanly. If they were
                // rounded too, the rounding inset would mask the cut and small _CutSize would do nothing.
                float radiusPx = (p.x * p.y > 0.0) ? _CornerRadius * minDim * 0.5 : 0.0;
                float2 q = abs(p) - sizePx * 0.5 + radiusPx;
                float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radiusPx;

                // Chamfer (cut) the square top-left and bottom-right corners with two 45-degree
                // half-planes. _CutSize 0 leaves them square; larger cuts deeper.
                float cutPx = _CutSize * minDim * 0.5;
                float diag = (sizePx.x + sizePx.y) * 0.5 * 0.70710678;
                float cutTL = (-p.x + p.y) * 0.70710678 - (diag - cutPx);
                float cutBR = ( p.x - p.y) * 0.70710678 - (diag - cutPx);
                dist = max(dist, max(cutTL, cutBR));

                // Ring of width thickPx just inside the edge, antialiased over ~1px.
                float aa = max(fwidth(dist), 1e-4);
                float frame = saturate((1.0 - smoothstep(-aa, aa, dist))
                                       - (1.0 - smoothstep(-aa, aa, dist + thickPx)));

                // Diagonal, slowly rotating split between the two player colours.
                float2 ruv = RotateUV(uv, _Time.y * _RotateSpeed);
                float t = smoothstep(0.5 - _Thickness * 0.5, 0.5 + _Thickness * 0.5, ruv.x);
                fixed4 col = lerp(_Player0Color, _Player1Color, t);

                col.rgb *= i.color.rgb;
                col.a *= frame * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
