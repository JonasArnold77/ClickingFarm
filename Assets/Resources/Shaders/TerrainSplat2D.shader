// Boden-Material fuer die frei gezeichnete Welt (URP 2D, beleuchtet durch 2D-Lichter).
// _MainTex ist die Splat-Maske (vom SpriteRenderer): R = Schlamm, G = Erdweg, B = Blumenwiese.
// Die Boden-Texturen werden in Weltkoordinaten gekachelt; Uebergaenge werden mit Rauschen
// ausgefranst und auf das Pixelraster gerastet, damit sie wie gemalte Pixel-Art aussehen.
Shader "SeedClicker/TerrainSplat2D"
{
    Properties
    {
        _MainTex("Splat Mask (R Schlamm, G Weg, B Wiese)", 2D) = "black" {}
        _GrassTex("Gras", 2D) = "white" {}
        _MudTex("Schlamm", 2D) = "white" {}
        _DirtTex("Erdweg", 2D) = "white" {}
        _MeadowTex("Blumenwiese", 2D) = "white" {}
        _NoiseTex("Rauschen", 2D) = "gray" {}
        _TexWorldSize("Texturgroesse in Welteinheiten", Float) = 4
        _PixelsPerUnit("Pixel pro Einheit", Float) = 32
        _EdgeNoise("Ausfransen der Kanten", Range(0, 1)) = 0.35
        _EdgeSharpness("Kantenschaerfe", Range(1, 30)) = 10
        _RimDarken("Dunkler Rand am Schlamm", Range(0, 1)) = 0.25

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    // Gemeinsame Mischfunktion fuer beide Passes
    #define TERRAIN_BLEND(posWS, uv, outColor) \
        { \
            float2 snapped = floor(posWS * _PixelsPerUnit) / _PixelsPerUnit; \
            float2 tuv = snapped / _TexWorldSize; \
            half4 splat = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv); \
            half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, snapped * 0.35).r - 0.5h; \
            half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, snapped * 1.3 + 0.37).r - 0.5h; \
            half nn = n * 0.7h + n2 * 0.3h; \
            half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, tuv).rgb; \
            half3 meadow = SAMPLE_TEXTURE2D(_MeadowTex, sampler_MeadowTex, tuv).rgb; \
            half3 mud = SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, tuv).rgb; \
            half3 dirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, tuv).rgb; \
            half wMeadow = saturate((splat.b + nn * _EdgeNoise - 0.5h) * _EdgeSharpness * 0.5h + 0.5h); \
            half wDirt = saturate((splat.g + nn * _EdgeNoise - 0.5h) * _EdgeSharpness + 0.5h); \
            half wMud = saturate((splat.r + nn * _EdgeNoise - 0.5h) * _EdgeSharpness + 0.5h); \
            half3 tcol = lerp(grass, meadow, wMeadow); \
            half rimD = 1.0h - _RimDarken * saturate(1.0h - abs(wDirt - 0.5h) * 4.0h); \
            tcol = lerp(tcol, dirt, wDirt) * rimD; \
            half rimM = 1.0h - _RimDarken * saturate(1.0h - abs(wMud - 0.5h) * 3.0h); \
            tcol = lerp(tcol, mud, wMud) * rimM; \
            outColor = tcol; \
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
                float2  posWS       : TEXCOORD2;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_GrassTex);  SAMPLER(sampler_GrassTex);
            TEXTURE2D(_MudTex);    SAMPLER(sampler_MudTex);
            TEXTURE2D(_DirtTex);   SAMPLER(sampler_DirtTex);
            TEXTURE2D(_MeadowTex); SAMPLER(sampler_MeadowTex);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;
            float _TexWorldSize, _PixelsPerUnit;
            half _EdgeNoise, _EdgeSharpness, _RimDarken;

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
                o.posWS = TransformObjectToWorld(v.positionOS).xy;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 frag(Varyings i) : SV_Target
            {
                half3 col;
                TERRAIN_BLEND(i.posWS, i.uv, col)
                col *= i.color.rgb;
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(col, 1.0h, half4(1, 1, 1, 1), surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        // Unbeleuchteter Fallback (z. B. Szene-Ansicht ohne 2D-Renderer)
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 posWS : TEXCOORD1; };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_GrassTex);  SAMPLER(sampler_GrassTex);
            TEXTURE2D(_MudTex);    SAMPLER(sampler_MudTex);
            TEXTURE2D(_DirtTex);   SAMPLER(sampler_DirtTex);
            TEXTURE2D(_MeadowTex); SAMPLER(sampler_MeadowTex);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            float4 _MainTex_ST;
            float _TexWorldSize, _PixelsPerUnit;
            half _EdgeNoise, _EdgeSharpness, _RimDarken;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.posWS = TransformObjectToWorld(v.positionOS).xy;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 col;
                TERRAIN_BLEND(i.posWS, i.uv, col)
                return half4(col * i.color.rgb, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
