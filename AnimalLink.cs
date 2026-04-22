using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace panda.AnimalLink
{
    public enum AnimalLinkWorkMode
    {
        Work = 0,
        Follow = 1,
        Guard = 2
    }

    public class LinkedAnimalData : IExposable
    {
        public Pawn pawn;
        public int groupIndex;
        public bool selected;
        public bool drafted;
        public bool autoTreat;
        public AnimalLinkWorkMode workMode = AnimalLinkWorkMode.Work;
        public Area allowedArea;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref groupIndex, "groupIndex", 0);
            Scribe_Values.Look(ref selected, "selected", false);
            Scribe_Values.Look(ref drafted, "drafted", false);
            Scribe_Values.Look(ref autoTreat, "autoTreat", false);
            Scribe_Values.Look(ref workMode, "workMode", AnimalLinkWorkMode.Work);
            Scribe_References.Look(ref allowedArea, "allowedArea");
        }
    }

    public static class AnimalLinkUtility
    {
        public static Hediff_AnimalLinkImplant GetLink(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("AnimalLinkImplant");
            if (def == null) return null;

            return pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_AnimalLinkImplant;
        }

        public static BodyPartRecord GetBrain(Pawn pawn)
        {
            return pawn?.RaceProps?.body?.AllParts?.FirstOrDefault(p => p.def?.defName == "Brain");
        }
    }

    public class Hediff_AnimalLinkImplant : HediffWithComps
    {
        public const int BaseBandwidth = 6;
        public const int BaseControlGroups = 2;

        public int CountInGroup(int groupIndex)
        {
            Cleanup();
            return linkedAnimals.Count(e => e.groupIndex == groupIndex);
        }

        public int LinkedCount
        {
            get
            {
                Cleanup();
                return linkedAnimals.Count;
            }
        }

        public List<LinkedAnimalData> linkedAnimals = new List<LinkedAnimalData>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref linkedAnimals, "linkedAnimals", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && linkedAnimals == null)
                linkedAnimals = new List<LinkedAnimalData>();
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (linkedAnimals == null)
                linkedAnimals = new List<LinkedAnimalData>();
        }

        public override void Tick()
        {
            base.Tick();

            if (pawn != null && pawn.IsHashIntervalTick(60))
            {
                Cleanup();
                EnforceOrders();
            }
        }

        public int MaxBandwidth => BaseBandwidth;
        public int MaxControlGroups => BaseControlGroups;

        public int UsedBandwidth
        {
            get
            {
                Cleanup();
                int total = 0;
                foreach (var entry in linkedAnimals)
                {
                    if (entry?.pawn == null) continue;
                    total += BandwidthCostFor(entry.pawn);
                }
                return total;
            }
        }

        public int RemainingBandwidth => MaxBandwidth - UsedBandwidth;

        public static int BandwidthCostFor(Pawn animal)
        {
            if (animal == null) return 0;

            float s = animal.BodySize;
            if (s <= 0.5f) return 1;
            if (s <= 1.5f) return 1;
            if (s <= 2.5f) return 2;
            if (s <= 4.0f) return 3;
            return 4;
        }

        public IEnumerable<LinkedAnimalData> Entries()
        {
            Cleanup();
            return linkedAnimals
                .Where(e => e != null && e.pawn != null)
                .OrderBy(e => e.groupIndex)
                .ThenBy(e => e.pawn.LabelShortCap);
        }

        public LinkedAnimalData EntryFor(Pawn animal)
        {
            if (animal == null) return null;
            Cleanup();
            return linkedAnimals.FirstOrDefault(e => e?.pawn == animal);
        }

        public bool IsLinked(Pawn animal) => EntryFor(animal) != null;

        public bool TryLinkAnimal(Pawn animal)
        {
            Cleanup();

            if (animal == null || animal.DestroyedOrNull() || animal.Dead || !animal.Spawned)
            {
                Messages.Message("AnimalLink failed: invalid target.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!animal.RaceProps.Animal)
            {
                Messages.Message("Only animals can be linked.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (IsLinked(animal))
            {
                Messages.Message($"{animal.LabelShortCap} is already linked.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            int cost = BandwidthCostFor(animal);
            if (UsedBandwidth + cost > MaxBandwidth)
            {
                Messages.Message("Not enough AnimalLink bandwidth.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            linkedAnimals.Add(new LinkedAnimalData
            {
                pawn = animal,
                groupIndex = 0,
                selected = false,
                drafted = false,
                autoTreat = false,
                workMode = AnimalLinkWorkMode.Work,
                allowedArea = null
            });

            Messages.Message($"{animal.LabelShortCap} linked.", animal, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public void Cleanup()
        {
            if (linkedAnimals == null)
            {
                linkedAnimals = new List<LinkedAnimalData>();
                return;
            }

            linkedAnimals.RemoveAll(e =>
                e == null ||
                e.pawn == null ||
                e.pawn.DestroyedOrNull() ||
                e.pawn.Dead ||
                !e.pawn.Spawned ||
                e.pawn.Map != pawn.Map ||
                !e.pawn.RaceProps.Animal);
        }

        public int SelectedCount()
        {
            Cleanup();
            return linkedAnimals.Count(e => e.selected);
        }

        public void SelectAll()
        {
            Cleanup();
            foreach (var e in linkedAnimals) e.selected = true;
        }

        public void SelectNone()
        {
            Cleanup();
            foreach (var e in linkedAnimals) e.selected = false;
        }

        public void DeleteSelected()
        {
            Cleanup();
            linkedAnimals.RemoveAll(e => e.selected);
        }

        public void SetSelectedGroup(int groupIndex)
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                if (e.selected) e.groupIndex = groupIndex;
        }

        public void SelectGroup(int groupIndex)
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                e.selected = e.groupIndex == groupIndex;
        }

        public void SetSelectedDraft(bool drafted)
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                if (e.selected) e.drafted = drafted;
        }

        public void SetSelectedAutoTreat(bool autoTreat)
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                if (e.selected) e.autoTreat = autoTreat;
        }

        public void SetSelectedWorkMode(AnimalLinkWorkMode mode)
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                if (e.selected) e.workMode = mode;
        }

        public void CycleSelectedArea()
        {
            Cleanup();
            var map = pawn?.Map;
            if (map == null) return;

            List<Area> areas = map.areaManager.AllAreas.ToList();
            if (areas.Count == 0) return;

            foreach (var e in linkedAnimals.Where(x => x.selected))
            {
                int idx = -1;
                if (e.allowedArea != null)
                    idx = areas.IndexOf(e.allowedArea);

                int next = (idx + 1) % (areas.Count + 1);
                e.allowedArea = next == areas.Count ? null : areas[next];
                if (e.pawn.playerSettings != null)
                    e.pawn.playerSettings.AreaRestrictionInPawnCurrentMap = e.allowedArea;
            }
        }

        public void RecallAll() => RecallWhere(e => true);
        public void RecallSelected() => RecallWhere(e => e.selected);
        public void RecallGroup(int groupIndex) => RecallWhere(e => e.groupIndex == groupIndex);

        private void RecallWhere(Func<LinkedAnimalData, bool> predicate)
        {
            Cleanup();

            foreach (var e in linkedAnimals.Where(predicate))
            {
                if (e?.pawn == null || e.pawn.DestroyedOrNull() || !e.pawn.Spawned) continue;
                if (e.pawn.Map != pawn.Map) continue;

                e.pawn.jobs?.TryTakeOrderedJob(
                    JobMaker.MakeJob(JobDefOf.Goto, pawn),
                    JobTag.Misc
                );
            }
        }

        public void StopSelected()
        {
            Cleanup();

            foreach (var e in linkedAnimals.Where(x => x.selected))
            {
                if (e?.pawn == null || e.pawn.DestroyedOrNull() || !e.pawn.Spawned) continue;
                e.pawn.jobs?.StopAll();
            }
        }

        public void AttackTarget(LocalTargetInfo target)
        {
            Cleanup();

            if (!target.IsValid || target.Thing == null) return;

            foreach (var e in linkedAnimals.Where(x => x.selected))
            {
                if (e?.pawn == null || e.pawn.DestroyedOrNull() || !e.pawn.Spawned) continue;
                if (e.pawn.Map != pawn.Map) continue;

                e.pawn.jobs?.TryTakeOrderedJob(
                    JobMaker.MakeJob(JobDefOf.AttackMelee, target.Thing),
                    JobTag.Misc
                );
            }
        }

        private void EnforceOrders()
        {
            Cleanup();

            foreach (var e in linkedAnimals)
            {
                if (e?.pawn == null || e.pawn.DestroyedOrNull() || !e.pawn.Spawned) continue;
                if (e.pawn.Map != pawn.Map) continue;

                // AutoTreat behavior
                if (e.autoTreat && e.pawn.health.HasHediffsNeedingTend())
                {
                    // Ensure the animal waits in place to be treated
                    if (e.pawn.jobs != null && (e.pawn.CurJob == null || e.pawn.CurJob.def != JobDefOf.Wait_MaintainPosture))
                    {
                        e.pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture), JobTag.Misc);
                    }

                    // Assign the tend job to the overseer pawn if they are capable and not already doing it
                    if (pawn.Spawned && !pawn.Downed && !pawn.Dead && !pawn.Drafted)
                    {
                        if (pawn.jobs != null && (pawn.CurJob == null || pawn.CurJob.def != JobDefOf.TendPatient || pawn.CurJob.targetA.Thing != e.pawn))
                        {
                            Job tendJob = JobMaker.MakeJob(JobDefOf.TendPatient, e.pawn);
                            pawn.jobs.TryTakeOrderedJob(tendJob, JobTag.Misc);
                        }
                    }
                    continue;
                }

                if (e.workMode == AnimalLinkWorkMode.Follow)
                {
                    if (e.pawn.CurJob == null || e.pawn.CurJob.def != JobDefOf.Goto)
                    {
                        e.pawn.jobs?.TryTakeOrderedJob(
                            JobMaker.MakeJob(JobDefOf.Goto, pawn),
                            JobTag.Misc
                        );
                    }
                }

                if (e.allowedArea != null && e.pawn.playerSettings != null)
                {
                    e.pawn.playerSettings.AreaRestrictionInPawnCurrentMap = e.allowedArea;
                }
            }
        }

        public string SummaryLabel()
        {
            Cleanup();
            return $"{UsedBandwidth}/{MaxBandwidth} bandwidth, {MaxControlGroups} groups, {linkedAnimals.Count} linked";
        }

        public string Tooltip()
        {
            Cleanup();
            return $"Bandwidth used: {UsedBandwidth}/{MaxBandwidth}\nControl groups: {MaxControlGroups}\nLinked animals: {linkedAnimals.Count}";
        }
    }
}