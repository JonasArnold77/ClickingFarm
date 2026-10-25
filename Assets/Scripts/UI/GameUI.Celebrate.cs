using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    public partial class GameUI
    {
        readonly Queue<Action> celebrations = new Queue<Action>();
        bool celebrating, sleepingUI;
        RectTransform topLayer;

        RectTransform Top
        {
            get
            {
                if (topLayer == null) topLayer = UIKit.Rect(canvasRt, "Top").Stretch();
                topLayer.SetAsLastSibling();
                return topLayer;
            }
        }

        void QueueCelebration(Action a) => celebrations.Enqueue(a);

        void PumpCelebrations()
        {
            if (celebrating || sleepingUI || celebrations.Count == 0) return;
            CloseWindow();
            ClosePopup();
            celebrating = true;
            celebrations.Dequeue()();
        }

        // ------------------------------------------------------------------ Konfetti

        class Confetto { public RectTransform rt; public Image img; public Vector2 v; public float life, spin; }
        readonly List<Confetto> confetti = new List<Confetto>();
        static readonly string[] confettiColors = { "#FF5F8F", "#FFE45C", "#6CCB3F", "#4FA8F0", "#9B6BFF", "#FFA53D" };

        void Confetti(int n, Vector2 origin, float power = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                var img = UIKit.Img(Top, "c", Art.Get(UnityEngine.Random.value < 0.3f ? "px:star" : "white"), Balance.Hex(confettiColors[i % confettiColors.Length]));
                img.raycastTarget = false;
                img.preserveAspect = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(UnityEngine.Random.Range(18, 30), UnityEngine.Random.Range(12, 26));
                rt.anchoredPosition = origin;
                float a = UnityEngine.Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float sp = UnityEngine.Random.Range(900f, 2200f) * power;
                confetti.Add(new Confetto { rt = rt, img = img, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, spin = UnityEngine.Random.Range(-720f, 720f) });
            }
        }

        void UpdateConfetti(float dt)
        {
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var c = confetti[i];
                c.life += dt;
                c.v.y -= 2600f * dt;
                c.v *= 1f - Mathf.Clamp01(1.6f * dt);
                c.rt.anchoredPosition += c.v * dt;
                c.rt.Rotate(0, 0, c.spin * dt);
                float sx = Mathf.Abs(Mathf.Sin(c.life * 8f + i));
                c.rt.localScale = new Vector3(0.3f + sx * 0.7f, 1, 1);
                if (c.life > 3f || c.rt.anchoredPosition.y < -1400) { Destroy(c.rt.gameObject); confetti.RemoveAt(i); }
            }
        }

        // ------------------------------------------------------------------ Feier-Fenster

        Transform CelebrationPanel(out GameObject root, string title, Color titleColor)
        {
            root = MakeOverlay("Celebration", 0.72f, null);
            var go = root;

            // Strahlen hinter dem Fenster
            var glow = UIKit.Img(go.transform, "Glow", Art.Get("soft"), new Color(1f, 0.92f, 0.5f, 0.55f));
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1400, 1400));
            glow.raycastTarget = false;
            glow.gameObject.AddComponent<Pulse>().amount = 0.08f;
            for (int i = 0; i < 12; i++)
            {
                var ray = UIKit.Img(glow.transform, "Ray", Art.Get("white"), new Color(1f, 0.95f, 0.6f, 0.18f));
                ray.preserveAspect = false; ray.raycastTarget = false;
                var rrt = ray.rectTransform;
                rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0f);
                rrt.sizeDelta = new Vector2(70, 700); rrt.localRotation = Quaternion.Euler(0, 0, i * 30f);
            }
            StartCoroutine(Spin(glow.transform));

            var p = UIKit.Panel(go.transform, "Panel");
            var rt = p.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(940, 0);
            var col = p.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(40, 40, 40, 44); col.spacing = 20; col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var t = UIKit.Txt(p.transform, title, 84, titleColor, TextAnchor.MiddleCenter, true, true);
            t.LE(-1, 120);
            t.GetComponent<Outline>().effectDistance = new Vector2(5, -5);
            StartCoroutine(PopIn(p.transform, 0.3f));
            return p.transform;
        }

        IEnumerator Spin(Transform t)
        {
            while (t != null) { t.Rotate(0, 0, Time.unscaledDeltaTime * 20f); yield return null; }
        }

        void FinishCelebration(GameObject root)
        {
            CloseOverlay(root);
            celebrating = false;
            RefreshHud();
        }

        void ShowLevelUp(int level, List<Unlock> unlocks)
        {
            var p = CelebrationPanel(out var root, Loc.T("ui.levelup", level), Balance.Hex("#FFE45C"));
            var sub = UIKit.Txt(p, Loc.T("ui.unlocked"), 40, UIKit.TextMid, TextAnchor.MiddleCenter, true);
            sub.LE(-1, 60);

            var grid = UIKit.Rect(p, "Unlocks");
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(250, 270); gl.spacing = new Vector2(16, 16);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = Mathf.Clamp(unlocks.Count, 1, 3);
            gl.childAlignment = TextAnchor.MiddleCenter;
            int idx = 0;
            foreach (var u in unlocks)
            {
                var cell = UIKit.Panel(grid, "U", "paper");
                var ic = UIKit.Icon(cell.transform, Game.UnlockIcon(u), 150);
                ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(150, 150));
                ic.gameObject.AddComponent<Pulse>().amount = 0.06f;
                var nt = UIKit.Txt(cell.transform, u.name, 32, UIKit.TextDark, TextAnchor.MiddleCenter, true);
                nt.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(-16, 80));
                StartCoroutine(DelayedPop(cell.transform, 0.15f + idx++ * 0.12f));
            }
            var b = UIKit.Btn(p, Loc.T("ui.continue"), UIKit.BtnGreen, () => FinishCelebration(root), null, 46);
            b.LE(-1, 130);

            Confetti(90, new Vector2(0, -300), 1.1f);
            Sfx.Play("levelup");
            Haptics.Tap(50);
            CameraController.I?.Shake(0.15f);
        }

        IEnumerator DelayedPop(Transform t, float delay)
        {
            t.localScale = Vector3.zero;
            yield return new WaitForSecondsRealtime(delay);
            Sfx.Play("pop", 1.2f, 0.6f);
            yield return PopIn(t, 0f);
        }

        void ShowBigReward(string title, string subtitle, string icon)
        {
            var p = CelebrationPanel(out var root, title, Balance.Hex("#FFE45C"));
            var ic = UIKit.Icon(p, icon, 260);
            ic.gameObject.AddComponent<Pulse>().amount = 0.07f;
            StartCoroutine(DelayedPop(ic.transform, 0.1f));
            var s = UIKit.Txt(p, subtitle, 42, UIKit.TextDark, TextAnchor.MiddleCenter, true);
            s.LE(-1, 120);
            var b = UIKit.Btn(p, Loc.T("ui.continue"), UIKit.BtnGreen, () => FinishCelebration(root), null, 46);
            b.LE(-1, 130);
            Confetti(55, new Vector2(0, -200), 0.9f);
            Sfx.Play("unlock");
            Haptics.Tap(40);
        }

        void ShowWelcomeBack(OfflineSummary s)
        {
            var p = CelebrationPanel(out var root, Loc.T("ui.welcome_back"), Balance.Hex("#B8FF8A"));
            PopupText(p, Loc.T("ui.away_for", Fmt.Duration(s.seconds)), 38, UIKit.TextDark);
            if (s.grown > 0) PopupText(p, Loc.T("ui.offline_grown", s.grown));
            if (s.harvested > 0) PopupText(p, Loc.T("ui.offline_harvested", s.harvested));
            double total = s.value + s.honey;
            if (total > 0) PopupText(p, Loc.T("ui.offline_value", Fmt.Num(total)), 46, Balance.Hex("#C98A10"));
            if (total > 0)
                PopupButton(p, Loc.T("ui.double_ad"), UIKit.BtnPink, () => AdService.ShowRewarded(AdService.OfflineDouble, () =>
                {
                    g.RewardOfflineDouble();
                    Confetti(60, Vector2.zero);
                    FinishCelebration(root);
                }), "video");
            PopupButton(p, Loc.T("ui.collect"), UIKit.BtnGreen, () => FinishCelebration(root));
            Sfx.Play("morning");
        }

        // ------------------------------------------------------------------ Schlafen

        IEnumerator SleepRoutine(bool exhausted)
        {
            while (celebrating) yield return null;
            sleepingUI = true;
            CloseWindow(); ClosePopup();
            var root = MakeOverlay("Night", 0f, null);
            var bg = root.GetComponent<Image>();
            Sfx.Play("sleep");
            var moon = UIKit.Icon(root.transform, "moon", 220);
            moon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(220, 220));
            var txt = UIKit.Txt(root.transform, exhausted ? Loc.T("ui.exhausted") : Loc.T("ui.sleeping"), 52, Color.white, TextAnchor.MiddleCenter, true, true);
            txt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(950, 200));
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
            {
                bg.color = new Color(0.04f, 0.04f, 0.12f, t * 0.96f);
                moon.color = new Color(1, 1, 1, t); txt.color = new Color(1, 1, 1, t);
                yield return null;
            }
            bg.color = new Color(0.04f, 0.04f, 0.12f, 0.96f);
            yield return new WaitForSecondsRealtime(0.9f);

            var sum = g.CompleteNight(exhausted);
            Destroy(moon.gameObject); Destroy(txt.gameObject);

            var p = UIKit.Panel(root.transform, "Morning");
            var rt = p.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(900, 0);
            var col = p.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(40, 40, 40, 44); col.spacing = 16; col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.Icon(p.transform, "sun", 160);
            UIKit.Txt(p.transform, Loc.T("ui.good_morning", sum.newDay), 58, UIKit.TextDark, TextAnchor.MiddleCenter, true).LE(-1, 90);
            if (sum.payout > 0) PopupText(p.transform, Loc.T("ui.night_payout", Fmt.Num(sum.payout)), 50, Balance.Hex("#C98A10"));
            if (sum.grown > 0) PopupText(p.transform, Loc.T("ui.night_grown", sum.grown));
            if (sum.weeds > 0) PopupText(p.transform, Loc.T("ui.night_weeds", sum.weeds));
            PopupText(p.transform, Loc.T("res.energy") + ": " + Fmt.Num(g.MaxEnergy), 36, UIKit.TextMid);
            bool go = false;
            var b = UIKit.Btn(p.transform, Loc.T("ui.continue"), UIKit.BtnGreen, () => go = true, null, 46);
            b.LE(-1, 130);
            StartCoroutine(PopIn(p.transform, 0.5f));
            Sfx.Play("morning");
            if (sum.payout > 0) { Confetti(40, Vector2.zero, 0.8f); Sfx.Play("coin"); }

            while (!go) yield return null;
            Destroy(p.gameObject);
            for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 0.6f)
            {
                bg.color = new Color(0.04f, 0.04f, 0.12f, t * 0.96f);
                yield return null;
            }
            CloseOverlay(root);
            sleepingUI = false;
            g.EndSleep();
            RefreshHud();
        }

        // ------------------------------------------------------------------ Platzhalter-Werbung

        void ShowFakeAd(string placement, Action onReward)
        {
            StartCoroutine(FakeAdRoutine(onReward));
        }

        IEnumerator FakeAdRoutine(Action onReward)
        {
            var root = MakeOverlay("Ad", 0.94f, null);
            root.transform.SetParent(Top, false);
            var ic = UIKit.Icon(root.transform, "video", 200);
            ic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(200, 200));
            ic.gameObject.AddComponent<Pulse>();
            var t = UIKit.Txt(root.transform, Loc.T("ui.ad_watching"), 50, Color.white, TextAnchor.MiddleCenter, true);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 100));
            var t2 = UIKit.Txt(root.transform, Loc.T("ui.ad_placeholder"), 34, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            t2.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(900, 80));
            var bar = UIKit.Bar(root.transform, "Bar", new Color(1, 1, 1, 0.2f), UIKit.Green, out var fill);
            bar.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(700, 50));
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 2f) { UIKit.SetBar(fill, k); yield return null; }
            CloseOverlay(root);
            onReward?.Invoke();
            Sfx.Play("unlock");
            Confetti(30, Vector2.zero, 0.7f);
            RefreshHud();
            RefreshWindow();
        }
    }
}
