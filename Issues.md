# Issues — open work

Open defects only; closed rows are **deleted** — recover with `git log --grep 'fix(P-xx)'`. Numbers are never reused or renumbered; merged rows keep every number they absorbed. Highest number ever issued: **P-71** (`Part` measure-on-demand, closed by #26). An earlier provisional P-71/P-72 became P-02/P-43, so `--grep P-71` returns two unrelated changes. Reserved — cited by source, never written up: P-20, P-48, P-54, P-55, P-59, T-23.

**Rules**
- **Warnings are the maintainer's call** — options named, never a blocker, never grounds for a public API change. Fix, suppress or ignore is yours.
- `Guard` is the only evidence field; `none` means a fix could land and revert green. **A behaviour change with no test asserting the observable result is not closed.** If the guard is hard to write, the method's visibility is the bug (P-02).
- **`dotnet test`'s case total is the diff that matters** (CI prints none) — but it only proves nothing was *lost*: #26's migration held the total while two tests went red. A build that recompiles nothing prints no warnings; clean-build before reading either.
- **Assert the externally determined answer.** Never pin a value §0 leaves open. No commented-out guards. **A test's data is part of its claim**, and a guard whose precondition lives in a fixture can be retired by a fixture cleanup — build the state in the test.
- Deleting dead code beats annotating it; causing a defect to disappear beats testing around it. A sentinel you must compare against to detect is the defect; so is a comment that contradicts the line under it.
- A row over ~6 lines is a design question: `CHANGES_*.md`, link it, keep the row. No CI state, no annotation counts, no "verified at `<sha>`".

## 0. Decisions that block work

| ID | Question | Blocks |
|---|---|---|
| D-E | May the R-tree index a geographic CRS? Its MBR math is planar; a degree-space box is not a metric box. | P-02, P-44 |
| D-F | Filter-and-count, or refuse, when input can't be placed? **Unforced today**, so it gets decided by accident on first real data — and three sites in `SpatialJoins` already answer "silently skip" (zero-vertex part, partless polygon, schema backfill that runs regardless). Right under "filter", wrong under "refuse", none says which. | P-21, P-64, P-69 |
| — | Which traversal is canonical: `getCandidateEndNodesByMBR` (returns end-nodes) or `getCandidateFeatNodesByMBR` (tests children, returns the parent)? `findByXY` uses the latter, `getEndNodes` the former. One datum: it descends near the leaves, so over-reporting is small and a public `Query` can afford exactness. | P-47 |

## 1. Wrong answers today

| P | What's wrong | Where | Guard |
|---|---|---|---|
| P-03 | `SphericalPointToSegmentDistance` can't detect a foot behind `a` (the `acos` term is non-negative by construction): 1000.8 m where truth is 1057.2. Fix — reject when `cos(bearing(a,p)-bearing(a,b)) < 0`, return `R*delta13`. | `GeometryMath` | skipped |
| P-04 | `EarthRadiusFeet = 20925524.9` is **equatorial**; the docstring says authalic and `SphericalArea` squares it. Authalic-in-feet is `20902254.53`. | constants | skipped |
| P-05 | `AreaSquareMeters` treats `Parts[0]` as exterior unconditionally, so a hole-first feature reports positive area; and `- hole.AreaSquareMeters ?? 0` coalesces a null `Part`'s docstring forbids. No test covers `IsHole` on the read path. | `Feature` | none |
| P-41 | `getAddedSizeToAccomodate` returns set-union area `A + feat − \|A∩B\|`, not MBR growth (`[0,10]²` vs `[20,30]²`: 200, truth 800), which for disjoint features reduces to "smallest child wins". One-line fix `BoundingBox.EnlargementToContain(bbox)` — **only after the box-model row**, since `Empty.EnlargementToContain(box)` is `−∞` and sorts first. | `RTreeNode` | goldens on the member, none on child choice (T-20) |
| P-10, P-19 | `JoinType.Average`/`Sum` return **0 for an all-null column** — a fabrication, should be null. `ToDouble`'s string fallback calls `double.TryParse` with **no `InvariantCulture`**, so aggregation is host-locale dependent. The point→polygon direction has no aggregation path, so these arms are reachable one way only. | `SpatialJoins` | none |
| P-09 | `AttributeTable` is schema-only, so the old "loses data" mechanism became a worse one: `RenameColumn`/`RemoveField` never touch `Feature.Attributes`, so after a rename the schema says `COUNTY`, features still key `FIPS`, and `CoerceRow` (schema-known keys only) **drops the values silently on write**. Second route: both joins backfill the schema only when the dest field appears in `sourceFields`, so a mismatched pair writes `Attributes[field]` for a column never added. **Third route, the writer half:** `SpatialWriter.Write` creates fields from `Schema` but writes values from `foreach (var kv in feat.Attributes) SetOgrField(of, kv.Key, kv.Value)` (`SpatialWriter.cs:82-85`), so the declared type and the type handed to `SetField` come from two unrelated sources and nothing on disk records the disagreement — which is also why no round-trip test can guard `SetOgrField`'s per-CLR arms. Since P-65 centralised coercion this is six lines (iterate `Schema.ColumnNames`, `c.Coerce(raw)`), but it changes four things: an `Attributes` key with no column stops being written, `TextFT` truncates to `Length` where a width exists, field order becomes schema order, and a value that fails to coerce becomes NULL. Check whether `feat.Attributes` is case-insensitive first — `AttributeTable`'s dictionary is `OrdinalIgnoreCase`, and if `Attributes` is ordinal a case difference silently writes NULL. Move all three or refuse; never half-move. | `AttributeTable`, `SpatialJoins`, `SpatialWriter` | none |
| P-23, P-23a | `GeometryMath` docstrings wrong ×4 (great-circle triangle −0.8491%; 65°N −0.6614%; pole-crossing ring 64.8× out), and the advice to *densify before measuring* makes it worse. Four copies of one walk loop; `Area` and `Centroid` are one shoelace and `Measure()` calls both, so every ring is walked twice for one sum. | `GeometryMath` | prose |

## 2. Blocked on a decision, then mechanical

| P | Decision, then the sites | Guard |
|---|---|---|
| P-21, P-64, P-69, D-F | **Refuse loudly, or filter and report?** Ten sites, one policy, one line each: `ProcessGeometry`'s missing `else` (drops `wkbMultiLineString`/`wkbPoint` → zero parts, which is what reaches the joins' silent skip); the joins' **schema backfill, which runs before any geometry is examined** — `PolygonWithNoPartsIsNotMatchedAndWritesNoAttributes` pins that asymmetry as current behaviour; `SpatialWriter`'s `Parts.Where(Vertices.Count > 0)`; `BuildTree`'s `!= Empty` skip; `CoerceRow`'s unknown-key drop; `FromVertices` skipping NaN via `if (x < minX)` (so `[(0,0),(NaN,NaN)]` gives a *plausible finite wrong* box, while the ctor's `Math.Min`/`Max` do propagate NaN — two NaN policies in one struct); writer accepting `long` > 2^53 (GeoJSON parses via `double`; DBF ceiling is width **18** — 20 *causes* the `OFTReal` demotion); `bool` written `"1"/"0"` into `OFTString` then rejected by `bool.TryParse`; `Aggregate`'s `_ => null`; and a join distance that overflows to `+∞` — not the `MaxValue` sentinel, so it escapes as measurable, loses every `<`, and the input vanishes unnamed. `addFeature`'s gate is the model: reject at the boundary, name the value. | none |
| P-39, P-62, P-15, D-E | **Box model.** `Empty` is the full-range sentinel, so `Area`/`Perimeter` are `+inf`, `ContainsPoint` is true for everything, and `EnlargementToContain` is asymmetric about `Empty` (`0` one way, `−∞` the other) while `Overlaps`/`Contains`/`Union`/`OverlappingArea` do guard — four unguarded members disagreeing with three siblings. Narrower than it looked: union propagation on finite boxes is guarded and green, so this is the sentinel and the public setters (`RTreeManager.Root`, `RTreeNode.BoundingBox`; `addChild` validates nothing). `RTreeTests`' `Covers`/`ContainsPoint` reimplement the members instead of calling them — delete them as part of this row. | goldens on the members, none on the sentinel |
| P-17, T-25, P-18 | Split `RTreeManager`'s insert path from its split path so the split is testable at all (T-25 is the seam, P-17 the dead `if` inside it), then P-18's node-count/`MaxChildren` off-by-one. **Sequenced before P-41**, whose golden is only meaningful once child choice is observable. | none |
| P-42, P-40, P-46, P-50 | Box over-growth narrowed to its one live symptom: `addChild` overwrites `child.Parent` without checking it already has one (T-23's exact-union test is green, so propagation isn't the problem). Rest of the row: the reinsert-and-redescend path has no test at node capacity. | T-23's union test |
| P-43, P-02 | `deleteFeature`/`deleteFeatureRec` — the visibility bug is why no guard exists: the interesting branch is unreachable from a public call that can construct the state. Make the node builder `internal` first, then write the guard. | none |
| P-47 | Public `Query`: decide traversal (§0), then it can afford exactness because the descent reaches near the leaves. | candidate-set equality |
| P-08 | `DateFT`: `CsharpType` and `Coerce` say `DateTime`, `SpatialReader` returns `string` — three places, one commit. `SingleFT`→`typeof(double)` is §6. | none |

## 3. Needs a guard

| P | Missing evidence | Guard |
|---|---|---|
| P-20, P-48, P-54, P-55, P-59 | Cited by source or by earlier rows, never written up. **P-54 is `Part`'s own comment** (`make CrsInfo`'s setters `init`) and it is what makes `Measure`'s reference-identity cache key sound — mutate a `CrsInfo` in place after a measurement and the cache is silently stale. | none |
| T-20 | No golden pins *which child* `getAddedSizeToAccomodate` picks, so P-41's fix can be inverted and stay green. | write with P-41 |
| P-65 residue | `FieldTypes`' `length > 0` guard (unknown width must not truncate) is a live behaviour change with **no test**. Needs `UnknownWidthDoesNotTruncate`; the two coercion test files should be one file. | none |

## 4. Known, accepted, documented

| ID | Accepted because |
|---|---|
| D-A | The R-tree stays. §2 and §3 are about making it honest, not replacing it. |
| D-B | Skip budget is exactly two (P-03, P-04), verified by name. A third skip is a decision, not a fix. |
| D-D | `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is` prints once per run and is not a failure. |
| — | `RemoveFeature` leaves a detached `Feature` resolving the **old** CRS. `RemoveFeatureLeavesTheDetachedFeatureResolvingTheOldCrs` pins it deliberately so clearing `Owner` has to be a reviewed change. |

## 5. Behaviour-preserving cleanups

| P | What | Guard |
|---|---|---|
| P-36, P-53 | **Closed by #26.** Eight test files share `Nsi.Geospatial.Tests`, so a duplicated test class is `CS0101` instead of an invisible twin. One `Tolerance.Rel`, one `TestFeatures`. **Two lessons the file records:** a shim is sometimes required, not lazy — bare `Rel(…)` resolves through the containing class, so deleting the private method deletes the name (58 errors); and same-named builders with **different return types** (`Projected`/`Geographic` returning `Features` vs `CrsInfo`) must not be "unified" — the `…Crs` suffix is load-bearing, because `Projected(double, LinearUnit)` asserts `Unit = Meter` while `ProjectedCrs(double)` leaves `Unit = Unknown`, and this file's subject *is* the `Unknown` path. **`Tolerance`'s `absTol` floor silently overrides tight relative tolerances:** it wins unless `expected ≥ 1e-9 / relTol`, so `1e-12` is decorative below 1000 — two sites pass `absTol: 0.0`, two still read `1e-12` while floored (2× and 11×, accepted). **`absTol` and `RelD` have no other callers.** Still open: `RTreeTests`' helpers (P-39's list), two coercion test files where one would do, `GeometryTests` is entirely `BoundingBox` — rename before the next member lands. | baseline held |
| P-61 | `GdalConfiguration.cs` is duplicated per project — one generated file, two outputs, and the source of all six `CS8600`s. | — |
| — | Prose sweep, while the file is open: `DistanceFeatureToFeature`'s doc, `BuildTree`'s dead-signature doc, `// Xmax=10, Xmin=0…`, `soint`, `FeatureCollection` prose, delete `addFeatureChild`, `RTreeTests`' `P-48` citation, `FieldTypes`' "six…seventh" contradiction, `SphericalMetricsTests`' class doc dating itself to `feature/spherical` (its *other* paragraph — expected values derived independently of the implementation — is the best in the repo and should be the model). | — |

## 6. Deferred by choice

| Item | Note |
|---|---|
| `SingleFT` → `typeof(double)` | `ClrType(SingleFT) => typeof(double)` is a **public** `CsharpType` change. `CoerceBoxesWhatTheColumnDeclares` asserts *agreement*, not truth — it passed when the declaration was moved to match the box — so it cannot settle whether `SingleFT` is real or should go. |
| `TreatWarningsAsErrors` | An **if**, not a when. `WarningsNotAsErrors` beats `NoWarn` if it happens. |
| `[CallerArgumentExpression]` on `Tolerance.Rel`'s `what` | Zero call-site edits, and failures print `metres.Area: expected 5000, got null` instead of `assertion:`. Displaces `Describe`'s default; nothing may then pass `what` positionally. |

## 7. Order of work

1. **Finish P-36**: `RTreeTests`' helpers (→ delete per P-39), merge the coercion test files + `UnknownWidthDoesNotTruncate`, `GeometryTests` → `BoundingBoxTests`. Each must hold §8's baseline.
2. **T-5**, then **T-14**.
3. **P-21 + D-F** (the ten-site row) — needs §0 answered first.
4. **P-17 + T-25** → **P-18** → **P-09** (incl. its writer half) → **P-10** → box-model bundle (P-15/P-39/P-62) → **P-43** → P-42/P-40/P-46/P-50 → **P-47** → **P-41 last** (`EnlargementToContain` is asymmetric about `Empty`: `0` / `−∞`) → P-02/P-44.
5. **P-08 (`DateFT`)** whenever `SpatialReader` is open anyway.

## 8. Commands

```bash
rm -rf tests/**/obj tests/**/bin Nsi.Geospatial*/obj Nsi.Geospatial*/bin && dotnet build Geospatial.slnx -c Release
dotnet test 2>&1 | tail -1     # BASELINE = 182 (180 pass, 2 skip: P-03, P-04). Behaviour-preserving work holds it; a new test moves it, say which.
grep -rn "^namespace" tests/   # one per assembly; two means P-53 is back
grep -rc "TestFeatures\." tests/Nsi.Geospatial.Tests/*.cs   # a member nobody prefixes is dead code
grep -rn "Skip *=" tests/      # budget is exactly 2 (P-03, P-04)
grep -n "Seal" tests/Nsi.Geospatial.Tests/TestFeatures.cs   # no commented-out calls
grep -n "foreach (var kv in feat.Attributes)" Nsi.Geospatial.Io/SpatialWriter.cs   # P-09's writer half
```