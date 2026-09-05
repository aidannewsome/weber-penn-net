using System;
using System.Collections.Generic;

namespace WeberPenn
{
	/// <summary>A point or direction. The tree grows along +Z, as in the paper.</summary>
	public readonly struct Vector3
	{
		public readonly double X;
		public readonly double Y;
		public readonly double Z;

		public Vector3(double x, double y, double z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static Vector3 Zero => new(0, 0, 0);
		public static Vector3 UnitX => new(1, 0, 0);
		public static Vector3 UnitY => new(0, 1, 0);
		public static Vector3 UnitZ => new(0, 0, 1);

		public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

		public Vector3 Normalized()
		{
			double l = Length;
			return l > 0 ? this / l : this;
		}

		public static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

		public static Vector3 Cross(Vector3 a, Vector3 b) =>
			new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

		public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);
		public static Vector3 operator *(Vector3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
		public static Vector3 operator *(double s, Vector3 a) => new(a.X * s, a.Y * s, a.Z * s);
		public static Vector3 operator /(Vector3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);

		public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
	}

	/// <summary>A texture coordinate.</summary>
	public readonly struct Vector2
	{
		public readonly double X;
		public readonly double Y;

		public Vector2(double x, double y)
		{
			X = x;
			Y = y;
		}

		public override string ToString() => $"({X:0.###}, {Y:0.###})";
	}

	/// <summary>
	/// A stem's local coordinate system: an origin and three unit axes. The paper puts each
	/// stem's Z along its axis, with the trunk's Z up and its X parallel to the ground, and
	/// describes every bend as a rotation of this frame. Rotations here follow that reading:
	/// a local rotation turns the axes about one of themselves, a world rotation turns them
	/// about a fixed direction, and the origin stays where it is either way.
	/// </summary>
	public readonly struct Frame
	{
		public readonly Vector3 Origin;
		public readonly Vector3 X;
		public readonly Vector3 Y;
		public readonly Vector3 Z;

		public Frame(Vector3 origin, Vector3 x, Vector3 y, Vector3 z)
		{
			Origin = origin;
			X = x;
			Y = y;
			Z = z;
		}

		public static Frame Identity => new(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

		/// <summary>A local point taken into the world.</summary>
		public Vector3 Apply(Vector3 local) => Origin + Direction(local);

		/// <summary>A local direction taken into the world.</summary>
		public Vector3 Direction(Vector3 local) => X * local.X + Y * local.Y + Z * local.Z;

		public Frame Translate(Vector3 by) => new(Origin + by, X, Y, Z);

		public Frame At(Vector3 origin) => new(origin, X, Y, Z);

		/// <summary>Rotation about the local X axis, the paper's curvature rotation.</summary>
		public Frame RotX(double degrees) => RotLocal(Vector3.UnitX, degrees);

		/// <summary>Rotation about the local Y axis.</summary>
		public Frame RotY(double degrees) => RotLocal(Vector3.UnitY, degrees);

		/// <summary>Rotation about the local Z axis, the paper's rotation of a child round its parent.</summary>
		public Frame RotZ(double degrees) => RotLocal(Vector3.UnitZ, degrees);

		/// <summary>
		/// A child's direction: turned about the parent's Z by its rotation angle, then tilted
		/// away from the parent's Z by its down angle.
		/// </summary>
		public Frame RotXZ(double downDegrees, double rotateDegrees) => RotZ(rotateDegrees).RotX(downDegrees);

		/// <summary>
		/// Rotation away from the local Z by an angle, about a local axis lying in the XY plane
		/// at the given bearing. This is how the random curvature variation is applied.
		/// </summary>
		public Frame RotAwayFromZ(double degrees, double bearingDegrees)
		{
			double b = bearingDegrees * Math.PI / 180;
			return RotLocal(new Vector3(Math.Cos(b), Math.Sin(b), 0), degrees);
		}

		/// <summary>Rotation of the axes about a local unit axis by Rodrigues' formula.</summary>
		public Frame RotLocal(Vector3 axis, double degrees)
		{
			double t = degrees * Math.PI / 180;
			double c = Math.Cos(t), s = Math.Sin(t);
			Vector3 ex = Column(axis, Vector3.UnitX, c, s);
			Vector3 ey = Column(axis, Vector3.UnitY, c, s);
			Vector3 ez = Column(axis, Vector3.UnitZ, c, s);
			return new Frame(Origin, Direction(ex), Direction(ey), Direction(ez));
		}

		/// <summary>Rotation of the axes about a fixed world direction, the tree's Z for split spreading.</summary>
		public Frame RotWorld(Vector3 axis, double degrees)
		{
			double t = degrees * Math.PI / 180;
			double c = Math.Cos(t), s = Math.Sin(t);
			Vector3 k = axis.Normalized();
			return new Frame(Origin, Rodrigues(k, X, c, s), Rodrigues(k, Y, c, s), Rodrigues(k, Z, c, s));
		}

		static Vector3 Column(Vector3 axis, Vector3 e, double c, double s) => Rodrigues(axis, e, c, s);

		static Vector3 Rodrigues(Vector3 k, Vector3 v, double c, double s) =>
			v * c + Vector3.Cross(k, v) * s + k * (Vector3.Dot(k, v) * (1 - c));
	}

	/// <summary>
	/// The paper's RANDOM: uniform numbers from a seed. A struct, so a copy is a saved state
	/// and assigning it back restores one, which pruning needs to re-grow a stem the same way
	/// at a shorter length. Any generator would do; this one is SplitMix64, chosen for being
	/// a few lines and the same on every platform.
	/// </summary>
	public struct Rng
	{
		ulong state;

		public Rng(ulong seed)
		{
			state = seed;
		}

		ulong NextRaw()
		{
			state += 0x9E3779B97F4A7C15UL;
			ulong z = state;
			z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
			z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
			return z ^ (z >> 31);
		}

		/// <summary>A number in [0, 1).</summary>
		public double Next() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);

		/// <summary>The paper's ± variation: a number in [-variation, +variation].</summary>
		public double Var(double variation) => (Next() * 2 - 1) * variation;

		/// <summary>A seed for a child generator, so every stem draws its own sequence.</summary>
		public ulong NextSeed() => NextRaw();
	}

	/// <summary>
	/// One level of recursion's parameters: the paper's nLength, nCurveRes and the rest, with
	/// the n dropped because the level is the index into <see cref="Parameters.Level"/>. Level 0
	/// is the trunk, 1 the main branches, 2 the secondary branches, 3 the tertiary. Anything
	/// deeper uses level 3's.
	/// </summary>
	public sealed class Level
	{
		/// <summary>nLength: a stem's length as a fraction of its parent's, or of the tree's scale for the trunk.</summary>
		public double Length { get; set; } = 1;
		/// <summary>nLengthV: random variation of Length.</summary>
		public double LengthV { get; set; }
		/// <summary>nTaper: 0 an untapered cylinder, 1 a cone, 2 a spherical end, 3 periodic (concatenated spheres); fractions in between.</summary>
		public double Taper { get; set; } = 1;
		/// <summary>nSegSplits: clones added per segment, 1 a dichotomous split at every segment, 2 ternary; fractions are spread evenly.</summary>
		public double SegSplits { get; set; }
		/// <summary>nSplitAngle: the angle a stem and its clones turn away from the previous segment, less the declination.</summary>
		public double SplitAngle { get; set; }
		/// <summary>nSplitAngleV: random variation of SplitAngle.</summary>
		public double SplitAngleV { get; set; }
		/// <summary>nCurveRes: the segments a stem is drawn in.</summary>
		public int CurveRes { get; set; } = 1;
		/// <summary>nCurve: the whole stem's bend, spread over its segments; over the first half when CurveBack is set.</summary>
		public double Curve { get; set; }
		/// <summary>nCurveBack: the bend of the second half of the stem, for S shapes.</summary>
		public double CurveBack { get; set; }
		/// <summary>nCurveV: random bend per stem, spread over its segments; negative makes the stem a helix with this declination.</summary>
		public double CurveV { get; set; }
		/// <summary>nDownAngle: a child's angle from its parent's axis.</summary>
		public double DownAngle { get; set; }
		/// <summary>nDownAngleV: random variation of DownAngle; negative varies it along the parent instead, downward near the base and upward near the top.</summary>
		public double DownAngleV { get; set; }
		/// <summary>nRotate: each child's rotation round the parent relative to the previous child; negative alternates sides for planar stems.</summary>
		public double Rotate { get; set; }
		/// <summary>nRotateV: random variation of Rotate.</summary>
		public double RotateV { get; set; }
		/// <summary>nBranches: the most children a stem at the level above can carry.</summary>
		public int Branches { get; set; }
	}

	/// <summary>
	/// The paper's parameter list, appendix and Figure 1, under the paper's names. Where a
	/// name in the paper begins with a level digit it lives in <see cref="Level"/>; the three
	/// trunk-only ones (0Scale, 0ScaleV, 0BaseSplits) cannot begin with a digit in C#, so
	/// they are TrunkScale, TrunkScaleV and BaseSplits. ZScale and ZScaleV appear in the
	/// appendix table but in none of the equations, and are not here.
	/// </summary>
	public sealed class Parameters
	{
		/// <summary>Shape: the crown's shape ratio curve, 0 conical, 1 spherical, 2 hemispherical, 3 cylindrical, 4 tapered cylindrical, 5 flame, 6 inverse conical, 7 tend flame, 8 the pruning envelope.</summary>
		public int Shape { get; set; } = 1;
		/// <summary>BaseSize: the fraction of the trunk with no branches.</summary>
		public double BaseSize { get; set; } = 0.3;
		/// <summary>Scale: the tree's size in metres; the trunk's length is 0Length times this.</summary>
		public double Scale { get; set; } = 13;
		/// <summary>ScaleV: random variation of Scale.</summary>
		public double ScaleV { get; set; }
		/// <summary>Levels: levels of recursion, usually 3 or 4; leaves come after the last.</summary>
		public int Levels { get; set; } = 3;
		/// <summary>Ratio: the trunk's radius as a fraction of its length.</summary>
		public double Ratio { get; set; } = 0.015;
		/// <summary>RatioPower: how fast children thin relative to their parents.</summary>
		public double RatioPower { get; set; } = 1.2;
		/// <summary>Lobes: sinusoidal lobes round the trunk's cross section; odd numbers look best.</summary>
		public int Lobes { get; set; }
		/// <summary>LobeDepth: the lobes' depth as a fraction of the radius.</summary>
		public double LobeDepth { get; set; }
		/// <summary>Flare: exponential widening at the base of the trunk.</summary>
		public double Flare { get; set; }
		/// <summary>0Scale: an extra scaling of the trunk's radius.</summary>
		public double TrunkScale { get; set; } = 1;
		/// <summary>0ScaleV: random variation of 0Scale.</summary>
		public double TrunkScaleV { get; set; }
		/// <summary>0BaseSplits: splits at the end of the trunk's first segment, for multiple trunks.</summary>
		public int BaseSplits { get; set; }
		/// <summary>The per-level parameters; index 0 is the trunk.</summary>
		public Level[] Level { get; set; } = [new Level(), new Level(), new Level(), new Level()];
		/// <summary>Leaves: leaves per parent stem at the last level; negative fans them from the stem's end.</summary>
		public int Leaves { get; set; }
		/// <summary>LeafShape: an index into a list of leaf outlines, kept on the leaf for a mesher to interpret.</summary>
		public int LeafShape { get; set; }
		/// <summary>LeafScale: a leaf's length in metres.</summary>
		public double LeafScale { get; set; } = 0.17;
		/// <summary>LeafScaleX: a leaf's width relative to its length.</summary>
		public double LeafScaleX { get; set; } = 1;
		/// <summary>The paper's bend: how far each leaf turns to face outward and upward, 0 to 1.</summary>
		public double LeafBend { get; set; }
		/// <summary>AttractionUp: how strongly branches from level 2 curve toward the sky; negative droops them.</summary>
		public double AttractionUp { get; set; }
		/// <summary>PruneRatio: how much of the pruning to apply, 0 none, 1 all.</summary>
		public double PruneRatio { get; set; }
		/// <summary>PruneWidth: the envelope's greatest width as a fraction of the scale.</summary>
		public double PruneWidth { get; set; } = 0.5;
		/// <summary>PruneWidthPeak: where up the envelope the greatest width is, 0 to 1.</summary>
		public double PruneWidthPeak { get; set; } = 0.5;
		/// <summary>PrunePowerLow: the envelope's curvature below the peak; 1 straight, less than 1 convex, more concave.</summary>
		public double PrunePowerLow { get; set; } = 0.5;
		/// <summary>PrunePowerHigh: the envelope's curvature above the peak.</summary>
		public double PrunePowerHigh { get; set; } = 0.5;
		/// <summary>The paper's quality factor for leaves, usually 1; fewer, larger leaves below it.</summary>
		public double Quality { get; set; } = 1;

		/// <summary>The parameters for a level; levels past 3 use level 3's.</summary>
		public Level LevelAt(int level) => Level[Math.Min(level, Level.Length - 1)];

		/// <summary>
		/// The paper's ShapeRatio(shape, ratio): how long a branch is relative to its longest,
		/// by its position up the crown from 0 at the top to 1 at the base of the crown.
		/// </summary>
		public double ShapeRatio(int shape, double ratio)
		{
			switch (shape)
			{
				case 0: return 0.2 + 0.8 * ratio;
				case 1: return 0.2 + 0.8 * Math.Sin(Math.PI * ratio);
				case 2: return 0.2 + 0.8 * Math.Sin(0.5 * Math.PI * ratio);
				case 3: return 1.0;
				case 4: return 0.5 + 0.5 * ratio;
				case 5: return ratio <= 0.7 ? ratio / 0.7 : (1 - ratio) / 0.3;
				case 6: return 1 - 0.8 * ratio;
				case 7: return ratio <= 0.7 ? 0.5 + 0.5 * ratio / 0.7 : 0.5 + 0.5 * (1 - ratio) / 0.3;
				case 8:
					if (ratio < 0 || ratio > 1) return 0;
					return ratio < 1 - PruneWidthPeak
						? Math.Pow(ratio / (1 - PruneWidthPeak), PrunePowerHigh)
						: Math.Pow((1 - ratio) / (1 - PruneWidthPeak), PrunePowerLow);
				default: throw new ArgumentOutOfRangeException(nameof(shape), "Shape is 0 to 8.");
			}
		}
	}

	/// <summary>A cross section of a stem: where it is, which way it faces, how thick it is.</summary>
	public readonly struct Section
	{
		/// <summary>The stem's frame here; Origin is the section's centre, Z the stem's direction.</summary>
		public readonly Frame Frame;
		/// <summary>The radius here, before lobes.</summary>
		public readonly double Radius;
		/// <summary>Distance along the stem from its base.</summary>
		public readonly double Distance;

		public Section(Frame frame, double radius, double distance)
		{
			Frame = frame;
			Radius = radius;
			Distance = distance;
		}

		public Vector3 Position => Frame.Origin;
	}

	/// <summary>A leaf: a frame whose origin is the leaf's base and whose Z runs to its tip, with the blade in the XZ plane facing Y.</summary>
	public readonly struct Leaf
	{
		public readonly Frame Frame;
		/// <summary>Along Z, in metres.</summary>
		public readonly double Length;
		/// <summary>Along X, in metres.</summary>
		public readonly double Width;
		/// <summary>The LeafShape index, for a mesher to pick an outline.</summary>
		public readonly int Shape;

		public Leaf(Frame frame, double length, double width, int shape)
		{
			Frame = frame;
			Length = length;
			Width = width;
			Shape = shape;
		}
	}

	/// <summary>
	/// A stem, the paper's word for the trunk or any branch: a run of sections from base to
	/// tip, the children that grow from it, the clones it split into, and its leaves if it
	/// is at the last level. A clone begins at the split and carries only the sections past it.
	/// </summary>
	public sealed class Stem
	{
		public int Level { get; internal set; }
		public Stem Parent { get; internal set; }
		/// <summary>True when this stem is a clone made by a split of another stem at the same level.</summary>
		public bool IsClone { get; internal set; }
		/// <summary>Distance along the parent at which this stem grows, in metres.</summary>
		public double Offset { get; internal set; }
		/// <summary>The stem's full length, in metres; for a clone, the length of the stem it split from.</summary>
		public double Length { get; internal set; }
		/// <summary>The radius at the base, before taper and flare.</summary>
		public double BaseRadius { get; internal set; }
		public IReadOnlyList<Section> Sections => sections;
		public IReadOnlyList<Stem> Children => children;
		public IReadOnlyList<Stem> Clones => clones;
		public IReadOnlyList<Leaf> Leaves => leaves;

		internal readonly List<Section> sections = [];
		internal readonly List<Stem> children = [];
		internal readonly List<Stem> clones = [];
		internal readonly List<Leaf> leaves = [];

		internal Parameters Parameters;

		/// <summary>The paper's radius at a distance along the stem: taper, then flare on the trunk.</summary>
		public double RadiusAt(double distance)
		{
			Level level = Parameters.LevelAt(Level);
			double z = Math.Min(Math.Max(distance / Length, 0), 1);
			double taper = level.Taper;
			double unitTaper = taper < 1 ? taper : taper < 2 ? 2 - taper : 0;
			double taperZ = BaseRadius * (1 - unitTaper * z);
			double radius;
			if (taper < 1)
			{
				radius = taperZ;
			}
			else
			{
				double z2 = (1 - z) * Length;
				double depth = taper < 2 || z2 < taperZ ? 1 : taper - 2;
				double z3 = taper < 2 ? z2 : Math.Abs(z2 - 2 * taperZ * (int)(z2 / (2 * taperZ) + 0.5));
				radius = taper < 2 && z3 >= taperZ
					? taperZ
					: (1 - depth) * taperZ + depth * Math.Sqrt(Math.Max(0, taperZ * taperZ - (z3 - taperZ) * (z3 - taperZ)));
			}
			if (Level == 0 && Parameters.Flare != 0)
			{
				double y = Math.Max(0, 1 - 8 * z);
				radius *= Parameters.Flare * (Math.Pow(100, y) - 1) / 100 + 1;
			}
			return radius;
		}

		/// <summary>The paper's lobing: the trunk's radius scaled round its circumference.</summary>
		public double LobeAt(double angleRadians) =>
			Level == 0 && Parameters.Lobes > 0 ? 1 + Parameters.LobeDepth * Math.Sin(Parameters.Lobes * angleRadians) : 1;
	}

	/// <summary>
	/// A tree grown from a <see cref="Parameters"/> and a seed: the trunk with everything on it,
	/// and the same stems and leaves flattened for reading straight through. Z is up and the
	/// units are metres. The same parameters and seed always give the same tree.
	/// </summary>
	public sealed class Tree
	{
		public Parameters Parameters { get; }
		public int Seed { get; }
		/// <summary>The paper's scale_tree: Scale with its random variation applied.</summary>
		public double Scale { get; }
		public Stem Trunk { get; }
		/// <summary>Every stem including clones, trunk first, each stem before its clones and children.</summary>
		public IReadOnlyList<Stem> Stems => stems;
		public IReadOnlyList<Leaf> Leaves => leaves;
		public double Height { get; private set; }
		/// <summary>The greatest distance of any section from the trunk's axis.</summary>
		public double Width { get; private set; }

		readonly List<Stem> stems = [];
		readonly List<Leaf> leaves = [];

		internal Tree(Parameters p, int seed, double scale, Stem trunk)
		{
			Parameters = p;
			Seed = seed;
			Scale = scale;
			Trunk = trunk;
			if (trunk != null) Collect(trunk);
		}

		public static Tree Grow(Parameters p, int seed) => Grower.Grow(p, seed);

		void Collect(Stem stem)
		{
			stems.Add(stem);
			foreach (Section s in stem.Sections)
			{
				Vector3 q = s.Position;
				if (q.Z > Height) Height = q.Z;
				double r = Math.Sqrt(q.X * q.X + q.Y * q.Y);
				if (r > Width) Width = r;
			}
			leaves.AddRange(stem.leaves);
			foreach (Stem c in stem.Clones) Collect(c);
			foreach (Stem c in stem.Children) Collect(c);
		}
	}

	/// <summary>
	/// Section 4 of the paper, Tree Creation: the curved stem, stem splits, stem children,
	/// stem radius, leaves, pruning and vertical attraction, in that order. Where the paper
	/// leaves a step unsaid the choice is noted at the step, and the README lists them.
	/// </summary>
	internal sealed class Grower
	{
		readonly Parameters p;
		readonly double scale;
		double trunkScale;
		// The paper's error values for spreading fractional counts evenly: one per level for
		// splits and children, one for leaves.
		readonly double[] splitError = new double[4];
		readonly double[] childError = new double[4];
		double leafError;

		Grower(Parameters p, double scale)
		{
			this.p = p;
			this.scale = scale;
		}

		/// <summary>The state of one stem while it is being drawn; a clone starts from a copy.</summary>
		sealed class Building
		{
			public Stem Stem;
			public Level Lp;
			public int SegCount;
			public double SegLen;
			public double SplitCorrection;
			public double ChildrenPerSegment;
			public double LeavesPerSegment;
			public double ChildLengthMax;
			public double RotAngle;
			public int Side = 1;
			public bool HasCloned;
			public bool PruneTest;
			public Rng Rng;

			public Building CloneFor(Stem clone, ulong seed) => new()
			{
				Stem = clone,
				Lp = Lp,
				SegCount = SegCount,
				SegLen = SegLen,
				SplitCorrection = SplitCorrection,
				ChildrenPerSegment = ChildrenPerSegment,
				LeavesPerSegment = LeavesPerSegment,
				ChildLengthMax = ChildLengthMax,
				RotAngle = RotAngle + 180,
				Side = Side,
				HasCloned = true,
				PruneTest = PruneTest,
				Rng = new Rng(seed),
			};
		}

		public static Tree Grow(Parameters p, int seed)
		{
			var rng = new Rng((ulong)seed);
			double scale = p.Scale + rng.Var(p.ScaleV);
			var grower = new Grower(p, scale);
			Stem trunk = grower.MakeStem(0, Frame.Identity, null, 0, rng.NextSeed(), 0);
			return new Tree(p, seed, scale, trunk);
		}

		Stem MakeStem(int level, Frame frame, Stem parent, double offset, ulong seed, double parentChildLengthMax)
		{
			var stem = new Stem { Level = level, Parent = parent, Offset = offset, Parameters = p };
			var b = new Building { Stem = stem, Lp = p.LevelAt(level), Rng = new Rng(seed) };
			b.SegCount = Math.Max(1, b.Lp.CurveRes);

			// Section 4.3: the length of the trunk, of a main branch by the shape ratio, and of
			// anything deeper by its position along its parent.
			if (level == 0)
			{
				stem.Length = (b.Lp.Length + b.Rng.Var(b.Lp.LengthV)) * scale;
				trunkScale = p.TrunkScale + b.Rng.Var(p.TrunkScaleV);
			}
			else if (level == 1)
			{
				double baseLength = p.BaseSize * scale;
				double ratio = (parent.Length - offset) / (parent.Length - baseLength);
				stem.Length = parent.Length * parentChildLengthMax * p.ShapeRatio(p.Shape, ratio);
			}
			else
			{
				stem.Length = parentChildLengthMax * (parent.Length - 0.6 * offset);
			}
			if (stem.Length <= 0) return null;
			b.SegLen = stem.Length / b.SegCount;

			// Section 4.4: the trunk's radius from its length, a child's from its parent's, and
			// never thicker than the parent is where the child grows.
			stem.BaseRadius = BaseRadius(stem);
			if (stem.BaseRadius <= 0) return null;

			// Section 4.6: a stem that would leave the envelope is regrown shorter.
			if (level > 0 && p.PruneRatio > 0) Prune(b, frame);

			// Section 4.3: how many children this stem carries.
			Level next = p.LevelAt(level + 1);
			b.ChildLengthMax = next.Length + b.Rng.Var(next.LengthV);
			if (level < p.Levels - 1)
			{
				double count;
				if (level == 0) count = next.Branches;
				else if (level == 1) count = (int)(next.Branches * (0.2 + 0.8 * (stem.Length / parent.Length) / parentChildLengthMax));
				else count = (int)(next.Branches * (1.0 - 0.5 * offset / parent.Length));
				b.ChildrenPerSegment = level == 0 ? count / b.SegCount / (1 - p.BaseSize) : count / b.SegCount;
			}
			// Section 4.5: how many leaves, by the stem's position along its parent.
			if (level == p.Levels - 1 && p.Leaves != 0)
			{
				// A lone stem, Levels of 1, takes its leaves as a stem at its parent's top would.
				double ratio = parent == null ? 1 : offset / parent.Length;
				b.LeavesPerSegment = Math.Abs(p.Leaves) * p.ShapeRatio(4, ratio) * p.Quality / b.SegCount;
			}

			MakeSegments(b, 0, frame);
			return stem;
		}

		double BaseRadius(Stem stem)
		{
			if (stem.Level == 0)
			{
				// 0Scale ± 0ScaleV, drawn once per tree; the paper applies it to the trunk's
				// radius without saying whether per tree or per cross section.
				return stem.Length * p.Ratio * trunkScale;
			}
			double radius = stem.Parent.BaseRadius * Math.Pow(stem.Length / stem.Parent.Length, p.RatioPower);
			return Math.Min(radius, stem.Parent.RadiusAt(stem.Offset));
		}

		/// <summary>
		/// Draws segments start to the end, placing children, leaves and clones as it goes.
		/// In a pruning test, returns the first segment whose end leaves the envelope, else -1.
		/// </summary>
		int MakeSegments(Building b, int start, Frame frame)
		{
			Stem stem = b.Stem;
			for (int s = start; s < b.SegCount; s++)
			{
				if (s > 0) frame = NewDirection(b, frame, s);
				double d0 = s * b.SegLen;
				if (s == start) stem.sections.Add(new Section(frame, stem.RadiusAt(d0), d0));
				if (!b.PruneTest)
				{
					if (stem.Level < p.Levels - 1) MakeChildren(b, frame, s);
					if (b.LeavesPerSegment > 0) MakeLeaves(b, frame, s);
				}
				if (b.Lp.CurveV < 0) AddHelix(b, frame, s);
				Frame end = frame.Translate(frame.Z * b.SegLen);
				stem.sections.Add(new Section(end, stem.RadiusAt(d0 + b.SegLen), d0 + b.SegLen));
				frame = end;
				if (b.PruneTest && !Inside(end.Origin)) return s;
				if (s < b.SegCount - 1)
				{
					int outside = MakeClones(b, ref frame, s);
					if (outside >= 0) return outside;
				}
			}
			return -1;
		}

		/// <summary>Section 4.1: the segment's turn, the random turn, and section 4.8's pull toward the sky.</summary>
		Frame NewDirection(Building b, Frame frame, int s)
		{
			Level lp = b.Lp;
			double delta = lp.CurveBack == 0
				? lp.Curve / lp.CurveRes
				: s < (lp.CurveRes + 1) / 2 ? lp.Curve * 2 / lp.CurveRes : lp.CurveBack * 2 / lp.CurveRes;
			delta += b.SplitCorrection;
			frame = frame.RotX(delta);
			// The paper adds a random rotation of magnitude nCurveV/nCurveRes without naming its
			// axis; it is taken as a tilt away from the stem's axis in a random direction.
			if (lp.CurveV > 0) frame = frame.RotAwayFromZ(b.Rng.Var(lp.CurveV) / lp.CurveRes, 180 + b.Rng.Var(180));
			if (p.AttractionUp != 0 && b.Stem.Level >= 2)
			{
				// The paper resolves the pull onto the local X axis through cos(orientation); it is
				// applied here directly about the horizontal axis that turns the stem upward,
				// which is the same rotation without the projection.
				double declination = Math.Acos(Math.Max(-1, Math.Min(1, frame.Z.Z)));
				double curveUp = p.AttractionUp * declination / lp.CurveRes * 180 / Math.PI;
				var axis = new Vector3(-frame.Z.Y, frame.Z.X, 0);
				if (axis.Length > 1e-9) frame = frame.RotWorld(axis, -curveUp);
			}
			return frame;
		}

		/// <summary>
		/// Section 4.1: a negative nCurveV makes each segment a helix whose tangent leans from
		/// the axis by that declination. One turn per segment, back on the axis at the end.
		/// </summary>
		void AddHelix(Building b, Frame frame, int s)
		{
			const int Steps = 10;
			for (int i = 1; i < Steps; i++)
			{
				double where = (double)i / Steps;
				double distance = (s + where) * b.SegLen;
				b.Stem.sections.Add(new Section(frame.At(PointInSegment(b, frame, where)), b.Stem.RadiusAt(distance), distance));
			}
		}

		/// <summary>A point a fraction of the way along a segment, on the helix when the stem is helical.</summary>
		Vector3 PointInSegment(Building b, Frame frame, double where)
		{
			if (b.Lp.CurveV >= 0) return frame.Apply(new Vector3(0, 0, where * b.SegLen));
			double lean = Math.Abs(b.Lp.CurveV) * Math.PI / 180;
			double radius = Math.Tan(lean) * b.SegLen / (2 * Math.PI);
			double a = 2 * Math.PI * where;
			return frame.Apply(new Vector3(radius * Math.Cos(a) - radius, radius * Math.Sin(a), where * b.SegLen));
		}

		/// <summary>Section 4.3: children spaced along the segment, none on the trunk's bare base.</summary>
		void MakeChildren(Building b, Frame frame, int s)
		{
			Stem stem = b.Stem;
			double perSegment = b.ChildrenPerSegment;
			double offs = 0;
			if (stem.Level == 0)
			{
				double baseLength = p.BaseSize * scale;
				double segStart = s * b.SegLen;
				if (segStart + b.SegLen <= baseLength) return;
				if (segStart < baseLength)
				{
					offs = (baseLength - segStart) / b.SegLen;
					perSegment *= 1 - offs;
				}
			}
			else if (s == 0)
			{
				// The first child clears the parent's thickness; the paper does not say where
				// along a branch its children begin.
				offs = Math.Min(1, stem.Parent.RadiusAt(stem.Offset) / b.SegLen);
			}
			int count = Round(perSegment, ref childError[Math.Min(stem.Level, 3)]);
			if (count <= 0) return;
			double dist = (1 - offs) / count;
			for (int k = 0; k < count; k++)
			{
				double where = offs + dist / 2 + k * dist;
				double childOffset = (s + where) * b.SegLen;
				Frame direction = ChildDirection(b, frame, childOffset).At(PointInSegment(b, frame, where));
				Stem child = MakeStem(stem.Level + 1, direction, stem, childOffset, b.Rng.NextSeed(), b.ChildLengthMax);
				if (child != null) stem.children.Add(child);
			}
		}

		/// <summary>Section 4.3: a child's rotation round its parent and its down angle.</summary>
		Frame ChildDirection(Building b, Frame frame, double offset)
		{
			Stem stem = b.Stem;
			Level next = p.LevelAt(stem.Level + 1);
			double rotate;
			if (next.Rotate >= 0)
			{
				b.RotAngle = (b.RotAngle + next.Rotate + b.Rng.Var(next.RotateV) + 360) % 360;
				rotate = b.RotAngle;
			}
			else
			{
				b.Side = -b.Side;
				rotate = b.Side * (180 + next.Rotate + b.Rng.Var(next.RotateV));
			}
			double down;
			if (next.DownAngleV >= 0)
			{
				down = next.DownAngle + b.Rng.Var(next.DownAngleV);
			}
			else
			{
				// Negative nDownAngleV: the angle runs from the base of the parent to its top,
				// signed as the paper's example (the black tupelo) needs.
				double baseLength = stem.Level == 0 ? p.BaseSize * scale : 0;
				double ratio = (stem.Length - offset) / (stem.Length - baseLength);
				down = next.DownAngle + next.DownAngleV * (1 - 2 * p.ShapeRatio(0, ratio));
			}
			return frame.RotXZ(down, rotate);
		}

		/// <summary>Section 4.5: leaves along the last level's stems, or fanned from their ends when Leaves is negative.</summary>
		void MakeLeaves(Building b, Frame frame, int s)
		{
			Stem stem = b.Stem;
			double length = p.LeafScale / Math.Sqrt(p.Quality);
			double width = p.LeafScale * p.LeafScaleX / Math.Sqrt(p.Quality);
			if (p.Leaves > 0)
			{
				int count = Round(b.LeavesPerSegment, ref leafError);
				if (count <= 0) return;
				double offs = s == 0 && stem.Parent != null ? Math.Min(1, stem.Parent.RadiusAt(stem.Offset) / b.SegLen) : 0;
				double dist = (1 - offs) / count;
				for (int k = 0; k < count; k++)
				{
					double where = offs + dist / 2 + k * dist;
					Frame f = ChildDirection(b, frame, (s + where) * b.SegLen).At(PointInSegment(b, frame, where));
					stem.leaves.Add(new Leaf(Bend(f), length, width, p.LeafShape));
				}
			}
			else if (s == b.SegCount - 1)
			{
				Level next = p.LevelAt(stem.Level + 1);
				int count = (int)(b.LeavesPerSegment * b.SegCount + 0.5);
				if (count <= 0) return;
				Frame tip = frame.Translate(frame.Z * b.SegLen);
				double step = next.Rotate / count;
				double stepV = next.RotateV / count;
				double first;
				if (count % 2 == 1)
				{
					stem.leaves.Add(new Leaf(Bend(tip), length, width, p.LeafShape));
					first = step;
				}
				else
				{
					first = step / 2;
				}
				for (int k = 0; k < count / 2; k++)
				{
					for (int side = 1; side >= -1; side -= 2)
					{
						Frame f = tip.RotY(side * (first + k * step + b.Rng.Var(stepV))).RotX(next.DownAngle + b.Rng.Var(next.DownAngleV));
						stem.leaves.Add(new Leaf(Bend(f), length, width, p.LeafShape));
					}
				}
			}
		}

		/// <summary>Section 4.9: a leaf turned part way to face outward from the trunk, then upward.</summary>
		Frame Bend(Frame leaf)
		{
			if (p.LeafBend == 0) return leaf;
			Vector3 pos = leaf.Origin;
			Vector3 normal = leaf.Y;
			double thetaPosition = Math.Atan2(pos.Y, pos.X);
			double thetaBend = thetaPosition - Math.Atan2(normal.Y, normal.X);
			leaf = leaf.RotWorld(Vector3.UnitZ, p.LeafBend * thetaBend * 180 / Math.PI);
			normal = leaf.Y;
			double phiBend = Math.Atan2(Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y), normal.Z);
			return leaf.RotX(p.LeafBend * phiBend * 180 / Math.PI);
		}

		/// <summary>
		/// Section 4.2: the stem forks into clones at the end of a segment. All turn away by the
		/// split angle less the declination and turn back over the remaining segments; the
		/// clones are spread round the tree's axis. In a pruning test, returns the first segment
		/// of any clone that leaves the envelope, else -1.
		/// </summary>
		int MakeClones(Building b, ref Frame frame, int s)
		{
			Stem stem = b.Stem;
			Level lp = b.Lp;
			bool baseSplit = stem.Level == 0 && s == 0 && p.BaseSplits > 0;
			int splits;
			if (baseSplit)
			{
				splits = p.BaseSplits;
			}
			else
			{
				// A stem that has cloned, or is a clone, splits half as readily.
				double segSplits = lp.SegSplits * (stem.IsClone || b.HasCloned ? 0.5 : 1);
				splits = Round(segSplits, ref splitError[Math.Min(stem.Level, 3)]);
			}
			if (splits < 1) return -1;

			double declination = Math.Acos(Math.Max(-1, Math.Min(1, frame.Z.Z))) * 180 / Math.PI;
			double splitAngle = Math.Max(0, lp.SplitAngle + b.Rng.Var(lp.SplitAngleV) - declination);
			int remaining = b.SegCount - s - 1;
			b.SplitCorrection -= splitAngle / remaining;
			b.HasCloned = true;
			// The children are shared out among the stem and its clones, so nBranches still
			// counts the whole stem; the paper does not say.
			b.ChildrenPerSegment /= splits + 1;
			b.LeavesPerSegment /= splits + 1;

			for (int i = 1; i <= splits; i++)
			{
				var clone = new Stem
				{
					Level = stem.Level,
					Parent = stem.Parent,
					IsClone = true,
					Offset = stem.Offset,
					Length = stem.Length,
					BaseRadius = stem.BaseRadius,
					Parameters = p,
				};
				Building cb = b.CloneFor(clone, b.Rng.NextSeed());
				Frame f = frame.RotX(splitAngle);
				double diverge;
				if (baseSplit)
				{
					diverge = 360.0 / (splits + 1) * i + b.Rng.Var(lp.SplitAngleV);
				}
				else
				{
					double r = b.Rng.Next();
					diverge = 20 + 0.75 * (30 + Math.Abs(declination - 90)) * r * r;
					if (b.Rng.Next() < 0.5) diverge = -diverge;
				}
				f = f.RotWorld(Vector3.UnitZ, diverge);
				int outside = MakeSegments(cb, s + 1, f);
				if (outside >= 0) return outside;
				stem.clones.Add(clone);
			}
			// The original stem turns away too but is not spread; the paper spreads "a stem
			// and its clones" and this keeps one of them on course.
			frame = frame.RotX(splitAngle);
			return -1;
		}

		/// <summary>
		/// Section 4.6: grow the stem in test, and while it leaves the envelope shorten it and
		/// grow it again with the same random draws. PruneRatio then blends the pruned length
		/// with the original. The shortening steps are a choice: to the segment that left the
		/// envelope, by at most half and at least a fifteenth of the original at a time.
		/// </summary>
		void Prune(Building b, Frame frame)
		{
			Stem stem = b.Stem;
			double original = stem.Length;
			Rng savedRng = b.Rng;
			double savedCorrection = b.SplitCorrection;
			var savedSplitError = (double[])splitError.Clone();
			void Restore()
			{
				b.Rng = savedRng;
				b.SplitCorrection = savedCorrection;
				Array.Copy(savedSplitError, splitError, splitError.Length);
				b.HasCloned = false;
				stem.sections.Clear();
				stem.clones.Clear();
			}

			b.PruneTest = true;
			int outside = MakeSegments(b, 0, frame);
			while (outside >= 0 && stem.Length > 0.001 * scale)
			{
				Restore();
				double shortest = stem.Length / 2;
				double longest = stem.Length - original / 15;
				stem.Length = Math.Min(Math.Max(b.SegLen * outside, shortest), longest);
				b.SegLen = stem.Length / b.SegCount;
				stem.BaseRadius = BaseRadius(stem);
				outside = stem.Length > 0.001 * scale ? MakeSegments(b, 0, frame) : -1;
			}
			stem.Length = original - (original - stem.Length) * p.PruneRatio;
			b.SegLen = stem.Length / b.SegCount;
			stem.BaseRadius = BaseRadius(stem);
			Restore();
			b.PruneTest = false;
		}

		/// <summary>Section 4.6: whether a point is inside the pruning envelope.</summary>
		bool Inside(Vector3 point)
		{
			double r = Math.Sqrt(point.X * point.X + point.Y * point.Y);
			double ratio = (scale - point.Z) / (scale * (1 - p.BaseSize));
			return r / scale < p.PruneWidth * p.ShapeRatio(8, ratio);
		}

		/// <summary>The paper's error diffusion: a fractional count rounded so the fractions add up over time.</summary>
		static int Round(double value, ref double error)
		{
			int effective = (int)Math.Floor(value + error + 0.5);
			error -= effective - value;
			return effective;
		}
	}


	/// <summary>
	/// A plain triangle mesh of a tree: every stem as a tube through its sections, every leaf
	/// as a quad. Enough to look at the tree; a renderer wanting the paper's degradation at
	/// range, or its leaf outlines, builds its own from the stems and leaves.
	/// </summary>
	public sealed class Mesh
	{
		public IReadOnlyList<Vector3> Vertices => vertices;
		/// <summary>Triangles as three vertex indices, counter clockwise seen from outside.</summary>
		public IReadOnlyList<int[]> Faces => faces;
		/// <summary>
		/// Both in metres along the bark, V along the stem and U round it by the stem's base
		/// radius, so a texture tiles at its real size and every quad stays a rectangle; where
		/// the stem flares or tapers the picture stretches a little rather than skewing.
		/// </summary>
		public IReadOnlyList<Vector2> UV => uv;
		public IReadOnlyList<Vector3> LeafVertices => leafVertices;
		public IReadOnlyList<int[]> LeafFaces => leafFaces;
		public IReadOnlyList<Vector2> LeafUV => leafUV;

		readonly List<Vector3> vertices = [];
		readonly List<int[]> faces = [];
		readonly List<Vector2> uv = [];
		readonly List<Vector3> leafVertices = [];
		readonly List<int[]> leafFaces = [];
		readonly List<Vector2> leafUV = [];

		/// <summary>
		/// Meshes a tree with up to the given number of sides per ring, stems below the given
		/// level only when one is given. The trunk gets all the sides; a thinner stem gets
		/// fewer, keeping a ring's facets about as wide as the trunk's, never fewer than
		/// three, so the wood's triangles go where the eye can see them. The trunk's first
		/// segment gets extra rings so the flare shows, and helical stems already carry their
		/// own sections.
		/// </summary>
		public static Mesh Of(Tree tree, int sides = 8, int levels = int.MaxValue)
		{
			var mesh = new Mesh();
			double facet = tree.Trunk == null ? 0 : 2 * Math.PI * tree.Trunk.BaseRadius / sides;
			foreach (Stem stem in tree.Stems)
			{
				if (stem.Level >= levels) continue;
				int n = facet > 0 ? (int)Math.Ceiling(2 * Math.PI * stem.BaseRadius / facet) : sides;
				mesh.AddStem(stem, Math.Max(3, Math.Min(sides, n)));
			}
			foreach (Leaf leaf in tree.Leaves) mesh.AddLeaf(leaf);
			return mesh;
		}

		void AddStem(Stem stem, int sides)
		{
			var rings = new List<Section>();
			for (int i = 0; i < stem.Sections.Count; i++)
			{
				Section a = stem.Sections[i];
				if (i == 0 && stem.Level == 0 && !stem.IsClone && stem.Parameters.Flare != 0 && stem.Sections.Count > 1)
				{
					// Ring the flare at halving distances so its curve reads.
					Section b = stem.Sections[1];
					rings.Add(a);
					for (int k = 6; k >= 1; k--)
					{
						double t = 1.0 / Math.Pow(2, k);
						double distance = a.Distance + (b.Distance - a.Distance) * t;
						rings.Add(new Section(a.Frame.At(a.Position + (b.Position - a.Position) * t), stem.RadiusAt(distance), distance));
					}
					continue;
				}
				rings.Add(a);
			}
			int first = vertices.Count;
			foreach (Section ring in rings)
			{
				for (int j = 0; j <= sides; j++)
				{
					double angle = 2 * Math.PI * j / sides;
					double r = ring.Radius * stem.LobeAt(angle);
					vertices.Add(ring.Frame.Apply(new Vector3(Math.Cos(angle) * r, Math.Sin(angle) * r, 0)));
					uv.Add(new Vector2(angle * stem.BaseRadius, ring.Distance));
				}
			}
			int stride = sides + 1;
			for (int i = 0; i + 1 < rings.Count; i++)
			{
				for (int j = 0; j < sides; j++)
				{
					int a = first + i * stride + j;
					int b = a + 1;
					int c = a + stride;
					int d = c + 1;
					faces.Add([a, b, d]);
					faces.Add([a, d, c]);
				}
			}
		}

		void AddLeaf(Leaf leaf)
		{
			int first = leafVertices.Count;
			double w = leaf.Width / 2;
			leafVertices.Add(leaf.Frame.Apply(new Vector3(-w, 0, 0)));
			leafVertices.Add(leaf.Frame.Apply(new Vector3(w, 0, 0)));
			leafVertices.Add(leaf.Frame.Apply(new Vector3(w, 0, leaf.Length)));
			leafVertices.Add(leaf.Frame.Apply(new Vector3(-w, 0, leaf.Length)));
			leafUV.Add(new Vector2(0, 0));
			leafUV.Add(new Vector2(1, 0));
			leafUV.Add(new Vector2(1, 1));
			leafUV.Add(new Vector2(0, 1));
			leafFaces.Add([first, first + 1, first + 2]);
			leafFaces.Add([first, first + 2, first + 3]);
		}
	}
}
