# bobby-oni-mod

Collection of [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mods by [bobby-modding](https://github.com/bobby-modding).

## Mods

| Mod | Description |
|---|---|
| [Material Search Overlay](./MaterialSearchOverlay) | Search materials by name and highlight their locations on the map |

More mods coming soon.

## Requirements

- .NET SDK (for building)
- [Refasmer](https://www.nuget.org/packages/Refasmer) + [AssemblyPublicizer](https://www.nuget.org/packages/AssemblyPublicizer) (restored via `dotnet tool restore`)
- ONI development install with `Refs/` directory set up

## Build

```bash
dotnet tool restore                 # install build tools
dotnet build <ProjectName>          # compile and auto-deploy to dev folder
```

Set `GameLibsFolder` and `ModFolder` in `Directory.Build.props.user` (see `Directory.Build.props.default` for a template).

## Scaffold a new mod

```bash
dotnet new install ./MyOniModTemplate
dotnet new myonimodtemplate -n <Name> -o <Name>
```

## Repository structure

```
├── Directory.Build.props            # shared MSBuild properties
├── bobby-oni-mod.sln                # root solution
├── Refs/                            # stripped game reference DLLs
├── MaterialSearchOverlay/           # mod project
├── MyOniModTemplate/                # dotnet new template for scaffolding
└── AGENTS.md                        # development reference
```

## License

MIT
