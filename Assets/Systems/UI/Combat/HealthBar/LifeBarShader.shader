Shader "Custom/LifeBarShader"
{
    Properties
    {
        // Unity UI requires this property to exist. 
        // [PerRendererData] hides it in the inspector because the Image component fills it automatically.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FilledColor("Filled Color", Color) = (0.2, 0.8, 0.2, 1) // Green
        _DepletingColor("Depleting Color", Color) = (0.8, 0.1, 0.1, 1) // Red
        _DepletedColor("Depleted Color", Color) = (0, 0, 0, 0) // Transparent

        _PatternTexture("Scrolling Pattern", 2D) = "white" {}
        _ScrollSpeed("Scroll Speed", Float) = 1.0

        _Health("Current Health", Range(0.0, 1.0)) = 0.5
        _HealthCatchup("Catchup Health", Range(0.0, 1.0)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            // Standard transparent blending
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // Declare our main shape mask (from the UI Image)
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Declare our scrolling pattern
            TEXTURE2D(_PatternTexture);
            SAMPLER(sampler_PatternTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _FilledColor;
                half4 _DepletingColor;
                half4 _DepletedColor;

                float4 _PatternTexture_ST;
                float _ScrollSpeed;

                float _Health;
                float _HealthCatchup;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Read the alpha from the actual Sprite assigned to the UI Image
                half4 shapeMask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 2. Calculate the health ranges
                half maskFilled = step(IN.uv.x, _Health);
                half maskCatchup = step(IN.uv.x, _HealthCatchup);
                half maskDepleting = maskCatchup - maskFilled;
                half maskDepleted = 1.0 - maskCatchup;

                // 3. Scroll the pattern texture
                float2 patternUv = TRANSFORM_TEX(IN.uv.yx, _PatternTexture);
                patternUv.y += _Time.y * _ScrollSpeed;
                half4 patternColor = SAMPLE_TEXTURE2D(_PatternTexture, sampler_PatternTexture, patternUv);

                // 4. Combine the health colors
                half4 finalColor = (_FilledColor * maskFilled) +
                    (_DepletingColor * maskDepleting) +
                    (_DepletedColor * maskDepleted);

                // 5. Apply the scrolling pattern
                finalColor *= patternColor;

                // 6. Clip the final result using the Image sprite's alpha channel
                finalColor.a *= shapeMask.a;

                return finalColor;
            }
            ENDHLSL
        }
    }
}