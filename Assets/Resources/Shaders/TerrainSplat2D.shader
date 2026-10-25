// Boden-Material fuer die frei gezeichnete Welt (URP 2D, beleuchtet durch 2D-Lichter).
// Alle Bodentexturen kommen aus EINEM Atlas (Skyden "Ground Tiles", 8x8 Kacheln a 256 px).
// _MainTex ist die Splat-Maske (vom SpriteRenderer):
//   R = Schlamm, G = Erdweg, B = dunkle Wiese, A = Kopfsteinpflaster.
// Jede Bodenart ist ein Rechteck im Atlas (xy = Ecke, zw = Groesse in UV) und wird in
// Weltkoordinaten gekachelt. Uebergaenge werden mit Rauschen ausgefranst.
Shader "SeedClicker/TerrainSplat2D"
{
    Properties
    {
        _MainTex("Splat Mask", 2D) = "black" {}
        _Plateau("Plateau-Maske (R = begehbarer Boden, sonst Abgrund)", 2D) = "white" {}
        _Atlas("Boden-Atlas", 2D) = "white" {}
        _GrassRect("Gras (Atlas-Rechteck)", Vector) = (0.5, 0.875, 0.125, 0.125)
        _MeadowRect("Dunkles Gras", Vector) = (0.375, 0.875, 0.125, 0.125)
        _DirtRect("Erdweg", Vector) = (0.125, 0.25, 0.125, 0.125)
        _StoneRect("Kopfsteinpflaster", Vector) = (0.125, 0.625, 0.125, 0.125)
        _MudRect("Schlamm (Beet-Ausschnitt)", Vector) = (0.527, 0.526, 0.068, 0.068)
        _MudTint("Schlamm-Faerbung", Color) = (0.82, 0.78, 0.74, 1)
        _NoiseTex("Rauschen", 2D) = "gray" {}
        _TileWorld("Kachelgroesse in Welteinheiten", Float) = 1
        _EdgeNoise("Ausfransen der Kanten", Range(0, 1)) = 0.35
        _EdgeSharpness("Kantenschaerfe", Range(1, 30)) = 7
        _RimDarken("Dunkler Rand", Range(0, 1)) = 0.18

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    // Atlas-Kachel nahtlos wiederholen (mit 1 Texel Abstand zum Rand gegen Ausbluten)
    #define ATLAS_UV(rect, tuv) (rect.xy + (0.012 + frac(tuv) * 0.976) * rect.zw)
    // gespiegelt kacheln (fuer Ausschnitte, die selbst nicht nahtlos sind)
    #define ATLAS_UV_MIRROR(rect, tuv) (rect.xy + (0.01 + abs(frac((tuv) * 0.5) * 2.0 - 1.0) * 0.98) * rect.zw)

    #define TERRAIN_BLEND(posWS, uv, outColor) \
        { \
            float2 tuv = posWS / _TileWorld; \
            half4 splat = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv); \
            half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, posWS * 0.18).r - 0.5h; \
            half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, posWS * 0.9 + 0.37).r - 0.5h; \
            half nn = n * 0.65h + n2 * 0.35h; \
            half3 grass = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, ATLAS_UV(_GrassRect, tuv)).rgb; \
            half3 dark = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, ATLAS_UV(_MeadowRect, tuv * 0.5 + 0.31)).rgb; \
            half3 dirt = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, ATLAS_UV(_DirtRect, tuv)).rgb; \
            half3 stone = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, ATLAS_UV(_StoneRect, tuv)).rgb; \
            half3 mud = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, ATLAS_UV(_DirtRect, tuv * 1.3 + 0.37)).rgb; \
            half lum = dot(mud, half3(0.3h, 0.55h, 0.15h)); \
            mud = lerp(mud, lum.xxx, 0.25h) * half3(0.56h, 0.5h, 0.46h); \
            half pv = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, posWS * 0.55 + 0.71).r; \
            half puddle = saturate((pv - 0.56h) * 14.0h); \
            half shine = saturate((n2 + 0.05h) * 8.0h) * saturate((pv - 0.6h) * 14.0h); \
            mud *= 1.0h - saturate(1.0h - abs(pv - 0.56h) * 25.0h) * 0.3h; \
            mud = lerp(mud, half3(0.30h, 0.35h, 0.38h), puddle * 0.85h); \
            mud = lerp(mud, half3(0.7h, 0.78h, 0.82h), shine * 0.55h); \
            half wDark = saturate((splat.b + nn * _EdgeNoise - 0.5h) * _EdgeSharpness * 0.5h + 0.5h); \
            half wDirt = saturate((splat.g + nn * _EdgeNoise - 0.5h) * _EdgeSharpness + 0.5h); \
            half wStone = saturate((splat.a + nn * _EdgeNoise * 0.6h - 0.5h) * _EdgeSharpness * 1.5h + 0.5h); \
            half wMud = saturate((splat.r + nn * _EdgeNoise - 0.5h) * _EdgeSharpness + 0.5h); \
            half3 tcol = lerp(grass, dark, wDark * 0.85h) * (1.0h + n * 0.16h); \
            half rimD = 1.0h - _RimDarken * saturate(1.0h - abs(wDirt - 0.5h) * 3.0h); \
            tcol = lerp(tcol, dirt, wDirt) * rimD; \
            tcol = lerp(tcol, stone, wStone); \
            half rimM = 1.0h - _RimDarken * 1.5h * saturate(1.0h - abs(wMud - 0.5h) * 3.0h); \
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

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_Plateau);  SAMPLER(sampler_Plateau);
            TEXTURE2D(_Atlas);    SAMPLER(sampler_Atlas);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;
            float4 _GrassRect, _MeadowRect, _DirtRect, _StoneRect, _MudRect;
            half4 _MudTint;
            float _TileWorld;
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
                // Klippenrand: ausserhalb des Plateaus durchsichtig (dahinter liegt der Abgrund)
                half pl = SAMPLE_TEXTURE2D(_Plateau, sampler_Plateau, i.uv).r;
                half en = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.posWS * 1.7).r - 0.5h;
                half alpha = saturate((pl + en * 0.25h - 0.5h) * 14.0h + 0.5h);
                col *= 1.0h - saturate(1.0h - pl * 1.6h) * 0.35h;
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(col, alpha, half4(1, 1, 1, 1), surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
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
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 posWS : TEXCOORD1; };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_Plateau);  SAMPLER(sampler_Plateau);
            TEXTURE2D(_Atlas);    SAMPLER(sampler_Atlas);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            float4 _MainTex_ST;
            float4 _GrassRect, _MeadowRect, _DirtRect, _StoneRect, _MudRect;
            half4 _MudTint;
            float _TileWorld;
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
                half pl = SAMPLE_TEXTURE2D(_Plateau, sampler_Plateau, i.uv).r;
                return half4(col * i.color.rgb, saturate((pl - 0.5h) * 14.0h + 0.5h));
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
