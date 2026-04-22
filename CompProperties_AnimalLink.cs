using RimWorld;
using Verse;

namespace panda.AnimalLink
{
    public class CompProperties_AnimalLink : CompProperties_UseEffect
    {
        public CompProperties_AnimalLink()
        {
            compClass = typeof(CompUseEffect_AnimalLink);
        }
    }

    public class CompUseEffect_AnimalLink : CompUseEffect
    {
        public override void DoEffect(Pawn user)
        {
            if (user == null) return;

            if (AnimalLinkUtility.GetLink(user) != null)
            {
                Messages.Message("Already has AnimalLink.", user, MessageTypeDefOf.RejectInput);
                return;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("AnimalLinkImplant");
            if (hediffDef == null) return;

            var brain = user.RaceProps.body.AllParts.FirstOrDefault(p => p.def?.defName == "Brain");
            if (brain == null) return;

            user.health.AddHediff(hediffDef, brain);
            parent.Destroy();
        }
    }
}