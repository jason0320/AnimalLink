using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace panda.AnimalLink
{
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
    public static class Patch_AnimanitorStartImplant
    {
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            if (__result == null) return;
            if (__result.kindDef == null) return;

            // Only your special starting pawn kind
            if (__result.kindDef.defName != "Animanitor")
                return;

            // Don't add twice
            if (AnimalLinkUtility.GetLink(__result) != null)
                return;

            HediffDef implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("AnimalLinkImplant");
            if (implantDef == null)
                return;

            BodyPartRecord brain = __result.RaceProps.body.AllParts
                .FirstOrDefault(p => p.def != null && p.def.defName == "Brain");

            if (brain == null)
            {
                Log.Warning($"AnimalLink: No brain part found for pawn {__result.LabelShortCap}.");
                return;
            }

            __result.health.AddHediff(implantDef, brain);
        }
    }
}