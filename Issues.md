# ISSUES — consolidated backlog

**Repository:** [USACE-NSI/Geospatial](https://github.com/USACE-NSI/Geospatial)
**Branch reviewed:** `feature/spherical` @ `56c8005` (PR #6, "initial spherical math")
**Supersedes:** `CHANGES_08312026.md`, `CHANGES_09012026.md`, `CHANGES_09082026.md`
**Review date:** 2026-09-08 · **Revised:** 2026-09-08 (post `16585ef` reconciliation)

Single source of truth for known defects and open work. The three `CHANGES_*` files are
dated review artefacts, not a live tracker — move them to `docs/reviews/` unedited and
record new work only here. See §7 N-7 for why that matters more than it sounds.

---

## 1. How to use this file

**Status values** (use exactly these so the file stays greppable):

| Status | Meaning |
|---|---|
| `open` | Confirmed present at HEAD. No work started. |
| `open (partial)` | Later work reduced the scope; remainder described in the row. |
| `blocked` | Cannot be estimated until a named decision or spike lands. |
| `closed` | Verified fixed in code. Listed in §6, never deleted. |

**Priorities:** **P0** correctness / data loss · **P1** robustness / latent crashes ·
**P2** minor / consistency · **P3** housekeeping.

**Numbering.** `P-xx` IDs are assigned once and never reused, including after closure or
retraction. When you close an item, set Status to `closed` and add `Closed by <sha>`. Do
not delete the row — the `0901` file *deleted* its P1-10 row instead of resolving it, and
the P/Invoke layer it referred to survived being "resolved".

**ID map.** `RTreeManager` and `RTreeNode` are **foundational and permanent** — see §2
D-A. All R-tree work is fix work; deletion and replacement were considered and rejected.

---

## 2. Resolved decisions

### D-A · RESOLVED — the R-tree stays. All R-tree work is fix work.

`RTreeManager`/`RTreeNode` are a foundational feature of this library and will not be
removed or replaced. They already support bulk adding, and they have been benchmarked
against other .NET R-tree implementations (including RBush) and are more performant.
Earlier framing in this review that treated "no production caller" as an argument for
removal was mistaken: it is simply `P-02`, and given the above it is a requirement rather
than a judgement call. **`P-02` is now mandatory: the spatial joins must use the R-tree, or
expose an explicit option to.**

### D-B · RESOLVED — the R-tree tests pass, and deliberately so

`16585ef` (AlexRyanUSACE, 2026-09-01 17:31 UTC) converted
`[Fact(Skip = "Fails by design: asserts correct behavior that the original (unfixed) RTree
split/overlap defects violate…")]` into a live `[Fact]` as the proof that its MBR
propagation fix worked. CI is green because that fix works. `CHANGES_08312026.md` still
claims these tests "fail (or are skipped with a reason) … that's evidence, not a
regression" — it was written the previous day and never revised. **House rule settled:
`RTreeTests` asserts correct behaviour.** The leftover `//(Skip = ...)` comment is debris
and should be deleted (`P-48`).

### D-C · OPEN — spike: what can the gdal 3.11.3 C# binding actually do?

Gates `P-06` and `P-20`. `0901 P0-4` prescribes `SetField(name, l)` plus `OFTInteger64`,
but `SpatialWriter` carries an in-code comment stating the binding has "no object overload,
and no `Layer.FieldIndex` in 3.11.3", that "the string setter works for any OGR field
type", and that `P0-4` is "intentionally left unchanged in this class". The changelog and
the code comment disagree about what is possible. Establish the real surface — `SetField`
overloads, `OFTInteger64`, and `Geometry`/`Feature` ownership for `P-20` — before
estimating either. `tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs` is a commented-out
tool built to answer exactly this question for P1-10; run it once, capture the output,
then delete it (`P-37`).

### D-D · OPEN — design: one CRS-token and authority model

Gates `P-14`. `CHANGES_09082026.md` P2-15 recommends changing
`Projection.AlbersUsa.EpsgCode` from `"EPSG:102003"` to `"ESRI:102003"`. **Applying that
recommendation as written introduces a new bug:** `Reprojector.CrsToken` prepends `"EPSG:"`
to any token not already starting with that exact prefix, producing
`"EPSG:ESRI:102003"`, which OSR cannot resolve. Token builder, parser
(`SpatialReader.ParseEpsg`) and comparison (`SameCrs`) must be redesigned together.

### D-E · OPEN — policy: may the R-tree index geographic (lon/lat) collections?

Gates `P-02` and `P-47`. Axis-aligned boxes in degrees have two problems the tree cannot
solve internally:

1. `BoundingBox`'s constructor **normalises** (`MinX = Math.Min(minX, maxX)`), so any
   footprint spanning the antimeridian is silently inverted into a box covering most of
   the globe.
2. A degree of longitude is not a degree of latitude (roughly half by 65°N), so
   box-distance is not monotone in ground distance and kNN pruning is unsound in degrees
   without a latitude-aware bound.

Choose: **(a)** require projected input and throw when `Crs.Kind == CrsKind.Geographic`, or
**(b)** accept geographic input and pad the search window conservatively
(`dLat = d/111320`, `dLon = d/(111320 · cos φ)`) — over-fetches candidates, never misses
the true nearest. (b) is safer for callers; either way, dateline data needs explicit
handling regardless.

---

## 3. P0 — correctness / data loss

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-39` | **`BoundingBox.Empty` is the entire double plane, not an empty box.** `Empty` is built as `(MaxValue, MaxValue, MinValue, MinValue)` and the constructor normalises, yielding `[-1.8e308 … +1.8e308]` in both axes. The `== Empty` guards in `Overlaps`/`Contains`/`Union` hide this; the unguarded members are wrong: `Area()` overflows to `+∞`, `ContainsPoint` returns true for every point, `RTreeNode.getArea` is `+∞` (its `MaxX < MinX` guard can never fire), `getMBRoverlap` returns `+∞` against everything. `Empty` is the **initial value of every tree node**, and `addFeature` validates nothing — one feature with an unset box hands the tree `+∞` costs and insertion ranking becomes arbitrary | `Geometry/BoundingBox.cs`, `Spatial/RTreeManager.cs` | absorbs `0901 P1-4` | `open` | Add `public readonly bool IsEmpty` set by a private ctor; `Area()` and `ContainsPoint` return 0/false when set; switch `Overlaps`/`Contains`/`Union` from `== Empty` to `IsEmpty`; `addFeature` throws `ArgumentException` on an empty box. `FromVertices`' unreachable `IsPositiveInfinity` check then becomes unnecessary. **Prerequisite for `P-01` and `P-02`.** |
| `P-02` | **Both joins build an R-tree and immediately discard it** (`_ = pointTree ?? BuildTree(points)`). The public API advertises tree-based candidate selection it does not perform; joins are O(n·m). Also `NearestPolygonsToPoints` leaves destination fields unset when nothing matched, and takes no `JoinType` (asymmetric with its sibling) | `Spatial/SpatialJoins.cs` | `0901 P0-2`, `0831` | `blocked` on `P-39` → `P-01` → `P-47` → `P-44` → **D-E** | **Mandatory per D-A.** Tree-driven candidate selection by default, with an explicit opt-out retained (brute-force overload or `useTree: false`) so results can be verified. Acceptance: `SpatialJoinTests` **fails if the tree is broken** — assert tree-assisted and brute-force results are identical on random data, so the discarded-parameter problem cannot silently return. Delete `_ = tree ?? BuildTree(…)`. Fix `BuildTree`'s docstring, which still describes the argument order `a6f3def` deleted. |
| `P-03` | `SphericalPointToSegmentDistance` cannot detect a perpendicular foot behind `a`; `deltaAT = acos(cos d13 / cos dXT)` is non-negative by construction, so the case is unreachable | `Geometry/GeometryMath.cs` | `0908 P0-10` | `open` | Guard `cos(InitialBearing(a,p) - InitialBearing(a,b)) < 0` → return `radius * delta13`. Measured today: returns 1000.786 m where the nearest point is `a` at 1057.160 m. Un-skip `PointToSegment_WhenFootIsBehindTheNearEndpoint_ReturnsDistanceToA`. **On the join path** — nearest-line joins currently prefer segments they should not. |
| `P-04` | **Units contract.** `EarthRadiusFeet = 20925524.9` is documented as "6,371,000 m in feet" but × 0.3048 = 6,378,099.99 m — the WGS84 **equatorial** radius. `SphericalArea`'s docstring warns that the equatorial radius "inflates them ~0.22%" and then instructs callers to pass this constant for square feet | `Geometry/GeometryMath.cs` | `0908 P0-11`, PR #6 body | `open` | Don't merely correct the constant. PR #6's stated goal is *"right now it outputs meters instead of feet - we should probably change that"*, so expose `AreaSquareFeet` / `LengthFeet` (or a unit-tagged quantity) and delete the pass-a-magic-radius advice. Acceptance: `EarthRadiusFeet_IsTheAuthalicRadiusInFeet` un-skipped and green; no caller needs to know a radius. |
| `P-05` | `Feature.AreaSquareMeters` hole accounting depends on a flag known to be inverted: treats `Parts[0]` as exterior and subtracts every later part flagged `IsHole`, where `IsHole` is derived `!Direction` | `Geometry/Feature.cs`, `Geometry/Part.cs` | `0901 P2-1` **+ new** | `open` | `P2-1` was rated P2 because `IsHole`/`Direction` were "dead except inside `CloseRing`". No longer true — the field is load-bearing in a headline metric, and `SphericalMetricsTests.Ring()` relies on the derivation. Fix polarity to the shapefile convention (outer rings CCW) or decide holes by containment rather than a flag. Acceptance: an exterior+hole polygon returns exterior-minus-hole. |
| `P-06` | Writer field typing: `SetOgrField` casts `long` → `int`; `FieldType.LongFT` → `OFTString`; bools written `"1"`/`"0"` but read through `bool.TryParse` (coerce to null) | `Io/SpatialWriter.cs` | `0901 P0-4` | `blocked` on **D-C** | Acceptance: a 64-bit ID greater than 2³¹ and a `bool` survive write-then-read unchanged; one bool representation end to end. |
| `P-07` | `CloseRing()` is called on linestrings and points — appends the first vertex to open lines (inflating perimeter, polluting distances) and duplicates point vertices | `Io/SpatialReader.cs` | `0901 P0-5` | `open` | Call `CloseRing` only for polygon rings. The defect is currently **pinned by test**: `SpatialIoTests.LinesShapefileRoundTrip` comments "The reader closes every part, so assert on the leading (real) vertices" — that assertion must be rewritten, not preserved. Acceptance: `LengthMeters` on a read line equals the sum of its edges. |
| `P-08` | CSV round-trip corrupts values containing newlines: the writer quotes them, `ReadAll`/`ParseLine` reset state per line | `Io/Csv/CsvHelper.cs` | `0901 P0-8` | `open` | Accumulate lines while a quote is open, **or** declare the writer write-only and stop quoting `\n`. Acceptance: round-trip test for embedded newline, comma, doubled quote. |
| `P-09` | `AttributeTable.RenameColumn` moves only the schema key; per-feature `Attributes` keep the old key, so `CoerceRow` silently drops the renamed column | `Attributes/AttributeTable.cs` | `0901 P0-9` | `open` | Propagate the rename to rows, or rename `RenameColumnSchemaOnly` and delete `AddField`'s now-empty backfill comment. |
| `P-10` | `JoinType.Average` still throws when values exist but none are numeric, and returns a silent `0` when no values exist | `Spatial/SpatialJoins.cs` | `0901 P0-6` | `open (partial)` | Current code is `values.Count == 0 ? 0d : values.Where(v => ToDouble(v) is not null).Average(...)` — the guard was moved, not fixed. Filter to numerics first, then guard; decide empty-vs-zero explicitly. |
| `P-11` | `DistanceFeatureToFeature` indexes `Vertices[0]` when `Vertices.Count == 0` (the `< 2` branch still dereferences), and reads point coordinates off `BoundingBox` with no `Empty` check | `Spatial/SpatialJoins.cs` | `0901 P0-7` | `open` | Skip empty parts; refuse when `BoundingBox.IsEmpty` (after `P-39`). Note this is also the route by which an empty box enters the tree — see `P-39`. |

---

## 4. P1 — robustness, latent crashes, and the R-tree's pruning defect

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-01` | **`getMBRoverlap` reports 0 for a node MBR fully contained in the query box.** Verified: query inside node → works; point query → works; node fully contained (`bbox.MinX < MinX && bbox.MaxX > MaxX`) → both clauses false → returns 0. `CHANGES_08312026.md` #15 has this right; `CHANGES_09012026.md` P0-1 states it inverted. **Dormant today** because the only reachable shapes dodge it: `findByXY` builds a degenerate box (`MinX == MaxX`) so both clauses collapse to a correct containment test, and the insert path is caught by the all-leaves fallback. It becomes live the moment `Query(BoundingBox)` or kNN exists — where an expanding window swallows whole node MBRs and prunes them | `Spatial/RTreeNode.cs` | `0901 P0-1` (mis-attributed, see N-7), `0831` #15 | `open` — **P0 → P1**, see §5 | Delegate to the existing, tested, **uncalled** `BoundingBox.Overlaps`; delete the `Math.Max(…, 1)` floor (a correct closed-interval test makes it unnecessary, and it is actively harmful — see `P-41`); split into `Intersects(q)` (predicate) and `OverlapArea(q)` (split scoring). Keep a deprecated `getMBRoverlap` shim if the API must survive. **Blocks `P-02`.** |
| `P-40` | Insertion falls back to scanning **every leaf**: `getCandidateEndNodesByMBR` prunes with the buggy gate, and `if (candidateKids.Count == 0) candidateKids = TreeManager.getEndNodes;` fires whenever a feature intersects no existing leaf MBR — which is most of the time. Placement quality is fine (min-enlargement over all leaves); the cost is quadratic build. **Not a correctness bug** | `Spatial/RTreeNode.cs` | new | `open` | Add Guttman `ChooseSubtree` — descend to the leaf minimising enlargement, tie-break on least area — returning exactly one leaf, and delete the fallback. `BoundingBox.EnlargementToContain` is already implemented and **has no callers**; use it. Because bulk adding is a supported, benchmarked path, **measure before and after**: split the benchmark into build and query, since a comparison against `RBush.Load()` (bulk STRtree) versus incremental `addFeature` compares different workloads. If build already wins, this is a refinement, not a fix. |
| `P-41` | `getAddedSizeToAccomodate` returns `getArea + featArea - getMBRoverlap(bbox)` — that is **union area**, not enlargement, despite the name; and the `max(overlap, 1)` floor is a bare `1` in raw CRS units (1 m² in metres; ~12,000 km² in degrees, swamping genuine overlaps) | `Spatial/RTreeNode.cs` | `0831` (`Math.Max` floor) | `open` | Split into `Enlargement(b)` = `BoundingBox.EnlargementToContain(b)` for insertion cost, and `OverlapArea` used only for split scoring, with no floor. If union-area is the intended criterion, name and document it — it changes which leaf wins, which is benchmark-visible. |
| `P-42` | `buildChildOptions` **re-parents live children while scoring discarded split options**: `node1.addChild(Child, false, false)` executes `child.Parent = this` onto a throwaway candidate. Self-healing only because `split()` calls `UpdateParents` on the winning pair; the losers leave every child's `Parent` pointing at a discarded object until then | `Spatial/RTreeNode.cs` | new — **confirmed by `16585ef`** | `open` | `16585ef` added `canPropagateMBRup: false` at exactly these call sites, i.e. it suppressed the *second* symptom (ancestor MBR corruption) while leaving the mutation in place. Fix the cause: score candidates by accumulating `BoundingBox` locally (or a small `SplitCandidate` holding a child slice and two boxes) and construct `RTreeNode`s only for the winner. Also removes O(children × options) pointless writes per split. |
| `P-43` | `Options.First()` throws for legal-looking constructor arguments. `for (split = MinChidrens; split <= Children.Count - MinChidrens; split++)` is empty when `Count < 2·min`, and `Count` at split time is `max + 1`, so the invariant is **`max >= 2·min − 1`**. Defaults 4/10 (10 ≥ 7) and the tests' 3/6 (6 ≥ 5) are safe; `new RTreeManager(4, 6)` throws from inside `split()`, far from the cause | `Spatial/RTreeManager.cs`, `Spatial/RTreeNode.cs` | `0901 P1-1` | `open` | Validate in the `RTreeManager` constructor with a message naming the rule; additionally guard `split()` with an explicit `InvalidOperationException`. |
| `P-47` | **The tree cannot be used by the joins — the API doesn't expose what they need.** `RTreeManager` offers `findByXY` (point → end nodes), `findByInd` (index → node path) and `getEndNodes`. **None returns feature indices**, and there is no rectangular range query, no kNN, no `Count`, no removal. `NearestPointsToPolygons` cannot be wired without the caller re-implementing child re-testing using the same buggy predicate — which is exactly what `RTreeTests.FeatureIndicesAt` has to do by hand | `Spatial/RTreeManager.cs`, `Spatial/RTreeNode.cs` | new | `open` — **promoted P2 → P1** (gates the P0 `P-02`) | Add: `IEnumerable<int[]> Query(BoundingBox)` (≈15 lines once `P-01`/`P-46` land); `List<(int[] FeatureIndex, double Distance)> Nearest(point, k, distanceFn)` as branch-and-bound over a priority queue keyed by min-distance-to-MBR, pruning once the queue head exceeds the k-th best; `int Count`; a bulk entry point over `FeatureCollection` that validates boxes (`P-39`). kNN soundness in geographic CRS is **D-E**. |
| `P-44` | **`SpatialJoins.DistanceFeatureToFeature` ranks candidates with planar `GeometryMath.Distance` / `PointToSegmentDistance` regardless of CRS kind.** A nearest-join on a lon/lat collection ranks by degree-distance, where a degree of longitude ≠ a degree of latitude. Wrong today, independent of the tree | `Spatial/SpatialJoins.cs` | new | `open` | Dispatch on `Crs.Kind` to the spherical primitives. Must land in the **same change** as `P-02`, or the tree prunes by one metric while survivors are ranked by another. |
| `P-48` | **The R-tree test suite cannot see any of these defects.** Every case uses uniformly spaced, pairwise-disjoint boxes on a diagonal, queried by a point inside the feature's own box — the easiest configuration an R-tree can be given, structurally incapable of triggering `P-01`, `P-41` or depth problems. `SpatialJoinTests` gives the tree **zero** coverage: it passes `pointTree: SpatialJoins.BuildTree(pnts)` into a parameter the implementation discards, so it would pass identically if `BuildTree` returned `null` or threw after construction | `tests/Nsi.Geospatial.Tests/RTreeTests.cs` | new | `open` | In priority order: **(1) a regression test for upward MBR propagation** — insert a feature that expands a deep leaf, assert every ancestor's box covers it; this is the invariant `16585ef` established and **nothing currently protects it** against a refactor dropping `canPropagateMBRup`. **(2)** dense random and nested (big-box-containing-small-boxes) geometry — the direct `P-01` guard. **(3)** a `Query(box)` vs LINQ full-scan equivalence property test over random data — subsumes the hand-picked cases and would have caught `P-01` immediately. **(4)** `new RTreeManager(4, 6)` validation. **(5)** empty-box insert. **(6)** a depth/fanout assertion. Also delete the fossil `//(Skip = ...)` comment — a reader cannot tell whether it is a live TODO or debris. |
| `P-12` | `Ogr.RegisterAll()` called unconditionally on every `Read`/`Write`. Not idempotent, not thread-safe: two threads double-register plugin drivers and abort the process. Worked around in tests by `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, which serialises the suite but does not fix production | `Io/SpatialReader.cs`, `Io/SpatialWriter.cs` | `0908 P1-16`, `0901 P2-9` | `open` | One-time guard (`internal static class GdalBootstrap` with `Lazy<bool>` — `ExecutionAndPublication` is the right default). Then delete the assembly attribute, whose own comment says to remove it together with this fix. Acceptance: Io.Tests run in parallel. |
| `P-13` | `LengthMeters` means different things per CRS branch: Projected uses `Part.Perimeter` (open walk), Geographic uses `SphericalPerimeter` (which **closes** the ring). A 3-vertex 1-degree line reports 379,639.757 m against a true 222,390.159 m (+70.7%) | `Geometry/Part.cs`, `Geometry/GeometryMath.cs` | `0908 P1-15` + `P2-11` | `open` | Make both branches an open walk, or stop `SphericalPerimeter` closing. Fix the `SphericalPerimeter` docstring in the same commit — it describes an open walk while the loop indexes `% pts.Count`. Un-skip `LengthMeters_MeansTheSameThingInBothCrsKinds`. |
| `P-14` | Retire the surviving P/Invoke layer and unify CRS-token handling. `Reprojector.cs` is still in the tree, still public, still P/Invokes **private** OGR symbols (`OGRNewCoordinateTransformation`, `OGR_CT_Transform`), and `CrsToken` still prefers the EPSG token over WKT. Meanwhile `SameCrs`/`ParseEpsg` discard the authority, so `ESRI:102003` compares equal to `"EPSG:102003"` | `Reprojection/Reprojector.cs`, `Io/SpatialReader.cs`, `Projections/Projection.cs` | `0901 P1-10` (remainder), `0908 P2-15`, `0908` "P1-10 follow-on" | `open (partial)`, `blocked` on **D-D** | `0908` marks P1-10 "Resolved", but only `CoordinateTransformer` migrated to the managed binding; `Reprojector` survives as a duplicate second implementation of the same job. Land together: authority-aware token builder and parser, authority-checked `SameCrs`, `"ESRI:102003"`, removal of `Reprojector.Native`. Also add `Transformer_EatsLonLatNotLatLon` — the axis-order fix (closed, §6) has **no test guarding it**, so a refactor can silently re-transpose every geographic transform. |
| `P-15` | R-tree encapsulation and hardening: `Root` has a **public setter** (`split()` does `TreeManager.Root = newRoot` on root split, so any caller that cached the root holds a detached subtree); `addFeature` performs no box validation | `Spatial/RTreeManager.cs` | `0901 P1-3` (remainder) | `open` | `{ get; private set; }` with the assignment performed by an `internal` method (or have `split()` return the new root and let the manager swap it). Validate boxes on insert alongside `P-39`. The `FeatureIndex` null-guard formerly in this row was re-rated — see `P-51`. |
| `P-17` | `Feature.Parts` is a public `List<Part>` while `BoundingBox` only advances in `AddPart`, so a direct `Parts.Add` leaves a stale box — `SpatialJoinTests` itself calls `Parts.Add` then `ComputeBoundingBox()` | `Geometry/Feature.cs` | `0901 P1-6` | `open (partial)` | `Mbr` renamed to `BoundingBox` and `ComputeBoundingBox()` added; encapsulation not done. Expose `IReadOnlyList<Part>` or compute lazily. Feeds `P-11`/`P-39` — a stale box is how garbage reaches the tree. |
| `P-18` | Identity model: `RemoveFeature` renumbers every surviving `Id` (desyncing any tree built earlier, while the removed feature keeps its old `Id`), and `BuildTree` keys on `f.Id` rather than collection position, so `findByInd` is useless for features not added via `AddFeature` (all `Id == 0`) | `Geometry/FeatureCollection.cs`, `Spatial/SpatialJoins.cs` | `0901 P1-7`, `P1-8` | `open` | Give features stable identity, or rebuild indexes on mutation, or document both at the call site. Grows in importance once `P-02` makes the tree load-bearing — and pairs naturally with a removal API in `P-47`. |
| `P-19` | `AttributeColumn.Coerce` parses with current-culture `ToString`/`TryParse` for double, float, int, long and DateTime | `Attributes/AttributeColumn.cs` | `0901 P1-9` | `open` | `CultureInfo.InvariantCulture` plus explicit `NumberStyles`/`DateTimeStyles`. Acceptance: under a `de-DE` culture, `1,5` reads correctly and `1.5` as intended. |
| `P-20` | OGR ownership unverified: `using var defn = feat.GetFieldDefnRef(i)` may free shared layer state; `using var of` passed to `CreateFeature` may double-free; `of.SetGeometry(geom); geom.Dispose();` rests on an unverified "OGR copies the geometry" comment | `Io/SpatialReader.cs`, `Io/SpatialWriter.cs` | `0901 P1-11` | `blocked` on **D-C** | Verify against the 3.11.3 binding; add focused tests. Native memory corruption is silent. |
| `P-21` | Geometry-type coverage is asymmetric. `MapGeomType` (reader) and `MapShapeTypeToOgr` (writer) both silently default unknown types to Point; multipolygon rings flatten into one part list losing exterior↔hole association; and `BuildOgrGeometry` **emits** `wkbMultiLineString` which `ProcessGeometry` cannot **read** | `Io/SpatialReader.cs`, `Io/SpatialWriter.cs` | `0901 P1-13` + new | `open` | Handle types explicitly (throw on unmapped); handle `wkbMultiLineString`/`wkbMultiPoint`/collections; preserve ring grouping. Acceptance: write a multi-part line, read it back. |

---

## 5. Retracted and relocated — do not re-report

| ID | Disposition | Why |
|---|---|---|
| `P-45` | **RETRACTED — not a defect.** Claimed "a split cannot cascade (`canSplit: false`), so fanout silently exceeds `MaxChidrens`" | I read only the first `addChild` call. `16585ef` pairs them deliberately: `Parent.addChild(kid[0], false, true)` then `Parent.addChild(kid[1], true, true)`. Net +1 on the parent, and the **second** add has `canSplit: true`, so the parent splits if it overflows. The asymmetry avoids splitting on the intermediate state and predates the commit, which translated it faithfully into the three-argument signature. **The `maxChildren` invariant holds.** |
| `P-01` | **Relocated P0 → P1** | See N-7. `16585ef` fixed the defect that actually lost data (MBR propagation, now `P-49`). The containment gate remains, but is provably dormant for point queries and inserts, and no rectangular or kNN query API exists yet. It is a prerequisite for `P-02`, not an active data-loss bug. |
| `P-16` | **Merged into `P-39`** | The `BoundingBox` empty-sentinel item was the same root cause, seen from `BoundingBox.cs` rather than from its effect on the tree. `P-39` is the complete statement. |
| `P-51` | **Split out of `P-15`, re-rated P1 → P3** | `getChildrenContainingInd`'s unguarded `node.FeatureIndex[0]` only NREs if an *internal* node sits under a node reporting `getIsEndNode`. Since `getIsEndNode` tests only `Children[0].FeatureIndex`, that requires mixed-level children — and it is **not constructible through the public API**: splits always produce same-level siblings, and `addFeatureChildEnforceIntersect` appends feature nodes only to nodes from `getEndNodes`. Defensive hardening, not a reachable crash. Keep the guard (`node.FeatureIndex is { Length: > 0 } fi && fi[0] == ind`); stop calling it a bug. |

---

## 6. Already fixed — do not re-report

Verified in code at `56c8005`.

| Legacy ID | Item | Evidence |
|---|---|---|
| **`P-49`** | **R-tree MBR upward propagation — the defect that actually lost data.** An MBR that did not propagate upward left every ancestor's box **too small**, so a query pruned a subtree genuinely containing matches: features silently went missing as the tree grew. **No changelog ever named this bug.** `0831`'s inventory of "defects preserved on purpose" lists four items (split double-parenting, the containment gate, stale `cumulativeOverlap`/`siblingOverlap`, the `Math.Max` floor) — propagation is not among them, and `0901` has no propagation row | **Closed by `16585ef`** (AlexRyanUSACE, 2026-09-01 17:31): `addChild(child, bool canSplit, bool canPropagateMBRup)` with `else if (canPropagateMBRup) RecomputeMBR();`. Fully preserved at HEAD — `a6f3def`, `42ac0c9`, `ac3bb82`, `1cd383a` only reshaped it (`BoundingBox.Union` replaced the four field-wise `if`s; same semantics). Un-skipped `BulkInsert_AllFeaturesFindableByPoint` as its proof. **Guard with the `P-48` regression test.** |
| `0831 #9` | `AttributeColumn.Coerce` threw NRE on null | `if (raw is null) return null;` with the `fix(#9)` comment in place |
| `0831 #10` | `FindUniques` replaced by `Distinct` | `CsvHelper.ReadUniqueColumn` uses `.Distinct(StringComparer.OrdinalIgnoreCase)` |
| `0831 #11` | `ReadCSVtoDict` naive split and trailing-null NRE | Gone; quote-aware `ParseLine` present |
| `0831 #15` | `BoundingBox.Overlaps` closed-interval test | Present with `fix(#15)` comment plus `Empty` guards — but still has **no production caller** (N-1); `P-01`/`P-47` consume it |
| `0831 fix:` | `Feat` parallel lists replaced | `Feature` holds `Parts` + `Attributes` + `Owner` together |
| `0831 fix:` | Deterministic OGR disposal; no hardcoded `C:\Software\GDAL GISInternals` | `using var` throughout reader/writer; CI exports `GDAL_DATA`/`PROJ_LIB`/`LD_LIBRARY_PATH` |
| `0901 P1-5` | `addFeature(featInd, Xmax, Xmin, Ymax, Ymin)` reversed argument order | Now `addFeature(int[] featInd, BoundingBox bbox)` — closed by `a6f3def`. **Docs still describe the old signature** — see N-3 |
| `0901 P1-3` | `public _root` field reassigned on split | Now `public RTreeNode Root { get; set; }`; the still-public setter is `P-15` |
| `0901 P3-3` | `--filter Category!=Gdal` was a no-op | Filter removed from `ci.yml`; the trait now exists — successor problem is `P-35` |
| `0908 P1-14` | Axis order unpinned; geographic transforms silently transposed | `srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER)` on both SRSes. **Unguarded** — the recommended `Transformer_EatsLonLatNotLatLon` test was never added; tracked in `P-14` |
| `0908 P1-10` | P/Invoke threw `EntryPointNotFoundException` | Half-closed: `CoordinateTransformer` uses the managed binding; `Reprojector`'s layer survives. Remainder is `P-14` |

**Partially fixed** (kept above with `open (partial)`): `P-17` (was `P1-6`), `P-26` (was
`P2-4`), remainder of `P-22` (was `P2-5`/`P2-7`), `P-28` (was `P2-9`), `P-35` (was `P3-3`),
`P-14` (was `P1-10`).

---

## 7. P2 / P3 — consistency, docs, CI

### P2

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-23` | One consolidated docstring pass over `GeometryMath`: (a) `SphericalArea` claims "Exact on a sphere; the only error is sphere-vs-ellipsoid", true only for rings whose edges follow meridians and parallels — a great-circle 1-degree triangle is −0.8491%; (b) the "0.63% low at 65N" figure measures −0.6614%; (c) a pole-enclosing ring sweeps the full longitude range and returns an area off by 64.8× (2.511583e14 against a true 3.874521e12) | `Geometry/GeometryMath.cs` | `0908 P2-11`–`P2-14` | `open` | One commit, four docstring fixes. State the curve (+0.4489% at 0°, +0.1029% at 30°, −0.2106% at 44°, −0.5671% at 60°, −0.6614% at 65°). Document the pole limitation or detect longitude wrap and throw. **Do not densify** — `0908` shows the error is orders of magnitude below the accepted ellipsoid term at NSI footprint scale. |
| `P-24` | Nearest-neighbour tie-break epsilon `1e-9` is in absolute CRS units — meaningless across CRS kinds | `Spatial/SpatialJoins.cs` | `0901 P2-2` | `open` | Relative tolerance or an explicit parameter. Revisit with `P-44`/`P-02`, since it is the same comparison. |
| `P-25` | Sum/Average/Count write numbers into columns backfilled with the **source** column's type, which may be Text | `Spatial/SpatialJoins.cs` | `0901 P2-3` | `open` | Widen the backfilled column type for aggregate joins. |
| `P-26` | Dead API surface: `interiorOnly` accepted and never read; `ContainsIndex` wraps a single `==`; `Feature.Wkt` is never written by Io at all | `Spatial/SpatialJoins.cs`, `Geometry/Feature.cs` | `0901 P2-6`, remainder of `P2-4` | `open (partial)` | `P2-4` is half-done: `FeatureCollection` and `Part` no longer expose `Wkt`, and `CrsInfo.Wkt` is now the one authoritative copy — so `Feature.Wkt` should be **deleted**, not renamed to `SrsWkt` as `0901` recommended. |
| `P-27` | Column order relies on `Dictionary` insertion order, so DBF field order depends on an implementation detail; `Reorder` rebuilds a `Dictionary` | `Attributes/AttributeTable.cs` | `0901 P2-8` | `open` | Back with `List<AttributeColumn>` plus a name index. |
| `P-28` | Reader null semantics inconsistent: unset text → `""`, unset date → `null` | `Io/SpatialReader.cs` | `0901 P2-9` (remainder) | `open (partial)` | `LayerIndex` was added (multi-layer data now addressable); null semantics unsettled. Pick one convention. |
| `P-29` | `CoordinateTransformationOptions` is available (`SetAreaOfInterest`, `SetBallparkAllowed`, `SetDesiredAccuracy`, `SetOnlyBest`) but unused, so PROJ may pick a global ballpark transform over a grid-based one. NAD83/conus grid shifts are cm–m; ballpark reaches tens of m | `Reprojection/CoordinateTransformer.cs` | `0908 P3-3` | `open` — **re-rated P3 → P2** | Fold into `P-14` so reprojection is made correct once. Pass an area of interest for conus NSI work. |
| `P-46` | `getCandidateEndNodesByMBR` applies **no MBR test at leaf level** (`if (getIsEndNode) nodeWalk.Add(this);`). Pruning happens only at intermediate levels, so returned leaves may not intersect the query. Harmless for insertion, wrong for any *query* use — which is why `RTreeTests.FeatureIndicesAt` must re-test every child by hand | `Spatial/RTreeNode.cs` | new | `open` | Test `Intersects(bbox)` before adding, and let `P-47`'s `Query` return feature indices so re-verification isn't the caller's job. |
| `P-50` | `getIsEndNode` inspects only `Children[0].FeatureIndex`, so mixed-level children would break `getEndNodes`/`getCandidateEndNodesByMBR`. Not reachable through the current API (see `P-51`) | `Spatial/RTreeNode.cs` | new | `open` | Assert uniform child depth as an invariant rather than inferring leaf-ness from the first child — this also makes `P-48`'s depth assertion meaningful. |

### P3

| ID | Title | Component | Legacy | Status | Fix / acceptance |
|---|---|---|---|---|---|
| `P-22` | R-tree naming and dead code: `MaxChidrens`/`MinChidrens` typos; lowerCamelCase public members; `cumulativeOverlap`/`siblingOverlap` are **written by `buildChildOptions` and read by nothing** (the sort uses `metrics[0..2]`, and `cumulativeOverlap = overlap + siblingOverlap` reads *the splitting node's* property, not the candidate's — so even if consumed it would be meaningless); `addFeatureChild` unreachable; unused `System.Xml`, `System.Xml.Linq`, `System.Text`, `System.Threading.Tasks` usings; `int[] featInd = null` on a non-nullable parameter, a warning suppressed only by `TreatWarningsAsErrors=false` | `Spatial/RTreeManager.cs`, `Spatial/RTreeNode.cs` | `0901 P2-5`, `P2-7`, `0831` | `open` | Rename to `MaxChildren`/`MinChildren`, PascalCase members, delete the dead pair and `addFeatureChild`, drop unused usings. Resolve C2 in the process: `0831` calls the overlap properties a live defect and `0901` calls them dead code — **they are dead**, confirmed by reading the sort keys. |
| `P-30` | README carries four inaccuracies — the project is `Nsi.Geospatial`, not `Nsi.Geospatial.Core`; `TreatWarningsAsErrors` is claimed but `Directory.Build.props` sets `false`; CSV is claimed behind `IFeatureSource`/`IFeatureSink` but `CsvHelper` is a static class wired to neither; ".NET 8 is enough" conflicts with the SDK pin. **Additionally: the feature this branch exists for is undocumented** — `CrsInfo`, `CrsInspector`, `AreaSquareMeters`, `LengthMeters`, `ReprojectTo`, `RequireInspectableCrs` appear nowhere | `README.md` | `0901 P3-1`, `P2-5` (docs half) | `open` | Fix the four claims; document the spherical/CRS surface with a worked example. |
| `P-31` | The SDK/target-framework policy is stated five inconsistent ways: `global.json` pins `9.0.100` (`rollForward: latestFeature`, which will not cross a major), `Directory.Build.props` targets `net8.0`, README says ".NET 8 is enough", `ci.yml` installs 8.0.x **and** 9.0.x, `release.yml` requests `dotnet-version: 8.0.x` | `global.json`, `Directory.Build.props`, `README.md`, `ci.yml`, `release.yml` | `0901 P2-10`, `P3-1` | `open` | Choose one floor and express it once. The `release.yml`/`global.json` conflict postdates the changelogs and is a release-pipeline failure waiting to happen. |
| `P-32` | `tests/Nsi.Geospatial.Tests/*.csproj` missing `<IsPackable>false</IsPackable>` (Io.Tests has it) — a solution-wide `dotnet pack` emits test packages | `tests/**` | `0901 P3-2` | `open` | Add the property. |
| `P-33` | `PackageReference Include="gdal"` (lowercase) is a non-canonical package id | `Nsi.Geospatial.Io.csproj` | `0901 P3-4` | `open` | Use `GDAL`. |
| `P-34` | `release.yml` invokes a reusable workflow pinned to a mutable `@main` in another org | `.github/workflows/release.yml` | `0901 P3-5` | `open` | Pin to a tag or commit SHA. |
| `P-35` | CI runs `Nsi.Geospatial.Io.Tests` twice — `dotnet test Geospatial.slnx` already includes it, and a following step runs the project again. The `Category=Gdal` trait is now declared on `SpatialIoTests`/`CrsInspectionTests` but consumed by no filter | `.github/workflows/ci.yml` | successor to `0901 P3-3` | `open (partial)` | Split into a GDAL-free job and a native-GDAL job, or drop the trait. |
| `P-36` | Three test files cover the same spherical/CRS surface with divergent helpers and two namespaces. `SphericalMathTests.cs` (8.6 KB) and `SphericalMetricsTests.cs` (31.4 KB) both assert closed-form graticule area, antimeridian crossing, winding independence, degenerate rings, haversine distance, perimeter summation and point-to-segment — with two private `Rel` helpers of different signatures and two cell builders (`LonLatCell` vs `Cell`) — declaring `Nsi.Geospatial.Tests` vs `Nsi.Geospatial.Core.Tests`. `CrsInfoAndAreaTests.cs` (12.7 KB) and `CrsInspectionTests.cs` likely overlap the same way | `tests/**` | new | `open` | Merge by function, not authoring session. One namespace. One tolerance idiom — `SpatialIoTests` passes `const double Tol = 1e-9` to `Assert.Equal` while `CrsInspectionTests` uses digit counts (`Assert.Equal(1.0, …, 12)`), which is the very overload confusion `P-37`/`0908 P3-4` describes; unify before closing that. |
| `P-37` | Dead test and writer code: `ProbeOsrBinding.cs` is 100% commented out, self-labelled "TEMPORARY diagnostic for P1-10 … Delete once settled" — P1-10 is settled; and `SpatialWriter.ClosedRing`/`RingWkt`/`Fmt` are orphaned by the switch to the programmatic OGR geometry API | `tests/Nsi.Geospatial.Io.Tests/ProbeOsrBinding.cs`, `Io/SpatialWriter.cs` | new | `open` | Run the probe once first if its output is still wanted (**D-C**), then delete. |
| `P-38` | `Feature.ShapeType` and `FeatureCollection.ShapeType` are duplicated state; Io reads only the collection's while tests set both | `Geometry/Feature.cs`, `Geometry/FeatureCollection.cs` | new | `open` | Drop the per-feature copy, or make it authoritative and validate consistency. |

---

## 8. Duplicate clusters collapsed

Eleven root causes were reported more than once, sometimes with different IDs **and
different recommended fixes**. Each is now one row.

| Cluster | Root cause | Reported as | Now |
|---|---|---|---|
| C1 | R-tree containment gate | `0831` #15 + "intentionally unfixed"; `0901 P0-1` | `P-01` — and `0901` mis-attributed the *symptom* to it; the real cause was propagation (`P-49`). See N-7 |
| C2 | `cumulativeOverlap`/`siblingOverlap` | `0831` "live defect"; `0901 P2-7` "dead code" | `P-22` — **resolved: dead.** The sort keys on `metrics[0..2]`; nothing reads them |
| C3 | Join builds a tree and discards it | `0831`; `0901 P0-2` | `P-02` |
| C4 | `Ogr.RegisterAll()` per call | `0901 P2-9`; `0908 P1-16` | `P-12` (kept the P1; retired the P2) |
| C5 | `SphericalPerimeter` closes the ring | `0908 P1-15`; `0908 P2-11` | `P-13` (+ docstring half in `P-23`) |
| C6 | CRS authority confusion | `0901 P1-10` (row deleted); `0908` "P1-10 follow-on"; `0908 P2-15` | `P-14` — **the two prescribed fixes contradict each other**; see **D-D** |
| C7 | R-tree dead code | `0831`; `0901 P2-7` | `P-22` |
| C8 | `TreatWarningsAsErrors=false` | `0901 P2-5`; `0901 P3-1` | `P-30` (docs claim) |
| C9 | .NET 8 vs SDK pin | `0901 P2-10`; `0901 P3-1` | `P-31` (now also collides with `release.yml`) |
| C10 | R-tree style and typos | `0831` "typos preserved on purpose"; `0901 P2-5` | `P-22` — and **both descriptions are stale**: `42ac0c9`/`ac3bb82`/`1cd383a` already refactored these files |
| C11 | CSV correctness | `0831` #10, #11 (**done**); `0901 P0-8` (**open**) | `P-08`; #10/#11 in §6. Not duplicates, but adjacent rows in one file invite conflation |

**Numbering is not a citable scheme.** `0831` states #1–#8 and #12–#14 are unreferenced;
`0901` has no P0-3 and no P1-12; `0908` has no P0-12, P1-12 or P2-16. Cite `P-xx` from now
on.

---

## 9. Cross-cutting notes

- **N-1 — three correct primitives sit uncalled.** `BoundingBox.Overlaps` (the correct
  closed-interval test, `fix(#15)`), `BoundingBox.Contains`, and
  `BoundingBox.EnlargementToContain` (the correct insertion cost function) have **no
  production caller**. `0831` instructs "new geometry code should use the corrected
  `BoundingBox.Overlaps`" while `RTreeManager` still queries `getMBRoverlap`. `P-01`,
  `P-40`/`P-41` and `P-47` exist largely to connect them. Either connect or delete.
- **N-2 — the axis-order fix is unguarded.** `0908` recommended
  `Transformer_EatsLonLatNotLatLon` as the guard for `P1-14`. The fix landed; no such test
  exists. Tracked in `P-14`.
- **N-3 — three docs describe a signature that no longer exists.**
  `SpatialJoins.BuildTree`'s docstring says "Note the original addFeature argument order:
  (featInd, Xmax, Xmin, Ymax, Ymin)" about a signature `a6f3def` deleted; `RTreeTests`
  carries the same stale `// Xmax=10, Xmin=0` inline comments; and `RTreeTests`' class
  docstring claims the tests "deliberately do NOT assert correct behavior" while its
  method names assert exactly that (and `16585ef` made that intentional). Fix all three in
  `P-02`/`P-48`.
- **N-4 — `0908`'s own summary is not reconcilable with its table.** It claims "11 open …
  1 hypothesis refuted", but the refuted hypothesis appears nowhere in the readable table.
- **N-5 — coverage caveat for this consolidation.** `CHANGES_09012026.md` and
  `CHANGES_09082026.md` exceed my fetch limit and their prose tails were unreadable through
  every mirror attempted: `0908`'s full "Resolved: P1-10" narrative and the refuted
  hypothesis, and `0901`'s rows below `P3-5`. Everything in the readable portion of both
  tables is accounted for above. **Re-read those two tails and reconcile before treating §6
  as exhaustive.**
- **N-6 — keep `0831`'s numbering legend readable.** `fix(#N)` and `fix:` markers are still
  in the source (`BoundingBox.Overlaps`, `AttributeColumn.Coerce`, `CsvHelper`,
  `Part.AddVertex`, `Feature`). Keep `0831` at `docs/reviews/` so those in-code markers stay
  decodable, or migrate the legend into §6 and re-tag the comments.
- **N-7 — the process lesson, and a standing instruction.** A fix that stopped data loss
  (`16585ef`, MBR propagation) landed on 2026-09-01 and appears in **none** of the three
  review files. `0901` was written 4 h 38 min *before* it, and pinned the correct symptom —
  "features silently go missing once the tree grows" — on the wrong cause (the containment
  gate). The `0908` review then read the changelogs as its map rather than the history as
  its record and never mentioned it. The files were **written between fixes and never
  reconciled**, so their causal claims misdirect fixes, not just their status columns.
  **Standing rule: any row citing `0901 P0-1` must be re-read against `16585ef` first.**
  Corollary: when closing an item, name the commit; when reviewing, read
  `git log -- Nsi.Geospatial/Spatial/` before trusting a changelog.
- **N-8 — benchmark hygiene for the R-tree.** Build cost and query cost are separate numbers
  here: the all-leaves fallback (`P-40`) makes *build* quadratic while leaving *query*
  quality untouched. If the RBush comparison used `RBush.Load()` (bulk STRtree) against
  incremental `addFeature`, it compared a bulk loader to a per-item insert. Re-run as two
  series before and after `P-40`/`P-41` so the perf claim stays defensible and any
  regression is visible.

---

## 10. Suggested sequencing

1. **`P-39`** — `BoundingBox.IsEmpty`. Everything R-tree-adjacent reads a box; the sentinel
   is wrong today and `P-01`'s guards depend on it.
2. **`P-01` → `P-43` → `P-48` (items 1–3)** — fix pruning, validate configuration, then land
   the propagation regression test and the `Query`-vs-LINQ equivalence test. Fix and guard
   together.
3. **`P-40` / `P-41`** — cost-function correctness and the fallback removal, with the
   split build/query benchmark from N-8.
4. **`P-47`** — `Query`, `Nearest`, `Count`, bulk add. Decide **D-E** first.
5. **`P-44` → `P-02`** — CRS-correct distance, then wire the joins, with the
   tree-vs-brute-force equivalence test so `SpatialJoinTests` actually covers the tree.
6. **`P-42`, `P-46`, `P-50`, `P-15`, `P-22`, `P-51`** — the remaining R-tree hardening and
   cleanup pass, once behaviour is locked by tests.
7. **The two spikes (`D-C`, `D-D`)** as timeboxed work, then estimate `P-06`, `P-14`, `P-20`,
   `P-29`.
8. **`P-03`, `P-04`, `P-05`** — the spherical-math P0s, independent of all of the above.
   `P-04` is what PR #6 set out to ship.
9. **`P-12`** — small, unblocks test parallelism, removes a workaround the code explicitly
   asks to have removed.
10. **`P-36`, `P-37`, `P-30`, `P-31`, `P-32`, `P-33`, `P-34`, `P-35`** — consolidation and
    housekeeping. Cheap, and they reduce the odds of another divergent set of review files.