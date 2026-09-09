# Issues — living tracker

Single source of truth for known defects and open work. 

Reviewed against `feature/spherical` @ `3f62485` (PR #6).

## How to use this file

- `P-xx` numbers are stable. Never reuse one. Do not renumber.
- Commit with `fix(P-xx)` in the message. The old `fix(#N)` markers in source refer to
  `CHANGES_08312026.md`'s numbering, **not** these numbers — see N-6.
- An item is closed only when a passing, un-skipped test guards the fix. Comments and
  commit messages are not evidence.
- Section 10 is the recommended work order, not a priority list.

---

## 0. Standing decisions

| ID | Decision |
|---|---|
| D-A | **The R-tree stays.** It is foundational, supports bulk add, and outperforms RBush and other .NET implementations. Every R-tree item below is fix work; deletion and replacement are off the table. |
| D-B | Skipped tests assert behaviour the library *should* have and does not. They are deliberate and are the spec for P-03 and P-04. |
| D-C | Spike needed: confirm the GDAL 3.11.3 binding surface (`Layer.FieldIndex`, object overloads, `CoordinateTransformationOptions`). Gates P-06 and P-20. |
| D-D | Decide the CRS token/authority model. `Reprojector.CrsToken` prepends `"EPSG:"`, so `"ESRI:102003"` becomes `"EPSG:ESRI:102003"`. GDAL now warns about this on every run (see the `EPSG:102003` line in test output). Gates P-14. |
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

### P-02 Spatial joins discard the tree  *(mandatory)*
`SpatialJoins` enumerates features instead of descending the index. Blocked on P-01,
P-47 (no usable query API) and D-E.

### P-12 `Ogr.RegisterAll()` thread-safety
Called on every `Read`. Not idempotent-safe under concurrent reads.

### P-14 Retire `Reprojector`'s hand-rolled P/Invoke  *(needs D-D)*
Also fix the authority model. Folded in: P-29 (`CoordinateTransformationOptions` /
area-of-interest, re-rated P2 → merged here so reprojection correctness is done once).

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

### P-18 Identity and `RemoveFeature`
`RemoveFeature` renumbers surviving ids and leaves the detached feature's `Owner`
pointing at the collection, so it still resolves the old CRS. `RemoveFeatureLeavesThe
DetachedFeatureResolvingTheOldCrs` pins this deliberately. It also means the detached
feature is outside `FeatureCollection.Crs`'s invalidation sweep — currently rescued only
by `Part`'s `CrsInfo` identity check, which is now the sole defence. Pin that with a
test. `SpatialJoins.BuildTree` keys on `f.Id`, which mutates under `RemoveFeature`.

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

### P-26 `interiorOnly` / `ContainsIndex`; delete `Feature.Wkt`
`Feature.Wkt` is dead state that goes stale the moment vertices change.

### P-27 DBF column order
Round-tripped schema order differs from the source.

### P-28 Null semantics: `""` vs `null`
Reader and writer disagree; a null text field comes back as empty string.

### P-46 `getCandidateEndNodesByMBR` performs no MBR test at the leaf
Descends using MBRs then accepts every leaf entry, so the pruning is illusory below one
level.

### P-50 `getIsEndNode` inspects only `Children[0]`
Assumes all children of a node are at the same level. True today because splits only
produce same-level siblings; unguarded and will silently mis-classify if that changes.
Assert it.

### P-53 `Rel` cannot compare against zero  *(both test files)*
`diff <= Math.Abs(expected) * tol` degenerates to `diff <= 0` when `expected == 0`, and
the message divides by zero (`rel diff ∞` — observed). Several planned acceptance tests
assert zero lengths and zero areas and will silently demand bit-exactness.
**Fix:** `Assert.True(diff <= Math.Max(Math.Abs(expected) * relTol, absTol), ...)` with an
explicit `absTol` chosen per unit (≈1e-6 m, not 1e-9 scaled from degrees). Belongs in the
shared helper from P-36.

### P-55 `Feature._crs` is a dead field  *(new, `3f62485`)*
`private CrsInfo _crs = Projections.CrsInfo.Unknown;` in `Feature.cs` is never read —
`Feature.Crs` is still `Owner?.Crs ?? CrsInfo.Unknown`. Expect `CS0414`. Delete it: it
was copied from the `FeatureCollection` snippet, and it implies features can carry their
own CRS, which is not the design.

### P-56 Ring storage is mixed open/closed  *(new, `44fbf06`)*
`Seal()` no longer strips a duplicate closing vertex and the reader does not normalise,
so **authored rings are stored open (`Count == n`) and read-back rings closed
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

### P-59 `.editorconfig` indent rules conflict for C#  *(new)*
`[*]` sets 4, then `[*.{csproj,sln,props,targets,xml,yml,yaml,json,cs}]` sets 2 — and
that brace list includes `cs`, so `.cs` resolves to 2 (the `[*.cs]` section sets no
`indent_size`). The committed files appear to use 1 space. `dotnet format` will therefore
want to reindent the whole solution, and CI runs it as a hard gate. Pick one width and
land it as a single formatting-only commit.

---

## 4. P3 — hygiene

### P-22 Naming and dead code
`MaxChidrens`/`MinChidrens` misspelling (`Children`) and `addFeatureChild` unreachable;
`cumulativeOverlap`/`siblingOverlap` written and never read; unused `System.Xml`,
`System.Xml.Linq`, `System.Text`, `System.Threading.Tasks`.
**Deleted in `44fbf06`:** `Part.Direction` (write-only after the `IsHole` derivation was
removed; winding survives as vertex order, and one fewer `IsClockwise()` P/Invoke per
ring), `Part.BeginIndex`, `Part.EndIndex`.
**Remaining:** `Feature._crs` (P-55), plus `FeatureCollection.Crs`'s setter reaching
through `f.Parts` to call `Part.InvalidateMetrics()` — add `Feature.InvalidateMetrics()`
and forward, so the collection does not enumerate another type's internals.

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
`Nsi.Geospatial.Core.Tests`), and **two independent copies of `Rel`** plus two copies of
`PointInPolygon`. Every fix has to be applied twice (P-53 already does). Hoist one
`TestAssert` and shared geometry helpers.

### P-37 Delete probes and dead writer helpers
`ProbeOsrBinding.cs`; `SpatialWriter.ClosedRing`, `.RingWkt`, `.Fmt`.

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

---

## 5. Partially complete — in flight

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

---

## 8. Notes

- **N-1** Three correct `BoundingBox` primitives are never called: `Overlaps`,
  `Contains`, `EnlargementToContain`. They are the fixes for P-01, P-41 and P-46.
- **N-2** `Transformer_EatsLonLatNotLatLon` was described but never added.
- **N-3** Three docstrings describe signatures that no longer exist. Verified still
  stale: `SpatialJoins.BuildTree` (documents the `addFeature` argument order `a6f3def`
  deleted). Verify with `grep -rn "CloseRing" --include=*.cs .` — expected hits are only
  the `SpatialReader.ProcessGeometry` docstring ("*CloseRing runs after any transform*",
  "*Direction must come from the source*", both about removed code) and `P-37`'s
  `SpatialWriter.ClosedRing`.
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

---

## 9. Build hygiene

- **Warnings (10 known, `Nsi.Geospatial` only).** Six are real: `CS8625` (RTreeNode.cs:26,
  `null` default on a non-nullable parameter) and `CS8600` ×3 (80, 153, 184) and `CS8602`
  (295) — read all four before annotating; a wrong `!` is a latent NRE inside the index.
  `CA1854` (`AttributeTable.cs:57`, use `TryGetValue`). `CA1805` ×2 (`RTreeNode.cs:18,19`,
  `Max/MinChidrens` explicitly `= 0` — check which is the real default, against P-43).
- **`CA1829` at `RTreeNode.cs:104` may be a hot path.** If line 104 is the split loop's
  bound, `Children.Count()` allocates an enumerator on every iteration of a loop that
  runs on every insert. Hoist it.
- **`CA1711` (`FeatureCollection` naming) is a design opinion and a breaking rename.**
  Suppress with a rationale in `.editorconfig`, do not rename.
- These have been visible on every clean build; incremental builds hid them because
  `Nsi.Geospatial` was not recompiling. Once the six are fixed, flip
  `TreatWarningsAsErrors` to `true` with the scoped `NoWarn` from P-30.
- `dotnet format Geospatial.slnx --verify-no-changes --no-restore` is a hard CI step and
  has been failing. See P-59 for why the diff may be much larger than expected.

---

## 10. Recommended order

1. Green build and format: P-55, the six real warnings, `CA1711` suppression, P-59, then
   `TreatWarningsAsErrors=true`.
2. P-53 (shared `Rel`, part of P-36) — needed *before* the zero-assertion tests land.
3. P-56 + P-57 together (storage convention), then T-1…T-4, T-7, T-13.
4. **T-5** — the read-path hole test. Highest value per line in this file.
5. P-05 remainder (`Parts[0].IsHole` check, null-hole handling), P-21 (silent geometry
   drop — likely to surface as T-3 failures).
6. T-8…T-12, then P-17 so the cache invariant has no remaining hole (N-10).
7. P-04 and P-03 — the two remaining skips, and P-04 is PR #6's stated purpose.
8. R-tree cluster: **P-39 → P-01 → P-43 → P-48 → P-40/P-41 → P-47 → P-44 → P-02 →
   P-42/P-46/P-50/P-15/P-51**, with D-E resolved before P-02.
9. Everything else as touched.