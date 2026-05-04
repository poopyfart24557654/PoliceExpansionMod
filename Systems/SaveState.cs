using System;
using System.IO;
using MelonLoader;
using Newtonsoft.Json;
using PoliceExpansionMod.Core;

namespace PoliceExpansionMod.Systems
{
    [Serializable]
    public class SaveData
    {
        public float  GlobalHeat     { get; set; }
        public int    EscalationTier { get; set; }
        public string SavedAt        { get; set; } = DateTime.Now.ToString("o");
    }

    public static class SaveState
    {
        private static readonly string SavePath =
            Path.Combine("Mods", "PoliceExpansionMod", "save.json");

        public static void Save(PoliceExpansionMod.Core.PoliceExpansionMod mod)
        {
            try
            {
                var data = new SaveData {
                    GlobalHeat     = mod.HeatSystem?.GlobalHeat ?? 0f,
                    EscalationTier = mod.EscalationSystem?.CurrentTier ?? 0
                };
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented));
                MelonLogger.Msg("[SaveState] Saved.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[SaveState] Save failed: {e.Message}");
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                    return JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[SaveState] Load failed: {e.Message}");
            }
            return new SaveData();
        }
    }
}
