# Issues — living tracker

Single source of truth for known defects and open work.

Reviewed against `feature/warnings` @ `d4fecd1` (PR #12, "removing warnings", 1 commit;
base `main` @ `26d2840`). **`26d2840` is the merge of PR #11**, so the P-01/P-66 closures
below are on `main`, not merely on a branch. PR #12 is a **breaking public-API change
disguised as a warning sweep** — see P-70.

**No build state is recorded against `d4fecd1`.** Its `build` check run was **still in
progress** when reviewed (started 20:19:23Z, `conclusion: null`, `annotations_count: 0`).
Per N-11 that means *nothing* is asserted about it — not green, not red, not a warning
count. Section 9 carries a **prediction** derived from reading the source, explicitly
labelled as one, and it must be replaced with the real annotation list once the run lands.

Last SHA with a **verified** state is `b264607` (PR #11 head): CI green including the
format step, 11 annotations, no test totals exposed. Previous local green was `04118f4`
(112/110/2/0, 16 warnings). PR #8 closed at `389dc4a`.

**Naming note, applies file-wide:** PR #12 renames `FeatureCollection` → `Features` and its
`.Features` property → `.FeatureSet`. This file has been updated to the new names
throughout, including in historical entries — a citation to `FeatureCollection` after
`d4fecd1` reads as a stale pointer, which is exactly the defect N-3 sweeps for. Where an
entry describes behaviour *before* the rename, the old name is kept and marked.

## How to use this file

- `P-xx` numbers are stable. Never reuse one. Do not renumber.
- Commit with `fix(P-xx)` in the message. The old `fix(#N)` markers in source refer to
  `CHANGES_08312026.md`'s numbering, **not** these numbers — see N-6.
- An item is closed only when a passing, un-skipped test guards the fix. Comments and
  commit messages are not evidence. A green suite is not evidence either — it proves
  nothing *broke*, not that anything is *guarded*.
- Deletions are recorded inline against the item that asked for them, with the commit that
  performed them (`44fbf06`, PR #8 `051e8d8`, PR #9 `9c7124a`, `04118f4`, PR #11 `d3464e6`,
  PR #12 `d4fecd1`, …). Do not delete the entry; mark it.
- A closed item keeps its entry, retitled to what the defect actually was. Several
  entries here were scoped by guess and turned out narrower (P-06) or wider (P-14), and
  two were wrong in the direction of overstating a mechanism (P-39's third bullet, P-01's
  proposed fix). Re-read an item's prose against the source before acting on it.
- **A local `dotnet test` run is not a property of a commit.** Three failures of attribution
  (N-11) came from reading a working-tree run as a committed state. Before
  recording a pass or fail against a SHA, `git show <sha>:<path>`.
- **A CI conclusion is evidence of *success*, not of *counts*.** A check run says the
  job passed; it does not say how many tests ran. `b264607` is recorded as "CI green,
  format step included" and no total is quoted, because the API returns annotations only.
  Prefer CI's conclusion to a local run — it is pinned to a SHA by construction — and
  still never infer a number from it.
- **A check run that has not finished is evidence of nothing.** `d4fecd1`'s run was
  in progress at review time; the correct output was "unknown", and section 9 says so
  rather than guessing from the source. Reading the code tells you what *should* happen;
  it does not tell you what the compiler *did*.
- **Commit messages that name this file's `P-xx` are claims, not evidence, including when
  the claim came from this file.** `d3464e6`'s subject and `b264607`'s docstring are
  verbatim text written here; both were verified against the test bodies before the
  closures below were recorded. **A commit message that names no item is the mirror
  problem:** `d4fecd1` says "removing warnings" and renames a public type. The title
  under-describes the change, which is how a breaking rename enters through the side door.
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
| D-B | Skipped tests assert behaviour the library *should* have and does not. They are deliberate and are the spec for P-03 and P-04. **The skip budget is exactly two** (N-11); a third skip is a defect being hidden, not a test being deferred. Confirmed exactly two at `04118f4`; unchanged at `b264607` **by inspection** — PR #11 adds no `Skip =` anywhere. PR #12 touches five test files (rename only, on the evidence available) and adds no skip; **re-verify when its CI lands**, because a rename pass over test files is a plausible place for a `Skip` to be added quietly. |
| D-C | **Closed by PR #8 `9bee4e4`.** The binding surface is answered by code that compiles and tests that pass, not by further reflection — see N-14 for why this kept decaying. Settled against the installed GDAL 3.11.3 binding: `Feature.SetField(string, long)` exists and is the correct setter; the int64 getter is `GetFieldAsInteger64` and **`GetFieldAsLong` does not exist**; there is **no** `Layer.FieldIndex` and **no** `object` overload, so the comment in `SpatialWriter.SetOgrField` that this file spent two revisions calling into question was *correct*; and `Layer.GetLayerDefn()` returns `OSGeo.OGR.FeatureDefn` — **there is no `LayerDefn` type** — which production code never noticed because it binds everything with `var`. Neither `Layer.FieldIndex` nor an object overload was ever needed, so neither blocks anything. `CoordinateTransformationOptions` remains unexamined and now gates only P-20. The deleted spike (`ProbeOsrBinding.cs`, PR #8 `051e8d8`) is not worth recovering; `git show 051e8d8^:tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs` if ever needed. |
| D-D | Decide the CRS token/authority model. **Correction: the double-prefix defect this row asserted does not exist.** `CoordinateTransformer.CrsToken` tests `StartsWith("EPSG:")` and passes an already-prefixed token through unchanged, so `"ESRI:102003"` does **not** become `"EPSG:ESRI:102003"`, and T-15 as previously written would have passed without testing anything. The warning we blamed on it has a different cause, and it is real: **(a)** `Projection.AlbersUsa`/`Nad83` hardcode `EPSG:` for codes GDAL 3 attributes to `ESRI:` — hence `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is. Assuming ESRI:102003 was meant` on every run (still present at `04118f4`), with correct results reached only by GDAL's auto-correction. **(b)** `CrsInfo.EpsgCode` is `int?`, so a non-EPSG authority is *unrepresentable*: `CrsInspector` reads `GetAuthorityCode("PROJCS")`, gets `102003`, stores it in a field named `EpsgCode`, silently relabelling the authority — and `SpatialReader.SameCrs` then compares `102003 == 102003` as an authoritative match across two different authorities. **Fix:** carry authority and code as a pair, not more string handling. Gates P-14, T-15. |
| D-E | Decide whether the R-tree may index a geographic CRS. Its MBR math is planar; a degree-space box is not a metric box. Gates P-02 and P-44. **Sharpened by PR #9:** the box arithmetic is now all in `BoundingBox`, so this decision has one place to be implemented rather than two. |
| D-F | **New, from `04118f4`: the index rejects features it cannot place.** `addFeature` now throws on an extent-less or non-finite box rather than indexing it, and PR #11's T-18 guards it from both directions. That is the right default for a library, but it moves the policy question up a layer: a caller reading a file containing `POINT EMPTY` or a geometry type `ProcessGeometry` drops (P-21) now gets an exception instead of a degraded index. **Decide whether `BuildTree` filters-and-counts or propagates.** Recommended: filter, count, expose the count — see P-02. The green run at `04118f4` shows no existing input in this repository produces such a box, which means the decision is currently *unforced* and will be made by accident the first time real data arrives. |
| D-G | **New, from PR #12: is a style analyzer allowed to change the public API?** `d4fecd1` renames the public type `Features` (was `FeatureCollection`) and its public property `FeatureSet` (was `Features`) to silence one `CA1711` annotation. Section 9's recorded answer was **no — suppress it**; the commit did the opposite, without a changelog note and without naming the item. Both answers are defensible; *silently* is not. **Decide explicitly, then record it in one breaking-changes list alongside P-14, P-61 and P-67** (P-70). Until then assume the precedent exists: another `CA17xx` naming opinion could rename another public type in the next warning sweep. |

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
**Still live at `d4fecd1`:** `SpatialWriter.BuildOgrGeometry` filters empty parts on the
way in (`feat.Parts.Where(p => p.Vertices.Count > 0)`), but its `Point` arm still does
`parts[0].Vertices[0]` — safe *there* only because of that filter. The filter and the
indexing are 6 lines apart in one method and the safety depends entirely on the reader
noticing. Name the filter (`nonEmptyParts`) or assert the invariant.

### P-39 `BoundingBox.Empty` is the full-range box  *(partly mitigated by `04118f4`; characterised by tests at `b264607`; type unchanged)*
`Empty = new(MaxValue, MaxValue, MinValue, MinValue)` and the constructor normalises, so
`Empty` spans `[-1.8e308, +1.8e308]` on both axes. **The sentinel itself is unchanged by
`04118f4`** — only the index was fenced off from it. `d4fecd1` does not touch
`BoundingBox`. Consequences still live:
- `Area()` overflows to `+infinity`; **`Perimeter()` is also `+infinity`, not the
  ~`7.2e308` this file previously stated** — `MaxX - MinX` is `DBL_MAX + DBL_MAX`, which is
  `+inf` *before* the doubling, so there is no large finite value to report.
  `EnlargementToContain` inherits the same `+inf`. **`Area`,
  `Perimeter`, `ContainsPoint` and `EnlargementToContain` have no `== Empty` guard**,
  while `Overlaps`, `Contains`, `Union` and `OverlappingArea` do.
  Four unguarded members of one struct disagree with three siblings about the same value:
  `OverlappingArea(Empty)` = 0, `Area()` = +inf, `Perimeter()` = +inf. Now characterised by
  committed tests rather than by this prose (`PerimeterOfEmptyOverflows`,
  `EnlargementToContainIsAsymmetricAboutEmpty`), which is the right way for a fix-not-yet-made
  to be recorded.
- `ContainsPoint` returns true for every point, and is now the **only predicate in the
  file with no guard at all**.
- **The sentinel is not the only overflow source, and the gate does not catch the others —
  but the gap is one step narrower than stated here.** This entry claimed `±DBL_MAX` clears
  both checks and is `!= Empty`. **Wrong: `-double.MaxValue` *is* `double.MinValue`**, so a
  box built that way normalises to exactly `Empty` and the first check rejects it. The
  surviving input is a *near*-full-range box — `new BoundingBox(-1e308, -1e308, 1e308, 1e308)`
  is not `Empty`, is finite at all four corners, clears both gates, and still has
  `Area() == +inf`. Pinned by
  `NegatedMaxValueIsTheSentinelButNearMaxValueOverflowsUnnoticed`, which asserts both halves
  (the normalisation *and* the surviving overflow) so tightening the gate will trip it.
  Still reachable from garbage coordinates in a projected CRS — the same class that produces
  NaN, which *is* caught. When the magnitude bound lands, add the `Throws` case to that test.
- **`bestCandidate ??= TreeManager.Root` is load-bearing from the very first insert, and is
  now tested.** On a fresh tree `Root.BoundingBox == Empty`, so `Root.Area` is
  `+inf`, so `getAddedSizeToAccomodate` returns `+inf`, and `+inf < double.MaxValue` (the
  initial `minExtension`) is **false** — the candidate is rejected and `bestCandidate`
  stays null. Worked: `inf + featArea − OverlappingArea(bbox)` where `OverlappingArea`
  returns `0` against `Empty` → `inf`. The insert still succeeds, *because* of the
  fallback. Anyone who reads `??= Root` as defensive cruft and deletes it breaks the first
  `addFeature` of every tree. **Pinned by T-22**, which arrived as two members — the premise
  (`Root.Area` is `+inf`, `inf < MaxValue` is false) and the outcome — so the fallback's
  *necessity* is under test, not just the insert.
  **`d4fecd1` leaves this line exactly as it was** (`bestCandidate ??= TreeManager.Root` in
  `addFeatureChildEnforceIntersect`), and leaves the sibling `bestCandidate!.addFeatureChild(feature)`
  in the dead method using a null-forgiving operator instead — two opposite responses to the
  same nullable question in one file. See section 9.

**Correction, retracted as written:** a previous revision of this entry claimed
"`Feature.AddPart` unions it, so one empty part swallows its feature's entire MBR."
**That is false.** `AddPart` is `BoundingBox = BoundingBox.Union(part.BoundingBox)` and
`Union` short-circuits — `return other` when `this == Empty`, `return this` when
`other == Empty` — so an empty part leaves a real feature box untouched. Verified against
the normalised `Empty` (`MinValue,MinValue,MaxValue,MaxValue`), which is what the
constructor produces from the `Empty` initialiser, so `== Empty` does match. `T-12` is
retitled accordingly. **Now also pinned by a test:** `UnionTreatsEmptyAsTheIdentityElement`
asserts both short-circuits and that two real boxes are not swallowed.

**The escalation through the index, for the record** (steps 2–4 are now unreachable
through `addFeature`, but remain reachable through the public setters — P-15):
1. A feature with **zero parts** keeps `BoundingBox == Empty`. ~~`addFeature` validates
   nothing~~ **now throws** (`04118f4`), and the throw is guarded (`d3464e6`).
2. `RecomputeMBR` rebuilds a node from `Children.Min(c => c.BoundingBox.MinX)` / `.Max(…)`
   — **not** `Union`, so it has none of `Union`'s guards. A full-range child forces every
   ancestor to full range. **Re-read at `d4fecd1`: unchanged**, four separate
   `Children.Min`/`Children.Max` enumerations, i.e. four passes over the child list per
   recomputation, in a method `addChild` calls on every insert and `RecomputeMBR` recurses
   up the tree through. So this is both the guard-less path (correctness) and an O(depth ×
   children × 4) one (performance). P-42's file, P-40's neighbourhood.
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
the public API*. **The weakest link in that chain is now tested from both ends.**

**Fix, remainder:** an explicit `IsEmpty` flag, or a normalisation that keeps `MinX > MaxX`
inverted; `Empty`/overflow short-circuits in `Area`/`Perimeter`/`ContainsPoint`/
`EnlargementToContain`; and a magnitude bound or overflow check so the `1e308` case above is
closed too.
**One consequence to expect when the guards land:** `FreshRootHasInfiniteAreaSoTheComparisonNeverFires`
and `EnlargementToContainIsAsymmetricAboutEmpty` both assert the *current* `+inf` behaviour.
Making `Area()` return `0` for `Empty` turns them red — correctly, because `bestCandidate ??=
TreeManager.Root` stops being the only route to the first insert. Update those two comments
in the same commit; keep the assertions that describe outcomes.
**Sequencing:** settle P-62 in the same change, or the guards get written around members
that are about to be deleted.

---

## 2. P1 — correctness under load, or blocked features

### P-01 `getMBRoverlap` containment gate  *(CLOSED by PR #11 `d3464e6`, merged at `26d2840`)*
**Closed, and now closed on `main` rather than on a branch.** The two reopening reasons were
both about tests, and both are resolved by committed, un-skipped assertions:

1. *Nothing asserts `Overlaps`* — **fixed.** `GeometryTests.OverlapsIsSymmetricAndTrueForContainment`
   is 17 rows of hand goldens asserting **both** `a.Overlaps(b)` and `b.Overlaps(a)` per row:
   containment in both directions (the `fix(#15)` case, which is what this item was),
   identical, corner overlap, flush edge, flush corner, disjoint, point inside / on boundary /
   just outside, point-vs-point equal and unequal, zero-width line crossing / on boundary /
   outside, and a near-full-range box that must **not** short-circuit on size alone. Plus
   `OverlapsIsFalseWhenEitherSideIsEmpty` (three assertions, `Empty` on each side and both).
2. *The oracle is self-referential* — **not fixed, and now tracked on its own terms under
   P-48.2**, which is the honest home for it. P-01 was the defect and its guard; the guard
   exists. Leaving an unrelated test-independence debt open is what kept this entry
   unclosable across three revisions.

`BoundingBox.Overlaps` is now the most heavily guarded predicate in the library, and the
`≥ 1` floor stays deleted with a test saying why (`RTreeTests.PointShapedFeaturesSurviveSplitsAndAreFound`).

The defect, for the record: `RTreeNode.getMBRoverlap` only tested whether a *corner* of the
query lay inside the node, so a node fully contained by the query returned 0 overlap.
`CHANGES_09012026.md` P0-1 stated this defect inverted; `CHANGES_08312026.md` #15 stated it
correctly (N-6, N-7).

**Done by `9c7124a`:** the member is deleted and all four call sites redirected — three
descents in `RTreeNode` plus the oracle in `RTreeTests.FeatureIndicesAt` use
`BoundingBox.Overlaps`; `getAddedSizeToAccomodate` uses `BoundingBox.OverlappingArea`.
This is N-13 resolved the right way on the *code*: the survivor (`Overlaps`, written with a
closed-interval test under `fix(#15)`) is the correct one. **Re-verified at `d4fecd1`:** the
three descents (`getCandidateEndNodesByMBR`, `getCandidateFeatNodesByMBR`) and
`getAddedSizeToAccomodate` all still route through `BoundingBox`; no `getMBRoverlap` survives.

**Fix:** done — T-21 delivered in `d3464e6`. P-48.2 survives this entry.
**Correction retained:** an earlier revision of this file said "delete it and call
`BoundingBox.Overlaps`, preserving the *return ≥ 1 when overlapping* convention" —
nonsense, since `getMBRoverlap` returns `double` and `Overlaps` returns `bool`. The floor
existed so point features were not pruned; `Overlaps`' closed interval serves that purpose
better. The floor is gone and should stay gone. What it *also* happened to prevent is
P-66, now closed.

### P-66 An `Empty`-boxed node or feature is silently pruned from index search  *(CLOSED by PR #11 `d3464e6`, merged at `26d2840`)*
**Closed, on the file's own rule: a passing un-skipped test guards it.** Four test
members, all reaching the gate:

- `FeatureWithNoExtentIsRejected` — `Assert.Throws<ArgumentException>` on
  `addFeature(…, BoundingBox.Empty)`, **plus** `Assert.Contains("no extent", ex.Message)`
  and `Assert.Empty(tree.Root.Children)`. That last assertion is the one that matters: it
  proves validation precedes mutation, so the gate cannot be satisfied by throwing *after*
  inserting.
- `FeatureWithNonFiniteExtentIsRejected` — 5 rows: NaN in either of two corners, `+∞`,
  `−∞`, and `−∞ … +∞`. Each also asserts `Assert.Empty(tree.Root.Children)`.
- `PointShapedFeaturesSurviveSplitsAndAreFound` — 30 `BoundingBox.Point(i, i)` inserts
  (defaults min 4 / max 10, so several splits) plus a 31st inserted *after* the splits,
  each found through an independent `Findable` helper, **plus a negative control** at
  `(500,500)`. This is the test that retires the `≥ 1` floor permanently.
- `OverlapsIsFalseWhenEitherSideIsEmpty` — the guard itself, both directions.

**Residue, moved out rather than kept here:** the public setters (`RTreeManager.Root`,
`RTreeNode.BoundingBox`, unvalidated `addChild`) are **P-15**, and `1e308`-class overflow is
**P-39**. Both were always those items' jobs; P-66 asked for the gate and a guard, and has
both. The `Debug.Assert` suggestion is dropped — T-18 asserts the same thing on the one
reachable route, and the setter route is P-15's to close properly.

The defect, for the record: `getMBRoverlap`'s `Math.Max(xAxisOverlap * YAxisOverlap, 1)` did
double duty. Against an `Empty` node the corner gate passed, both axis overlaps came out `0`,
and the floor returned **1** → descend. `BoundingBox.Overlaps` carries
`if (this == Empty || other == Empty) return false;` → **prune**, taking the whole subtree
with it.

**Closed in effect by `04118f4`, through the invariant in P-39:** `addFeature` rejects an
`Empty` or non-finite box, so no leaf can be `Empty`, so no internal node can become
`Empty`, so the guard never fires on the search path. This is the right fix — the
alternative was a policy that tolerates unknown extents, which is the floor by another
name.

**Recorded while open, kept for the mechanism.** The gate shipped in `04118f4` touching no
test file, and the green run was the *proof* of that rather than reassurance — a `throw`
reachable from no test can be deleted by any future cleanup without a single failure. That
is why the rule "a green suite is not evidence" exists, and why this entry is now closed by
assertions rather than by the run.

### P-70 `Features`/`FeatureSet`: a breaking public-API rename performed as a warning fix  *(new, PR #12 `d4fecd1`)*
`d4fecd1` is titled "removing warnings" and its largest change is **renaming the public type
`FeatureCollection` to `Features`** (`Geometry/FeatureCollection.cs` → `Geometry/Features.cs`,
git similarity 68 %) and **its public property `.Features` to `.FeatureSet`**. `IFeatureSource.Read`,
`IFeatureSink.Write`, `SpatialReader.Read`, `SpatialWriter.Write` and `Feature.Owner` all change
signature. This is the **largest breaking change since 0.1.x**, larger than P-14's, P-61's and
P-67's, and it arrived with no item number, no changelog note, and no mention in the message.

**Four specific problems, in order of severity.**

1. **It contradicts a recorded decision.** Section 9's warning table said, about this exact
   annotation: `FeatureCollection.cs:8 CA1711 — Naming opinion, breaking rename. Suppress
   with a rationale in .editorconfig; do not rename.` The commit did the opposite. That is a
   legitimate call for the owner to make — **but it is a decision, and D-G now exists so that
   it is made once, in the open, rather than by whoever is fixing warnings that day.**
2. **The second break was involuntary and is the worse one.** Renaming the type to `Features`
   makes a member named `Features` illegal (C# forbids a member with its enclosing type's
   name), so `.Features` → `.FeatureSet` was *forced*. The result is that the library's
   central type is now `Features` and its central property is `FeatureSet` —
   `collection.FeatureSet` reads worse than `collection.Features` did, and every call site in
   `SpatialWriter`, `SpatialReader`, `SpatialJoins` and five test files had to change for a
   naming opinion. **When assessing the rename, judge the two breaks together, not the type
   name alone** — this is the part section 9's "do not rename" failed to predict (N-15 entry 12).
3. **It silenced one annotation by touching 13 files.** One `CA1711` cost ~90 lines of
   call-site churn across three assemblies and obscured the four warnings in the same sweep
   that were *not* fixed (section 9). The diff-to-value ratio is the worst in the branch.
4. **The rename is incomplete, which makes it a correctness hazard rather than only a style
   one.** `Feature.Owner`'s docstring still reads *"Set by `FeatureCollection`.AddFeature"* —
   a type that no longer exists. `Features.Crs`'s own summary still says "everything in this
   *collection*". `SpatialReader` still binds `var fc = new Features()`. `SpatialWriter.Write`
   still names its parameter `collection` while the interface it implements names the same
   parameter `features`. Each of these is small; together they mean **grep for
   `FeatureCollection` no longer finds the places that talk about the type**, which is the
   property that made the old name searchable. N-3 gains a sweep clause for it.

**Decide, then record.** Recommended: **keep the type rename** (`Features` is a better name
and the sooner the better, pre-0.1.x) **and fix the residue in the same PR**: `FeatureSet` →
`Add`/`Remove`/`IReadOnlyList` facade is already P-17's job, so let P-17 own that name rather
than re-opening it; add the stale docstrings to N-3's sweep; and add all four 0.x breaks to
one breaking-changes list. **What must not happen is leaving `FeatureCollection` in comments
while it is gone from code.**

**Guards:** none needed for a rename — the compiler is the guard, and the five test files
changing is the evidence it propagated. **What has no guard is the *decision***: nothing in
the repo records that the public surface changed deliberately, which is why the changelog
note is part of the fix.

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
**Not re-verified this pass:** `SpatialJoins.cs` changed by 18 lines in `d4fecd1`, consistent
with the `Features`/`FeatureSet` rename, but I did not read the hunks, so **the presence of
`_ = tree ?? BuildTree(features);` at `d4fecd1` is assumed, not confirmed.** Re-grep before
anyone starts this item.

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
involved — which supersedes the "the guard is circular" caveat in earlier revisions of this
entry, and closes what was planned as T-16. It also pins the `Integer`/`Integer64`
split that P-65's consolidation could collapse, so keep the `SMALL` assertion when the
mappers are merged. **Confirmed passing** — `Nsi.Geospatial.Io.Tests` succeeded at
`04118f4` and CI succeeded at `b264607`.

**Second guard, added by PR #11:** `FeatureAttributeTests.GetAttributeCoercesSoAValueOnlyAssertionCannotDetectAMistypedColumn`
makes this entry's central rationale executable rather than prose — it asserts the right
value *and* that the boxed CLR type is `string`, i.e. it pins the very situation where the
digits are correct and the schema lies. `GetAttributeWidensAnIntToLongWithoutLosingTheValue`
holds the `Integer`/`Integer64` split from the CLR side.

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
no longer casting to `int`) have **no test at all**. See P-68 / T-17. PR #11 added
attribute tests on the *model* (`Feature.GetAttribute`), not on the writer, and PR #12 did not
touch the arms — **re-read `SpatialWriter.MapFieldType` at `d4fecd1`: both arms intact, the
`// new` trailing comment still on the `OFTInteger64` line two commits after it stopped being
new.** That comment should go with T-17.

**Split out of this item, deliberately not fixed:** the write side's representability is
**P-64**; the `FieldType`↔OGR↔CLR four-table consolidation is **P-65**. This defect
recurred precisely because of P-65: `LongFT` was missing from three of the four tables.

**Resolved as a side effect:** the comment above `SpatialWriter.SetOgrField` claiming
"no object overload, and no `Layer.FieldIndex` in 3.11.3" is correct (D-C) and must not be
restated as doubt. Its trailing `P0-4 … intentionally left unchanged in this class`
sentence is now false and should be replaced — see N-6 for the numbering collision.
**Still present verbatim at `d4fecd1`.**

### P-12 `Ogr.RegisterAll()` thread-safety
Called on every `Read` **and** on every `Write`. Not idempotent-safe under concurrent
use. `tests/Nsi.Geospatial.Io.Tests/AssemblyInfo.cs` disables test parallelisation as a
workaround and names the registration guard as the real fix; that guard is this item.
A fourth entry point into the same non-idempotent call has been proposed (opening datasets
directly from a probe); the probes are gone, so the count is back to the two production
callers plus `SpatialIoTests`. Re-count before fixing. **Both calls re-confirmed at
`d4fecd1`** (`SpatialReader.Read` and `SpatialWriter.Write`, first statement of each).
**New adjacency:** PR #11's culture-mutating test sits in the *other* assembly, which has no
parallelisation guard — see P-19. If this item's guard lands, that file's implicit assumption
becomes an enforced one, which is the right outcome.

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
- **P-70 joins P-14/P-61/P-67 as the fourth 0.x breaking rename.** Keep them in one list so
  the eventual changelog note is written once.

### P-15 `Root` public setter; no box validation  *(now the only route to P-66's former failure)*
`RTreeManager.Root { get; set; }` accepts any node; `RTreeNode.BoundingBox { get; set; }`
accepts any box; `addChild` validates neither. `04118f4` closed the `addFeature` route,
which was the *automatic* one, and left these three, which are the *manual* ones. The
invariant that makes P-66's old failure unreachable is therefore maintained by one method and
assumptions, not by the type. **With P-66 closed on the `addFeature` route, this is now the
highest-value remaining R-tree item**, because it is what makes the invariant durable rather
than incidental.
**All three re-confirmed at `d4fecd1`:** `public BoundingBox BoundingBox { get; set; } =
BoundingBox.Empty;` is still a public setter, and `addChild` still runs
`BoundingBox = BoundingBox.Union(child.BoundingBox)` with no validation.
**Fix:** make `Root` set-once (or internal), make `RTreeNode.BoundingBox` settable only at
construction or through `Union`, and reuse `addFeature`'s two checks in `addChild`. Those two
checks now have a test on the `addFeature` path (T-18), so `addChild` reuse comes with a
reference implementation *and* a template for its guard.
**Sequencing note created by PR #12:** P-15 renames or restricts the very property P-63 wants
to rename (`RTreeNode.BoundingBox` → `MBR`). Do them in one change, not two — the property is
named in ~15 places in `RTreeNode` alone.
See P-39 for what an empty box does, P-66 for what it cost. Split out: P-51.

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
`Union`'s guards make it correct (P-39's retraction, now test-pinned by
`UnionTreatsEmptyAsTheIdentityElement`). The defect here is staleness under
mutation, not miscomputation at construction. Don't go looking for the former.
**Widen to `Features.FeatureSet`, which PR #12 left exactly as exposed as `FeatureCollection.Features`
was:** `public List<Feature> FeatureSet { get; } = new();` — public, mutable, and `Crs`'s
invalidation sweep plus `AddFeature`'s id assignment both assume nobody touches it behind
`AddFeature`/`RemoveFeature`. Same fix, same shape, second class. **One new detail from
`d4fecd1`:** `Features` now exposes `Count` and `this[int]` as forwarding members, so a caller
can enumerate through the facade and mutate through the list in the same expression — the
façade makes the hole easier to fall into, not harder. See N-10.

### P-18 Identity and `RemoveFeature`
`RemoveFeature` renumbers surviving ids and leaves the detached feature's `Owner`
pointing at the collection, so it still resolves the old CRS.
`RemoveFeatureLeavesTheDetachedFeatureResolvingTheOldCrs` pins this deliberately. It also
means the detached feature is outside `Features.Crs`'s invalidation sweep —
currently rescued only by `Part`'s `CrsInfo` identity check, which is now the sole
defence. Pin that with a test. `SpatialJoins.BuildTree` keys on `f.Id`, which mutates
under `RemoveFeature`.
**Re-confirmed verbatim at `d4fecd1`:** `RemoveFeature` is still
`FeatureSet.RemoveAt(index)` followed by a renumbering loop, with no `Owner = null` on the
removed feature. The rename touched the method and fixed none of it.

### P-19 Culture
Number formatting/parsing is not `InvariantCulture` in every path (`CsvHelper`,
`SpatialWriter` string fields). `Feature.GetAttribute` does use invariant — the others do not.
**Now test-pinned, not asserted here:** `FeatureAttributeTests.GetAttributeParsesStringsInvariantlyRegardlessOfCurrentCulture`
sets `CurrentCulture` to `de-DE` and asserts `"1.5"` still parses as `1.5`. Under `de-DE` a
period is the group separator, so a dropped invariant argument parses it as `15` — the test
fails loudly rather than shifting decimals silently in a locale. Saves and restores in
`finally`.
**One load-bearing and currently invisible assumption:** `Nsi.Geospatial.Tests` has no
parallelisation guard (unlike `Io.Tests`, per P-12), and this is the first culture-mutating
test in it. It is safe **because it is synchronous** — an `await` inside the try would let
another test observe `de-DE`. Add one line to the file saying so; do not make the test async.
**Adjacent, re-confirmed at `d4fecd1`:** `SpatialWriter.SetOgrField`'s `default:` arm *does*
pass `CultureInfo.InvariantCulture` to `Convert.ToString`, so the fallback path is invariant
and untested, while the `float`/`double` arms hand numbers to OGR's own converters with no
culture argument at all. The untested surface is the OGR side, not the `ToString` side — worth
knowing before writing T-17's culture assertions.

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
**Now characterised at the model level** (PR #11): `AreaOfAnEmptyOrUnreferencedFeatureIsUnknown`
asserts a zero-part `Feature` has `Parts` empty, `AreaSquareMeters` null, `Crs.Kind ==
Unknown`, and `BoundingBox == Empty` — i.e. exactly the box `addFeature` refuses. The drop
itself (reader side) is still untested, and **`d4fecd1` did not touch `ProcessGeometry`**: the
`wkbMultiLineString` emit path in `SpatialWriter` and the missing `else` are both intact.

### P-40 All-leaves fallback makes the build quadratic
`insert` falls back to scanning all leaves; with P-41's enlargement metric this is
quadratic in feature count.
**Made slightly worse by `d4fecd1` in an unrelated way:** `RecomputeMBR` runs four separate
`Children.Min`/`Children.Max` enumerations per call and recurses to the root (see P-39's step 2),
so per-insert cost is O(depth × children × 4) before any of P-40's scanning is counted. Not a
new defect — the shape was always there — but it is now visible because the same method is on
section 9's fix list for other reasons.

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
**Re-confirmed at `d4fecd1`:** the body is byte-identical, including the hand-rolled
`featArea`. Two commits of warning sweeps have now passed over this line without touching it,
which is consistent with it being correct-but-wrong: it compiles, it warns about nothing, and
it produces a plausible number.
**Caveat for P-40:** enlargement is a *cheaper* comparison but does not change the
asymptotics.
**Guard:** T-20 still open — nothing yet asserts *which child an insert chooses*, so the
fix itself remains unguarded. But the member it substitutes **now has goldens** (PR #11):
`EnlargementToContainIsMbrGrowth`, 5 hand rows including the two this entry worked by hand
(`[0,10]²` vs `[20,30]²` → **800**, not 100; `[0,10]²` vs `[5,15]²` → **125**), nested → 0,
identical → 0, point-growing-to-box → 100. So the one-line swap is no longer "a substitution
into a member with no test".
**New constraint the goldens exposed, and it argues for sequencing this after P-39:**
`EnlargementToContain` is **asymmetric about `Empty`** — `box.EnlargementToContain(Empty)`
is `0`, but `Empty.EnlargementToContain(box)` is **`−∞`**, and `−∞` sorts *first*.
Substituting this into `getAddedSizeToAccomodate` today would let any `Empty` node win every
insertion and then prune the feature on search. Unreachable while `addFeature`'s gate holds
(T-18 now guards that) and while `Union`'s identity guards hold (now guarded by
`UnionTreatsEmptyAsTheIdentityElement`) — but it is a real ordering hazard, which is precisely
why section 10 still puts P-41 last in the box-model cluster.

### P-42 `buildChildOptions` re-parents live children
While scoring candidate placements it re-parents children of the *real* node, then
discards the options. Mutates the tree during a read-only decision.
Now also the owner of the file's three remaining nullability warnings — its
`List<RTreeNode> sortedChidrens = null;` is `RTreeNode.cs:73`, one of the six real
warnings in section 9. **Still there at `d4fecd1`: PR #12 passed directly through this method
(renaming `MaxChidrens`→`MaxChlidren` inside it) and left the `null` initialisation and the
four-branch `if`/`else` structure untouched.** Fixing this item properly (build the sort key,
then order once) removes the `null` initialisation as a side effect rather than annotating it,
and would also delete the third spelling of the misspelling (P-22).
**Also unchanged and worth restating:** `buildChildOptions` still assigns
`node1.siblingOverlap` / `cumulativeOverlap` and never reads them (P-22), and still scores
options into `options` for `Options.First()` to sort (P-43).

### P-43 `Options.First()` throws; no min/max invariant
The split loop `for (split = MinChlidren; split <= Children.Count - MinChlidren; split++)` is
empty when `Count < 2*min`. At split time `Count == max + 1`, so the invariant is
**`max >= 2*min - 1`**. Defaults (10, 4) and the tests' (6, 3) satisfy it; `new
RTreeManager(4, 6)` throws from inside `split()`. Validate in the constructor with a
clear message.
**Parameter-name drift is now three-way, and PR #12 added the third variant.** Before:
`RTreeManager`'s public constructor took `minChilds`/`maxChilds` while the properties were
`MaxChidrens`/`MinChidrens`. Now: the properties are `MaxChlidren`/`MinChlidren`, the local in
`buildChildOptions` is still `sortedChidrens`, and `RTreeManager`'s constructor parameters are
unchanged. **Three spellings of the same non-word in one file** (`Childs`, `Chidrens`,
`Chlidren`), none of which is `Children`. P-22's "the misspelling family is wider than the
fields" was right; the fix as executed made it wider rather than closing it. See section 9's
PR #12 table — the two `CA1805`s this rename was bundled with were the *only* thing it fixed
about these members.
**Answered by the clean build:** the two `CA1805` warnings (`RTreeNode.cs:11,12`) confirmed
both initialisers were pure noise. **Both deleted by `d4fecd1`** — that part of the item is
done, and it is the only part of P-22/P-43's naming cluster that is.

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
**Test-side note from PR #11:** `Findable` in `RTreeTests` is effectively a private
rectangular-membership query written in the test file, with a negative control. When
`Query` lands, it has a ready-made independent oracle to be checked against — that is T-23's
shape already, half-built.
**New, from reading `RTreeNode.cs` at `d4fecd1`:** there is a **second** traversal,
`getCandidateFeatNodesByMBR`, that `getCandidateEndNodesByMBR` does not have and that this
file has never mentioned. It differs in the leaf branch: where `getCandidateEndNodesByMBR`
adds `this` unconditionally, this one tests `node.BoundingBox.Overlaps(bbox)` on each child
and then adds **`this`, not `node`**. Whether that is the intended contract (returning
feature-parents) or a copy-paste of the unconditional `Add(this)` is undetermined — and it
matters, because the two methods give different answers for the same box. **I did not read
`RTreeManager.cs` at this SHA, so I am not claiming it is uncalled or that the asymmetry is
a bug**; whoever takes P-47 should resolve which traversal is canonical before wrapping
either, because wrapping the wrong one makes the public `Query` inherit the ambiguity.

### P-48 The R-tree test suite cannot detect a regression
1. **No regression test for MBR propagation** (`16585ef`, closed as P-49). The fix that
   made the index return all features is unguarded.
2. **The brute-force oracle is no longer independent, and the damage is concentrated in the
   one test that mattered** — sharper than first recorded. `FeatureIndicesAt` used to
   cross-check `findByXY` against a second, *differently broken* implementation
   (`getMBRoverlap`); it now calls `BoundingBox.Overlaps`, the predicate the traversal under
   test calls. `FeatureIndicesAt` is unchanged by PR #11, so its two callers are
   self-referential — above all `BulkInsertAllFeaturesFindableByPoint`, 500 features at
   `minChilds: 3, maxChilds: 6`, the strongest end-to-end test in the repository. Before
   `9c7124a` its passing meant "traversal and an independent predicate agree despite both
   being wrong"; now its remaining power is "every feature was indexed and the walk reaches
   the leaves", and it **cannot detect a wrong `Overlaps` at all**. PR #11 partially answered
   this in the new tests — `Findable` is written independently of `Overlaps`, and
   `PointShapedFeatures…` carries a negative control — which makes the untouched oracle the
   specific gap. T-23. **Unchanged by PR #12**, which is the reason to do this before the next
   R-tree change: a rename pass over `RTreeTests` that *doesn't* fix the oracle is the
   situation getting further from correct while the diff gets bigger.
3. No test for `min`/`max` invariant (P-43).
4. No test that the tree's boxes match the features they index. `RecomputeMBR`'s
   `Children.Min/Max` form versus `Union` is precisely where they could diverge, and
   P-39's `1e308` case is a concrete input where the two give different-looking results.
5. ~~public `throw` with no test~~ **Closed by `d3464e6`** — T-18, four members, both
   directions, `Assert.Empty(tree.Root.Children)` proving the throw precedes mutation.
6. ~~first-insert path untested~~ **Closed by `d3464e6`** — T-22 arrived as *two* members:
   `FreshRootHasInfiniteAreaSoTheComparisonNeverFires` pins the premise itself, and
   `FirstInsertIntoEmptyTreeLandsOnRoot` pins the outcome plus `Assert.Same(tree.Root,
   tree.Root.Children[0].Parent)` and that a real box propagated over `Empty`.
7. ~~`RTreeTests` docstring claims the tests do not assert correct behaviour~~ **Closed by
   `b264607`.** N-3's `getMBRoverlap` sweep clause is satisfied for source and comments.
8. Superseded: ~~no test for the containment gate (P-01)~~ — closed with P-01.
9. **New, from `d4fecd1`: the suite cannot tell `Features.FeatureSet` from a private
   façade.** Five test files changed in the rename, all through the public mutable list, and
   no test asserts that `AddFeature`/`RemoveFeature` are the only supported mutation route.
   That is P-17's hole, and until it is closed a rename of `FeatureSet` will keep touching
   tests that are quietly relying on it being a `List`.

**Still open: .1, .2, .3, .4, .9.**

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
third-party warning noise. This is what P-30's scoped `NoWarn` has to work around twice (see
P-30's correction). **CI at `b264607` annotates neither copy** (section 9), which does not
disprove the local doubling — it means CI's raw build log is the only remaining evidence
either way, and nobody has read it.
**Fix:** fold into `Nsi.Geospatial.Io`, delete the csproj and the solution entry — which
also halves P-30's problem. Public namespace change, same caveat as P-14: 0.x breaking
removal, document it.
**PR #12 changes this item's arithmetic in one useful way:** the rename touched
`IFeatureSource`/`IFeatureSink` in `Io` but not `Reprojection`, which is a clean demonstration
that the split boundary is not where the API lives. Still the cheapest structural deletion in
the file.

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
`SpatialWriter` calls `fdefn.SetWidth(c.Length)` with `Length == 0`, so the driver picks, and
**the width ceiling for a `long` is 18 — never 20.** A width of 20 *causes* the
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
adjacent failure mode (there: non-finite; here: width overflow). Copy that pattern. P-69 is
the same class arriving from the opposite direction: a filter where a refusal belongs.
**Still open inside P-64, same shape, untested — all three re-confirmed at `d4fecd1`:**
- `fdefn.SetWidth(c.Length)` still passes the column's `Length` with no representability
  check, and `SetPrecision(c.DecimalPlaces)` beside it is unchecked the same way.
- `bool` is written as `"1"`/`"0"` into an `OFTString` column (`MapFieldType` has no
  `BooleanFT` arm — the `_ =>` default catches it, re-confirmed) and
  `AttributeColumn.Coerce`'s `BooleanFT` branch uses `bool.TryParse`, which rejects `"1"`.
  A `bool` does not survive a round trip as a `bool` — it returns `null`. Needs
  `BoolFieldTests` mirroring `FieldTypeTests`, plus the three arms.
- `FloatFT` is written via `f.SetField(name, (double)fl)`, so a `float` returns as a boxed
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
**Reinforced by PR #11:** `OverlapsIsSymmetricAndTrueForContainment` asserts `true` for
flush-edge and flush-corner contact, where the overlap area is exactly `0`. So the two
members' disagreement at zero-area contact is now **pinned in both directions** — `Overlaps`
true, `OverlappingArea` 0 — which is the correct semantics and is no longer deniable by a
future "consistency" edit.

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
**Model-side behaviour now pinned** (PR #11):
`MissingAndNullColumnsYieldDefaultsRatherThanThrowing` asserts that on `Feature` itself —
absent and null columns both yield `default(T)` for value types, `null` for `string`, and
`string.Empty` from `GetAttributeAsString`. So the *model* layer's choice is decided and
guarded; the open half of P-28 is that the **reader and writer** still disagree about which
of `""`/`null` crosses a file boundary. Don't re-litigate the model behaviour when fixing
that half — it is now a test.
**Writer side re-confirmed at `d4fecd1`:** `SetOgrField` still begins `if (value is null)
return; // leave the field NULL`, so the writer's convention is "null stays null" and the
asymmetry lives entirely in the reader.

### P-46 `getCandidateEndNodesByMBR` performs no MBR test at the leaf
Descends using MBRs then accepts every leaf entry, so the pruning is illusory below one
level. **PR #9 half-closed this:** the *descent* now uses `BoundingBox.Overlaps`, so the
interior level is correct; the leaf acceptance in `getCandidateEndNodesByMBR` is still
unconditional (`nodeWalk.Add(this)` with no test at all). **Byte-identical at `d4fecd1`.**
`BoundingBox.Overlaps`/`Contains` remain the missing test at the leaf, and `Overlaps` is now
proven live in three other places **and heavily unit-tested (P-01 closed)**, so the fix has no
remaining excuse. Note the leaf-level fix is what makes a rectangular `Query` (P-47) actually
prune — and see P-47's new note about the *other* traversal, which already attempts a
leaf-level test and adds a different node than this one does.

### P-50 `getIsEndNode` inspects only `Children[0]`
Assumes all children of a node are at the same level. True today because splits only
produce same-level siblings; unguarded and will silently mis-classify if that changes.
Assert it. **Unchanged at `d4fecd1`**, and note the first arm
(`Children.Count == 0 && FeatureIndex == null`) makes a *childless, feature-less* node an end
node — which is what a fresh `RTreeNode` is, and is why `FindByIndOnAnEmptyTreeReturnsNothing`
returns nothing rather than throwing. That behaviour is now tested; the assumption behind it
still is not.

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
**PR #12 touched three of the four files this item names** (`CrsInspectionTests` 15 lines,
`CrsInfoAndAreaTests` 9, `SphericalMetricsTests` 24) and fixed none of them — which is the
cheapest missed consolidation in the branch: three of the four files were already open.

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
**Third whitespace-only pass over this method without a fix.** `d207943`/PR #10 reindented
`BuildOgrGeometry` 55/55 and left the line byte-identical; `d4fecd1`/PR #12 churned the same
~114-line region again with, on the content I can read, no semantic change at all. The line
`if (verts[^1].Coordinates != first.Coordinates)` is present and unchanged at `d4fecd1`.
**Two live facts here:** the defect survives its third appearance in a diff, and the file has
now been reformatted twice by two different passes that both satisfy `dotnet format` — see
section 9's note on what that implies about the check.

### P-62 `BoundingBox` public surface with no owner  *(extends N-1; `04118f4` adds one member; untouched by PR #12)*
Four uncalled members were listed at `7b4c6aa`; `Overlaps` and `OverlappingArea` are now
called from production, and `Perimeter()` added in `04118f4` is called immediately through
`RTreeNode.Perimeter`, so it is not dead. Remaining:

- `Contains` → **keep**, P-46's leaf test. Still uncalled.
- `EnlargementToContain` → **keep**, P-41's fix. Still uncalled, but **no longer untested**:
  PR #11 gave it 5 hand goldens plus the `Empty` asymmetry case, so the substitution is now a
  change to a guarded member. See P-41 for the ordering hazard those tests exposed.
- `ContainsPoint` → homeless, wrong for every point under P-39, and now the **only
  predicate in the file with no `== Empty` guard**. Either guard it and use it (joins and
  `findByXY` are candidates), or delete it. It is the one `BoundingBox` predicate with **no
  test of any kind** after PR #11.
- `FromVertices` → genuinely unreferenced with no planned consumer (`Part` maintains its
  MBR incrementally via `Union`; `Feature` recomputes by `Union`). Two corrections to what
  this file said about it, both from PR #11's tests:
  - The `double.IsPositiveInfinity(minX)` check **is** dead — `minX` starts at `MaxValue`
    and only decreases — but the empty-input path does **not** return a garbage box. It
    returns `Empty`, via the constructor's normalisation. `FromVerticesOfNothingIsTheSentinel`
    pins that. The dead branch is harmless, not a defect; the earlier claim that
    `addFeature`'s new gate gave it a reason to exist was based on that wrong output.
  - **The live defect is elsewhere and was not suspected: `FromVertices` silently drops
    non-finite vertices.** `[(0,0), (NaN,NaN)]` yields the box `[0,0]²` — a perfectly
    *finite*, plausible, *wrong* box that `addFeature` would accept without complaint. This
    is worse than the garbage box this entry predicted: garbage is refused by the gate,
    plausible-wrong is not. Filed as **P-69**.
  So: delete, or fix the NaN filter and then test it. `FromVerticesSilentlyDropsNonFiniteVertices`
  currently characterises the bug so a fix cannot land silently.
- **`Point(x, y)`** — was never called from production, and `RTreeManager.findByXY`
  hand-writes `new BoundingBox(x, y, x, y)` three lines away. **PR #11 made it a test
  dependency:** `PointShapedFeaturesSurviveSplitsAndAreFound` and
  `FindByIndReturnsAPathToTheFeature` build their degenerate boxes with it, so deleting it is
  now a test edit too. Use it in `findByXY`, or keep it as the canonical way to express a
  zero-area box — but decide, because "uncalled" is no longer accurate.

**Telling:** `SpatialIoTests` needed a point-in-region check and hand-rolled
`PointInPolygon` rather than calling `ContainsPoint`. Same N-13 pattern as `Overlaps` vs
`getMBRoverlap`. Resolve inside P-39's change so guards are not written around members
about to go.
**One member's status improved by PR #11 in a way to preserve:** `Union`'s two `Empty`
short-circuits are what let a fresh `Empty` node acquire a real box in `addChild`. That
invariant was carried by prose; `UnionTreatsEmptyAsTheIdentityElement` now carries it.

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
is precisely the hole P-68 describes, and precisely the mistake P-01's reopening was about.
**PR #11 does not change this ordering.** `FeatureAttributeTests` covers `Feature`'s own
coercion, which is a *fifth* mapping surface (`AttributeColumn.Coerce`) rather than one of
these four; note it when building the consolidated table so the new file is not mistaken for
write coverage.
**PR #12 is a cautionary example of exactly this:** it renamed the *type* four tables refer to
across three assemblies, and none of the tables moved, so a future consolidation now has to
reconcile `Features` in the new code with `FeatureCollection` in the docstrings (P-70's residue)
as well as the four tables' own disagreements.

### P-68 `FieldTypeTests` is the residue of a larger file, and its docstring now lies  *(new, PR #8 `9bee4e4`)*
`FieldTypeTests.cs` as committed contains **one** test. The constants, aliases and
docstring are leftovers from the six-test version, and the drift is actively misleading:

- Class docstring: *"LongFT through the writer and reader (P-06)"*. **No writer is
  involved** — the sole test reads a hand-written GeoJSON string. The sentence describes
  the coverage the file no longer has, and it is the coverage P-06's closure might be
  read as claiming.
- Dead: `const string Column = "BIG"`, `const long AboveInt32` (the test writes
  `4_000_000_000L` inline), the `Feature` and `OGR FieldType` alias blocks, and
  `using OSGeo.OGR;` / `using Nsi.Geospatial.Geometry;`. The alias comment is three lines
  long and defends aliases that are no longer used.
- **The real cost:** the write arms `9bee4e4` landed in the same commit —
  `SpatialWriter.MapFieldType`'s `LongFT => OFTInteger64` and `SetOgrField`'s `case long l:`
  no longer casting to `int` — have **zero coverage**. P-06's closure is
  sound for the read path only; nothing prevents a revert of either writer arm shipping
  green, and it still does. T-17 closes this and is the reason P-65 should follow it, not
  precede it. **PR #11 added no Io-side tests and PR #12 did not either**, so this is unchanged
  at `d4fecd1` — although PR #12 *did* edit `SpatialWriter.Write`'s signature and its
  `foreach (var feat in collection.FeatureSet)` loop, i.e. it modified code inside the
  untested write path twice without adding a test for it.

**Fix:** delete the dead constants and aliases, retitle the docstring to what the file
tests, and add T-17. Keep the "two independent assertions" rationale — move it onto the
test that still earns it.

### P-69 `FromVertices` silently drops non-finite vertices  *(new, PR #11 `f86c689`)*
Found while testing a member this file assumed was merely dead. `BoundingBox.FromVertices`
skips vertices that fail a finiteness test instead of rejecting the input, so
`[(0,0), (NaN,NaN)]` returns `[0,0]²`. The result is finite at every corner, is not `Empty`,
and **clears both `addFeature` gates** — the index accepts a box that omits geometry it was
handed. Contrast the `addFeature` gate, which does the opposite and correctly: refuse at the
boundary and name the value.

Characterised, not fixed, by `GeometryTests.FromVerticesSilentlyDropsNonFiniteVertices`.
**Fix:** decide which is right — throw naming the offending vertex, or return `Empty` so the
caller's gate fires — and change the test to assert it. Do not leave it as silent filtering:
a box that quietly shrinks is the failure mode P-64 and P-21 are both about.
**Severity depends on adoption.** Nothing calls `FromVertices` today (P-62), so this is P2 by
reach and P1 by consequence. It becomes live the moment P-62 adopts the member, which is the
other half of why P-62's keep-or-delete decision should be made before anything calls it.
**Same shape, newly noticed in `SpatialWriter` at `d4fecd1`:**
`feat.Parts.Where(p => p.Vertices.Count > 0)` silently drops empty parts before emitting
geometry, so a multi-part feature with one empty part writes a *smaller* geometry than it
has parts, with no diagnostic. That is P-69's pattern in the writer, and P-11/P-21's data
loss in the same one-line shape. Not filed separately — fold it into P-21's fix, where the
filter's existence is the thing being reconsidered anyway.

### P-67 `getArea` / `getPerimeter` duplicated `BoundingBox` arithmetic  *(closed by `04118f4` — duplication half; the `Empty` half moved to P-39)*
`RTreeNode` carried its own area and perimeter formulas, both `Empty`-blind, both feeding
`split()`'s tie-breaks (`totalArea` is the second sort key, `perimeterTotal` the third), so
one uninitialised child could dominate the split ranking silently.
**Deleted in `04118f4`:** `getArea` → `public double Area => BoundingBox.Area();`,
`getPerimeter` → `public double Perimeter => BoundingBox.Perimeter();`, with a new
`BoundingBox.Perimeter()` member. `buildChildOptions`, `addFeatureChild` and
`getAddedSizeToAccomodate` updated to the new names. One definition of box size now wins.
**Confirmed complete** at `04118f4`, at CI `b264607`, and again at `d4fecd1` — the two
delegating properties are still the last two lines of `RTreeNode` and unchanged.
**Now guarded, one level up** (PR #11): `PerimeterIsTwiceTheSumOfTheExtents` pins the
formula on real boxes and `PerimeterOfEmptyOverflows` pins the `Empty` behaviour;
`NodeAreaAndPerimeterAreTheBoundingBoxes` asserts the `RTreeNode` properties actually
delegate. So the delegation this entry asked for is under test, not merely committed.
**What is *not* fixed, and why the entry stays:** the `Empty`-blindness was
not removed, only relocated into `BoundingBox`, where it is now visible as a disagreement
between adjacent members of one type — `OverlappingArea` returns `0` for `Empty` while
`Area()` and `Perimeter()` both return `+inf` for the same value (`Perimeter` was previously
recorded here as ~`7.2e308`; that was wrong — the subtraction overflows before the doubling).
`PerimeterOfEmptyOverflows` pins the true value. That contradiction is P-39's to settle;
P-67 asked only for one definition, and got one.
**Breaking-change note:** `getArea`/`getPerimeter` were public members of a public class.
Renaming them to `Area`/`Perimeter` is a source-breaking API change like P-14's, P-61's and
**P-70's** — note it before 0.1.x is consumed.

### P-30 README and warnings-as-errors  *(three errors at `b264607`; was four)*
**One of the four fixed by PR #11 `518630c`:** the Conventions line now reads
``TreatWarningsAsErrors=false``, which matches `Directory.Build.props`. Three remain.
**New nit on that very line:** it states a *workaround* under **Conventions**, and the
Conventions list is what a contributor reads first and then tries to "correct". Reword to
the reason, e.g. "warnings are not errors: the GDAL NuGet package emits `CS8600` from
generated `obj/…/GdalConfiguration.cs` (see P-30)". Otherwise the next person flips the
flag to match the stated convention and breaks the build for a third-party file.
Also relevant to P-30's premise: **analysis applies to the test assemblies too** —
`CA1050` and 4× `CA1861` fired on test code in this cycle — so flipping
`TreatWarningsAsErrors` gates on test files as well as production ones. Section 9's path
must be walked in both.
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
warnings. So the scoped `NoWarn` must be added to **both** `Nsi.Geospatial.Io.csproj` and
`Nsi.Geospatial.Reprojection.csproj` — or, better, P-61 folds Reprojection into Io and
one of the two copies disappears entirely. Do not add a global `NoWarn`: `RTreeNode.cs` has
three genuine `CS8600`s of its own (section 9) that a global suppression would silence —
**and `d4fecd1` left all three of them in place, so the reason not to globalise is still
live.**
**PR #12 did not touch `Directory.Build.props`, either csproj, or `.editorconfig`.** So the
`suggested` CA1711 suppression was bypassed rather than applied (see P-70, D-G), and the flag
remains `false` with **five** code warnings outstanding instead of six.
Full warning inventory with exact lines is in section 9.

### P-31 SDK policy
The pinned SDK version is stated five inconsistent ways across `global.json`, `ci.yml`,
`release.yml`, the README and `Directory.Build.props`.
Sixth inconsistency, and the local machine has already drifted: this run again reports
`xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.11)` while every project
targets `net8.0`. Nothing noticed, which is the point of pinning.
**Seventh, from CI at `b264607`:** the `build` job annotates **Node.js 20 deprecation** for
`actions/cache@v4`, `actions/checkout@v4`, `actions/setup-dotnet@v4` and
`mamba-org/setup-micromamba@v2`, all forced onto Node 24. Not the SDK pin, but the same
class of "the toolchain the repo names is not the toolchain that runs".
**Not addressed by PR #12**, which is worth noting because PR #12's stated purpose was
"removing warnings" and this is one of the eleven CI annotations — the only one that is
neither our code nor GDAL's, and the only one fixable without touching C#.

### P-32 `IsPackable`
Test projects are packable.

### P-33 `gdal` → `GDAL` naming.

### P-34 `release.yml` references `@main`.

### P-35 CI runs `Io.Tests` twice.
**Confirmed at `b264607`'s `ci.yml`, and it is worse than "twice":** the step labelled
`Test (non-Gdal)` is `dotnet test Geospatial.slnx -c Release --no-restore` with **no
`--filter` at all**, so it runs the whole solution — `Io.Tests` included — and the following
step runs `Io.Tests` again. The label promises a partition that does not exist, so a
Gdal-dependent failure is reported twice and nobody can tell which step meant to own it.
Fix is one flag: `--filter "Category!=Gdal"`. Also note CI builds and tests `-c Release`
while local runs are Debug (section 9), so a Release-only failure has no local repro command.
**Unchanged at `d4fecd1`** (`.github/` is not in the diff). **Consequence specific to PR #12:**
because the second test step exists, a rename that missed one call site in `Io.Tests` would
still be caught — but only after the first step already failed for the same reason, in the
same job, with the same message twice.

### P-36 Test project duplication
Three overlapping test files, two namespaces (`Nsi.Geospatial.Tests` and
`Nsi.Geospatial.Core.Tests`), and two independent copies of `Rel` plus two copies of
`PointInPolygon`. Every fix has to be applied twice (P-53 already does). Hoist one
`TestAssert` and shared geometry helpers.
**Reduced by PR #8 `4a0ec8b`:** `SphericalMathTests.cs` deleted (−246). It was a subsumed
earlier generation of `SphericalMetricsTests.cs` — ~90 % duplicated, each case in a weaker
form. Both assertions worth keeping were ported first, and the D-B spec tests survived. Two
files remain, still in two namespaces inside one assembly — which is *why* the duplicate went
unnoticed: the classes could not collide by name.
**Grown by PR #8 `9bee4e4`:** `FieldTypeTests.cs` adds a **third** copy of
`TempDir()`/`Cleanup()`. Hoist before adding a fourth.
**Grown by PR #9 `9c7124a`:** `GeometryTests.cs` (new, 73 lines) lands in
`Nsi.Geospatial.Tests` — the *other* namespace — so it will not see the shared helpers
when they are hoisted, and the split survives a third file. It also opens with three
unused `using`s (`System.Collections.Generic`, `System.Linq`, `Nsi.Geospatial.Spatial`;
the file references only `BoundingBox` and `Xunit`), which is the exact defect class
`7b4c6aa` was deleting. **Confirmed still present at `b264607` and invisible to CI** — the
format step passes with them, because `IDE0005` is not in `.editorconfig` (section 9). Decide
the namespace once, in the hoist.
**`04118f4` added no tests.** **PR #11 reversed that** with ~35 new test members across three
files, and that is why P-01 and P-66 are now closed. It grew the split in four ways:

- **`FeatureAttributeTests.cs` is a fourth file in the `Nsi.Geospatial.Tests` namespace**
  (104 lines, 6 members). Its name is fine; the namespace accumulation is P-36's problem.
- **`GeometryTests.cs` is now ~290 lines and is entirely `BoundingBox`** — `OverlappingArea`,
  `Overlaps`, `Union`, `EnlargementToContain`, `Perimeter`, `FromVertices`, overflow. Nothing
  else lives there. **Rename it `BoundingBoxTests` before the next member lands**, while the
  rename is one file and no other class shares the name. The name is already misleading in a
  file whose own docstring says "Tests for the Geometry stuff".
  **PR #12 is the argument for doing this now rather than later:** it is a rename PR that
  renamed a production type across five test files and did not touch this one, because the
  name mismatch makes it non-obvious which files are about what. Every rename PR will keep
  skipping it for the same reason.
- **A duplicate I introduced:** `FindByIndReturnsAPathToTheFeature` (30 point features)
  overlaps `FindByIndReturnsLeafToRootPath` (100 features, predating PR #11) — both build a
  tree and call `findByInd`. Not contradictory (`getPathReverse` prepends `this` then walks
  `Parent`, so `path[0]` is the leaf and `path[^1]` the root, which is what both assert), but
  two tests where one belongs. Fold the root assertion and the two absent-id cases into the
  existing test and delete mine. The failure was process, not knowledge (N-13's fourth lesson).
- **Inconsistent helpers in one class:** `zerozero`/`onezero` (lowercase, top of file,
  `new[] {…}` form) versus `Id0`/`Id7`/`Id99` (PascalCase, mid-class, collection-expression
  form). One block, one style, both syntaxes equivalent. The `CA1861` rationale comment also
  exists in two flavours.
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
- **PR #11:** two point-set helpers in `RTreeTests` doing the same job with different
  contracts — `FeatureIndicesAt` (returns the hit set, self-referential per P-48.2) and
  `Findable` (returns a bool, independent of `Overlaps`). `Findable` is the better one; when
  P-48.2/T-23 rebuilds the oracle, consolidate on it rather than keeping both.
- **PR #12 adds a new class of duplication: the same construction expression rewritten per
  test file.** `SphericalMetricsTests` (24 lines), `SpatialIoTests` (35),
  `CrsInspectionTests` (15), `CrsInfoAndAreaTests` (9) and `SpatialJoinTests` (5) all changed
  only because each builds its own `Features` literal. A shared
  `TestFeatures.With(params Feature[])` helper would have made this a one-file PR. This is
  the first *measurable* cost of not hoisting: **~88 lines of test churn for a rename, all of
  it avoidable.**

### P-37 Delete probes and dead writer helpers  *(done, PR #8)*
`ProbeOsrBinding.cs` deleted in `051e8d8` (−115); `SpatialWriter.ClosedRing`, `.RingWkt`,
`.Fmt` deleted in `7b4c6aa`. The WKT emission path those three served is gone for good —
`BuildOgrGeometry` builds geometry through the OGR API because `CreateFromWkt` rejects
valid polygon WKT on the 3.11.3 binding. Keep that comment; without it the helpers look
like an accidental omission. **Comment intact at `d4fecd1`, and it survived two reindents of
the method it explains.**
**Correction:** an earlier revision of this entry warned that `9bee4e4` had added two more
`Assert.Fail` diagnostics as a *recurrence*. It had not — those existed only in the
working tree and were never committed (N-11). The warning was right as a rule and wrong as
a fact; the rule is what to keep: **a measurement that cannot be expressed as a passing
assertion belongs in prose here, not in the test suite.** PR #11 respected this: every new
test asserts a behaviour that holds today, including the ones characterising defects
(`FromVerticesSilentlyDropsNonFiniteVertices`,
`NegatedMaxValueIsTheSentinelButNearMaxValueOverflowsUnnoticed`), which pass on the buggy
code and are labelled as characterisation rather than aspiration.

### P-38 `Feature.ShapeType` vs `Features.ShapeType`
Two sources of truth, free to disagree. **Unchanged by PR #12** — the rename moved both
declarations and reconciled neither, and `SpatialWriter.MapShapeTypeToOgr(collection.ShapeType)`
still trusts the collection's value while `BuildOgrGeometry` switches on it per-feature-part
shape assumptions.

### P-51 `FeatureIndex` unguarded `[0]`  *(P1 → P3, but now pays for itself — and PR #12 did not pay it)*
`getChildrenContainingInd` dereferences `FeatureIndex[0]` without a guard, but it is
unreachable through the public API: it needs mixed-level children, and splits always
produce same-level siblings.
**Promoted back on cost, not on risk:** the clean build identifies this exact line as
`RTreeNode.cs(288,13): warning CS8602 — Dereference of a possibly null reference`, and CI
annotates the same line at `b264607`.
**Verified at `d4fecd1`: still `if (node.FeatureIndex[0] == ind)`, byte-identical, and the
CS8602 annotation for it is expected to still fire.** One line —
`if (node.FeatureIndex is { Length: > 0 } && node.FeatureIndex[0] == ind)` — closes P-51
**and** removes one of the six real warnings. `FindByIndOnAnEmptyTreeReturnsNothing` and
`FindByIndForAnAbsentIdReturnsNothing` already exercise the line on a childless tree and with
an absent id, so the guard can be verified rather than eyeballed. **This is the single
cheapest open item in the repository and it has now been adjacent to two warning-fixing PRs.**

### P-60 `PartType` lives in a namespace its consumers guess wrong  *(was N-9)*
`PartType` is in `Nsi.Geospatial.Enums` while its only consumer `Part` is in
`Nsi.Geospatial.Geometry`, so IDEs auto-suggest `Nsi.Geospatial.Geometry.Enums` — which
cost a real compile error during this branch. Decide the convention across `Enums/`.
**Same trap, one namespace over, caught in PR #11:** `CrsKind` is not reachable unqualified
from `Nsi.Geospatial.Tests`, which produced a `CS0103` in a working-tree edit
(`FeatureAttributeTests.cs(98,18)`). Two data points, one rule: **qualification failures in
this codebase cluster on the `Enums`/`Projections` types that sit one namespace away from
their consumers.** See N-15 entry 11.
**Third data point, from `d4fecd1`:** `Features.cs` writes `Projections.CrsInfo.Unknown`
fully qualified in two places (`Crs`'s default and `CrsInfo _crs = …`) inside a file that
already has `using Nsi.Geospatial.Projections;` — i.e. the qualification is redundant, which
is the mirror image of the same confusion. Whoever settles the `Enums/` convention should
delete these two prefixes in the same pass.

### P-63 Writer/reader local hygiene  *(new, PR #8 review; alias advice reversed)*
- `SpatialWriter`'s polygon branch states the degenerate-ring guard twice with different
  messages and identical conditions (`i == 0 && verts.Count < 3`,
  `i > 0 && verts.Count < 3`). One guard naming the role in the message. **Unchanged by
  PR #10's reindent and by PR #12's second pass through the same 60 lines.**
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
  **Still the exact line at `d4fecd1`,** and PR #12's rename pass had `RTreeNode` fully open.
  Sequence with P-15, which wants to restrict the same property's setter.
- `dotnet format` details that will bite, both from `.editorconfig`: `indent_size = 2` for
  `*.cs`, and `csharp_style_var_when_type_is_apparent = true:warning`. `dotnet format`
  defaults to `--severity-level warn`, so explicit types where the type is apparent get
  rewritten and `--verify-no-changes` fails on them. That is why production code uses `var`
  everywhere, and it is the likeliest CI trip in any new test file.
  **Now measured rather than predicted:** PR #11's ~35 new test members pass the CI format
  step, and PRs #10 and #12 are both passes over `SpatialWriter`. **But see section 9:** two
  consecutive formatting passes over one method, both accepted by `--verify-no-changes`,
  means the check is not pinning the file to one shape. The formatter's opinion is a floor,
  not a canonical form.
- README calls the core project `Nsi.Geospatial.Core` while the assembly is
  `Nsi.Geospatial` (P-30). The split test namespaces in P-36 are the fossil record of
  that abandoned rename; settle the name once.
- **New from `d4fecd1`, same family — parameter names that disagree with the interface they
  implement:** `IFeatureSink.Write(Features features, …)` vs
  `SpatialWriter.Write(Features collection, …)`, and `SpatialReader` still binds
  `var fc = new Features()`. Individually trivial; collectively they mean the rename's
  vocabulary is not settled, which is what makes P-70's residue searchable-again step worth
  doing before anything else touches these files.

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
**Partly addressed by PR #12 `d4fecd1`, and the partial fix is worse than no fix:**
`MaxChidrens`/`MinChidrens` → **`MaxChlidren`/`MinChlidren`**. The misspelling is corrected
*into a different misspelling*, and because the local in `buildChildOptions` was left as
`sortedChidrens` while `RTreeManager`'s constructor parameters remain `minChilds`/`maxChilds`,
the file now contains **three** distinct wrong spellings where it previously had two
(P-43's note). **If the intent was to fix the typo, the fix is `MaxChildren`/`MinChildren`**
— the properties already have `private set`, so the rename is internal-only except for
`RTreeManager`'s constructor parameter names, and it is a one-commit change with the compiler
as its guard. Do it before a third spelling appears in a new method.
**Remaining:**
- `addFeatureChild` is not merely unreachable, it is *misleading*: it contains an area
  tie-break (`extensionReq == minExtension && childnode.Area < (bestCandidate?.Area ?? …)`)
  that the live path `addFeatureChildEnforceIntersect` lacks, so a reader will assume the
  tie-break is active. **`04118f4`, PR #11 and `d4fecd1` have now each passed through it
  without deleting it**; at `d4fecd1` it is byte-identical, still calling
  `getAddedSizeToAccomodate`, still ending in `bestCandidate!.addFeatureChild(feature)` with
  a null-forgiving operator that asserts a fact the compiler was asking about
  (`RTreeNode.cs:146 CS8600`). P-41's substitution changes its (dead) semantics too — say so
  in the commit so nobody resurrects it expecting the old ranking.
  **Deleting it also removes `RTreeNode.cs:146 CS8600`** — one of the six real warnings,
  deleted rather than annotated. Prefer that route. Three warning-fixing passes have now
  chosen *neither* route (not deletion, not annotation) and left the warning firing.
- `cumulativeOverlap` / `siblingOverlap` are assigned in `buildChildOptions` and never
  read — the split sorts on the local `overlap`/`totalArea`/`perimeterTotal` triple. Dead
  state carried by every node, as `public double` properties on a public class. **Still both
  present and still both public at `d4fecd1`,** assigned in the method PR #12 edited.
- `getAddedSizeToAccomodate` still hand-rolls `bbox`'s area instead of calling
  `bbox.Area()` — the last duplicate of that expression, and P-41 deletes the whole body.
- `Features.Crs`'s setter reaches through `f.Parts` to call `Part.InvalidateMetrics()` —
  add `Feature.InvalidateMetrics()` and forward. **Still doing exactly that at `d4fecd1`**
  (two nested `foreach` loops inside the property setter), and the property's own docstring
  still says "this collection" after P-70's rename.
- New in `04118f4`: a **constant interpolated string with no interpolation hole** —
  `$"Feature has no extent and cannot be indexed."`. Drop the `$`. (Its sibling
  `$"Feature has non-finite extent {bbox}."` does interpolate and is fine.) No compiler
  warning for this in the clean build, so it is style-only, but it is free. **Confirmed
  style-only by CI:** the format step passes with the `$` in place.
- **Naming, PR #11:** `RTreeTests` carries both `zerozero`/`onezero` and `Id0`/`Id7`/`Id99`
  for the same kind of thing. Same family as the constructor-parameter mismatch above; pick
  one convention when the block is consolidated (P-36).
- **Naming, PR #12:** `Features.FeatureSet` is a name chosen to dodge a compiler rule
  (P-70's point 2), not to describe anything. If P-17 replaces the raw `List` with a façade,
  the property can become something readable in the same change; don't leave `FeatureSet` as
  a permanent artifact.

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

### Closed by PR #8 (`051e8d8`, `4a0ec8b`, `7b4c6aa`, `9bee4e4`, `389dc4a`) — net −483 lines, plus the read-path fix
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
| `RTreeNode.getMBRoverlap` deleted (−23) | P-01 (**closed**) |
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
| `addFeature` throws on `BoundingBox.Empty` | **P-66 mitigated** (→ closed by `d3464e6`), D-F, P-11/P-21 (new throw path) |
| `addFeature` throws on any non-finite corner | P-66; **P-39 gains the `±DBL_MAX` gap** (since corrected to `1e308`) |
| `BoundingBox.Perimeter()` added | P-67 (closed for duplication) |
| `RTreeNode.getArea`/`getPerimeter` → `Area`/`Perimeter` properties delegating to `BoundingBox` | **P-67 closed**, P-22; breaking rename |
| `Issues.md` updated to `9c7124a` | this file |

**Commit-message accuracy, since the file's closure rule depends on it:** the subject
claims `fixing p39`. P-39 is **not** fixed — the sentinel is unchanged, `Area`,
`Perimeter`, `ContainsPoint` and `EnlargementToContain` are still unguarded. What landed is
the *containment* of P-39's worst consequence. Prefer `fix(P-66) (mitigation)` or `refs P-39`;
a bare `fix(P-xx)` is a claim this file will check. **PR #11 followed this; PR #12 went the
other way and named nothing at all (see P-70).**
Also: "gaurds" is a typo, in a branch that has an open item about typos (P-36).

**Build and suite state at `04118f4`: GREEN. Established, not assumed.**
`total: 112, failed: 0, succeeded: 110, skipped: 2`; build succeeded with 16 warnings.
1. ~~a test now hits the `throw`~~ — **no**, at that SHA. **Since corrected: PR #11 supplies
   those tests.**
2. ~~a stale `getArea`/`getPerimeter` reference~~ — **no.**
3. ~~formatting of the new guard blocks~~ — **evaluated later:** CI's format step passed from
   `fe4e6a5` and again at `b264607`.

**What green did *not* establish:** that anything added since `4a0ec8b` was guarded. P-01,
P-66, P-68 and P-48 all remained open *because* of what the green run showed. P-01 and P-66
have since been closed by PR #11; P-68 and P-48 have not.

### PR #10 `fe4e6a5` (merged from `formatting`, `d207943`) — 1 file, 55/55, no behaviour change
| Change | Recorded under |
|---|---|
| `SpatialWriter.BuildOgrGeometry` reindented — `case` bodies and braces, `Point`/`PointM`/`Line`/`Polygon` | nothing; formatting only |
| Confirms `dotnet format`'s opinion on that region is now committed, so the CI format step is green from `fe4e6a5` forward | section 9 |
| **`P-57` and `P-63` survived it verbatim** | P-57, P-63 |

### PR #11 `d3464e6`, `5f6f2c3`, `f86c689`, `518630c`, `b264607` — **MERGED at `26d2840`** — tests + README
| Change | Recorded under |
|---|---|
| T-21: `OverlapsIsSymmetricAndTrueForContainment` (17 rows × both directions), `OverlapsIsFalseWhenEitherSideIsEmpty` | **P-01 CLOSED** |
| T-18: `FeatureWithNoExtentIsRejected`, `FeatureWithNonFiniteExtentIsRejected` (5 rows), `PointShapedFeaturesSurviveSplitsAndAreFound` + negative control | **P-66 CLOSED**, D-F, P-48.5 |
| T-22: `FreshRootHasInfiniteAreaSoTheComparisonNeverFires` + `FirstInsertIntoEmptyTreeLandsOnRoot` | P-48.6 closed, P-39 |
| `BoundingBox` characterisation: `EnlargementToContainIsMbrGrowth` (5 rows), `…IsAsymmetricAboutEmpty`, `UnionTreatsEmptyAsTheIdentityElement`, `UnionIsCommutativeAndNeverSmallerThanEitherSide`, `PerimeterIsTwiceTheSumOfTheExtents`, `PerimeterOfEmptyOverflows`, `FromVerticesOfNothingIsTheSentinel`, `FromVerticesSilentlyDropsNonFiniteVertices`, `NegatedMaxValueIsTheSentinelButNearMaxValueOverflowsUnnoticed` | P-41 (member guarded), P-62, **P-69 filed**, P-67, P-39 |
| `FeatureAttributeTests.cs` new — coercion/type-lies, `Integer`→`Long` widening, missing+null defaults, case-insensitive lookup, **invariant parsing under `de-DE`**, empty/unreferenced feature area + box | P-06's rationale made executable, P-19, P-28, P-21, P-39 |
| `RTreeTests` docstring rewritten (all three false clauses removed) | **P-48.7 closed**, N-3 |
| `Id0`/`Id7`/`Id99` as `private static readonly int[]` with a sharing-safety rationale | removes 4× `CA1861`; P-36 style split |
| README `TreatWarningsAsErrors` → `=false` | **P-30: 4 errors → 3** |
| `FindByIndReturnsAPathToTheFeature`, `FindByIndOnAnEmptyTreeReturnsNothing`, `FindByIndForAnAbsentIdReturnsNothing`, `NodeAreaAndPerimeterAreTheBoundingBoxes` | P-51 (line reached), P-48.4 partly, **P-36 duplicate** |
| This file, +707/−251 | now on `main` |

**Commit-message accuracy:** `d3464e6` claims `test(P-66, P-01, P-39)`. P-66 and P-01 are
**correctly** claimed — the assertions exist and pass. P-39 is **correctly not** claimed as
fixed. `5f6f2c3` ("adding in static arrays to get rid of warnings") and `b264607` ("updating
comment") name no item; both are recorded above against the items they touch, and `b264607`
is what closed P-48.7.

**Build and suite state at `b264607`: CI GREEN — established, not assumed.** Check run
`build` concluded `success` in 58s, covering the format step and both test steps. The
check-run API returns **11 annotations and no test totals**, so no total is recorded; the
local figure that preceded this (139) was a working-tree number and is withdrawn (N-11).

### PR #12 `d4fecd1` ("removing warnings") — 13 files, +134/−145, **breaking public API**
| Change | Recorded under |
|---|---|
| `FeatureCollection` → **`Features`** (file rename, 68 % similarity); `.Features` property → **`.FeatureSet`**; `IFeatureSource.Read` / `IFeatureSink.Write` / `SpatialReader.Read` / `SpatialWriter.Write` / `Feature.Owner` signatures change | **P-70 filed**, D-G, P-14/P-61/P-67 (breaking-changes list) |
| `MaxChidrens`/`MinChidrens` → `MaxChlidren`/`MinChlidren` (still misspelled; local `sortedChidrens` and ctor params `minChilds`/`maxChilds` untouched) | **P-22, P-43 — third spelling now live** |
| `int[]? featInd = null` in the `RTreeNode` ctor | **`RTreeNode.cs:19` CS8625 fixed** — 1 of 6 real warnings |
| `MaxChlidren`/`MinChlidren` `= 0` initialisers deleted | **`RTreeNode.cs:11,12` CA1805 ×2 fixed** |
| `Children.Count() - MinChlidren` → `Children.Count - MinChlidren` | **`RTreeNode.cs:97` CA1829 fixed — the hot-path win** |
| `FeatureCollection` type name gone | **CA1711 silenced by rename**, contradicting section 9 → P-70, D-G |
| `RTreeNode.cs:73` `sortedChidrens = null` | **NOT fixed** — method edited around it |
| `RTreeNode.cs:146` `RTreeNode bestCandidate = null` in dead `addFeatureChild` | **NOT fixed** — method still present, still has `bestCandidate!` |
| `RTreeNode.cs:177` `RTreeNode bestCandidate = null` in the live path | **NOT fixed** |
| `RTreeNode.cs:288` `node.FeatureIndex[0]` | **NOT fixed** — **P-51 still open**, one line |
| `AttributeTable.cs:57` CA1854 `ContainsKey` + indexer | **NOT fixed** — file not in the diff |
| `SpatialWriter.BuildOgrGeometry` ~114 lines churned again | **whitespace-only on the content readable to me**; P-57's `Coordinates` compare and P-63's doubled guard unchanged → section 9 |
| `Feature.Owner` docstring still names `FeatureCollection`; `Features.Crs` summary says "this collection"; `var fc = new Features()`; `Write(Features collection)` vs interface `features` | **P-70 point 4**, N-3 new sweep clause |
| `SpatialJoins.cs` 18 lines (rename) | P-02 — **the `_ = tree ?? BuildTree(...)` discard not re-verified** |
| Five test files, ~88 lines (rename) | P-36 — first measurable cost of not hoisting helpers |

**Commit-message accuracy, third test of the rule:** "removing warnings" describes 4 of the
11 annotations and omits the largest API change in the repository's history. The failure mode
is the inverse of `04118f4`'s: that message over-claimed a `fix`; this one under-describes a
break. **Both are caught by the same rule — the message is not the evidence, so read the diff.**

**Build state at `d4fecd1`: NOT ESTABLISHED.** Its check run was **in progress** at review
time (`status: in_progress`, `conclusion: null`, 0 annotations). No count, no verdict, and no
substitute inferred from the source. Section 9 carries the reading-based prediction, labelled
as one, to be replaced when the run lands.

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
| T-8 | `MetricsDoNotDependOnWhetherOrWhenSealWasCalled` — reading `LengthMeters` must not change what `AreaSquareMeters` reports | P-54 invariant |
| T-9 | `AddingAVertexAfterMeasuringReMeasures` — read length, `AddVertex`, read again | cache invalidation |
| T-10 | `ReplacingTheCollectionsCrsInvalidatesPartMetrics` | cascade + identity key |
| T-11 | `DetachedFeatureRemainsMeasurable` — `RemoveFeature` then change `fc.Crs`; the removed feature must still recompute | P-18 |
| T-12 | ~~`EmptyPartDoesNotInflateFeatureBoundingBox`~~ **retitled — the described defect does not exist.** `Feature.AddPart` goes through `Union`, whose `== Empty` guards already make an empty part a no-op on a real box (P-39's retraction, pinned by `UnionTreatsEmptyAsTheIdentityElement`). What should be pinned instead: **`FeatureWithNoPartsHasNoUsableBox`** — **half done by PR #11:** `AreaOfAnEmptyOrUnreferencedFeatureIsUnknown` asserts `Assert.Empty(f.Parts)` and `f.BoundingBox == BoundingBox.Empty`, and `FeatureWithNoExtentIsRejected` asserts the refusal. **Remaining: wire the two together** — feed a zero-part *feature*'s box to `addFeature` in one test, so the feature-level premise and the index-level consequence are linked rather than assumed | P-39, P-11 |
| T-13 | Degenerate ring `[A,B,A]` — assert whichever of `0.0` / `null` is chosen, in both CRS kinds | P-07 |
| T-14 | **`ReprojectToUsesTraditionalGisOrder`** — read a lon/lat fixture into a projected CRS and assert X is still longitude. Also closes N-2. The only path that pins axis order now, since the P/Invoke path that ignored it was deleted in PR #8 | P-14 |
| T-15 | ~~`CrsTokenDoesNotDoublePrefixANonEpsgAuthority`~~ **invalid as written — it passes today**, because the `StartsWith("EPSG:")` guard already exists (D-D). Rewrite once the model is chosen: `Projection("ESRI:102003")` resolves to the same SRS GDAL reports for `ESRI:102003` **without emitting a warning**, and `CrsInfo` round-trips the authority rather than relabelling it `EpsgCode` | D-D, P-14 |
| T-16 | ~~`ReadsAnInteger64AuthoredElsewhere`~~ **done — PR #8 `9bee4e4`, verified passing at `04118f4` and in CI at `b264607`.** Hand-written GeoJSON (`"BIG":4000000000, "SMALL":7`), asserts `BIG` boxed `long` / `Schema["BIG"].FieldType == LongFT` and `SMALL` still boxed `int`. Non-circular by construction. Keep the `SMALL` assertion through P-65's consolidation | P-06 |
| T-17 | **`LongFTWritesAsInteger64`** — write `4_000_000_000` to a `LongFT` column with our writer, then reopen and assert the **declared** OGR type is `OFTInteger64` (not `OFTString`/`OFTReal`) *and* the boxed CLR type is `long`. Currently **no test touches** `SpatialWriter.MapFieldType`'s `LongFT` arm or `SetOgrField`'s `case long l:`. **PR #12 modified `SpatialWriter.Write`'s signature and its feature loop twice without adding coverage, so this debt is now two commits deeper than when it was filed** | **P-68 — the write arms of `9bee4e4` are unguarded** |
| T-18 | ~~**`IndexRejectsBoxesItCannotPlace`**~~ **DONE — PR #11 `d3464e6`, three members, CI-verified green at `b264607`, on `main` since `26d2840`.** Delivered as `FeatureWithNoExtentIsRejected` (asserts the message *and* `Assert.Empty(tree.Root.Children)`, i.e. rejection precedes mutation), `FeatureWithNonFiniteExtentIsRejected` (5 rows: NaN×2, +∞, −∞, both infinities), `PointShapedFeaturesSurviveSplitsAndAreFound` (**30** points, plus a 31st after the splits, and a negative control at `(500,500)`) | **P-66 CLOSED**, P-39, D-F, P-48.5 closed |
| T-19 | **`RejectsALongTheDriverCannotStore`** — `Assert.Throws` on writing `1_000_000_000_000_000_007` per driver, plus the `9_000_000_000_000_000` maximum-safe boundary. Also puts a **repro in the tree** for P-64's measurement, which currently has none | P-64 |
| T-20 | `InsertionChoosesSmallestEnlargement` — a small feature must land in the smaller-enlarging node. Fails on today's `Area + featArea − overlap`, which reports set-union area (worked numbers in P-41) | P-41, P-48.4 |
| T-21 | ~~**`OverlapsIsSymmetricAndTrueForContainment`**~~ **DONE — PR #11 `d3464e6`; closes P-01.** 17 rows with symmetry asserted both directions per row, plus `OverlapsIsFalseWhenEitherSideIsEmpty`. Adds beyond spec: identical boxes, flush *corner*, point-vs-point equal/unequal, three zero-width-line cases, and a `-1e308` box asserting that size alone never short-circuits | **P-01 CLOSED** |
| T-22 | ~~`FirstInsertIntoEmptyTreeLandsOnRoot`~~ **DONE — PR #11 `d3464e6`, as two members:** `FirstInsertIntoEmptyTreeLandsOnRoot` (asserts `Single(Root.Children)`, the child's box, **`Root.BoundingBox` propagated over `Empty`**, `Assert.Same(Root, child.Parent)`, and `Findable`) and `FreshRootHasInfiniteAreaSoTheComparisonNeverFires`, which pins the *premise* — `Root.Area` is `+inf`, `getAddedSizeToAccomodate` is `+inf`, `inf < MaxValue` is false. Its comment flags that P-39 will require the comment (not the assertions) to change | P-39, P-48.6 closed |
| T-23 | `OracleDoesNotCallThePredicateUnderTest` — not a new test so much as a rule for P-48.2: `FeatureIndicesAt` must decide membership with an interval test written in the test file, not `BoundingBox.Overlaps`. Then a scale test comparing `findByXY` against it over a few hundred boxes. **Half-built already:** PR #11's `Findable` is exactly such a helper (independent of `Overlaps`, with a negative control), so this is now "convert `FeatureIndicesAt`'s two callers to `Findable` and delete the self-referential version", not "invent an oracle". Highest-value remaining test item in this table, because `BulkInsertAllFeaturesFindableByPoint` is the repository's strongest end-to-end assertion and currently cannot fail for the reason it exists | P-48.2, P-01 |
| T-24 | `FromVerticesRejectsOrFlagsNonFiniteVertices` — convert `FromVerticesSilentlyDropsNonFiniteVertices` from characterisation to specification once P-69 decides: either `Assert.Throws` naming the vertex, or an `Empty` result that `addFeature` then refuses. The current test pins the bug, so it **must be changed in the same commit as the fix**, not deleted before it | **P-69, P-62** |
| T-25 | **`FeaturesIsOnlyMutableThroughItsApi`** — assert that `AddFeature` assigns `Id` and `Owner`, and that `RemoveFeature` renumbers *and* clears `Owner` on the detached feature (the latter currently fails — that is the P-18 pin this file has been asking for). Write it against `FeatureSet` only through `AddFeature`/`RemoveFeature`, so P-17's façade swap makes the test *easier*, not impossible | **P-18, P-17, P-48.9** |

---

## 8. Notes

- **N-1** ~~Five `BoundingBox` primitives are never called~~ **reduced to four uncalled
  members.** Called in production: `Overlaps` (three `RTreeNode` descents, the
  `RTreeTests` oracle), `OverlappingArea` (`getAddedSizeToAccomodate`), `Perimeter` (added
  `04118f4`, via `RTreeNode.Perimeter`). Uncalled: `Contains` (→ P-46),
  `EnlargementToContain` (→ P-41), `ContainsPoint` (homeless, and the last unguarded
  predicate), plus `FromVertices` and `Point`, which were never counted. Split decision:
  P-62.
  **Status after PR #11/#12 — "uncalled" and "untested" are now different lists.** Still uncalled
  from production: `Contains`, `EnlargementToContain`, `ContainsPoint`, `FromVertices`.
  But `EnlargementToContain` and `FromVertices` now have direct tests, `Point` is used by
  tests, and **`ContainsPoint` is the only member of the type with no test at all** — which
  makes it the one whose keep-or-delete decision is least informed. `BoundingBox` was not
  touched by `d4fecd1`, so this list is current as of `d4fecd1`. Update when P-62 resolves.
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
  **Add two more to sweep:** `grep -rn "P0-4\|intentionally left unchanged"` (N-6 — **re-confirmed
  present at `d4fecd1`**); and `grep -rn "through the writer and reader"` — `FieldTypeTests`'
  class docstring describes a writer test that is not in the file (P-68).
  **Add a third:** `grep -rn "getArea\|getPerimeter\|getMBRoverlap"`. **Verified clean at
  `04118f4`** for code and **at `b264607`** for comments — `RTreeTests`' class docstring was
  the last surviving mention and `b264607` rewrote it. Remaining surface: the changelogs,
  which are N-5-unreadable.
  **Fourth sweep clause, added by PR #11:** `grep -rn "restored verbatim\|do NOT assert fixed
  behavior"` — those two phrases were the docstring's load-bearing claims and are the shape
  a stale test-suite docstring takes.
  **Fifth sweep clause, added by PR #12, and this one is urgent:** `grep -rn "FeatureCollection"`.
  The type is gone and at least these survive pointing at it — `Feature.Owner`'s docstring
  (*"Set by FeatureCollection.AddFeature"*), `Features.Crs`'s summary ("this collection"),
  `SpatialReader`'s `var fc`, and `SpatialWriter.Write`'s `collection` parameter against an
  interface that says `features`. **After a type rename, the grep for the old name is the
  cleanup, and it is the step that a rename PR structurally cannot notice it needs.** Add to
  the list of things to run *because* you renamed something, with the same status as a
  rebuild: `grep` for the old identifier in comments and in string literals.
- **N-5** `CHANGES_09012026.md` below P3-5 and parts of `CHANGES_09082026.md`'s narrative
  have never been readable through any fetch path. Do not assume they are empty.
- **N-6** Keep `CHANGES_08312026.md`'s `fix(#N)` legend: those markers live in
  `BoundingBox.Overlaps`, `AttributeColumn.Coerce`, `CsvHelper`, `Part.AddVertex` and
  `Feature`, and they index *that* file, not this one. `CHANGES_08312026.md` also claims
  the R-tree files are "restored verbatim from master, all original typos included" —
  false since `16585ef` and `ac3bb82`, and now quadruply so: PR #9 deleted a method and
  renamed two more, PR #11 rewrote the test docstring that repeated the claim, and PR #12
  renamed `Min`/`Max` properties inside the same files.
  **The test-side copy of this lie is gone (`b264607`); the changelog's copy is not, and is
  N-5-unreadable.**
  **Live collision, worth fixing on sight:** `SpatialWriter.SetOgrField`'s comment carries
  `P0-4`, which reads as `P-04` in this file (`EarthRadiusFeet`) but means
  `CHANGES_08312026.md` item 4 (field typing). Two unrelated defects, one glyph, in the
  one method whose behaviour this branch just changed. **Still verbatim at `d4fecd1`, now
  three PRs after being flagged.**
- **N-7** Timeline rule: `CHANGES_09012026.md`'s P0-1 blamed the containment gate for
  missing features; `16585ef` fixed the actual cause (MBR propagation) four and a half
  hours later and the changelog was never reconciled. Any row citing 0901 P0-1 must be
  re-read against P-49. **Now settleable:** the gate was real, was dormant, and is the
  thing `9c7124a` deleted. **P-01 is closed, which makes this a historical citation: cite it
  as "resolved, see P-01" and nothing more.**
- **N-8** Benchmark build and query separately. The RBush comparison may have measured
  `Load()` (bulk STR-style) rather than incremental `addFeature`, which would explain
  part of the gap and is exactly what P-40 will change.
- **N-10** `Part`'s metric cache depends on the invariant that geometry cannot change
  behind its back. That holds for `Part` (`Vertices` is read-only) but **not** for
  `Feature.Parts` (P-17) or `Features.FeatureSet` (also a public `List`). Fixing
  P-17 should close the last hole.
  **PR #12 made this marginally worse and did not intend to:** `Features` now exposes `Count`
  and `this[int]` forwarding to `FeatureSet`, so the type offers a read-only *appearance* at
  the same time as a mutable *escape hatch*. A caller that reads `foreach (var f in features)`
  and separately does `features.FeatureSet.Clear()` gets a silently stale `Crs` invalidation
  trail and stale `Id`s. **The façade members are the right shape; adding them without closing
  the list is what turns "public list" into "public list with a convincing alternative".**
  Do P-17 as one change covering both `Parts` and `FeatureSet`, and it closes N-10 outright.
- **N-11** **Attribution. Three failures of this in a row, now corrected and the mechanism
  recorded.**
  - `total: 107, failed: 5` was quoted as a property of `9bee4e4`. It was not: the committed
    `FieldTypeTests.cs` at that SHA contains one test and nothing that fails. The five
    failures were a working-tree state.
  - `failed: 2` was quoted as a property of `9c7124a`. It was not: the committed
    `GeometryTests.cs` contains the three corrected theories, and the failing
    `…ConsistentWithEnlargement` theory existed only in the working tree.
  - After PR #11's first push the local run reported a build failure (`CS0103` on `CrsKind`)
    alongside 17 warnings. That was a working-tree state at the moment of the edit, not a
    property of any commit — and `b264607`'s CI run is green with zero test-assembly
    annotations, so it was fixed before it was ever committable.
  Common cause: a local run was read as a claim about a commit. **Rule: `git show
  <sha>:<path>` before recording any pass or fail against a SHA.** Its corollary: **when the
  local tree is red, say "the tree is red" and fix it, rather than recording a count that no
  SHA owns.**

  **Verified state at `04118f4` (clean build, both test assemblies):**
  `total: 112, failed: 0, succeeded: 110, skipped: 2`, duration 18.6s; build succeeded
  with 16 warnings. Skip budget exactly two (D-B):
  `EarthRadiusFeetIsTheAuthalicRadiusInFeet` (P-04) and
  `PointToSegmentWhenFootIsBehindTheNearEndpointReturnsDistanceToA` (P-03). (`RTreeTests.BulkInsertAllFeaturesFindableByPoint` carries a
  commented-out `Skip` from `16585ef`; leave it as history, do not re-enable.)
  `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is…` still prints once
  per run (D-D) — a passing test that pollutes output.

  **Verified state at `b264607` (CI, `-c Release`):** check run `build` **success**, 19:16:08 →
  19:17:06 Z, steps Restore → Build → **Format check** → Test (non-Gdal) → Test (Io.Tests).
  11 warning annotations (section 9), no test totals exposed. **Establishes:** the format step
  passes, all five commits' test code compiles and passes, and no test assembly emits a
  warning. **Does not establish:** any count, the skip budget, or the effect of `-c Release`
  on the GDAL warnings.

  **State at `d4fecd1`: deliberately blank.** The check run was `in_progress` with
  `conclusion: null` and `annotations_count: 0` at review time. The temptation in this exact
  situation is to write "should be green — four of the annotations are fixed in the diff",
  and that is N-11 in a new costume: **an inference from source is a prediction about a build,
  not a record of one.** Section 9 therefore labels its numbers as predictions and this entry
  records nothing. Replace both when the run concludes.
- **N-12** Dangling `P-` references: **P-20** (cited by D-C), **P-54** (cited by P-13,
  T-8, and `Part`'s `_measuredCrs` comment), **P-55** (cited by P-22, and its subject
  `Feature._crs` no longer exists), **P-59** (cited twice by section 9) have no entry in
  this file. Either they were closed by deletion without the citations being cleaned, or
  they live in the unreadable changelogs (N-5). Do not reuse these numbers; restore the
  definitions or mark the citations dead. D-C's closure removes one citation to P-20 but
  P-20 itself is still undefined.
  **Counting rule, since the file now runs to P-70:** the sequence has holes (P-52, P-45,
  P-49 closed-as-merged, P-57/P-58 territory) and four undefined numbers. Before citing a
  number you have not written, search for it. P-69 and P-70 were both filed against things I
  read at the SHA rather than inferred, which is the only difference between a new item and a
  new mistake.
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
  **Fourth lesson, and it is about the review, not the code: the entry that documents this
  pattern did not prevent the reviewer from committing a duplicate.** PR #11's
  `FindByIndReturnsAPathToTheFeature` duplicated an existing `findByInd` test (P-36). The
  pattern is documented in three places in this file and was still not applied, because
  "grep for the predicate" was written as advice about *production* members. Generalise it:
  **before adding a test, grep for the member name in the test assemblies.**
  **Fifth, and the constructive version of the third:** PR #11's `Findable` shows the right
  shape for an oracle — written independently of the predicate under test, *and* with a
  negative control. A brute-force oracle without a negative control is the same trap as a
  self-referential one, arrived at from the other side. Both halves are required; T-23 should
  demand both.
  **Sixth, from PR #12, and it is about *names* rather than code: two spellings of one
  concept is how a rename ends up half-done.** `MaxChlidren` + `sortedChidrens` +
  `minChilds` now coexist, and `Features` + `FeatureCollection`(in comments) coexist as well.
  The mechanism is the same in both cases: the rename was applied where the compiler demanded
  it and not where it didn't. **When you rename, the compiler is the floor and grep is the
  job.**
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
  **Third payoff, from PR #11, and it is the generalisable one: a *test suite* is the same
  kind of instrument as a build log.** Six of this file's numeric claims about `BoundingBox`
  were settled by writing the assertion and running it, in one pass, at zero risk. Where a
  question is "what does this member actually do", the cheapest instrument in the repository
  is a characterisation test.
  **Fourth payoff, from PR #12, and it points the other way: a build log also tells you what
  a *warning sweep did not do*.** The four surviving `CS860x` annotations are the evidence
  that the four hard fixes weren't attempted, which is information a passing build does not
  give you by itself — `TreatWarningsAsErrors=false` means "warnings decreased" and "warnings
  unchanged" look identical in a green check. **Until the flag is on, the only way to tell a
  warning fix from a warning pass-through is to count the annotations, so count them, every
  PR, and put the number in the commit message** ("4 of 10 warnings" would have made PR #12's
  scope visible without any prose at all).
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
  **Entries 7–11, all in the PR #9/#11 cycle, and 9–11 are mine in code rather than in prose:**
  - `Perimeter(Empty)` was recorded as ~`7.2e308`. It is `+infinity` — the subtraction
    overflows before the doubling. (7)
  - "`±DBL_MAX` clears both `addFeature` checks and is `!= Empty`." `-double.MaxValue` **is**
    `double.MinValue`, so it normalises to `Empty` and the first gate catches it. The real
    gap is `1e308`. The claim was five characters from correct and entirely wrong. (8)
  - "`FromVertices`' empty path returns a normalised garbage box." It returns `Empty`. The
    *actual* defect in that member — silent dropping of NaN vertices, **P-69** — was missed
    entirely while the imaginary one was described twice. (9)
  - Four `CA1861` warnings shipped in test code I wrote, then needed a dedicated commit
    (`5f6f2c3`) to remove. `AnalysisLevel=latest-recommended` applies to test assemblies,
    which P-30 had not accounted for. (10)
  - A `FeatureAttributeTests.cs` delivered with `CrsKind` unqualified against a namespace
    that was only described in a comment — a build failure (`CS0103`) that "complete files,
    not fragments" already exists to prevent. (11)
  **What 9–11 share, and it is not the same shape as 1–5:** the first five were relationships
  asserted before the arithmetic was done. These three were *deliverables* shipped without
  the check that was available for free — a build, a grep of the test assemblies, a warning
  list. The rule "show the four numbers, then run the build" was half-applied: the numbers
  got shown, and the build did not get run before the file was handed over. Extending it:
  **a file you cannot compile is a fragment, however many lines it has.**
  **Entry 12, and it is about a recommendation, not an observation.** Section 9's
  `CA1711` row said *"Suppress with a rationale in `.editorconfig`; do not rename."* The
  rename happened anyway, and while P-70 argues the *decision* was defensible, **my
  prescription was incomplete in a way that only shows up after the fact: I priced the type
  rename and not the property rename.** A member cannot share its enclosing type's name, so
  `FeatureCollection.Features` → `Features.FeatureSet` was forced, and the second break is
  the one that touches every call site. **Rule extension: when recommending against a rename
  on breaking-change grounds, enumerate every break the rename forces, including the ones the
  compiler forces** — a "just suppress it" that hasn't costed the alternative is a preference
  wearing a prescription's clothes. (12)
  **One more pattern visible across all twelve:** nine of the first eleven are claims about
  **what a specific member does** — its return value, its guards, its empty-input path. Zero
  are claims about architecture, structure, or sequencing; the designs this file proposed and
  `04118f4`/PR #11 implemented all held. **So the failure mode is specific and cheap to fix:
  never describe a member's behaviour without either reading its body or asserting it.
  Judging the shape of the system has been reliable; reciting the details has not.** Entry 12
  extends the same lesson to *recommendations*: describe the whole blast radius or don't
  prescribe.
  **Counterweight, so this file does not become an excuse for timidity:** the two designs
  this file proposed and `04118f4` implemented as written — the two-check gate in
  `addFeature`, and collapsing `getArea`/`getPerimeter` into `BoundingBox` — hold up
  against the same arithmetic discipline, and the invariant they buy checks out, and the
  branch is green with them in. **PR #11 is the strongest form of the counterweight: the
  invariant was written as prose here, implemented as code there, and is now asserted from
  both ends by tests that pass.** PR #12 is the weaker form and worth stating for fairness:
  the four warnings it *did* fix are all correct, the `CA1829` hoist is the real hot-path win
  this file asked for, and the `Features` name is defensible on its merits. The lesson is not
  "propose less"; it is "show the four numbers, then run the build", and if the change is a
  rename, "then grep for the old name".

---

## 9. Build hygiene  *(read at `d4fecd1`; verified inventories at `b264607` and `04118f4`)*

**Three inventories, two of them verified.** State which one a claim comes from. **PR #12's CI
run had not finished, so its numbers below are a source-reading prediction and are marked as
one.**

**Verified — CI at `b264607` (`-c Release`) — 11 annotations, complete:**

| Path | Line | Code | Message | Status at `d4fecd1` (read, not built) |
|---|---|---|---|---|
| `.github` | 6 | — | Node.js 20 deprecation: `actions/cache@v4`, `actions/checkout@v4`, `actions/setup-dotnet@v4`, `mamba-org/setup-micromamba@v2` | **still fires** — `.github/` not in PR #12 (P-31) |
| `RTreeNode.cs` | 97 | CA1829 | Use `Count` instead of `Enumerable.Count()` | **FIXED** — `Children.Count` |
| `AttributeTable.cs` | 57 | CA1854 | Prefer `TryGetValue` over indexer guarded by `ContainsKey` | **still fires** — file not in PR #12 |
| `RTreeNode.cs` | 12 | CA1805 | `MinChidrens` explicitly initialized to default | **FIXED** — initialiser deleted (property renamed to `MinChlidren`) |
| `RTreeNode.cs` | 11 | CA1805 | `MaxChidrens` explicitly initialized to default | **FIXED** — same |
| `FeatureCollection.cs` | 8 | CA1711 | Rename type so it does not end in `Collection` | **gone — renamed instead of suppressed** (P-70, D-G) |
| `RTreeNode.cs` | 288 | CS8602 | Dereference of a possibly null reference | **still fires** — `node.FeatureIndex[0]` byte-identical (**P-51**) |
| `RTreeNode.cs` | 177 | CS8600 | Converting null literal or possible null to non-nullable | **still fires** — live path, unchanged |
| `RTreeNode.cs` | 146 | CS8600 | Same, in `addFeatureChild` | **still fires** — method not deleted (**P-22**) |
| `RTreeNode.cs` | 73 | CS8600 | Same, `sortedChidrens` | **still fires** — method edited around it (**P-42**) |
| `RTreeNode.cs` | 19 | CS8625 | Cannot convert null literal to non-nullable reference type | **FIXED** — `int[]? featInd = null` |

**Predicted for `d4fecd1`: 6 annotations — 5 code + the Node.js one.** Not a record; replace
this line when the run concludes.

**The shape of that column is the finding, and it is not a quibble: PR #12 fixed 3 of the 4
style warnings and 1 of the 6 real ones.** Every warning it removed was fixable without
understanding the code — delete a `= 0`, change `Count()` to `Count`, add a `?` to a
parameter. **Every warning it left requires deciding something:** which of `sortedChidrens`'s
four assignments is live (P-42), whether to delete or annotate `addFeatureChild` (P-22),
whether `bestCandidate`'s nullability is a fact or a hope (P-39's fallback), whether
`FeatureIndex[0]` needs a guard (P-51), and whether `ContainsKey`+indexer should be
`TryGetValue` (mechanical, but the file wasn't in the diff). **A sweep that fixes what is
easy and passes through what is not is worse than no sweep, because it retires the item's
apparent priority while leaving the defect.** The message "removing warnings" plus a green
check is exactly how that reads from the outside, which is why the annotation count belongs in
the commit message (N-14's fourth payoff).

**Verified — local at `04118f4` (`-c Debug`) — 16 warnings:** the ten above, plus 3× `CS8600` at
`Nsi.Geospatial.Io/obj/Debug/net8.0/NuGet/98F74982D37CCAAA/GDAL/3.11.3/GdalConfiguration.cs(60,41/58/77)`
and the identical three under `Nsi.Geospatial.Reprojection/…GDAL/3.11.3/GdalConfiguration.cs`.
Package-generated, one set per project referencing the GDAL package — the doubling is P-61's
evidence, and `TreatWarningsAsErrors=false` is what keeps them from failing the build (P-30).
**Zero annotations from either generated file in CI at `b264607`**, which cannot distinguish
"not emitted in Release" from "not annotatable from `obj/`" — so **do not conclude the scoped
`NoWarn` is unnecessary in CI**; the flag flip needs it in both places until someone reads CI's
raw build log.

**Remaining path to `TreatWarningsAsErrors=true` — five items, unchanged in substance, with
three now retired:**

| Location | Code | What it is | Fix |
|---|---|---|---|
| `RTreeNode.cs:73` | CS8600 | `List<RTreeNode> sortedChidrens = null;` assigned in one of four `if` branches | Build the key selector, then one `OrderBy` — a 4-arm switch, initialise in the declaration. Deletes the `null` instead of annotating it, and deletes the third misspelling while there (P-42, P-22). |
| `RTreeNode.cs:146` | CS8600 | `RTreeNode bestCandidate = null;` in **`addFeatureChild`** | **Delete the method** (P-22 — unreachable; its `bestCandidate!` is the null-forgiving operator asking the compiler to stop asking). |
| `RTreeNode.cs:177` | CS8600 | `RTreeNode bestCandidate = null;` in the *live* path | `RTreeNode? bestCandidate = null;` — free, since the method already ends `bestCandidate ??= TreeManager.Root`, which the flow analysis will then accept. **Do not annotate the dead one the same way; deleting it is the fix.** |
| `RTreeNode.cs:288` | CS8602 | `if (node.FeatureIndex[0] == ind)` | `if (node.FeatureIndex is { Length: > 0 } …)`. **This is P-51**, already exercised by `FindByIndOnAnEmptyTreeReturnsNothing`. |
| `AttributeTable.cs:57` | CA1854 | indexer guarded by `ContainsKey` | `TryGetValue`. Mechanical. |

**Done by PR #12, so no longer on this list:** `:19` CS8625 (`int[]? featInd = null`), `:11`+`:12`
CA1805 (both `= 0` initialisers deleted), `:97` CA1829 (**the hot-path win this file has asked
for since it first listed the line — `Enumerable.Count()` was allocating an enumerator per
iteration of the split loop, on every split, during every insert**), and CA1711 by rename.

Then, still outstanding: `CA1711` needs **either** the `.editorconfig` rationale this file
recommended **or** an accepted break recorded under P-70 — currently it has neither, so the
annotation is gone but the decision is undocumented; scoped `NoWarn` in **both** csprojs
(P-30); the Node.js action bumps (P-31); and **a pass over the test assemblies**, which are
analysed too and which PR #11 showed can regress — `CA1050` and 4× `CA1861` appeared and were
removed within five commits, and `b264607` shows zero test-assembly annotations.

**`dotnet format --verify-no-changes` — VERIFIED GREEN at `fe4e6a5` and `b264607`.** That
retired all four checks this section used to list as pending (unused `using`s, second trailing
newlines, the wrapped `if (!double.IsFinite(…))` guards, the non-interpolated `$"…"`): the
formatter accepts all of them, `IDE0005` is confirmed not elevated, and the `$` in
`$"Feature has no extent and cannot be indexed."` is style-only, as P-22 said.

**New, and it qualifies that conclusion: the format check is a floor, not a canonical form.**
`SpatialWriter.BuildOgrGeometry` has now been reformatted by `d207943` (PR #10, 55/55) and
again by `d4fecd1` (PR #12, ~114 lines of the same method), and **`--verify-no-changes` passed
on both.** Whatever those two commits disagree about, the check does not pin — most likely
line endings or trailing whitespace, both of which `.gitattributes` (2518 bytes, present)
should already be handling and evidently isn't pinning for this file. Two consequences:
1. **The format step cannot be relied on to prevent formatting churn**, so large whitespace
   diffs will keep landing inside files that carry open defects — `P-57`'s `Coordinates`
   comparison has now been inside two whitespace-only diffs of its own method without being
   fixed, and P-63's doubled guard likewise. Reviewers must use `git diff -w` on `SpatialWriter.cs`.
2. **I could not determine from the fetched content what the second pass actually changed** —
   the two versions read identically to me. That is stated as a limit of this review rather
   than as a finding, and the cheap resolution is `git show d4fecd1 -- Nsi.Geospatial.Io/SpatialWriter.cs | git apply -R --check`
   locally, or simply `git diff -w b264607 d4fecd1 -- Nsi.Geospatial.Io/SpatialWriter.cs`
   (expect empty). **If that returns empty, PR #12 contains ~114 lines of pure churn in a file
   with two open defects in the churned region, and that is worth saying in the PR thread.**

**Still unverified in CI:** the Release/Debug split's effect on the GDAL warnings (needs the
raw build log), the actual test totals (the API exposes neither pass nor skip counts), and
everything about `d4fecd1` until its run concludes.

---

## 10. Recommended order

0. **DONE at `04118f4` and `b264607`; re-opened only for `d4fecd1`.** Local green at
   `04118f4` (112/110/2/0). CI green at `b264607` including the format step. `d4fecd1`'s run
   was in progress at review time — **wait for it and record the annotation count**, both to
   confirm the prediction above and to establish the baseline for the next step.
1. ~~T-18 + T-21 + T-22~~ **DONE in PR #11 `d3464e6`, merged at `26d2840`;** they closed P-01
   and P-66. All three arrived with more cases than specified here.
   *Kept as the record of why it was sequenced first:* the branch had shipped a public `throw`,
   a public rename, and a new `BoundingBox` member with **zero new assertions**, and the green
   run proved the throw was unreachable from every existing test. That is no longer true, and
   it is the pattern a "tests first" ordering is supposed to buy: PR #12 then changed
   `RTreeManager`'s property names and `SpatialWriter.Write`'s signature **against a suite that
   was guarding the gate, the goldens, and the first-insert path**, which is why a rename of
   this size across three assemblies could be done in one commit and reviewed in one screen.
1a. **Close out P-70 before anything else lands on `main`.** This is new and it is first
   because it is the only item on this list that gets more expensive with every commit: the
   longer `FeatureCollection` lives in docstrings while it is dead in code, the more places
   re-learn the wrong name. Three sub-steps, all mechanical:
   - **Decide D-G** — keep the rename (recommended, pre-0.1.x) or revert it. One sentence, in
     the PR thread, with the reasoning.
   - **`grep -rn "FeatureCollection"`** and fix every hit: `Feature.Owner`'s docstring,
     `Features.Crs`'s "this collection", `var fc`, the `collection` vs `features` parameter
     split (N-3's fifth clause).
   - **Add all four 0.x breaks to one list** — P-14 (`Reprojector` removal), P-67
     (`getArea`/`getPerimeter`), P-61 (namespace, when done), P-70 (this) — so the changelog
     note is written once and nobody has to reconstruct the API history from `Issues.md`.
2. **Finish the warning sweep that PR #12 started** — the five rows in section 9, in the order
   that makes each one a decision rather than an annotation:
   **P-51** (one line, test already exists — cheapest item in the repository, now adjacent to
   two warning PRs that skipped it), **P-22**'s `addFeatureChild` deletion (deletes `:146`
   rather than annotating it), **P-42**'s 4-arm key-selector switch (deletes `:73` and the
   third misspelling in the same edit), `:177`'s `RTreeNode?`, `AttributeTable:57`'s
   `TryGetValue`. Then `CA1711`'s documented outcome (either the suppression or P-70's
   accepted break), the scoped `NoWarn` in **both** csprojs, and the Node.js action bumps
   (P-31 — the only annotation not fixable in C#).
   **Consider `IDE0005` in `.editorconfig` in this commit** so the unused `using`s P-36 lists
   become CI-visible; it will flag `GeometryTests.cs` immediately and is the cheapest forcing
   function for that item.
   **Do not flip `TreatWarningsAsErrors` in this step** unless all five plus the two `NoWarn`s
   plus a clean test-assembly pass land together — and when it flips, note in the message that
   test code is gated too.
2a. **T-23 / P-48.2 — promote ahead of its old slot.** It was sequenced inside step 11's
   R-tree cluster, but PR #11 changed the calculus: `Findable` already exists as an
   independent oracle with a negative control, so the work is converting `FeatureIndicesAt`'s
   two callers and deleting the self-referential version. Until it is done,
   `BulkInsertAllFeaturesFindableByPoint` (500 features, the repository's strongest end-to-end
   assertion) **cannot detect a regression in the one predicate P-01 just closed**. PR #12
   reinforces the ordering: it is the second R-tree-touching PR in a row that did not fix the
   oracle, and the gap has had two chances to close.
3. **P-68 + T-17** — small, and it closes the one coverage hole PR #8 opened (untested write
   arms), now two commits deeper because PR #12 edited `SpatialWriter.Write` twice with no test.
   Do it before P-65 so the consolidation starts with both directions pinned.
4. P-53 + P-36 (one `AssertRel`, shared geometry helpers, shared golden constants, the
   triplicated `TempDir`/`Cleanup`, **rename `GeometryTests` → `BoundingBoxTests`**, fold the
   duplicate `findByInd` test, unify the `Id`/`zerozero` helper block, add a
   `TestFeatures.With(...)` builder, and **settle the two-namespace split** before a fifth file
   lands on the wrong side) — needed *before* the zero-assertion tests land, or they demand
   bit-exactness. **PR #12 supplies the strongest argument yet: ~88 lines of test churn across
   five files for one rename, all of it avoidable with a shared construction helper, and
   three of the four P-53 files were open in the same commit and were not consolidated.**
5. P-56 + P-57 together (storage convention), then T-1…T-4, T-7, T-13. **P-57 is the one to
   do here rather than later: its line has now appeared inside two whitespace-only diffs, and
   each one makes it more likely a reviewer skims past it.**
6. **T-5** — the read-path hole test. Highest value per line in this file.
7. **T-14** — axis order. One test, closes N-2, and guards the sole remaining transform
   path. Cheap enough to fold into step 6.
8. P-05 remainder (`Parts[0].IsHole` check, null-hole handling), then **P-21 + D-F
   together**: the silent geometry drop and the filter-and-count policy for extent-less
   features are the same user-visible failure now that `addFeature` throws. Fixing one
   without the other converts a silent drop into a crash. **PR #12 adds a third member of the
   same family to fix in one pass:** `SpatialWriter`'s
   `Parts.Where(p => p.Vertices.Count > 0)` silently drops empty parts on the way out, which
   is P-69's pattern (P-69's note explains why that belongs here rather than on its own).
9. T-8…T-11 + **T-25**, then **P-17 across both `Feature.Parts` and `Features.FeatureSet`** so
   the cache invariant has no remaining hole (N-10). PR #12's `Count`/indexer forwarding makes
   the façade more urgent, not less.
10. P-04 and P-03 — the two remaining skips, and P-04 is PR #6's stated purpose.
11. **R-tree cluster: P-39 + P-62 + P-15 as one box-model change → P-43 →
    P-48 (incl. .2 if not done at 2a, .1, .4) → P-41 + T-20 → P-47 → P-44 → P-02 →
    P-42/P-46/P-50/P-51**, with D-E resolved before P-02 and D-F resolved before P-02's
    tree wiring.
    - **P-66 is closed, so what remains here is** (a) the public setters that bypass the
      invariant — **P-15**, the highest-value remaining R-tree item, and it can reuse
      `addFeature`'s two checks, which are themselves now tested — and (b) the `1e308`-class
      overflow the finite check lets through.
    - P-39's change must bundle: the `IsEmpty` representation, guards in
      `Area`/`Perimeter`/`ContainsPoint`/`EnlargementToContain`, the overflow/magnitude
      bound, P-62's keep-or-delete decisions, **P-69's `FromVertices` decision**, and
      **P-15/P-63's `RTreeNode.BoundingBox` → `MBR` rename**, which is a property-restriction
      change wearing a naming costume and should not be a separate edit to the same lines.
    - **P-41 last**, because `EnlargementToContain` should not gain a caller until `Empty`
      and overflow are coherent. **This is now a measured hazard, not a hunch:**
      `Empty.EnlargementToContain(box)` is `−∞`, which sorts first. See P-41's caveat.
    - **Add P-47's new sub-question at the head of the cluster:** resolve whether
      `getCandidateFeatNodesByMBR` or `getCandidateEndNodesByMBR` is canonical before wrapping
      either in a public `Query`. Two traversals, different leaf semantics, one of them never
      mentioned in this file until now.
12. Field typing, once, in one change: **P-65** (one mapper, both directions adjacent) with
    **P-64** (reject unrepresentable values; `bool` and `float` arms; `BoolFieldTests`;
    T-19). Both are cheapest immediately after T-16 and T-17 pin both directions, and
    splitting them risks a second round of the same three-file hunt.
13. Structural cleanups while their files are already open: **P-61** (fold the Reprojection
    project into `Io` — one fewer GDAL package copy, one fewer `GdalConfiguration.cs`, three
    fewer warnings, one fewer restore, one fewer CI target), P-23a (`Walk`/`Shoelace`),
    P-63 (writer/reader hygiene — no alias blocks; the `MBR` rename is now folded into P-15
    above), P-26 (join duplication). All behaviour-preserving.
14. Everything else as touched.