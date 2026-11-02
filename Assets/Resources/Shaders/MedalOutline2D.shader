// Comic-Kontur der Medaillen (URP 2D, unbeleuchtet): eine harte, deckende Linie in Bronze, Silber oder Gold,
// die genau auf der Aussenlinie des Bildes liegt (innen am Rand und direkt aussen, wo das Bild Platz hat),
// aussen davon ein schmaler dunkler Tuschestrich. Kein Schein, kein Pulsieren.
// Liegt als deckungsgleiche Kopie ueber dem Bild. _UVRect: UV-Bereich des Bildes im Atlas (Proben ausserhalb zaehlen als leer).
Shader "SeedClicker/MedalOutline2D"
{
    Properties
    {
        _MainTex("Sprite", 2D) = "white" {}
        _UVRect("UV-Bereich (min x, min y, max x, max y)", Vector) = (0, 0, 1, 1)
        _Grow("Vergroesserung der Kopie", Float) = 1
        _Width("Linienbreite (Anteil der Bildgroesse)", Float) = 0.025
        _Seed("Versatz", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    float4 _MainTex_TexelSize;
    float4 _UVRect;
    float _Grow, _Width, _Seed;

    struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
    struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

    Varyings vert(Attributes v)
    {
        Varyings o;
        o.positionCS = TransformObjectToHClip(v.positionOS);
        o.uv = v.uv;
        o.color = v.color;
        return o;
    }

    // Deckkraft des Bildes an uv (ausserhalb seines Bereichs im Atlas: leer)
    half A(float2 uv)
    {
        if (uv.x < _UVRect.x || uv.y < _UVRect.y || uv.x > _UVRect.z || uv.y > _UVRect.w) return 0;
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
    }

    half4 frag(Varyings i) : SV_Target
    {
        // (_Grow: falls die Kopie vergroessert ist, zurueck auf die Groesse des Originals rechnen; normal 1)
        float2 c = (_UVRect.xy + _UVRect.zw) * 0.5;
        float2 uv = c + (i.uv - c) * _Grow;
        half self = A(uv);

        // Breiten in Texeln, gleich in x und y (sonst waere die Linie bei schmalen Bildern einseitig dick)
        float2 rectTex = (_UVRect.zw - _UVRect.xy) / _MainTex_TexelSize.xy;
        float2 px = _MainTex_TexelSize.xy * max(rectTex.x, rectTex.y);
        // Comic-Kontur: harte, deckende Linie in Medaillenfarbe genau an der Aussenlinie, aussen ein dunkler Tuschestrich
        half inMin = 1, outMax = 0, inkMax = 0;
        [unroll] for (int k = 0; k < 16; k++)
        {
            float ang = k * 0.3926991;   // 22,5 Grad
            float2 d = float2(cos(ang), sin(ang)) * px;
            half a1 = A(uv + d * _Width);
            inMin = min(inMin, a1);
            outMax = max(outMax, a1);
            inkMax = max(inkMax, A(uv + d * _Width * 1.6));
        }
        half solid = step(0.5, self);
        half band = saturate(solid * (1 - step(0.5, inMin)) + (1 - solid) * step(0.5, outMax));   // innen am Rand + direkt aussen
        half ink = (1 - solid) * (1 - step(0.5, outMax)) * step(0.5, inkMax);                     // schmaler dunkler Strich ganz aussen
        half3 col = lerp(i.color.rgb, i.color.rgb * 0.35, ink);
        return half4(col, saturate(band + ink) * i.color.a);
    }
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
