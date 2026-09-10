# Issues — living tracker

Single source of truth for known defects and open work.

Reviewed against `feature/remove-duplication` @ `9c7124a` (PR #9, "migrating from rtree to
bbox"). PR #8 closed at `389dc4a`.

## How to use this file

- `P-xx` numbers are stable. Never reuse one. Do not renumber.
- Commit with `fix(P-xx)` in the message. The old `fix(#N)` markers in source refer to
  `CHANGES_08312026.md`'s numbering, **not** these numbers — see N-6.
- An item is closed only when a passing, un-skipped test guards the fix. Comments and
  commit messages are not evidence.
- Deletions are recorded inline against the item that asked for them, with the commit
  that performed them (`44fbf06`, PR #8 `051e8d8`, PR #9 `9c7124a`, …). Do not delete the
  entry; mark it.
- A closed item keeps its entry, retitled to what the defect actually was. Several
  entries here were scoped by guess and turned out narrower (P-06) or wider (P-14), and
  two were wrong in the direction of overstating a mechanism (P-39's third bullet, P-01's
  proposed fix). Re-read an item's prose against the source before acting on it.
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
| D-B | Skipped tests assert behaviour the library *should* have and does not. They are deliberate and are the spec for P-03 and P-04. **The skip budget is exactly two** (N-11); a third skip is a defect being hidden, not a test being deferred. |
| D-C | **Closed by PR #8 `9bee4e4`.** The binding surface is answered by code that compiles and tests that pass, not by further reflection — see N-14 for why this kept decaying. Settled against the installed GDAL 3.11.3 binding: `Feature.SetField(string, long)` exists and is the correct setter; the int64 getter is `GetFieldAsInteger64` and **`GetFieldAsLong` does not exist**; there is **no** `Layer.FieldIndex` and **no** `object` overload, so the comment in `SpatialWriter.SetOgrField` that this file spent two revisions calling into question was *correct*; and `Layer.GetLayerDefn()` returns `OSGeo.OGR.FeatureDefn` — **there is no `LayerDefn` type** — which production code never noticed because it binds everything with `var`. Neither `Layer.FieldIndex` nor an object overload was ever needed, so neither blocks anything. `CoordinateTransformationOptions` remains unexamined and now gates only P-20. The deleted spike (`ProbeOsrBinding.cs`, PR #8 `051e8d8`) is not worth recovering; `git show 051e8d8^:tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs` if ever needed. |
| D-D | Decide the CRS token/authority model. **Correction: the double-prefix defect this row asserted does not exist.** `CoordinateTransformer.CrsToken` tests `StartsWith("EPSG:")` and passes an already-prefixed token through unchanged, so `"ESRI:102003"` does **not** become `"EPSG:ESRI:102003"`, and T-15 as previously written would have passed without testing anything. The warning we blamed on it has a different cause, and it is real: **(a)** `Projection.AlbersUsa`/`Nad83` hardcode `EPSG:` for codes GDAL 3 attributes to `ESRI:` — hence `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is. Assuming ESRI:102003 was meant` on every run, with correct results reached only by GDAL's auto-correction. **(b)** `CrsInfo.EpsgCode` is `int?`, so a non-EPSG authority is *unrepresentable*: `CrsInspector` reads `GetAuthorityCode("PROJCS")`, gets `102003`, stores it in a field named `EpsgCode`, silently relabelling the authority — and `SpatialReader.SameCrs` then compares `102003 == 102003` as an authoritative match across two different authorities. **Fix:** carry authority and code as a pair, not more string handling. Gates P-14, T-15. |
| D-E | Decide whether the R-tree may index a geographic CRS. Its MBR math is planar; a degree-space box is not a metric box. Gates P-02 and P-44. **Sharpened by PR #9:** the box arithmetic is now all in `BoundingBox`, so this decision has one place to be implemented rather than two. |

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
at least one vertex. **PR #9 raises the stakes:** an empty part yields an `Empty` feature
box, which P-66 shows is now pruned from index search rather than traversed. Fixing the
reachability at `addFeature` is the shared fix for both.

### P-39 `BoundingBox.Empty` is the full-range box
`Empty = new(MaxValue, MaxValue, MinValue, MinValue)` and the constructor normalises, so
`Empty` spans `[-1.8e308, +1.8e308]` on both axes. Consequences, all live:
- `Area()` overflows to `+infinity`; `RTreeNode.getArea` returns `+infinity` (its
  `MaxX < MinX` guard can never fire, because normalisation guarantees `MaxX >= MinX`);
  `getPerimeter` returns ~`7.2e308`.
- `ContainsPoint` returns true for every point. **`Area`, `ContainsPoint` and
  `EnlargementToContain` have no `== Empty` guard**, while `Overlaps`, `Contains`,
  `Union` and — as of PR #9 `9c7124a` — `OverlappingArea` do. P-39 used to name two
  unguarded members; `EnlargementToContain` joins the list because it routes through
  `Union` and `Area()`, so `Empty` on either side yields `±inf`/`NaN`.
- `Empty` is every node's initial box and every empty `Part`/`Feature` box, and
  `addFeature` validates nothing.

**Correction, retracted as written:** a previous revision of this entry claimed
"`Feature.AddPart` unions it, so one empty part swallows its feature's entire MBR."
**That is false.** `AddPart` is `BoundingBox = BoundingBox.Union(part.BoundingBox)` and
`Union` short-circuits — `return other` when `this == Empty`, `return this` when
`other == Empty` — so an empty part leaves a real feature box untouched. Verified against
the normalised `Empty` (`MinValue,MinValue,MaxValue,MaxValue`), which is what the
constructor produces from the `Empty` initialiser, so `== Empty` does match. `T-12` is
retitled accordingly.

**The real escalation runs through the index, not through `Feature`:**
1. A feature with **zero parts** keeps `BoundingBox == Empty` — `ComputeBoundingBox()`
   assigns `Empty` outright when `Parts` is empty, and `addFeature` validates nothing.
2. `RecomputeMBR` rebuilds a node from `Children.Min(c => c.BoundingBox.MinX)` /
   `.Max(…)` — **not** `Union`, so it has none of `Union`'s guards. A full-range child
   forces every ancestor to full range.
3. Every ancestor's `getArea` becomes `+inf`, so every candidate's
   `getAddedSizeToAccomodate` is `inf`, `extensionReq < minExtension` never fires,
   `bestCandidate` stays null, and `bestCandidate ??= TreeManager.Root` takes it.
   **One zero-part feature degenerates the whole index into a flat list under `Root`.**
4. With `inf` in the arithmetic, `inf + featArea - inf` is reachable as `NaN` (P-66),
   which also fails `<` and is silently skipped.

**Reachability to confirm with one grep before fixing:** whether `SpatialJoins.BuildTree`
(or any other `addFeature` caller) can pass a feature with `Parts.Count == 0`. Steps 2–4
are read off the source at `9c7124a`; step 1's premise is the unverified link. Do not
record this as measured until that grep is in the commit message.

**Fix:** an explicit `IsEmpty` flag, or a normalisation that keeps `MinX > MaxX` inverted,
with `Empty` short-circuits in `Area`/`ContainsPoint`/`EnlargementToContain`; reject an
empty box at `addFeature`; and route `getArea`/`getPerimeter` through `BoundingBox` so
there is one definition of box size in the index (P-22).
**Sequencing:** P-66 depends on this. Settle P-62 in the same change, or the guards get
written around members that are about to be deleted.

---

## 2. P1 — correctness under load, or blocked features

### P-01 `getMBRoverlap` containment gate  *(closed by PR #9 `9c7124a`)*
`RTreeNode.getMBRoverlap` only tested whether a *corner* of the query lay inside the
node, so a node fully contained by the query returned 0 overlap. `CHANGES_09012026.md`
P0-1 stated this defect inverted; `CHANGES_08312026.md` #15 stated it correctly (N-6,
N-7).

**Fix, as landed:** the member is deleted and all four call sites redirected to
`BoundingBox` — three descents in `RTreeNode` (`getCandidateEndNodesByMBR`, both loops in
`getCandidateFeatNodesByMBR`) and the oracle in `RTreeTests.FeatureIndicesAt` now use
`BoundingBox.Overlaps`; `getAddedSizeToAccomodate` uses the new `BoundingBox.OverlappingArea`.
`getMBRoverlap` no longer appears in production source or either test assembly. This is
N-13 resolved the right way: the survivor (`Overlaps`, written with a closed-interval test
under `fix(#15)`) is the correct one, and the unfixed original is the one that went.

**Correction to the fix proposed in an earlier revision of this file**, which said
"delete it and call `BoundingBox.Overlaps`, preserving the *return ≥ 1 when overlapping*
convention." That sentence was nonsense — `getMBRoverlap` returns a `double`, `Overlaps`
returns a `bool`, and there is no convention to preserve once the return type is `bool`.
The `≥ 1` floor existed so point-shaped features were not pruned; `Overlaps`' closed
interval serves that purpose exactly and better. The floor is gone and should stay gone.

**What this did *not* close:** the floor also happened to prevent pruning of
`Empty`-boxed nodes, and that protection is now gone — **P-66**.

### P-66 An `Empty`-boxed node or feature is now silently pruned from index search  *(new, PR #9)*
`getMBRoverlap`'s `Math.Max(xAxisOverlap * YAxisOverlap, 1)` did double duty, and only one
of the two duties was the one anyone meant. Against an `Empty` node the corner gate passed
(`Empty` spans every finite coordinate), both axis overlaps came out `0`, and the floor
returned **1** — so the caller descended. `BoundingBox.Overlaps` carries
`if (this == Empty || other == Empty) return false;` — so the same node is now
**pruned**.

| point query vs. `Empty`-boxed node | old `getMBRoverlap` | new `Overlaps` |
|---|---|---|
| corner gate | passes | — |
| axis overlaps | `0` and `0` | — |
| result | `Max(0, 1)` = **1** → descend | guard → **false** → prune |

The "never prune on a degenerate box" instinct and the "don't lose point features"
instinct were the same line of code, so fixing the latter properly deleted the former.
`Overlaps` is right about zero-area contact and wrong about *unknown extent*: an
uninitialised node box and a legitimately point-shaped feature are different situations
that the old floor treated identically and the new predicate also cannot distinguish — it
just errs the other way.

**Consequences today, all silent:**
- A zero-part feature (P-39 step 1) is **unfindable** by `findByXY` and absent from
  every candidate set. Before PR #9 it was found. This is a behaviour change on the read
  path of the index, untested in either direction.
- A node whose box is `Empty` prunes its **entire subtree**, so the loss is not one
  feature but every feature below it.
- `getArea` is now the only quantity in this path that is *not* `Empty`-aware: it returns
  `+inf` for `Empty` while `OverlappingArea` returns `0` for the same box, so
  `getAddedSizeToAccomodate` mixes two incompatible readings of the same node in one
  expression. When both halves reach `inf` the result is `NaN`, which fails
  `extensionReq < minExtension` and is skipped without a word.

**Not a reason to revert.** `Overlaps` is the correct predicate; the defect is that
`Empty` is an incoherent box (P-39), and the old floor was an accident that masked it.
**Fix:** P-39's `IsEmpty`, which makes "unknown extent" representable and distinguishable
from "zero area", plus a guard in `addFeature` so such a box never enters the index.
**Guard:** T-18. **Blocks:** nothing, but it is the item that makes P-01's fix safe to
keep, so it inherits P-01's position in the ordering (section 10 step 9).

### P-02 Spatial joins discard the tree  *(mandatory)*
`SpatialJoins` enumerates features instead of descending the index. Blocked on P-47 (no
usable query API) and D-E. **P-01, formerly a hard prerequisite, is closed by PR #9.**
The discarding is explicit and worth deleting with the fix: both join directions contain
`_ = tree ?? BuildTree(features);`, which constructs a whole R-tree and throws it away.
`pointTree` / `polyTree` are therefore pure cost today.

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
mappers are merged.

**Correction:** earlier revisions of this entry named
`LongColumnIsInteger64InGeoJsonToo` as the guard and warned it was circular because it
authored its own input. That test is **not in the repository** — see section 5 for how
that misunderstanding happened. The closure claim stands on the fixture test above.

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
  non-EPSG authority unrepresentable on read. The `EPSG:102003` warning line in every
  test run is the cheapest reproduction in the repository — cite it.
- Guard with T-15 once the model is chosen, **rewritten** — the current wording asserts a
  defect that does not exist.
- Folded in: P-29 (`CoordinateTransformationOptions` / area-of-interest).

### P-15 `Root` public setter; no box validation
`RTreeManager.Root { get; set; }` accepts any node, and `addFeature` validates no box.
See P-39 for what an empty box does, and P-66 for what PR #9 made it cost. Split out: P-51.

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
empty part then flows into every consumer. **PR #9 makes the consequence sharper:** a
zero-part feature now carries an `Empty` box, which P-66 shows is pruned from search — so
this silent drop can become a silent *disappearance* rather than merely an empty feature.

### P-40 All-leaves fallback makes the build quadratic
`insert` falls back to scanning all leaves; with P-41's enlargement metric this is
quadratic in feature count.

### P-41 `getAddedSizeToAccomodate` measures set-union area, not MBR enlargement
Chooses the child with the smallest value of `getArea + featArea - OverlappingArea(bbox)`.
PR #9 made that expression exactly what it is, and it is worth naming, because the name
of the method and its docstring both promise something else.

`getArea + featArea - |A ∩ B|` **is the area of the true set-union** `|A ∪ B|`. What the
R-tree heuristic needs is **MBR growth**, `|bbox(A ∪ B)| − |A|`. Those coincide only when
`A ∪ B` is already a rectangle (nested pairs, flush-aligned pairs). Otherwise the bbox
union invents area that neither box contains:

- `A = [0,10]²`, `B = [20,30]²`: the method reports `100 + 100 − 0 = 200`; true MBR growth
  is `900 − 100 = 800`. Off by 4×.
- For a feature disjoint from *every* candidate the value reduces to `getArea + featArea`,
  so across candidates it ranks by **smallest existing child**. That is exactly the bias
  this entry has always described, now with its mechanism named rather than guessed.

PR #9's change was still a strict improvement: subtracting the true overlap instead of a
corner-gated `0` fixes the containment case (a node fully inside the feature no longer
looks maximal-cost). The disjoint case is untouched.
**Fix:** `return BoundingBox.EnlargementToContain(bbox);` — one line, and the only reason
`BoundingBox` currently has a second uncalled member. It also makes `OverlappingArea`
load-bearing a second time, this time through `Union`.
**Caveat for P-40:** enlargement is a *cheaper* comparison than the current expression
but does not change the asymptotics. The tie-break floor P-41 also complains about
disappears with the substitution.
**Guard:** T-20.

### P-42 `buildChildOptions` re-parents live children
While scoring candidate placements it re-parents children of the *real* node, then
discards the options. Mutates the tree during a read-only decision.

### P-43 `Options.First()` throws; no min/max invariant
The split loop `for (split = MinChidrens; split <= Count - MinChidrens; split++)` is
empty when `Count < 2*min`. At split time `Count == max + 1`, so the invariant is
**`max >= 2*min - 1`**. Defaults (10, 4) and the tests' (6, 3) satisfy it; `new
RTreeManager(4, 6)` throws from inside `split()`. Validate in the constructor with a
clear message. Note the public constructor's parameter names (`minChilds`, `maxChilds`)
do not match the properties they assign (`MinChildren`, `MaxChildren`) — the misspelling
family in P-22 is wider than the `Chidrens` fields.

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
When `Query(BoundingBox)` lands, P-46's missing leaf test becomes live in the same commit.

### P-48 The R-tree test suite cannot detect a regression
Six gaps; the first is the important one:
1. **No regression test for MBR propagation** (`16585ef`, closed as P-49). The fix that
   made the index return all features is unguarded.
2. No test asserts query results against a brute-force oracle at scale.
3. No test for `min`/`max` invariant (P-43).
4. ~~No test for the containment gate (P-01)~~ **superseded** — P-01 is closed. The
   surviving half of this row is now T-18: no test distinguishes a *point-shaped* feature
   (must be found) from an *unknown-extent* box (P-66), which is the distinction PR #9
   silently changed.
5. No test that the tree's boxes match the features they index. `RecomputeMBR`'s
   `Children.Min/Max` form versus `Union` is precisely where they could diverge (P-39).
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
(section 5). The corruption is real and the mechanism is documented below with its
citations, but there is currently **no repro in the tree** — a bisect or a reviewer cannot
reproduce it without writing the case again. T-19 asks for a committed one, as an
`Assert.Throws` once the writer rejects.

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
0`) is the guard against exactly that regression.

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
places, so the fix has no remaining excuse.

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

### P-62 `BoundingBox` public surface with no owner  *(extends N-1; reduced by PR #9 `9c7124a`)*
This entry listed five uncalled members at `7b4c6aa`. **Two are now consumed**, in
production code, at `9c7124a`: `Overlaps` (three descents in `RTreeNode`, plus the
`RTreeTests` oracle) and `OverlappingArea` (added in this very PR, called from
`getAddedSizeToAccomodate`). Three remain, plus one that was never counted:

- `Contains` → **keep**, P-46's leaf test. Still uncalled.
- `EnlargementToContain` → **keep**, P-41's fix. Still uncalled, and P-41 now has a
  one-line substitution ready.
- `ContainsPoint` → homeless, and wrong for every point under P-39. Note it is now the
  **only predicate in the file with no `== Empty` guard at all** (`OverlappingArea` gained
  one in PR #9). Either give it the guard and use it (joins and `findByXY` are candidates),
  or delete it.
- `FromVertices` → genuinely unreferenced with no planned consumer (`Part` maintains its
  MBR incrementally via `Union`; `Feature` recomputes by `Union`). Its
  `double.IsPositiveInfinity(minX)` check can never fire, so the empty-input path returns a
  garbage normalised box rather than `Empty`. Delete, or justify and test it.
- **`Point(x, y)`** — never called, and `RTreeManager.findByXY` hand-writes
  `new BoundingBox(x, y, x, y)` three lines away. Use it there or delete it; a
  factory nobody uses while its expression is duplicated inline is the worst of both.

**Telling:** `SpatialIoTests` needed a point-in-region check and hand-rolled
`PointInPolygon` rather than calling `ContainsPoint`. That is the same N-13 pattern as
`Overlaps` vs `getMBRoverlap` — a second implementation arriving beside an existing one.
Resolve inside P-39's change so guards are not written around members about to go.

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
is precisely the hole P-68 describes.

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
  green. T-17 closes this and is the reason P-65 should follow it, not precede it.

**Fix:** delete the dead constants and aliases, retitle the docstring to what the file
tests, and add T-17. Keep the "two independent assertions" rationale — move it onto the
test that still earns it.

### P-67 `getArea` / `getPerimeter` are `Empty`-blind and feed the split ranking  *(new; extends P-22)*
`getPerimeter` has no guard of any kind, so an `Empty` node contributes ~`7.2e308` to
`perimeterTotal`, which is the **third sort key** in `split()`'s
`OrderBy(metrics[0]).ThenBy(metrics[1]).ThenBy(metrics[2])`. `getArea` feeds
`totalArea`, the second key. So a single uninitialised child can dominate both tie-breaks
and choose the split, silently.
Both members also re-implement `BoundingBox` arithmetic (P-22), and after PR #9 the file
contains a split-brain: `OverlappingArea` says an `Empty` box contributes `0` while
`getArea` says the same box contributes `+inf` (P-66).
**Fix:** delete both in favour of `BoundingBox.Area()` / a `Perimeter()` member, add the
`Empty` guard to `Area()` under P-39, and one definition wins. Cheapest R-tree item in
this file; do it inside P-39's change, not separately.

### P-30 README and warnings-as-errors
Four factual errors in the README; the spherical feature is undocumented.
`TreatWarningsAsErrors=false` in `Directory.Build.props` is legitimately required because
the GDAL NuGet package's own generated `obj/.../GdalConfiguration.cs` emits 3× `CS8600`
(confirmed present on the `Nsi.Geospatial.Io` build, so a clean CI build will show them).
Note that `[obj/**/*] generated_code = true` suppresses **CA analyzers only** — compiler
diagnostics still fire, which is why no `CA` warnings appear from that file but three
`CS` ones do. Scope `<NoWarn>$(NoWarn);CS8600</NoWarn>` to `Nsi.Geospatial.Io.csproj`
only — a global one would silence the real `CS8600`s in `RTreeNode.cs`.

### P-31 SDK policy
The pinned SDK version is stated five inconsistent ways across `global.json`, `ci.yml`,
`release.yml`, the README and `Directory.Build.props`.
Sixth inconsistency, and the local machine has already drifted: `dotnet test` reports
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
weaker form (looser tolerances, `0.3048` literals instead of `CrsInfo.MetersPerFoot`, no
absolute-floor guard). Both assertions worth keeping were ported first, and the D-B spec
tests survived. Two files remain, still in two namespaces inside one assembly — which is
*why* the duplicate went unnoticed: the classes could not collide by name.
**Grown by PR #8 `9bee4e4`:** `FieldTypeTests.cs` adds a **third** copy of
`TempDir()`/`Cleanup()`. Hoist before adding a fourth.
**Grown by PR #9 `9c7124a`:** `GeometryTests.cs` (new, 73 lines) lands in
`Nsi.Geospatial.Tests` — the *other* namespace — so it will not see the shared helpers
when they are hoisted, and the split survives a third file. It also opens with three
unused `using`s (`System.Collections.Generic`, `System.Linq`, `Nsi.Geospatial.Spatial`;
the file references only `BoundingBox` and `Xunit`), which is the exact defect class
`7b4c6aa` was deleting. Decide the namespace once, in the hoist.
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
working tree and were never committed (section 5). The warning was right as a rule and
wrong as a fact; the rule is what to keep: **a measurement that cannot be expressed as a
passing assertion belongs in prose here, not in the test suite.**

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
- **One correction to the above, from PR #9:** a predicted `CS0120` on `BoundingBox.MinX`
  ("object reference required") never materialised. `BoundingBox.OverlappingArea(bbox)`
  and `BoundingBox.Overlaps(bbox)` inside `RTreeNode` are legal — `BoundingBox` there is
  the *property*, and member access on it resolves normally. Confusing; consider naming
  the property `MBR` when next in the file. Not a defect, and not worth a rename on its
  own.
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
**Deleted in `44fbf06`:** `Part.Direction` (write-only after the `IsHole` derivation was
removed; winding survives as vertex order, and one fewer `IsClockwise()` P/Invoke per
ring), `Part.BeginIndex`, `Part.EndIndex`.
**Deleted in PR #8 `7b4c6aa`:** unused `using System`, `System.Collections.Generic`,
`System.Linq`, `System.Text`, `System.Threading.Tasks` from `RTreeManager`/`RTreeNode`,
plus `System.Xml`, `System.Xml.Linq`, and the redundant `using Nsi.Geospatial.Io;` inside
`namespace Nsi.Geospatial.Io` in `SpatialReader`. (Safe: `ImplicitUsings` is on.)
**Deleted in PR #9 `9c7124a`:** `RTreeNode.getMBRoverlap` (−23, recorded under P-01).
**Corrected:** this entry used to list `Feature._crs` (P-55) as remaining. It is already
gone — `Feature` resolves CRS through its owner chain and holds no field. P-55 is a
dangling reference; see N-12.
**Remaining:**
- `addFeatureChild` is not merely unreachable, it is *misleading*: it contains an area
  tie-break (`extensionReq == minExtension && childnode.getArea < …`) that the live path
  `addFeatureChildEnforceIntersect` lacks, so a reader will assume the tie-break is
  active. Delete it, or move the tie-break to the live path as its own change. Note it
  calls `getAddedSizeToAccomodate`, so P-41's substitution changes its (dead) semantics
  too — mention that in the commit so nobody later resurrects it expecting the old
  ranking.
- `cumulativeOverlap` / `siblingOverlap` are assigned in `buildChildOptions` and never
  read — the split sorts on the local `overlap`/`totalArea`/`perimeterTotal` triple. Dead
  state carried by every node.
- `RTreeNode.getArea` re-implements `BoundingBox.Area()`;
  `getAddedSizeToAccomodate` re-implements `BoundingBox.EnlargementToContain` (P-41).
  Both are `Empty`-blind in ways that reach the split ranking — P-67.
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

Residue the PR created, all small: `"peremiter"` label, `Deg2Rad` inconsistency, the
axis-order guarantee now resting on one untested path (T-14), and `FieldTypeTests`' dead
constants/aliases with a docstring that describes coverage it no longer has (P-68) — all
filed above.

### Added by PR #9 `9c7124a` ("migrating from rtree to bbox") — +100/−28, 4 files, behaviour change on the index read path
| Change | Recorded under |
|---|---|
| `RTreeNode.getMBRoverlap` deleted (−23) | **P-01 closed** |
| Three descents + the `RTreeTests` oracle redirected to `BoundingBox.Overlaps` | **P-01**, **P-66**, P-46 (half) |
| `BoundingBox.OverlappingArea` added: `Empty` guard, per-axis clamp at `<= 0`, `dx * dy` | new member, live immediately |
| `getAddedSizeToAccomodate` subtracts `BoundingBox.OverlappingArea(bbox)` | P-41 (improved, still open), P-66 |
| `GeometryTests.cs` added (73 lines, new file, `Nsi.Geospatial.Tests`) | P-36 |

**This is the first green `dotnet test` on the branch:** `Nsi.Geospatial.Tests` 2 failures,
both in `OverlappingAreaIsSymmetricAndConsistentWithEnlargement`;
`Nsi.Geospatial.Io.Tests` **succeeded**; skips unchanged at 2 (D-B budget intact).

**Both failures are a wrong test, not a wrong library** — the assertion was authored in
this tracker's review and is being deleted, so record why, because the near-miss is the
lesson:

`EnlargementToContain` is `Union(other).Area() - Area()`, and `Union` returns the
**bounding box** of the two boxes, not their union region. Inclusion–exclusion
(`|B| − |A∩B| == |bbox(A∪B)| − |A|`) therefore holds only when `A ∪ B` is itself a
rectangle. Verified by hand:

- `A=[0,10]²`, `B=[5,15]²`: bbox union `[0,15]²`=225 → enlargement 125. True union
  `100+100−25=175`. The identity demanded `100−25=75`. Observed: expected 75, actual 125.
- `A=[0,10]²`, `B=[20,30]²`: bbox union `[0,30]²`=900 → enlargement 800. True added area
  100. Observed: expected 100, actual 800.

`OverlappingArea` was correct in both cases (symmetry passed on all five rows, including
the two that failed). Fixed by splitting the theory: hand-computed goldens carry the
weight in `OverlappingAreaIsSymmetricAndHandCorrect`, and the identity survives only in
`OverlappingAreaAgreesWithEnlargementWhenTheUnionIsARectangle` over nested pairs. The
same false identity, note, is what makes **P-41** a real defect rather than a stylistic
one — see its worked numbers.

**Residue to clean in the next commit:** three unused `using`s in `GeometryTests.cs` and a
second trailing newline appended to `BoundingBox.cs`, `GeometryTests.cs` and
`RTreeTests.cs` (P-36, section 9 format step).

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
| T-12 | ~~`EmptyPartDoesNotInflateFeatureBoundingBox`~~ **retitled — the described defect does not exist.** `Feature.AddPart` goes through `Union`, whose `== Empty` guards already make an empty part a no-op on a real box (P-39's retraction). What should be pinned instead: **`FeatureWithNoPartsHasNoUsableBox`** — a zero-part feature's box must be recognisably empty, and `addFeature` must refuse it. See T-18 for the index-side consequence | P-39, P-11 |
| T-13 | Degenerate ring `[A,B,A]` — assert whichever of `0.0` / `null` is chosen, in both CRS kinds | P-07 |
| T-14 | **`ReprojectToUsesTraditionalGisOrder`** — read a lon/lat fixture into a projected CRS and assert X is still longitude. Also closes N-2 (`Transformer_EatsLonLatNotLatLon`). The only path that pins axis order now, since the P/Invoke path that ignored it was deleted in PR #8 | P-14 |
| T-15 | ~~`CrsTokenDoesNotDoublePrefixANonEpsgAuthority`~~ **invalid as written — it passes today**, because the `StartsWith("EPSG:")` guard already exists (D-D). Rewrite once the model is chosen, to assert what should hold: `Projection("ESRI:102003")` resolves to the same SRS GDAL reports for `ESRI:102003` **without emitting a warning**, and `CrsInfo` round-trips the authority rather than relabelling it `EpsgCode` | D-D, P-14 |
| T-16 | ~~`ReadsAnInteger64AuthoredElsewhere`~~ **done — implemented in PR #8 `9bee4e4`, not as a backlog item.** Hand-written GeoJSON (`"BIG":4000000000, "SMALL":7`), asserts `BIG` boxed `long` / `Schema["BIG"].FieldType == LongFT` and `SMALL` still boxed `int`. Non-circular by construction. Keep the `SMALL` assertion through P-65's consolidation — it is what pins the `Integer`/`Integer64` split | P-06 |
| T-17 | **`LongFTWritesAsInteger64`** — write `4_000_000_000` to a `LongFT` column with our writer, then reopen and assert the **declared** OGR type is `OFTInteger64` (not `OFTString`/`OFTReal`) *and* the boxed CLR type is `long`. Currently **no test touches** `SpatialWriter.MapFieldType`'s `LongFT` arm or `SetOgrField`'s `case long l:`; a value-only assertion here would pass on a text column (P-06's rationale applies verbatim) | **P-68 — the write arms of `9bee4e4` are unguarded** |
| T-18 | **`DegenerateBoxIsNotSilentlyPrunedFromTheIndex`** — add a feature whose box is point-shaped (must be found by `findByXY`) and one whose box is `Empty` (must be rejected at `addFeature`, or found — but must not be silently unreachable). Asserts the distinction PR #9 removed, since `getMBRoverlap`'s `≥ 1` floor previously made both findable | **P-66, P-39, P-48.4** |
| T-19 | **`RejectsALongTheDriverCannotStore`** — `Assert.Throws` on writing `1_000_000_000_000_000_007` per driver, plus the `9_000_000_000_000_000` maximum-safe boundary. Also puts a **repro in the tree** for P-64's measurement, which currently has none (see provenance note there) | P-64 |
| T-20 | `InsertionChoosesSmallestEnlargement` — a small feature equidistant from a large and a small node must land in the small one. Fails on today's `getArea + featArea − overlap`, which reduces to "prefer the smaller existing node" only when disjoint and reports set-union area otherwise (worked numbers in P-41) | P-41, P-48.5 |

---

## 8. Notes

- **N-1** ~~Five `BoundingBox` primitives are never called~~ **reduced to three by PR #9.**
  `Overlaps` and `OverlappingArea` are now called from production code (`RTreeNode`).
  Uncalled: `Contains` (→ P-46), `EnlargementToContain` (→ P-41), `ContainsPoint`
  (homeless), plus `FromVertices` and `Point`, which were never counted. Split decision:
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
  **Add two more to sweep:** `grep -rn "P0-4\|intentionally left unchanged"` — the
  `SpatialWriter.SetOgrField` comment now asserts the code does something it no longer
  does (N-6); and `grep -rn "through the writer and reader"` — `FieldTypeTests`' class
  docstring now describes a writer test that is not in the file (P-68).
- **N-5** `CHANGES_09012026.md` below P3-5 and parts of `CHANGES_09082026.md`'s narrative
  have never been readable through any fetch path. Do not assume they are empty.
- **N-6** Keep `CHANGES_08312026.md`'s `fix(#N)` legend: those markers live in
  `BoundingBox.Overlaps`, `AttributeColumn.Coerce`, `CsvHelper`, `Part.AddVertex` and
  `Feature`, and they index *that* file, not this one. `CHANGES_08312026.md` also claims
  the R-tree files are "restored verbatim from master, all original typos included" —
  that has been false since `16585ef` and `ac3bb82`, and PR #9 `9c7124a` deleted a whole
  method from them.
  **Live collision, worth fixing on sight:** `SpatialWriter.SetOgrField`'s comment carries
  `P0-4`, which reads as `P-04` in this file (`EarthRadiusFeet`) but means
  `CHANGES_08312026.md` item 4 (field typing). Two unrelated defects, one glyph, in the
  one method whose behaviour this branch just changed.
- **N-7** Timeline rule: `CHANGES_09012026.md`'s P0-1 blamed the containment gate for
  missing features; `16585ef` fixed the actual cause (MBR propagation) four and a half
  hours later and the changelog was never reconciled. Any row citing 0901 P0-1 must be
  re-read against P-49. **Now doubly relevant:** P-01 is closed, so the changelog's claim
  can finally be settled — the gate was real and dormant, and it is the thing PR #9
  deleted.
- **N-8** Benchmark build and query separately. The RBush comparison may have measured
  `Load()` (bulk STR-style) rather than incremental `addFeature`, which would explain
  part of the gap and is exactly what P-40 will change.
- **N-10** `Part`'s metric cache depends on the invariant that geometry cannot change
  behind its back. That holds for `Part` (`Vertices` is read-only) but **not** for
  `Feature.Parts` (P-17) or `FeatureCollection.Features` (also a public `List`). Fixing
  P-17 should close the last hole.
- **N-11** `dotnet test` at `9bee4e4` reported `total: 107, failed: 5, succeeded: 100,
  skipped: 2`. **Attribution corrected:** those five failures were in the *working tree*,
  not in the commit — `FieldTypeTests.cs` at `9bee4e4` contains one test
  (`ReadsAnInteger64AuthoredElsewhere`) and nothing that fails. Do not cite that count as
  a property of any commit.
  At `9c7124a`: `Nsi.Geospatial.Io.Tests` succeeds; `Nsi.Geospatial.Tests` has 2 failures,
  both in the oracle theory described in section 5, both a wrong assertion rather than a
  wrong library.
  **The two skips are unchanged and are the entire skip budget** (D-B):
  `EarthRadiusFeetIsTheAuthalicRadiusInFeet` (P-04) and
  `PointToSegmentWhenFootIsBehindTheNearEndpointReturnsDistanceToA` (P-03). A new skip is
  a new defect being hidden. (`RTreeTests.BulkInsertAllFeaturesFindableByPoint` carries a
  commented-out `Skip` from `16585ef`; leave it as history, do not re-enable.)
- **N-12** Dangling `P-` references: **P-20** (cited by D-C), **P-54** (cited by P-13,
  T-8, and `Part`'s `_measuredCrs` comment), **P-55** (cited by P-22, and its subject
  `Feature._crs` no longer exists), **P-59** (cited twice by section 9) have no entry in
  this file. Either they were closed by deletion without the citations being cleaned, or
  they live in the unreadable changelogs (N-5). Do not reuse these numbers; restore the
  definitions or mark the citations dead. D-C's closure removes one citation to P-20 but
  P-20 itself is still undefined.
- **N-13** Duplication tends to arrive as a *second* correct implementation rather than a
  second broken one. Three cases so far: `BoundingBox.Overlaps` vs `getMBRoverlap` (P-01,
  **resolved `9c7124a`**), `BoundingBox.EnlargementToContain` vs
  `getAddedSizeToAccomodate` (P-41, open), and two reprojection engines with different
  axis handling (P-14, resolved). When adding a predicate, grep for one that already
  exists — and when deleting a duplicate, check whether the survivor is the *correct* one.
  **Fourth lesson, from PR #9:** checking that the survivor is correct is not sufficient —
  also check what the deleted copy was *accidentally* protecting. `getMBRoverlap`'s `≥ 1`
  floor was wrong about points and accidentally right about unknown extents; removing it
  produced P-66 without any test changing colour. A deleted defensive kludge deserves a
  test in its own right, because nothing else records why it existed.
- **N-14** Answer a binding-surface question by compiling against it, not by reflecting
  over it. D-C sat open across two revisions and talked itself into contradicting the
  0901 changelog, because each reflection pass produced another unreadable dump instead of
  a yes/no. Three lines of code settled it: `SetField(string, long)` exists, the getter is
  `GetFieldAsInteger64` (`GetFieldAsLong` does not exist), and `GetLayerDefn()` returns
  `FeatureDefn` (there is no `LayerDefn`). The compile errors that produced this —
  `CS0104` ambiguity, two `CS0246`s, `CS0118` — were the answer all along. Corollary: when
  a comment in production code contradicts a changelog, compile a probe before editing
  either.
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

---

## 9. Build hygiene

- **Warnings (10 known, `Nsi.Geospatial` only).** Six are real: `CS8625` (RTreeNode.cs:26,
  `null` default on a non-nullable parameter) and `CS8600` ×3 (80, 153, 184) and `CS8602`
  (295) — read all four before annotating; a wrong `!` is a latent NRE inside the index.
  `CA1854` (`AttributeTable.cs:57`, use `TryGetValue`). `CA1805` ×2 (`RTreeNode.cs:18,19`,
  `Max/MinChidrens` explicitly `= 0` — check which is the real default, against P-43).
  **Line numbers have moved twice** (PR #8 removed `using` blocks; PR #9 deleted a
  23-line method at the old ~313) — re-run a clean build before annotating, and expect
  `CS8625`'s constructor to have shifted.
  Separate and *not* ours: 3× `CS8600` on the `Nsi.Geospatial.Io` build, all from the
  package-generated `obj/.../GDAL/3.11.3/GdalConfiguration.cs`. Cannot fail CI while
  `TreatWarningsAsErrors=false`; scoped `NoWarn` per P-30 is the only action.
- **`CA1829` at `RTreeNode.cs:104` may be a hot path.** If line 104 is the split loop's
  bound (`Children.Count()` in the `for` condition), that allocates an enumerator on every
  iteration of a loop that runs on every insert. Still present at `9c7124a`; hoist it.
- **`CA1711` (`FeatureCollection` naming) is a design opinion and a breaking rename.**
  Suppress with a rationale in `.editorconfig`, do not rename.
- These have been visible on every clean build; incremental builds hid them because
  `Nsi.Geospatial` was not recompiling. Once the six are fixed, flip
  `TreatWarningsAsErrors` to `true` with the scoped `NoWarn` from P-30.
- `dotnet format Geospatial.slnx --verify-no-changes --no-restore` is a hard CI step and
  its status is **still unverified**. Three things to check, cheapest first, all on the
  PR #9 head:
  1. `GeometryTests.cs`'s three unused `using`s (P-36) — if `IDE0005` is elevated in
     `.editorconfig` this fails on a file added yesterday.
  2. The second trailing newline on `BoundingBox.cs`, `GeometryTests.cs`, `RTreeTests.cs`.
  3. `FieldTypeTests.cs`'s dead aliases and the comment that splits its `using` block into
     two sort groups (P-68) — if the dead aliases go, the comment's premise goes with them.
  Note PR #8 relocated test bodies by hand (`7b4c6aa`) and PR #9 added a whole file; both
  are the classic csharpier failure, so do not assume a clean local run means the step is
  green.

---

## 10. Recommended order

0. **Delete the bad oracle.** `OverlappingAreaIsSymmetricAndConsistentWithEnlargement`
   asserts an identity that is false for non-rectangular unions (section 5, worked
   numbers). Replace with `OverlappingAreaIsSymmetricAndHandCorrect` +
   `OverlappingAreaAgreesWithEnlargementWhenTheUnionIsARectangle` +
   `OverlappingAreaAgainstEmptyIsZero`. Also drop `GeometryTests.cs`'s three unused
   `using`s and the stray trailing newlines. This is the only thing between the branch and
   green, and green is the precondition for measuring everything below.
1. Green build and format: the six real warnings, `CA1711` suppression, P-59, then
   `TreatWarningsAsErrors=true`. Confirm the format step's status per section 9.
2. **P-68 + T-17** — small, and it closes the one coverage hole the branch itself opened
   (untested write arms). Do it before P-65 so the consolidation starts with both
   directions pinned.
3. P-53 + P-36 (one `AssertRel`, shared geometry helpers, shared golden constants, the
   triplicated `TempDir`/`Cleanup`, and **settling the two-namespace split** before a
   fourth file lands on the wrong side) — needed *before* the zero-assertion tests land,
   or they demand bit-exactness.
4. P-56 + P-57 together (storage convention), then T-1…T-4, T-7, T-13.
5. **T-5** — the read-path hole test. Highest value per line in this file.
6. **T-14** — axis order. One test, closes N-2, and guards the sole remaining transform
   path. Cheap enough to fold into step 5.
7. P-05 remainder (`Parts[0].IsHole` check, null-hole handling), P-21 (silent geometry
   drop — likely to surface as T-3 failures, and now implicated in P-66's reachability
   question).
8. T-8…T-11, then P-17 so the cache invariant has no remaining hole (N-10).
9. P-04 and P-03 — the two remaining skips, and P-04 is PR #6's stated purpose.
10. **R-tree cluster: P-39 + P-62 + P-66 + P-67 as one change → T-18 → P-43 → P-48 →
    P-41 + T-20 → P-47 → P-44 → P-02 → P-42/P-46/P-50/P-15/P-51**, with D-E resolved
    before P-02.
    - **P-01 is closed; it no longer gates this cluster.** What gates it is **P-66**:
      PR #9's redirection is correct in the limit and unsafe while `Empty` is the
      full-range box, so P-39 now carries a live regression risk rather than merely a
      theoretical one.
    - P-39's change must bundle: the `IsEmpty` representation, `Empty` guards in
      `Area`/`ContainsPoint`/`EnlargementToContain`, a rejection in `addFeature`, P-67's
      `getArea`/`getPerimeter` deletion, and P-62's keep-or-delete decisions — one
      coherent change to the box model, and the cheapest place to land four items at once.
    - Confirm P-39 step 1's reachability (the `BuildTree` grep) inside this change and put
      the answer in the commit message.
    - **P-41 last**, because `EnlargementToContain` should not gain a caller until `Empty`
      is coherent — substituting it now would route `Empty` through `Union`/`Area` with no
      guard on either side.
11. Field typing, once, in one change: **P-65** (one mapper, both directions adjacent) with
    **P-64** (reject unrepresentable values; `bool` and `float` arms; `BoolFieldTests`;
    T-19). Both are cheapest immediately after T-16 and T-17 pin both directions, and
    splitting them risks a second round of the same three-file hunt.
12. Structural cleanups while their files are already open: P-61 (fold the Reprojection
    project into `Io`), P-23a (`Walk`/`Shoelace`), P-63 (writer/reader hygiene — no alias
    blocks), P-26 (join duplication). All behaviour-preserving.
13. Everything else as touched.