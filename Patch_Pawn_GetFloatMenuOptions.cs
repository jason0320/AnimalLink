using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace panda.AnimalLink
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetFloatMenuOptions))]
    public static class Patch_Pawn_GetFloatMenuOptions
    {
        // __instance  = the pawn being right-clicked (the animal)
        // selPawn     = the pawn that is currently selected (the Animanitor)
        public static IEnumerable<FloatMenuOption> Postfix(
            IEnumerable<FloatMenuOption> __result,
            Pawn __instance,
            Pawn selPawn)
        {
            foreach (var opt in __result)
                yield return opt;

            // Only inject when the SELECTED pawn is an Animanitor
            if (selPawn == null) yield break;
            var link = AnimalLinkUtility.GetLink(selPawn);
            if (link == null) yield break;

            // Only inject when the TARGET is an animal
            if (__instance == null) yield break;
            if (!__instance.RaceProps.Animal) yield break;
            if (!__instance.Spawned || __instance.Dead) yield break;
            if (__instance.Map != selPawn.Map) yield break;

            Pawn animal = __instance; // capture for lambdas

            if (link.IsLinked(animal))
            {
                // ── Release ───────────────────────────────────────────────────
                yield return new FloatMenuOption(
                    $"Release {animal.LabelShortCap} from AnimalLink",
                    () =>
                    {
                        link.linkedAnimals.RemoveAll(e => e?.pawn == animal);
                        Messages.Message(
                            $"{animal.LabelShortCap} released from AnimalLink.",
                            animal, MessageTypeDefOf.NeutralEvent, false);
                    },
                    MenuOptionPriority.Default, null, animal);
            }
            else
            {
                // ── Link ──────────────────────────────────────────────────────
                int cost = Hediff_AnimalLinkImplant.BandwidthCostFor(animal);
                bool canLink = link.UsedBandwidth + cost <= link.MaxBandwidth;

                if (canLink)
                {
                    yield return new FloatMenuOption(
                        $"Link {animal.LabelShortCap} to AnimalLink (cost: {cost} BW)",
                        () => link.TryLinkAnimal(animal),
                        MenuOptionPriority.Default, null, animal);
                }
                else
                {
                    yield return new FloatMenuOption(
                        $"Cannot link {animal.LabelShortCap} – not enough bandwidth " +
                        $"({cost} BW needed, {link.RemainingBandwidth} free)",
                        null)   // null action = greyed-out / unclickable
                    { Disabled = true };
                }
            }
        }
    }
}
