# bobby-oni-mod

```bash
dotnet tool restore
```

```bash
dotnet tool install -g JetBrains.Refasmer.CliTool
```

```bash
refasmer -v -O ./Refs -c --all "/mnt/c/Program Files (x86)/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed/"*.dll
```

```bash
dotnet new install ./MyOniModTemplate
```

```bash
# Create the solution
dotnet new sln -n SuperDuperMod -o SuperDuperMod

# Generate the project using your new CLI template
dotnet new myonimodtemplate -n SuperDuperMod -o SuperDuperMod

# Add it to the solution
dotnet sln add SuperDuperMod/SuperDuperMod.csproj
```

```bash
dotnet new sln -n MaterialSearchOverlay -o MaterialSearchOverlay && dotnet new myonimodtemplate -n MaterialSearchOverlay -o MaterialSearchOverlay && dotnet sln add MaterialSearchOverlay/MaterialSearchOverlay.csproj
```

```bash
dotnet new sln -n SuperDuperMod -o SuperDuperMod && dotnet new myonimodtemplate -n SuperDuperMod -o SuperDuperMod && dotnet sln add SuperDuperMod/SuperDuperMod.csproj
```

```bash
dotnet build
```

```bash
cat "/mnt/c/Users/USER/AppData/LocalLow/Klei/Oxygen Not Included/Player.log" | grep SuperDuperMod
```

<!-- https://docs.docker.com/desktop/features/wsl/ -->
<!-- https://developer.valvesoftware.com/wiki/SteamCMD#Docker -->
```bash
docker run -it --rm --name=steamcmd_container -v $(pwd):/home/steam/steamcmd/modding cm2network/steamcmd bash
```

```bash
docker run -it --rm --name=steamcmd_container -v $(pwd):/home/steam/steamcmd/modding cm2network/steamcmd /home/steam/steamcmd/steamcmd.sh +login YOUR_STEAM_USERNAME +workshop_build_item /home/steam/steamcmd/modding/MyOniModTemplate/MyOniModTemplate.vdf +quit
```

```bash
docker run -it --rm --name=steamcmd_container \
  -v $(pwd):/home/steam/steamcmd/modding \
  -v $(pwd)/logs:/home/steam/steamcmd/workshopbuilds \
  -v "/mnt/c/Program Files (x86)/Steam/steamapps":/home/steam/Steam/steamapps \
  cm2network/steamcmd \
  /home/steam/steamcmd/steamcmd.sh +login YOUR_STEAM_USERNAME +app_update 457140 validate +workshop_build_item /home/steam/steamcmd/modding/SuperDuperMod.vdf +quit"
```

```bash
docker run -it --rm --name=steamcmd_container \
  -v $(pwd):/home/steam/steamcmd/modding \
  -v "/mnt/c/Program Files (x86)/Steam/steamapps":/home/steam/Steam/steamapps \
  cm2network/steamcmd \
  bash
```


if error find log in
```bash
cat ~/Steam/logs/workshop_log.txt
```


```bash
.\steamcmd
```

```bash
login YourSteamUsername
```

Then, input your password and Steam Guard code if you have 2FA enabled.


[02:48:15.395] [1] [INFO] [SuperDuperMod] OnLoad: Before patches!
[02:48:15.395] [1] [INFO] [SuperDuperMod] OnLoad: After patches!
```


https://forums.kleientertainment.com/forums/topic/116697-modding-guidelines/
https://forums.kleientertainment.com/forums/topic/115346-unofficial-modding-guide/
https://forums.kleientertainment.com/forums/topic/74765-creatingusing-translation-files-updated-august-22nd-2017/



# SteamCmd Integration
It is not implemented yet.
https://partner.steamgames.com/doc/features/workshop/implementation#SteamCmd

1. Go to SteamDB's page for Oxygen Not Included Depots.
https://steamdb.info/app/457140/depots/
2. Look at the list of Depots.
3. Compare it to a game that does support SteamCMD uploads, like Garry's Mod (AppID 4000).
https://steamdb.info/app/4000/depots/
4. If you look at Garry's Mod, you will see a specific depot named "Garry's Mod Workshop" (Depot ID 4006).
