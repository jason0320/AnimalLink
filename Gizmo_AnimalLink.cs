using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace panda.AnimalLink
{
    // =========================================================================
    //  Gizmo 1 – Bandwidth display
    //  Shows used/max bandwidth as a row of coloured blocks, like the Mechlink
    //  bandwidth gizmo.  Tooltip explains what bandwidth is and the cost of each
    //  linked animal.
    // =========================================================================
    public class Gizmo_AnimalLinkBandwidth : Gizmo
    {
        public Hediff_AnimalLinkImplant link;
        public Pawn pawn;

        public override float GetWidth(float maxWidth) => 136f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth,
                                                GizmoRenderParms parms)
        {
            Rect outer = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(outer);

            Rect inner = outer.ContractedBy(6f);

            // ── label ────────────────────────────────────────────────────────
            Text.Font = GameFont.Small;
            Widgets.Label(
                new Rect(inner.x, inner.y, inner.width, 20f),
                $"Bandwidth  {link.UsedBandwidth} / {link.MaxBandwidth}");

            // ── block bar ────────────────────────────────────────────────────
            float blockW   = 10f;
            float blockH   = 10f;
            float spacing  = 2f;
            float bx       = inner.x;
            float by       = inner.y + 24f;

            for (int i = 0; i < link.MaxBandwidth; i++)
            {
                Color c = i < link.UsedBandwidth
                    ? new Color(1f, 0.85f, 0.2f)   // yellow = used
                    : new Color(0.3f, 0.3f, 0.3f);  // grey  = free
                Widgets.DrawBoxSolid(new Rect(bx, by, blockW, blockH), c);
                bx += blockW + spacing;

                // wrap to next row after 8 blocks
                if ((i + 1) % 8 == 0)
                {
                    bx  = inner.x;
                    by += blockH + spacing;
                }
            }

            // ── tooltip ──────────────────────────────────────────────────────
            if (Mouse.IsOver(outer))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("AnimalLink Bandwidth");
                sb.AppendLine($"Used: {link.UsedBandwidth} / {link.MaxBandwidth}");
                sb.AppendLine($"Remaining: {link.RemainingBandwidth}");
                sb.AppendLine();
                sb.AppendLine("Bandwidth cost by body size:");
                sb.AppendLine("  ≤ 1.5 body size  →  1 BW");
                sb.AppendLine("  ≤ 2.5 body size  →  2 BW");
                sb.AppendLine("  ≤ 4.0 body size  →  3 BW");
                sb.AppendLine("  > 4.0 body size  →  4 BW");

                if (link.linkedAnimals.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Linked animals:");
                    foreach (var e in link.Entries())
                    {
                        if (e?.pawn == null) continue;
                        sb.AppendLine(
                            $"  {e.pawn.LabelShortCap}  " +
                            $"({Hediff_AnimalLinkImplant.BandwidthCostFor(e.pawn)} BW)");
                    }
                }

                TooltipHandler.TipRegion(outer, sb.ToString().TrimEnd());
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }

    // =========================================================================
    //  Gizmo 2 – Group 1 control
    //  Click to select all animals in group 1.
    //  Tooltip shows count, work mode, and draft state.
    // =========================================================================
    public class Gizmo_AnimalLinkGroup : Gizmo
    {
        public Hediff_AnimalLinkImplant link;
        public Pawn pawn;
        public int groupIndex;

        public override float GetWidth(float maxWidth) => 136f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth,
                                                GizmoRenderParms parms)
        {
            Rect outer = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(outer);

            var animals = link.linkedAnimals
                .Where(e => e?.pawn != null && e.groupIndex == groupIndex)
                .ToList();

            bool hasAnimals = animals.Count > 0;

            // ── coloured background box ───────────────────────────────────────
            Rect box = outer.ContractedBy(4f);
            Widgets.DrawBoxSolid(box,
                hasAnimals
                    ? new Color(0.15f, 0.35f, 0.65f, 0.55f)
                    : new Color(0.22f, 0.22f, 0.22f, 0.55f));

            // ── title ─────────────────────────────────────────────────────────
            Text.Font = GameFont.Small;
            Widgets.Label(
                new Rect(box.x + 4f, box.y + 2f, box.width - 8f, 20f),
                $"Group {groupIndex + 1}");

            if (!hasAnimals)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                Widgets.Label(
                    new Rect(box.x + 4f, box.y + 24f, box.width - 8f, 20f),
                    "(empty)");
                GUI.color = Color.white;
            }
            else
            {
                // ── animal portrait grid ──────────────────────────────────────
                float iconSz = 18f;
                float ix     = box.x + 4f;
                float iy     = box.y + 24f;

                foreach (var entry in animals)
                {
                    RenderTexture tex =
                        PortraitsCache.Get(entry.pawn, new Vector2(32f, 32f), Rot4.South);
                    GUI.DrawTexture(new Rect(ix, iy, iconSz, iconSz), tex);
                    ix += iconSz + 2f;
                    if (ix + iconSz > box.xMax - 2f)
                    {
                        ix  = box.x + 4f;
                        iy += iconSz + 2f;
                    }
                }
            }

            // ── click: select this group ──────────────────────────────────────
            if (Widgets.ButtonInvisible(outer))
                link.SelectGroup(groupIndex);

            // ── tooltip ───────────────────────────────────────────────────────
            if (Mouse.IsOver(outer))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Group {groupIndex + 1}");
                sb.AppendLine($"Animals: {animals.Count}");
                sb.AppendLine("Click to select this group.");

                if (hasAnimals)
                {
                    sb.AppendLine();
                    foreach (var e in animals)
                    {
                        if (e?.pawn == null) continue;
                        float hp = e.pawn.health.summaryHealth.SummaryHealthPercent;
                        sb.AppendLine(
                            $"  {e.pawn.LabelShortCap}  " +
                            $"HP:{Mathf.RoundToInt(hp * 100f)}%  " +
                            $"Mode:{e.workMode}  " +
                            $"Draft:{(e.drafted ? "On" : "Off")}");
                    }
                }

                TooltipHandler.TipRegion(outer, sb.ToString().TrimEnd());
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }

    // =========================================================================
    //  Legacy combined gizmo – kept for any external references.
    //  Now simply renders the three separate gizmos side-by-side in one wider
    //  box so existing code that creates Gizmo_AnimalLink still works.
    //  The Patch_Pawn_GetGizmos yields the three individual gizmos instead,
    //  but keeping this class avoids compilation errors.
    // =========================================================================
    public class Gizmo_AnimalLink : Gizmo
    {
        public Hediff_AnimalLinkImplant link;
        public Pawn pawn;

        // Not used directly anymore – patch yields the three sub-gizmos.
        public override float GetWidth(float maxWidth) => 1f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth,
                                                GizmoRenderParms parms)
            => new GizmoResult(GizmoState.Clear);
    }
}
