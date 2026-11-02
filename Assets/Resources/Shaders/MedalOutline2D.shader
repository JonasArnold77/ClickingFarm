// Leuchtende Medaillen-Kontur (URP 2D, unbeleuchtet – leuchtet auch nachts).
// Liegt als deckungsgleiche Kopie ueber dem Bild und zeichnet nur den Rand der Silhouette: eine schmale
// Leuchtlinie, die sich genau an die Aussenlinie schmiegt (innen am Rand, aussen wo Platz ist), und einen
// zarten, leicht pulsierenden Schein, der in sanften Strahlen um die Form wandert. Farbe kommt vom SpriteRenderer (Bronze, Silber, Gold).
// _UVRect: UV-Bereich des Bildes im Atlas (Proben ausserhalb zaehlen als leer), _Grow: Vergroesserung der Kopie.
Shader "SeedClicker/MedalOutline2D"
{
    Properties
    {
        _MainTex("Sprite", 2D) = "white" {}
        _UVRect("UV-Bereich (min x, min y, max x, max y)", Vector) = (0, 0, 1, 1)
        _Grow("Vergroesserung der Kopie", Float) = 1
        _Width("Linienbreite (Anteil der Bildgroesse)", Float) = 0.015
        _Glow("Scheinbreite (Anteil der Bildgroesse)", Float) = 0.04
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
    float _Grow, _Width, _Glow, _Seed;

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
        half inMin = 1, outMax = 0, glow = 0;
        [unroll] for (int k = 0; k < 12; k++)
        {
            float ang = k * 0.5235988;   // 30 Grad
            float2 d = float2(cos(ang), sin(ang)) * px;
            half a1 = A(uv + d * _Width);
            inMin = min(inMin, a1);
            outMax = max(outMax, a1);
            glow = max(glow, A(uv + d * _Glow) * 0.6);
        }
        // Linie genau an der Silhouette: ein schmaler Streifen innen am Rand und (wo Platz ist) direkt aussen
        half inner = saturate(self - inMin) * step(0.1, self);
        half outer = saturate(outMax - self);
        half ring = saturate(inner + outer);
        glow = saturate(glow - self);

        // leichtes Strahlen: der Schein aussen pulsiert, ein paar weiche Strahlen wandern um die Form
        float t = _Time.y + _Seed;
        float2 p = i.uv - c;
        float rays = 0.7 + 0.3 * sin(atan2(p.y, p.x) * 7.0 + t * 1.3);
        float pulse = 0.85 + 0.15 * sin(t * 2.1);

        half alpha = saturate(ring * 0.9 * pulse + glow * 0.35 * rays * pulse);
        half3 col = i.color.rgb * (1.0 + 0.4 * ring);
        return half4(col, alpha * i.color.a);
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
