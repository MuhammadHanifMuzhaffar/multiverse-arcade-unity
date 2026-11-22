# Dragon Hunter — Asset Manifest
PIC: Muhammad Hanif (Velmiree)

Record each original or third-party asset used in this game. Do not reuse assets from other games.

| Asset | Type | Creator / Source | License / Permission | Unique to this game? |
| :--- | :--- | :--- | :--- | :--- |
| `Materials/M_DH_Arena*.mat`, `M_DH_EmberCrack.mat` (5) | Material (Standard shader, flat colour) | Created in-project by PIC via `DragonHunterSceneBuilder` | Original work | Yes |
| `Materials/M_DH_Hunter*.mat`, `M_DH_Blade.mat`, `M_DH_Gold.mat`, `M_DH_Arrow.mat` (7) | Material (Standard shader, flat colour) | Created in-project by PIC | Original work | Yes |
| `Materials/M_DH_Dragon*.mat` (6) | Material (Standard shader, flat colour) | Created in-project by PIC | Original work | Yes |
| `Materials/M_DH_Telegraph*.mat` (2) | Material (built-in Sprites/Default shader, transparent) | Created in-project by PIC | Original work | Yes |
| `Materials/M_DH_FireParticle.mat` | Material (built-in Legacy Particles/Additive shader) | Created in-project by PIC | Original work | Yes |
| Hunter, Emberscale dragon, arena geometry | Composed from Unity built-in primitive meshes inside `DragonHunter_Main.unity` | Assembled by PIC | Unity built-in (engine resource, not a project asset) | Yes (composition) |
| Attack telegraph sector meshes | Procedural mesh generated at runtime (`ProceduralMeshes.cs`) | PIC | Original work | Yes |
| UI text font | Unity built-in `LegacyRuntime.ttf` | Unity | Engine resource | Placeholder — replace with a font exclusive to Dragon Hunter in Sprint 2 |

Sprint 2 (planned): dragon/hunter models, roar/fire/sword SFX, battle music. Each must be added here with source and license before commit.
