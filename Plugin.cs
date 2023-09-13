using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace NightLight
{
    public enum BrightnessDirectionOption
        {
            Darken,
            Brighten
        }
    public enum Toggle
        {
            On = 1,
            Off = 0
        }

    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class NightLightPlugin : BaseUnityPlugin
    {
        internal const string ModName = "NightLight";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource NightLightLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        public void Awake()
        {
            ControlToggle = config("1 - General", "Control Night Brightness or Darkness", Toggle.On,
                "If on, the mod will control the brightness or darkness at night.");
            BrightnessDirection = config("1 - General", "Brightness Direction", BrightnessDirectionOption.Darken,
                "Choose whether to make the game brighter or darker.");
            NightBrightnessMultiplier = config("1 - General", "Night Bright or Dark Multiplier", 0f,
                "Changes how bright or dark it looks at night. Try using 1.0, 2.0, or 3.0. Higher values will get very dark. 0 is default.");

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                NightLightLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                NightLightLogger.LogError($"There was an issue loading your {ConfigFileName}");
                NightLightLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        #region ConfigOptions

        public static ConfigEntry<Toggle> ControlToggle = null!;
        public static ConfigEntry<BrightnessDirectionOption> BrightnessDirection = null!;
        public static ConfigEntry<float> NightBrightnessMultiplier = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            //var configEntry = Config.Bind(group, name, value, description);

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion
    }

    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
    static class EnvManSetEnvPatch
    {

        static void Prefix(EnvMan __instance, ref EnvSetup env)
        {
            if (NightLightPlugin.ControlToggle.Value == Toggle.On)
            {
                ApplyEnvModifier(env);
            }
        }

        private static void ApplyEnvModifier(EnvSetup env)
        {
            env.m_ambColorNight = ApplyBrightnessModifier(env.m_ambColorNight);
            env.m_fogColorNight = ApplyBrightnessModifier(env.m_fogColorNight);
            env.m_fogColorSunNight = ApplyBrightnessModifier(env.m_fogColorSunNight);
            env.m_sunColorNight = ApplyBrightnessModifier(env.m_sunColorNight);
        }

        private static Color ApplyBrightnessModifier(Color color)
        {  
            float brightnessMultiplier = NightLightPlugin.NightBrightnessMultiplier.Value;
            float scaleFunc = 1f;

            if (NightLightPlugin.BrightnessDirection.Value == BrightnessDirectionOption.Darken)
            {
                // Darken the colors
                scaleFunc = Mathf.Clamp01(brightnessMultiplier >= 0 ?
                    1f - (Mathf.Sqrt(brightnessMultiplier) * 1.069952679E-4f) :
                    1f + (Mathf.Sqrt(Mathf.Abs(brightnessMultiplier)) * 1.069952679E-4f));
            }
            else
            {
                // Brighten the colors
                scaleFunc = Mathf.Clamp01(brightnessMultiplier >= 0 ?
                    1f + (Mathf.Sqrt(brightnessMultiplier) * 1.069952679E-4f) :
                    1f - (Mathf.Sqrt(Mathf.Abs(brightnessMultiplier)) * 1.069952679E-4f));
            }

            Color.RGBToHSV(color, out float h, out float s, out float v);
            v *= scaleFunc;
            return Color.HSVToRGB(h, s, v);
        }

    }
}