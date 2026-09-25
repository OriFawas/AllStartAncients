# All Starting Bonuses

A mod for **Slay the Spire 2** that unlocks every available starter bonus from the current Ancient (Neow, etc.)

## Features

- **Full Option Pool**: Replaces the standard limited starter choices with each Ancient's complete `AllPossibleOptions` list.
- **Configurable Ancient Selection**: By default, only unlocks all options for **Neow**. You can easily configure it to unlock all Ancients (Darv, Pael, Nonupeipe, etc.) or select specific ones.

## Configuration

A configuration file (`AllStartingBonuses.config.json`) is located alongside the mod DLL (and will be automatically generated if missing):

```json
{
  "UnlockAll": false,
  "Ancients": {
    "Neow": true,
    "Darv": false,
    "Nonupeipe": false,
    "Orobas": false,
    "Pael": false,
    "Tanx": false,
    "Tezcatara": false,
    "Vakuu": false
  }
}
```

### Settings

- **`UnlockAll`** (`bool`, default: `false`): When `true`, overrides individual settings and unlocks all bonus options for every Ancient in the game.
- **`Ancients`** (`object`): Individual toggles for each Ancient in Slay the Spire 2.
  - By default, **`Neow`** is set to `true`, and all other Ancients (`Darv`, `Nonupeipe`, `Orobas`, `Pael`, `Tanx`, `Tezcatara`, `Vakuu`) are set to `false` (vanilla behavior).
  - Set any individual Ancient to `true` to unlock its entire starting bonus list.

## Installation

1. Download or compile `AllStartingBonuses.dll`.
2. Copy the following files into your game's `mods/` directory (e.g. `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\`):
   - `AllStartingBonuses.dll`
   - `AllStartingBonuses.json`
   - `AllStartingBonuses.config.json` *(optional, auto-generated if omitted)*

## Building from Source

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Slay the Spire 2 game installation

### Build Steps

Set `$env:STS2_DIR` (or environment variable `STS2_DIR`) to your game's data directory containing `sts2.dll` and `GodotSharp.dll`, then build:

```powershell
$env:STS2_DIR = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64"
dotnet restore
dotnet build -c Release
```

The compiled mod DLL will be located at:

```text
bin\Release\net9.0\AllStartingBonuses.dll
```

## License

MIT
