using BepInEx.Configuration;

namespace PlanBuildLocations
{
    internal static class Config
    {
        private const string DirectorySection = "Directories";
        public static ConfigEntry<string> LocationDirectoryConfig;

        internal static void Init()
        {
            int order = 0;

            LocationDirectoryConfig = Plugin.Instance.Config.Bind(
                DirectorySection, "Locations directory", "BepInEx/config/PlanBuild/locations",
                new ConfigDescription("Directory to search for blueprint files to use as locations, relative paths are relative to the valheim.exe location", null,
                    new ConfigurationManagerAttributes { Order = --order }));

        }
    }
}
