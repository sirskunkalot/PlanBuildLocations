using Jotunn.Configs;
using Jotunn.Managers;
using PlanBuild.Blueprints;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace PlanBuildLocations
{
    internal class BlueprintLocation
    {
        private const string HeaderName = "#Name:";
        private const string HeaderCreator = "#Creator:";
        private const string HeaderDescription = "#Description:";
        private const string HeaderSnapPoints = "#SnapPoints";
        private const string HeaderTerrain = "#Terrain";
        private const string HeaderLocation = "#Location";
        private const string HeaderPieces = "#Pieces";

        public enum Format
        {
            BlueprintLocation
        }

        private enum ParserState
        {
            SnapPoints,
            Terrain,
            Location,
            Pieces
        }

        /// <summary>
        ///     ID of the blueprint instance.
        /// </summary>
        public string ID;

        /// <summary>
        ///     Name of the blueprint instance.
        /// </summary>
        public string Name;

        /// <summary>
        ///     Name of the player who created this blueprint.
        /// </summary>
        public string Creator;

        /// <summary>
        ///     Optional description for this blueprint
        /// </summary>
        public string Description = string.Empty;

        /// <summary>
        ///     Array of the <see cref="PieceEntry"/>s this blueprint is made of
        /// </summary>
        public PieceEntry[] PieceEntries;

        /// <summary>
        ///     Array of the <see cref="SnapPointEntry"/>s of this blueprint
        /// </summary>
        public SnapPointEntry[] SnapPoints;

        /// <summary>
        ///     Array of the <see cref="TerrainModEntry"/>s of this blueprint
        /// </summary>
        public TerrainModEntry[] TerrainMods;
        
        /// <summary>
        ///     Location configuration used by Jötunn
        /// </summary>
        public GameObject LocationPrefab;

        /// <summary>
        ///     Location configuration used by Jötunn
        /// </summary>
        public LocationConfig LocationConfig;

        /// <summary>
        ///     Create a blueprint instance from a file in the filesystem. Reads VBuild and Blueprint files.
        ///     Reads an optional thumbnail from a PNG file with the same name as the blueprint.
        /// </summary>
        /// <param name="fileLocation">Absolute path to the blueprint file</param>
        /// <returns><see cref="BlueprintLocation"/> instance with an optional thumbnail, ID equals file name</returns>
        public static BlueprintLocation FromFile(string fileLocation)
        {
            string filename = Path.GetFileNameWithoutExtension(fileLocation);
            string extension = Path.GetExtension(fileLocation).ToLowerInvariant();

            Format format;
            switch (extension)
            {
                case ".bplocation":
                    format = Format.BlueprintLocation;
                    break;

                default:
                    throw new Exception($"Format {extension} not recognized");
            }

            string[] lines = File.ReadAllLines(fileLocation);
            Logger.LogDebug($"Read {lines.Length} lines from {fileLocation}");

            BlueprintLocation ret = FromArray(filename, lines, format);

            return ret;
        }

        /// <summary>
        ///     Create a blueprint instance from a <see cref="ZPackage"/>.
        /// </summary>
        /// <param name="pkg"></param>
        /// <returns><see cref="BlueprintLocation"/> instance with an optional thumbnail, ID comes from the <see cref="ZPackage"/></returns>
        public static BlueprintLocation FromZPackage(ZPackage pkg)
        {
            string id = pkg.ReadString();
            BlueprintLocation bp = FromBlob(id, pkg.ReadByteArray());
            return bp;
        }

        /// <summary>
        ///     Create a blueprint instance with a given ID from a compressed BLOB.
        /// </summary>
        /// <param name="id">The unique blueprint ID</param>
        /// <param name="payload">BLOB with blueprint data</param>
        /// <returns><see cref="BlueprintLocation"/> instance with an optional thumbnail</returns>
        public static BlueprintLocation FromBlob(string id, byte[] payload)
        {
            BlueprintLocation ret;
            List<string> lines = new List<string>();
            using MemoryStream m = new MemoryStream(global::Utils.Decompress(payload));
            using (BinaryReader reader = new BinaryReader(m))
            {
                int numLines = reader.ReadInt32();
                for (int i = 0; i < numLines; i++)
                {
                    lines.Add(reader.ReadString());
                }
                ret = FromArray(id, lines.ToArray(), Format.BlueprintLocation);
            }

            return ret;
        }

        /// <summary>
        ///     Create a blueprint instance with a given ID from a string array holding blueprint information.
        /// </summary>
        /// <param name="id">The unique blueprint ID</param>
        /// <param name="lines">String array with either VBuild or Blueprint format information</param>
        /// <param name="format"><see cref="Format"/> of the blueprint lines</param>
        /// <returns><see cref="BlueprintLocation"/> instance built from the given lines without a thumbnail and the default filesystem paths</returns>
        public static BlueprintLocation FromArray(string id, string[] lines, Format format)
        {
            BlueprintLocation ret = new BlueprintLocation();
            ret.ID = id;

            List<PieceEntry> pieceEntries = new List<PieceEntry>();
            List<SnapPointEntry> snapPoints = new List<SnapPointEntry>();
            List<TerrainModEntry> terrainMods = new List<TerrainModEntry>();
            ret.LocationConfig = new LocationConfig();

            ParserState state = ParserState.Pieces;

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                if (line.StartsWith(HeaderName))
                {
                    ret.Name = line.Substring(HeaderName.Length);
                    continue;
                }
                if (line.StartsWith(HeaderCreator))
                {
                    ret.Creator = line.Substring(HeaderCreator.Length);
                    continue;
                }
                if (line.StartsWith(HeaderDescription))
                {
                    ret.Description = line.Substring(HeaderDescription.Length);
                    if (ret.Description.StartsWith("\""))
                    {
                        ret.Description = SimpleJson.SimpleJson.DeserializeObject<string>(ret.Description);
                    }
                    continue;
                }
                if (line == HeaderSnapPoints)
                {
                    state = ParserState.SnapPoints;
                    continue;
                }
                if (line == HeaderTerrain)
                {
                    state = ParserState.Terrain;
                    continue;
                }
                if (line == HeaderLocation)
                {
                    state = ParserState.Location;
                    continue;
                }
                if (line == HeaderPieces)
                {
                    state = ParserState.Pieces;
                    continue;
                }
                if (line.StartsWith("#"))
                {
                    continue;
                }
                switch (state)
                {
                    case ParserState.SnapPoints:
                        snapPoints.Add(new SnapPointEntry(line));
                        continue;
                    case ParserState.Terrain:
                        terrainMods.Add(new TerrainModEntry(line));
                        continue;
                    case ParserState.Location:
                        var split = line.Split(':');
                        var param = split[0].Trim().ToLowerInvariant();
                        var value = split[1].Trim();
                        switch (param)
                        {
                            case "biome":
                                ret.LocationConfig.Biome = (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), value);
                                break;
                            case "prioritized":
                                ret.LocationConfig.Priotized = bool.Parse(value);
                                break;
                            case "quantity":
                                ret.LocationConfig.Quantity = int.Parse(value);
                                break;
                            case "exteriorradius":
                                ret.LocationConfig.ExteriorRadius = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "centerfirst":
                                ret.LocationConfig.CenterFirst = bool.Parse(value);
                                break;
                            case "inforest":
                                ret.LocationConfig.InForest = bool.Parse(value);
                                break;
                            case "forestthresholdmin":
                                ret.LocationConfig.ForestTresholdMin = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "forestthresholdmax":
                                ret.LocationConfig.ForestTrasholdMax = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "unique":
                                ret.LocationConfig.Unique = bool.Parse(value);
                                break;
                            case "minaltitude":
                                ret.LocationConfig.MinAltitude = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "maxaltitude":
                                ret.LocationConfig.MaxAltitude = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "maxdistance":
                                ret.LocationConfig.MaxDistance = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "mindistance":
                                ret.LocationConfig.MinDistance = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "group":
                                ret.LocationConfig.Group = value;
                                break;
                            case "mindistancefromsimilar":
                                ret.LocationConfig.MinDistanceFromSimilar = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "minterraindelta":
                                ret.LocationConfig.MinTerrainDelta = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "maxterraindelta":
                                ret.LocationConfig.MaxTerrainDelta = float.Parse(value, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
                                break;
                            case "sloperotation":
                                ret.LocationConfig.SlopeRotation = bool.Parse(value);
                                break;
                            case "randomrotation":
                                ret.LocationConfig.RandomRotation = bool.Parse(value);
                                break;
                            case "snaptowater":
                                ret.LocationConfig.SnapToWater = bool.Parse(value);
                                break;
                            case "cleararea":
                                ret.LocationConfig.ClearArea = bool.Parse(value);
                                break;
                            default:
                                Logger.LogDebug($"Invalid location config {param}");
                                break;
                        }
                        continue;
                    case ParserState.Pieces:
                        switch (format)
                        {
                            case Format.BlueprintLocation:
                                pieceEntries.Add(PieceEntry.FromBlueprint(line));
                                break;
                        }
                        continue;
                }
            }

            if (string.IsNullOrEmpty(ret.Name))
            {
                ret.Name = ret.ID;
            }

            ret.SnapPoints = snapPoints.ToArray();
            ret.TerrainMods = terrainMods.ToArray();
            ret.PieceEntries = pieceEntries.ToArray();

            return ret;
        }

        /// <summary>
        ///     Creates a string array of this blueprint instance in format <see cref="Format.BlueprintLocation"/>.
        /// </summary>
        /// <returns>A string array representation of this blueprint without the thumbnail</returns>
        public string[] ToArray()
        {
            if (PieceEntries == null)
            {
                return null;
            }

            List<string> ret = new List<string>();

            ret.Add(HeaderName + Name);
            ret.Add(HeaderCreator + Creator);
            ret.Add(HeaderDescription + SimpleJson.SimpleJson.SerializeObject(Description));
            if (SnapPoints.Any())
            {
                ret.Add(HeaderSnapPoints);
                foreach (SnapPointEntry snapPoint in SnapPoints)
                {
                    ret.Add(snapPoint.line);
                }
            }
            if (TerrainMods.Any())
            {
                ret.Add(HeaderTerrain);
                foreach (TerrainModEntry terrainMod in TerrainMods)
                {
                    ret.Add(terrainMod.line);
                }
            }
            ret.Add(HeaderPieces);
            foreach (var piece in PieceEntries)
            {
                ret.Add(piece.line);
            }

            return ret.ToArray();
        }

        /// <summary>
        ///     Creates a compressed BLOB of this blueprint instance as <see cref="Format.BlueprintLocation"/>.
        /// </summary>
        /// <returns>A byte array representation of this blueprint including the thumbnail</returns>
        public byte[] ToBlob()
        {
            string[] lines = ToArray();
            if (lines == null || lines.Length == 0)
            {
                return null;
            }

            using MemoryStream m = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(m))
            {
                writer.Write(lines.Length);
                foreach (string line in lines)
                {
                    writer.Write(line);
                }
            }
            return global::Utils.Compress(m.ToArray());
        }

        /// <summary>
        ///     Creates a <see cref="ZPackage"/> from this blueprint including the ID and the instance.
        /// </summary>
        /// <returns></returns>
        public ZPackage ToZPackage()
        {
            ZPackage package = new ZPackage();
            package.Write(ID);
            package.Write(ToBlob());
            return package;
        }

        /// <summary>
        ///     Creates a location container of this blueprint. Can be used to add custom locations using Jötunn.
        /// </summary>
        public void CreateLocation()
        {
            if (LocationPrefab)
            {
                return;
            }

            Logger.LogDebug($"Creating location of blueprint {ID}");

            // Create location container
            LocationPrefab = ZoneManager.Instance.CreateLocationContainer(ID);

            // Create location pieces
            Transform tf = LocationPrefab.transform;

            foreach (SnapPointEntry snapPoint in SnapPoints)
            {
                GameObject snapPointObject = new GameObject
                {
                    name = "_snappoint",
                    layer = LayerMask.NameToLayer("piece"),
                    tag = "snappoint"
                };
                snapPointObject.SetActive(false);
                Object.Instantiate(snapPointObject, snapPoint.GetPosition(), Quaternion.identity, tf);
            }
            
            List<PieceEntry> pieces = new List<PieceEntry>(PieceEntries);
            Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            foreach (var pieceEntry in pieces.GroupBy(x => x.name).Select(x => x.FirstOrDefault()))
            {
                GameObject go = PrefabManager.Instance.GetPrefab(pieceEntry.name);
                if (!go)
                {
                    Logger.LogWarning($"No prefab found for {pieceEntry.name}! You are probably missing a dependency for blueprint {Name}");
                    continue;
                }
                prefabs.Add(pieceEntry.name, go);
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                PieceEntry pieceEntry = pieces[i];
                try
                {
                    if (prefabs.TryGetValue(pieceEntry.name, out var prefab))
                    {
                        var child = Object.Instantiate(prefab, pieceEntry.GetPosition(), pieceEntry.GetRotation(), tf);
                        child.transform.localScale = pieceEntry.GetScale();
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Error while creating location piece of line: {pieceEntry.line}\n{e}");
                }
            }
        }
    }
}