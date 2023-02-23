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

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            ControlBrightnessToggle = config("1 - General", "Control Brightness", Toggle.On,
                "If on, the mod will control the brightness at night.");
            NightBrightnessMultiplier = config("1 - General", "Night Brightness Multiplier", 0f,
                "Changes how bright it looks at night. A value between 5 and 10 will result in nearly double in brightness. 0 is default.");


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

        internal static ConfigEntry<Toggle> ControlBrightnessToggle = null!;
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
            if (NightLightPlugin.ControlBrightnessToggle.Value == NightLightPlugin.Toggle.On)
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
            float scaleFunc = Mathf.Clamp01(brightnessMultiplier >= 0 ?
                (Mathf.Sqrt(brightnessMultiplier) * 1.069952679E-4f) + 1f :
                1f - (Mathf.Sqrt(Mathf.Abs(brightnessMultiplier)) * 1.069952679E-4f));
            Color.RGBToHSV(color, out float h, out float s, out float v);
            v *= scaleFunc;
            return Color.HSVToRGB(h, s, v);
        }
    }
}