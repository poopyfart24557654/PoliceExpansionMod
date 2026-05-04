# Police Expansion Mod — Setup Guide
### For Complete Beginners

---

## What You'll Need (install in this order)

1. **Schedule I** (Steam)
2. **.NET 6 SDK** → https://dotnet.microsoft.com/download/dotnet/6.0
3. **MelonLoader** → https://melonloader.xyz
   - Run the installer, point it at `Schedule I.exe`
   - Launch the game once so MelonLoader creates its folders, then close it
4. **Visual Studio 2022** (free Community edition) OR **Visual Studio Code**

---

## Step-by-Step: Build the DLL

### 1 — Open the project
- Unzip this folder somewhere (e.g. `Documents\PoliceExpansionMod`)
- Open `PoliceExpansionMod.csproj` in Visual Studio

### 2 — Fix the game path (important!)
Open `PoliceExpansionMod.csproj` and find this line:

```xml
<ScheduleIPath>C:\Program Files (x86)\Steam\steamapps\common\Schedule I</ScheduleIPath>
```

Change it to wherever Schedule I is installed on your PC.
- Not sure? Right-click Schedule I in Steam → Manage → Browse Local Files

### 3 — Build
- In Visual Studio: **Build → Build Solution** (or press `Ctrl+Shift+B`)
- If successful, the DLL will automatically copy itself to your Mods folder

### 4 — Launch the game
- Start Schedule I normally through Steam
- MelonLoader will load the mod automatically

### 5 — Verify it worked
- In the MelonLoader console (appears on game launch), look for:
  ```
  Police Expansion Mod v1.0.0 initializing...
  ```

---

## Configuration (Optional)

After running the game once, a config file is created at:
```
Schedule I\Mods\PoliceExpansionMod\config.json
```

Open it in Notepad to change difficulty settings. Available presets:
- `"Preset": "Casual"` — relaxed police, faster heat decay
- `"Preset": "Normal"` — balanced (default)
- `"Preset": "Hardcore"` — brutal enforcement

You can also adjust individual sliders like:
```json
"PatrolDensity": 1.5,
"BriberySuccessMultiplier": 0.5,
"UndercoverChance": 0.2
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Build fails: "Reference not found" | Double-check the `<ScheduleIPath>` in the .csproj file |
| Mod doesn't load | Make sure MelonLoader is installed and you launched the game once already |
| Game crashes on load | Check the MelonLoader log at `MelonLoader\Latest.log` |
| No console window | Enable it in MelonLoader settings |

---

## What the Mod Does (Quick Overview)

| System | What It Does |
|---|---|
| **Heat** | Tracks Global / District / Property heat. Rises from crimes, decays over time |
| **Patrols** | Officers dynamically respond to heat, curfew, and hotspots |
| **Escalation** | 6 tiers from local attention → full city crackdown |
| **Evidence** | Police build real cases — destroy evidence to fight back |
| **Raids** | Warrant-based raids with a warning window to react |
| **Informants** | Workers/customers can betray you based on loyalty |
| **Bribery** | Bribe officers — success depends on personality and district |
| **Undercover** | Fake buyers and sting operations — watch for suspicious behavior |

---

## Need Help?

If you get stuck, the MelonLoader Discord and NexusMods forums are the best places to ask.
Good luck — and stay clean. 👮
