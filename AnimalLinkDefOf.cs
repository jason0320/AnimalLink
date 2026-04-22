using RimWorld;
using Verse;

namespace panda.AnimalLink
{
    [DefOf]
    public static class AnimalLinkDefOf
    {
        public static StatDef AnimalBandwidth;

        static AnimalLinkDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AnimalLinkDefOf));
        }
    }
}