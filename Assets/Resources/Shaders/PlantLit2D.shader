// Pflanzen-Material (URP 2D, beleuchtet durch die 2D-Lichter).
// Macht die flachen Happy-Harvest-Pflanzen plastisch: Die Normal-Map (Secondary Texture "_NormalMap",
// vom Sprite-Import automatisch gesetzt) wird mit einer festen Sonne von oben links schattiert,
// dazu ein warmer Lichtsaum und kuehle Schatten. Unten wird die Pflanze dunkler (Bodenschatten).
// _Flash faerbt die ganze Pflanze kurz weiss ein (Treffer-Blitz beim Klicken).
Shader "SeedClicker/PlantLit2D"
{
    Properties
    {
        _MainTex("Sprite", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _SunDir("Sonnenrichtung", Vector) = (-0.55, 0.65, 0.55, 0)
        _Shade("Staerke der Schattierung", Range(0, 1)) = 0.85
        _Ambient("Grundhelligkeit", Range(0, 1)) = 0.5
        _LightTint("Lichtfarbe", Color) = (1.08, 1.0, 0.88, 1)
        _ShadowTint("Schattenfarbe", Color) = (0.62, 0.72, 0.9, 1)
        _Rim("Lichtsaum", Range(0, 1)) = 0.35
        _BaseDarken("Bodenschatten unten", Range(0, 1)) = 0.35
        _Flash("Treffer-Blitz", Range(0, 1)) = 0
        _FlashColor("Blitzfarbe", Color) = (1, 1, 0.92, 1)

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    #define PLANT_SHADE(col, nrm, uv, outCol) \
        { \
            half3 L = normalize(_SunDir.xyz); \
            half ndl = dot(nrm, L); \
            half diff = saturate(ndl * 0.5h + 0.5h); \
            half3 lit = lerp(_ShadowTint.rgb, _LightTint.rgb, smoothstep(0.25h, 0.85h, diff)); \
            half3 shaded = col * lerp(half3(1, 1, 1), lit * (_Ambient + (1.0h - _Ambient) * diff * 1.25h), _Shade); \
            half rim = pow(saturate(1.0h - nrm.z), 2.0h) * saturate(dot(nrm.xy, L.xy) * 2.0h); \
            shaded += rim * _Rim * _LightTint.rgb * 0.6h; \
            shaded *= 1.0h - _BaseDarken * pow(saturate(1.0h - uv.y * 2.2h), 2.0h); \
            outCol = lerp(shaded, _FlashColor.rgb, _Flash); \
        }
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                half4   color       : COLOR;
                float2  uv          : TEXCOORD0;
                half2   lightingUV  : TEXCOORD1;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;
            float4 _SunDir;
            half _Shade, _Ambient, _Rim, _BaseDarken, _Flash;
            half4 _LightTint, _ShadowTint, _FlashColor;

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 nrm = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                half3 col;
                PLANT_SHADE(tex.rgb * i.color.rgb, nrm, i.uv, col)
                half alpha = tex.a * i.color.a;
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(col, alpha, half4(1, 1, 1, 1), surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                half4 res = CombinedShapeLightShared(surfaceData, inputData);
                // der Treffer-Blitz soll auch nachts hell bleiben
                res.rgb = lerp(res.rgb, _FlashColor.rgb, _Flash * 0.85h);
                return res;
            }
            ENDHLSL
        }

        // Unbeleuchteter Fallback
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _SunDir;
            half _Shade, _Ambient, _Rim, _BaseDarken, _Flash;
            half4 _LightTint, _ShadowTint, _FlashColor;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 nrm = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                half3 col;
                PLANT_SHADE(tex.rgb * i.color.rgb, nrm, i.uv, col)
                return half4(col, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
