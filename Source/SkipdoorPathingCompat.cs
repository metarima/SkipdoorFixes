using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VEF;
using VEF.Buildings;
using Verse;
using Verse.AI;

namespace SkipdoorFixes
{
    // Optional fixes for "Skipdoor Pathing (Unlimited)", which reroutes pawns through skipdoors from a
    // StartPath prefix. Applied only when that mod is loaded.
    internal static class SkipdoorPathing
    {
        public static readonly Type Util = AccessTools.TypeByName("SkipdoorPathing.SkipdoorPathingUtil");
        public static readonly Type Manager = AccessTools.TypeByName("SkipdoorPathing.SkipdoorTransitManager");
        private static readonly MethodInfo planData = Manager == null ? null : AccessTools.Method(Manager, "TryGetPlanDraftData");

        // "As above, So below" stacks every level of a map into bands of the same cell grid.
        private static readonly Func<Map, IntVec3, IntVec3, bool> sameBand =
            AccessTools.Method("AsAboveSoBelow.ABBands:SameBand") is MethodInfo m
                ? AccessTools.MethodDelegate<Func<Map, IntVec3, IntVec3, bool>>(m)
                : null;

        private static readonly ConditionalWeakTable<Map, MapComponent> managers = new();

        public static bool SameLevel(Map map, IntVec3 a, IntVec3 b) => sameBand == null || sameBand(map, a, b);

        public static bool TryGetEntryCell(Pawn pawn, out IntVec3 entryCell)
        {
            entryCell = IntVec3.Invalid;
            MapComponent manager = ManagerOf(pawn.Map);
            if (manager == null) return false;
            object[] args = { pawn, null, null, null, null };
            if (!(bool)planData.Invoke(manager, args)) return false;
            entryCell = (IntVec3)args[1];
            return true;
        }

        private static MapComponent ManagerOf(Map map)
        {
            if (map == null || planData == null) return null;
            if (managers.TryGetValue(map, out MapComponent found)) return found;
            foreach (MapComponent comp in map.components)
            {
                if (comp.GetType() != Manager) continue;
                managers.Add(map, comp);
                return comp;
            }
            return null;
        }
    }

    // Reject routes whose entry door is on another level than the pawn, whose exit door is on another level than
    // the destination, or whose exit is closer than the entry (its 60-tick cache replaying a finished hop).
    // Each of these sends the pawn back and forth between stairs and skipdoors.
    [HarmonyPatch]
    public static class SkipdoorPathing_FindPathToTeleporter_Patch
    {
        public static bool Prepare() => SkipdoorPathing.Util != null;

        public static MethodBase TargetMethod() => AccessTools.Method(SkipdoorPathing.Util, "FindPathToTeleporter");

        public static void Postfix(ref bool __result, Pawn pawn, LocalTargetInfo destination,
            ref DoorTeleporter startTeleporter, ref DoorTeleporter endTeleporter)
        {
            if (!__result || startTeleporter == null || endTeleporter == null) return;

            Map map = pawn.Map;
            IntVec3 from = pawn.Position;
            if (SkipdoorPathing.SameLevel(map, from, startTeleporter.Position)
                && SkipdoorPathing.SameLevel(map, endTeleporter.Position, destination.Cell)
                && (from - startTeleporter.Position).LengthManhattan < (from - endTeleporter.Position).LengthManhattan)
                return;

            __result = false;
            startTeleporter = null;
            endTeleporter = null;
        }
    }

    // Reaching the entry door would count as reaching the job's destination and advance the toil (sleeping
    // outside the bed, building from across the map). Hold the pawn; Skipdoor Pathing teleports it and repaths.
    [HarmonyPatch(typeof(Pawn_PathFollower), "PatherArrived")]
    public static class Pawn_PathFollower_PatherArrived_Patch
    {
        public static bool Prepare() => SkipdoorPathing.Manager != null;

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            if (___pawn.Drafted || ___pawn.CurJobDef == VEFDefOf.VEF_UseDoorTeleporter || !OnDoorCell(___pawn)) return true;
            if (!SkipdoorPathing.TryGetEntryCell(___pawn, out IntVec3 entryCell) || ___pawn.Position != entryCell)
                return true;

            __instance.StopDead();
            return false;
        }

        private static bool OnDoorCell(Pawn pawn)
        {
            foreach (DoorTeleporter door in WorldComponent_DoorTeleporterManager.Instance.DoorTeleporters)
                if (door.Map == pawn.Map && door.InteractionCell == pawn.Position) return true;
            return false;
        }
    }
}
