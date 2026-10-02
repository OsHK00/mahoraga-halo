# Radiance Halo

Hollow Knight mod that replaces the Absolute Radiance halo with a custom PNG and
re-animates its rotation.

## Dependencies

* Satchel
* ModCommon

## Texture

You can replace the halo like this:

`halo.png` sits next to the DLL, in
`Hollow Knight_Data\Managed\Mods\RadianceHalo\`

If the PNG cannot be read, the mod keeps the original texture,
so the halo never disappears because of a bad file.

## Build

```powershell
dotnet build RadianceHalo.csproj
```

Settings are saved by the modding API to `RadianceHalo.GlobalSettings.json`.