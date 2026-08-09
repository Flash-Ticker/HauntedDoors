using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Network;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("HauntedDoors", "RustFlash", "1.4.0")]
    [Description("Spawns scary ghosts when opening double doors")]
    public class HauntedDoors : RustPlugin
    {
        #region Configuration
        
        private Configuration config;
        
        public class Configuration
        {
            [JsonProperty("Ghost spawn chance (0.0 - 1.0)")]
            public float SpawnChance { get; set; } = 0.15f;
            
            [JsonProperty("Ghost visibility duration (seconds)")]
            public float GhostDuration { get; set; } = 4f;
            
            [JsonProperty("Play sound effects")]
            public bool EnableSounds { get; set; } = true;
            
            [JsonProperty("Show visual effects (smoke/fog)")]
            public bool EnableVisualEffects { get; set; } = true;
        }
        
        #endregion
        
        #region Fields
        
        private Dictionary<NetworkableId, string> ghostDespawnSounds = new Dictionary<NetworkableId, string>();
        private readonly System.Random random = new System.Random();
        
        private readonly List<GhostType> ghostTypes = new List<GhostType>
        {
            new GhostType
            {
                Prefab = "assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab",
                SpawnSound = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab",
                DespawnSound = "assets/bundled/prefabs/fx/missing.prefab"
            },
            new GhostType
            {
                Prefab = "assets/rust.ai/agents/zombie/zombie.prefab",
                SpawnSound = "assets/bundled/prefabs/fx/missing.prefab",
                DespawnSound = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab"
            }
        };
        
        private readonly List<string> spawnEffects = new List<string>
        {
            "assets/bundled/prefabs/fx/explosions/explosion_03.prefab",
            "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab",
            "assets/bundled/prefabs/fx/explosions/explosion_02.prefab"
        };
        
        private readonly List<string> despawnEffects = new List<string>
        {
            "assets/bundled/prefabs/fx/explosions/explosion_03.prefab",
            "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab",
            "assets/bundled/prefabs/fx/explosions/explosion_02.prefab"
        };
        
        private class GhostType
        {
            public string Prefab { get; set; }
            public string SpawnSound { get; set; }
            public string DespawnSound { get; set; }
        }
        
        #endregion
        
        #region Hooks
        
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            
            if (entity.net != null && ghostDespawnSounds.ContainsKey(entity.net.ID))
            {
                return true;
            }
            
            if (entity is BasePlayer player)
            {
                if (info.damageTypes.Has(Rust.DamageType.Explosion))
                {
                    foreach (var ghostId in ghostDespawnSounds.Keys.ToList())
                    {
                        var ghost = BaseNetworkable.serverEntities.Find(ghostId) as BaseEntity;
                        if (ghost != null && !ghost.IsDestroyed && Vector3.Distance(ghost.transform.position, player.transform.position) < 10f)
                        {
                            info.damageTypes.Set(Rust.DamageType.Explosion, 0f);
                            break;
                        }
                    }
                }
            }
            
            return null;
        }
        
        #endregion
        
        #region Initialization
        
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Loading default config.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        
        protected override void SaveConfig() => Config.WriteObject(config);
        
        #endregion
        
        #region Door Events
        
        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (door == null || player == null || player.IsNpc)
                return;
            
            HandleDoorOpen(door, player);
        }

        private void HandleDoorOpen(Door door, BasePlayer player)
        {
            if (!IsDoubleDoor(door))
                return;
            
            if (random.NextDouble() > config.SpawnChance)
                return;
            
            SpawnGhost(door, player);
        }
        
        #endregion
        
        #region Core Methods
        
        private bool IsDoubleDoor(Door door)
        {
            if (door?.ShortPrefabName == null)
                return false;
            
            string prefabName = door.ShortPrefabName.ToLower();
            
            return prefabName == "door.hinged.wood" ||
                   prefabName == "door.double.hinged.wood" ||
                   prefabName == "door.hinged.metal" ||
                   prefabName == "door.double.hinged.metal" ||
                   prefabName == "door.hinged.toptier" ||
                   prefabName == "door.double.hinged.toptier";
        }
        
        private void SpawnGhost(Door door, BasePlayer player)
        {
            if (ghostTypes.Count == 0)
            {
                PrintError("No ghost types configured!");
                return;
            }
            
            GhostType selectedGhost = ghostTypes[random.Next(0, ghostTypes.Count)];
            Vector3 spawnPos = CalculateGhostSpawnPosition(player);
            
            ApplySpawnEffects(spawnPos, player, selectedGhost.SpawnSound);
            
            timer.Once(0.5f, () =>
            {
                if (player == null || player.IsDestroyed)
                    return;
                
                BaseEntity ghost = GameManager.server.CreateEntity(selectedGhost.Prefab, spawnPos);
                if (ghost == null)
                {
                    PrintError($"Failed to create ghost entity: {selectedGhost.Prefab}");
                    return;
                }
                
                Vector3 direction = (player.transform.position - spawnPos).normalized;
                if (direction != Vector3.zero)
                    ghost.transform.rotation = Quaternion.LookRotation(direction);
                
                ghost.Spawn();
                
                ghostDespawnSounds[ghost.net.ID] = selectedGhost.DespawnSound;
                
                timer.Once(0.1f, () =>
                {
                    if (ghost != null && !ghost.IsDestroyed)
                    {
                        ConfigureGhost(ghost, player);
                    }
                });
                
                timer.Once(config.GhostDuration, () => DespawnGhost(ghost, player));
            });
        }
        
        private void ConfigureGhost(BaseEntity ghost, BasePlayer player)
        {
            if (ghost.ShortPrefabName != null && ghost.ShortPrefabName.Contains("zombie"))
            {
                var npc = ghost as NPCPlayer;
                if (npc != null)
                {
                    npc.InitializeHealth(999999f, 999999f);
                    npc.startHealth = 999999f;
                    npc.lifestate = BaseCombatEntity.LifeState.Alive;
                    
                    npc.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
                    npc.baseProtection.amounts = new float[25];
                    for (int i = 0; i < 25; i++)
                        npc.baseProtection.amounts[i] = 999999f;
                    
                    npc.SetFlagLocal(BaseEntity.Flags.Reserved8, true);
                    
                    var navigator = npc.GetComponent<NPCPlayerNavigator>();
                    if (navigator != null)
                        navigator.enabled = false;
                    
                    var brain = npc.GetComponent<BaseAIBrain>();
                    if (brain != null)
                        brain.enabled = false;
                        
                    npc.SetDestination(npc.transform.position);
                }
            }
            else if (ghost is ScarecrowNPC scarecrow)
            {
                scarecrow.InitializeHealth(999999f, 999999f);
                scarecrow.startHealth = 999999f;
                scarecrow.lifestate = BaseCombatEntity.LifeState.Alive;
                
                scarecrow.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
                scarecrow.baseProtection.amounts = new float[25];
                for (int i = 0; i < 25; i++)
                    scarecrow.baseProtection.amounts[i] = 999999f;
                
                scarecrow.SetFlagLocal(BaseEntity.Flags.Reserved8, true);
                
                var brain = scarecrow.GetComponent<ScarecrowBrain>();
                if (brain != null)
                    brain.enabled = false;
            }
            else
            {
                if (ghost is BaseCombatEntity combatEntity)
                {
                    combatEntity.InitializeHealth(999999f, 999999f);
                    combatEntity.startHealth = 999999f;
                    combatEntity.lifestate = BaseCombatEntity.LifeState.Alive;
                    
                    combatEntity.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
                    combatEntity.baseProtection.amounts = new float[25];
                    for (int i = 0; i < 25; i++)
                        combatEntity.baseProtection.amounts[i] = 999999f;
                    
                    combatEntity.SetFlagLocal(BaseEntity.Flags.Reserved8, true);
                }
                
                if (player != null && !player.IsDestroyed)
                {
                    Vector3 direction = (player.transform.position - ghost.transform.position).normalized;
                    if (direction != Vector3.zero)
                        ghost.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            
            ghost.EnableSaving(false);
            ghost.EnableGlobalBroadcast(false);
            ghost.SetFlagLocal(BaseEntity.Flags.Reserved8, true);
            ghost.SendNetworkUpdate();
        }
        
        private void DespawnGhost(BaseEntity ghost, BasePlayer player)
        {
            if (ghost == null || ghost.IsDestroyed)
                return;
            
            string despawnSound = null;
            if (ghostDespawnSounds.ContainsKey(ghost.net.ID))
            {
                despawnSound = ghostDespawnSounds[ghost.net.ID];
                ghostDespawnSounds.Remove(ghost.net.ID);
            }
            
            ApplyDespawnEffects(ghost.transform.position, player, despawnSound);
            ghost.Kill();
        }
        
        #endregion
        
        #region Effects
        
        private void ApplySpawnEffects(Vector3 position, BasePlayer player, string spawnSound)
        {
            if (config.EnableSounds && !string.IsNullOrEmpty(spawnSound))
            {
                Effect.server.Run(spawnSound, position);
            }
            
            if (config.EnableVisualEffects && spawnEffects.Count > 0)
            {
                string randomEffect = GetRandomEffect(spawnEffects);
                if (!string.IsNullOrEmpty(randomEffect))
                {
                    Effect.server.Run(randomEffect, position);
                }
            }
        }
        
        private void ApplyDespawnEffects(Vector3 position, BasePlayer player, string despawnSound)
        {
            if (config.EnableSounds && !string.IsNullOrEmpty(despawnSound))
            {
                Effect.server.Run(despawnSound, position);
            }
            
            if (config.EnableVisualEffects && despawnEffects.Count > 0)
            {
                string randomEffect = GetRandomEffect(despawnEffects);
                if (!string.IsNullOrEmpty(randomEffect))
                {
                    Effect.server.Run(randomEffect, position);
                }
            }
        }
        
        private string GetRandomEffect(List<string> effects)
        {
            if (effects == null || effects.Count == 0)
                return null;
            
            return effects[random.Next(0, effects.Count)];
        }
        
        #endregion
        
        #region Helper Methods
        
        private Vector3 CalculateGhostSpawnPosition(BasePlayer player)
        {
            Vector3 playerForward = player.eyes.HeadForward();
            Vector3 spawnPos = player.transform.position + (playerForward * 2f);
            
            RaycastHit hit;
            if (Physics.Raycast(new Ray(spawnPos + Vector3.up * 2f, Vector3.down), out hit, 4f, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                spawnPos.y = hit.point.y;
            }
            else
            {
                spawnPos.y = player.transform.position.y;
            }
            
            return spawnPos;
        }
        
        #endregion
    }
}