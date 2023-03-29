Component Attacher Search
=========================

A [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod for [Neos VR](https://neos.com/) that adds search functionality to the component picker.

Specific categories can be excluded by their path. By default this is the Actions, Cast, Math and Operators categories of LogiX, as they contain a lot of Types with very similar names.  
When searching within the categories, the search will work as usual. 

If you're writing a mod that adds Categories that you don't want to be searched by default, add the following code to your `OnEngineInit` method:
```CSharp
Engine.Current.OnReady += () =>
{
    if (ModLoader.Mods().FirstOrDefault(mod => mod.Name == "ComponentAttacherSearch") is NeosModBase searchMod
     && (searchMod.GetConfiguration()?.TryGetValue(new ModConfigurationKey<HashSet<string>>("ExcludedCategories"), out var excludedCategories) ?? false))
        excludedCategories.Add(CategoryPath);
};
```

## Installation
1. Install [NeosModLoader](https://github.com/zkxs/NeosModLoader).
2. Place [ComponentAttacherSearch.dll](https://github.com/Banane9/NeosComponentAttacherSearch/releases/latest/download/ComponentAttacherSearch.dll) into your `nml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\nml_mods` for a default install. You can create it if it's missing, or if you launch the game once with NeosModLoader installed it will create the folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Neos logs.
