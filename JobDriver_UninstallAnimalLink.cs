using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace panda.AnimalLink
{
    public class JobDriver_UninstallAnimalLink : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield break;
        }
    }
}