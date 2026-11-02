// Leuchtende Medaillen-Kontur (URP 2D, unbeleuchtet – leuchtet auch nachts).
// Liegt als vergroesserte Kopie hinter dem Bild und zeichnet nur, was ausserhalb der Silhouette liegt:
// eine klare Leuchtlinie direkt am Rand und ein weicher, leicht pulsierender Schein dahinter, der in
// sanften Strahlen um die Form wandert. Farbe kommt vom SpriteRenderer (Bronze, Silber, Gold).
// _UVRect: UV-Bereich des Bildes im Atlas (Proben ausserhalb zaehlen als leer), _Grow: Vergroesserung der Kopie.
Shader "SeedClicker/MedalOutline2D"
{
    Properties
    {
        _MainTex("Sprite", 2D) = "white" {}
        _UVRect("UV-Bereich (min x, min y, max x, max y)", Vector) = (0, 0, 1, 1)
        _Grow("Vergroesserung der Kopie", Float) = 1.2
        _Width("Linienbreite (Anteil der Bildgroesse)", Float) = 0.02
        _Glow("Scheinbreite (Anteil der Bildgroesse)", Float) = 0.07
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
        // die Kopie ist um _Grow vergroessert: zurueck auf die Groesse des Originals rechnen
        float2 c = (_UVRect.xy + _UVRect.zw) * 0.5;
        float2 uv = c + (i.uv - c) * _Grow;
        half self = A(uv);

        float2 px = _UVRect.zw - _UVRect.xy;   // Breiten im Verhaeltnis zur Bildgroesse (gleich dick bei jeder Aufloesung)
        half line_ = 0, glow = 0;
        [unroll] for (int k = 0; k < 12; k++)
        {
            float ang = k * 0.5235988;   // 30 Grad
            float2 d = float2(cos(ang), sin(ang)) * px;
            line_ = max(line_, A(uv + d * _Width));
            glow = max(glow, A(uv + d * _Glow * 0.55) * 0.75);
            glow = max(glow, A(uv + d * _Glow) * 0.4);
        }
        line_ = saturate(line_ - self);
        glow = saturate(glow - self);

        // leichtes Strahlen: der Schein pulsiert und ein paar weiche Strahlen wandern um die Form
        float t = _Time.y + _Seed;
        float2 p = i.uv - c;
        float rays = 0.75 + 0.25 * sin(atan2(p.y, p.x) * 7.0 + t * 1.3);
        float pulse = 0.8 + 0.2 * sin(t * 2.1);

        half alpha = saturate(line_ * 0.95 + glow * 0.55 * rays * pulse);
        half3 col = i.color.rgb * (1.0 + 0.35 * line_);
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
