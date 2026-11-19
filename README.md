# bobby-oni-mod

Collection of [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mods by [bobby-modding](https://github.com/bobby-modding).

## Mods

| Mod | Description |
|---|---|
| [Material Search Overlay](./MaterialSearchOverlay) | Search materials by name and highlight their locations on the map |
| [No Limit Timer Sensor](./NoLimitTimerSensor) | Raises the Timer Sensor duration limits, with configurable maximum seconds and cycles |

More mods coming soon.

## Requirements

- .NET SDK (for building)
- [Refasmer](https://www.nuget.org/packages/Refasmer) + [AssemblyPublicizer](https://www.nuget.org/packages/AssemblyPublicizer) (restored via `dotnet tool restore`)
- An installed copy of the game (its `Assembly-CSharp.dll` under `OxygenNotIncluded_Data/Managed` is used as the reference assemblies; PLib and ILRepack are fetched from nuget.org during the first build)

## Build

```bash
dotnet tool restore                 # install build tools
dotnet build <ProjectName>          # compile, merge PLib via ILRepack, and auto-deploy to the dev folder
```

Set `GameLibsFolder` and `ModFolder` in `Directory.Build.props.user` (see `Directory.Build.props.default` for a template).

## Scaffold a new mod

```bash
dotnet new install ./MyOniModTemplate
dotnet new myonimodtemplate -n <Name> -o <Name>
```

## Repository structure

```
├── Directory.Build.props            # shared MSBuild properties (+ .user: machine-local, gitignored)
├── bobby-oni-mod.sln                # root solution
├── MaterialSearchOverlay/           # mod project (README, TESTING.md, translations/)
├── MyOniModTemplate/                # dotnet new template for scaffolding
└── NoLimitTimerSensor/              # mod project
```

Each mod project keeps its compiled binaries in `archived_versions/<game-build>/`
(e.g. `u60_763612`); builds archive into and then deploy from that folder, so the
dev-folder copy is always byte-identical to the archive.

## License

MIT
