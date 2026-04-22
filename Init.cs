using HarmonyLib;
using Verse;

namespace panda.AnimalLink
{
    [StaticConstructorOnStartup]
    public static class Init
    {
        static Init()
        {
            new Harmony("panda.AnimalLink").PatchAll();
        }
    }
}
