# HauntedDoors

**__Project details__**

**Project:** *HauntedDoors*  
**dev language:** *c# oxide*  
**Plugin language:** *en*  
**Author:** [@RustFlash](https://github.com/Flash-Ticker)

[![RustFlash - Your Favourite Trio Server](https://github.com/Flash-Ticker/HauntedDoors/blob/main/HauntedDoors_Thumb.png)](https://youtu.be/vnDBqlicfLU)

## Description

**HauntedDoors** spawns spooky “ghosts” when a player opens certain **hinged doors**. Despite the display description (“double doors”), the code checks **both single and double hinged** wood/metal/HQM doors. Ghosts spawn with optional **sound** and **visual** effects, then **despawn after a short duration**.

## Features

- **Random ghost spawn on door open**: Triggered by the `OnDoorOpened` hook → checks door type → rolls `SpawnChance` (default **0.15**) before spawning.  
- **Timed despawn**: Each ghost is removed after `GhostDuration` seconds (default **4**).  
- **Effects**: Optional **sounds** and **smoke/explosion-style visuals** on spawn and despawn.  
- **Ghost pool**: Picks from a list of prefabs (e.g., **scarecrow**, **zombie**) with per-ghost spawn/despawn sounds.  
- **Invulnerable & passive**: Ghosts are made effectively indestructible (huge health and protections), AI is disabled, orientation faces the player. They’re also not saved to disk and do not broadcast globally.  
- **Explosion dampening near ghosts**: Reduces/blocks explosion damage around players when ghosts are active (prevents accidental deaths from nearby booms).  
- **No commands or permissions**: Runs automatically; purely event-driven.

## Configuration (default)

`oxide/config/HauntedDoors.json`

```json
{
  "Ghost spawn chance (0.0 - 1.0)": 0.15,
  "Ghost visibility duration (seconds)": 4.0,
  "Play sound effects": true,
  "Show visual effects (smoke/fog)": true
}
```

**Notes**
- **Door filter**: Triggers on the following door short prefab names:  
  `door.hinged.wood`, `door.double.hinged.wood`, `door.hinged.metal`, `door.double.hinged.metal`, `door.hinged.toptier`, `door.double.hinged.toptier`.
- **Ghost types & effects**: Prefabs and SFX/VFX are defined in code. You can extend the ghost list and make your own effects pool if you fork the plugin.
- **Behavior**: Ghosts are invulnerable (very high health/protection), AI off, not persistent, and look toward the player. Explosion damage close to ghosts gets zeroed out for safety.

## Config keys explained

- `Ghost spawn chance (0.0 - 1.0)`: Probability that opening an allowed door spawns a ghost (0.0–1.0).  
- `Ghost visibility duration (seconds)`: Lifetime for spawned ghosts before they vanish.  
- `Play sound effects`: Toggle audio on spawn/despawn.  
- `Show visual effects (smoke/fog)`: Toggle visual FX on spawn/despawn.

---

**load, run, enjoy** 💝
