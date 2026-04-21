using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static panda.AnimalLink.AnimalLinkUtility;

namespace panda.AnimalLink
{
    // ---------------------------------------------------------------------------
    //  Job drivers – install and uninstall the AnimalLink implant
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A pawn walks to the AnimalLink on the ground (or in a shelf) and
    /// spends time self-operating to install it.  Requires Medicine skill >= 4.
    /// The item is consumed and the hediff is added on completion.
    /// </summary>
    public class JobDriver_InstallAnimalLink : JobDriver
    {
        // TargetIndex.A = the AnimalLink thing on the ground
        private const int WorkTicks = 600; // ~10 seconds at normal speed

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => AnimalLinkUtility.GetLink(pawn) != null);

            // 1. Walk to item
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 2. Work toil – self-surgery with skill progress bar
            Toil work = new Toil();
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration     = WorkTicks;
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.activeSkill         = () => SkillDefOf.Medicine;
            work.tickAction          = () => pawn.skills?.Learn(SkillDefOf.Medicine, 0.05f);
            work.handlingFacing      = true;
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return work;

            // 3. Finish – add hediff, destroy item, post message
            Toil finish = new Toil();
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            finish.initAction = () =>
            {
                Thing item = job.targetA.Thing;
                if (item == null || item.Destroyed) return;

                HediffDef def = DefDatabase<HediffDef>.GetNamed("AnimalLinkImplant");
                pawn.health.AddHediff(def);
                item.Destroy();

                Messages.Message(
                    $"{pawn.LabelShortCap} installed an animallink implant.",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
            };
            yield return finish;
        }
    }

    /// <summary>
    /// The pawn spends time self-operating to remove their AnimalLink implant.
    /// All linked animals are unlinked first.  On completion the hediff is
    /// removed and a new AnimalLink is spawned at the pawn's feet.
    /// </summary>
    public class JobDriver_UninstallAnimalLink : JobDriver
    {
        private const int WorkTicks = 500;

        // Captured at start so the progress bar denominator stays constant
        private int totalTicks;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => AnimalLinkUtility.GetLink(pawn) == null);

            // 1. Stand-and-work toil
            Toil work = new Toil();
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration     = WorkTicks;
            work.WithProgressBar(TargetIndex.None, () =>
                1f - (float)pawn.jobs.curDriver.ticksLeftThisToil / WorkTicks);
            work.activeSkill    = () => SkillDefOf.Medicine;
            work.tickAction     = () => pawn.skills?.Learn(SkillDefOf.Medicine, 0.04f);
            work.handlingFacing = true;
            yield return work;

            // 2. Finish – remove hediff and return item to the world
            Toil finish = new Toil();
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            finish.initAction = () =>
            {
                var link = AnimalLinkUtility.GetLink(pawn);
                if (link == null) return;

                link.linkedAnimals?.Clear();
                pawn.health.RemoveHediff(link);

                ThingDef itemDef = DefDatabase<ThingDef>.GetNamedSilentFail("AnimalLink");
                if (itemDef != null)
                {
                    Thing dropped = ThingMaker.MakeThing(itemDef);
                    GenPlace.TryPlaceThing(dropped, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }

                Messages.Message(
                    $"{pawn.LabelShortCap} removed their animallink implant.",
                    pawn,
                    MessageTypeDefOf.NeutralEvent
                );
            };
            yield return finish;
        }
    }

    // ---------------------------------------------------------------------------
    //  Comp on AnimalLink – "Install animallink" gizmo when item is selected
    // ---------------------------------------------------------------------------

    public class CompProperties_AnimalLink : CompProperties
    {
        public CompProperties_AnimalLink() => compClass = typeof(Comp_AnimalLink);
    }

    public class Comp_AnimalLink : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned) yield break;

            yield return new Command_Action
            {
                defaultLabel = "Install animallink",
                defaultDesc  = "Order a colonist (Medicine 4+) to install this animallink implant.",
                icon         = ContentFinder<Texture2D>.Get("UI/Commands/Install", false)
                               ?? BaseContent.WhiteTex,
                action = () =>
                {
                    Find.Targeter.BeginTargeting(
                        new TargetingParameters
                        {
                            canTargetPawns      = true,
                            canTargetHumans     = true,
                            canTargetAnimals    = false,
                            onlyTargetColonists = true,
                            validator = t =>
                            {
                                var p = t.Thing as Pawn;
                                if (p == null || !p.IsColonistPlayerControlled) return false;
                                if (AnimalLinkUtility.GetLink(p) != null) return false;
                                return (p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0) >= 4;
                            }
                        },
                        target =>
                        {
                            var installer = target.Thing as Pawn;
                            if (installer == null) return;
                            Job j = JobMaker.MakeJob(
                                DefDatabase<JobDef>.GetNamed("InstallAnimalLink"),
                                parent);
                            installer.jobs.TryTakeOrderedJob(j, JobTag.Misc);
                        });
                }
            };
        }
    }

    public class LinkedAnimalData : IExposable
    {
        public Pawn pawn;
        public int groupIndex;
        public bool selected;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref groupIndex, "groupIndex", 0);
            Scribe_Values.Look(ref selected, "selected", false);
        }
    }



    public class Window_AnimalLink : Window
    {
        private readonly Hediff_AnimalLinkImplant link;
        private Vector2 scroll;
        private int activeGroup;

        public Window_AnimalLink(Hediff_AnimalLinkImplant link)
        {
            this.link = link;
            forcePause = false;
            absorbInputAroundWindow = true;
            draggable = true;
            closeOnClickedOutside = false;
            preventCameraMotion = false;
            doCloseButton = true;
            doCloseX = true;
            layer = WindowLayer.Dialog;
            resizeable = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(980f, 680f);

        public override void DoWindowContents(Rect inRect)
        {
            if (link == null || link.pawn == null)
            {
                Widgets.Label(inRect, "No mechanitor link found.");
                return;
            }

            link.Cleanup();

            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 60f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(header.x, header.y, header.width, 32f), "Animal Mechlink");
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(header.x, header.y + 34f, header.width, 24f), link.Tooltip());

            float y = header.yMax + 8f;

            // Control groups row
            Widgets.Label(new Rect(inRect.x, y, 180f, 24f), "Control groups:");
            float groupX = inRect.x + 170f;
            for (int i = 0; i < link.MaxControlGroups; i++)
            {
                Rect b = new Rect(groupX, y - 1f, 90f, 28f);
                bool selected = activeGroup == i;
                if (Widgets.ButtonText(b, selected ? $"[{i + 1}]" : $"Group {i + 1}"))
                    activeGroup = i;

                groupX += 96f;
            }

            y += 36f;

            // Global actions
            Rect left = new Rect(inRect.x, y, inRect.width, 34f);
            float bx = left.x;

            if (Widgets.ButtonText(new Rect(bx, left.y, 140f, 30f), "Select all"))
                link.SelectAll();
            bx += 146f;

            if (Widgets.ButtonText(new Rect(bx, left.y, 140f, 30f), "Select none"))
                link.SelectNone();
            bx += 146f;

            if (Widgets.ButtonText(new Rect(bx, left.y, 140f, 30f), "Assign group"))
                link.SetSelectedGroup(activeGroup);
            bx += 146f;

            if (Widgets.ButtonText(new Rect(bx, left.y, 140f, 30f), "Select group"))
                link.SelectGroup(activeGroup);
            bx += 146f;

            if (Widgets.ButtonText(new Rect(bx, left.y, 140f, 30f), "Recall selected"))
                link.RecallSelected();
            bx += 146f;

            if (Widgets.ButtonText(new Rect(bx, left.y, 140f, 30f), "Stop selected"))
                link.StopSelected();

            y += 44f;

            // Roster
            Rect rosterLabel = new Rect(inRect.x, y, inRect.width, 24f);
            Widgets.Label(rosterLabel, "Linked animals");
            y += 26f;

            Rect viewRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            float rowHeight = 30f;
            float contentHeight = Math.Max(1, link.linkedAnimals.Count * rowHeight + 10f);
            Rect contentRect = new Rect(0f, 0f, viewRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(viewRect, ref scroll, contentRect);
            float curY = 0f;

            List<LinkedAnimalData> toRemove = null;

            foreach (var entry in link.Entries())
            {
                if (entry?.pawn == null) continue;

                Rect row = new Rect(0f, curY, contentRect.width, rowHeight);
                Widgets.DrawHighlightIfMouseover(row);

                Rect check = new Rect(row.x, row.y + 2f, 300f, 26f);
                bool wasSelected = entry.selected;
                Widgets.CheckboxLabeled(
                    check,
                    $"{entry.pawn.LabelShortCap}  |  cost {Hediff_AnimalLinkImplant.BandwidthCostFor(entry.pawn)}  |  G{entry.groupIndex + 1}",
                    ref wasSelected
                );
                entry.selected = wasSelected;

                float rx = row.x + 315f;

                if (Widgets.ButtonText(new Rect(rx, row.y + 1f, 80f, 24f), $"G{activeGroup + 1}"))
                    entry.groupIndex = activeGroup;
                rx += 86f;

                if (Widgets.ButtonText(new Rect(rx, row.y + 1f, 80f, 24f), "Recall"))
                    entry.pawn.jobs?.TryTakeOrderedJob(
                        JobMaker.MakeJob(JobDefOf.Goto, link.pawn),
                        JobTag.Misc
                    );
                rx += 86f;

                if (Widgets.ButtonText(new Rect(rx, row.y + 1f, 80f, 24f), "Stop"))
                    entry.pawn.jobs?.StopAll();
                rx += 86f;

                if (Widgets.ButtonText(new Rect(rx, row.y + 1f, 80f, 24f), "Unlink"))
                {
                    if (toRemove == null) toRemove = new List<LinkedAnimalData>();
                    toRemove.Add(entry);
                }

                curY += rowHeight + 2f;
            }

            if (toRemove != null)
            {
                foreach (var e in toRemove)
                    link.linkedAnimals.Remove(e);
            }

            Widgets.EndScrollView();

            Text.Font = GameFont.Small;
        }
    }

}