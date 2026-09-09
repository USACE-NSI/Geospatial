# Issues — living tracker

Single source of truth for known defects and open work.

Reviewed against `feature/remove-duplication` @ `7b4c6aa` (PR #8).

## How to use this file

- `P-xx` numbers are stable. Never reuse one. Do not renumber.
- Commit with `fix(P-xx)` in the message. The old `fix(#N)` markers in source refer to
  `CHANGES_08312026.md`'s numbering, **not** these numbers — see N-6.
- An item is closed only when a passing, un-skipped test guards the fix. Comments and
  commit messages are not evidence.
- Deletions are recorded inline against the item that asked for them, with the commit
  that performed them (`44fbf06`, PR #8 `051e8d8`, …). Do not delete the entry; mark it.
- Section 10 is the recommended work order, not a priority list.
- P-54, P-55, P-59 and P-20 are cited by other entries but are not defined here. See N-12
  before assuming a number was skipped by accident.

---

## 0. Standing decisions

| ID | Decision |
|---|---|
| D-A | **The R-tree stays.** It is foundational, supports bulk add, and outperforms RBush and other .NET implementations. Every R-tree item below is fix work; deletion and replacement are off the table. |
| D-B | Skipped tests assert behaviour the library *should* have and does not. They are deliberate and are the spec for P-03 and P-04. |
| D-C | Spike needed: confirm the GDAL 3.11.3 binding surface (`Layer.FieldIndex`, object overloads, `CoordinateTransformationOptions`). Gates P-06 and P-20. **The spike's artifact is gone:** `tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs` was deleted in PR #8 `051e8d8`. It was 100 % commented out and had never answered the question, so nothing was lost — but re-derive the surface against the installed binding rather than looking for that file. Recover the draft with `git show 051e8d8^:tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs`. |
| D-D | Decide the CRS token/authority model. `Reprojector.CrsToken` moved to `CoordinateTransformer.CrsToken` (private) in PR #8 `051e8d8`, and still prepends `"EPSG:"` to any non-empty `Projection.EpsgCode`, so `"ESRI:102003"` becomes `"EPSG:ESRI:102003"`. GDAL warns about this on every run (see the `EPSG:102003` line in test output). Gates P-14. |
| D-E | Decide whether the R-tree may index a geographic CRS. Its MBR math is planar; a degree-space box is not a metric box. Gates P-02 and P-47. |

---

## 1. P0 — wrong answers today

### P-03 `SphericalPointToSegmentDistance` cannot detect a foot behind `a`  *(skipped test)*
`deltaAT = acos(cos(d13)/cos(dXT))` is non-negative by construction, so only the far
side (`deltaAT > delta12`) is ever detected. A perpendicular foot behind `a` returns
the cross-track distance instead of `R * delta13`, understating the result — measured
1000.786 m where the truth is 1057.160 m.
**Fix:** reject when `cos(bearing(a,p) - bearing(a,b)) < 0` and return `R * delta13`.
**Guard:** un-skip `PointToSegmentWhenFootIsBehindTheNearEndpointReturnsDistanceToA`.

### P-04 `EarthRadiusFeet` is the wrong radius in the wrong unit  *(skipped test)*
Value `20925524.9` is the **equatorial** radius in feet (6378137 m), but the docstring
calls it "6,371,000 m in feet" and `SphericalArea`'s docstring tells callers to pass it
for square feet. Wrong scale *and* wrong unit — and `SphericalArea` squares it, so the
error is ~0.22% plus the unit factor.
This is also the entire subject of PR #6 ("right now it outputs meters instead of feet").
**Fix:** `EarthRadiusAuthalicFeet = EarthRadiusAuthalicMeters / MetersPerFoot`
(20,902,254.53) for areas, plus a mean-radius-in-feet for distances; rename the current
constant to `EarthRadiusEquatorialFeet` and forbid its use in area math.
**Guard:** un-skip `EarthRadiusFeetIsTheAuthalicRadiusInFeet`.

### P-05 Hole accounting — authoring path closed, read path and edge cases open
`IsHole` is now explicit everywhere (the reader sets `IsHole = r > 0`; helpers set
`IsHole = !exterior`), and the old `AddVertex` derivation is gone. Still open:
- `Feature.AreaSquareMeters` treats `Parts[0]` as exterior unconditionally and never
  checks `Parts[0].IsHole`. A feature built hole-first reports a **positive** area.
  One line: `if (Parts[0].IsHole) return null;` — or sum non-holes, subtract holes.
- `total -= Parts[i].AreaSquareMeters ?? 0` silently treats a null (unmeasurable) hole
  as zero area. `Part`'s docstring now warns callers about exactly this; the library's
  own caller does not heed it.
- **No test exercises the read path.** `IsHole = r > 0` has zero coverage. A reader
  change that dropped it would ship green and double-count every donut. See T-5.
- Multipolygon flattening restarts `r` per sub-polygon, so a two-part multipolygon
  marks hole 1 of each — verify that is intended.

### P-06 Writer field typing  *(needs D-C)*
`SpatialWriter` does not type fields to match `AttributeColumn`, and carries a comment
claiming "no object overload, and no `Layer.FieldIndex` in 3.11.3" which contradicts
the fix prescribed in the 0901 changelog. Resolve D-C, then fix or document.
Related, and blocking a correct fix: the `FieldType`↔`OGR`↔`CLR` mapping is spread over
four hand-maintained tables that nothing keeps in agreement —
`SpatialReader.MapFieldType`, `SpatialWriter.MapFieldType`, `SpatialReader.ReadFieldValue`
(which decides that `Double|Float|Numeric|Single` are doubles) and
`AttributeColumn.FieldTypeToType`. Group both directions of each pair in one internal
mapper so a divergence is visible in the diff.

### P-08 CSV newlines
Values containing CR/LF corrupt the CSV round trip. Quote or reject.

### P-09 `RenameColumn`
Does not update the column's identity consistently; renamed columns lose their data or
their type after a write.

### P-10 `JoinType.Average`
Not implemented / produces wrong results for multi-match joins.

### P-11 `Vertices[0]` on an empty part
Still reachable: `Measure()` handles `Count == 0`, but external callers (joins, writer,
`SpatialIoTests`' `Parts[0].Vertices[0]`) index without checking. Note `Part` can now be
legitimately empty and unmeasurable — `AddVertex` is the only mutator, so nothing forces
at least one vertex.

### P-39 `BoundingBox.Empty` is the full-range box
`Empty = new(MaxValue, MaxValue, MinValue, MinValue)` and the constructor normalises, so
`Empty` spans `[-1.8e308, +1.8e308]` on both axes. Consequences, all live:
- `Area()` overflows to `+infinity`; `RTreeNode.getArea` returns `+infinity` (its
  `MaxX < MinX` guard can never fire); `getMBRoverlap` returns `+infinity` against
  everything.
- `ContainsPoint` returns true for every point; `ContainsPoint` and `Area` have no
  `== Empty` guard, while `Overlaps`/`Contains`/`Union` do.
- `Empty` is every node's initial box and every empty `Part`/`Feature` box, and
  `addFeature` validates nothing. `Feature.AddPart` unions it, so **one empty part
  swallows its feature's entire MBR**; `Feature.ComputeBoundingBox` on a feature with
  zero parts returns the full-range box.
**Fix:** an explicit `IsEmpty` flag, or a normalisation that keeps `MinX > MaxX`
inverted, with `Empty` short-circuits in `Area`/`ContainsPoint`.
**Sequencing:** settle P-62 (which of these members survive) in the same change, or the
guards get written around members that are about to be deleted.

---

## 2. P1 — correctness under load, or blocked features

### P-01 `getMBRoverlap` containment gate  *(downgraded from P0, dormant)*
`getMBRoverlap` only tests whether a *corner* of the query lies inside the node. A node
fully contained by the query returns 0 overlap. Verified dormant today: `findByXY`
builds a degenerate box (`MinX == MaxX`) so both clauses collapse to a correct
containment test, and the insert path is caught by the all-leaves fallback. It becomes
live the moment a rectangular query exists — **hard prerequisite for P-02.**
`CHANGES_09012026.md` P0-1 states this defect inverted; `CHANGES_08312026.md` #15 states
it correctly.
**The correct predicate already exists in this codebase.** `BoundingBox.Overlaps` carries
a closed-interval test (`fix(#15)`, written specifically so that a query *containing* the
node still overlaps). `getMBRoverlap` is the unfixed original doing the same job a second
time. Fix = delete it and call `BoundingBox.Overlaps`, preserving the
"return ≥ 1 when overlapping" convention at the call sites (it exists so point-shaped
features are not pruned). Do not fix the corner test in place — that keeps two overlap
predicates that can drift again.

### P-02 Spatial joins discard the tree  *(mandatory)*
`SpatialJoins` enumerates features instead of descending the index. Blocked on P-01,
P-47 (no usable query API) and D-E.
The discarding is explicit and worth deleting with the fix: both join directions contain
`_ = tree ?? BuildTree(features);`, which constructs a whole R-tree and throws it away.
`pointTree` / `polyTree` are therefore pure cost today.

### P-12 `Ogr.RegisterAll()` thread-safety
Called on every `Read` **and** on every `Write`. Not idempotent-safe under concurrent
use. `tests/Nsi.Geospatial.Io.Tests/AssemblyInfo.cs` disables test parallelisation as a
workaround and names the registration guard as the real fix; that guard is this item.

### P-14 CRS authority/token model and transform options  *(needs D-D)*  *(half closed)*
**Closed by PR #8 `051e8d8`:** `Nsi.Geospatial.Reprojection/Reprojector.cs` is deleted,
with its hand-rolled `NativeLibrary` P/Invoke class (−164 lines). `CoordinateTransformer`
is now the library's only transform path. That removed a live correctness hazard, not
merely dead code: the P/Invoke path never called
`SetAxisMappingStrategy(OAMS_TRADITIONAL_GIS_ORDER)`, so it returned **transposed**
coordinates for any geographic CRS while `CoordinateTransformer` returned correct ones.
Two consequences to record: `Reprojector` was `public static` in a packable assembly, so
this is a breaking public-API removal (note it before 0.1.x is consumed anywhere); and
the axis-order guarantee now rests on a single code path with **no test** — see T-14.

Still open:
- `CoordinateTransformer.CrsToken` still prepends `"EPSG:"` unconditionally (D-D).
  Guard with T-15 once the model is chosen.
- `Projection.EpsgCode` is a `string?` carrying an authority-prefixed token, and three
  places independently build or parse that string: `CoordinateTransformer.CrsToken`,
  `SpatialReader.ParseEpsg`, `CrsInspector.ParseCode`. `SpatialReader` formats
  `$"EPSG:{code}"` in `ToProjection` and parses it straight back in `SameCrs` — a
  round-trip through a string inside one method body. Making the field an `int?` (plus a
  separate authority field if `ESRI:` codes are wanted) deletes all three parsers and the
  round-trip.
- Folded in: P-29 (`CoordinateTransformationOptions` / area-of-interest).

### P-15 `Root` public setter; no box validation
`RTreeManager.Root { get; set; }` accepts any node, and `addFeature` validates no box.
See P-39 for what an empty box does. Split out: P-51.

### P-17 `Parts` public; cached boxes go stale
`Feature.Parts` is a public `List<Part>`. `Feature.BoundingBox` is maintained
incrementally by `AddPart`, so `feature.Parts.Clear()` or `.RemoveAt()` silently leaves
a stale MBR. `Part` now defends against exactly this pattern (`Vertices` is read-only,
`AddVertex` is the sole mutator) — `Feature` has not received the same treatment, and
`Part` has no channel to notify its owner. Apply the same shape: private list +
`IReadOnlyList<Part>` facade + `AddPart`/`RemovePart`.
Also two maintenance paths for one box: `AddPart` unions incrementally *and*
`ComputeBoundingBox()` rebuilds from scratch. Callers use both. Pick one.

### P-18 Identity and `RemoveFeature`
`RemoveFeature` renumbers surviving ids and leaves the detached feature's `Owner`
pointing at the collection, so it still resolves the old CRS.
`RemoveFeatureLeavesTheDetachedFeatureResolvingTheOldCrs` pins this deliberately. It also
means the detached feature is outside `FeatureCollection.Crs`'s invalidation sweep —
currently rescued only by `Part`'s `CrsInfo` identity check, which is now the sole
defence. Pin that with a test. `SpatialJoins.BuildTree` keys on `f.Id`, which mutates
under `RemoveFeature`.

### P-19 Culture
Number formatting/parsing is not `InvariantCulture` in every path (`CsvHelper`,
`SpatialWriter` string fields). `Feature.GetAttribute` does use invariant — the others
do not.

### P-21 Geometry-type coverage
`SpatialWriter` emits `wkbMultiLineString`, which `SpatialReader.ProcessGeometry` cannot
read: its `if`/`else if` chain has no `else`, so the geometry is **silently dropped** and
the feature ends up with zero parts. Same for `wkbMultiPoint`. Combined with P-11 an
empty part then flows into every consumer.

### P-40 All-leaves fallback makes the build quadratic
`insert` falls back to scanning all leaves; with P-41's enlargement metric this is
quadratic in feature count.

### P-41 `getAddedSizeToAccomodate` measures union area, not enlargement
Chooses the child with the smallest *union* area, which biases toward already-large
children — the R-tree heuristic is enlargement (`union − existing`). The tie-break floor
is also in raw CRS units, so it means different things in metres and degrees.
`BoundingBox.EnlargementToContain` is exactly this quantity, already written and never
called (N-1). Use it rather than re-deriving it.

### P-42 `buildChildOptions` re-parents live children
While scoring candidate placements it re-parents children of the *real* node, then
discards the options. Mutates the tree during a read-only decision.

### P-43 `Options.First()` throws; no min/max invariant
The split loop `for (split = MinChidrens; split <= Count - MinChidrens; split++)` is
empty when `Count < 2*min`. At split time `Count == max + 1`, so the invariant is
**`max >= 2*min - 1`**. Defaults (10, 4) and the tests' (6, 3) satisfy it; `new
RTreeManager(4, 6)` throws from inside `split()`. Validate in the constructor with a
clear message.

### P-44 `SpatialJoins` uses planar distance regardless of CRS
Near-endpoint and proximity joins call planar `PointToSegmentDistance`/`Distance` even
when the collection is geographic. Should dispatch on `Crs.Kind` (the `Part` accessors
now show the pattern) or refuse.

### P-47 R-tree API gaps
No `Query(BoundingBox)`, no k-NN, no `Count`, and no way to get feature indices back out
of a traversal. `findByXY` is the only entry point and returns node-scoped results.
Prerequisite for P-02 and P-44.

### P-48 The R-tree test suite cannot detect a regression
Six gaps; the first is the important one:
1. **No regression test for MBR propagation** (`16585ef`, closed as P-49). The fix that
   made the index return all features is unguarded.
2. No test asserts query results against a brute-force oracle at scale.
3. No test for `min`/`max` invariant (P-43).
4. No test for the containment gate (P-01) — needs a rectangular query, so needs P-47.
5. No test that the tree's boxes match the features they index.
6. `RTreeTests`' class docstring still claims the tests do not assert correct behaviour,
   which was made false by `16585ef`.

### P-61 `Nsi.Geospatial.Reprojection` is a project holding one class  *(new, PR #8)*
After `051e8d8` deleted `Reprojector`, the project contains `CoordinateTransformer` plus a
206-byte csproj — and still costs a `Geospatial.slnx` node, a `ProjectReference` from
`Io`, and a CI `--include` target. The stated reason for the split (keeping core
GDAL-free) is already satisfied by `Nsi.Geospatial.Io`, and both projects require the
same native GDAL runtime, so the boundary buys nothing at build or run time.
**Fix:** fold into `Nsi.Geospatial.Io`, delete the csproj and the solution entry. Public
namespace change, same caveat as P-14: 0.x breaking removal, document it.

---

## 3. P2 — measurable defects, bounded impact

### P-23 `GeometryMath` docstring errors (four)
Great-circle triangle error is −0.8491%, not as documented; the 65°N figure is −0.6614%,
not 0.63%; the pole-crossing ring is 64.8× out (2.511583e14 vs 3.874521e12). **Do not
densify** — the docstring suggestion to densify before measuring makes it worse.

### P-24 `1e-9` tolerances are in CRS units
Degenerate-segment and centroid guards compare against `1e-12`/`1e-9` in whatever the
CRS's units are. Meaningless in degrees, trivially large in metres. Take an epsilon
parameter or scale by the CRS.

### P-25 Aggregate results into the Text column
Join aggregations have no output home.

### P-26 `interiorOnly` / `ContainsIndex` / duplicated join bodies; delete `Feature.Wkt`
- `Feature.Wkt` is dead state that goes stale the moment vertices change.
- `interiorOnly` is accepted by `NearestPointsToPolygons` and **never read**. Its sibling
  `exteriorOnly` *is* read, so one filter silently does nothing.
- `ContainsIndex(int index, long candidate) => candidate == index` is an indirection that
  only obscures `polyIdx == exteriorOnly.Value`. Delete both with the parameter.
- `NearestPointsToPolygons` and `NearestPolygonsToPoints` copy-paste the schema-backfill
  block and the nearest-neighbour scan (one keeps ties within `1e-9`, the other keeps a
  single best), and copy the matched field two different ways — `Aggregate(...)` versus
  an inline `Attributes.TryGetValue`. One shared scan + one shared backfill.
- `FieldCopy` asymmetry: the point→polygon direction has no aggregation path at all, so
  P-10's `Average` gap is directional, not general. Note it when fixing P-10.

### P-27 DBF column order
Round-tripped schema order differs from the source.

### P-28 Null semantics: `""` vs `null`
Reader and writer disagree; a null text field comes back as empty string.

### P-46 `getCandidateEndNodesByMBR` performs no MBR test at the leaf
Descends using MBRs then accepts every leaf entry, so the pruning is illusory below one
level. `BoundingBox.Overlaps`/`Contains` are the missing test (N-1).

### P-50 `getIsEndNode` inspects only `Children[0]`
Assumes all children of a node are at the same level. True today because splits only
produce same-level siblings; unguarded and will silently mis-classify if that changes.
Assert it.

### P-53 `Rel` cannot compare against zero  *(half fixed)*
`diff <= Math.Abs(expected) * tol` degenerates to `diff <= 0` when `expected == 0`, and
the message divides by zero (`rel diff ∞` — observed). Several planned acceptance tests
assert zero lengths and zero areas and will silently demand bit-exactness.
**Already correct:** `SphericalMetricsTests.RelD` implements the fix —
`diff <= Math.Max(Math.Abs(expected) * relTol, absTol)`.
**Remaining:** `CrsInfoAndAreaTests.Rel` still has the broken form, and
`CrsInspectionTests` uses no helper at all, hand-rolling
`Math.Abs(actual - expected) / expected` inline twice plus a bare `< 1e-3`.
**Fix:** adopt `RelD` as the single shared `AssertRel` (P-36) with an explicit `absTol`
chosen per unit (≈1e-6 m, not 1e-9 scaled from degrees), and delete the other forms.

### P-56 Ring storage is mixed open/closed  *(new, `44fbf06`)*
`Seal()` no longer strips a duplicate closing vertex and the reader does not normalise, so
**authored rings are stored open (`Count == n`) and read-back rings closed
(`Count == n + 1`)**. Every metric agrees (the `%n` primitives are invariant to it) but
the vertex list does not, so any consumer walking consecutive pairs without wrapping is
correct on read-back geometry and wrong on authored geometry.
- `PointInPolygon` in the Io test helpers is the exposure. If it iterates `i` to
  `Count - 1` it silently drops the closing edge on an open ring — and
  `PointDatasetIntersectsPolygonDataset` only feeds it read-back polygons, so it cannot
  catch this. Make it wrap (`for (int i = 0, j = Count - 1; i < Count; j = i++)`).
- Decide the convention and enforce it in one place. Recommended: keep `Seal()`
  non-mutating and strip the duplicate in `SpatialReader` (compare `XY`, not
  `Coordinates` — shapefile Z on the closing vertex is often unset).
- Until then, polygon vertex-count assertions must state `+ 1` explicitly rather than
  hiding behind `Assert.True(b.Count >= o.Count)`.

### P-57 `SpatialWriter` compares Z when deciding to close a ring  *(new)*
`if (verts[^1].Coordinates != first.Coordinates) ring.AddPoint(...)` compares the full
tuple. A source ring whose closing vertex has an unset Z does not match vertex 0, so the
writer appends a **second** closing point and the ring grows by one vertex per round
trip. Closure is a planar property: compare `XY`.
Note: `ClosedRing(Part)`, the helper that encoded this rule correctly for a `List<Vertex>`,
was deleted in PR #8 `7b4c6aa` as unused — the polygon branch had inlined its own copy.
The deletion was right; the inlined copy still carries the Z bug. Fix it in place.

### P-62 `BoundingBox` public surface with no owner  *(new; extends N-1)*
Verified across all production source and both test assemblies at `7b4c6aa`: `Overlaps`,
`Contains`, `ContainsPoint`, `FromVertices` and `EnlargementToContain` have **zero**
callers. Tellingly, `SpatialIoTests` needed a point-in-region check and hand-rolled
`PointInPolygon` rather than calling `ContainsPoint`. Split the decision rather than
treating the five alike:
- `Overlaps` → consumed by P-01. `EnlargementToContain` → P-41. `Contains` → P-46.
  **Keep**, and they stop being dead the moment those items land.
- `ContainsPoint` → homeless, and wrong for every point under P-39. Either give it the
  `Empty` guard and use it (joins and `findByXY` are candidates), or delete it.
- `FromVertices` → genuinely unreferenced with no planned consumer
  (`Part` maintains its MBR incrementally via `Union`; `Feature` recomputes by `Union`).
  Delete, or justify and test it.
Resolve inside P-39's change so guards are not written around members about to go.

---

## 4. P3 — hygiene

### P-22 Naming and dead code
`MaxChidrens`/`MinChidrens` misspelling (`Children`) and `addFeatureChild` unreachable;
`cumulativeOverlap`/`siblingOverlap` written and never read.
**Deleted in `44fbf06`:** `Part.Direction` (write-only after the `IsHole` derivation was
removed; winding survives as vertex order, and one fewer `IsClockwise()` P/Invoke per
ring), `Part.BeginIndex`, `Part.EndIndex`.
**Deleted in PR #8 `7b4c6aa`:** unused `using System`, `System.Collections.Generic`,
`System.Linq`, `System.Text`, `System.Threading.Tasks` from `RTreeManager`/`RTreeNode`,
plus `System.Xml`, `System.Xml.Linq`, and the redundant `using Nsi.Geospatial.Io;` inside
`namespace Nsi.Geospatial.Io` in `SpatialReader`. (Safe: `ImplicitUsings` is on.)
**Corrected:** this entry used to list `Feature._crs` (P-55) as remaining. It is already
gone — `Feature` resolves CRS through its owner chain and holds no field. P-55 is a
dangling reference; see N-12.
**Remaining:**
- `addFeatureChild` is not merely unreachable, it is *misleading*: it contains an area
  tie-break (`extensionReq == minExtension && childnode.getArea < …`) that the live path
  `addFeatureChildEnforceIntersect` lacks, so a reader will assume the tie-break is
  active. Delete it, or move the tie-break to the live path as its own change.
- `cumulativeOverlap` / `siblingOverlap` are assigned in `buildChildOptions` and never
  read — the split sorts on the local `overlap`/`totalArea`/`perimeterTotal` triple. Dead
  state carried by every node.
- `RTreeNode.getArea` re-implements `BoundingBox.Area()`;
  `getAddedSizeToAccomodate` re-implements `BoundingBox.EnlargementToContain` (P-41).
- `FeatureCollection.Crs`'s setter reaches through `f.Parts` to call
  `Part.InvalidateMetrics()` — add `Feature.InvalidateMetrics()` and forward, so the
  collection does not enumerate another type's internals.

### P-23a `GeometryMath` walk loops duplicated four times
`ClosedWalk`/`SphericalPerimeter` are the same loop over `pts[i], pts[(i+1)%n]` differing
only in the per-edge metric; `OpenWalk`/`SphericalLength` likewise for the open walk.
`Area` and `Centroid` are the same shoelace loop, and `Part.Measure` calls **both**, so
every ring is walked twice to produce two values from one accumulated sum.
**Fix:** one private `Walk(pts, closed, edgeMetric)` and one
`Shoelace(pts) → (area, cx, cy)`. Behaviour-preserving: the public entry points stay, and
`SphericalPerimeterClosesTheRingEvenForAnOpenPolyline` pins the semantics that make the
merge safe. Also halves `Measure()`'s work.

### P-30 README and warnings-as-errors
Four factual errors in the README; the spherical feature is undocumented.
`TreatWarningsAsErrors=false` is legitimately required because the GDAL NuGet package's
own generated `obj/.../GdalConfiguration.cs` emits 3× `CS8600`. Note that
`[obj/**/*] generated_code = true` suppresses **CA analyzers only** — compiler
diagnostics still fire, which is why no `CA` warnings appear from that file but three
`CS` ones do. Scope `<NoWarn>$(NoWarn);CS8600</NoWarn>` to `Nsi.Geospatial.Io.csproj`
only — a global one would silence the real `CS8600`s in `RTreeNode.cs`.

### P-31 SDK policy
The pinned SDK version is stated five inconsistent ways across `global.json`,
`ci.yml`, `release.yml`, the README and `Directory.Build.props`.

### P-32 `IsPackable`
Test projects are packable.

### P-33 `gdal` → `GDAL` naming.

### P-34 `release.yml` references `@main`.

### P-35 CI runs `Io.Tests` twice.

### P-36 Test project duplication
Three overlapping test files, two namespaces (`Nsi.Geospatial.Tests` and
`Nsi.Geospatial.Core.Tests`), and two independent copies of `Rel` plus two copies of
`PointInPolygon`. Every fix has to be applied twice (P-53 already does). Hoist one
`TestAssert` and shared geometry helpers.
**Reduced by PR #8 `4a0ec8b`:** `SphericalMathTests.cs` deleted (−246). It was a subsumed
earlier generation of `SphericalMetricsTests.cs` — ~90 % duplicated, each case in a
weaker form (looser tolerances, `0.3048` literals instead of `CrsInfo.MetersPerFoot`, no
absolute-floor guard). Both assertions worth keeping were ported first
(`SphericalPerimeterIsTheSumOfGreatCircleEdges`,
`PointToSegmentEastOfNorthSouthSegmentIsOneDegreeOfLongitude`), and the D-B spec tests
survived. Two files remain, still in two namespaces inside one assembly — which is *why*
the duplicate went unnoticed: the classes could not collide by name.
**Still duplicated:**
- `Cell(lon, lat, w, h)` — byte-identical in `SphericalMetricsTests` and
  `CrsInspectionTests`. (`LonLatCell(lonMin, lonMax, latMin, latMax)`, the incompatible
  second signature, was removed in `7b4c6aa`; `Cell` is now the one convention. Do not
  reintroduce a bounds-based variant — the two forms produce different polygons from
  arguments that look interchangeable.)
- `IntoFeature(Part)` — identical in `SphericalMetricsTests` and `CrsInspectionTests`.
- `Ring` / `Polygon` / `Geographic()` / `Projected()` — re-derived per class.
- `TempDir()` / `Cleanup(dir)` — in both Io test classes.
- **Golden values duplicated across assemblies:** `8.8187588297044e9` is
  `SphericalMetricsTests.CellAt44N` *and* a bare literal in `CrsInspectionTests`; ditto
  `8.8373695264e9` / `EllipsoidCellAt44N`. Changing `EarthRadiusAuthalicMeters` therefore
  breaks a test project that does not reference the file you edited. Share the constants.
- `Deg2Rad` was added to `SphericalMetricsTests` in `4a0ec8b` but the rest of that file
  still writes `Math.PI / 180.0` inline (`AnalyticCell`,
  `SphericalAreaShrinksWithLatitudeAsCosineOfMidLatitude`, `ToUnitVector`). Use it or
  drop it.
- Two surviving typos in test names/messages: `"peremiter = sum of edges"` (added in
  `7b4c6aa`) and `PointToSegmentOfDepenerateSegmentIsTheDistanceToThePoint`.

### P-37 Delete probes and dead writer helpers  *(done, PR #8)*
`ProbeOsrBinding.cs` deleted in `051e8d8` (−115); `SpatialWriter.ClosedRing`, `.RingWkt`,
`.Fmt` deleted in `7b4c6aa`. The WKT emission path those three served is gone for good —
`BuildOgrGeometry` builds geometry through the OGR API because `CreateFromWkt` rejects
valid polygon WKT on the 3.11.3 binding. Keep that comment; without it the helpers look
like an accidental omission.

### P-38 `Feature.ShapeType` vs `FeatureCollection.ShapeType`
Two sources of truth, free to disagree.

### P-51 `FeatureIndex` unguarded `[0]`  *(P1 → P3)*
`getChildrenContainingInd` dereferences `FeatureIndex[0]` without a guard, but it is
unreachable through the public API: it needs mixed-level children, and splits always
produce same-level siblings. Add the guard while in the file; do not prioritise.

### P-60 `PartType` lives in a namespace its consumers guess wrong  *(was N-9)*
`PartType` is in `Nsi.Geospatial.Enums` while its only consumer `Part` is in
`Nsi.Geospatial.Geometry`, so IDEs auto-suggest `Nsi.Geospatial.Geometry.Enums` — which
cost a real compile error during this branch. Decide the convention across `Enums/`.

### P-63 Writer/reader local hygiene  *(new, PR #8 review)*
- `SpatialWriter`'s polygon branch states the degenerate-ring guard twice with different
  messages and identical conditions (`i == 0 && verts.Count < 3`,
  `i > 0 && verts.Count < 3`). One guard naming the role in the message.
- `SpatialReader` and `SpatialWriter` carry ~15 fully-qualified names per file
  (`global::System.IO.File`, `global::System.Globalization.CultureInfo`,
  `OSGeo.OGR.Feature`, `Nsi.Geospatial.Enums.FieldType`) to dodge two collisions. Two
  `using` alias blocks per file remove the noise without changing behaviour.
- README calls the core project `Nsi.Geospatial.Core` while the assembly is
  `Nsi.Geospatial` (P-30). The split test namespaces in P-36 are the fossil record of
  that abandoned rename; settle the name once.

---

## 5. Partially complete — in flight

### Closed by PR #8 (`051e8d8`, `4a0ec8b`, `7b4c6aa`) — net −483 lines, no behaviour change
| Change | Recorded under |
|---|---|
| `Reprojector.cs` deleted: duplicate transform engine + hand-rolled P/Invoke (−164) | P-14 (half) |
| `ProbeOsrBinding.cs` deleted, fully commented-out spike (−115) | P-37 |
| `SpatialWriter.ClosedRing` / `.RingWkt` / `.Fmt` deleted (−16) | P-37, P-57 |
| `SphericalMathTests.cs` deleted, subsumed duplicate suite (−246) | P-36 |
| `LonLatCell` removed in favour of `Cell`; ported tests relocated; `"what"` labels named | P-36 |
| `CrsToken` `internal` → `private` (one caller, same file) | P-14 |
| Unused `using`s removed from `RTreeManager`, `RTreeNode`, `SpatialReader` | P-22 |

Residue the PR created, all small: `"peremiter"` label, `Deg2Rad` inconsistency, and the
axis-order guarantee now resting on one untested path (T-14) — all filed above.

### P-07 Open vs closed geometry  *(substantially done)*
Done: `PartType` enum; required `Part(PartType)` constructor; `Kind` get-only;
`IsRing`; `Seal()` derives `Perimeter` via `ClosedWalk`/`OpenWalk` and is idempotent;
the trailing-duplicate `RemoveAt` removed; `ClosedWalk`/`OpenWalk`/`SphericalLength`
added; `Area` is `double?` and `null` for a polyline; `AddVertex` no longer computes
metrics; `Vertices` read-only; `Measure()` the single writer.
Remaining: **P-56** (storage convention), **P-57** (writer `XY` compare), the T-1…T-7
acceptance tests, and the degenerate `[A,B,A]` case (now reports `Area == 0.0` rather
than `null` — defensible, but pin it in a test so it is a decision, not an artefact).

### P-13 `LengthMeters` per CRS  *(done)*
Both branches now sum the same edge set and differ only in metric. Keep
`LengthMetersMeansTheSameThingInBothCrsKinds` un-skipped and add the order-independence
test T-8.

### P-05, P-15, P-48 — see their sections for what remains.

---

## 7. Test backlog

| ID | Test | Guards |
|---|---|---|
| T-1 | `LineLengthIsTheSumOfItsEdges` — `(0,0),(5,5),(10,0)` in metres → `2*sqrt(50)`, `Area is null` | P-07 |
| T-2 | Same in a geographic collection | P-13 |
| T-3 | `ReadLinePreservesVertexCount` / `ReadPointHasASingleVertex` — exact counts, replacing `Assert.True(b.Count >= o.Count, "line lost vertices on round-trip")` | P-07, P-21 |
| T-4 | `SealIsIdempotent` — `Seal(); Seal();` twice leaves `Perimeter` unchanged, and `[A,B,C,D]` vs `[A,B,C,D,A]` give identical area and perimeter | P-07 |
| T-5 | **`PolygonHoleIsSubtractedAfterRead`** — exterior + hole through a real shapefile; `IsHole == true` on ring 1; `Feature.AreaSquareMeters == exterior − hole` | **P-05 — the only guard for a live production behaviour; nothing covers the read path today** |
| T-6 | `LineInGeographicCrsHasNoArea` → `AreaSquareMeters is null` | P-07 |
| T-7 | `OpenRingRoundTripsStably` — vertex count constant across write/read/re-read | P-56, P-57 |
| T-8 | `MetricsDoNotDependOnWhetherOrWhenSealWasCalled` — reading `LengthMeters` must not change what `AreaSquareMeters` reports; sealed-attach-later and attach-later-unsealed agree | P-54 invariant |
| T-9 | `AddingAVertexAfterMeasuringReMeasures` — read length, `AddVertex`, read again | cache invalidation |
| T-10 | `ReplacingTheCollectionsCrsInvalidatesPartMetrics` | cascade + identity key |
| T-11 | `DetachedFeatureRemainsMeasurable` — `RemoveFeature` then change `fc.Crs`; the removed feature must still recompute | P-18 |
| T-12 | `EmptyPartDoesNotInflateFeatureBoundingBox` | P-39 |
| T-13 | Degenerate ring `[A,B,A]` — assert whichever of `0.0` / `null` is chosen, in both CRS kinds | P-07 |
| T-14 | **`ReprojectToUsesTraditionalGisOrder`** — read a lon/lat fixture into a projected CRS and assert X is still longitude. Also closes N-2 (`Transformer_EatsLonLatNotLatLon`). The only path that pins axis order now, since the P/Invoke path that ignored it was deleted in PR #8 | P-14 |
| T-15 | `CrsTokenDoesNotDoublePrefixANonEpsgAuthority` — `Projection(epsgCode: "ESRI:102003")` must not produce `"EPSG:ESRI:102003"` | D-D, P-14 |

---

## 8. Notes

- **N-1** Five `BoundingBox` primitives are never called: `Overlaps`, `Contains`,
  `ContainsPoint`, `FromVertices`, `EnlargementToContain`. Three are the ready-made fixes
  for P-01, P-41 and P-46; two have no owner. Split decision: P-62.
- **N-2** `Transformer_EatsLonLatNotLatLon` was described but never added. Filed as T-14;
  it became *more* important in PR #8, not less.
- **N-3** Three docstrings describe signatures that no longer exist. Verified still
  stale: `SpatialJoins.BuildTree` (documents the `addFeature` argument order `a6f3def`
  deleted); `SpatialReader.ProcessGeometry` ("*CloseRing runs after any transform*",
  "*Direction must come from the source*" — both about removed code);
  `SpatialReader.ToProjection` ("*WKT for the source, which always carries it after
  inspection*" — it returns a `Projection`, and the sentence is a copy of `WktOf`'s
  summary); and `CrsInfoAndAreaTests.Ring`'s doc-comment, which explains that "*AddVertex
  derives `IsHole = !Direction`*" — `Part.Direction` was deleted in `44fbf06`, so the
  comment now describes an invariant the code cannot enforce.
  `grep -rn "CloseRing\|Direction" --include=*.cs .` after each cleanup.
- **N-5** `CHANGES_09012026.md` below P3-5 and parts of `CHANGES_09082026.md`'s narrative
  have never been readable through any fetch path. Do not assume they are empty.
- **N-6** Keep `CHANGES_08312026.md`'s `fix(#N)` legend: those markers live in
  `BoundingBox.Overlaps`, `AttributeColumn.Coerce`, `CsvHelper`, `Part.AddVertex` and
  `Feature`, and they index *that* file, not this one. `CHANGES_08312026.md` also claims
  the R-tree files are "restored verbatim from master, all original typos included" —
  that has been false since `16585ef` and `ac3bb82`.
- **N-7** Timeline rule: `CHANGES_09012026.md`'s P0-1 blamed the containment gate for
  missing features; `16585ef` fixed the actual cause (MBR propagation) four and a half
  hours later and the changelog was never reconciled. Any row citing 0901 P0-1 must be
  re-read against P-49.
- **N-8** Benchmark build and query separately. The RBush comparison may have measured
  `Load()` (bulk STR-style) rather than incremental `addFeature`, which would explain
  part of the gap and is exactly what P-40 will change.
- **N-10** `Part`'s metric cache depends on the invariant that geometry cannot change
  behind its back. That holds for `Part` (`Vertices` is read-only) but **not** for
  `Feature.Parts` (P-17) or `FeatureCollection.Features` (also a public `List`). Fixing
  P-17 should close the last hole.
- **N-11** `dotnet test` currently reports 2 skips: `EarthRadiusFeetIsTheAuthalicRadiusIn
  Feet` (P-04) and `PointToSegmentWhenFootIsBehindTheNearEndpointReturnsDistanceToA`
  (P-03). Those two are the entire skip budget; a new skip is a new defect being hidden.
  (`RTreeTests.BulkInsertAllFeaturesFindableByPoint` carries a commented-out `Skip` from
  `16585ef`; leave it as history, do not re-enable.)
- **N-12** Dangling `P-` references: **P-20** (cited by D-C), **P-54** (cited by P-13,
  T-8, and `Part`'s `_measuredCrs` comment), **P-55** (cited by P-22, and its subject
  `Feature._crs` no longer exists), **P-59** (cited twice by section 9) have no entry in
  this file. Either they were closed by deletion without the citations being cleaned, or
  they live in the unreadable changelogs (N-5). Do not reuse these numbers; restore the
  definitions or mark the citations dead.
- **N-13** Duplication tends to arrive as a *second* correct implementation rather than a
  second broken one. Three cases so far: `BoundingBox.Overlaps` vs
  `RTreeNode.getMBRoverlap` (P-01), `BoundingBox.EnlargementToContain` vs
  `getAddedSizeToAccomodate` (P-41), and two reprojection engines with different axis
  handling (P-14). When adding a predicate, grep for one that already exists — and when
  deleting a duplicate, check whether the survivor is the *correct* one.

---

## 9. Build hygiene

- **Warnings (10 known, `Nsi.Geospatial` only).** Six are real: `CS8625` (RTreeNode.cs:26,
  `null` default on a non-nullable parameter) and `CS8600` ×3 (80, 153, 184) and `CS8602`
  (295) — read all four before annotating; a wrong `!` is a latent NRE inside the index.
  `CA1854` (`AttributeTable.cs:57`, use `TryGetValue`). `CA1805` ×2 (`RTreeNode.cs:18,19`,
  `Max/MinChidrens` explicitly `= 0` — check which is the real default, against P-43).
  Line numbers shifted after PR #8 removed the unused `using` blocks — re-run a clean
  build before annotating.
- **`CA1829` at `RTreeNode.cs:104` may be a hot path.** If line 104 is the split loop's
  bound, `Children.Count()` allocates an enumerator on every iteration of a loop that
  runs on every insert. Hoist it.
- **`CA1711` (`FeatureCollection` naming) is a design opinion and a breaking rename.**
  Suppress with a rationale in `.editorconfig`, do not rename.
- These have been visible on every clean build; incremental builds hid them because
  `Nsi.Geospatial` was not recompiling. Once the six are fixed, flip
  `TreatWarningsAsErrors` to `true` with the scoped `NoWarn` from P-30.
- `dotnet format Geospatial.slnx --verify-no-changes --no-restore` is a hard CI step and
  has been failing. See P-59 for why the diff may be much larger than expected. Note also
  that PR #8 relocated test bodies by hand (`7b4c6aa`) — hand-moved blocks are the
  classic csharpier failure, so re-verify the format step on the PR head before assuming
  the remaining diff predates it.

---

## 10. Recommended order

1. Green build and format: the six real warnings, `CA1711` suppression, P-59, then
   `TreatWarningsAsErrors=true`. Re-run format on the PR #8 head first (section 9).
2. P-53 + P-36 (one `AssertRel`, shared geometry helpers, shared golden constants) —
   needed *before* the zero-assertion tests land, or they demand bit-exactness.
3. P-56 + P-57 together (storage convention), then T-1…T-4, T-7, T-13.
4. **T-5** — the read-path hole test. Highest value per line in this file.
5. **T-14** — axis order. One test, closes N-2, and guards the sole remaining transform
   path. Cheap enough to fold into step 4.
6. P-05 remainder (`Parts[0].IsHole` check, null-hole handling), P-21 (silent geometry
   drop — likely to surface as T-3 failures).
7. T-8…T-12, then P-17 so the cache invariant has no remaining hole (N-10).
8. P-04 and P-03 — the two remaining skips, and P-04 is PR #6's stated purpose.
9. R-tree cluster: **P-39 + P-62 → P-01 → P-43 → P-48 → P-40/P-41 → P-47 → P-44 → P-02 →
   P-42/P-46/P-50/P-15/P-51**, with D-E resolved before P-02. P-62 moves first so P-01
   and P-41 can consume `Overlaps`/`EnlargementToContain` instead of re-deriving them,
   and P-22's `addFeatureChild`/dead-field deletions ride along.
10. Structural cleanups while their files are already open: P-61 (fold the Reprojection
    project into `Io`), P-23a (`Walk`/`Shoelace`), P-63 (writer/reader hygiene), P-26
    (join duplication). All behaviour-preserving; all cheapest immediately after the
    tests in steps 2–7 are green.
11. Everything else as touched.