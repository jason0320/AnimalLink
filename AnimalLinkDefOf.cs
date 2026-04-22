using RimWorld;
using Verse;

namespace panda.AnimalLink
{
    [DefOf]
    public static class AnimalLinkDefOf
    {
        public static StatDef AnimalBandwidth;
        public static StatDef AnimalControlGroups;

        static AnimalLinkDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AnimalLinkDefOf));
        }
    }
}