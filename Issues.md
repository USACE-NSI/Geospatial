# Issues — open work

Single source of truth for open defects. Closed items are **deleted** — recover them with
`git log --grep 'fix(P-xx)'` and the commit body. Numbers are never reused or renumbered;
merged rows keep every number they absorbed, so `fix(P-41)` still resolves.

Highest number in use: **P-70** (now folded into §5 as comment/local residue — the number stays
reserved). P-71/P-72 were filed twice by mistake and are retired (they were P-02 and P-43).
P-20, P-54, P-55, P-59 are cited elsewhere and undefined — do not reuse. So is **P-48**, cited
by `RTreeTests`' class docstring, and **T-23**, closed but named in four docstrings in that file.
`P-02`/`D-E` are cited from a comment in `SpatialJoins.cs`. Once source names an id it is
reserved for the life of the repo even after its row goes — close with a commit, not silence.

**Rules**
- **Warnings are the maintainer's call.** Listed here with the options named, never as a blocker
  — and never as grounds for changing the public API. No rename, signature change or other
  breakage is justified by a rule id: fix, suppress or ignore is the maintainer's decision.
  Dead code is a different thing — unreachable code is a defect whoever wrote it.
- **Deleting dead code beats annotating it.** Unreachable members get removed, not marked
  nullable or `SuppressMessage`d.
- `Guard` is the only evidence field. `none` means a fix could land and revert green.
- **A behaviour change with no test asserting the observable result is not closed.** Three
  successive commits to one distance loop shipped a crash, then a silent wrong answer, then the
  fix — all green. Fix and its guard land in the same commit; if the guard is hard to write, the
  method's visibility is the bug (see P-02).
- **Assert the externally determined answer.** A bound derived from how a component behaves
  internally is both weaker and easier to get wrong — I derived one as failing on a correct tree
  and CI said otherwise. Exact equality on what the data determines catches false positives too.
- **A test's data is part of its claim.** Disjoint boxes, dead-centre queries and a coverage-only
  check cannot fail on overlapping data, on a near miss, or on a box that grew too large.
- **A guard must not pin a decision §0 leaves open.** If the asserted value depends on an
  undocumented policy, the test fights the tracker.
- Never satisfy a guard by committing the test commented out. `[Fact(Skip = "…")]` with a
  reason, or no test.
- A row needing more than ~6 lines is a design question: write it up in `CHANGES_*.md`, link it,
  keep the row.
- No CI state, no annotation counts, no "verified at `<sha>`". CI is live; a SHA claim here is
  stale within a day. Read `dotnet build` / `dotnet test` output, not the annotations API.
- Closed → delete the row. If it needs explaining, the commit message explains it.
- Before adding a test, grep the test assemblies for the member name. Before renaming anything,
  `grep` the old identifier in comments and string literals — the compiler is the floor, not the
  job.

## 0. Decisions that block work

| ID | Question | Blocks |
|---|---|---|
| D-E | May the R-tree index a geographic CRS? Its MBR math is planar; a degree-space box is not a metric box. | P-02, P-44 |
| D-F | Filter-and-count or refuse, when input can't be placed? **Unforced today** — no repo input triggers it, so it will be decided by accident on first real data. | P-21, P-64, P-69 |
| — | Which traversal is canonical, `getCandidateEndNodesByMBR` (returns end-nodes) or `getCandidateFeatNodesByMBR` (tests children, returns the parent)? `findByXY` uses the latter, `getEndNodes` the former. **One datum in hand, see P-47: the traversal descends near the leaves, so over-reporting is small — the public `Query` can afford exactness.** | P-47 |

## 1. Wrong answers today

| P | What's wrong | Where | Guard |
|---|---|---|---|
| P-11 | Two live defects in `DistanceFeatureToFeature`, both unguarded. (a) `if (part.Vertices.Count < 2) { var first = part.Vertices[0]; }` — `< 2` includes `0`, and `Part` permits a zero-vertex part, so `IndexOutOfRangeException` from inside a join; needs two arms, not one range test. (b) a partless polygon returns the `double.MaxValue` **sentinel, which then participates in the `<`** — so an empty polygon can win a nearest join and have its attributes copied into the target. A sentinel must be unable to win, not merely large. Same zero input throws `ArgumentException` from `addFeature` on the index side. | `SpatialJoins` | none |
| P-03 | `SphericalPointToSegmentDistance` can't detect a foot behind `a` (`acos` term is non-negative by construction): 1000.8 m where truth is 1057.2. Fix: reject when `cos(bearing(a,p)-bearing(a,b)) < 0`, return `R*delta13`. | `GeometryMath` | skipped |
| P-04 | `EarthRadiusFeet = 20925524.9` is **equatorial**; the docstring says authalic and `SphericalArea` squares it. Authalic-in-feet is `20902254.53`. | constants | skipped |
| P-05 | `AreaSquareMeters` treats `Parts[0]` as exterior unconditionally — hole-first feature reports positive area. And `- hole.AreaSquareMeters ?? 0` ignores that `Part`'s own docstring forbids coalescing null. **No test covers `IsHole` on the read path.** | `Feature` | none |
| P-41 | `getAddedSizeToAccomodate` returns set-union area `A + feat − \|A∩B\|`, not MBR growth. `[0,10]²` vs `[20,30]²`: reports 200, truth is 800. For disjoint features it reduces to "smallest child wins". One-line fix: `BoundingBox.EnlargementToContain(bbox)` — but **only after the box model row**, because `Empty.EnlargementToContain(box)` is `−∞` and sorts first. | `RTreeNode` | goldens on the member, none on the child choice (T-20) |
| P-10, P-19 | `JoinType.Average`/`Sum` return **0 for an all-null column** — a fabrication, not a measurement; should be null. `ToDouble`'s string fallback calls `double.TryParse` with **no `InvariantCulture`**, so aggregation is host-locale dependent. And the point→polygon direction has no aggregation path at all, so these arms are reachable one way only. | `SpatialJoins` | none |
| P-09 | `AttributeTable` is schema-only now, so the old "loses data" mechanism is gone — replaced by a worse one. `RenameColumn`/`RemoveField` never touch `Feature.Attributes`, so after a rename the schema says `COUNTY`, every feature still keys `FIPS`, and `CoerceRow` (which copies only schema-known keys) **drops the values silently on write**. Second route in: both join directions backfill the schema **only when the dest field appears in `sourceFields`**, so a mismatched pair writes `Attributes[field]` for a column that was never added — same silent drop, from a call site that looks validated. Move both or refuse; never half-move. | `AttributeTable`, `SpatialJoins` | none |
| P-23, P-23a | `GeometryMath` docstrings wrong ×4 (great-circle triangle −0.8491%, not as written; 65°N −0.6614%; pole-crossing ring 64.8× out). Its advice to *densify before measuring* makes it worse. Four copies of the same walk loop; `Area` and `Centroid` are one shoelace and `Measure()` calls both, so every ring is walked twice for one sum. | `GeometryMath` | prose |

## 2. Blocked on a decision, then mechanical

| P | Decision, then the sites | Guard |
|---|---|---|
| P-21, P-64, P-69, D-F | **Refuse loudly, or filter and report?** Eight sites, one policy, all one line each: `ProcessGeometry`'s missing `else` (drops `wkbMultiLineString`/`wkbPoint` silently → zero parts, which is what feeds P-11's empty-polygon-wins path); `SpatialWriter`'s `Parts.Where(Vertices.Count > 0)`; `SpatialJoins.BuildTree`'s `!= Empty` skip; `CoerceRow`'s unknown-key drop; `FromVertices` skipping NaN via `if (x < minX)` (so `[(0,0),(NaN,NaN)]` → a *plausible finite wrong* box that clears both index gates — and the constructor uses `Math.Min`/`Math.Max`, which *does* propagate NaN: two NaN policies in one struct); writer accepting `long` > 2^53 (GeoJSON parses through `double` → digits silently lost; DBF ceiling is width **18**, never 20 — width 20 *causes* the `OFTReal` demotion); `bool` written as `"1"`/`"0"` into `OFTString` then rejected by `bool.TryParse` on read, so a `bool` returns `null`; `Aggregate`'s `_ => null` for an unhandled `JoinType`. `addFeature`'s existing gate is the model: reject at the boundary, name the value. | none |
| P-39, P-62, P-15, D-E | **Box model.** `Empty` is the full-range sentinel, so `Area`/`Perimeter` are `+inf`, `ContainsPoint` is true for everything, and `EnlargementToContain` = `Union(other).Area() - Area()` is asymmetric about `Empty` (`0` one way, `−∞` the other) — while `Overlaps`/`Contains`/`Union`/`OverlappingArea` *do* guard. Four unguarded members of one struct disagreeing with three siblings. **Scope is narrower than it looked: everyday union propagation on finite boxes is now guarded and green, so this row is the sentinel and the public setters, not normal inserts.** Still in it: a near-full-range box (`±1e308`) is finite, not `Empty`, clears both gates and still overflows; `RTreeManager.Root` and `RTreeNode.BoundingBox` are public setters and `addChild` validates nothing; `ContainsPoint` is the only member with no test; `FromVertices` and `Point` are uncalled-but-tested / tested-and-uncalled and need a keep-or-delete call. The `BoundingBox` → `MBR` property rename was bundled here historically — **it is a change you may decline; the setter restriction is the substance and it does not need the rename.** **Expect `FreshRootHasInfiniteAreaSoTheComparisonNeverFires` and `EnlargementToContainIsAsymmetricAboutEmpty` to go red when `Area(Empty)` stops being `+inf`; that is correct, and `bestCandidate ??= TreeManager.Root` stops being load-bearing — say so in the commit.** | partial |
| P-14, D-D | **CRS authority is unrepresentable.** `CrsInfo.EpsgCode` is `int?`, so `CrsInspector` reads `GetAuthorityCode("PROJCS")`, gets `102003`, stores it in a field named `EpsgCode` — and `SameCrs` then reports an authoritative match across two different authorities. Three separate places build/parse the `"EPSG:…"` string; `SpatialReader` formats and re-parses it inside one method. Carry authority+code as a pair and delete the parsers. The `EPSG:102003 … but ESRI:102003 is` warning every run is the cheapest repro in the repo. Axis order now rests on one code path with no test (T-14). | none |

## 3. R-tree and tests

| P | What's wrong | Guard |
|---|---|---|
| P-42, P-43, P-40, P-46, P-50 | One file, one PR, ordered cheapest first. **P-43:** the ctor validates nothing → *two* crash paths from one missing invariant: `Options.First()` when `2*min > Count`, and `Children.Min(…)` on an empty list when `min == 0`. Guard once in the ctor (`1 <= min && min*2 <= max`), not at each symptom. **P-42, and what is now ruled out:** box over-growth is **not** a live symptom — every node box equals its children's union and the root equals the closed-form box of everything inserted, green on 200 overlapping features. So the remaining damage from `buildChildOptions` allocating two fresh nodes per candidate × every split position × four orderings and calling `addChild` on real children is the **`child.Parent` overwrite into throwaway nodes** (repaired only on the two surviving options' paths) and the allocation churn, not geometry. Its `sortedChidrens = null` was **annotated, not removed**: the four-branch `if`/`else` still stands, so the latent null is latent — a `(xAxis, min) switch` key selector deletes it, which is the fix *and* the simplification, not an annotation. `siblingOverlap`/`cumulativeOverlap` written and never read (one reads the enclosing node's field, so both candidates get identical values). **P-40:** all-leaves fallback + `RecomputeMBR`'s four `Children.Min/Max` enumerations per level per insert. **P-46:** leaf acceptance is unconditional. **P-50:** `getIsEndNode` classifies on `Children[0]` while `getChildrenContainingInd` dereferences across all of them — the two methods disagree about the same invariant. | partial — propagation and candidate completeness guarded; `Parent` integrity and child choice are not |
| P-47 | No `Query(BoundingBox)`, no k-NN, no `Count`, no way to get feature indices out. `findByXY` hand-writes `new BoundingBox(x,y,x,y)` where `Point(x,y)` exists. Resolve §0's canonical-traversal question **before** wrapping either, or the public `Query` inherits the ambiguity. **Empirical datum, first one the index has produced:** with 200 mutually overlapping boxes (`[4i, 4i+9]²`) and `minChilds: 3, maxChilds: 6`, a point query never offered more than 8 features — so the traversal descends close to the leaves rather than returning high covering nodes, and over-reporting is small. Design the public `Query` around exact results; don't budget for a bloated candidate set. Measure more before deciding; don't infer the mechanism from one shape. Decide too whether an `Empty`/non-finite *query* box throws or returns empty — recommend empty; a caller can fix a feature but not a query. | none |
| P-68, T-17 | `SpatialWriter.MapFieldType`'s `LongFT => OFTInteger64` and `SetOgrField`'s `case long l:` have **never had a test**, through several PRs one of which edited `Write`'s signature and feature loop directly. `FieldTypeTests`' docstring still claims writer coverage the file doesn't have. T-17: assert the **declared OGR type** *and* the **boxed CLR type** — a value-only assertion cannot fail here, because a `LongFT` column authored as `OFTString` round-trips `4000000000` perfectly through `Convert.ChangeType` while the schema lies. | — |
| T-5, T-14 | **Highest value per line in the file.** T-5: exterior+hole through a real shapefile, assert `IsHole` on ring 1 and `Area == exterior − hole` — the only guard for a live production behaviour with zero read-path coverage. T-14: read lon/lat into a projected CRS, assert X is still longitude — the sole remaining transform path, and the deleted P/Invoke twin ignored axis order. | — |

## 4. Joins, model, IO

| P | What's wrong | Guard |
|---|---|---|
| P-02, P-26, P-25, P-44 | Both join directions end in `_ = pointTree ?? BuildTree(points);` — a whole R-tree built and dropped; candidate selection is a full O(polygons × points) scan. The comment now says so honestly and names this row, but the cost is unfixed: an MBR genuinely can't bound distance-to-segment, so the decision is *sound radial prefilter or delete the tree and the public parameter*. **Decide `DistanceFeatureToFeature`'s visibility in the same change**: it is `private`, so `T-7` can only assert *which polygon wins*, never the distance — pinning the value needs `internal` + `InternalsVisibleTo`, or a public `SpatialJoins.Distance(Feature, Feature)` that the joins then call. Also in here: planar distance regardless of `Crs.Kind`; the two directions copy-paste the schema backfill and disagree about ties (`1e-9` in CRS units — ~0.1 nm in metres, ~0.1 mm in degrees); `interiorOnly` is accepted and never read while `exteriorOnly` is; `ContainsIndex(i, c) => c == i` is pure indirection; `Feature.Wkt` is stale state; aggregations have no output column; and the `NearestPolygonsToPoints` docstring reads `soint, attach…`. | `T-7` on the winner only |
| P-17, P-38, N-10 | `Feature.Parts` and `Features.FeatureSet` are public mutable `List`s, while both types also expose `Count`/`this[]` forwarding — a read-only *appearance* with a mutable *escape hatch*. `Parts.Clear()` leaves a stale `BoundingBox`; mutating `FeatureSet` bypasses `AddFeature`'s id assignment **and** `Crs`'s invalidation sweep, whose damage is invisible (stale metrics, not a stale count). `Part` already has the right shape — private list, `IReadOnlyList` facade, one mutator. Do both classes in one change. `Feature.ShapeType` vs `Features.ShapeType` are two free-to-disagree sources of truth. | none |
| P-18 | `RemoveFeature` renumbers survivors and leaves the detached feature's `Owner` pointing at the collection, so it keeps resolving the old CRS — currently rescued only by `Part`'s `CrsInfo` reference-identity check in a different class. `BuildTree` keys on `f.Id`, which mutates under removal, so a pre-existing index silently returns the wrong feature. | pinned as broken |
| P-65 | The `FieldType`↔OGR↔CLR mapping lives in **six** hand-maintained places (`SpatialReader.MapFieldType`, `SpatialWriter.MapFieldType`, `ReadFieldValue`, `AttributeColumn.FieldTypeToType`, `AttributeColumn.Coerce`, and `CoerceRow`, which decides *which* values get coerced while `Coerce` decides *how* — two of the six in one file, mutually unaware). P-06 happened because `LongFT` was missing from three of them. One internal mapper, both directions adjacent, no public change. **Sequence after T-17** — a consolidation with one direction tested silently breaks the untested one, which is exactly how P-01 got reopened. | — |
| P-08, P-27, P-28 | CSV values containing CR/LF corrupt the round trip (quote or reject). DBF schema order differs after round trip. Writer's convention is "null stays null", reader returns `""` for a null text field, and `Aggregate`'s `.Where(v => v is not null)` converts unknown→zero a third time — fix the reader half knowing that line will quietly disagree with it. Model-layer null semantics are decided and tested; don't re-litigate them. | partial |
| P-12 | `Ogr.RegisterAll()` on every `Read` **and** every `Write`, not idempotent-safe. `Io.Tests` disables parallelisation as a workaround and names this as the real fix. The culture-mutating test lives in the *other* assembly, which has no such guard — it is safe only because it is synchronous, and nothing says so in the file. | none |

## 5. Hygiene (behaviour-preserving; do while the file is open)

| P | What's left |
|---|---|
| P-22, P-60, P-63, P-33, P-70 | **Comments and locals only — no type, member or signature changes (Rules).** The `FeatureCollection` → `Features` rename shipped and stays; what it left behind is prose that is wrong about current code: `Feature.Owner`'s docstring ("Set by `FeatureCollection`.AddFeature"), `Part`'s class docstring naming the dead type in its invariant, `SpatialJoins.BuildTree(Features fc)`, `Features.Crs`'s "this collection", `var fc = new Features()`, `string? collectionWkt`. Verified by one grep returning nothing: `FeatureCollection`. **`addFeatureChild` is unreachable and misleading** — it holds an area tie-break the live path lacks, so a reader assumes the tie-break is active, and it ends in `bestCandidate!.addFeatureChild(…)` asking the compiler to stop asking; delete it. `siblingOverlap`/`cumulativeOverlap` dead. Three spellings of one non-word (`Childs`/`Chidrens`/`Chlidren`), none of them `Children` — all internal, so `MaxChildren`/`MinChildren` is free to take or leave. **The deleted `addFeature(int[], double, double, double, double)` signature still survives in prose in two places**: `BuildTree`'s docstring (wrong argument order since `04118f4`, and `the collection'sMBRs`) and `RTreeTests`' trailing comments on `new BoundingBox(0, 0, 10, 10)` (`// Xmax=10, Xmin=0, Ymax=10, Ymin=0` — the ctor is `(minX, minY, maxX, maxY)`). `RTreeTests`' class docstring points at `P-48`, which has no row. `SpatialWriter`'s degenerate-ring guard stated twice with identical conditions. `SpatialWriter.SetOgrField`'s `P0-4` comment means changelog item 4, not P-04. No `using` alias blocks in Io — bare `Geometry` → `CS0118` (enclosing namespaces beat file-level usings) while bare `Feature` binds to *OGR's*; that style is load-bearing, not accidental. `PartType` sits a namespace away from its only consumer. `gdal` → `GDAL`. |
| P-30, P-31, P-32, P-34, P-35 | **`TreatWarningsAsErrors` is an *if*, not a *when*.** Nothing in this file treats it as owed, and it is the mechanism that let an analyzer drive a public rename once — so if it ever goes on, it goes on because you want it, and the ordering below is only about making each remaining warning a decision instead of an annotation: delete `addFeatureChild`, P-42's key selector, `RTreeNode? bestCandidate`, then a `WarningsNotAsErrors` for `CS8600` in **both** GDAL-referencing csprojs (prefer it to `NoWarn` so our own `CS8600`s stay visible). Analysis covers test assemblies too. **The rest is CI and workflow, unrelated to the flag:** README's remaining errors; Node 20 deprecation on all four actions; test projects `IsPackable`; `release.yml` pins `@main`; **CI's "Test (non-Gdal)" step has no `--filter`, so it runs the solution and then `Io.Tests` again — one flag (`--filter "Category!=Gdal"`)**; **`dotnet test` prints a case total and CI prints one too, nothing captures it, so a test that stops being discovered is invisible and green** — write it into the job summary and diff it; CI is `-c Release`, local is Debug, so a Release-only failure has no local repro command. |
| P-61 | `Nsi.Geospatial.Reprojection` is one class in a project: a solution node, a `ProjectReference`, a CI `--include` target, and a **second full copy of the GDAL package** — restore, native runtime, and a second identical set of `GdalConfiguration.cs:60` warnings, since the generated file is compiled into every project that references the package. That duplication is the evidence; `dotnet build` prints it, the annotations API does not. The "keeps core GDAL-free" rationale is already satisfied by `Io`. Cheapest structural deletion here, and it deletes warnings as a side effect rather than as a reason. |
| P-36, P-53 | Two namespaces in one test assembly (fossil of an abandoned `Nsi.Geospatial.Core` rename), which is *why* a 246-line duplicate suite went unnoticed — the classes couldn't collide. Three `Rel` implementations, two of them unable to compare against zero (`diff <= \|expected\|*tol` becomes `diff <= 0`, and the message divides by zero); adopt `RelD`'s `Math.Max(\|expected\|*tol, absTol)` with `absTol` chosen per unit. Duplicate `Cell`/`IntoFeature`/`TempDir`/`PointInPolygon`; golden constants duplicated across assemblies, so changing `EarthRadiusAuthalicMeters` breaks a project that doesn't reference the file you edited. `GeometryTests` is entirely `BoundingBox` — rename before the next member lands. **`RTreeTests` now carries `AuthoredBox`, `OverlappingBox`, `Covers`, `ContainsPoint`, `CandidateFeatureIds`, `CollectFeatureIds`, `AssertCovers`, `AssertIsExactUnion`; `SpatialJoinTests` carries `Rectangle`, `PointAt`.** Right where they are for now — but two of those are geometric containment and one is a box union, all three reimplementing `BoundingBox` members in a file that isn't about bounding boxes. `TestFeatures.With(...)` must absorb them rather than let a third copy appear. |

## 6. Breaking changes that shipped, before 0.1.x — changelog note, written once

None of these are owed to a rule id; this is a record of what is now true of the API.

`Reprojector` removal (public static, P/Invoke path returned transposed coordinates for geographic CRS) · `getArea`/`getPerimeter` → `Area`/`Perimeter` · `FeatureCollection` → `Features` **and** `.Features` → `.FeatureSet` (the second was compiler-forced: a member can't share its type's name — and it's the one that touched every call site) · `SpatialWriter.Write`'s parameter `collection` → `features`, which breaks named-argument callers against the concrete class only · `Reprojection` namespace, if P-61 lands.

## 7. Work order

1. **P-11** — two arms and a non-winnable sentinel in `DistanceFeatureToFeature`. `BuildTree`'s
   docstring and `RTreeTests`' `Xmax=` comments are the same defect class and can ride along.
2. **P-68 + T-17**, then **P-65**. Both directions pinned before the consolidation.
3. **P-53 + P-36** — before any zero-assertion test lands, or they demand bit-exactness.
4. **T-5**, then **T-14**.
5. **P-21 + D-F** together with the box-model row — fixing the silent drop without the policy
   converts a silent drop into a crash with a message about bounding boxes.
6. **P-17 + T-25**, then P-18, then P-09 and P-10.
7. **P-15 + P-39 + P-62** as one box-model change → P-43 → P-42/P-40/P-46/P-50 → **P-47 with §0's
   traversal decision** → **P-41 last** → P-02/P-44.
8. P-04 and P-03 — the last two skips. **The skip budget is exactly two**; a third skip is a
   defect being hidden.
9. P-61, P-23a, P-26, and §5's comment/local sweep while those files are open.

## 8. Gradients worth running, not inferring

```bash
rm -rf **/obj **/bin && dotnet build Geospatial.slnx -c Release   # read the output, not CI
dotnet test 2>&1 | tail -1                                        # the case total; diff it
grep -rn "Skip *=" tests/                                         # must be P-03 and P-04, by name
grep -rn "^ *// *\[[Fact\|[Theory]" tests/                        # commented-out tests: fake guards
grep -rn "Overlaps" tests/                                        # oracles compute with comparisons
grep -rn "private static.*(" tests/ \| sort \| uniq -c            # committed-but-uncalled helpers
grep -rn "FeatureCollection\|collectionWkt\|soint" --include=*.cs .   # residue in comments and locals
grep -rn "Xmax\|Xmin\|Ymax\|Ymin" --include=*.cs .                # prose for a deleted signature
grep -rn "getArea\|getPerimeter\|getMBRoverlap" --include=*.cs .
grep -rn "addFeatureChild\b" --include=*.cs .
grep -rn "Vertices\[i + 1\]\|Count - 1; i++" --include=*.cs .     # pair-walks that must wrap on a ring
grep -rn "double.MaxValue" Nsi.Geospatial/Spatial/                # sentinels that can enter a min
grep -rn "P0-4\|intentionally left unchanged" --include=*.cs .
grep -n "void Write" Nsi.Geospatial.Io/SpatialWriter.cs Nsi.Geospatial.Io/IFeatureSink.cs
git diff -w <base> <head> -- Nsi.Geospatial.Io/SpatialWriter.cs   # format churn vs real change