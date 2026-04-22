using System.Collections.Generic;
using HarmonyLib;
using panda.AnimalLink;
using Verse;

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

        // Yield the bandwidth UI gizmo
        yield return new Gizmo_AnimalLinkBandwidth { pawn = __instance, link = link };

        // Dynamically yield group gizmos based on the animanitor's current max groups
        int maxGroups = link.MaxControlGroups;
        for (int i = 0; i < maxGroups; i++)
        {
            yield return new Gizmo_AnimalLinkGroup { pawn = __instance, link = link, groupIndex = i };
        }
    }
}