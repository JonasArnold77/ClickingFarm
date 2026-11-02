using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Aufgabenliste gross an der linken Seite, untereinander: oben der Auftrag fuer heute, darunter jede offene
    /// Bedingung fuer die naechste Pflanze als eigene Aufgabe ("Baue 6 Mauerstücke – 3 / 6"), danach die naechste
    /// Verbesserung bzw. das naechste Bauwerk, das noch an etwas auf dem Hof haengt. Jede Zeile: Bild, Titel, wofuer
    /// sie ist, Fortschrittsbalken; erledigte bekommen einen Haken. Ersetzt die alten Ziel- und Auftragsfelder.
    /// </summary>
    public partial class WorldUI
    {
        class TaskRow { public RectTransform rt; public Image icon, barFill, check; public Text title, sub, count; }

        struct TaskData { public Sprite icon; public string title, sub, count; public float f; public bool done; public Color col; }

        RectTransform taskPanel;
        Text taskHeader;
        readonly List<TaskRow> taskRows = new List<TaskRow>();
        float taskTimer;
        const float TaskRowH = 112f, TaskTop = 64f;
        const int TaskMax = 7;

        void BuildTasks()
        {
            var p = Panel(safe, "Tasks", new Color(0.12f, 0.1f, 0.08f, 0.62f));
            p.raycastTarget = false;
            taskPanel = p.rectTransform;
            taskPanel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -290), new Vector2(620, 200));
            taskHeader = UIKit.Txt(p.transform, "Aufgaben", 34, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true, true);
            taskHeader.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -8), new Vector2(-40, 50));
            for (int i = 0; i < TaskMax; i++) taskRows.Add(MakeTaskRow(p.transform, i));
            // die alten Felder (naechstes Ziel, Tagesauftrag) zeigt jetzt diese Liste
            if (goalText != null) goalText.transform.parent.gameObject.SetActive(false);
        }

        TaskRow MakeTaskRow(Transform parent, int i)
        {
            var r = new TaskRow();
            var bg = UIKit.Rect(parent, "Task").gameObject.AddComponent<Image>();
            bg.sprite = Rounded; bg.color = new Color(1f, 1f, 1f, 0.07f); bg.raycastTarget = false;
            r.rt = bg.rectTransform;
            r.rt.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -TaskTop - i * TaskRowH), new Vector2(-24, TaskRowH - 8));
            r.icon = UIKit.Rect(bg.transform, "Icon").gameObject.AddComponent<Image>();
            r.icon.preserveAspect = true; r.icon.raycastTarget = false;
            r.icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(84, 84));
            r.title = UIKit.Txt(bg.transform, "", 28, Color.white, TextAnchor.MiddleLeft, true, true);
            r.title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(102, -4), new Vector2(-250, 40));
            r.count = UIKit.Txt(bg.transform, "", 30, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleRight, true, true);
            r.count.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -4), new Vector2(150, 40));
            r.sub = UIKit.Txt(bg.transform, "", 21, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleLeft, false, true);
            r.sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(102, -42), new Vector2(-116, 30));
            var barBg = UIKit.Rect(bg.transform, "BarBg").gameObject.AddComponent<Image>();
            barBg.sprite = Rounded; barBg.color = new Color(1f, 1f, 1f, 0.14f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(102, 10), new Vector2(-116, 20));
            r.barFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            r.barFill.sprite = Rounded; r.barFill.raycastTarget = false;
            r.barFill.rectTransform.anchorMin = Vector2.zero; r.barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            r.barFill.rectTransform.offsetMin = r.barFill.rectTransform.offsetMax = Vector2.zero;
            r.check = UIKit.Rect(bg.transform, "Check").gameObject.AddComponent<Image>();
            r.check.sprite = Rounded; r.check.color = new Color(0.45f, 0.8f, 0.4f, 0.9f); r.check.raycastTarget = false;
            r.check.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(84, -30), new Vector2(30, 30));
            var tick = UIKit.Txt(r.check.transform, "✓", 24, Color.white, TextAnchor.MiddleCenter, true, true);
            tick.rectTransform.Stretch();
            return r;
        }

        /// <summary>Welche Aufgaben gerade anstehen (in dieser Reihenfolge).</summary>
        List<TaskData> CollectTasks()
        {
            var l = new List<TaskData>();
            // 1) Auftrag fuer heute
            var ch = PlantBalance.I.Plant(wb.ChallengeType);
            if (ch != null)
            {
                var tiers = wb.ChallengeTiers;
                int tier = wb.ChallengeTier, count = wb.ChallengeCount;
                string[] medal = { "Bronze", "Silber", "Gold" };
                bool all = tier >= tiers.Length;
                int target = all ? tiers[tiers.Length - 1] : tiers[tier];
                int prev = tier > 0 && !all ? tiers[tier - 1] : 0;
                l.Add(new TaskData
                {
                    icon = SpeciesSprite(ch.Genome),
                    title = "Heute: liefere " + WorldBuilder.ProductName(ch),
                    sub = all ? "Gold geschafft – morgen gibt es einen neuen Auftrag"
                              : "Für " + medal[tier] + ": +" + Num.Short(wb.ChallengePreview(tier)) + " Münzen" + (tier == 1 ? ", 2 Runen" : tier == 2 ? " und ein Schatz" : ""),
                    count = all ? count.ToString() : count + " / " + target,
                    f = all ? 1f : (count - prev) / (float)Mathf.Max(1, target - prev),
                    done = all,
                    col = tier == 0 ? BronzeUi : tier == 1 ? SilverUi : GoldUi
                });
            }
            // 2) naechste Pflanze: jede Bedingung als eigene Aufgabe
            var next = wb.NextUnlock();
            if (next != null)
            {
                var icon = SpeciesSprite(next.Genome);
                string forWhat = "für " + next.name + ": " + Helps(next);
                if (next.IsCombat)
                    l.Add(new TaskData { icon = icon, title = "Überstehe Nacht " + next.unlockWave, sub = forWhat, count = wb.Save.wave + " / " + next.unlockWave, f = wb.Save.wave / (float)Mathf.Max(1, next.unlockWave), col = AccentWarm });
                else
                {
                    bool coinsOk = wb.Earned >= next.unlockEarned;
                    l.Add(new TaskData { icon = icon, title = "Verdiene " + Num.Short(next.unlockEarned) + " Münzen", sub = forWhat, count = Num.Short(System.Math.Min(wb.Earned, next.unlockEarned)) + " / " + Num.Short(next.unlockEarned), f = (float)(wb.Earned / System.Math.Max(1, next.unlockEarned)), done = coinsOk, col = GoldUi });
                    if (next.unlockFields > 0)
                        l.Add(new TaskData { icon = icon, title = "Baue " + next.unlockFields + " Felder", sub = forWhat, count = Mathf.Min(wb.FieldCount, next.unlockFields) + " / " + next.unlockFields, f = wb.FieldCount / (float)next.unlockFields, done = wb.FieldCount >= next.unlockFields, col = new Color(0.65f, 0.85f, 0.45f) });
                    foreach (var n in wb.NeedsProgress(next.needs))
                        l.Add(new TaskData { icon = icon, title = n.task, sub = forWhat, count = Mathf.Min(n.v, n.n) + " / " + n.n, f = n.v / (float)Mathf.Max(1, n.n), done = n.v >= n.n, col = new Color(0.6f, 0.8f, 1f) });
                }
            }
            // 3) Verbesserung oder Bauwerk, das nur noch an Hof-Bedingungen haengt
            foreach (var u in PlantBalance.I.upgrades)
            {
                if (wb.HasUpgrade(u.id) || wb.Earned < u.unlockEarned || wb.NeedsMet(u.needs)) continue;
                foreach (var n in wb.NeedsProgress(u.needs))
                    if (n.v < n.n) l.Add(new TaskData { icon = null, title = n.task, sub = "für die Verbesserung " + u.name + ": " + u.desc, count = n.v + " / " + n.n, f = n.v / (float)Mathf.Max(1, n.n), col = new Color(0.85f, 0.7f, 1f) });
                break;
            }
            foreach (var s in PlantBalance.I.structures)
            {
                if (wb.Earned < s.unlockEarned || wb.NeedsMet(s.needs)) continue;
                foreach (var n in wb.NeedsProgress(s.needs))
                    if (n.v < n.n) l.Add(new TaskData { icon = null, title = n.task, sub = "für das Bauwerk " + s.name + ": " + s.desc, count = n.v + " / " + n.n, f = n.v / (float)Mathf.Max(1, n.n), col = new Color(0.85f, 0.7f, 1f) });
                break;
            }
            return l;
        }

        void UpdateTasks(float dt)
        {
            if (taskPanel == null || wb == null) return;
            taskTimer -= dt;
            if (taskTimer > 0f) return;
            taskTimer = 0.4f;
            var tasks = CollectTasks();
            int n = Mathf.Min(tasks.Count, TaskMax);
            for (int i = 0; i < taskRows.Count; i++)
            {
                var r = taskRows[i];
                r.rt.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var t = tasks[i];
                r.icon.enabled = t.icon != null; r.icon.sprite = t.icon;
                r.title.text = t.title;
                r.title.color = t.done ? new Color(0.65f, 0.9f, 0.55f) : Color.white;
                r.sub.text = t.sub;
                r.count.text = t.count;
                r.barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(t.f), 1f);
                r.barFill.color = t.done ? new Color(0.45f, 0.8f, 0.4f) : t.col;
                r.check.gameObject.SetActive(t.done);
            }
            taskPanel.sizeDelta = new Vector2(taskPanel.sizeDelta.x, TaskTop + n * TaskRowH + 10f);
            taskHeader.text = n > 0 ? "Aufgaben" : "Aufgaben – alles erledigt!";
        }
    }
}
