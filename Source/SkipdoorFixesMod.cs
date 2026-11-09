using HarmonyLib;
using Verse;

namespace SkipdoorFixes
{
    public class SkipdoorFixesMod : Mod
    {
        public SkipdoorFixesMod(ModContentPack content) : base(content)
        {
            new Harmony("local.skipdoorfixes").PatchAll();
        }
    }
}
