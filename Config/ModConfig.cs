using System;
using System.IO;
using MelonLoader;
using Newtonsoft.Json;

namespace PoliceExpansionMod.Config
{
    [Serializable]
    public class ModConfig
    {
        private static readonly string ConfigPath =
            Path.Combine("Mods", "PoliceExpansionMod", "config.json");

        // Difficulty preset
        public DifficultyPreset Preset { get; set; } = DifficultyPreset.Normal;

        // ── Patrol ──────────────────────────────────────────────
        [JsonProperty] public float PatrolDensity         { get; set; } = 1.0f;   // 0.1–3.0
        [JsonProperty] public float OfficerAggression     { get; set; } = 1.0f;   // 0.1–3.0
        [JsonProperty] public float CheckpointFrequency   { get; set; } = 1.0f;
        [JsonProperty] public bool  EnableCurfewPatrols   { get; set; } = true;

        // ── Raids ────────────────────────────────────────────────
        [JsonProperty] public float RaidFrequency         { get; set; } = 1.0f;
        [JsonProperty] public float RaidWarnTime          { get; set; } = 30f;    // seconds

        // ── Undercover ───────────────────────────────────────────
        [JsonProperty] public float UndercoverChance      { get; set; } = 0.15f;  // 0–1

        // ── Evidence & Investigation ─────────────────────────────
        [JsonProperty] public float EvidenceGrowthRate    { get; set; } = 1.0f;
        [JsonProperty] public float InvestigationSpeed    { get; set; } = 1.0f;

        // ── Heat ─────────────────────────────────────────────────
        [JsonProperty] public float HeatDecaySpeed        { get; set; } = 1.0f;

        // ── Bribery ──────────────────────────────────────────────
        [JsonProperty] public float BriberySuccessMultiplier { get; set; } = 1.0f;

        // ── Informants ───────────────────────────────────────────
        [JsonProperty] public float InformantChance       { get; set; } = 0.1f;

        // ────────────────────────────────────────────────────────
        public static ModConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<ModConfig>(json) ?? Default();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[PEM] Config load failed: {e.Message}. Using defaults.");
            }

            var cfg = Default();
            cfg.Save();
            return cfg;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[PEM] Config save failed: {e.Message}");
            }
        }

        public void ApplyPreset(DifficultyPreset preset)
        {
            Preset = preset;
            switch (preset)
            {
                case DifficultyPreset.Casual:
                    PatrolDensity = 0.5f; OfficerAggression = 0.5f;
                    RaidFrequency = 0.4f; UndercoverChance = 0.05f;
                    HeatDecaySpeed = 2.0f; BriberySuccessMultiplier = 1.8f;
                    InformantChance = 0.05f; EvidenceGrowthRate = 0.5f;
                    break;

                case DifficultyPreset.Normal:
                    ApplyDefaults(); break;

                case DifficultyPreset.Hardcore:
                    PatrolDensity = 2.0f; OfficerAggression = 2.5f;
                    RaidFrequency = 2.0f; UndercoverChance = 0.35f;
                    HeatDecaySpeed = 0.4f; BriberySuccessMultiplier = 0.4f;
                    InformantChance = 0.25f; EvidenceGrowthRate = 2.5f;
                    break;
            }
            Save();
        }

        private void ApplyDefaults()
        {
            PatrolDensity = 1f; OfficerAggression = 1f;
            RaidFrequency = 1f; UndercoverChance = 0.15f;
            HeatDecaySpeed = 1f; BriberySuccessMultiplier = 1f;
            InformantChance = 0.1f; EvidenceGrowthRate = 1f;
        }

        private static ModConfig Default()
        {
            var c = new ModConfig();
            c.ApplyDefaults();
            return c;
        }
    }

    public enum DifficultyPreset { Casual, Normal, Hardcore, Custom }
}
