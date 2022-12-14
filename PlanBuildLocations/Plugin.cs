// PlanBuildLocations
// a Valheim mod for using PlanBuild blueprints as locations
// 
// File:    Plugin.cs
// Project: PlanBuildLocations

using BepInEx;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static ZoneSystem;
using Paths = BepInEx.Paths;

namespace PlanBuildLocations
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.10.0")]
    //[NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "marcopogo.PlanBuildLocations";
        public const string PluginName = "PlanBuildLocations";
        public const string PluginVersion = "0.0.1";

        public static Plugin Instance;

        private Harmony Harmony;

        private Dictionary<string, BlueprintLocation> LocationBlueprints = new Dictionary<string, BlueprintLocation>();
        private static CustomRPC LocationsRPC;

        private void Awake()
        {
            Instance = this;

            // Init config
            PlanBuildLocations.Config.Init();

            // Load server locations in J?tunn on every ZoneManager.Awake
            ZoneManager.OnVanillaLocationsAvailable += LoadServerLocations;

            // RPC for initial location sync
            LocationsRPC = NetworkManager.Instance.AddRPC(
                "bplocations", null, ReceiveLocationsZPackage);

            SynchronizationManager.Instance.AddInitialSynchronization(LocationsRPC, CreateLocationsZPackage);

            // Harmony patch to unload all locations from J?tunn on ZNetScene.Shutdown
            Harmony = new Harmony(PluginGUID);
            Harmony.PatchAll(typeof(Patches));
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown)), HarmonyPostfix]
            private static void ZNetScene_Shutdown() => Instance.RemoveCustomLocations();

            [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.CreateLocationProxy)), HarmonyPrefix]
            private static bool ZoneSystem_CreateLocationProxy(ZoneLocation location)
                => Instance.PreventProxy(location);

            [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SpawnLocation)), HarmonyPostfix]
            private static void ZoneSystem_SpawnLocation(ZoneLocation location, Vector3 pos, Quaternion rot, SpawnMode mode)
                => Instance.ApplyTerrainModifier(location, pos, rot, mode);
        }

        private bool PreventProxy(ZoneLocation location)
        {
            if (LocationBlueprints.ContainsKey(location.m_prefabName))
            {
                return false;
            }

            return true;
        }

        private void ApplyTerrainModifier(ZoneLocation location, Vector3 pos, Quaternion rot, SpawnMode mode)
        {
            if (mode == SpawnMode.Client)
            {
                return;
            }

            if (!LocationBlueprints.TryGetValue(location.m_prefabName, out var blueprint))
            {
                return;
            }

            foreach (TerrainModEntry mod in blueprint.TerrainMods)
            {
                Dictionary<TerrainComp, Indices> indices = new Dictionary<TerrainComp, Indices>();
                Vector3 finalPostition = pos + rot * mod.GetPosition();
                Quaternion finalRotation = rot * mod.GetRotation();

                if (mod.shape.Equals("circle", StringComparison.OrdinalIgnoreCase))
                {
                    indices = TerrainTools.GetCompilerIndicesWithCircle(finalPostition, mod.radius * 2,
                        BlockCheck.Off);
                }
                if (mod.shape.Equals("square", StringComparison.OrdinalIgnoreCase))
                {
                    indices = TerrainTools.GetCompilerIndicesWithRect(finalPostition, mod.radius * 2,
                        mod.radius * 2,
                        finalRotation.eulerAngles.y * Mathf.PI / 180f, BlockCheck.Off);
                }
                TerrainTools.ResetTerrain(indices, finalPostition, mod.radius);
                TerrainTools.LevelTerrain(indices, finalPostition, mod.radius, mod.smooth, finalPostition.y);
                if (!string.IsNullOrEmpty(mod.paint))
                {
                    TerrainTools.PaintTerrain(indices, finalPostition, mod.radius,
                        (TerrainModifier.PaintType)Enum.Parse(typeof(TerrainModifier.PaintType), mod.paint));
                }
            }
        }

        /// <summary>
        ///     Load locations via filesystem per world on a server
        /// </summary>
        private void LoadServerLocations()
        {
            if (!ZNet.instance.IsServer())
            {
                return;
            }

            try
            {
                Jotunn.Logger.LogInfo($"Loading location blueprints for world {ZNet.m_world.m_name}");

                // Get location files for the current world from the config directory
                if (!Directory.Exists(PlanBuildLocations.Config.LocationDirectoryConfig.Value))
                {
                    Directory.CreateDirectory(PlanBuildLocations.Config.LocationDirectoryConfig.Value);
                }

                List<string> blueprintFiles = new List<string>();

                string worldLocationsDirectory = $"{PlanBuildLocations.Config.LocationDirectoryConfig.Value}/{ZNet.m_world.m_name}";

                if (!Directory.Exists(worldLocationsDirectory))
                {
                    return;
                }

                blueprintFiles.AddRange(Directory.EnumerateFiles(worldLocationsDirectory, "*.bplocation"));
                blueprintFiles = blueprintFiles.Select(absolute => absolute.Replace(Paths.BepInExRootPath, null)).ToList();

                // Try to load blueprint locations
                foreach (var relativeFilePath in blueprintFiles)
                {
                    try
                    {
                        Jotunn.Logger.LogDebug($"Loading blueprint location {relativeFilePath}");

                        BlueprintLocation bp = BlueprintLocation.FromFile(relativeFilePath);
                        bp.ID = $"bplocation:{bp.ID}";
                        if (LocationBlueprints.ContainsKey(bp.ID))
                        {
                            throw new Exception($"Blueprint location ID {bp.ID} already exists");
                        }
                        LocationBlueprints.Add(bp.ID, bp);
                    }
                    catch (Exception ex)
                    {
                        Jotunn.Logger.LogWarning($"Could not load blueprint location {relativeFilePath}: {ex}");
                    }
                }

                // Create blueprint locations
                foreach (var bp in LocationBlueprints.Values)
                {
                    try
                    {
                        bp.CreateLocation();
                        var location = new CustomLocation(bp.LocationPrefab, false, bp.LocationConfig);
                        ZoneManager.Instance.AddCustomLocation(location);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error caught while instantiating location {bp.ID}: {ex}");
                    }
                }

            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Exception caught while adding custom locations: {ex}");
            }
        }

        private ZPackage CreateLocationsZPackage(ZNetPeer peer)
        {
            Jotunn.Logger.LogDebug($"Sending {LocationBlueprints.Count} blueprint locations to peer #{peer.m_uid}");

            ZPackage newPackage = new ZPackage();

            newPackage.Write(LocationBlueprints.Count);
            foreach (var entry in LocationBlueprints)
            {
                Jotunn.Logger.LogDebug(entry.Key);
                newPackage.Write(entry.Value.ToZPackage());
            }

            return newPackage;
        }

        private IEnumerator ReceiveLocationsZPackage(long sender, ZPackage package)
        {
            Jotunn.Logger.LogMessage($"Received blueprint locations");

            LocationBlueprints.Clear();

            // Deserialize package
            var numBlueprints = package.ReadInt();
            while (numBlueprints > 0)
            {
                BlueprintLocation bp = BlueprintLocation.FromZPackage(package.ReadPackage());
                LocationBlueprints.Add(bp.ID, bp);
                numBlueprints--;

                Jotunn.Logger.LogDebug(bp.ID);
            }

            // Create blueprint locations
            foreach (var bp in LocationBlueprints.Values)
            {
                bp.CreateLocation();
                var location = new CustomLocation(bp.LocationPrefab, false, bp.LocationConfig);
                ZoneManager.Instance.AddCustomLocation(location);
                ZoneManager.Instance.RegisterLocationInZoneSystem(location.ZoneLocation);
            }

            yield break;
        }

        private void RemoveCustomLocations()
        {
            Jotunn.Logger.LogInfo("Removing location blueprints");

            foreach (var location in LocationBlueprints)
            {
                ZoneManager.Instance.DestroyCustomLocation(location.Key);
            }
            LocationBlueprints.Clear();
        }
    }
}