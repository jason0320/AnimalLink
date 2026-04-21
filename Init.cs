using HarmonyLib;
using Verse;

namespace panda.AnimalLink
{
    [StaticConstructorOnStartup]
    public static class Init
    {
        static Init()
        {
            new HarmonyLib.Harmony("panda.animallink").PatchAll();
        }
    }
}