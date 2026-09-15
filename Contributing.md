# Contributing to Nsi.Geospatial

Thanks for taking the time. This library is used in USACE/HEC hydrologic
workflows, so the bar is "correct and explainable" ahead of "fast and clever".

Everything below is checked by CI or by a reviewer. The
[CI contract](#the-ci-contract-what-must-be-green) section is the short version
of what the machine will complain about; the rest is the reasoning.

## Before you start

1. **Read [`Issues.md`](Issues.md).** It is the work tracker: one row per known
   defect, cleanup, and open decision, each with a `P-##` identifier.
   `Issues_longform.md` holds the long write-ups behind them.
2. **Check for an existing row before opening a new issue or PR.** If your change
   corresponds to a row, say so in the PR title/body with that `P-##`.
3. **Do not assign yourself a new `P-##`.** Identifiers are issued by the
   maintainer when a row is created. A source comment or commit message that
   cites a `P-##` with no row in `Issues.md` will be rejected — an id that only
   exists in prose is not a tracked decision, and the next person cannot tell it
   apart from a real one.
4. **Open a question before a large change.** Anything that changes public API,
   CRS handling, or ring/hole semantics is a design conversation first. Those
   areas have deliberate, documented behaviour that looks like a bug.

## Getting set up

Follow **Building locally** in the [README](README.md). The short form:

- Visual Studio 2022 **17.14+**, or any editor plus a .NET SDK **9.0.200+**
  (the solution is `Geospatial.slnx`, which needs that band).
- `Nsi.Geospatial` and `tests/Nsi.Geospatial.Tests` need **no GDAL**. Most
  model, R-tree, and join work can be done without installing anything.
- `Nsi.Geospatial.Io` and `Nsi.Geospatial.Reprojection` need the native GDAL
  runtime plus the SWIG `*_wrap` binaries, with `GDAL_DATA`, `PROJ_LIB`, and
  `PROJ_DATA` set. Version pair used by the dev container and CI: conda-forge
  `gdal=3.11` + `gdal-csharp`, matching the `gdal` 3.11.3 NuGet package.

Then confirm you are on a baseline that works before you change anything:

```powershell
dotnet build Geospatial.slnx -c Release
dotnet test Geospatial.slnx -c Release
dotnet format Geospatial.slnx --verify-no-changes --no-restore
```

If any of those three fail on an unmodified checkout, **stop and report it**
rather than working around it. A broken baseline turns every later failure into
an argument about who caused it.

## Day-to-day workflow

1. Branch from `main`. Use a descriptive branch name; `P-05-hole-winding` beats
   `fix-stuff`.
2. Make the change **and** its test in the same commit, so the test is the
   evidence that the change does what the message says.
3. Keep one concern per pull request. A pull request that fixes a defect and
   renames three things cannot be reviewed as one unit, and the rename part
   usually has to be reverted.
4. Rebase or merge `main` before asking for review if CI has moved.
5. Open the pull request against `main`. Fill in: what was wrong, what fixes it,
   which `P-##` it closes, and **how you verified it** (paste the test output).

### Commit and PR titles

Use a conventional prefix and the tracker id when there is one:

```
fix(P-11b): unmeasurable polygons must not win a nearest join
test(P-56): guard ring authoring under both open and closed loops
docs: add GDAL environment variables for non-container builds
```

`release.yml` publishes on `v*.*.*` tags, so a title is not a version — but it is
the only history a reader of `git log` gets.

## The CI contract: what must be green

`.github/workflows/ci.yml` runs, in order, on every push to `main`/`master` and
every pull request:

| Step | Command | Notes |
| --- | --- | --- |
| Restore | `dotnet restore Geospatial.slnx -v:n` | |
| Build | `dotnet build Geospatial.slnx -c Release --no-restore` | |
| **Format** | `dotnet format Geospatial.slnx --verify-no-changes --no-restore --include Nsi.Geospatial Nsi.Geospatial.Io Nsi.Geospatial.Reprojection tests` | Fails the PR. Run it locally before pushing. |
| Test (non-GDAL) | `dotnet test Geospatial.slnx -c Release --no-restore` | |
| Test (GDAL) | `dotnet test tests/Nsi.Geospatial.Io.Tests/Nsi.Geospatial.Io.Tests.csproj -c Release --no-restore` | GDAL comes from `.github/gdal.yml` via `setup-micromamba`. |

Two things to notice:

- **The format step lists its directories explicitly.** If you add a new
  top-level project, add it to that list in the same PR, or your new code is not
  format-checked and will disagree with everyone else's formatter the moment
  someone touches it.
- **CI installs GDAL 3.11 from conda-forge.** If you bump the `gdal`
  `PackageReference`, bump `.github/gdal.yml` and the dev container's pin in the
  same PR. Those three pins drifting is the single most annoying class of
  "works on my machine" this repo can produce.

### Formatting

`.editorconfig` is the rule, not a suggestion: 2-space indent, CRLF, UTF-8,
trailing newline, file-scoped namespaces (`csharp_style_namespace_declarations`
= warning), `System` directives first, `var` where the type is apparent.

Run `dotnet format Geospatial.slnx` before pushing and keep formatting churn in
its own commit, never mixed into a behavioural change.

## Code style

- File-scoped namespaces; nullable reference types are on — do not silence a
  warning with `!` or `#nullable disable` without a comment saying why.
- `TreatWarningsAsErrors` is `false`, which means warnings are still expected to
  be fixed. Incremental builds reuse compiled assemblies and hide warnings, so
  verify with a clean build (delete `obj`/`bin`, or **Rebuild Solution**).
- **`Nsi.Geospatial` must not reference GDAL.** The core model stays testable
  with no native runtime. PROJ-dependent code belongs in
  `Nsi.Geospatial.Reprojection` or `Nsi.Geospatial.Io`, and the core consumes
  the *result* of inspection (`CrsInfo`) rather than inspecting anything itself.
- Keep the `Projection` / `CrsInfo` distinction: `Projection` is what you want
  to transform *to*; `CrsInfo` is a fact about data you already read. Adding a
  second way to name a CRS is how the authority-token problem came to exist.
- Deterministic disposal of OGR handles matters. Every `SpatialReference`,
  `Layer`, `DataSource`, and borrowed `Geometry` in `Nsi.Geospatial.Io` is
  accounted for; a new code path that obtains one must dispose it.
- Prefer explaining a non-obvious choice in a doc comment over a clever line.
  Several members document *why the answer is null* or *why the naive
  generalisation was rejected*; match that style.

## Testing

Tests are xUnit (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`).

| Put your test in… | When |
| --- | --- |
| `tests/Nsi.Geospatial.Tests` (namespace `Nsi.Geospatial.Tests`) | Pure model, geometry math, bounding boxes, R-tree, joins. No GDAL, must run anywhere. |
| `tests/Nsi.Geospatial.Io.Tests` (namespace `Nsi.Geospatial.Io.Tests`) | Anything that reads or writes a real dataset, or needs PROJ/OSR. Tagged `[Trait("Category", "Gdal")]`. |

Rules that come from mistakes already made here:

- **A test that cannot fail is not coverage.** If the code under test overwrites
  the value your assertion checks, the assertion is decoration. Before you claim
  a case is covered, ask what change to production code would make this test
  fail; if you cannot answer, it does not guard anything.
- **Watch for silent-zero results.** A `dotnet test --filter` that matches no
  tests, and a test project that fails to compile, both produce
  `failed: 0`. This has actually happened here: the summary read `total: 193,
  failed: 0` while an entire 26-test assembly was missing because it would not
  build. So check the **per-assembly** lines, not just the final summary:

  ```powershell
  dotnet test Geospatial.slnx -c Release 2>&1 | Select-String "test (net8.0 )?(succeeded|failed)"
  ```

  Two `succeeded` lines means both assemblies ran. One means a whole assembly is
  absent and `failed: 0` is a lie.
- **Do not parallelise GDAL tests.** `Ogr.RegisterAll()` is called on every read
  and write and is unsafe to enter concurrently — two threads double-register
  the drivers and abort the process. That is why
  `tests/Nsi.Geospatial.Io.Tests/AssemblyInfo.cs` sets
  `DisableTestParallelization = true`. Do not remove it without adding a
  registration guard in `Nsi.Geospatial.Io`.
- **Keep reference formulas in tests.** When a test computes an expected area or
  distance analytically, that arithmetic is the specification. It is not
  duplication to be factored out.
- **Name tests and files after the public subject they exercise**, and if a test
  reaches a member only through a wrapper, say so in a comment.
- **A test name or doc comment describing behaviour that no longer exists is a
  defect.** Fix it in the same change that changed the behaviour.
- Round-trip tests should **generate their own data** in a temp directory (see
  `SpatialIoTests`) rather than committing binary fixtures.

### Refactoring rules

- **Relocating or consolidating a helper means copying its definition verbatim** —
  same name, same parameter order, same semantics. Existing call sites are the
  contract. A move that requires editing call sites is not a cleanup, it is a
  behavioural change wearing a costume; one here cost 31 call-site edits.
- Copying a body into a new file while renaming a parameter can flip its
  meaning. If a helper takes `bool exterior` and its body computes
  `IsHole = !exterior`, re-declaring the parameter as `bool isHole` and keeping
  the `!` silently inverts every flag.
- Do not merge two same-named helpers that return different types.
- Public API removal or signature change is a breaking change to a **packaged**
  assembly. It needs an `Issues.md` row, a decision, and a note in the PR
  describing who downstream is affected.

## Behaviour that looks wrong and is not

Do not "fix" these without an `Issues.md` decision row. Each is pinned by a test
or a documented rationale:

- **`Feature.AreaSquareMeters` returns `null` rather than guessing.** Null is a
  real answer meaning "no unit is dependable" or "a contributing part has unknown
  area". It is not converted to `0`.
- **A feature's `Crs` comes from its owner** (`Owner?.Crs ?? CrsInfo.Unknown`),
  because the collection owns the CRS. `RemoveFeature` leaving a detached feature
  resolving the *old* CRS is pinned deliberately by
  `RemoveFeatureLeavesTheDetachedFeatureResolvingTheOldCrs`. Clearing `Owner` is
  a reviewed change, not a tidy-up.
- **`Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is`** is
  printed once per run and is not a failure. `Projection.AlbersUsa` carries an
  `EPSG:`-prefixed string for a code that actually lives in the ESRI registry;
  the authority/token model is tracked separately.
- **Ring classification on write is positional** (`Parts[0]` = exterior, the rest
  = holes) while `AreaSquareMeters` reads the `IsHole` flag. Both are known and
  intentional for now; changing either is a spec decision, not a bug fix.
- **The R-tree stays.** Work in this area is about making it honest, not
  replacing it. `NearestPointsToPolygons` builds a tree it does not query
  because an MBR cannot bound distance-to-segment; the source comment says so.

## Documentation

- Public types and members get XML doc comments. `///` summaries describe
  behaviour and its limits; `<remarks>` is where "why, and what the naive
  alternative does" belongs.
- Parameter names are documentation. If a parameter is misleading, annotate it
  rather than renaming it and breaking call sites.
- New behaviour in `Nsi.Geospatial.Io` that a caller could trip over — driver
  support, sidecar cleanup, CRS quirks, field-name truncation — belongs in the
  README's driver table, not only in a code comment.
- Update the README's **Layout** and **Testing** tables when you add a project,
  and the CI format-step directory list with it.
- Do not put a `P-##` in a doc comment before that id has a row in `Issues.md`.

## Review

What a reviewer will look for, in order:

1. Does the change actually fix the stated problem, and is there a test that
   fails without it?
2. Did anything outside the intended scope change — call sites, helper
   signatures, parameter order, ring or CRS semantics?
3. Does it respect the GDAL boundary into `Nsi.Geospatial` core?
4. Are OGR handles disposed on every path, including the throw paths?
5. Does `dotnet format` pass, and did both test assemblies run?
6. Is the docs/README impact handled?

Review is by a maintainer with commit access. Do not merge your own pull request.
Ask for review on anything touching public API, CRS handling, ring/hole
semantics, or the GDAL pins.

## Releases

Releases are tag-driven and you should not need to touch them: pushing a `v*.*.*`
tag runs `.github/workflows/release.yml`, which publishes `Nsi.Geospatial`,
`Nsi.Geospatial.Io`, and `Nsi.Geospatial.Reprojection` to the `USACE-NSI` GitHub
Packages feed using the shared
`HydrologicEngineeringCenter/dotnet-workflows` release workflow. Test projects set
`IsPackable=false` and are never published. Package metadata (version `0.1.0`,
authors, MIT, README as the package README) comes from `Directory.Build.props`.

## Getting help

Open an issue for anything you would otherwise guess at. If you are fighting the
GDAL environment rather than the code, say so in the issue and include the output
of `gdalinfo --version` and your `GDAL_DATA` / `PROJ_LIB` / `PROJ_DATA` values —
that turns a long exchange into one reply.
