# Building from source

This repository contains the mod's C# source, isolated logic tests and DLL compilation script. The separate model-authoring projects and the full asset/release pipeline are not included. Compiling the DLL does **not** create an installable mod package: the matching release's `Assets/rsn_core_windows` bundle is also required.

## Requirements

- Windows and PowerShell 5.1 or newer.
- Unity Editor **6000.0.75f1**, including its bundled C# compiler and .NET Framework reference assemblies. The commands below do not open the Unity Editor or launch Valheim.
- To build the plugin: your local Valheim installation and a BepInEx profile containing Jötunn. The current source targets Valheim **1.0.15**, BepInEx **5.4.23.5** and Jötunn **2.30.2**.

Game and dependency DLLs are local compilation references. They are not stored in this repository or copied to the output.

## Compile the DLL

Run from the repository root, replacing the example paths with your own:

```powershell
.\tools\Compile.ps1 `
  -EditorData 'C:\Path\To\Unity\6000.0.75f1\Editor\Data' `
  -ValheimManaged 'C:\Path\To\Valheim\valheim_Data\Managed' `
  -BepInExPath 'C:\Path\To\Profile\BepInEx'
```

The script expects Jötunn at `plugins\ValheimModding-Jotunn\Jotunn.dll` inside the supplied BepInEx folder. Output: `artifacts\compile\RunicStorageNetwork.dll`. Use `-Output` to select another directory.

For local testing, use the asset bundle from the release matching the source version. Future source changes to assets may require a new matching release bundle. Do not replace an installed mod while the game is running.

## Run isolated logic tests

These tests require only the Unity compiler/reference assemblies and Windows, without game or mod DLLs:

```powershell
.\tools\Compile.ps1 -Tests `
  -EditorData 'C:\Path\To\Unity\6000.0.75f1\Editor\Data' `
  -Output '.\artifacts\tests'
```

They cover resource planning, network graphs, reservations, recovery, localization, resource counts and the container and build-tool allow/deny rules. They do not simulate Valheim networking, Harmony patches or the game UI; multiplayer changes also need in-game testing.

## Save local paths

Optionally create `.local\BuildPaths.psd1`:

```powershell
@{
  EditorData = 'C:\Path\To\Unity\6000.0.75f1\Editor\Data'
  ValheimManaged = 'C:\Path\To\Valheim\valheim_Data\Managed'
  BepInExPath = 'C:\Path\To\Profile\BepInEx'
}
```

The script loads these defaults; explicit parameters take priority. The `.local` directory is ignored by Git.

`RunicStorageNetwork.csproj` is available for IDE use. Supply `EditorData`, `ValheimManaged` and `BepInExPath` as MSBuild properties or in an ignored `.local\Build.props` file. The tested compilation path is `tools\Compile.ps1`.

## Repository contents

- `src/`: plugin and shared logic.
- `tests/`: isolated logic tests.
- `tools/Compile.ps1`: DLL and test compilation.
- `README.md`: player documentation in English and Russian.
- `CHANGELOG_EN.md`: English release notes used in mod packages.
- `CHANGELOG.md`: Russian release notes.
- `.github/ISSUE_TEMPLATE/`: English and Russian bug report forms.

The root `.gitignore` allows only the public source and documentation paths. Build output, logs, local configuration, game references, Unity caches and authoring notes stay outside Git. Add new public paths explicitly when needed.

To publish a prepared package through GitHub Actions, see [PUBLISHING.md](PUBLISHING.md).
