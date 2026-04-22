using System.Linq;
using RimWorld;
using Verse;

namespace panda.AnimalLink
{
    public class MainButtonWorker_AnimalLink : MainButtonWorker_ToggleTab
    {
        public override bool Visible
        {
            get
            {
                // If standard visibility checks fail (e.g., no map loaded), keep it hidden
                if (!base.Visible || Find.CurrentMap == null)
                {
                    return false;
                }

                // Show the tab ONLY if there is at least one colonist with an AnimalLink implant
                return Find.CurrentMap.mapPawns.FreeColonistsSpawned
                           .Any(p => AnimalLinkUtility.GetLink(p) != null);
            }
        }
    }
}