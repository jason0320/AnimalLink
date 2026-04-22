using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace panda.AnimalLink
{
    /// <summary>
    /// Bottom-bar tab window modelled exactly on the vanilla Mechs tab.
    /// Registered via MainButtonDef XML (workerClass = this type).
    /// RimWorld instantiates bottom-tab windows through MainButtonWorker_ToggleTab,
    /// which expects the window to extend MainTabWindow directly.
    /// </summary>
    public class MainTabWindow_AnimalLink : MainTabWindow
    {
        // ── layout ──────────────────────────────────────────────────────────
        private const float HeaderH      = 32f;
        private const float SubHeaderH   = 22f;
        private const float OverseerH    = 44f;
        private const float RowH         = 34f;
        private const float ScrollBarW   = 16f;

        private const float ColName      = 160f;
        private const float ColHealth    = 90f;
        private const float ColDraft     = 52f;
        private const float ColRepair    = 82f;
        private const float ColOverseer  = 180f;
        private const float ColGroup     = 92f;
        private const float ColWork      = 98f;
        private const float ColAreaUnres = 90f;   // "Unrestricted" pill
        private const float ColAreaPill  = 80f;   // each named-area pill
        private const float ColStop      = 58f;
        private const float ColUnlink    = 58f;

        private Vector2 _scroll;

        // ── window size ──────────────────────────────────────────────────────
        public override Vector2 RequestedTabSize =>
            new Vector2(UI.screenWidth, 480f);

        protected override float Margin => 6f;

        // ── helpers ──────────────────────────────────────────────────────────
        private static IEnumerable<Pawn> Animanitors()
        {
            if (Find.CurrentMap == null) yield break;
            foreach (var p in Find.CurrentMap.mapPawns.FreeColonistsSpawned)
                if (AnimalLinkUtility.GetLink(p) != null)
                    yield return p;
        }

        // ── main draw ────────────────────────────────────────────────────────
        public override void DoWindowContents(Rect inRect)
        {
            var list = Animanitors().ToList();

            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, 300f, HeaderH), "AnimalLink");
            Text.Font = GameFont.Small;

            // "Manage areas…" top-right  (vanilla Mechs tab has this button)
            if (Find.CurrentMap != null)
            {
                Rect manageBtn = new Rect(inRect.xMax - 162f, inRect.y + 2f, 156f, 26f);
                if (Widgets.ButtonText(manageBtn, "Manage areas..."))
                    Find.WindowStack.Add(new Dialog_ManageAreas(Find.CurrentMap));
            }

            if (list.Count == 0)
            {
                Widgets.Label(
                    new Rect(inRect.x, inRect.y + HeaderH + 8f, 600f, 30f),
                    "No Animanitors with an AnimalLink implant are present on this map.");
                return;
            }

            // Column header row
            float hY = inRect.y + HeaderH + 2f;
            DrawColumnHeaders(inRect.x, hY, inRect.width);

            // Scrollable roster
            float listY    = hY + SubHeaderH + 2f;
            Rect  listRect = new Rect(inRect.x, listY, inRect.width, inRect.yMax - listY);

            // Measure content height
            float contentH = list.Sum(a =>
                OverseerH + 2f +
                AnimalLinkUtility.GetLink(a).linkedAnimals.Count * RowH +
                6f);
            contentH = Mathf.Max(contentH, 1f);

            Rect viewRect = new Rect(0f, 0f, listRect.width - ScrollBarW, contentH);
            Widgets.BeginScrollView(listRect, ref _scroll, viewRect);

            float curY = 0f;
            foreach (var animanitor in list)
                curY = DrawOverseerSection(animanitor,
                           AnimalLinkUtility.GetLink(animanitor),
                           viewRect.width, curY);

            Widgets.EndScrollView();
        }

        // ── Column headers ────────────────────────────────────────────────────
        private static void DrawColumnHeaders(float x, float y, float totalW)
        {
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            Widgets.DrawLineHorizontal(x, y + SubHeaderH - 1f, totalW);
            GUI.color = prev;

            float cx = x + 8f;
            Label(ref cx, y, ColName,     "Name");
            Label(ref cx, y, ColHealth,   "Health");
            Label(ref cx, y, ColDraft,    "Draft");
            Label(ref cx, y, ColRepair,   "Auto treat");
            Label(ref cx, y, ColOverseer, "Overseer");
            Label(ref cx, y, ColGroup,    "Control group");
            Label(ref cx, y, ColWork,     "Work mode");
            Label(ref cx, y, 320f,        "Allowed area");
        }

        private static void Label(ref float cx, float y, float w, string text)
        {
            Widgets.Label(new Rect(cx, y, w, SubHeaderH), text);
            cx += w;
        }

        // ── Overseer section ──────────────────────────────────────────────────
        private static float DrawOverseerSection(Pawn animanitor,
                                                  Hediff_AnimalLinkImplant link,
                                                  float w, float y)
        {
            Rect oRow = new Rect(0f, y, w, OverseerH);
            Widgets.DrawMenuSection(oRow);

            // Portrait
            GUI.DrawTexture(
                new Rect(4f, y + 4f, 36f, 36f),
                PortraitsCache.Get(animanitor, new Vector2(36f, 36f), Rot4.South));

            // Name + bandwidth
            Widgets.Label(new Rect(46f, y + 4f,  260f, 20f), animanitor.LabelShortCap);
            Widgets.Label(new Rect(46f, y + 24f, 130f, 16f),
                $"Bandwidth  {link.UsedBandwidth} / {link.MaxBandwidth}");

            float bwPct = link.MaxBandwidth > 0
                ? (float)link.UsedBandwidth / link.MaxBandwidth : 0f;
            Widgets.FillableBar(new Rect(182f, y + 27f, 130f, 11f), bwPct);

            // Click header → select pawn
            if (Widgets.ButtonInvisible(new Rect(0f, y, 360f, OverseerH)))
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(animanitor);
            }

            y += OverseerH + 2f;

            // Animal rows
            List<LinkedAnimalData> toDelete = null;
            foreach (var entry in link.Entries().ToList())
            {
                if (entry?.pawn == null) continue;
                y = DrawAnimalRow(entry, link, animanitor, w, y, ref toDelete);
            }
            if (toDelete != null)
                foreach (var e in toDelete) link.linkedAnimals.Remove(e);

            return y + 6f;
        }

        // ── Single animal row ─────────────────────────────────────────────────
        private static float DrawAnimalRow(LinkedAnimalData entry,
                                            Hediff_AnimalLinkImplant link,
                                            Pawn animanitor, float w, float y,
                                            ref List<LinkedAnimalData> toDelete)
        {
            Rect row = new Rect(0f, y, w, RowH);
            Widgets.DrawHighlightIfMouseover(row);
            if (entry.selected) Widgets.DrawHighlightSelected(row);

            // Click row body (left of buttons) to toggle selection
            if (Widgets.ButtonInvisible(new Rect(0f, y, ColName, RowH)))
                entry.selected = !entry.selected;

            float cx = 8f;

            // Name
            Widgets.Label(new Rect(cx, y + 6f, ColName - 10f, 22f), entry.pawn.LabelShortCap);
            cx += ColName;

            // Health bar
            float hp    = entry.pawn.health.summaryHealth.SummaryHealthPercent;
            Rect  hpBar = new Rect(cx, y + 8f, ColHealth - 8f, 18f);
            Widgets.FillableBar(hpBar, hp);
            Widgets.Label(hpBar, $"{Mathf.RoundToInt(hp * 100f)}%");
            cx += ColHealth;

            // Draft checkbox
            bool drafted = entry.drafted;
            Widgets.Checkbox(cx + 14f, y + 8f, ref drafted, 18f);
            entry.drafted = drafted;
            cx += ColDraft;

            // Auto-treat checkbox
            bool autoTreat = entry.autoTreat;
            Widgets.Checkbox(cx + 20f, y + 8f, ref autoTreat, 18f);
            entry.autoTreat = autoTreat;
            cx += ColRepair;

            // Overseer
            Widgets.Label(new Rect(cx, y + 6f, ColOverseer - 4f, 22f), animanitor.LabelShortCap);
            cx += ColOverseer;

            // Control group (cycle)
            if (Widgets.ButtonText(new Rect(cx + 4f, y + 4f, ColGroup - 8f, 24f),
                    $"Group {entry.groupIndex + 1}"))
                entry.groupIndex = (entry.groupIndex + 1) % link.MaxControlGroups;
            cx += ColGroup;

            // Work mode (cycle)
            if (Widgets.ButtonText(new Rect(cx + 2f, y + 4f, ColWork - 4f, 24f),
                    entry.workMode.ToString()))
                entry.workMode = (AnimalLinkWorkMode)(((int)entry.workMode + 1) % 3);
            cx += ColWork;

            // Allowed area inline pills
            cx = DrawAreaButtons(cx, y, entry, link);

            // Stop
            if (Widgets.ButtonText(new Rect(cx + 2f, y + 4f, ColStop, 24f), "Stop"))
                entry.pawn.jobs?.StopAll();
            cx += ColStop + 6f;

            // Unlink
            if (Widgets.ButtonText(new Rect(cx + 2f, y + 4f, ColUnlink, 24f), "Unlink"))
            {
                if (toDelete == null) toDelete = new List<LinkedAnimalData>();
                toDelete.Add(entry);
            }

            return y + RowH;
        }

        // ── Inline area pills ─────────────────────────────────────────────────
        private static float DrawAreaButtons(float cx, float y,
                                              LinkedAnimalData entry,
                                              Hediff_AnimalLinkImplant link)
        {
            var map = link.pawn?.Map;
            if (map == null) return cx + ColAreaUnres;

            // "Unrestricted"
            Rect unBtn = new Rect(cx + 2f, y + 4f, ColAreaUnres - 4f, 24f);
            if (entry.allowedArea == null) Widgets.DrawHighlightSelected(unBtn);
            if (Widgets.ButtonText(unBtn, "Unrestricted"))
                SetArea(entry, null);
            cx += ColAreaUnres;

            // Named areas
            foreach (var area in map.areaManager.AllAreas)
            {
                Rect ar = new Rect(cx + 2f, y + 4f, ColAreaPill - 4f, 24f);
                Widgets.DrawBoxSolid(ar, entry.allowedArea == area
                    ? area.Color * new Color(1f, 1f, 1f, 0.85f)
                    : new Color(0.25f, 0.25f, 0.25f, 0.85f));
                if (Widgets.ButtonInvisible(ar))
                    SetArea(entry, area);
                Widgets.Label(new Rect(ar.x + 3f, ar.y + 3f, ar.width - 6f, ar.height - 6f),
                              area.Label);
                cx += ColAreaPill;
            }

            return cx;
        }

        private static void SetArea(LinkedAnimalData entry, Area area)
        {
            entry.allowedArea = area;
            if (entry.pawn?.playerSettings != null)
                entry.pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
        }
    }
}
