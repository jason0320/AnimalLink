using UnityEngine;
using Verse;

namespace panda.AnimalLink
{
    public static class AnimalLinkTex
    {
        // Reuse vanilla Biotech mech-style ability icons.
        public static readonly Texture2D Open = ContentFinder<Texture2D>.Get("UI/Abilities/PreachHealth", true);
        public static readonly Texture2D Link = ContentFinder<Texture2D>.Get("UI/Abilities/AnimalCalm", true);
        public static readonly Texture2D Recall = ContentFinder<Texture2D>.Get("UI/Abilities/AnimalWarcall", true);
        public static readonly Texture2D Attack = ContentFinder<Texture2D>.Get("UI/Abilities/AnimalWarcall", true);

        // Fall back to the same vanilla mech icon for the rest.
        public static readonly Texture2D Stop = Attack;
        public static readonly Texture2D Group = Recall;
        public static readonly Texture2D Unlink = Link;
    }
}