# Issues — open work

Single source of truth for open defects. Closed items are **deleted** — recover them with
`git log --grep 'fix(P-xx)'` and the commit body. Numbers are never reused or renumbered;
merged rows keep every number they absorbed, so `fix(P-41)` still resolves.

Highest number in use: **P-70**. P-71/P-72 were filed twice by mistake and are retired (they
were P-02 and P-43). P-20, P-54, P-55, P-59 are cited elsewhere and undefined — do not reuse.
`P-02` and `D-E` are cited from a comment in `SpatialJoins.cs`: once source names a number it
is reserved for the life of the repo even after its row goes, so close it with a commit, not
a silence.

**Rules**
- `Guard` is the only evidence field. `none` means a fix could land and revert green.
- A row needing more than ~6 lines is a design question: write it up in `CHANGES_*.md`, link it,
  keep the row.
- No CI state, no annotation counts, no "verified at `<sha>`". CI is live; a SHA claim here is
  stale within a day. Read `dotnet build` output, not the annotations API.
- Closed → delete the row. If it needs explaining, the commit message explains it.
- **A behaviour change with no test asserting the value is not closed.** Three successive commits
  to one distance loop shipped a crash, then a silent wrong answer, then the fix — all green,
  because nothing anywhere asserts a distance. Fix and its guard land in the same commit.
- Prefer **deleting** dead code to annotating it, and **causing** a warning to disappear over
  silencing it (`TreatWarningsAsErrors` is `false`, so a green build cannot tell you a sweep
  skipped the hard half — count fixes yourself and put the number in the message).
- Before adding a test, grep the test assemblies for the member name. Before renaming anything,
  `grep` the old identifier in comments and string literals — the compiler is the floor, not the
  job.

## 0. Decisions that block work

| ID | Question | Blocks |
|---|---|---|
| D-E | May the R-tree index a geographic CRS? Its MBR math is planar; a degree-space box is not a metric box. | P-02, P-44 |
| D-F | Filter-and-count or refuse, when input can't be placed? **Unforced today** — no repo input triggers it, so it will be decided by accident on first real data. | P-21, P-64, P-69 |
| D-G | May a style analyzer change the public API? PR #12 answered *yes* silently; `CA1711` is now absent and the type is renamed. Decide once, in the open. | P-70 |
| — | Which traversal is canonical, `getCandidateEndNodesByMBR` (returns end-nodes) or `getCandidateFeatNodesByMBR` (tests children, returns the parent)? `findByXY` uses the latter, `getEndNodes` the former. | P-47 |

## 1. Wrong answers today

| P | What's wrong | Where | Guard |
|---|---|---|---|
| P-56, P-11 | `DistanceFeatureToFeature` skipped the closing edge of an authored-open ring, so the nearest polygon differed by whether the caller typed the closing vertex — square from `(-1,5)`: truth 1.0, measured 5.0990. Corrected by gating on `part.IsRing` and walking `Vertices[(i + 1) % n]`. **Not closed: no test asserts a distance anywhere** (T-7). Still live in the same method: `if (part.Vertices.Count < 2) { var first = part.Vertices[0]; }` admits `0` → `IndexOutOfRangeException` from inside a join; and a partless polygon returns the `double.MaxValue` sentinel **which participates in the `<`**, so an empty polygon can win a nearest join and have its attributes copied. Two arms, not one range test; a sentinel that can never win. | `SpatialJoins` | none → T-7 |
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
| P-21, P-64, P-69, D-F | **Refuse loudly, or filter and report?** Eight sites, one policy, all one line each: `ProcessGeometry`'s missing `else` (drops `wkbMultiLineString`/`wkbPoint` silently → zero parts, which is what feeds P-56's empty-polygon-wins path); `SpatialWriter`'s `Parts.Where(Vertices.Count > 0)`; `SpatialJoins.BuildTree`'s `!= Empty` skip; `CoerceRow`'s unknown-key drop; `FromVertices` skipping NaN via `if (x < minX)` (so `[(0,0),(NaN,NaN)]` → a *plausible finite wrong* box that clears both index gates — and the constructor uses `Math.Min`/`Math.Max`, which *does* propagate NaN: two NaN policies in one struct); writer accepting `long` > 2^53 (GeoJSON parses through `double` → digits silently lost; DBF ceiling is width **18**, never 20 — width 20 *causes* the `OFTReal` demotion); `bool` written as `"1"`/`"0"` into `OFTString` then rejected by `bool.TryParse` on read, so a `bool` returns `null`; `Aggregate`'s `_ => null` for an unhandled `JoinType`. `addFeature`'s existing gate is the model: reject at the boundary, name the value. | none |
| P-39, P-62, P-15, D-E | **Box model.** `Empty` is the full-range sentinel, so `Area`/`Perimeter` are `+inf`, `ContainsPoint` is true for everything, and `EnlargementToContain` = `Union(other).Area() - Area()` is asymmetric about `Empty` (`0` one way, `−∞` the other) — while `Overlaps`/`Contains`/`Union`/`OverlappingArea` *do* guard. Four unguarded members of one struct disagreeing with three siblings. Also: a near-full-range box (`±1e308`) is finite, not `Empty`, clears both gates and still overflows; `RTreeManager.Root` and `RTreeNode.BoundingBox` are public setters and `addChild` validates nothing, so `RecomputeMBR`'s unguarded `Children.Min/Max` propagates a bad box to every ancestor; `ContainsPoint` is the only member with no test; `FromVertices` and `Point` are uncalled-but-tested / tested-and-uncalled and need a keep-or-delete call. Bundle the `BoundingBox` → `MBR` property rename here — it is a setter-restriction change wearing a naming costume. **Expect `FreshRootHasInfiniteAreaSoTheComparisonNeverFires` and `EnlargementToContainIsAsymmetricAboutEmpty` to go red when `Area(Empty)` stops being `+inf`; that is correct, and `bestCandidate ??= TreeManager.Root` stops being load-bearing — say so in the commit.** | partial |
| P-14, D-D | **CRS authority is unrepresentable.** `CrsInfo.EpsgCode` is `int?`, so `CrsInspector` reads `GetAuthorityCode("PROJCS")`, gets `102003`, stores it in a field named `EpsgCode` — and `SameCrs` then reports an authoritative match across two different authorities. Three separate places build/parse the `"EPSG:…"` string; `SpatialReader` formats and re-parses it inside one method. Carry authority+code as a pair and delete the parsers. The `EPSG:102003 … but ESRI:102003 is` warning every run is the cheapest repro in the repo. Axis order now rests on one code path with no test (T-14). | none |
| P-70, D-G | **Rename residue, one commit.** `CA1725` is closed — `IFeatureSink` and `SpatialWriter` both say `features`. What's left is what no analyzer sees: `Feature.Owner`'s docstring ("Set by `FeatureCollection`.AddFeature"), `Part`'s class docstring naming the dead type in its invariant, `SpatialJoins.BuildTree(Features fc)`, `Features.Crs`'s "this collection", `var fc = new Features()`, `string? collectionWkt`. Decide D-G, then one grep, then §6. | grep |

## 3. R-tree and tests

| P | What's wrong | Guard |
|---|---|---|
| P-42, P-43, P-40, P-46, P-50 | One file, one PR, ordered cheapest first. **P-43:** the ctor validates nothing → *two* crash paths from one missing invariant: `Options.First()` when `2*min > Count`, and `Children.Min(…)` on an empty list when `min == 0`. Guard once in the ctor (`1 <= min && min*2 <= max`), not at each symptom. **P-42:** `buildChildOptions` allocates two fresh nodes per candidate × every split position × four orderings and calls `addChild` on real children — which overwrites `child.Parent` into throwaway nodes, repaired only on the two surviving options' paths. Its `sortedChidrens = null` was **annotated, not removed**: the four-branch `if`/`else` still stands, so the latent null is still latent. Replace with a `(xAxis, min) switch` key selector and the null stops existing. `siblingOverlap`/`cumulativeOverlap` are written and never read (and one reads the enclosing node's field, so both candidates get identical values). **P-40:** all-leaves fallback + `RecomputeMBR`'s four `Children.Min/Max` enumerations per level per insert. **P-46:** leaf acceptance is unconditional. **P-50:** `getIsEndNode` classifies on `Children[0]` while `getChildrenContainingInd` dereferences across all of them — the two methods disagree about the same invariant. | partial |
| P-47 | No `Query(BoundingBox)`, no k-NN, no `Count`, no way to get feature indices out. `findByXY` hand-writes `new BoundingBox(x,y,x,y)` where `Point(x,y)` exists. Resolve §0's canonical-traversal question **before** wrapping either, or the public `Query` inherits the ambiguity. Decide now whether an `Empty`/non-finite *query* box throws or returns empty — recommend empty; a caller can fix a feature but not a query. `Findable` in `RTreeTests` is already an independent rectangular-membership oracle with a negative control; it is the ready-made check. | none |
| P-48.2, T-23 | `FeatureIndicesAt` calls `BoundingBox.Overlaps` — the predicate the traversal under test calls — so `BulkInsertAllFeaturesFindableByPoint` (500 features) **cannot detect a wrong `Overlaps` at all**. Deleting `getMBRoverlap` removed the second opinion that made it cross-checking. Work is "convert its two callers to `Findable`, delete the self-referential version", not "invent an oracle". Also open: no MBR-propagation test, no min/max test, no test that a node's box matches its features. | — |
| P-68, T-17 | `SpatialWriter.MapFieldType`'s `LongFT => OFTInteger64` and `SetOgrField`'s `case long l:` have **never had a test**, through several PRs one of which edited `Write`'s signature and feature loop directly. `FieldTypeTests`' docstring still claims writer coverage the file doesn't have. T-17: assert the **declared OGR type** *and* the **boxed CLR type** — a value-only assertion cannot fail here, because a `LongFT` column authored as `OFTString` round-trips `4000000000` perfectly through `Convert.ChangeType` while the schema lies. | — |
| T-7 | `DistanceFeatureToFeature` has **no distance assertion in any assembly** — which is precisely how P-56 survived, and why two wrong variants of its replacement were green. Same two squares, authored open and authored closed: assert `DistanceFeatureToFeature` agrees, and assert `NearestPolygonsToPoints` picks the same winner all four ways. Fails today only if the ring fix regresses — write it *before* the next edit to that loop, not after. | none |
| T-5, T-14 | **Highest value per line in the file.** T-5: exterior+hole through a real shapefile, assert `IsHole` on ring 1 and `Area == exterior − hole` — the only guard for a live production behaviour with zero read-path coverage. T-14: read lon/lat into a projected CRS, assert X is still longitude — the sole remaining transform path, and the deleted P/Invoke twin ignored axis order. | — |

## 4. Joins, model, IO

| P | What's wrong | Guard |
|---|---|---|
| P-02, P-26, P-25, P-44 | Both join directions end in `_ = pointTree ?? BuildTree(points);` — a whole R-tree built and dropped; candidate selection is a full O(polygons × points) scan. The comment now says so honestly and names this row, but the cost is unfixed: an MBR genuinely can't bound distance-to-segment, so the decision is *sound radial prefilter or delete the tree and the public parameter*. Also in here: planar distance regardless of `Crs.Kind`; the two directions copy-paste the schema backfill and disagree about ties (`1e-9` in CRS units — ~0.1 nm in metres, ~0.1 mm in degrees); `interiorOnly` is accepted and never read while `exteriorOnly` is; `ContainsIndex(i, c) => c == i` is pure indirection; `Feature.Wkt` is stale state; aggregations have no output column; and the `NearestPolygonsToPoints` docstring reads `soint, attach…`. | none |
| P-17, P-38, N-10 | `Feature.Parts` and `Features.FeatureSet` are public mutable `List`s, while both types also expose `Count`/`this[]` forwarding — a read-only *appearance* with a mutable *escape hatch*. `Parts.Clear()` leaves a stale `BoundingBox`; mutating `FeatureSet` bypasses `AddFeature`'s id assignment **and** `Crs`'s invalidation sweep, whose damage is invisible (stale metrics, not a stale count). `Part` already has the right shape — private list, `IReadOnlyList` facade, one mutator. Do both classes in one change, and let it also pick a readable name to replace `FeatureSet`. `Feature.ShapeType` vs `Features.ShapeType` are two free-to-disagree sources of truth. | none |
| P-18 | `RemoveFeature` renumbers survivors and leaves the detached feature's `Owner` pointing at the collection, so it keeps resolving the old CRS — currently rescued only by `Part`'s `CrsInfo` reference-identity check in a different class. `BuildTree` keys on `f.Id`, which mutates under removal, so a pre-existing index silently returns the wrong feature. | pinned as broken |
| P-65 | The `FieldType`↔OGR↔CLR mapping lives in **six** hand-maintained places (`SpatialReader.MapFieldType`, `SpatialWriter.MapFieldType`, `ReadFieldValue`, `AttributeColumn.FieldTypeToType`, `AttributeColumn.Coerce`, and `CoerceRow`, which decides *which* values get coerced while `Coerce` decides *how* — two of the six in one file, mutually unaware). P-06 happened because `LongFT` was missing from three of them. One internal mapper, both directions adjacent, no public change. **Sequence after T-17** — a consolidation with one direction tested silently breaks the untested one, which is exactly how P-01 got reopened. | — |
| P-08, P-27, P-28 | CSV values containing CR/LF corrupt the round trip (quote or reject). DBF schema order differs after round trip. Writer's convention is "null stays null", reader returns `""` for a null text field, and `Aggregate`'s `.Where(v => v is not null)` converts unknown→zero a third time — fix the reader half knowing that line will quietly disagree with it. Model-layer null semantics are decided and tested; don't re-litigate them. | partial |
| P-12 | `Ogr.RegisterAll()` on every `Read` **and** every `Write`, not idempotent-safe. `Io.Tests` disables parallelisation as a workaround and names this as the real fix. The culture-mutating test lives in the *other* assembly, which has no such guard — it is safe only because it is synchronous, and nothing says so in the file. | none |

## 5. Hygiene (behaviour-preserving; do while the file is open)

| P | What's left |
|---|---|
| P-22, P-60, P-63, P-33 | **`addFeatureChild` is unreachable and misleading** — it holds an area tie-break the live path lacks, so a reader assumes the tie-break is active, and it ends in `bestCandidate!.addFeatureChild(…)` asking the compiler to stop asking. Delete it rather than annotating it. `siblingOverlap`/`cumulativeOverlap` dead. Three spellings of one non-word (`Childs`/`Chidrens`/`Chlidren`), none of them `Children`; the last rename corrected a typo into a different typo — finish it as `MaxChildren`/`MinChildren`, it's internal. **`BuildTree`'s docstring is now the file's last stale prose**: it documents an `addFeature` signature deleted in `a6f3def`, asserts an argument order wrong since `04118f4`, and reads `the collection'sMBRs` — and the call it describes is `addFeature(new[] { f.Id, 0 }, f.BoundingBox)`. `SpatialWriter`'s degenerate-ring guard stated twice with identical conditions. `SpatialWriter.SetOgrField`'s `P0-4` comment means changelog item 4, not P-04. No `using` alias blocks in Io — bare `Geometry` → `CS0118` (enclosing namespaces beat file-level usings) while bare `Feature` binds to *OGR's*; that style is load-bearing, not accidental. `PartType` sits a namespace away from its only consumer. `gdal` → `GDAL`. |
| P-30, P-31, P-32, P-34, P-35 | Path to `TreatWarningsAsErrors=true`, in order that makes each a decision not an annotation: delete `addFeatureChild`, P-42's key selector, `RTreeNode? bestCandidate` — then a `WarningsNotAsErrors` for `CS8600` in **both** GDAL-referencing csprojs (prefer it to `NoWarn` so our own `CS8600`s stay visible; the package's generated `GdalConfiguration.cs:60` is compiled into both projects, which is P-61's evidence). Analysis covers test assemblies too. README's remaining errors; state *why* warnings aren't errors under Conventions, or the next contributor flips the flag. Node 20 deprecation on all four actions. Test projects `IsPackable`. `release.yml` pins `@main`. **CI's "Test (non-Gdal)" step has no `--filter`, so it runs the solution and then `Io.Tests` again — one flag (`--filter "Category!=Gdal"`).** CI is `-c Release`, local is Debug, so a Release-only failure has no local repro command. |
| P-61 | `Nsi.Geospatial.Reprojection` is one class in a project: a solution node, a `ProjectReference`, a CI `--include` target, and a second full copy of the GDAL package (restore, native runtime, generated-file warnings). The "keeps core GDAL-free" rationale is already satisfied by `Io`. Cheapest structural deletion here. |
| P-36, P-53 | Two namespaces in one test assembly (fossil of an abandoned `Nsi.Geospatial.Core` rename), which is *why* a 246-line duplicate suite went unnoticed — the classes couldn't collide. Three `Rel` implementations, two of them unable to compare against zero (`diff <= \|expected\|*tol` becomes `diff <= 0`, and the message divides by zero); adopt `RelD`'s `Math.Max(\|expected\|*tol, absTol)` with `absTol` chosen per unit. Duplicate `Cell`/`IntoFeature`/`TempDir`/`PointInPolygon`; golden constants duplicated across assemblies, so changing `EarthRadiusAuthalicMeters` breaks a project that doesn't reference the file you edited. `GeometryTests` is entirely `BoundingBox` — rename before the next member lands. A shared `TestFeatures.With(...)` would have turned PR #12's ~88 lines of rename churn into one file, and would make T-7 a two-line test. |

## 6. Breaking changes before 0.1.x — write the changelog note once

`Reprojector` removal (public static, P/Invoke path returned transposed coordinates for geographic CRS) · `getArea`/`getPerimeter` → `Area`/`Perimeter` · `FeatureCollection` → `Features` **and** `.Features` → `.FeatureSet` (the second was compiler-forced: a member can't share its type's name — and it's the one that touched every call site) · `SpatialWriter.Write`'s parameter `collection` → `features`, which breaks named-argument callers against the concrete class only · `Reprojection` namespace, when P-61 lands.

## 7. Work order

1. **Land the open ring branch with T-7 in the same commit.** It carries the closing-edge fix and
   the `CA1725` fix; without an assertion on the returned distance it has the same no-test history
   as the bug it replaced. Fold in the `Count == 0` arm and `BuildTree`'s docstring while the file
   is open.
2. **P-70 / D-G**, before anything else lands: it is the only item that gets more expensive per
   commit. Decide, grep, list §6.
3. **T-23** — several R-tree-adjacent PRs have now passed without fixing the oracle.
4. **P-68 + T-17**, then **P-65**. Both directions pinned before the consolidation.
5. **P-53 + P-36** — before any zero-assertion test lands, or they demand bit-exactness.
6. **T-5**, then **T-14**.
7. **P-21 + D-F** together with the box-model row — fixing the silent drop without the policy
   converts a silent drop into a crash with a message about bounding boxes.
8. **P-17 + T-25**, then P-18, then P-09 and P-10.
9. **P-15 + P-39 + P-62** as one box-model change → P-43 → P-42/P-40/P-46/P-50 → P-47 → **P-41
   last** → P-02/P-44.
10. P-04 and P-03 — the last two skips. **The skip budget is exactly two**; a third skip is a
    defect being hidden.
11. P-61, P-23a, P-26, hygiene while those files are open.

## 8. Gradients worth running, not inferring

```bash
rm -rf **/obj **/bin && dotnet build Geospatial.slnx -c Release   # read the output, not CI
grep -rn "Skip *=" tests/                                        # settles the skip budget
grep -rn "FeatureCollection\|collectionWkt\|soint" --include=*.cs .   # residue in comments and locals
grep -rn "getArea\|getPerimeter\|getMBRoverlap" --include=*.cs .
grep -rn "addFeatureChild\b" --include=*.cs .
grep -rn "Vertices\[i + 1\]\|Count - 1; i++" --include=*.cs .     # pair-walks that must wrap on a ring
grep -rn "double.MaxValue" Nsi.Geospatial/Spatial/                # sentinels that can enter a min
grep -rn "P0-4\|intentionally left unchanged" --include=*.cs .
grep -n "void Write" Nsi.Geospatial.Io/SpatialWriter.cs Nsi.Geospatial.Io/IFeatureSink.cs
git diff -w <base> <head> -- Nsi.Geospatial.Io/SpatialWriter.cs   # format churn vs real change