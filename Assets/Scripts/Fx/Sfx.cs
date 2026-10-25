using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>Platzhalter-Sounds, zur Laufzeit synthetisiert (keine Audiodateien noetig).</summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx inst;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        int next;
        const int Rate = 44100;

        void Awake()
        {
            inst = this;
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++) { pool[i] = gameObject.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
            Build();
        }

        public static void Play(string name, float pitch = 1f, float volume = 1f)
        {
            if (inst == null || (Game.I != null && !Game.I.S.sound)) return;
            if (!inst.clips.TryGetValue(name, out var clip)) return;
            var src = inst.pool[inst.next];
            inst.next = (inst.next + 1) % inst.pool.Length;
            src.pitch = pitch * Random.Range(0.96f, 1.04f);
            src.volume = volume;
            src.PlayOneShot(clip);
        }

        delegate float Wave(float t, float p);

        void Build()
        {
            clips["pop"] = Tone(0.09f, (t, p) => Sin(Lerp(520, 980, p) * t) * Env(p, 0.01f, 0.6f), 0.5f);
            clips["grow"] = Tone(0.11f, (t, p) => (Sin(660 * t) * 0.7f + Sin(1320 * t) * 0.3f) * Env(p, 0.005f, 0.8f), 0.45f);
            clips["crit"] = Tone(0.35f, (t, p) => (Sin(Lerp(880, 1760, p) * t) + Sin(Lerp(1100, 2200, p) * t) * 0.5f) * Env(p, 0.01f, 0.5f), 0.4f);
            clips["chop"] = Tone(0.08f, (t, p) => (Noise() * 0.7f + Sin(180 * t) * 0.6f) * Env(p, 0.002f, 0.9f), 0.5f);
            clips["rock"] = Tone(0.09f, (t, p) => (Noise() * 0.5f + Square(120 * t) * 0.4f) * Env(p, 0.002f, 0.9f), 0.45f);
            clips["break"] = Tone(0.22f, (t, p) => (Noise() * 0.6f + Sin(Lerp(300, 90, p) * t) * 0.6f) * Env(p, 0.002f, 0.7f), 0.55f);
            clips["dig"] = Tone(0.12f, (t, p) => (LowNoise(0.15f) * 0.9f + Sin(Lerp(140, 80, p) * t) * 0.4f) * Env(p, 0.005f, 0.8f), 0.6f);
            clips["water"] = Tone(0.25f, (t, p) => LowNoise(0.35f) * Env(p, 0.05f, 0.6f) + Sin(Lerp(900, 1500, p) * t) * 0.15f * Env(p, 0.01f, 0.4f), 0.5f);
            clips["sow"] = Tone(0.1f, (t, p) => (LowNoise(0.3f) * 0.5f + Sin(Lerp(400, 700, p) * t) * 0.5f) * Env(p, 0.005f, 0.8f), 0.45f);
            clips["harvest"] = Arp(new[] { 784f, 988f, 1175f }, 0.06f, 0.4f);
            clips["coin"] = Arp(new[] { 1318f, 1760f }, 0.06f, 0.35f);
            clips["levelup"] = Arp(new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.09f, 0.45f);
            clips["unlock"] = Arp(new[] { 659f, 880f, 1108f, 1318f }, 0.08f, 0.4f);
            clips["build"] = Tone(0.12f, (t, p) => (Square(Lerp(220, 160, p) * t) * 0.4f + Noise() * 0.4f) * Env(p, 0.001f, 0.95f), 0.5f);
            clips["click"] = Tone(0.04f, (t, p) => Sin(1200 * t) * Env(p, 0.001f, 0.9f), 0.3f);
            clips["error"] = Tone(0.18f, (t, p) => Square(Lerp(180, 140, p) * t) * Env(p, 0.01f, 0.6f), 0.25f);
            clips["sleep"] = Arp(new[] { 784f, 659f, 523f, 392f }, 0.16f, 0.3f);
            clips["morning"] = Arp(new[] { 392f, 523f, 659f, 784f }, 0.12f, 0.3f);
            // Knackiger Klick beim Wachsen: kurzer Rausch-Knack + tiefer Plopp + heller Klick
            clips["crunch"] = Tone(0.09f, (t, p) => (Noise() * 0.55f * Env(p, 0.001f, 1.6f) + Sin(Lerp(190, 70, p) * t) * 0.7f * Env(p, 0.003f, 0.7f) + Sin(2400 * t) * 0.25f * Env(p, 0.0005f, 2.5f)), 0.65f);
            clips["tick"] = Tone(0.18f, (t, p) => (Sin(1760 * t) + Sin(2640 * t) * 0.5f) * Env(p, 0.002f, 0.6f), 0.35f);
            clips["groan"] = Tone(0.85f, (t, p) => (Sin(Lerp(105, 78, p) * t) * 0.55f + Sin(Lerp(212, 150, p) * t) * 0.25f + LowNoise(0.15f) * 0.3f) * Env(p, 0.18f, 1.1f) * (0.7f + 0.3f * Sin(5.5f * t)), 0.5f);
            clips["bug"] = Arp(new[] { 1568f, 2093f, 1568f, 2349f }, 0.04f, 0.3f);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Sin(float x) => Mathf.Sin(x * Mathf.PI * 2f);
        static float Square(float x) => Mathf.Sin(x * Mathf.PI * 2f) >= 0 ? 0.6f : -0.6f;
        static readonly System.Random rnd = new System.Random(7);
        static float Noise() => (float)(rnd.NextDouble() * 2 - 1);
        static float lowState;
        static float LowNoise(float k) { lowState += (Noise() - lowState) * k; return lowState * 2f; }

        // Huellkurve: kurzer Anstieg, exponentielles Abklingen
        static float Env(float p, float attack, float decayShape)
        {
            if (p < attack) return p / attack;
            float d = (p - attack) / (1 - attack);
            return Mathf.Pow(1 - d, 1f + decayShape * 3f);
        }

        AudioClip Tone(float dur, Wave w, float gain)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; data[i] = Mathf.Clamp(w(t, i / (float)n) * gain, -1, 1); }
            var c = AudioClip.Create("sfx", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        AudioClip Arp(float[] notes, float noteDur, float gain)
        {
            int per = (int)(Rate * noteDur);
            int tail = (int)(Rate * 0.15f);
            int n = per * notes.Length + tail;
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = k * per, len = per + tail;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)Rate, p = i / (float)len;
                    data[start + i] += (Sin(notes[k] * t) * 0.7f + Sin(notes[k] * 2 * t) * 0.2f) * Env(p, 0.02f, 0.5f) * gain;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1, 1);
            var c = AudioClip.Create("arp", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
