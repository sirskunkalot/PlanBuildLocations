# PlanBuildLocations

A mod for using your blueprints created with PlanBuild as Locations created on world generation in Valheim.

## Usage
You will have to make a new folder at `<Valheim>\BepInEx\config\PlanBuild\locations` on your *game installation folder* (it doesn't work on the Thunderstore profile) named exactly as the world you want the locations to be generated to.

![LocationFolder](https://raw.githubusercontent.com/sirskunkalot/PlanBuildLocations/master/resources/locafolder.png)

Copy over your blueprint files found in `<Valheim>\BepInEx\config\PlanBuild\blueprints` that you want to be spawned into the world. Blueprint locations have their own file name extension: `.bplocation`. This is necessary so PlanBuild doesn't load them as blueprints.

![LocationFiles](https://raw.githubusercontent.com/sirskunkalot/PlanBuildLocations/master/resources/locafiles.png)

Create a new section in those files: `#Location` which holds all the properties you need to change location generation. You can use all properties which are used by the vanilla locations (see the Jötunn tutorial for all the properties: https://valheim-modding.github.io/Jotunn/tutorials/zones.html#locations-1).

Here is an example of the location section spawning a blueprint in the `Meadows` and `Plains`, trying to place a total of `200` instances, with a minimum altitude of `0` (above water), checking in a radius of `7m` and trying to clear that area of vegetation:
```
#Location
Biome:Meadows,Plains
Quantity:200
MinAltitude:0
ExteriorRadius:7
ClearArea:true
```

## Installing

It is recommended to use a mod manager to install PlanBuildLocations and all of its dependencies.

If you want to install it manually, load all of these mods as they are all required for PlanBuildLocations to function and install them according to their respective install instructions:

* [BepInExPack for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim)
* [Jötunn, the Valheim Library](https://valheim.thunderstore.io/package/ValheimModding/Jotunn)

Finally extract *all* of the contents of the PlanBuildLocations mod archive into ```<Valheim>\BepInEx\plugins\PlanBuildLocations```

## Credits

The original PlanBuild mod was created by __[MarcoPogo](https://github.com/MathiasDecrock)__ & __[Jules](https://github.com/sirskunkalot)__

This companion mod was coded by __[Jules](https://github.com/sirskunkalot)__

Made with Löve and __[Jötunn](https://github.com/Valheim-Modding/Jotunn)__

## Contact

Source available on GitHub: [https://github.com/sirskunkalot/PlanBuildLocations](https://github.com/sirskunkalot/PlanBuildLocations)﻿. All contributions welcome!

You can find me at the [Jötunn Discord](https://discord.gg/DdUt6g7gyA) (```Jules#7950```).