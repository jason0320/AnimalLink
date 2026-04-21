using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace panda.AnimalLink
{
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

            yield return new Command_Action
            {
                defaultLabel = "AnimalLink",
                defaultDesc = "Open animanitor management.",
                icon = AnimalLinkTex.Open,
                action = () => Find.WindowStack.Add(new Window_AnimalLink(link))
            };

            yield return new Command_Target
            {
                defaultLabel = "Link animal",
                defaultDesc = "Select an animal to link.",
                icon = AnimalLinkTex.Link,
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetAnimals = true,
                    canTargetHumans = false,
                    canTargetBuildings = false,
                    canTargetMechs = false,
                    canTargetLocations = false,
                    canTargetSelf = false,
                    onlyTargetColonists = false,
                    validator = t =>
                    {
                        var p = t.Thing as Pawn;
                        return p != null && p.RaceProps.Animal;
                    }
                },
                action = target =>
                {
                    var animal = target.Thing as Pawn;
                    if (animal != null) link.TryLinkAnimal(animal);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "Recall all",
                defaultDesc = "Recall all linked animals to this pawn.",
                icon = AnimalLinkTex.Recall,
                action = () => link.RecallAll()
            };

            yield return new Command_Target
            {
                defaultLabel = "Attack with selected",
                defaultDesc = "Order selected linked animals to attack a target.",
                icon = AnimalLinkTex.Attack,
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetAnimals = true,
                    canTargetHumans = true,
                    canTargetBuildings = true,
                    canTargetMechs = true,
                    canTargetLocations = false,
                    canTargetSelf = false,
                    onlyTargetColonists = false,
                    validator = t => t.Thing != null && !t.Thing.DestroyedOrNull()
                },
                action = target => link.AttackTarget(target)
            };
        }
    }
}