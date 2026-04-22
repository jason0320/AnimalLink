using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace panda.AnimalLink
{
    // =========================================================================
    //  Gizmo patch – yields 3 separate gizmos for Animanitors
    // =========================================================================
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result)
                yield return g;

            var link = AnimalLinkUtility.GetLink(__instance);
            if (link == null) yield break;
            if (!__instance.Spawned || !__instance.IsColonistPlayerControlled) yield break;

            yield return new Gizmo_AnimalLinkBandwidth { pawn = __instance, link = link };
            yield return new Gizmo_AnimalLinkGroup    { pawn = __instance, link = link, groupIndex = 0 };
            yield return new Gizmo_AnimalLinkGroup    { pawn = __instance, link = link, groupIndex = 1 };
        }
    }

    // =========================================================================
    //  Right-click context menu – Link / Release
    //
    //  Pawn.GetFloatMenuOptions(Pawn selPawn) is called on the TARGET pawn
    //  when a selected pawn right-clicks it.  It is virtual, public, and has
    //  been stable since RimWorld 1.0.  We postfix it on the ANIMAL side:
    //  when the animal is right-clicked by a selected Animanitor, we inject
    //  Link or Release options.
    // =========================================================================
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
                int  cost    = Hediff_AnimalLinkImplant.BandwidthCostFor(animal);
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
