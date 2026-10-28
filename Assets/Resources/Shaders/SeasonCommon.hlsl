// Jahreszeiten fuer die frei gezeichnete Welt. Globale Werte (Shader.SetGlobalFloat):
//   _SeasonAutumn 0..1  -> gruenes Laub/Gras wird gold, orange, rostrot
//   _SeasonWinter 0..1  -> Schnee auf Gruenem, Reif ueberall, kuehlere Farben
// "Gruen" wird an der Farbe erkannt, so bleiben Stamm, Holz, Stein und Erde unveraendert.
#ifndef SEEDCLICKER_SEASON_COMMON
#define SEEDCLICKER_SEASON_COMMON

float _SeasonAutumn;
float _SeasonWinter;

float SeasonHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float SeasonNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = SeasonHash(i), b = SeasonHash(i + float2(1, 0)), c = SeasonHash(i + float2(0, 1)), d = SeasonHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// snowBias: >0 auch Nicht-Gruenes bekommt Schnee (Boden), <0 nur Gruenes (Baumkronen, Gras)
// evergreen: 1 = Nadelbaum, bleibt im Herbst gruen
half3 ApplySeason(half3 col, float2 posWS, half snowBias, half evergreen)
{
    half lum = dot(col, half3(0.3h, 0.59h, 0.11h));
    half green = saturate((col.g - max(col.r, col.b)) * 5.0h);
    float n = SeasonNoise(posWS * 0.7);
    float n2 = SeasonNoise(posWS * 2.3 + 7.1);

    // Herbst: Boden ocker-oliv mit Laubflecken, Baumkronen je Baum gold, orange oder rot
    half3 autumn;
    if (snowBias > 0.1h)
    {
        half3 au = lerp(half3(0.78h, 0.66h, 0.32h), half3(0.6h, 0.52h, 0.24h), (half)n2);
        half spots = saturate(((half)SeasonNoise(posWS * 1.6 + 3.3) - 0.6h) * 5.0h);
        au = lerp(au, half3(0.92h, 0.46h, 0.16h), spots * 0.7h);
        autumn = au * (lum * 1.45h + 0.04h);
    }
    else
    {
        half pick = saturate(((half)SeasonNoise(posWS * 0.32 + 11.0) - 0.5h) * 2.6h + 0.5h);   // pro Baum eine andere Farbe
        half3 gold = half3(1.0h, 0.66h, 0.16h), orange = half3(0.98h, 0.4h, 0.1h), red = half3(0.8h, 0.16h, 0.08h);
        half3 au = pick < 0.5h ? lerp(gold, orange, pick * 2.0h) : lerp(orange, red, (pick - 0.5h) * 2.0h);
        au = lerp(au, au * 1.25h, (half)n2 * 0.5h);   // Lichtflecken im Laub
        autumn = au * (lum * 1.6h + 0.06h);
    }
    col = lerp(col, autumn, green * (half)_SeasonAutumn * (1.0h - evergreen));

    // Winter
    half snow = saturate(green * 1.4h + snowBias + ((half)n - 0.5h) * 0.6h);
    half3 snowCol = half3(0.86h, 0.91h, 1.0h) * (0.78h + lum * 0.35h + (half)n2 * 0.08h);
    col = lerp(col, snowCol, snow * (half)_SeasonWinter);
    col = lerp(col, col * half3(0.9h, 0.95h, 1.08h) + 0.05h, (half)_SeasonWinter * 0.5h);
    return col;
}

#endif
