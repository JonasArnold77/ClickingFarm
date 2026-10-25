// Wabernder grauer Nebel fuer den Abgrund (URP 2D).
// Zwei Rauschebenen treiben in verschiedene Richtungen und verzerren sich gegenseitig
// (Domain Warping) -> der Nebel quillt, zieht und wabert staendig.
Shader "SeedClicker/Fog2D"
{
    Properties
    {
        _MainTex("Sprite (ungenutzt)", 2D) = "white" {}
        _NoiseTex("Rauschen", 2D) = "gray" {}
        _Light("Heller Nebel", Color) = (0.82, 0.83, 0.84, 1)
        _Dark("Dunkler Nebel", Color) = (0.42, 0.44, 0.46, 1)
        _Scale("Groesse der Schwaden", Float) = 0.11
        _Speed("Geschwindigkeit", Float) = 1
        _Warp("Verwirbelung", Float) = 0.9

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
    half4 _Light, _Dark;
    float _Scale, _Speed, _Warp;

    struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
    struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 posWS : TEXCOORD0; };

    Varyings vert(Attributes v)
    {
        Varyings o;
        o.positionCS = TransformObjectToHClip(v.positionOS);
        o.posWS = TransformObjectToWorld(v.positionOS).xy;
        o.color = v.color;
        return o;
    }

    half N(float2 p) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p).r; }

    half4 frag(Varyings i) : SV_Target
    {
        float t = _Time.y * _Speed;
        float2 p = i.posWS * _Scale;
        // Verzerrungsfeld, das selbst langsam treibt
        float2 w = float2(N(p * 0.5 + float2(t * 0.013, t * 0.004)), N(p * 0.5 + float2(-t * 0.009, t * 0.011) + 0.43)) - 0.5;
        half a = N(p + w * _Warp + float2(t * 0.02, t * 0.006));
        half b = N(p * 2.1 - w * _Warp * 0.8 + float2(-t * 0.031, t * 0.012) + 0.71);
        half c = N(p * 0.45 + float2(t * 0.008, -t * 0.004) + 0.17);
        half d = saturate(a * 0.5h + b * 0.25h + c * 0.25h);
        d = smoothstep(0.3h, 0.72h, d);
        half3 col = lerp(_Dark.rgb, _Light.rgb, d) * i.color.rgb;
        return half4(col, i.color.a);
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
