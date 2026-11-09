using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using VEF.Buildings;
using Verse;

namespace SkipdoorFixes
{
    // VEF re-blits the portal background every tick. On Waylith, Graphics.Blit leaves RenderTexture.active
    // on the portal texture, which redirects all GUI drawing into it (flicker at 1x, no UI at 2x/3x).
    // Restore the render target, and only re-blit once per frame for doors that are on screen.
    [HarmonyPatch(typeof(DoorTeleporter), nameof(DoorTeleporter.RecacheBackground))]
    public static class DoorTeleporter_RecacheBackground_Patch
    {
        private sealed class LastBlit { public int frame = -1; }

        private static readonly ConditionalWeakTable<Material, LastBlit> lastBlit = new();

        public static bool Prefix(DoorTeleporter __instance, out RenderTexture __state)
        {
            __state = RenderTexture.active;
            Material mat = __instance.backgroundMat;
            if (mat == null) return false;

            LastBlit last = lastBlit.GetOrCreateValue(mat);
            int frame = Time.frameCount;
            if (last.frame >= 0 && (last.frame == frame || !OnScreen(__instance))) return false;
            last.frame = frame;
            return true;
        }

        public static void Finalizer(RenderTexture __state)
        {
            if (!ReferenceEquals(RenderTexture.active, __state))
                RenderTexture.active = __state;
        }

        private static bool OnScreen(Thing door) =>
            door.Spawned
            && door.Map == Find.CurrentMap
            && !WorldRendererUtility.WorldSelected
            && Find.CameraDriver.CurrentViewRect.ExpandedBy(3).Contains(door.Position);
    }
}
