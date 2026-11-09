# Skipdoor Fixes

Harmony patch mod for the Vanilla Psycasts Expanded skipdoor (`VEF.Buildings.DoorTeleporter`) on RimWorld 1.7.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836713439) · Upstream fixes: [VEF #198](https://github.com/Vanilla-Expanded/VanillaExpandedFramework/pull/198), [VPE #39](https://github.com/Vanilla-Expanded/VanillaPsycastsExpanded/pull/39)

| Patch | Problem |
|---|---|
| `DoorTeleporter.RecacheBackground` | `Graphics.Blit` leaves `RenderTexture.active` on the portal texture; on Waylith that redirects all GUI drawing into it (UI flicker at 1x, no UI at 2x/3x). Restores the render target and only re-blits once per frame for on-screen doors. |
| `DoorTeleporter.Teleport` | VEF uses `Pawn.ExitMap()` even for same-map hops, which clears lord, duty and mind state and fires "left map" signals. Same-map hops now move the pawn; cross-map hops despawn and respawn it. |
| `Skipdoor.DoTeleportEffects` | Arrival cell falls back to `(0,0,0)` when no cardinal neighbour is standable. Picks a standable, unfogged cell next to the door instead. |
| Skipdoor Pathing (Unlimited), if loaded | Its route planner ignores the levels of As above, So below II and replays a 60-tick cached route after the hop, so pawns walk between levels to reach a skipdoor and bounce between doors forever. Routes whose doors are on the wrong level, or whose exit is nearer than the entry, are rejected. Reaching the entry door also counted as reaching the job's destination (sleeping outside the bed, building from across the map); the pawn now waits at the door for the hop. |

Patch-only: no defs, comps or saved data, so it is safe to add or remove mid-save.

## Building

```
cd Source
dotnet build -c Release
```

Compiles against the game's own assemblies and outputs to `1.7/Assemblies`. Override paths with `-p:RimWorldDir=...` and `-p:WorkshopDir=...` if your Steam library is elsewhere.
