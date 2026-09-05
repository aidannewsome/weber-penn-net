# WeberPenn

[![NuGet](https://img.shields.io/nuget/v/WeberPenn)](https://www.nuget.org/packages/WeberPenn)

A C# port of the tree model in Weber and Penn's
[Creation and Rendering of Realistic Trees](https://dl.acm.org/doi/10.1145/218380.218427),
SIGGRAPH 1995, under the paper's parameter names.
Written from the paper, checked against [Arbaro](https://github.com/wdiestel/arbaro).
One file, no dependencies, .NET Standard 2.0.

## Use

```
dotnet add package WeberPenn
```

```csharp
var aspen = new WeberPenn.Parameters
{
    Shape = 7, BaseSize = 0.4, Scale = 13, ScaleV = 3, Levels = 3, Ratio = 0.015, RatioPower = 1.2,
    Lobes = 5, LobeDepth = 0.07, Flare = 0.6,
    Level = new[]
    {
        new WeberPenn.Level { Length = 1, CurveRes = 3, CurveV = 20 },
        new WeberPenn.Level { DownAngle = 60, DownAngleV = -50, Rotate = 140, Branches = 50, Length = 0.3, CurveRes = 5, Curve = -40, CurveV = 50 },
        new WeberPenn.Level { DownAngle = 45, DownAngleV = 10, Rotate = 140, Branches = 30, Length = 0.6, CurveRes = 3, Curve = -40, CurveV = 75 },
        new WeberPenn.Level { DownAngle = 45, DownAngleV = 10, Rotate = 77, Branches = 10 },
    },
    Leaves = 25, LeafScale = 0.17, AttractionUp = 0.5,
};
var tree = WeberPenn.Tree.Grow(aspen, seed: 13);

tree.Trunk;        // its Sections, Children, Clones and Leaves
tree.Stems;        // every stem, flattened
tree.Leaves;       // a frame, a length and a width each

var mesh = WeberPenn.Mesh.Of(tree, sides: 8);
mesh.Vertices; mesh.Faces; mesh.UV;
mesh.LeafVertices; mesh.LeafFaces; mesh.LeafUV;
```

Z is up, units are metres. The names are the paper's: 1DownAngle is `Level[1].DownAngle`;
0Scale, 0ScaleV and 0BaseSplits, which cannot begin with a digit, are `TrunkScale`,
`TrunkScaleV` and `BaseSplits`. Every parameter is documented in
[WeberPenn.cs](WeberPenn.cs). [presets.json](presets.json) holds the four species from the
paper's appendix, quaking aspen, black tupelo, weeping willow and California black oak, as
printed, to use and to start your own from.

## Ported

All of section 4: the curved stem, splits, children, radius, leaves, pruning, vertical
attraction and leaf bend. Not the wind sway or the degradation at range, which are about
drawing, nor the leaf outlines, kept as the LeafShape index. Where the paper leaves a step
unsaid the choice is noted in the code beside it.

With random variation off, the port and Arbaro agree on the trunk's radius to five figures
and on branch counts, lengths and radii per level to three. Where they differ on purpose,
the port follows the paper: a cloned stem halves its tendency to split, and the presets
are the appendix's numbers.

## Licence

MIT, see [LICENSE](LICENSE). Arbaro was run for its numbers, never read into the code.
