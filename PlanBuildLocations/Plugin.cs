// PlanBuildLocations
// a Valheim mod for using PlanBuild blueprints as locations
// 
// File:    Plugin.cs
// Project: PlanBuildLocations

using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using HarmonyLib;

namespace PlanBuildLocations
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "marcopogo.PlanBuildLocations";
        public const string PluginName = "PlanBuildLocations";
        public const string PluginVersion = "0.0.1";

        public static Plugin Instance;
        
        private Harmony Harmony;

        private Dictionary<string, BlueprintLocation> LocationBlueprints;

        private void Awake()
        {
            Instance = this;

            // Init config
            PlanBuildLocations.Config.Init();

            // Load locations in Jötunn on every ZoneManager.Awake
            LocationBlueprints = new Dictionary<string, BlueprintLocation>();
            ZoneManager.OnVanillaLocationsAvailable += AddCustomLocations;

            // Harmony patch to unload all locations from Jötunn on ZNetScene.Shutdown
            Harmony = new Harmony(PluginGUID);
            Harmony.PatchAll(typeof(Patches));
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown)), HarmonyPostfix]
            private static void ZNetScene_Shutdown() => Instance.RemoveCustomLocations();
        }

        private void AddCustomLocations()
        {
            try
            {
                if (!Directory.Exists(PlanBuildLocations.Config.LocationDirectoryConfig.Value))
                {
                    Directory.CreateDirectory(PlanBuildLocations.Config.LocationDirectoryConfig.Value);
                }
                
                List<string> blueprintFiles = new List<string>();

                // Load locations per world on a server
                if (ZNet.instance.IsServer())
                {
                    Jotunn.Logger.LogWarning($"Loading location blueprints for world {ZNet.m_world.m_name}");

                    string worldLocationsDirectory = $"{PlanBuildLocations.Config.LocationDirectoryConfig.Value}/{ZNet.m_world.m_name}";

                    if (!Directory.Exists(worldLocationsDirectory))
                    {
                        return;
                    }
            
                    blueprintFiles.AddRange(Directory.EnumerateFiles(worldLocationsDirectory, "*.bplocation"));
                    blueprintFiles = blueprintFiles.Select(absolute => absolute.Replace(Paths.BepInExRootPath, null)).ToList();
                }
                // Load all locations on a client
                else
                {
                    Jotunn.Logger.LogWarning("Loading location blueprints in client mode");

                    blueprintFiles.AddRange(Directory.EnumerateFiles(PlanBuildLocations.Config.LocationDirectoryConfig.Value, "*.bplocation", SearchOption.AllDirectories));
                    blueprintFiles = blueprintFiles.Select(absolute => absolute.Replace(Paths.BepInExRootPath, null)).ToList();
                }

                // Try to load blueprint locations
                foreach (var relativeFilePath in blueprintFiles)
                {
                    try
                    {
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
                    var prefab = bp.CreateLocation();
                    var config = new LocationConfig
                    {
                        Biome = Heightmap.Biome.Meadows,
                        Quantity = 100
                    };
                    var location = new CustomLocation(prefab, false, config);
                    ZoneManager.Instance.AddCustomLocation(location);
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Exception caught while adding custom locations: {ex}");
            }
        }

        private void RemoveCustomLocations()
        {
            Jotunn.Logger.LogWarning("Removing location blueprints");

            foreach (var location in LocationBlueprints)
            {
                ZoneManager.Instance.DestroyCustomLocation(location.Key);
            }
            LocationBlueprints.Clear();
        }
    }
}