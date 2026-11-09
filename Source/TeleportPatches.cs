using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using VanillaPsycastsExpanded.Skipmaster;
using VEF.Buildings;
using Verse;
using Verse.AI.Group;

namespace SkipdoorFixes
{
    public static class ArrivalCells
    {
        private static bool Good(IntVec3 c, Map map) => c.InBounds(map) && c.Standable(map) && !c.Fogged(map);

        public static IntVec3 Pick(Thing dest, Map map, IntVec3 preferred)
        {
            if (dest == null || !dest.Spawned || dest.Map != map)
                return preferred.IsValid && preferred.InBounds(map) ? preferred : map.Center;

            if (preferred.IsValid && Good(preferred, map) && preferred.AdjacentTo8WayOrInside(dest))
                return preferred;
            if (GenAdj.CellsAdjacentCardinal(dest).Where(c => Good(c, map)).TryRandomElement(out IntVec3 cell)
                || GenAdj.CellsAdjacent8Way(dest).Where(c => Good(c, map)).TryRandomElement(out cell)
                || CellFinder.TryFindRandomCellNear(dest.Position, map, 4, c => Good(c, map), out cell))
                return cell;
            return dest.Position;
        }
    }

    // VPE picks a random standable cardinal cell; with none available that is (0,0,0), the map corner.
    [HarmonyPatch(typeof(Skipdoor), nameof(Skipdoor.DoTeleportEffects))]
    public static class Skipdoor_DoTeleportEffects_Patch
    {
        public static bool Prefix(Skipdoor __instance, Thing thing, int ticksLeftThisToil, Map targetMap,
            ref IntVec3 targetCell, DoorTeleporter dest)
        {
            if (ticksLeftThisToil != 15 || targetMap == null) return true;

            targetCell = ArrivalCells.Pick(dest, targetMap, IntVec3.Invalid);
            var target = new TargetInfo(targetCell, targetMap);
            Effecter effecter = EffecterDefOf.Skip_Exit.Spawn(targetCell, targetMap);
            effecter.ticksLeft = 15;
            effecter.EffectTick(target, target);
            __instance.teleportEffecters[thing] = effecter;
            return false;
        }
    }

    // VEF moves pawns with ExitMap(), which treats every hop as leaving the map: lord/duty/mind state are
    // cleared, quest "LeftMap" signals fire and the pather is never reset. Same-map hops just move the pawn
    // (keeps its job and carried thing), cross-map hops despawn/respawn like vanilla skip.
    [HarmonyPatch(typeof(DoorTeleporter), nameof(DoorTeleporter.Teleport))]
    public static class DoorTeleporter_Teleport_Patch
    {
        public static bool Prefix(DoorTeleporter __instance, Thing thing, Map mapTarget, IntVec3 cellTarget)
        {
            if (thing is not Pawn pawn || mapTarget == null || !pawn.Spawned) return true;

            try
            {
                DoorTeleporter dest = (pawn.jobs?.curDriver as JobDriver_UseDoorTeleporter)?.Dest
                    ?? WorldComponent_DoorTeleporterManager.Instance.DoorTeleporters
                        .FirstOrDefault(d => d.Spawned && d.Map == mapTarget && cellTarget.AdjacentTo8WayOrInside(d));
                IntVec3 cell = ArrivalCells.Pick(dest, mapTarget, cellTarget);

                if (pawn.Map == mapTarget)
                {
                    pawn.Position = cell;
                    pawn.Notify_Teleported(endCurrentJob: false);
                    return false;
                }

                Thing carried = null;
                if (pawn.carryTracker?.CarriedThing != null)
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out carried);

                bool drafted = pawn.Drafted;
                bool selected = Find.Selector.IsSelected(pawn);
                Lord lord = pawn.GetLord();

                pawn.DeSpawnOrDeselect();
                GenSpawn.Spawn(pawn, cell, mapTarget);
                pawn.Notify_Teleported(endCurrentJob: false);

                if (lord != null && lord.ownedPawns.Contains(pawn))
                    lord.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
                if (drafted && !pawn.Downed)
                    pawn.drafter.Drafted = true;
                if (selected && pawn.Map == Find.CurrentMap)
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);

                if (carried != null && !carried.Destroyed)
                {
                    if (carried.Spawned) carried.DeSpawn();
                    if (carried is Pawn carriedPawn)
                    {
                        GenSpawn.Spawn(carriedPawn, cell, mapTarget);
                        carriedPawn.Notify_Teleported();
                    }
                    else if (!GenPlace.TryPlaceThing(carried, cell, mapTarget, ThingPlaceMode.Near))
                    {
                        GenSpawn.Spawn(carried, cell, mapTarget);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[Skipdoor Fixes] Teleport failed for {pawn.ToStringSafe()}: {e}");
                if (!pawn.Spawned && !pawn.Destroyed && !pawn.IsWorldPawn())
                    GenSpawn.Spawn(pawn, ArrivalCells.Pick(null, mapTarget, cellTarget), mapTarget);
            }
            finally
            {
                __instance.teleportEffecters.Remove(thing);
            }
            return false;
        }
    }
}
