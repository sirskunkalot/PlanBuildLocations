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

        private Dictionary<string, BlueprintLocation> LocationBlueprints;

        private void Awake()
        {
            Instance = this;

            // Init config
            PlanBuildLocations.Config.Init();

            // Load locations on Jötunn event
            LocationBlueprints = new Dictionary<string, BlueprintLocation>();
            ZoneManager.OnVanillaLocationsAvailable += ZoneManager_OnVanillaLocationsAvailable;
        }

        private void ZoneManager_OnVanillaLocationsAvailable()
        {
            Jotunn.Logger.LogInfo("Loading location blueprints");
            
            if (!Directory.Exists(PlanBuildLocations.Config.LocationDirectoryConfig.Value))
            {
                Directory.CreateDirectory(PlanBuildLocations.Config.LocationDirectoryConfig.Value);
            }

            string worldLocationsDirectory = $"{PlanBuildLocations.Config.LocationDirectoryConfig.Value}/{ZNet.m_world.m_name}";

            if (!Directory.Exists(worldLocationsDirectory))
            {
                return;
            }
            
            List<string> blueprintFiles = new List<string>();
            blueprintFiles.AddRange(Directory.EnumerateFiles(worldLocationsDirectory, "*.bplocation"));
            blueprintFiles = blueprintFiles.Select(absolute => absolute.Replace(BepInEx.Paths.BepInExRootPath, null)).ToList();

            // Try to load all saved blueprint locations
            foreach (var relativeFilePath in blueprintFiles)
            {
                try
                {
                    BlueprintLocation bp = BlueprintLocation.FromFile(relativeFilePath);
                    if (LocationBlueprints.ContainsKey(bp.ID))
                    {
                        throw new Exception($"Blueprint location ID {bp.ID} already exists");
                    }
                    LocationBlueprints.Add(bp.ID, bp);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not load blueprint {relativeFilePath}: {ex}");
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
    }
}