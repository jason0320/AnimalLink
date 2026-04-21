using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace panda.AnimalLink
{
    [HarmonyLib.HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            var link = AnimalLinkUtility.GetLink(__instance);
            if (link == null) return;

            string extra = link.SummaryLabel();
            if (string.IsNullOrWhiteSpace(__result))
                __result = extra;
            else
                __result += "\n" + extra;
        }
    }
}
