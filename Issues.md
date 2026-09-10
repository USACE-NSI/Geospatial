# Issues — living tracker

Single source of truth for known defects and open work.

Reviewed against `feature/remove-duplication` @ `04118f4` (PR #9, "migrating from rtree to
bbox" + "fixing p39 and empty bounding box gaurds"). **Build and suite verified green at
this SHA** — `total: 112, failed: 0, succeeded: 110, skipped: 2`, build succeeded with 16
warnings (N-11, section 9). PR #8 closed at `389dc4a`.

## How to use this file

- `P-xx` numbers are stable. Never reuse one. Do not renumber.
- Commit with `fix(P-xx)` in the message. The old `fix(#N)` markers in source refer to
  `CHANGES_08312026.md`'s numbering, **not** these numbers — see N-6.
- An item is closed only when a passing, un-skipped test guards the fix. Comments and
  commit messages are not evidence. A green suite is not evidence either — it proves
  nothing *broke*, not that anything is *guarded*. P-01 and P-66 both stay open on a
  branch where nothing fails, because the assertions that would guard them do not exist.
- Deletions are recorded inline against the item that asked for them, with the commit
  that performed them (`44fbf06`, PR #8 `051e8d8`, PR #9 `9c7124a`, `04118f4`, …). Do not
  delete the entry; mark it.
- A closed item keeps its entry, retitled to what the defect actually was. Several
  entries here were scoped by guess and turned out narrower (P-06) or wider (P-14), and
  two were wrong in the direction of overstating a mechanism (P-39's third bullet, P-01's
  proposed fix). Re-read an item's prose against the source before acting on it.
- **A local `dotnet test` run is not a property of a commit.** Two failures of attribution
  in a row (N-11) came from reading a working-tree run as a committed state. Before
  recording a pass or fail against a SHA, `git show <sha>:<path>`.
- Measurements that cannot be expressed as passing assertions are recorded as prose with
  their evidence and their citations, not as skipped or permanently-failing tests.
  See P-64 for the shapefile/GeoJSON int64 measurements. Where such a measurement has no
  repro in the tree, that is said explicitly (T-19).
- Section 10 is the recommended work order, not a priority list.
- P-54, P-55, P-59 and P-20 are cited by other entries but are not defined here. See N-12
  before assuming a number was skipped by accident.

---

## 0. Standing decisions

| ID | Decision |
|---|---|
| D-A | **The R-tree stays.** It is foundational, supports bulk add, and outperforms RBush and other .NET implementations. Every R-tree item below is fix work; deletion and replacement are off the table. |
| D-B | Skipped tests assert behaviour the library *should* have and does not. They are deliberate and are the spec for P-03 and P-04. **The skip budget is exactly two** (N-11); a third skip is a defect being hidden, not a test being deferred. Confirmed still exactly two at `04118f4`. |
| D-C | **Closed by PR #8 `9bee4e4`.** The binding surface is answered by code that compiles and tests that pass, not by further reflection — see N-14 for why this kept decaying. Settled against the installed GDAL 3.11.3 binding: `Feature.SetField(string, long)` exists and is the correct setter; the int64 getter is `GetFieldAsInteger64` and **`GetFieldAsLong` does not exist**; there is **no** `Layer.FieldIndex` and **no** `object` overload, so the comment in `SpatialWriter.SetOgrField` that this file spent two revisions calling into question was *correct*; and `Layer.GetLayerDefn()` returns `OSGeo.OGR.FeatureDefn` — **there is no `LayerDefn` type** — which production code never noticed because it binds everything with `var`. Neither `Layer.FieldIndex` nor an object overload was ever needed, so neither blocks anything. `CoordinateTransformationOptions` remains unexamined and now gates only P-20. The deleted spike (`ProbeOsrBinding.cs`, PR #8 `051e8d8`) is not worth recovering; `git show 051e8d8^:tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs` if ever needed. |
| D-D | Decide the CRS token/authority model. **Correction: the double-prefix defect this row asserted does not exist.** `CoordinateTransformer.CrsToken` tests `StartsWith("EPSG:")` and passes an already-prefixed token through unchanged, so `"ESRI:102003"` does **not** become `"EPSG:ESRI:102003"`, and T-15 as previously written would have passed without testing anything. The warning we blamed on it has a different cause, and it is real: **(a)** `Projection.AlbersUsa`/`Nad83` hardcode `EPSG:` for codes GDAL 3 attributes to `ESRI:` — hence `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is. Assuming ESRI:102003 was meant` on every run (still present at `04118f4`), with correct results reached only by GDAL's auto-correction. **(b)** `CrsInfo.EpsgCode` is `int?`, so a non-EPSG authority is *unrepresentable*: `CrsInspector` reads `GetAuthorityCode("PROJCS")`, gets `102003`, stores it in a field named `EpsgCode`, silently relabelling the authority — and `SpatialReader.SameCrs` then compares `102003 == 102003` as an authoritative match across two different authorities. **Fix:** carry authority and code as a pair, not more string handling. Gates P-14, T-15. |
| D-E | Decide whether the R-tree may index a geographic CRS. Its MBR math is planar; a degree-space box is not a metric box. Gates P-02 and P-44. **Sharpened by PR #9:** the box arithmetic is now all in `BoundingBox`, so this decision has one place to be implemented rather than two. |
| D-F | **New, from `04118f4`: the index rejects features it cannot place.** `addFeature` now throws on an extent-less or non-finite box rather than indexing it. That is the right default for a library, but it moves the policy question up a layer: a caller reading a file containing `POINT EMPTY` or a geometry type `ProcessGeometry` drops (P-21) now gets an exception instead of a degraded index. **Decide whether `BuildTree` filters-and-counts or propagates.** Recommended: filter, count, expose the count — see P-02. The green run at `04118f4` shows no existing input in this repository produces such a box, which means the decision is currently *unforced* and will be made by accident the first time real data arrives. |

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
at least one vertex. **Raised in severity by `04118f4`:** an empty part yields an `Empty`
feature box, and `addFeature` now **throws** on one. So an empty part is no longer merely
unindexable, it aborts the whole `BuildTree`. The filter-and-count decision (D-F) belongs
with this item's fix, not with the R-tree's.

### P-39 `BoundingBox.Empty` is the full-range box  *(partly mitigated by `04118f4`; type unchanged)*
`Empty = new(MaxValue, MaxValue, MinValue, MinValue)` and the constructor normalises, so
`Empty` spans `[-1.8e308, +1.8e308]` on both axes. **The sentinel itself is unchanged by
`04118f4`** — only the index was fenced off from it. Consequences still live:
- `Area()` overflows to `+infinity`; `Perimeter()` returns ~`7.2e308`. **`Area`,
  `Perimeter`, `ContainsPoint` and `EnlargementToContain` have no `== Empty` guard**,
  while `Overlaps`, `Contains`, `Union` and `OverlappingArea` do. Four unguarded members
  of one struct now disagree with three of their siblings about what the same value means:
  `OverlappingArea(Empty)` = 0, `Area()` = +inf, `Perimeter()` = 7.2e308.
- `ContainsPoint` returns true for every point, and is now the **only predicate in the
  file with no guard at all**.
- **The sentinel is not the only overflow source, and the new gate does not catch the
  others.** `Empty`'s coordinates are `±DBL_MAX`, which `double.IsFinite` accepts. So
  `new BoundingBox(-DBL_MAX, -DBL_MAX, DBL_MAX, DBL_MAX)` clears both `addFeature` checks
  and is `!= Empty`, yet `MaxX - MinX` is `DBL_MAX + DBL_MAX` = `+inf`, and it poisons the
  ranking exactly as `Empty` did. Narrow, but reachable from garbage coordinates in a
  projected CRS — the same input class that produces NaN, which *is* caught.
- **`bestCandidate ??= TreeManager.Root` is load-bearing from the very first insert, and
  nothing tests it.** On a fresh tree `Root.BoundingBox == Empty`, so `Root.Area` is
  `+inf`, so `getAddedSizeToAccomodate` returns `+inf`, and `+inf < double.MaxValue` (the
  initial `minExtension`) is **false** — the candidate is rejected and `bestCandidate`
  stays null. Worked: `inf + featArea − OverlappingArea(bbox)` where `OverlappingArea`
  returns `0` against `Empty` → `inf`. The insert still succeeds, *because* of the
  fallback. Anyone who reads `??= Root` as defensive cruft and deletes it breaks the first
  `addFeature` of every tree — and the suite would not catch it, because every tree test
  starts from `new RTreeManager()` and passes through this path invisibly. Pinned by T-22.

**Correction, retracted as written:** a previous revision of this entry claimed
"`Feature.AddPart` unions it, so one empty part swallows its feature's entire MBR."
**That is false.** `AddPart` is `BoundingBox = BoundingBox.Union(part.BoundingBox)` and
`Union` short-circuits — `return other` when `this == Empty`, `return this` when
`other == Empty` — so an empty part leaves a real feature box untouched. Verified against
the normalised `Empty` (`MinValue,MinValue,MaxValue,MaxValue`), which is what the
constructor produces from the `Empty` initialiser, so `== Empty` does match. `T-12` is
retitled accordingly.

**The escalation through the index, for the record** (steps 2–4 are now unreachable
through `addFeature`, but remain reachable through the public setters — P-15):
1. A feature with **zero parts** keeps `BoundingBox == Empty`. ~~`addFeature` validates
   nothing~~ **now throws** (`04118f4`).
2. `RecomputeMBR` rebuilds a node from `Children.Min(c => c.BoundingBox.MinX)` / `.Max(…)`
   — **not** `Union`, so it has none of `Union`'s guards. A full-range child forces every
   ancestor to full range.
3. Every ancestor's `Area` becomes `+inf`, so every candidate's `getAddedSizeToAccomodate`
   is `inf`, the `<` never fires, `bestCandidate` stays null, and the feature lands on
   `Root`. **One empty feature degenerates the index into a flat list.**
4. `inf + featArea − inf` is reachable as `NaN`, which also fails `<` and is skipped
   without a word.

**Invariant `04118f4` buys, and why it is worth keeping:** every leaf has four finite
coordinates and is not the sentinel ⟹ no internal node is ever `Empty` (`addChild` unions
a real box into a fresh `Empty` node, and `Union` returns the real box; `RecomputeMBR`
takes min/max of real boxes; `split` hands the same boxes to its two new nodes) ⟹
`Overlaps`' `Empty` guard is dead code on the search path, and P-66 is unreachable *through
the public API*.

**Fix, remainder:** an explicit `IsEmpty` flag, or a normalisation that keeps `MinX > MaxX`
inverted; `Empty`/overflow short-circuits in `Area`/`Perimeter`/`ContainsPoint`/
`EnlargementToContain`; and a magnitude bound or overflow check so the `±DBL_MAX` case
above is closed too.
**Sequencing:** settle P-62 in the same change, or the guards get written around members
that are about to be deleted.

---

## 2. P1 — correctness under load, or blocked features

### P-01 `getMBRoverlap` containment gate  *(reopened — deleted and redirected, but unguarded, and its guard was disarmed)*
`RTreeNode.getMBRoverlap` only tested whether a *corner* of the query lay inside the node,
so a node fully contained by the query returned 0 overlap. `CHANGES_09012026.md` P0-1
stated this defect inverted; `CHANGES_08312026.md` #15 stated it correctly (N-6, N-7).

**Done by `9c7124a`:** the member is deleted and all four call sites redirected — three
descents in `RTreeNode` plus the oracle in `RTreeTests.FeatureIndicesAt` use
`BoundingBox.Overlaps`; `getAddedSizeToAccomodate` uses `BoundingBox.OverlappingArea`.
`getMBRoverlap` appears nowhere in production source or either test assembly (confirmed:
no test at `04118f4` references it). This is N-13 resolved the right way on the *code*:
the survivor (`Overlaps`, written with a closed-interval test under `fix(#15)`) is the
correct one.

**Why it is reopened rather than closed — two reasons, both about the tests.**

1. **Nothing asserts `Overlaps`.** `GeometryTests` covers `OverlappingArea` with hand
   goldens; no theory anywhere asserts `Overlaps`' return for containment, for flush
   contact, or for `Empty`. The behaviour that *was* the defect — a query containing the
   node still overlaps — has no test. The green run proves `Overlaps` works on the inputs
   the suite happens to use, which is not the same statement.
2. **The commit made the R-tree's oracle self-referential.** `RTreeTests.FeatureIndicesAt`
   is the suite's brute-force check on `findByXY`, and it used to call `getMBRoverlap` — a
   *second, different, independently broken* implementation. That disagreement was
   accidental test power. It now calls `node.BoundingBox.Overlaps(bbox)`, i.e. the same
   predicate the traversal under test calls, so the oracle and the code agree by
   construction and a regression in `Overlaps` is invisible. Deleting a duplicate is
   supposed to *reduce* the chance of divergence; here it also removed the only cross-check.

**Fix:** T-21 (direct goldens for `Overlaps`, including the containment case) and P-48.2
(rebuild the oracle on an independent predicate — an inline interval test in the test
file, not a call into the type under test).
**Correction retained:** an earlier revision of this file said "delete it and call
`BoundingBox.Overlaps`, preserving the *return ≥ 1 when overlapping* convention" —
nonsense, since `getMBRoverlap` returns `double` and `Overlaps` returns `bool`. The floor
existed so point features were not pruned; `Overlaps`' closed interval serves that purpose
better. The floor is gone and should stay gone. What it *also* happened to prevent is
P-66.

### P-66 An `Empty`-boxed node or feature is silently pruned from index search  *(mitigated by `04118f4`, not closed — no test)*
`getMBRoverlap`'s `Math.Max(xAxisOverlap * YAxisOverlap, 1)` did double duty. Against an
`Empty` node the corner gate passed, both axis overlaps came out `0`, and the floor
returned **1** → descend. `BoundingBox.Overlaps` carries
`if (this == Empty || other == Empty) return false;` → **prune**, taking the whole subtree
with it.

**Closed in effect by `04118f4`, through the invariant in P-39:** `addFeature` rejects an
`Empty` or non-finite box, so no leaf can be `Empty`, so no internal node can become
`Empty`, so the guard never fires on the search path. This is the right fix — the
alternative was a policy that tolerates unknown extents, which is the floor by another
name.

**Why it stays open.** Not closed by this file's own rule, and the residue is concrete:
- **Zero coverage, and the green run is the proof.** `04118f4` touched no test file. The
  two guards are a new `throw` on a public API that **no existing test reaches in either
  direction** — nothing in the suite feeds `addFeature` an `Empty` box (so nothing
  verifies the throw), and nothing verifies that the throw doesn't fire on legitimate
  input either. A `throw` that no test touches can be deleted during any future cleanup
  without a single failure. T-18 is now three tests, not one, and it is the cheapest thing
  in this file.
- **The fences around the invariant are public.** `RTreeManager.Root { get; set; }` and
  `RTreeNode.BoundingBox { get; set; }` both still accept an arbitrary box, and
  `addChild` validates nothing. One assignment reintroduces silent subtree pruning with no
  diagnostic. P-15 is now the item that makes this invariant durable.
- **`±DBL_MAX` still gets through** (P-39) and produces the same silent-candidate-skip via
  `+inf` instead of via the guard.
- **No `Debug.Assert` was added** at the three descent sites, which was the cheap
  tripwire for the setter routes. Optional, but it is the only thing that would make a
  future violation visible in a debug run.
**Guard:** T-18. **Blocks:** nothing, but it is what makes `9c7124a`'s redirect safe, so it
inherits P-01's position in the ordering (section 10 step 11).

### P-02 Spatial joins discard the tree  *(mandatory)*
`SpatialJoins` enumerates features instead of descending the index. Blocked on P-47 (no
usable query API) and D-E. **P-01's gate is gone, so it no longer blocks this.**
The discarding is explicit and worth deleting with the fix: both join directions contain
`_ = tree ?? BuildTree(features);`, which constructs a whole R-tree and throws it away.
`pointTree` / `polyTree` are therefore pure cost today.
**New obligation:** whoever wires the tree up for real owns D-F. Since `04118f4`,
`BuildTree` over a dataset containing one extent-less feature throws instead of joining.
Filter-and-count there, and name the skipped ids — otherwise the first real caller of the
index turns a partial join into a crash.

### P-06 `SpatialReader` could not read `OFTInteger64` fields  *(closed by PR #8 `9bee4e4`)*
`SpatialReader.MapFieldType` had no `OFTInteger64` arm, so a 64-bit integer column in a
file authored elsewhere was typed `TextFT`, and `ReadFieldValue` accordingly returned a
boxed `string`. `GetAttribute<long>` produced its value only by accident, via
`Convert.ChangeType`; anything reading `Attributes[name]` and casting to `long` threw
`InvalidCastException`.

**Fix (read path only, two arms):** `OFTInteger64 => FieldType.LongFT` in
`SpatialReader.MapFieldType`, and `LongFT => feat.GetFieldAsInteger64(i)` in
`SpatialReader.ReadFieldValue`. `GetFieldAsDouble` is **not** an acceptable substitute —
it transits a `double` and loses the low digits above 2^53.

**Guard:** `FieldTypeTests.ReadsAnInteger64AuthoredElsewhere` — a hand-written GeoJSON
fixture (`"BIG":4000000000, "SMALL":7`), asserting `BIG` is boxed `long` with declared
`LongFT` and `SMALL` is still boxed `int`. **Non-circular by construction** — no writer is
involved — which supersedes the "the guard is circular" caveat in earlier revisions of
this entry, and closes what was planned as T-16. It also pins the `Integer`/`Integer64`
split that P-65's consolidation could collapse, so keep the `SMALL` assertion when the
mappers are merged. **Confirmed passing** — `Nsi.Geospatial.Io.Tests` succeeded at
`04118f4`.

**Correction:** earlier revisions of this entry named `LongColumnIsInteger64InGeoJsonToo`
as the guard and warned it was circular because it authored its own input. That test is
**not in the repository** — see N-11 for how that misunderstanding happened. The closure
claim stands on the fixture test above.

**Why a value-only assertion is worthless here, recorded so it is not re-litigated:** a
`LongFT` column *created as* `OFTString` round-trips `4000000000` perfectly, because
`Feature.GetAttribute<long>` falls through to `Convert.ChangeType("4000000000", long)`.
The digits were right the whole time the schema lied. Both the declared OGR field type
and the boxed CLR type must be asserted, or the test cannot fail. The surviving test does
assert both, which is what makes it adequate for the read path.

**Not covered by it:** the **write** arms that `9bee4e4` landed in the same commit
(`SpatialWriter.MapFieldType`'s `LongFT => OFTInteger64`, and `SetOgrField`'s `case long l:`
no longer casting to `int`) have **no test at all**. See P-68 / T-17.

**Split out of this item, deliberately not fixed:** the write side's representability is
**P-64**; the `FieldType`↔OGR↔CLR four-table consolidation is **P-65**. This defect
recurred precisely because of P-65: `LongFT` was missing from three of the four tables.

**Resolved as a side effect:** the comment above `SpatialWriter.SetOgrField` claiming
"no object overload, and no `Layer.FieldIndex` in 3.11.3" is correct (D-C) and must not be
restated as doubt. Its trailing `P0-4 … intentionally left unchanged in this class`
sentence is now false and should be replaced — see N-6 for the numbering collision.

### P-12 `Ogr.RegisterAll()` thread-safety
Called on every `Read` **and** on every `Write`. Not idempotent-safe under concurrent
use. `tests/Nsi.Geospatial.Io.Tests/AssemblyInfo.cs` disables test parallelisation as a
workaround and names the registration guard as the real fix; that guard is this item.
A fourth entry point into the same non-idempotent call has been proposed (opening datasets
directly from a probe); the probes are gone, so the count is back to the two production
callers plus `SpatialIoTests`. Re-count before fixing.

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
- `Projection.EpsgCode` is a `string?` carrying an authority-prefixed token, and three
  places independently build or parse that string: `CoordinateTransformer.CrsToken`,
  `SpatialReader.ParseEpsg`, `CrsInspector.ParseCode`. `SpatialReader` formats
  `$"EPSG:{code}"` in `ToProjection` and parses it straight back in `SameCrs` — a
  round-trip through a string inside one method body. Making the field an `int?` (plus a
  separate authority field) deletes all three parsers and the round-trip.
- **The authority half is now the substantive part** (D-D): the fixtures hardcode
  `EPSG:102003` for an `ESRI:` code, and `CrsInfo.EpsgCode` being `int?` makes any
  non-EPSG authority unrepresentable on read. The `EPSG:102003` warning line is still
  printed on every run at `04118f4` and is the cheapest reproduction in the repository.
- Guard with T-15 once the model is chosen, **rewritten** — the current wording asserts a
  defect that does not exist.
- Folded in: P-29 (`CoordinateTransformationOptions` / area-of-interest).

### P-15 `Root` public setter; no box validation  *(now the only route to P-66)*
`RTreeManager.Root { get; set; }` accepts any node; `RTreeNode.BoundingBox { get; set; }`
accepts any box; `addChild` validates neither. `04118f4` closed the `addFeature` route,
which was the *automatic* one, and left these three, which are the *manual* ones. The
invariant that makes P-66 unreachable is therefore maintained by one method and
assumptions, not by the type.
**Fix:** make `Root` set-once (or internal), make `RTreeNode.BoundingBox` settable only at
construction or through `Union`, and reuse `addFeature`'s two checks in `addChild`. Until
then, keep P-66's `Debug.Assert` suggestion live — it is the only detector.
See P-39 for what an empty box does, P-66 for what it costs. Split out: P-51.

### P-17 `Parts` public; cached boxes go stale
`Feature.Parts` is a public `List<Part>`. `Feature.BoundingBox` is maintained
incrementally by `AddPart`, so `feature.Parts.Clear()` or `.RemoveAt()` silently leaves
a stale MBR. `Part` now defends against exactly this pattern (`Vertices` is read-only,
`AddVertex` is the sole mutator) — `Feature` has not received the same treatment, and
`Part` has no channel to notify its owner. Apply the same shape: private list +
`IReadOnlyList<Part>` facade + `AddPart`/`RemovePart`.
Also two maintenance paths for one box: `AddPart` unions incrementally *and*
`ComputeBoundingBox()` rebuilds from scratch. Callers use both. Pick one.
**Note for whoever takes this:** the incremental path is *not* broken for empty parts —
`Union`'s guards make it correct (P-39's retraction). The defect here is staleness under
mutation, not miscomputation at construction. Don't go looking for the former.

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
**Raised in severity by `04118f4`:** a zero-part feature now carries an `Empty` box, and
`addFeature` **throws** on one. So a `wkbMultiLineString` in an input file is no longer a
silently-empty feature — it is an `ArgumentException` from inside the index, three frames
and one abstraction away from the `ProcessGeometry` branch that dropped it. This item and
the D-F filter decision should land together, or the first real dataset turns a silent
drop into a crash with a message about bounding boxes.

### P-40 All-leaves fallback makes the build quadratic
`insert` falls back to scanning all leaves; with P-41's enlargement metric this is
quadratic in feature count.

### P-41 `getAddedSizeToAccomodate` measures set-union area, not MBR enlargement
Chooses the child with the smallest value of `Area + featArea - BoundingBox.OverlappingArea(bbox)`.
`04118f4` renamed the inputs but left the expression, and it is worth naming precisely,
because the method name and its docstring both promise something else.

`Area + featArea − |A ∩ B|` **is the area of the true set-union** `|A ∪ B|`. What the
R-tree heuristic needs is **MBR growth**, `|bbox(A ∪ B)| − |A|`. Those coincide only when
`A ∪ B` is already a rectangle (nested pairs, flush-aligned pairs). Otherwise the bbox
union invents area that neither box contains:

- `A = [0,10]²`, `B = [20,30]²`: the method reports `100 + 100 − 0 = 200`; true MBR growth
  is `900 − 100 = 800`. Off by 4×.
- For a feature disjoint from *every* candidate the value reduces to `Area + featArea`, so
  across candidates it ranks by **smallest existing child**. That is exactly the bias this
  entry has always described, now with its mechanism named rather than guessed.

`9c7124a` was still a strict improvement: subtracting the true overlap instead of a
corner-gated `0` fixes the containment case (a node fully inside the feature no longer
looks maximal-cost). The disjoint case is untouched.
**Fix:** `return BoundingBox.EnlargementToContain(bbox);` — one line, replaces the whole
body, and removes the last hand-rolled box-area expression in the file
(`(bbox.MaxX - bbox.MinX) * (bbox.MaxY - bbox.MinY)` still duplicates `bbox.Area()` here
even after P-67). It also makes `OverlappingArea` load-bearing a second time, through
`Union`.
**Caveat for P-40:** enlargement is a *cheaper* comparison but does not change the
asymptotics.
**Guard:** T-20.

### P-42 `buildChildOptions` re-parents live children
While scoring candidate placements it re-parents children of the *real* node, then
discards the options. Mutates the tree during a read-only decision.
Now also the owner of the file's three remaining nullability warnings — its
`List<RTreeNode> sortedChidrens = null;` is `RTreeNode.cs:73`, one of the six real
warnings in section 9. Fixing this item properly (build the sort key, then order once)
removes the `null` initialisation as a side effect rather than annotating it.

### P-43 `Options.First()` throws; no min/max invariant
The split loop `for (split = MinChidrens; split <= Count - MinChidrens; split++)` is
empty when `Count < 2*min`. At split time `Count == max + 1`, so the invariant is
**`max >= 2*min - 1`**. Defaults (10, 4) and the tests' (6, 3) satisfy it; `new
RTreeManager(4, 6)` throws from inside `split()`. Validate in the constructor with a
clear message. Note the public constructor's parameter names (`minChilds`, `maxChilds`)
do not match the properties they assign (`MinChildren`, `MaxChildren`) — the misspelling
family in P-22 is wider than the `Chidrens` fields.
**Answered by the clean build:** the two `CA1805` warnings (`RTreeNode.cs:11,12`) confirm
both `MaxChidrens` and `MinChidrens` are explicitly `= 0` and then unconditionally
overwritten by the constructor, so neither has a meaningful default and the initialisers
are pure noise. Delete both.

### P-44 `SpatialJoins` uses planar distance regardless of CRS
Near-endpoint and proximity joins call planar `PointToSegmentDistance`/`Distance` even
when the collection is geographic. Should dispatch on `Crs.Kind` (the `Part` accessors
now show the pattern) or refuse.

### P-47 R-tree API gaps
No `Query(BoundingBox)`, no k-NN, no `Count`, and no way to get feature indices back out
of a traversal. `findByXY` is the only entry point and returns node-scoped results.
Prerequisite for P-02 and P-44. **PR #9 sharpens this:** `getCandidateEndNodesByMBR`
already takes a `BoundingBox`, so a rectangular query is one wrapper away — which means
P-01's containment gate would have gone live *through* the new API if it had survived.
**`04118f4` adds a second prerequisite:** a public `Query` becomes a public way to trigger
the `addFeature` gate's counterpart on the read side, so decide first whether a query box
that is `Empty` or non-finite throws or returns empty. Recommend returning empty — reads
should not throw where writes do, because the caller cannot fix a query the way it can fix
a feature. When `Query(BoundingBox)` lands, P-46's missing leaf test becomes live in the
same commit.

### P-48 The R-tree test suite cannot detect a regression
1. **No regression test for MBR propagation** (`16585ef`, closed as P-49). The fix that
   made the index return all features is unguarded.
2. **The brute-force oracle is no longer independent** — this got *worse* in `9c7124a`.
   `FeatureIndicesAt` used to cross-check `findByXY` against a second implementation
   (`getMBRoverlap`); it now calls `BoundingBox.Overlaps`, the same predicate the traversal
   uses. Rebuild it on an inline interval test written in the test file. Pairs with P-01.
3. No test for `min`/`max` invariant (P-43).
4. No test that the tree's boxes match the features they index. `RecomputeMBR`'s
   `Children.Min/Max` form versus `Union` is precisely where they could diverge, and
   P-39's `±DBL_MAX` case is a concrete input where the two give different-looking results.
5. **`04118f4` added a public `throw` with no test** — see T-18, and note the green run
   establishes this rather than merely leaving it open to doubt.
6. **The first-insert path is untested and depends on an `inf` comparison** (P-39's
   `??= Root` bullet). Every existing test that builds a tree exercises it, and none would
   notice if the fallback were removed. T-22.
7. `RTreeTests`' class docstring still claims the tests do not assert correct behaviour,
   which was made false by `16585ef`.
8. Superseded: ~~no test for the containment gate (P-01)~~ — P-01's gate is deleted; the
   surviving need is T-21.

### P-61 `Nsi.Geospatial.Reprojection` is a project holding one class  *(new, PR #8; second reason found at `04118f4`)*
After `051e8d8` deleted `Reprojector`, the project contains `CoordinateTransformer` plus a
206-byte csproj — and still costs a `Geospatial.slnx` node, a `ProjectReference` from
`Io`, and a CI `--include` target. The stated reason for the split (keeping core
GDAL-free) is already satisfied by `Nsi.Geospatial.Io`, and both projects require the
same native GDAL runtime, so the boundary buys nothing at build or run time.
**New, and it is a concrete cost rather than a tidiness argument:** the clean build shows
the GDAL NuGet package pulls **its own copy of the generated `GdalConfiguration.cs` into
`Reprojection` as well**, emitting 3× `CS8600` at
`Nsi.Geospatial.Reprojection/obj/.../GDAL/3.11.3/GdalConfiguration.cs(60,41/58/77)` —
identical to the three already counted against `Io`. So the split doesn't just cost a
solution node, it doubles the native dependency, the restore, the build time, and the
third-party warning noise. This is what P-30's scoped `NoWarn` has to work around twice
(see P-30's correction).
**Fix:** fold into `Nsi.Geospatial.Io`, delete the csproj and the solution entry — which
also halves P-30's problem. Public namespace change, same caveat as P-14: 0.x breaking
removal, document it.

### P-64 `SpatialWriter` accepts values the target format cannot represent, and says nothing  *(new, PR #8 `9bee4e4`)*
The writer will emit a value the chosen driver cannot store, produce a file whose schema
reads as correct, and return a plausible **wrong number** on re-read. No exception, no
warning, no diagnostic.

**Proven instance — GeoJSON, with our type mapping correct.** Writing
`1_000_000_000_000_000_007` to a `LongFT` column and reading it back yields
`1000000000000000000` while reporting declared `OFTInteger64` and a boxed `long`.
Correct schema, correct CLR type, corrupted digits: GDAL's GeoJSON reader parses JSON
numbers through a `double`, so anything above 2^53 (9 007 199 254 740 992) is rounded.
This is the worst failure profile in the library — undetectable by the type system, by the
schema, or by inspecting the output file.

**Evidence provenance, because it matters for re-verifying this.** The measurement was
made by a scratch test that is **not in the repository**: at `9bee4e4`,
`FieldTypeTests.cs` as committed contains only `ReadsAnInteger64AuthoredElsewhere`
(N-11). The corruption is real and the mechanism is documented below with its citations,
but there is currently **no repro in the tree** — a bisect or a reviewer cannot reproduce
it without writing the case again. T-19 asks for a committed one, as an `Assert.Throws`
once the writer rejects.

**Measured limits by driver.** The honest supported range is the intersection:

| | \|v\| ≤ 2^53 | \|v\| &gt; 2^53 |
|---|---|---|
| GeoJSON | digits and type preserved | digits silently lost |
| ESRI Shapefile (DBF) | digits preserved; declared type depends on field width | digits lost, type demoted |

**DBF width rule** ([RFC 31](https://gdal.org/en/stable/development/rfc/rfc31_ogr_64.html),
[shapefile driver](https://gdal.org/en/latest/drivers/vector/shapefile.html)): DBF stores
no field type, only width and decimals, so GDAL infers on read — an `N` field with 0
decimals is `OFTInteger` at width ≤ 9, **`OFTInteger64` at width 10–18, `OFTReal` above
18**. `OFTInteger64` columns are auto-extended "to 19 or 20 if needed", and that
auto-extension is what crosses the threshold. `ADJUST_TYPE=YES` is the documented open
option that rescans the DBF to recover the narrower type. Consequence for this library:
`SpatialWriter` calls `fdefn.SetWidth(c.Length)` with `Length == 0`, so the driver picks,
and **the width ceiling for a `long` is 18 — never 20.** A width of 20 *causes* the
demotion rather than preventing it. (An earlier revision of this analysis recommended 20;
the probe table disproved it — requested widths 0/6/10/18/19 all read back as 19 because
one 19-digit value in the layer drove every column.) The DBF *type* question is still
open for the same reason: that probe defeated itself, and the digits-above-2^53 loss was
the only clean measurement it produced.

**Fix:** reject loudly at write time instead of writing a file whose contents changed — a
per-driver representability check naming the driver. For `long`: support \|v\| ≤ 2^53 in
every reachable driver, throw above it. This is a *class*, not a `long` symptom —
oversized strings against a short `TextFT` width and out-of-range dates join it.
**Note on shape:** `addFeature`'s new gate is the same design, correctly done — reject at
the boundary, name the offending value in the message, and add a second check for the
adjacent failure mode (there: non-finite; here: width overflow). Copy that pattern.

**Still open inside P-64, same shape, untested:**
- `bool` is written as `"1"`/`"0"` into an `OFTString` column (`MapFieldType` has no
  `BooleanFT` arm) and `AttributeColumn.Coerce`'s `BooleanFT` branch uses `bool.TryParse`,
  which rejects `"1"`. A `bool` does not survive a round trip as a `bool` — it returns
  `null`. Needs `BoolFieldTests` mirroring `FieldTypeTests`, plus the three arms.
- `FloatFT` is written via `SetField(name, (double)fl)`, so a `float` returns as a boxed
  `double`. Whether that is acceptable is undecided; it is at least undocumented.

**Test debt:** once the writer rejects, an above-2^53 round trip must **not** be written as
a round-trip assertion — the correct test is `Assert.Throws`, which is *stronger*: it pins
the behaviour instead of merely declining to observe the corruption. `4_000_000_000` is the
positive golden (already covered by the read fixture), `9_000_000_000_000_000` the
maximum-safe boundary.

**Why P1 and not P0:** it requires the caller to write a type the library never advertised
support for. Reading is unaffected, and P-06 covers that.

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
**Same disease, newly visible in geometry core:** `OverlappingArea` deliberately carries no
unit interpretation (area in CRS units squared), and its docstring says so. It must not
acquire a `Math.Max(…, 1)`-style floor "for consistency" with the deleted `getMBRoverlap`
— `GeometryTests.OverlappingAreaIsSymmetricAndHandCorrect`'s point-inside row (`expected:
0`) is the guard against exactly that regression, and it is committed and passing.

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
level. **PR #9 half-closed this:** the *descent* now uses `BoundingBox.Overlaps`, so the
interior level is correct; the leaf acceptance in `getCandidateEndNodesByMBR` is still
unconditional (`nodeWalk.Add(this)` with no test at all). `BoundingBox.Overlaps`/`Contains`
remain the missing test at the leaf, and `Overlaps` is now proven live in three other
places, so the fix has no remaining excuse. Note the leaf-level fix is what makes a
rectangular `Query` (P-47) actually prune.

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

### P-62 `BoundingBox` public surface with no owner  *(extends N-1; `04118f4` adds one member)*
Four uncalled members were listed at `7b4c6aa`; `Overlaps` and `OverlappingArea` are now
called from production, and `Perimeter()` added in `04118f4` is called immediately through
`RTreeNode.Perimeter`, so it is not dead. Remaining:

- `Contains` → **keep**, P-46's leaf test. Still uncalled.
- `EnlargementToContain` → **keep**, P-41's fix. Still uncalled, and P-41 now has a
  one-line substitution ready.
- `ContainsPoint` → homeless, wrong for every point under P-39, and now the **only
  predicate in the file with no `== Empty` guard**. Either guard it and use it (joins and
  `findByXY` are candidates), or delete it.
- `FromVertices` → genuinely unreferenced with no planned consumer (`Part` maintains its
  MBR incrementally via `Union`; `Feature` recomputes by `Union`). Its
  `double.IsPositiveInfinity(minX)` check can never fire — `minX` starts at `MaxValue` and
  only decreases, so the empty-input path returns a normalised garbage box rather than
  `Empty`. **`04118f4` gives it a reason to exist:** if it is ever adopted, its
  empty-input branch must be fixed to return `Empty`, because `Empty` is now the value
  `addFeature` rejects. Delete, or justify, fix, and test it.
- **`Point(x, y)`** — never called, and `RTreeManager.findByXY` hand-writes
  `new BoundingBox(x, y, x, y)` three lines away. Use it there or delete it. It is also
  the natural way to write T-18's point-feature case.

**Telling:** `SpatialIoTests` needed a point-in-region check and hand-rolled
`PointInPolygon` rather than calling `ContainsPoint`. Same N-13 pattern as `Overlaps` vs
`getMBRoverlap`. Resolve inside P-39's change so guards are not written around members
about to go.

### P-65 Four hand-maintained field-type tables with nothing keeping them in agreement  *(new; extracted from P-06)*
The `FieldType`↔OGR↔CLR mapping is spread over `SpatialReader.MapFieldType`,
`SpatialWriter.MapFieldType`, `SpatialReader.ReadFieldValue` (which additionally decides
that `Double|Float|Numeric|Single` are all doubles) and `AttributeColumn.FieldTypeToType`.
Nothing cross-checks them, so adding a type means editing four files and forgetting is
silent. **P-06 is the proof, not the motivation:** `LongFT` was absent from three of the
four, and the reader half of the absence was a live defect for any file authored by
someone else.

**Fix:** one internal `OgrMapping` (or equivalent) holding both directions of each pair
adjacent, so a divergence is visible in the diff. Behaviour-preserving; no public surface
change. Land it with P-64, whose `BooleanFT`/`FloatFT` gaps are the same absence in the
other three tables. **Sequence after T-16/T-17** (read *and* write coverage), because a
consolidation with only one direction tested can silently break the untested one — which
is precisely the hole P-68 describes, and precisely the mistake P-01's reopening is about.

### P-68 `FieldTypeTests` is the residue of a larger file, and its docstring now lies  *(new, PR #8 `9bee4e4`)*
`FieldTypeTests.cs` as committed contains **one** test. The constants, aliases and
docstring are leftovers from the six-test version, and the drift is actively misleading:

- Class docstring: *"LongFT through the writer and reader (P-06)"*. **No writer is
  involved** — the sole test reads a hand-written GeoJSON string. The sentence describes
  the coverage the file no longer has, and it is the coverage P-06's closure might be
  read as claiming.
- Dead: `const string Column = "BIG"`, `const long AboveInt32` (the test writes
  `4_000_000_000L` inline), the `Feature` and `OgrFieldType` alias blocks, and
  `using OSGeo.OGR;` / `using Nsi.Geospatial.Geometry;`. The alias comment is three lines
  long and defends aliases that are no longer used.
- **The real cost:** the write arms `9bee4e4` landed in the same commit —
  `SpatialWriter.MapFieldType`'s `LongFT => OFTInteger64` and `SetOgrField`'s
  `case long l:` no longer casting to `int` — have **zero coverage**. P-06's closure is
  sound for the read path only; nothing prevents a revert of either writer arm shipping
  green — and it now demonstrably does ship green, since `Io.Tests` passes with exactly one
  test in this file. T-17 closes this and is the reason P-65 should follow it, not
  precede it.

**Fix:** delete the dead constants and aliases, retitle the docstring to what the file
tests, and add T-17. Keep the "two independent assertions" rationale — move it onto the
test that still earns it.

### P-67 `getArea` / `getPerimeter` duplicated `BoundingBox` arithmetic  *(closed by `04118f4` — duplication half; the `Empty` half moved to P-39)*
`RTreeNode` carried its own area and perimeter formulas, both `Empty`-blind, both feeding
`split()`'s tie-breaks (`totalArea` is the second sort key, `perimeterTotal` the third), so
one uninitialised child could dominate the split ranking silently.
**Deleted in `04118f4`:** `getArea` → `public double Area => BoundingBox.Area();`,
`getPerimeter` → `public double Perimeter => BoundingBox.Perimeter();`, with a new
`BoundingBox.Perimeter()` member. `buildChildOptions`, `addFeatureChild` and
`getAddedSizeToAccomodate` updated to the new names. One definition of box size now wins.
**Confirmed complete:** the clean build at `04118f4` compiles with no reference to the old
names anywhere in the solution, so the rename left nothing dangling.
**What is *not* fixed, and why the entry stays:** the `Empty`-blindness was not removed,
only relocated into `BoundingBox`, where it is now visible as a three-way disagreement
between adjacent members of one type — `OverlappingArea` returns `0` for `Empty`,
`Area()` returns `+inf`, `Perimeter()` returns ~`7.2e308`, all for the same value. That
contradiction is P-39's to settle; P-67 asked only for one definition, and got one.
**Breaking-change note:** `getArea`/`getPerimeter` were public members of a public class.
Renaming them to `Area`/`Perimeter` is a source-breaking API change like P-14's and P-61's
— note it before 0.1.x is consumed.

### P-30 README and warnings-as-errors  *(prescription corrected at `04118f4`)*
Four factual errors in the README; the spherical feature is undocumented.
`TreatWarningsAsErrors=false` in `Directory.Build.props` is legitimately required because
the GDAL NuGet package's own generated `obj/.../GDAL/3.11.3/GdalConfiguration.cs` emits
3× `CS8600` at line 60. Note that `[obj/**/*] generated_code = true` suppresses **CA
analyzers only** — compiler diagnostics still fire, which is why no `CA` warnings appear
from that file but three `CS` ones do.
**Correction to this entry's prescription.** It previously said to scope
`<NoWarn>$(NoWarn);CS8600</NoWarn>` to `Nsi.Geospatial.Io.csproj` only. The clean build at
`04118f4` shows that is **half a fix**: the package drops its own copy of the same
generated file into `Nsi.Geospatial.Reprojection` too, emitting the identical three
warnings at
`Nsi.Geospatial.Reprojection/obj/Debug/net8.0/NuGet/769849ABBBE99B7D/GDAL/3.11.3/GdalConfiguration.cs(60,41/58/77)`.
So the scoped `NoWarn` must be added to **both** `Nsi.Geospatial.Io.csproj` and
`Nsi.Geospatial.Reprojection.csproj` — or, better, P-61 folds Reprojection into Io and
one of the two copies disappears entirely, along with its restore and build cost. Do not
add a global `NoWarn`: `RTreeNode.cs` has three genuine `CS8600`s of its own (section 9)
that a global suppression would silence.
Full warning inventory with exact lines is in section 9.

### P-31 SDK policy
The pinned SDK version is stated five inconsistent ways across `global.json`, `ci.yml`,
`release.yml`, the README and `Directory.Build.props`.
Sixth inconsistency, and the local machine has already drifted: this run again reports
`xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.11)` while every project
targets `net8.0`. Nothing noticed, which is the point of pinning.

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
weaker form. Both assertions worth keeping were ported first, and the D-B spec tests
survived. Two files remain, still in two namespaces inside one assembly — which is *why*
the duplicate went unnoticed: the classes could not collide by name. Visible in this run:
`EarthRadiusFeetIsTheAuthalicRadiusInFeet` reports under `Nsi.Geospatial.Core.Tests` while
`GeometryTests` reports under `Nsi.Geospatial.Tests`.
**Grown by PR #8 `9bee4e4`:** `FieldTypeTests.cs` adds a **third** copy of
`TempDir()`/`Cleanup()`. Hoist before adding a fourth.
**Grown by PR #9 `9c7124a`:** `GeometryTests.cs` (new, 73 lines) lands in
`Nsi.Geospatial.Tests` — the *other* namespace — so it will not see the shared helpers
when they are hoisted, and the split survives a third file. It also opens with three
unused `using`s (`System.Collections.Generic`, `System.Linq`, `Nsi.Geospatial.Spatial`;
the file references only `BoundingBox` and `Xunit`), which is the exact defect class
`7b4c6aa` was deleting. Decide the namespace once, in the hoist.
**`04118f4` added no tests**, which is the substance of P-66's and P-01's open status: the
branch now contains a public `throw`, a public rename, and a new `BoundingBox` member with
no new assertion anywhere. The 112 tests that pass are all older than that commit.
**Still duplicated:**
- `Cell(lon, lat, w, h)` — byte-identical in `SphericalMetricsTests` and
  `CrsInspectionTests`. (`LonLatCell(lonMin, lonMax, latMin, latMax)`, the incompatible
  second signature, was removed in `7b4c6aa`; `Cell` is now the one convention. Do not
  reintroduce a bounds-based variant — the two forms produce different polygons from
  arguments that look interchangeable.)
- `IntoFeature(Part)` — identical in `SphericalMetricsTests` and `CrsInspectionTests`.
- `Ring` / `Polygon` / `Geographic()` / `Projected()` — re-derived per class.
- `TempDir()` / `Cleanup(dir)` — now in all three Io test classes.
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
**Correction:** an earlier revision of this entry warned that `9bee4e4` had added two more
`Assert.Fail` diagnostics as a *recurrence*. It had not — those existed only in the
working tree and were never committed (N-11). The warning was right as a rule and wrong as
a fact; the rule is what to keep: **a measurement that cannot be expressed as a passing
assertion belongs in prose here, not in the test suite.**

### P-38 `Feature.ShapeType` vs `FeatureCollection.ShapeType`
Two sources of truth, free to disagree.

### P-51 `FeatureIndex` unguarded `[0]`  *(P1 → P3, but now pays for itself)*
`getChildrenContainingInd` dereferences `FeatureIndex[0]` without a guard, but it is
unreachable through the public API: it needs mixed-level children, and splits always
produce same-level siblings.
**Promoted back on cost, not on risk:** the clean build identifies this exact line as
`RTreeNode.cs(288,13): warning CS8602 — Dereference of a possibly null reference`. Column
13 is `node` in `if (node.FeatureIndex[0] == ind)`, and `FeatureIndex` is `int[]?`. The
compiler has been flagging it on every build. One line —
`if (node.FeatureIndex is { Length: > 0 } && node.FeatureIndex[0] == ind)` — closes P-51
**and** removes one of the six real warnings, which makes it the cheapest warning fix in
the file and worth folding into the section-10 step-2 warning sweep rather than deferring.

### P-60 `PartType` lives in a namespace its consumers guess wrong  *(was N-9)*
`PartType` is in `Nsi.Geospatial.Enums` while its only consumer `Part` is in
`Nsi.Geospatial.Geometry`, so IDEs auto-suggest `Nsi.Geospatial.Geometry.Enums` — which
cost a real compile error during this branch. Decide the convention across `Enums/`.

### P-63 Writer/reader local hygiene  *(new, PR #8 review; alias advice reversed)*
- `SpatialWriter`'s polygon branch states the degenerate-ring guard twice with different
  messages and identical conditions (`i == 0 && verts.Count < 3`,
  `i > 0 && verts.Count < 3`). One guard naming the role in the message.
- **Do not add `using` alias blocks to `SpatialReader`/`SpatialWriter`.** This entry
  originally recommended them; the evidence says the opposite. Io is already 100 %
  consistent about fully-qualified `OSGeo.OGR.*`, and that style is load-bearing rather
  than accidental: inside `namespace Nsi.Geospatial.Io.Tests`, bare `Geometry` binds to
  the namespace `Nsi.Geospatial.Geometry` — C# resolves enclosing namespaces before
  consulting file-level `using` directives — so `using OSGeo.OGR;` is never reached and
  you get `CS0118: 'Geometry' is a namespace but is used like a type`. Bare `Feature`, by
  contrast, resolves to *OGR's*, because `Feature` is not a direct member of
  `Nsi.Geospatial`. Same file, opposite outcomes, one rule. An alias block would make
  `Feature` mean ours — a behaviour change, not a readability fix. Keep aliases only where
  both names are genuine members and the alias is the only disambiguator, i.e. `FieldType`
  (as in `FieldTypeTests`).
- **One correction, from PR #9:** a predicted `CS0120` on `BoundingBox.MinX` ("object
  reference required") never materialised — `04118f4` compiles clean.
  `BoundingBox.OverlappingArea(bbox)` inside `RTreeNode` is legal: `BoundingBox` there is
  the *property*, and member access on it resolves normally. Confusing; consider naming
  the property `MBR` when next in the file. **`04118f4` made the same line worse:**
  `Area + featArea - BoundingBox.OverlappingArea(bbox)` mixes a property named like the
  type, a local, and a property-then-method chain that reads as a static call. Rename the
  property and this line becomes readable.
- `dotnet format` details that will bite, both from `.editorconfig`: `indent_size = 2` for
  `*.cs`, and `csharp_style_var_when_type_is_apparent = true:warning`. `dotnet format`
  defaults to `--severity-level warn`, so explicit types where the type is apparent get
  rewritten and `--verify-no-changes` fails on them. That is why production code uses `var`
  everywhere, and it is the likeliest CI trip in any new test file.
- README calls the core project `Nsi.Geospatial.Core` while the assembly is
  `Nsi.Geospatial` (P-30). The split test namespaces in P-36 are the fossil record of
  that abandoned rename; settle the name once.

---

## 4. P3 — hygiene

### P-22 Naming and dead code
`MaxChidrens`/`MinChidrens` misspelling (`Children`) and `addFeatureChild` unreachable;
`cumulativeOverlap`/`siblingOverlap` written and never read.
**Deleted in `44fbf06`:** `Part.Direction`, `Part.BeginIndex`, `Part.EndIndex`.
**Deleted in PR #8 `7b4c6aa`:** unused `using`s from `RTreeManager`/`RTreeNode`, plus
`System.Xml`, `System.Xml.Linq`, and the redundant `using Nsi.Geospatial.Io;`.
**Deleted in PR #9 `9c7124a`:** `RTreeNode.getMBRoverlap` (−23, recorded under P-01).
**Deleted in PR #9 `04118f4`:** `RTreeNode.getArea` and `.getPerimeter`, replaced by
`Area`/`Perimeter` delegating to `BoundingBox` (P-67); new `BoundingBox.Perimeter()`.
**Corrected:** this entry used to list `Feature._crs` (P-55) as remaining. It is already
gone. P-55 is a dangling reference; see N-12.
**Remaining:**
- `addFeatureChild` is not merely unreachable, it is *misleading*: it contains an area
  tie-break (`extensionReq == minExtension && childnode.Area < (bestCandidate?.Area ?? …)`)
  that the live path `addFeatureChildEnforceIntersect` lacks, so a reader will assume the
  tie-break is active. `04118f4` renamed its internals without changing that. Delete it, or
  move the tie-break to the live path as its own change. It also calls
  `getAddedSizeToAccomodate`, so P-41's substitution changes its (dead) semantics too —
  say so in the commit so nobody resurrects it expecting the old ranking.
  **Deleting it also removes `RTreeNode.cs:146 CS8600`** (`RTreeNode bestCandidate = null;`)
  — i.e. one of the six real warnings is deleted rather than annotated. Prefer that route.
- `cumulativeOverlap` / `siblingOverlap` are assigned in `buildChildOptions` and never
  read — the split sorts on the local `overlap`/`totalArea`/`perimeterTotal` triple. Dead
  state carried by every node, and `04118f4` left both assignments untouched.
- `getAddedSizeToAccomodate` still hand-rolls `bbox`'s area instead of calling
  `bbox.Area()` — the last duplicate of that expression, and P-41 deletes the whole body.
- `FeatureCollection.Crs`'s setter reaches through `f.Parts` to call
  `Part.InvalidateMetrics()` — add `Feature.InvalidateMetrics()` and forward.
- New in `04118f4`: a **constant interpolated string with no interpolation hole** —
  `$"Feature has no extent and cannot be indexed."`. Drop the `$`. (Its sibling
  `$"Feature has non-finite extent {bbox}."` does interpolate and is fine.) No compiler
  warning for this in the clean build, so it is style-only, but it is free.

### P-23a `GeometryMath` walk loops duplicated four times
`ClosedWalk`/`SphericalPerimeter` are the same loop over `pts[i], pts[(i+1)%n]` differing
only in the per-edge metric; `OpenWalk`/`SphericalLength` likewise for the open walk.
`Area` and `Centroid` are the same shoelace loop, and `Part.Measure` calls **both**, so
every ring is walked twice to produce two values from one accumulated sum.
**Fix:** one private `Walk(pts, closed, edgeMetric)` and one
`Shoelace(pts) → (area, cx, cy)`. Behaviour-preserving: the public entry points stay, and
`SphericalPerimeterClosesTheRingEvenForAnOpenPolyline` pins the semantics that make the
merge safe. Also halves `Measure()`'s work.

### P-55, P-54, P-59, P-20 — cited but undefined. See N-12; do not reuse the numbers.

---

## 5. Partially complete — in flight

### Closed by PR #8 (`051e8d8`, `4a0ec8b`, `7b4c6aa`, `9bee4e4`, `389dc4a`) — net −483 lines of production/test code, plus the read-path fix
| Change | Recorded under |
|---|---|
| `Reprojector.cs` deleted: duplicate transform engine + hand-rolled P/Invoke (−164) | P-14 (half) |
| `ProbeOsrBinding.cs` deleted, fully commented-out spike (−115) | P-37 |
| `SpatialWriter.ClosedRing` / `.RingWkt` / `.Fmt` deleted (−16) | P-37, P-57 |
| `SphericalMathTests.cs` deleted, subsumed duplicate suite (−246) | P-36 |
| `LonLatCell` removed in favour of `Cell`; ported tests relocated; `"what"` labels named | P-36 |
| `CrsToken` `internal` → `private` (one caller, same file) | P-14 |
| Unused `using`s removed from `RTreeManager`, `RTreeNode`, `SpatialReader` | P-22 |
| `MapFieldType` `+ OFTInteger64 => LongFT`; `ReadFieldValue` `+ LongFT => GetFieldAsInteger64` | **P-06 closed** |
| `SpatialWriter.MapFieldType` `+ LongFT => OFTInteger64`; `SetOgrField` `case long l:` no longer casts to `int` | P-64 (partial); **untested** → P-68 |
| `D-C` answered by compiling, not by reflection | **D-C closed**, N-14 |
| `Issues.md` rewritten wholesale (+228/−47, `389dc4a`) | this file |

### PR #9 `9c7124a` ("migrating from rtree to bbox") — +100/−28, 4 files
| Change | Recorded under |
|---|---|
| `RTreeNode.getMBRoverlap` deleted (−23) | P-01 (code done, guard missing) |
| Three descents + the `RTreeTests` oracle redirected to `BoundingBox.Overlaps` | P-01, **P-48.2 (oracle lost its independence)**, P-46 (half) |
| `BoundingBox.OverlappingArea` added: `Empty` guard, per-axis clamp at `<= 0`, `dx * dy` | new member, live, **guarded by hand goldens** |
| `getAddedSizeToAccomodate` subtracts `BoundingBox.OverlappingArea(bbox)` | P-41 (improved, still open), P-66 |
| `GeometryTests.cs` added (73 lines, `Nsi.Geospatial.Tests`) | P-36 |

The three committed theories are the corrected set — `OverlappingAreaIsSymmetricAndHandCorrect`
(6 rows of hand-computed goldens, symmetry asserted both directions),
`OverlappingAreaAgreesWithEnlargementWhenTheUnionIsARectangle` (3 nested pairs), and
`OverlappingAreaAgainstEmptyIsZero`. All pass. The theory that failed in an earlier local
run, `OverlappingAreaIsSymmetricAndConsistentWithEnlargement`, asserted
`|B| − |A∩B| == |bbox(A∪B)| − |A|`, which is false unless `A ∪ B` is itself a rectangle —
`Union` returns the **bounding box** of the two, not their union region. Worked:
`A=[0,10]², B=[5,15]²` → bbox union `[0,15]²`=225, enlargement 125, while true union is
`100+100−25=175`; and `A=[0,10]², B=[20,30]²` → enlargement 800 vs true added area 100.
`OverlappingArea` was correct in both (symmetry passed on all five rows). **That false
identity is the same confusion that makes P-41 a real defect** — see its worked numbers.
Recorded because the near-miss is the lesson, and because it is N-15's third entry.

### PR #9 `04118f4` ("fixing p39 and empty bounding box gaurds") — 4 files, behaviour change on a public API
| Change | Recorded under |
|---|---|
| `addFeature` throws on `BoundingBox.Empty` | **P-66 mitigated**, D-F, P-11/P-21 (new throw path) |
| `addFeature` throws on any non-finite corner | P-66; **P-39 gains the `±DBL_MAX` gap** |
| `BoundingBox.Perimeter()` added | P-67 (closed for duplication) |
| `RTreeNode.getArea`/`getPerimeter` → `Area`/`Perimeter` properties delegating to `BoundingBox` | **P-67 closed**, P-22; breaking rename |
| `Issues.md` updated to `9c7124a` | this file |

**Commit-message accuracy, since the file's closure rule depends on it:** the subject
claims `fixing p39`. P-39 is **not** fixed — the sentinel is unchanged, `Area`,
`Perimeter`, `ContainsPoint` and `EnlargementToContain` are still unguarded, and no test
was added, so by the rule in "How to use this file" it cannot be closed. What landed is
the *containment* of P-39's worst consequence. Prefer `fix(P-66) (mitigation)` or
`refs P-39` in future messages; a bare `fix(P-xx)` is a claim this file will check.
Also: "gaurds" is a typo, in a branch that has an open item about typos (P-36).

**Build and suite state at `04118f4`: GREEN. Established, not assumed.**
`total: 112, failed: 0, succeeded: 110, skipped: 2`; build succeeded with 16 warnings.
The three risks this entry previously listed are answered:
1. ~~a test now hits the `throw`~~ — **no.** Zero failures, so no test feeds `addFeature`
   an `Empty` or non-finite box. Recorded as evidence for P-66: the guard is exercised in
   neither direction.
2. ~~a stale `getArea`/`getPerimeter` reference~~ — **no.** Both test assemblies and all
   three production assemblies compile.
3. ~~formatting of the new guard blocks~~ — **not evaluated.** `dotnet test` does not
   invoke `dotnet format`; the CI step remains unverified (section 9).

**What green does *not* establish:** that anything added since `4a0ec8b` is guarded. The
110 passing tests predate both PR #9 commits for the R-tree work; the only new assertions
in the branch are `GeometryTests`' ten cases on `OverlappingArea`. P-01, P-66, P-68 and
P-48 all remain open *because* of what the green run shows, not despite it.

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
| T-12 | ~~`EmptyPartDoesNotInflateFeatureBoundingBox`~~ **retitled — the described defect does not exist.** `Feature.AddPart` goes through `Union`, whose `== Empty` guards already make an empty part a no-op on a real box (P-39's retraction). What should be pinned instead: **`FeatureWithNoPartsHasNoUsableBox`** — a zero-part feature's box must be recognisably empty, and `addFeature` must refuse it (the refusal now exists; the assertion does not) | P-39, P-11 |
| T-13 | Degenerate ring `[A,B,A]` — assert whichever of `0.0` / `null` is chosen, in both CRS kinds | P-07 |
| T-14 | **`ReprojectToUsesTraditionalGisOrder`** — read a lon/lat fixture into a projected CRS and assert X is still longitude. Also closes N-2. The only path that pins axis order now, since the P/Invoke path that ignored it was deleted in PR #8 | P-14 |
| T-15 | ~~`CrsTokenDoesNotDoublePrefixANonEpsgAuthority`~~ **invalid as written — it passes today**, because the `StartsWith("EPSG:")` guard already exists (D-D). Rewrite once the model is chosen: `Projection("ESRI:102003")` resolves to the same SRS GDAL reports for `ESRI:102003` **without emitting a warning**, and `CrsInfo` round-trips the authority rather than relabelling it `EpsgCode` | D-D, P-14 |
| T-16 | ~~`ReadsAnInteger64AuthoredElsewhere`~~ **done — implemented in PR #8 `9bee4e4`, verified passing at `04118f4`.** Hand-written GeoJSON (`"BIG":4000000000, "SMALL":7`), asserts `BIG` boxed `long` / `Schema["BIG"].FieldType == LongFT` and `SMALL` still boxed `int`. Non-circular by construction. Keep the `SMALL` assertion through P-65's consolidation — it pins the `Integer`/`Integer64` split | P-06 |
| T-17 | **`LongFTWritesAsInteger64`** — write `4_000_000_000` to a `LongFT` column with our writer, then reopen and assert the **declared** OGR type is `OFTInteger64` (not `OFTString`/`OFTReal`) *and* the boxed CLR type is `long`. Currently **no test touches** `SpatialWriter.MapFieldType`'s `LongFT` arm or `SetOgrField`'s `case long l:`; a value-only assertion here would pass on a text column (P-06's rationale applies verbatim) | **P-68 — the write arms of `9bee4e4` are unguarded** |
| T-18 | **`IndexRejectsBoxesItCannotPlace`** — three cases, none currently present: (a) `Assert.Throws<ArgumentException>` on `addFeature(…, BoundingBox.Empty)`; (b) the same for a non-finite corner (`NaN`, `+∞`); (c) **a point-shaped feature still indexes and is found** — 50 `BoundingBox.Point(i, i)` inserts to force splits, then `Assert.Contains(99, FeatureIndicesAt(tree, 25, 25))`. (c) is the one that says "the `≥ 1` floor's stated purpose is covered, do not bring the floor back"; (a)/(b) are the only thing that will make the new `throw` survive a future cleanup — the green run proves it is currently reachable from no test at all | **P-66, P-39, D-F, P-48.5** |
| T-19 | **`RejectsALongTheDriverCannotStore`** — `Assert.Throws` on writing `1_000_000_000_000_000_007` per driver, plus the `9_000_000_000_000_000` maximum-safe boundary. Also puts a **repro in the tree** for P-64's measurement, which currently has none | P-64 |
| T-20 | `InsertionChoosesSmallestEnlargement` — a small feature must land in the smaller-enlarging node. Fails on today's `Area + featArea − overlap`, which reports set-union area (worked numbers in P-41) | P-41, P-48.4 |
| T-21 | **`OverlapsIsSymmetricAndTrueForContainment`** — hand goldens for `BoundingBox.Overlaps`, mirroring the `OverlappingArea` theory: containment **both directions** (the `fix(#15)` case, which is what P-01 was about), corner overlap, disjoint, flush contact → `true`, point-in-box → `true`, `Empty` either side → `false`. Nothing in either assembly asserts any of this today, which is why P-01 cannot close on a green branch | **P-01 (the missing guard)** |
| T-22 | `FirstInsertIntoEmptyTreeLandsOnRoot` — `new RTreeManager()` then one `addFeature`, assert `findByXY` finds it and `Root` has the feature's box. Currently passes **only** via `bestCandidate ??= TreeManager.Root`, because `Root.Area` is `+inf` and `+inf < double.MaxValue` is false; the fallback reads like dead defensive code | P-39, P-48.6 |
| T-23 | `OracleDoesNotCallThePredicateUnderTest` — not a new test so much as a rule for P-48.2: `FeatureIndicesAt` must decide membership with an interval test written in the test file, not `BoundingBox.Overlaps`. Then a scale test comparing `findByXY` against it over a few hundred boxes | P-48.2, P-01 |

---

## 8. Notes

- **N-1** ~~Five `BoundingBox` primitives are never called~~ **reduced to four uncalled
  members.** Called in production: `Overlaps` (three `RTreeNode` descents, the
  `RTreeTests` oracle), `OverlappingArea` (`getAddedSizeToAccomodate`), `Perimeter` (added
  `04118f4`, via `RTreeNode.Perimeter`). Uncalled: `Contains` (→ P-46),
  `EnlargementToContain` (→ P-41), `ContainsPoint` (homeless, and the last unguarded
  predicate), plus `FromVertices` and `Point`, which were never counted. Split decision:
  P-62. Three of the five have a ready one-line consumer, which is the cheapest kind of
  dead code there is.
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
  **Add two more to sweep:** `grep -rn "P0-4\|intentionally left unchanged"` (N-6); and
  `grep -rn "through the writer and reader"` — `FieldTypeTests`' class docstring describes
  a writer test that is not in the file (P-68).
  **Add a third:** `grep -rn "getArea\|getPerimeter\|getMBRoverlap"` — `04118f4` renamed
  the first two and `9c7124a` deleted the third. **Verified clean at `04118f4`** (the
  solution compiles, so no code reference survives); re-run for comments and changelogs.
- **N-5** `CHANGES_09012026.md` below P3-5 and parts of `CHANGES_09082026.md`'s narrative
  have never been readable through any fetch path. Do not assume they are empty.
- **N-6** Keep `CHANGES_08312026.md`'s `fix(#N)` legend: those markers live in
  `BoundingBox.Overlaps`, `AttributeColumn.Coerce`, `CsvHelper`, `Part.AddVertex` and
  `Feature`, and they index *that* file, not this one. `CHANGES_08312026.md` also claims
  the R-tree files are "restored verbatim from master, all original typos included" —
  false since `16585ef` and `ac3bb82`, and now doubly so: PR #9 deleted a method and
  renamed two more.
  **Live collision, worth fixing on sight:** `SpatialWriter.SetOgrField`'s comment carries
  `P0-4`, which reads as `P-04` in this file (`EarthRadiusFeet`) but means
  `CHANGES_08312026.md` item 4 (field typing). Two unrelated defects, one glyph, in the
  one method whose behaviour this branch just changed.
- **N-7** Timeline rule: `CHANGES_09012026.md`'s P0-1 blamed the containment gate for
  missing features; `16585ef` fixed the actual cause (MBR propagation) four and a half
  hours later and the changelog was never reconciled. Any row citing 0901 P0-1 must be
  re-read against P-49. **Now settleable:** the gate was real, was dormant, and is the
  thing `9c7124a` deleted — so the changelog's claim can be marked resolved with a
  reference to P-01 rather than left ambiguous.
- **N-8** Benchmark build and query separately. The RBush comparison may have measured
  `Load()` (bulk STR-style) rather than incremental `addFeature`, which would explain
  part of the gap and is exactly what P-40 will change.
- **N-10** `Part`'s metric cache depends on the invariant that geometry cannot change
  behind its back. That holds for `Part` (`Vertices` is read-only) but **not** for
  `Feature.Parts` (P-17) or `FeatureCollection.Features` (also a public `List`). Fixing
  P-17 should close the last hole.
- **N-11** **Attribution. Two failures of this in a row, now corrected and the mechanism
  recorded.**
  - `total: 107, failed: 5` was quoted as a property of `9bee4e4`. It was not: the
    committed `FieldTypeTests.cs` at that SHA contains one test
    (`ReadsAnInteger64AuthoredElsewhere`) and nothing that fails. The five failures were a
    working-tree state.
  - `failed: 2` was quoted as a property of `9c7124a`. It was not: the committed
    `GeometryTests.cs` contains the three corrected theories, and the failing
    `…ConsistentWithEnlargement` theory existed only in the working tree.
  Common cause: a local run was read as a claim about a commit. **Rule: `git show
  <sha>:<path>` before recording any pass or fail against a SHA.**

  **Verified state at `04118f4` (clean build, both test assemblies, this run):**
  `total: 112, failed: 0, succeeded: 110, skipped: 2`, duration 18.6s; build succeeded
  with 16 warnings. `Nsi.Geospatial.Tests` succeeded, `Nsi.Geospatial.Io.Tests` succeeded.
  Skip budget is exactly two, unchanged (D-B):
  `EarthRadiusFeetIsTheAuthalicRadiusInFeet` (P-04) and
  `PointToSegmentWhenFootIsBehindTheNearEndpointReturnsDistanceToA` (P-03). A third skip is
  a new defect being hidden. (`RTreeTests.BulkInsertAllFeaturesFindableByPoint` carries a
  commented-out `Skip` from `16585ef`; leave it as history, do not re-enable.)
  `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is…` still prints once
  per run (D-D) — a passing test that pollutes output.
  **Not established by this run:** the `dotnet format` step. `dotnet test` does not invoke
  it. Section 9's four format checks are still open.
- **N-12** Dangling `P-` references: **P-20** (cited by D-C), **P-54** (cited by P-13,
  T-8, and `Part`'s `_measuredCrs` comment), **P-55** (cited by P-22, and its subject
  `Feature._crs` no longer exists), **P-59** (cited twice by section 9) have no entry in
  this file. Either they were closed by deletion without the citations being cleaned, or
  they live in the unreadable changelogs (N-5). Do not reuse these numbers; restore the
  definitions or mark the citations dead. D-C's closure removes one citation to P-20 but
  P-20 itself is still undefined.
- **N-13** Duplication tends to arrive as a *second* correct implementation rather than a
  second broken one. Cases: `BoundingBox.Overlaps` vs `getMBRoverlap` (P-01),
  `BoundingBox.EnlargementToContain` vs `getAddedSizeToAccomodate` (P-41, open), two
  reprojection engines with different axis handling (P-14, resolved), and `Area`/
  `getArea` (P-67, resolved `04118f4`). When adding a predicate, grep for one that already
  exists — and when deleting a duplicate, check whether the survivor is the *correct* one.
  **Second lesson, from `9c7124a`:** also check what the deleted copy was *accidentally*
  protecting. `getMBRoverlap`'s `≥ 1` floor was wrong about points and accidentally right
  about unknown extents; removing it produced P-66 with no test changing colour. A deleted
  defensive kludge deserves a test in its own right, because nothing else records why it
  existed.
  **Third lesson, same commit, and the opposite direction:** also check whether the
  deleted copy was serving as a *second opinion in a test*. `FeatureIndicesAt` calling
  `getMBRoverlap` was a bug used as an oracle — and removing the bug removed the
  cross-check, leaving the oracle calling the predicate under test. Deleting a duplicate
  can reduce test power as well as code count. T-23.
- **N-14** Answer a binding-surface question by compiling against it, not by reflecting
  over it. D-C sat open across two revisions and talked itself into contradicting the
  0901 changelog, because each reflection pass produced another unreadable dump instead of
  a yes/no. Three lines of code settled it: `SetField(string, long)` exists, the getter is
  `GetFieldAsInteger64` (`GetFieldAsLong` does not exist), and `GetLayerDefn()` returns
  `FeatureDefn` (there is no `LayerDefn`). The compile errors that produced this —
  `CS0104` ambiguity, two `CS0246`s, `CS0118` — were the answer all along. Corollary: when
  a comment in production code contradicts a changelog, compile a probe before editing
  either.
  **Same mechanism, second payoff:** the clean build's warning list answered four questions
  this file had been hedging about — where the six real warnings actually are, whether the
  `getArea`/`getPerimeter` rename left anything dangling (it didn't), whether the new
  `throw` breaks any caller (it doesn't), and whether `Reprojection` is warning-free (it
  isn't). A build log is four numbers long. Run it before writing prose about state.
- **N-15** **Five recommendations from this review have been wrong while the code was
  right.** `SetWidth(20)` as a fix (it *causes* the demotion); "let GeoJSON carry the full
  `long` range", asserted twice; the inclusion–exclusion oracle (section 5); "delete
  `getMBRoverlap` and preserve the `≥ 1` convention" (a `double` is not a `bool`); and
  "`Feature.AddPart` lets an empty part swallow the feature's MBR" (`Union` guards it).
  Common shape in all five: a *relationship* asserted before it was worked out on
  concrete numbers — and in three of the five the arithmetic was four numbers long.
  **Rule now in force:** any claimed invariant, type correspondence, or
  guards-a-method-does-not-have claim must be worked through on one explicit case *in the
  same message*, so a bad one costs a read instead of a test run. Two of the five were
  additionally claims about what code does that were derived from a paraphrase rather than
  from the source — read the member, not its docstring, before describing it (which is
  also why P-68 exists).
  **Sixth entry, milder:** this file recorded the suite state at `04118f4` as "unknown,
  three risks" and enumerated what might break. All three were answerable by running the
  build, which took seconds. Hedging in prose where a command gives an answer is the same
  error as N-14, pointed at test output instead of a binding surface.
  **Counterweight, so this note does not become an excuse for timidity:** the two designs
  this file proposed and `04118f4` implemented as written — the two-check gate in
  `addFeature`, and collapsing `getArea`/`getPerimeter` into `BoundingBox` — hold up
  against the same arithmetic discipline, and the invariant they buy (no `Empty` leaf ⟹ no
  `Empty` internal node ⟹ `Overlaps`' guard is dead on the search path) checks out, and the
  branch is green with them in. The lesson is not "propose less"; it is "show the four
  numbers, then run the build".

---

## 9. Build hygiene  *(clean build at `04118f4` — all lines verified, no estimates)*

`Build succeeded with 16 warning(s)`: **10** in `Nsi.Geospatial`, **3** in
`Nsi.Geospatial.Reprojection`, **3** in `Nsi.Geospatial.Io`. The two `Cs8600` triples are
the same third-party file compiled twice; see P-30 and P-61.

**The six real `Nsi.Geospatial` warnings, with line content and fix.** Each line below was
matched to source by column arithmetic against the file at `04118f4`, so the column in
parentheses is the reported one:

| Location | Code | What it is | Fix |
|---|---|---|---|
| `RTreeNode.cs:19` (col 96) | CS8625 | `int[] featInd = null` default on a non-nullable parameter, in the `RTreeNode` constructor | `int[]? featInd = null` — `FeatureIndex` is already `int[]?`, so the parameter is simply mis-declared. One character. |
| `RTreeNode.cs:73` (col 38) | CS8600 | `List<RTreeNode> sortedChidrens = null;` in `buildChildOptions`, assigned in one of four `if` branches | Build the key selector, then one `OrderBy`: `var key = (xAxis, min) switch { (true, true) => (Func<RTreeNode,double>) (c => c.BoundingBox.MinX), … };` and initialise in the declaration. Silences the warning by deleting the `null`, and collapses 20 lines of branching to a 4-arm switch (P-42's file). |
| `RTreeNode.cs:146` (col 33) | CS8600 | `RTreeNode bestCandidate = null;` in **`addFeatureChild`** | **Delete the method** (P-22 — unreachable and misleading). Prefer deletion to annotating dead code with `RTreeNode?`. |
| `RTreeNode.cs:177` (col 31) | CS8600 | `RTreeNode bestCandidate = null;` in `addFeatureChildEnforceIntersect` — the *live* path | `RTreeNode? bestCandidate = null;`. Correct and free here, because the method already ends in `bestCandidate ??= TreeManager.Root`, which the nullable flow will now accept. |
| `RTreeNode.cs:288` (col 13) | CS8602 | `if (node.FeatureIndex[0] == ind)` in `getChildrenContainingInd` — `node` at col 13, `FeatureIndex` is `int[]?` | `if (node.FeatureIndex is { Length: > 0 } && node.FeatureIndex[0] == ind)`. **This is P-51.** One line closes a P3 defect and a warning at once. |
| `AttributeTable.cs:57` (col 11) | CA1854 | indexer access guarded by `ContainsKey` | `TryGetValue`. Mechanical. |

**The four style/perf warnings:**

| Location | Code | Fix |
|---|---|---|
| `RTreeNode.cs:11` (col 48) | CA1805 | `MaxChidrens` explicitly `= 0`. Delete the initialiser — the constructor overwrites it unconditionally, which also answers the default-value question P-43 raised. |
| `RTreeNode.cs:12` (col 48) | CA1805 | Same, `MinChidrens`. |
| `RTreeNode.cs:97` (col 44) | CA1829 | Col 44 is `Children` in `for (int split = MinChidrens; split <= Children.Count() - MinChidrens; split++)`. **Confirmed as the split-loop bound**, i.e. the hot path this file long suspected: `Enumerable.Count()` allocates an enumerator per iteration, on every split, during every insert. Hoist `int count = Children.Count;` before the loop. |
| `FeatureCollection.cs:8` (col 21) | CA1711 | Naming opinion, breaking rename. **Suppress with a rationale in `.editorconfig`; do not rename.** |

**Not ours, and now known to be doubled:** 3× `CS8600` at
`Nsi.Geospatial.Io/obj/Debug/net8.0/NuGet/98F74982D37CCAAA/GDAL/3.11.3/GdalConfiguration.cs(60,41/58/77)`
**and** the identical three at
`Nsi.Geospatial.Reprojection/obj/Debug/net8.0/NuGet/769849ABBBE99B7D/GDAL/3.11.3/GdalConfiguration.cs(60,41/58/77)`.
Package-generated, one per project that references the GDAL NuGet package. Cannot fail CI
while `TreatWarningsAsErrors=false`. Scoped `NoWarn` in **both** csprojs per P-30's
correction, or delete one copy via P-61.

**Path to `TreatWarningsAsErrors=true`:** the six above are 1 line, 1 switch, 1 deletion,
1 annotation, 1 guard, 1 mechanical — all of them except the `buildChildOptions` switch
are single-line edits, and two of them (146, 288) close existing items P-22 and P-51.
Suppress `CA1711`, scope the `NoWarn` twice, then flip the flag.

- **`dotnet format Geospatial.slnx --verify-no-changes --no-restore` — still unverified.
  `dotnet test` does not run it.** Four checks, cheapest first, all on the `04118f4` head:
  1. `GeometryTests.cs`'s three unused `using`s (P-36) — if `IDE0005` is elevated in
     `.editorconfig` this fails on a file added two commits ago.
  2. The second trailing newline on `BoundingBox.cs`, `GeometryTests.cs`, `RTreeTests.cs`.
  3. `RTreeManager.addFeature`'s guard blocks — the wrapped multi-line
     `if (!double.IsFinite(…) || …)` is the shape most likely to be re-wrapped, plus the
     `$"…"` with no interpolation hole (P-22).
  4. `FieldTypeTests.cs`'s dead aliases and the comment splitting its `using` block into
     two sort groups (P-68).
  Note PR #8 relocated test bodies by hand (`7b4c6aa`) and PR #9 added a whole file; both
  are the classic csharpier failure. Run it at a known SHA and quote the SHA.

---

## 10. Recommended order

0. ~~Establish the build state at `04118f4`~~ **DONE — green: 112 / 110 / 2 skipped / 0
   failed** (N-11). Section 9 now has verified warning lines. One item remains from this
   step: the `dotnet format` check, which `dotnet test` does not cover.
1. **T-18 + T-21 + T-22 — three small tests, and they close the gap `04118f4` opened.**
   The branch ships a public `throw`, a public rename, and a new `BoundingBox` member with
   **zero new assertions**, and the green run proves the throw is unreachable from every
   existing test. T-18 protects the gate from being "simplified" away; T-21 is the guard
   P-01's closure requires and the only direct coverage of `Overlaps`; T-22 pins the
   first-insert path that depends on an `inf` comparison. All three are
   `GeometryTests`/`RTreeTests` additions, no production change, and none can fail on the
   current code — they can only fail *later*, which is the point.
2. **Warning sweep to `TreatWarningsAsErrors=true`** using the table in section 9. Fold in
   the two items the sweep pays for: **P-51** (the `CS8602` guard) and **P-22**'s
   `addFeatureChild` deletion (the `:146` warning). Add the scoped `NoWarn` to **both**
   `Io` and `Reprojection`, and the `CA1711` suppression. Also: `CA1829`'s hoist at line 97
   is a real hot-path win, not just a warning fix.
3. **P-68 + T-17** — small, and it closes the one coverage hole PR #8 opened (untested
   write arms). Do it before P-65 so the consolidation starts with both directions pinned.
4. P-53 + P-36 (one `AssertRel`, shared geometry helpers, shared golden constants, the
   triplicated `TempDir`/`Cleanup`, and **settling the two-namespace split** before a
   fourth file lands on the wrong side) — needed *before* the zero-assertion tests land,
   or they demand bit-exactness.
5. P-56 + P-57 together (storage convention), then T-1…T-4, T-7, T-13.
6. **T-5** — the read-path hole test. Highest value per line in this file.
7. **T-14** — axis order. One test, closes N-2, and guards the sole remaining transform
   path. Cheap enough to fold into step 6.
8. P-05 remainder (`Parts[0].IsHole` check, null-hole handling), then **P-21 + D-F
   together**: the silent geometry drop and the filter-and-count policy for extent-less
   features are the same user-visible failure now that `addFeature` throws. Fixing one
   without the other converts a silent drop into a crash.
9. T-8…T-11, then P-17 so the cache invariant has no remaining hole (N-10).
10. P-04 and P-03 — the two remaining skips, and P-04 is PR #6's stated purpose.
11. **R-tree cluster: P-39 + P-62 + P-15 + P-66's residue as one box-model change → P-43 →
    P-48 (incl. P-48.2 / T-23) → P-41 + T-20 → P-47 → P-44 → P-02 →
    P-42/P-46/P-50/P-51**, with D-E resolved before P-02 and D-F resolved before P-02's
    tree wiring.
    - **The gate landed, so the cluster's shape changed.** P-66 is mitigated rather than
      closed; what remains is (a) the public setters that bypass the invariant — now P-15's
      job, and the highest-value remaining R-tree item, because it is what makes the
      invariant durable rather than incidental — and (b) the `±DBL_MAX` overflow that the
      finite check lets through.
    - P-39's change must bundle: the `IsEmpty` representation, guards in
      `Area`/`Perimeter`/`ContainsPoint`/`EnlargementToContain`, the overflow/magnitude
      bound, and P-62's keep-or-delete decisions. One coherent change to the box model.
      With `OverlappingArea`'s semantics settled and tested, and `Area`/`Perimeter`
      finally single-sourced, this is smaller than it was — the guards go in one file.
    - **P-41 last**, because `EnlargementToContain` should not gain a caller until `Empty`
      and overflow are coherent — substituting it now routes both through `Union`/`Area`
      with no guard on either side.
12. Field typing, once, in one change: **P-65** (one mapper, both directions adjacent) with
    **P-64** (reject unrepresentable values; `bool` and `float` arms; `BoolFieldTests`;
    T-19). Both are cheapest immediately after T-16 and T-17 pin both directions, and
    splitting them risks a second round of the same three-file hunt.
13. Structural cleanups while their files are already open: **P-61** (fold the Reprojection
    project into `Io` — now with a measured payoff: one fewer GDAL package copy, one fewer
    `GdalConfiguration.cs`, three fewer warnings, one fewer restore, one fewer CI target),
    P-23a (`Walk`/`Shoelace`), P-63 (writer/reader hygiene — no alias blocks; rename
    `RTreeNode.BoundingBox` → `MBR`), P-26 (join duplication). All behaviour-preserving.
14. Everything else as touched.