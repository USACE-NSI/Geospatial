# ISSUES — consolidated backlog

**Repository:** [USACE-NSI/Geospatial](https://github.com/USACE-NSI/Geospatial)
**Branch reviewed:** `feature/spherical` @ `56c8005` (PR #6, "initial spherical math")
**Supersedes:** `CHANGES_08312026.md`, `CHANGES_09012026.md`, `CHANGES_09082026.md`
**Review date:** 2026-09-08

This file is the single source of truth for known defects and open work. The three
`CHANGES_*` files are dated review artefacts, not a live tracker: they review refs that
no longer exist (`refactor` @ `9ec2296`, and HEAD `2f57967`), they describe an
`RTreeManager.addFeature` signature that commit `a6f3def` replaced, and they duplicate
each other's root causes under different IDs. Move them to `docs/reviews/` and do not
edit them; record all new work here.

---

## How to use this file

**Status values** (use exactly these strings so the file stays greppable):

| Status | Meaning |
|---|---|
| `open` | Confirmed present at HEAD. No work started. |
| `open (partial)` | Later work reduced the scope; the remainder is described in the row. |
| `blocked` | Cannot be estimated until a named decision or spike lands. |
| `closed` | Verified fixed in code. Listed in §5, not in the backlog tables. |

**Priorities** carry over from the `CHANGES_*` files: **P0** correctness / data loss,
**P1** robustness / latent crashes, **P2** minor / consistency, **P3** housekeeping.

**Numbering.** `P-xx` IDs are assigned once and never reused, including after closure.
When you close an item, set Status to `closed` and add `Closed by <sha>` — do not delete
the row. The 0901 file *deleted* its P1-10 row rather than marking it resolved, which is
how the P/Invoke layer survived being "resolved".

**Legacy IDs.** Every row maps back to the `CHANGES_*` ID(s) it came from, so commit
messages and old review comments remain traceable.

---

## 1. Counts

| | |
|---|---|
| Distinct issues across the three changelogs | 41 |
| Already fixed in code (changelogs stale) | 9 |
| Partially fixed / restated by later work | 6 |
| Still open and confirmed in code | 26 |
| New items found in code, in no changelog | 12 |
| **Total backlog rows** | **38** (`P-01` … `P-38`) |
| Duplicate reports collapsed | 11 clusters (§6) |

---

## 2. Decisions and spikes required before scheduling

Four items cannot be estimated as written. Resolve these first; several backlog rows are
gated on them.

### D-A. Decision: what happens to the original R-tree? — gates `P-01`, `P-02`, `P-15`, `P-22`

`CHANGES_08312026.md` states that `RTreeManager.cs` and `RTreeNode.cs` "were **restored
verbatim** from master (namespace changes only, all original typos included)" and that the
known defects are "preserved on purpose". **That is no longer true.** Commits `a6f3def`
("updating to boundingbox throughout") and `ac3bb82` ("fixing warnings in Rtremanager")
refactored both files to `BoundingBox`, properties and csharpier formatting, silently
closing `P1-3` and `P1-5` and invalidating the 0831 file's central premise. The
"preserve the defects deliberately" policy has therefore already been abandoned by
accident.

Pick one:

1. **Fix it** — port the closed-interval overlap test that already exists as
   `BoundingBox.Overlaps` (`fix(#15)`) into `RTreeNode.getMBRoverlap`, drop the
   `Math.Max(overlap, 1)` floor, then land `P-15` and `P-22`.
2. **Delete it** — remove `RTreeManager`/`RTreeNode`/`SpatialJoins.BuildTree` and the
   `pointTree`/`polyTree` parameters, and adopt a maintained spatial index.

Either is defensible. The status quo — a defective tree that production code deliberately
bypasses — is not.

### D-B. Question: are the R-tree tests currently passing? — gates `P-01`

`CHANGES_08312026.md` says `BulkInsert_AllFeaturesFindableByPoint` and
`GetEndNodes_LeavesHoldAllFeatureIndices` "fail (or are skipped with a reason) — that's
evidence, not a regression." In `tests/Nsi.Geospatial.Tests/RTreeTests.cs` the skip is
**commented out** (`[Fact] //(Skip = ...)`) and `GetEndNodes…` has no skip at all. Both
are live under `dotnet test`, which CI runs.

So either CI is red, or the `BoundingBox` refactor changed tree behaviour enough that
they now pass. **Settle this before any R-tree work**, because the answer decides whether
the house rule in this area is "assert correct behaviour" or "pin current behaviour" —
`RTreeTests`' own class docstring claims the latter while its method names assert the
former.

### D-C. Spike: what can the gdal 3.11.3 C# binding actually do? — gates `P-06`, `P-20`

`P0-4` recommends `SetField(name, l)` plus `OFTInteger64`. But `SpatialWriter` carries an
in-code comment stating the binding has "no object overload, and no `Layer.FieldIndex` in
3.11.3", that "the string setter works for any OGR field type", and that `P0-4` is
"intentionally left unchanged in this class". The changelog and the code comment disagree
about what is possible. Determine the binding's real surface (including
`OFTInteger64`/`SetField` overloads and `Geometry`/`Feature` ownership for `P-20`) before
estimating either item.

`tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs` is a commented-out tool that was built
to answer exactly this question for P1-10. Un-comment it once, capture the output, then
delete it (`P-37`).

### D-D. Design: one CRS-token and authority model — gates `P-14`, `P-29`

`CHANGES_09082026.md` P2-15 recommends changing `Projection.AlbersUsa.EpsgCode` from
`"EPSG:102003"` to `"ESRI:102003"`. **Applying that recommendation as written introduces a
new bug:** `Reprojector.CrsToken` prepends `"EPSG:"` to any token not already starting
with that exact prefix, so the result becomes `"EPSG:ESRI:102003"`, which OSR cannot
resolve. The token builder, the parser (`SpatialReader.ParseEpsg`) and the comparison
(`SameCrs`) must be redesigned together — see `P-14`.

---

## 3. Backlog — P0 (correctness / data loss)

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-01` | R-tree `getMBRoverlap` containment gate drops subtrees on insert and query | `Spatial/RTreeNode.cs` | `0901 P0-1`, `0831` "intentionally unfixed" | `blocked` on **D-A** | If fixing: closed-interval test replaces the `(queryMax in [min,max]) \|\| (queryMin in [min,max])` gate; `Math.Max(overlap,1)` floor removed; `BulkInsert_AllFeaturesFindableByPoint` un-skipped and green. If deleting: `RTreeManager`, `RTreeNode`, `BuildTree`, both tree params and `RTreeTests` all gone. |
| `P-02` | Both joins build an R-tree and immediately discard it (`_ = pointTree ?? BuildTree(points)`); public API advertises tree-based candidate selection it does not perform | `Spatial/SpatialJoins.cs` | `0901 P0-2`, `0831` | `blocked` on **D-A** | O(n log m) candidate selection, or the params and `BuildTree` are removed and the docs say O(n m). Also: `NearestPolygonsToPoints` leaves destination fields unset when nothing matched, and takes no `JoinType` — asymmetric with its sibling. |
| `P-03` | `SphericalPointToSegmentDistance` cannot detect a perpendicular foot behind `a`; `deltaAT = acos(cos d13 / cos dXT)` is non-negative by construction | `Geometry/GeometryMath.cs` | `0908 P0-10` | `open` | Guard `cos(InitialBearing(a,p) - InitialBearing(a,b)) < 0` and return `radius * delta13`. Measured today: returns 1000.786 m where the true nearest point is `a` at 1057.160 m. Un-skip `PointToSegment_WhenFootIsBehindTheNearEndpoint_ReturnsDistanceToA`. |
| `P-04` | Units contract: `EarthRadiusFeet = 20925524.9` is documented as "6,371,000 m in feet" but x 0.3048 = 6,378,099.99 m — the WGS84 **equatorial** radius. `SphericalArea`'s docstring warns the equatorial radius "inflates them ~0.22%" and then instructs callers to pass this constant for square feet | `Geometry/GeometryMath.cs` | `0908 P0-11`, PR #6 body | `open` | Don't merely correct the constant. PR #6's stated goal is "right now it outputs meters instead of feet - we should probably change that", so expose `AreaSquareFeet` / `LengthFeet` (or a unit-tagged quantity) and delete the pass-a-magic-radius advice. Acceptance: `EarthRadiusFeet_IsTheAuthalicRadiusInFeet` un-skipped and green; no caller needs to know a radius. |
| `P-05` | `Feature.AreaSquareMeters` hole accounting depends on a flag known to be inverted: it treats `Parts[0]` as exterior and subtracts every later part flagged `IsHole`, where `IsHole` is derived as `!Direction` | `Geometry/Feature.cs`, `Geometry/Part.cs` | `0901 P2-1` **+ new** | `open` | `P2-1` was rated P2 on the reasoning that `IsHole`/`Direction` were "dead except inside `CloseRing`". That is no longer true — the field is now load-bearing in a headline metric, and `SphericalMetricsTests.Ring()` deliberately relies on the derivation. Fix the polarity to the shapefile convention (outer rings CCW) or decide holes by containment rather than a flag. Acceptance: a real exterior+hole polygon returns exterior-minus-hole. |
| `P-06` | Writer field typing: `SetOgrField` casts `long` to `int`; `FieldType.LongFT` maps to `OFTString`; bools written `"1"`/`"0"` but read back through `bool.TryParse` (so they coerce to null) | `Io/SpatialWriter.cs` | `0901 P0-4` | `blocked` on **D-C** | Acceptance: a 64-bit ID greater than 2^31 and a `bool` survive write-then-read unchanged; one bool representation end to end. |
| `P-07` | `CloseRing()` is called on linestrings and points — appends the first vertex to open lines (inflating perimeter, polluting distances) and duplicates point vertices | `Io/SpatialReader.cs` | `0901 P0-5` | `open` | Call `CloseRing` only for polygon rings. Note the defect is currently **pinned by test**: `SpatialIoTests.LinesShapefileRoundTrip` comments "The reader closes every part, so assert on the leading (real) vertices" — that assertion must be rewritten, not preserved. Acceptance: `LengthMeters` on a read line equals the sum of its edges. |
| `P-08` | CSV round-trip corrupts values containing newlines: the writer quotes them, `ReadAll`/`ParseLine` reset state per line | `Io/Csv/CsvHelper.cs` | `0901 P0-8` | `open` | Accumulate lines while a quote is open, **or** declare the writer write-only and stop quoting `\n`. Acceptance: round-trip test for embedded newline, comma and doubled quote. |
| `P-09` | `AttributeTable.RenameColumn` moves only the schema key; per-feature `Attributes` dictionaries keep the old key, so `CoerceRow` silently drops the renamed column | `Attributes/AttributeTable.cs` | `0901 P0-9` | `open` | Propagate the rename to rows, or rename the method `RenameColumnSchemaOnly` and delete `AddField`'s now-empty backfill comment. Acceptance: a renamed column still coerces. |
| `P-10` | `JoinType.Average` still throws when values exist but none are numeric; and returns a silent `0` when no values exist at all | `Spatial/SpatialJoins.cs` | `0901 P0-6` | `open (partial)` | Current code is `values.Count == 0 ? 0d : values.Where(v => ToDouble(v) is not null).Average(...)`. The guard was moved, not fixed: `InvalidOperationException` is still reachable on valid input, and a new silent-zero path was added. Filter to numerics first, then guard; decide empty-vs-zero explicitly. |
| `P-11` | `DistanceFeatureToFeature` indexes `Vertices[0]` when `Vertices.Count == 0` (the `< 2` branch still dereferences), and reads point coordinates off `BoundingBox` with no `Empty` check | `Spatial/SpatialJoins.cs` | `0901 P0-7` | `open` | Skip empty parts; refuse (or fall back to geometry) when `BoundingBox == BoundingBox.Empty`. Acceptance: no `IndexOutOfRangeException`; no garbage distances when `ComputeBoundingBox()` wasn't called. |

---

## 4. Backlog — P1 / P2 / P3

### P1 — robustness and latent crashes

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-12` | `Ogr.RegisterAll()` is called unconditionally on every `Read`/`Write`. Not idempotent, not thread-safe: two threads double-register plugin drivers and abort the process. Worked around in tests by `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, which serialises the suite but does not fix production | `Io/SpatialReader.cs`, `Io/SpatialWriter.cs` | `0908 P1-16`, `0901 P2-9` | `open` | One-time guard (`internal static class GdalBootstrap` with `Lazy<bool>` — `ExecutionAndPublication` is the correct default). Then delete the assembly attribute, whose own comment says to remove it together with this fix. Acceptance: Io.Tests run in parallel. |
| `P-13` | `LengthMeters` means different things per CRS branch: Projected uses `Part.Perimeter` (open walk), Geographic uses `SphericalPerimeter` (which **closes** the ring). A 3-vertex 1-degree line reports 379,639.757 m against a true 222,390.159 m (+70.7%) | `Geometry/Part.cs`, `Geometry/GeometryMath.cs` | `0908 P1-15` + `0908 P2-11` | `open` | Make both branches an open walk, or stop `SphericalPerimeter` closing. Fix the `SphericalPerimeter` docstring in the same commit — it describes an open walk while the loop indexes `% pts.Count`. Un-skip `LengthMeters_MeansTheSameThingInBothCrsKinds`. |
| `P-14` | Retire the surviving P/Invoke layer and unify CRS-token handling. `Reprojector.cs` is still in the tree, still public, still P/Invokes **private** OGR symbols (`OGRNewCoordinateTransformation`, `OGR_CT_Transform`), and `CrsToken` still prefers the EPSG token over WKT. Meanwhile `SpatialReader.SameCrs`/`ParseEpsg` discard the authority, so `ESRI:102003` compares equal to `"EPSG:102003"` | `Reprojection/Reprojector.cs`, `Io/SpatialReader.cs`, `Projections/Projection.cs` | `0901 P1-10` (remainder), `0908 P2-15`, `0908` "P1-10 follow-on" | `open (partial)`, `blocked` on **D-D** | `CHANGES_09082026.md` marks P1-10 "Resolved", but only `CoordinateTransformer` was migrated to the managed binding — `Reprojector` was left behind as a duplicate second implementation of the same job. Land together: authority-aware token builder and parser, authority-checked `SameCrs`, `"ESRI:102003"`, and removal of `Reprojector`'s `Native` layer. Also delete `SpatialReader`'s private `WktOf` duplication if `Reprojection` can supply it. |
| `P-15` | R-tree hardening: split-position loop is empty when `maxChildren < 2*minChildren - 1` so `Options.First()` throws (min 4 / max 6 is enough); `getChildrenContainingInd` dereferences `FeatureIndex` unconditionally; `Root`'s setter is still public so callers can detach a tree on split | `Spatial/RTreeManager.cs`, `Spatial/RTreeNode.cs` | `0901 P1-1`, `P1-2`, remainder of `P1-3` | `blocked` on **D-A** | Validate `max >= 2*min - 1` in the constructor; null-guard the index; make the `Root` setter private. |
| `P-16` | `BoundingBox` empty sentinel is a normalised whole-double-range box (`MinX = MaxValue`, `MaxX = MinValue`), so `FromVertices`' `IsPositiveInfinity` check is unreachable and `ContainsPoint(Empty)` is true for every point — while `Overlaps`, `Contains` and `Union` all guard on `== Empty` | `Geometry/BoundingBox.cs` | `0901 P1-4` | `open` | Add an `IsEmpty` flag (or return a nullable box) and guard `ContainsPoint` and `Area()` consistently with the other members. |
| `P-17` | `Feature.Parts` is a public `List<Part>` while `BoundingBox` only advances in `AddPart`, so a direct `Parts.Add` leaves a stale box — `SpatialJoinTests` itself has to call `Parts.Add` then `ComputeBoundingBox()` | `Geometry/Feature.cs` | `0901 P1-6` | `open (partial)` | Renamed `Mbr` to `BoundingBox` and added `ComputeBoundingBox()`; encapsulation not done. Expose `IReadOnlyList<Part>` or compute lazily. |
| `P-18` | Identity model: `RemoveFeature` renumbers every surviving `Id` (desyncing any tree built earlier, while the removed feature keeps its old `Id`), and `BuildTree` keys on `f.Id` rather than collection position, so `findByInd` is useless for features not added via `AddFeature` (all `Id == 0`) | `Geometry/FeatureCollection.cs`, `Spatial/SpatialJoins.cs` | `0901 P1-7`, `P1-8` | `open` | Give features stable identity, or rebuild indexes on mutation, or document both hazards at the call site. |
| `P-19` | `AttributeColumn.Coerce` parses with current-culture `ToString`/`TryParse` for double, float, int, long and DateTime | `Attributes/AttributeColumn.cs` | `0901 P1-9` | `open` | `CultureInfo.InvariantCulture` plus explicit `NumberStyles`/`DateTimeStyles`. Acceptance: a test under a `de-DE` culture reads `1,5` correctly and `1.5` as intended. |
| `P-20` | OGR ownership unverified: `using var defn = feat.GetFieldDefnRef(i)` may free shared layer state; `using var of` passed to `CreateFeature` may double-free; `of.SetGeometry(geom); geom.Dispose();` rests on an unverified "OGR copies the geometry" comment | `Io/SpatialReader.cs`, `Io/SpatialWriter.cs` | `0901 P1-11` | `blocked` on **D-C** | Verify against the 3.11.3 binding; add focused tests. Native memory corruption is silent. |
| `P-21` | Geometry-type coverage is asymmetric between writer and reader. `MapGeomType` (reader) and `MapShapeTypeToOgr` (writer) both silently default unknown types to Point; multipolygon rings are flattened into one part list losing exterior-to-hole association; and `BuildOgrGeometry` **emits** `wkbMultiLineString` which `ProcessGeometry` cannot **read** | `Io/SpatialReader.cs`, `Io/SpatialWriter.cs` | `0901 P1-13` + new | `open` | Handle types explicitly (throw on unmapped rather than defaulting to Point); handle `wkbMultiLineString`/`wkbMultiPoint`/collections; preserve ring grouping. Acceptance: write a multi-part line, read it back. |
| `P-22` | R-tree dead code: `addFeatureChild`, `cumulativeOverlap`, `siblingOverlap`, `RecomputeMBR`, `getCandidateFeatNodesByMBR`, plus unused `System.Xml`, `System.Xml.Linq`, `System.Text`, `System.Threading.Tasks` usings | `Spatial/RTreeManager.cs`, `Spatial/RTreeNode.cs` | `0901 P2-7`, `0831`, remainder of `P2-5` | `blocked` on **D-A** | Delete once `P-01` lands. Note `cumulativeOverlap`/`siblingOverlap` are simultaneously reported as a live defect (0831: `buildChildOptions` reads stale instance properties) and as dead code (0901 P2-7) — same lines, one truth needed. |

### P2 — minor and consistency

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-23` | One consolidated docstring pass over `GeometryMath`: (a) `SphericalArea` claims "Exact on a sphere; the only error is sphere-vs-ellipsoid", true only for rings whose edges follow meridians and parallels — a great-circle 1-degree triangle is -0.8491%; (b) the "0.63% low at 65N" figure measures -0.6614%; (c) a pole-enclosing ring sweeps the full longitude range and returns an area off by 64.8x (2.511583e14 against a true 3.874521e12) | `Geometry/GeometryMath.cs` | `0908 P2-11`, `P2-12`, `P2-13`, `P2-14` | `open` | One commit, four docstring fixes. State the accuracy curve (+0.4489% at 0 deg, +0.1029% at 30 deg, -0.2106% at 44 deg, -0.5671% at 60 deg, -0.6614% at 65 deg). Document the pole limitation or detect longitude wrap and throw. **Do not densify** — `0908` shows the error is orders of magnitude below the accepted ellipsoid term at NSI footprint scale. |
| `P-24` | Nearest-neighbour tie-break epsilon `1e-9` is in absolute CRS units, so it is meaningless when compared across CRS kinds | `Spatial/SpatialJoins.cs` | `0901 P2-2` | `open` | Relative tolerance, or an explicit parameter. |
| `P-25` | Sum/Average/Count write numbers into columns backfilled with the **source** column's type, which may be Text | `Spatial/SpatialJoins.cs` | `0901 P2-3` | `open` | Widen the backfilled column type for aggregate joins. |
| `P-26` | Dead API surface: `interiorOnly` is accepted and never read; `ContainsIndex` wraps a single `==`; and `Feature.Wkt` is never written by Io at all | `Spatial/SpatialJoins.cs`, `Geometry/Feature.cs` | `0901 P2-6`, remainder of `P2-4` | `open (partial)` | `P2-4` is half-done: `FeatureCollection` and `Part` no longer expose `Wkt`, and `CrsInfo.Wkt` is now the one authoritative copy — so `Feature.Wkt` should be **deleted**, not renamed to `SrsWkt` as `0901` recommended. |
| `P-27` | Column order relies on `Dictionary` insertion order, so DBF field order depends on an implementation detail; `Reorder` works by rebuilding a `Dictionary` | `Attributes/AttributeTable.cs` | `0901 P2-8` | `open` | Back with `List<AttributeColumn>` plus a name index. |
| `P-28` | Reader null semantics are inconsistent: unset text becomes `""`, unset date becomes `null` | `Io/SpatialReader.cs` | `0901 P2-9` (remainder) | `open (partial)` | `LayerIndex` was added (multi-layer GPKG now addressable); the null semantics are not settled. Pick one convention. |
| `P-29` | `CoordinateTransformationOptions` is available in the binding (`SetAreaOfInterest`, `SetBallparkAllowed`, `SetDesiredAccuracy`, `SetOnlyBest`) but unused, so PROJ may pick a global ballpark transform over a grid-based one. NAD83/conus grid shifts are cm-m; ballpark reaches tens of m | `Reprojection/CoordinateTransformer.cs` | `0908 P3-3` | `open` — **re-rated P3 to P2** | Fold into `P-14` so reprojection is made correct once. Pass an area of interest for conus NSI work. |

### P3 — housekeeping, docs, CI

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-30` | README carries four inaccuracies — the project is `Nsi.Geospatial`, not `Nsi.Geospatial.Core`; `TreatWarningsAsErrors` is claimed but `Directory.Build.props` sets `false`; CSV is claimed to sit behind `IFeatureSource`/`IFeatureSink` but `CsvHelper` is a static class wired to neither; and ".NET 8 is enough" conflicts with the SDK pin. **Additionally: the feature this branch exists for is undocumented** — `CrsInfo`, `CrsInspector`, `AreaSquareMeters`, `LengthMeters`, `ReprojectTo`, `RequireInspectableCrs` appear nowhere | `README.md` | `0901 P3-1`, `0901 P2-5` (docs half) | `open` | Fix the four claims and document the spherical/CRS surface with a worked example. |
| `P-31` | The SDK/target-framework policy is stated five inconsistent ways: `global.json` pins `9.0.100` (`rollForward: latestFeature`, which will not cross a major), `Directory.Build.props` targets `net8.0`, README says ".NET 8 is enough", `ci.yml` installs 8.0.x **and** 9.0.x, and `release.yml` requests `dotnet-version: 8.0.x` | `global.json`, `Directory.Build.props`, `README.md`, `ci.yml`, `release.yml` | `0901 P2-10`, `P3-1` (overlapping) | `open` | Choose one SDK floor and express it once. The `release.yml` / `global.json` conflict is new since the changelogs and is a release-pipeline failure waiting to happen. |
| `P-32` | `tests/Nsi.Geospatial.Tests/Nsi.Geospatial.Tests.csproj` is missing `<IsPackable>false</IsPackable>`; Io.Tests has it. A solution-wide `dotnet pack` emits test packages | `tests/Nsi.Geospatial.Tests/*.csproj` | `0901 P3-2` | `open` | Add the property. |
| `P-33` | `PackageReference Include="gdal"` (lowercase) is a non-canonical package id | `Nsi.Geospatial.Io.csproj` | `0901 P3-4` | `open` | Use `GDAL`. |
| `P-34` | `release.yml` invokes a reusable workflow pinned to a mutable `@main` in another org | `.github/workflows/release.yml` | `0901 P3-5` | `open` | Pin to a tag or commit SHA. |
| `P-35` | CI runs `Nsi.Geospatial.Io.Tests` twice — `dotnet test Geospatial.slnx` already includes it, and a following step runs the project again. The `Category=Gdal` trait is now declared on `SpatialIoTests` and `CrsInspectionTests` but consumed by no filter | `.github/workflows/ci.yml` | successor to `0901 P3-3` | `open (partial)` | `P3-3` itself is **closed** (the no-op `--filter Category!=Gdal` is gone and the trait now exists). Split into a GDAL-free job and a native-GDAL job, or drop the trait. |
| `P-36` | Three test files cover the same spherical/CRS surface with divergent helpers and two different namespaces. `SphericalMathTests.cs` (8.6 KB) and `SphericalMetricsTests.cs` (31.4 KB) both assert closed-form graticule area, antimeridian crossing, winding independence, degenerate rings, haversine distance, perimeter summation and point-to-segment — with two private `Rel` helpers of different signatures and two cell builders (`LonLatCell` vs `Cell`) — and declare `Nsi.Geospatial.Tests` vs `Nsi.Geospatial.Core.Tests`. `CrsInfoAndAreaTests.cs` (12.7 KB) and `CrsInspectionTests.cs` very likely overlap the same way | `tests/**` | new | `open` | Merge by function, not by authoring session. Pick one namespace. Adopt one tolerance idiom — `SpatialIoTests` uses `const double Tol = 1e-9` passed to `Assert.Equal` while `CrsInspectionTests` uses digit counts (`Assert.Equal(1.0, …, 12)`), which is exactly the overload confusion `P-37` describes; unify before closing that item. |
| `P-37` | Dead test and writer code: `ProbeOsrBinding.cs` is 100% commented out and self-labelled "TEMPORARY diagnostic for P1-10 … Delete once settled" — P1-10 is settled; and `SpatialWriter.ClosedRing`/`RingWkt`/`Fmt` are orphaned by the switch to the programmatic OGR geometry API | `tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs`, `Io/SpatialWriter.cs` | new | `open` | See **D-C** first if the probe's output is still wanted, then delete. |
| `P-38` | `Feature.ShapeType` and `FeatureCollection.ShapeType` are duplicated state; Io reads only the collection's, while tests set both | `Geometry/Feature.cs`, `Geometry/FeatureCollection.cs` | new | `open` | Drop the per-feature copy or make it authoritative and validate consistency. |

---

## 5. Already fixed — do not re-report

Verified in code at `56c8005`. These rows exist so nobody spends a review rediscovering
them.

| Legacy ID | Item | Evidence in code |
|---|---|---|
| `0831 #9` | `AttributeColumn.Coerce` threw NRE on null | `if (raw is null) return null;` with the `fix(#9)` comment in place |
| `0831 #10` | `FindUniques` replaced by `Distinct` | `CsvHelper.ReadUniqueColumn` uses `.Distinct(StringComparer.OrdinalIgnoreCase)` |
| `0831 #11` | `ReadCSVtoDict` naive split and trailing-null NRE | Gone; quote-aware `ParseLine` present |
| `0831 #15` | `BoundingBox.Overlaps` closed-interval test | Present with `fix(#15)` comment plus `Empty` guards — but see `N-1` below: it has **no production caller** |
| `0831 fix:` | `Feat` parallel lists replaced | `Feature` holds `Parts` + `Attributes` + `Owner` together |
| `0831 fix:` | Deterministic OGR disposal; no hardcoded `C:\Software\GDAL GISInternals` | `using var` throughout reader/writer; CI exports `GDAL_DATA`/`PROJ_LIB`/`LD_LIBRARY_PATH` |
| `0901 P1-5` | `addFeature(featInd, Xmax, Xmin, Ymax, Ymin)` reversed argument order | Now `addFeature(int[] featInd, BoundingBox bbox)` — closed by `a6f3def` |
| `0901 P1-3` | `public _root` field reassigned on split | Now `public RTreeNode Root { get; set; }`; the still-public setter is tracked in `P-15` |
| `0901 P3-3` | `--filter Category!=Gdal` was a no-op | Filter removed from `ci.yml`; the trait now exists — successor problem tracked in `P-35` |
| `0908 P1-14` | Axis order unpinned; geographic transforms silently transposed | `srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER)` on both SRSes in `CoordinateTransformer.CreateSpatialReference`. **Unprotected** — see `N-2` |
| `0908 P1-10` | P/Invoke threw `EntryPointNotFoundException` | Half-closed only: `CoordinateTransformer` uses the managed binding; `Reprojector`'s P/Invoke layer survives. Remainder is `P-14` |

**Partially fixed** (kept in the tables above with `open (partial)`): `P-17` (was `P1-6`),
`P-26` (was `P2-4`), remainder of `P-22` (was `P2-5`), `P-28` (was `P2-9`), `P-35` (was
`P3-3`), `P-14` (was `P1-10`).

---

## 6. Duplicate clusters collapsed

Eleven root causes were reported more than once, sometimes with different IDs **and
different recommended fixes**. Each is now a single row.

| Cluster | Root cause | Reported as | Now |
|---|---|---|---|
| C1 | R-tree containment gate | `0831` #15 note + "intentionally unfixed"; `0901 P0-1` | `P-01` |
| C2 | Stale `cumulativeOverlap`/`siblingOverlap` in `buildChildOptions` | `0831` "intentionally unfixed"; `0901 P2-7` | `P-22` (one truth needed: defect or dead code) |
| C3 | Join builds a tree and discards it | `0831`; `0901 P0-2` | `P-02` |
| C4 | `Ogr.RegisterAll()` per call | `0901 P2-9`; `0908 P1-16` | `P-12` (kept the P1 rating; retired the P2) |
| C5 | `SphericalPerimeter` closes the ring | `0908 P1-15`; `0908 P2-11` | `P-13` (+ docstring half in `P-23`) |
| C6 | CRS authority confusion | `0901 P1-10` (row deleted); `0908` "P1-10 follow-on"; `0908 P2-15` | `P-14` — **the two prescribed fixes contradict each other**, see **D-D** |
| C7 | R-tree dead code | `0831`; `0901 P2-7` | `P-22` |
| C8 | `TreatWarningsAsErrors=false` | `0901 P2-5`; `0901 P3-1` | `P-30` (docs claim) |
| C9 | .NET 8 vs SDK pin | `0901 P2-10`; `0901 P3-1` | `P-31` (now also collides with `release.yml`) |
| C10 | R-tree style and typos | `0831` "typos preserved on purpose"; `0901 P2-5` | folded into `P-22` / remainder of `P-15` — and **both are stale**, see **D-A** |
| C11 | CSV correctness | `0831` #10 and #11 (**done**); `0901 P0-8` (**open**) | `P-08`; #10/#11 are in §5. Not duplicates, but adjacent rows in one file invite conflation |

**Numbering is not a citable scheme.** `0831` states #1-#8 and #12-#14 are unreferenced;
`0901` has no P0-3 and no P1-12; `0908` has no P0-12, P1-12 or P2-16. Cite `P-xx` from now
on.

---

## 7. Cross-cutting notes

- **N-1 — `BoundingBox.Overlaps` is unused public API.** `0831` instructs "new geometry
  code should use the corrected `BoundingBox.Overlaps` (fix #15)". In fact `RTreeManager`
  still queries `getMBRoverlap`, and `SpatialJoins` queries nothing. `Overlaps`, `Contains`
  and `EnlargementToContain` appear to have no caller. Either wire them (that is `P-01` and
  `P-02`) or delete them.
- **N-2 — the axis-order fix is unguarded.** `0908` recommended adding
  `Transformer_EatsLonLatNotLatLon` as the guard for `P1-14`. The fix landed; I found no
  such test. Add it under `P-14`, otherwise a future refactor silently re-transposes every
  geographic transform.
- **N-3 — three changelog rows are actively misleading about code they describe.**
  `SpatialJoins.BuildTree`'s docstring still says "Note the original addFeature argument
  order: (featInd, Xmax, Xmin, Ymax, Ymin)" about a signature `a6f3def` replaced; the
  changelogs describe a `refactor` branch that no longer exists (`main` and
  `feature/spherical` are the only branches); and `0908` names HEAD `2f57967` while the
  branch is at `56c8005`. Fix the `BuildTree` docstring as part of `P-01`/`P-02`.
- **N-4 — `0908`'s own summary is not reconcilable with its table.** It claims "11 open …
  1 hypothesis refuted", but the refuted hypothesis appears nowhere in the readable table.
- **N-5 — coverage caveat for this consolidation.** `CHANGES_09012026.md` and
  `CHANGES_09082026.md` exceed my fetch limit, and their prose tails were unreadable
  through every mirror attempted: `0908`'s full "Resolved: P1-10" narrative and the refuted
  hypothesis, and `0901`'s rows below `P3-5`. Everything in the readable portion of both
  tables is accounted for above. **Re-read those two tails and reconcile before treating §5
  as exhaustive.**
- **N-6 — `0831`'s numbering legend should survive the move.** `fix(#N)` and `fix:` markers
  are still in the source (`BoundingBox.Overlaps`, `AttributeColumn.Coerce`, `CsvHelper`,
  `Part.AddVertex`, `Feature`). Keep `0831` readable at `docs/reviews/` so those in-code
  markers stay decodable, or migrate the legend into §5 and re-tag the comments.

---

## 8. Suggested sequencing

1. **`D-A` and `D-B`** — the R-tree decision and the CI question. Both are decisions, not
   tasks, and four backlog rows wait on them.
2. **`P-03`, `P-04`, `P-05`** — the spherical-math P0s. Self-contained, no external
   dependency, and `P-04` is what PR #6 actually set out to ship.
3. **The two spikes (`D-C`, `D-D`)** — schedule them as timeboxed work, then estimate
   `P-06`, `P-14`, `P-20`, `P-29`.
4. **`P-12`** — small, unblocks test parallelism, and removes a workaround the code
   explicitly asks to have removed.
5. **`P-36`, `P-37`, `P-30`, `P-31`, `P-32`, `P-33`, `P-34`, `P-35`** — the consolidation and
   housekeeping set. Cheap, and they reduce the chance the next review produces another
   divergent set of files.