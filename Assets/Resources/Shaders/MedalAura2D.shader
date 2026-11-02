// Wabernde Medaillen-Sphaere (URP 2D, unbeleuchtet – leuchtet auch nachts).
// Der Rand der Blase verformt sich staendig (mehrere Wellen laufen gegeneinander um den Kreis),
// innen ziehen weiche Schlieren, zum Rand hin wird sie dichter (wie eine Seifenblase), oben links ein Glanzpunkt.
// Farbe kommt vom SpriteRenderer (Bronze, Silber, Gold); _Seed versetzt jede Blase, damit nicht alle gleich wabern.
Shader "SeedClicker/MedalAura2D"
{
    Properties
    {
        _MainTex("Sprite (nur fuer die UV)", 2D) = "white" {}
        _Speed("Geschwindigkeit", Float) = 1
        _Wobble("Staerke des Waberns", Float) = 1
        _Seed("Versatz", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float _Speed, _Wobble, _Seed;

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

    half4 frag(Varyings i) : SV_Target
    {
        float2 p = i.uv * 2.0 - 1.0;
        float r = length(p);
        float a = atan2(p.y, p.x);
        float t = _Time.y * _Speed + _Seed;

        // Rand: drei Wellen mit verschiedener Zahl an Beulen laufen gegeneinander -> organisches Wabern
        float wob = (0.04 * sin(a * 3.0 + t * 1.7) + 0.025 * sin(a * 5.0 - t * 2.3) + 0.015 * sin(a * 8.0 + t * 3.4)) * _Wobble;
        float R = 0.8 + wob + 0.025 * sin(t * 1.1);
        float inside = 1.0 - smoothstep(R - 0.025, R + 0.025, r);

        // Seifenblase: innen fast klar, zum Rand dichter; leuchtender Saum knapp am Rand
        float k = saturate(r / R);
        float shell = k * k * k;
        float e = (r - R) / 0.045;   // (pow mit negativer Basis waere in HLSL undefiniert)
        float rim = exp(-e * e);

        // innen treibende Schlieren
        float swirl = 0.5 + 0.5 * sin(a * 2.0 + k * 7.0 - t * 1.6) * sin(a * 3.0 - k * 5.0 + t * 1.1);

        // Glanzpunkt oben links (wandert leicht mit)
        float2 h = p - float2(-0.33 + 0.04 * sin(t * 0.9), 0.38 + 0.03 * cos(t * 1.2));
        float spot = exp(-dot(h, h) / 0.018) * inside;

        // dezent: zarte Huelle, schmaler Saum, kleiner Glanz
        half alpha = inside * (0.02 + 0.18 * shell + 0.05 * swirl * k) + rim * 0.32 + spot * 0.25;
        half3 col = i.color.rgb * (0.9 + 0.15 * rim + 0.1 * swirl) + spot * 0.3;
        return half4(col, saturate(alpha) * i.color.a);
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
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
