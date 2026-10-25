using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    public partial class GameUI
    {
        // ------------------------------------------------------------------ Start-Samen waehlen

        void ShowStarterChoice()
        {
            StartCoroutine(StarterRoutine());
        }

        IEnumerator StarterRoutine()
        {
            yield return null;
            CloseWindow(); ClosePopup();
            celebrating = true;
            var root = MakeOverlay("Starter", 0.82f, null);
            var title = UIKit.Txt(root.transform, Loc.T("ui.choose_starter"), 70, Balance.Hex("#FFE45C"), TextAnchor.MiddleCenter, true, true);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(1040, 120));
            var sub = UIKit.Txt(root.transform, Loc.T("ui.choose_starter_sub"), 38, Color.white, TextAnchor.MiddleCenter, false, true);
            sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -340), new Vector2(1000, 70));

            var starters = Array.FindAll(g.B.crops, c => c.starter);
            string chosen = null;
            var cards = new Image[starters.Length];
            Button confirm = null;

            var row = UIKit.HRow(root.transform, "Cards", 18, TextAnchor.MiddleCenter);
            row.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1060, 900));
            for (int i = 0; i < starters.Length; i++)
            {
                var c = starters[i];
                var card = UIKit.Panel(row.transform, "Card", "paper");
                card.LE(330, 880);
                cards[i] = card;
                var b = card.gameObject.AddComponent<Button>();
                card.gameObject.AddComponent<Juicy>();
                int k = i;
                b.onClick.AddListener(() =>
                {
                    chosen = starters[k].id;
                    Sfx.Play("pop", 1.2f); Haptics.Tap(15);
                    for (int j = 0; j < cards.Length; j++) cards[j].color = j == k ? Color.white : new Color(0.62f, 0.58f, 0.55f);
                    confirm.interactable = true;
                    confirm.SetLabel(Loc.T("ui.take_starter") + " " + Loc.T("crop." + starters[k].id));
                });
                var col = UIKit.VCol(card.transform, "V", 8, TextAnchor.UpperCenter, new RectOffset(18, 18, 26, 20));
                col.GetComponent<RectTransform>().Stretch();
                col.childForceExpandWidth = true;
                var ic = UIKit.Icon(col.transform, "crop:" + c.id, 230);
                ic.gameObject.AddComponent<Pulse>().amount = 0.05f;
                UIKit.Txt(col.transform, Loc.T("crop." + c.id), 44, UIKit.TextDark, TextAnchor.MiddleCenter, true).LE(-1, 64);
                var el = UIKit.Txt(col.transform, Loc.T("ui.type", Loc.T("element." + c.defense.element)), 32, Color.white, TextAnchor.MiddleCenter, true, true);
                el.color = BattleView.ElementColor(c.defense.element); el.LE(-1, 46);
                UIKit.Txt(col.transform, Loc.T("cropdesc." + c.id), 28, UIKit.TextMid, TextAnchor.UpperCenter).LE(-1, 130);
                var d = c.defense;
                float dps = d.damage / d.cooldown * (d.pulse ? 1.6f : 1f) * (d.burn > 0 ? 1.3f : 1f);
                StatBar(col.transform, Loc.T("ui.stat_damage"), dps / 5.5f, Balance.Hex("#FF6F4A"));
                StatBar(col.transform, Loc.T("ui.stat_range"), d.range / 3.5f, Balance.Hex("#4FA8F0"));
                StatBar(col.transform, Loc.T("ui.stat_area"), d.pulse ? 0.8f : d.area > 0 ? 0.6f : 0.15f, Balance.Hex("#9B6BFF"));
                StatBar(col.transform, Loc.T("ui.stat_hp"), d.hp / 32f, Balance.Hex("#6CCB3F"));
                StartCoroutine(DelayedPop(card.transform, 0.15f + i * 0.15f));
            }

            confirm = UIKit.Btn(root.transform, Loc.T("ui.take_starter"), UIKit.BtnGreen, () =>
            {
                if (chosen == null) return;
                var id = chosen;
                g.ChooseStarter(id);
                CloseOverlay(root);
                celebrating = false;
                ShowBigReward(Loc.T("ui.starter_chosen", Loc.T("crop." + id)), Loc.T("cropdesc." + id), "crop:" + id);
                celebrating = true;
                RefreshHud();
            }, null, 46);
            confirm.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(760, 140));
            confirm.interactable = false;
            Sfx.Play("unlock");
        }

        void StatBar(Transform parent, string label, float v, Color col)
        {
            var row = UIKit.VCol(parent, "Stat", 2);
            row.LE(-1, 64);
            UIKit.Txt(row.transform, label, 24, UIKit.TextMid, TextAnchor.MiddleLeft, true).LE(-1, 28);
            var bar = UIKit.Bar(row.transform, "Bar", new Color(0.3f, 0.2f, 0.1f, 0.25f), col, out var fill);
            bar.LE(-1, 28);
            UIKit.SetBar(fill, Mathf.Clamp(v, 0.08f, 1f));
        }

        // ------------------------------------------------------------------ Nachtkampf

        void OnBattleStarted()
        {
            CloseWindow(); ClosePopup();
            ShowToast(Loc.T("ui.night_falls"), "moon");
            StartCoroutine(NightBanner());
        }

        IEnumerator NightBanner()
        {
            var t = UIKit.Txt(Top, Loc.T("ui.night_falls"), 84, Balance.Hex("#C9B8FF"), TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1060, 140));
            var s = UIKit.Txt(Top, Loc.T("ui.night_falls_sub"), 44, Color.white, TextAnchor.MiddleCenter, false, true);
            s.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1000, 80));
            for (float k = 0; k < 2.6f; k += Time.unscaledDeltaTime)
            {
                float a = k < 0.3f ? k / 0.3f : k > 2.1f ? 1 - (k - 2.1f) / 0.5f : 1;
                t.color = new Color(t.color.r, t.color.g, t.color.b, a); s.color = new Color(1, 1, 1, a);
                t.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0, 0.3f - k) * 1.5f);
                yield return null;
            }
            Destroy(t.gameObject); Destroy(s.gameObject);
        }

        void ShowBattleResult(BattleResult r)
        {
            var p = CelebrationPanel(out var root, r.victory ? Loc.T("ui.battle_won") : Loc.T("ui.battle_fled"), r.victory ? Balance.Hex("#FFE45C") : Balance.Hex("#C9B8FF"));
            var ic = UIKit.Icon(p, r.victory ? "crop:" + (string.IsNullOrEmpty(g.S.starter) ? "ember_pepper" : g.S.starter) : "moon", 200);
            ic.gameObject.AddComponent<Pulse>().amount = 0.06f;
            PopupText(p, Loc.T("ui.battle_kills", r.kills), 44, UIKit.TextDark);
            PopupText(p, Loc.T("ui.battle_coins", Fmt.Num(r.coins)), 44, Balance.Hex("#C98A10"));
            if (r.eaten > 0 || r.wilted > 0) PopupText(p, Loc.T("ui.battle_losses", r.eaten, r.wilted));
            if (r.stolen > 0) PopupText(p, Loc.T("ui.battle_stolen", Fmt.Num(r.stolen)), 36, UIKit.Red);
            PopupButton(p, Loc.T("ui.sleep_after_battle"), UIKit.BtnBlue, () => { FinishCelebration(root); g.BeginSleep(false); }, "moon");
            if (r.victory) { Confetti(70, new Vector2(0, -200)); Sfx.Play("levelup"); }
        }

        string BattleBanner() => Loc.T("ui.battle_banner", g.S.nightsSurvived + 1, g.Wave, g.TotalWaves, g.Monsters.Count);

        void OnDefenderTapped(int x, int y)
        {
            var t = g.Tile(x, y);
            var c = Balance.Crop(t.crop);
            if (c == null) return;
            string info = Loc.T("crop." + c.id) + ": " + Loc.T("ui.defender_info", Fmt.Num(g.DefenderDamage(c)), c.defense.range.ToString("0.#"), Fmt.Num(g.DefenderMaxHp(c) - t.dmg));
            ShowToast(info, "crop:" + c.id);
        }
    }
}
