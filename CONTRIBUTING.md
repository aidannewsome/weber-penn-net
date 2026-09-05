# Contributing

## A species

[presets.json](presets.json) holds the paper's four species. Found a table for another
species that reads as that tree at a glance? Open an issue with the entry as it would
appear in the file, the seed you used, and a picture of the result beside a photo of the
species. Tables from Arbaro or Blender's Sapling are welcome if you say which and note
any numbers you changed.

## Code

The port stays faithful to the paper: the same table gives the paper's tree. A change to
the equations needs a page and equation of the paper it follows, or a step the paper
leaves unsaid, stated in the pull request; the README lists the steps chosen so far.

Build and check with:

```
dotnet build WeberPenn.csproj
dotnet format WeberPenn.csproj --verify-no-changes --severity info
```

A warning fails the build.
