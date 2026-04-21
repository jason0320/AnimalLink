using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace panda.AnimalLink
{
    public class Hediff_AnimalLinkImplant : HediffWithComps
    {
        public const int BaseBandwidth = 6;
        public const int BaseControlGroups = 2;

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
            if (pawn != null && pawn.IsHashIntervalTick(250))
                Cleanup();
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

            // Simple but readable: bigger animals cost more.
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
                Messages.Message("animallink failed: invalid target.", MessageTypeDefOf.RejectInput, false);
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
                Messages.Message("Not enough animal-link bandwidth.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            linkedAnimals.Add(new LinkedAnimalData
            {
                pawn = animal,
                groupIndex = 0,
                selected = false
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
            foreach (var e in linkedAnimals)
                e.selected = true;
        }

        public void SelectNone()
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                e.selected = false;
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
            {
                if (e.selected)
                    e.groupIndex = groupIndex;
            }
        }

        public void SelectGroup(int groupIndex)
        {
            Cleanup();
            foreach (var e in linkedAnimals)
                e.selected = e.groupIndex == groupIndex;
        }

        public void RecallAll()
        {
            RecallWhere(e => true);
        }

        public void RecallSelected()
        {
            RecallWhere(e => e.selected);
        }

        public void RecallGroup(int groupIndex)
        {
            RecallWhere(e => e.groupIndex == groupIndex);
        }

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

        public string SummaryLabel()
        {
            Cleanup();
            return $"{UsedBandwidth}/{MaxBandwidth} bandwidth, {MaxControlGroups} control groups, {linkedAnimals.Count} linked";
        }

        public string Tooltip()
        {
            Cleanup();
            return $"Bandwidth used: {UsedBandwidth}/{MaxBandwidth}\nControl groups: {MaxControlGroups}\nLinked animals: {linkedAnimals.Count}";
        }
    }
    public static class AnimalLinkUtility
    {
        public static Hediff_AnimalLinkImplant GetLink(Pawn pawn)
        {
            return pawn.health?.hediffSet?.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamed("AnimalLinkImplant")
            ) as Hediff_AnimalLinkImplant;
        }
    }
}