using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Aufgabenliste gross an der linken Seite: das aktuelle Ziel ("Ziel 3 von 15"), was es freischaltet, und genau
    /// seine vier Aufgaben untereinander (Bild, Aufgabe, Stand, Balken, Haken wenn geschafft). Sind alle vier geschafft,
    /// wird das Ziel erreicht (Feier, Banner) und die naechsten vier erscheinen. Der Tagesauftrag steht darunter.
    /// </summary>
    public partial class WorldUI
    {
        class TaskRow { public RectTransform rt; public Image icon, barFill, check; public Text title, sub, count; }

        struct TaskData { public Sprite icon; public string title, sub, count; public float f; public bool done; public Color col; }

        RectTransform taskPanel;
        Text taskHeader, taskReward;
        readonly List<TaskRow> taskRows = new List<TaskRow>();
        float taskTimer;
        const float TaskRowH = 112f, TaskTop = 140f;
        const int TaskMax = 4;   // immer genau vier Aufgaben je Ziel

        void BuildTasks()
        {
            var p = Panel(safe, "Tasks", new Color(0.12f, 0.1f, 0.08f, 0.62f));
            p.raycastTarget = false;
            taskPanel = p.rectTransform;
            taskPanel.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -290), new Vector2(620, 200));
            taskHeader = UIKit.Txt(p.transform, "Aufgaben", 34, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true, true);
            taskHeader.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -8), new Vector2(-40, 50));
            taskReward = UIKit.Txt(p.transform, "", 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft, false, true);
            taskReward.supportRichText = true;
            taskReward.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -58), new Vector2(-44, 76));
            wb.MilestoneReached += m =>
            {
                var names = new List<string>();
                if (m.unlocks != null) foreach (var id in m.unlocks) names.Add(WorldBuilder.UnlockName(id));
                Banner("Ziel erreicht: " + m.name + "!", "Neu: " + string.Join(", ", names) + " – " + m.desc);
                taskTimer = 0f;
            };
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

        /// <summary>Die vier Aufgaben des aktuellen Ziels.</summary>
        List<TaskData> CollectTasks()
        {
            var l = new List<TaskData>();
            var m = wb.CurrentMilestone;
            if (m == null || m.tasks == null) return l;
            // Bild: die erste Pflanze, die das Ziel freischaltet (bei Pflanzen-Aufgaben die Pflanze selbst)
            Sprite reward = null;
            if (m.unlocks != null) foreach (var id in m.unlocks) { var c = PlantBalance.I.Plant(id); if (c != null) { reward = SpeciesSprite(c.Genome); break; } }
            int i = 0;
            foreach (var n in wb.NeedsProgress(m.tasks))
            {
                var pd = i < m.tasks.Length && m.tasks[i].StartsWith("plant:") ? PlantBalance.I.Plant(m.tasks[i].Split(':')[1]) : null;
                bool done = n.v >= n.n;
                l.Add(new TaskData
                {
                    icon = pd != null ? SpeciesSprite(pd.Genome) : reward,
                    title = n.task,
                    sub = done ? "geschafft!" : "Aufgabe " + (i + 1) + " von 4",
                    count = Num.Short(Mathf.Min(n.v, n.n)) + " / " + Num.Short(n.n),
                    f = n.v / (float)Mathf.Max(1, n.n),
                    done = done,
                    col = TaskCols[i % TaskCols.Length]
                });
                i++;
            }
            return l;
        }

        static readonly Color[] TaskCols = { new Color(0.95f, 0.78f, 0.36f), new Color(0.65f, 0.85f, 0.45f), new Color(0.6f, 0.8f, 1f), new Color(0.85f, 0.7f, 1f) };

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
            taskPanel.sizeDelta = new Vector2(taskPanel.sizeDelta.x, TaskTop + Mathf.Max(n, 1) * TaskRowH + 10f);
            var m = wb.CurrentMilestone;
            if (m == null) { taskHeader.text = "Alle Ziele erreicht!"; taskReward.text = "Dein Hof ist eine Großfarm – spiel weiter, so lange du magst."; }
            else
            {
                var names = new List<string>();
                if (m.unlocks != null) foreach (var id in m.unlocks) names.Add(WorldBuilder.UnlockName(id));
                taskHeader.text = "Ziel " + (wb.MilestoneIndex + 1) + " von " + wb.MilestoneTotal + ": " + m.name;
                taskReward.text = "<b>Schaltet frei: " + string.Join(", ", names) + "</b>\n" + m.desc;
            }
        }
    }
}
