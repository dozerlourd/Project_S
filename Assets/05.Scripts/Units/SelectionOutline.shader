Shader "ProjectS/SpriteSelectionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _InnerColor ("Inner Outline Color", Color) = (1, 0.72, 0.18, 0.98)
        _OuterColor ("Outer Outline Color", Color) = (0.78, 0.34, 0.03, 0.9)
        _UvMinMax ("Sprite UV Min Max", Vector) = (0, 0, 1, 1)
        _InnerWidth ("Inner Width Pixels", Float) = 6
        _OuterWidth ("Outer Width Pixels", Float) = 12
        _AlphaThreshold ("Alpha Threshold", Range(0.001, 0.99)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _InnerColor;
            fixed4 _OuterColor;
            float4 _UvMinMax;
            float _InnerWidth;
            float _OuterWidth;
            float _AlphaThreshold;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float SampleSpriteAlpha(float2 uv)
            {
                float2 uvMin = _UvMinMax.xy;
                float2 uvMax = _UvMinMax.zw;
                float inside =
                    step(uvMin.x, uv.x) * step(uvMin.y, uv.y) *
                    step(uv.x, uvMax.x) * step(uv.y, uvMax.y);

                float2 safeUv = clamp(uv, uvMin, uvMax);
                return tex2D(_MainTex, safeUv).a * inside;
            }

            void SampleDirection(float2 uv, float2 innerStep, float2 outerStep, float2 direction, inout float innerAlpha, inout float outerAlpha)
            {
                innerAlpha = max(innerAlpha, SampleSpriteAlpha(uv + direction * innerStep));
                outerAlpha = max(outerAlpha, SampleSpriteAlpha(uv + direction * outerStep));
            }

            void SampleOutlineAlpha(float2 uv, out float innerAlpha, out float outerAlpha)
            {
                float2 innerStep = _MainTex_TexelSize.xy * _InnerWidth;
                float2 outerStep = _MainTex_TexelSize.xy * _OuterWidth;
                innerAlpha = 0;
                outerAlpha = 0;

                SampleDirection(uv, innerStep, outerStep, float2( 1,  0), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2(-1,  0), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2( 0,  1), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2( 0, -1), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2( 0.70710678,  0.70710678), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2(-0.70710678,  0.70710678), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2( 0.70710678, -0.70710678), innerAlpha, outerAlpha);
                SampleDirection(uv, innerStep, outerStep, float2(-0.70710678, -0.70710678), innerAlpha, outerAlpha);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float centerAlpha = SampleSpriteAlpha(i.uv);
                float outsideSprite = 1 - step(_AlphaThreshold, centerAlpha);
                float innerAlpha;
                float outerAlpha;
                SampleOutlineAlpha(i.uv, innerAlpha, outerAlpha);

                float innerMask = outsideSprite * step(_AlphaThreshold, innerAlpha);
                float outerMask = outsideSprite * (1 - innerMask) * step(_AlphaThreshold, outerAlpha);
                fixed4 color = (_InnerColor * innerMask) + (_OuterColor * outerMask);
                color.a *= saturate(innerMask + outerMask);
                return color;
            }
            ENDCG
        }
    }
}
