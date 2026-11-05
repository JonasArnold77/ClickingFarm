// Metall-Ueberzug fuer Ernte-Pflanzen und Erntestuecke (Bronze, Silber, Gold) – nur auf den gruenen Teilen (Blaetter):
// Farbe = Metallton (Vertexfarbe des SpriteRenderers) mal Helligkeit des Bildes – die Schattierungen darunter bleiben
// erkennbar, helle Stellen bekommen einen Glanz. Deckkraft = Alpha des Bildes mal Alpha der Vertexfarbe.
Shader "SeedClicker/MetalCoat"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Sheen ("Glanz", Range(0, 2)) = 0.9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Sheen;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                half lum = dot(t.rgb, half3(0.3, 0.59, 0.11));
                // Kontrast etwas anheben, damit Blaetter und Fruechte als Metall-Relief wirken
                half k = saturate((lum - 0.08) * 1.35);
                half3 c = i.color.rgb * (0.22 + 1.05 * k) + pow(k, 4) * _Sheen * 0.55;
                // nur die gruenen Teile (Blaetter, Stiele) werden Metall – Fruechte und Koerner behalten ihre Farbe
                half green = saturate((t.g - max(t.r, t.b)) * 7.0);
                return fixed4(c, t.a * i.color.a * green);
            }
            ENDCG
        }
    }
}
