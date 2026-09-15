# Nsi.Geospatial

Geospatial model, spatial index, and IO library for HEC / USACE-NSI workflows.

`Nsi.Geospatial` is the pure, dependency-free geometry and attribute model.
`Nsi.Geospatial.Io` puts GDAL/OGR behind it so real shapefiles, GeoPackages, and
GeoJSON can be read and written. Nothing in the core model knows that GDAL exists.

## Layout

| Project | Depends on GDAL | What it is |
| --- | --- | --- |
| `Nsi.Geospatial` | No | Geometry (`Features`/`Feature`/`Part`/`Vertex`), attributes (`AttributeTable`), `BoundingBox`, the R-tree (`Spatial/RTreeManager`), and the join operators (`Spatial/SpatialJoins`). Zero external dependencies; builds and tests anywhere. |
| `Nsi.Geospatial.Reprojection` | Yes | OSR/PROJ-backed coordinate transforms (`CoordinateTransformer`). |
| `Nsi.Geospatial.Io` | Yes | `SpatialReader` / `SpatialWriter` behind `IFeatureSource` / `IFeatureSink`, `CrsInspector`, `CsvHelper`. |
| `tests/Nsi.Geospatial.Tests` | No | Core unit tests. |
| `tests/Nsi.Geospatial.Io.Tests` | Yes | Round-trip tests that create and read real OGR datasets. Tagged `Category=Gdal`. |

The solution is `Geospatial.slnx`. Packaged (published) projects are
`Nsi.Geospatial`, `Nsi.Geospatial.Io`, and `Nsi.Geospatial.Reprojection`;
the test projects set `IsPackable=false`.

> **Note on the old README.** It described a project named `Nsi.Geospatial.Core`.
> There is no such project — the dependency-free library is `Nsi.Geospatial`,
> and its root namespace is `Nsi.Geospatial` (`Nsi.Geospatial.Geometry`,
> `Nsi.Geospatial.Attributes`, `Nsi.Geospatial.Spatial`, …).

## Quickstart

### Install

The packages are published to the `USACE-NSI` GitHub Packages feed, not to
nuget.org, so add the feed once:

```powershell
dotnet nuget add source https://nuget.pkg.github.com/USACE-NSI/index.json `
  --name github --username <your-github-user> --password <pat-with-read:packages>
```

Then reference `Nsi.Geospatial` (always) plus `Nsi.Geospatial.Io` (only if you
touch files). `Nsi.Geospatial.Io` brings the `gdal` 3.11.3 managed assemblies with
it; the **native** GDAL runtime is still yours to install — see
[Building locally](#building-locally).

### Open two datasets in different OGR drivers, spatially join them, write a third driver

The reader picks the driver from the file itself, so a GeoPackage and a shapefile
are opened by the same call. The writer takes the driver name explicitly.

```csharp
using System.IO;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using Nsi.Geospatial.Projections;
using Nsi.Geospatial.Spatial;

// 1. Two inputs, two OGR drivers. GDAL selects the driver from the file, so
//    there is no driver argument on the read side.
//
//    ReprojectTo runs once per read, inside the reader, and makes the returned
//    collection's Crs describe the TARGET. Do it here rather than later: the join
//    below measures distance, and distance in degrees is not a distance.
//    RequireInspectableCrs turns "no usable CRS" into an exception instead of a
//    collection whose Area/Perimeter/Centroid are quietly null.
var polygonOptions = new SpatialReaderOptions
{
    ReprojectTo = Projection.AlbersUsa,
    RequireInspectableCrs = true,
};
var pointOptions = new SpatialReaderOptions
{
    ReprojectTo = Projection.AlbersUsa,
    RequireInspectableCrs = true,
};

Features basins = new SpatialReader(polygonOptions).Read("data/huc12.gpkg"); // GPKG driver
Features gages = new SpatialReader(pointOptions).Read("data/gages.shp"); // ESRI Shapefile driver

Console.WriteLine($"{basins.Count} basins ({basins.Crs}), {gages.Count} gages ({gages.Crs})");

// 2. Spatial join. The result is written INTO `basins`: new schema columns plus a
//    value on every matched polygon. It returns the indices of the polygons that
//    matched at least one point; unmatched polygons keep no value for the field.
//
//    destFields and sourceFields are paired positionally AND matched by name:
//    a destination field is only added to the output schema when its name also
//    appears in sourceFields. The writer emits schema columns only, so a
//    destination named something other than its source is computed, attached to
//    the feature, and then silently dropped on write. Keep the names identical.
List<long> matched = SpatialJoins.NearestPointsToPolygons(
    basins,
    gages,
    destFields: ["RUNOFF"],
    sourceFields: ["RUNOFF"],
    joinType: JoinType.Sum
);

Console.WriteLine($"{matched.Count} of {basins.Count} basins matched a gauge.");

// 3. Write the joined collection with a third OGR driver.
string outPath = "output/joined.geojson";
if (File.Exists(outPath))
{
    // SpatialWriter deletes stale sidecars for ESRI Shapefile only; for every
    // other driver, clear the previous output yourself.
    File.Delete(outPath);
}

new SpatialWriter().Write(basins, outPath, driverName: "GeoJSON");
```

The reverse direction (attach the nearest polygon's attributes to each point) is
`SpatialJoins.NearestPolygonsToPoints(points, polygons, destFields, sourceFields)`.

**Read this before trusting a join**

- **It is a nearest-neighbour join, not an intersection join.** For each polygon
  it finds the point(s) at minimum distance to the polygon's boundary and
  aggregates *only those tied-nearest points*. It does not select the points
  inside the polygon, so `JoinType.Count` is the number of tied nearest points
  with a non-null value, not a count of points in the polygon.
- **Cost is O(polygons x points).** `NearestPointsToPolygons` accepts an
  `RTreeManager` and builds one when you pass `null`, but the tree is not
  queried — an MBR cannot bound distance-to-segment, so candidate selection is a
  full scan. A source comment in `SpatialJoins.cs` records this as an open
  question (it refers to `Issues.md` P-02 / D-E).
- **Distance needs a real bounding box.** Distances are taken from
  `Feature.BoundingBox`. `SpatialReader` computes it for you; for features you
  build by hand, call `ComputeBoundingBox()` before joining.
- **The inputs are mutated.** `basins` gains columns and attribute values in
  place. If you need the original, copy it first.
- **`exteriorOnly` / `interiorOnly` are not what their names suggest**, and
  `interiorOnly` is currently ignored. Leave them unset.
- **GeoJSON declares CRS84.** Its spec fixes the coordinate reference system to
  WGS84 longitude/latitude, and GDAL writes the coordinates you hand it without
  reprojecting. Emitting Albers metres into `.geojson` therefore produces a file
  whose declared CRS disagrees with its numbers. For projected output write
  `ESRI Shapefile` (a `.prj` sidecar carries the CRS) or `GPKG` (a real CRS
  column); or reproject to `Projection.Wgs84` before writing GeoJSON.

### Driver notes for `SpatialWriter.Write(..., driverName)`

| driverName | Geometry supported here | CRS handling | Notes |
| --- | --- | --- | --- |
| `ESRI Shapefile` | Point, PointM, Line, Polygon | `.prj` from `Features.Crs.Wkt` | All four sidecars (`.shp/.shx/.dbf/.prj`) are deleted up front; a locked sidecar throws `IOException` instead of leaving a half-written dataset. Field names are truncated to 10 characters by the format. |
| `GPKG` | Point, PointM, Line, Polygon | Stored per layer | Best default for multi-layer or projected output. |
| `GeoJSON` | Point, Line, Polygon | Spec forces CRS84 | Single layer per file; see the warning above. |
| `CSV`, `MapInfo File`, … | As supported by OGR | Driver-specific | Geometry for anything outside the four shape types above is written as null. |

`IFeatureSink.Write` declares `"ESRI Shapefile"` as the default `driverName`, so
callers behind the interface get shapefiles unless they pass a driver explicitly.

### Reading and writing through the interfaces

```csharp
IFeatureSource source = new SpatialReader(new SpatialReaderOptions { LayerIndex = 1 });
IFeatureSink sink = new SpatialWriter();

Features layer2 = source.Read("data/city_basins.gpkg"); // layer 1 of the GeoPackage
sink.Write(layer2, "out/layer2.shp"); // default driver: ESRI Shapefile
```

`SpatialReaderOptions` has exactly three settings: `LayerIndex` (default 0),
`ReprojectTo` (default `null` = coordinates exactly as stored), and
`RequireInspectableCrs` (default `false`).

### Two constraints worth knowing up front

- `SpatialReader.Read` loads the entire layer into memory and offers no
  attribute filter, spatial filter, or streaming. Very large datasets need
  something else.
- For polygons, **the writer classifies rings by position, not by the `IsHole`
  flag**: `Parts[0]` becomes the exterior ring and every later part becomes a
  hole. A feature holding two separate shells therefore comes back with the
  second shell as a hole. `Feature.AreaSquareMeters`, by contrast, reads the
  `IsHole` flag. Keep one shell per feature.

## Building locally

### Using the dev container

Open the folder in the `Dev Containers` extension and it does everything for you:
`.devcontainer/Dockerfile` installs GDAL 3.11 plus the SWIG C# wrapper DSOs into
`/opt/gdal` and exports `LD_LIBRARY_PATH`, `GDAL_DATA`, `PROJ_DATA`, `PROJ_LIB`,
and `DOTNET_ROLL_FORWARD`. Nothing below is needed. The steps that follow are for
building **without** the container, in Visual Studio.

### Prerequisites

1. **Git**, and a working .NET SDK (below).
2. **Visual Studio 2022 17.14 or newer**, with the *.NET desktop development*
   (or *.NET cross-platform development*) workload.

   The version matters: this repository's solution file is `Geospatial.slnx`, the
   XML solution format. `.slnx` became a stable, generally available feature in
   Visual Studio 17.14. On 17.13 and earlier the IDE reads `.slnx` only when
   **Tools → Options → Preview Features → Use Solution File Persistence Model** is
   enabled, and otherwise will not load the solution at all.
3. **.NET SDK 9.0.200 or later.** `global.json` pins `9.0.100` with
   `"rollForward": "latestFeature"`, so the SDK resolver automatically selects the
   highest feature band you have installed within 9.0.x — and `.slnx` support
   landed in the 9.0.200 band. A machine with only a 9.0.1xx band installed cannot
   parse `Geospatial.slnx` and every solution-level command fails. Check with:

   ```powershell
   dotnet --version
   dotnet --list-sdks
   ```
4. **The .NET 8 runtime or targeting pack.** Every project targets `net8.0`
   (`Directory.Build.props`), even though the SDK is 9.x.

### GDAL native runtime (only for `Io` and `Reprojection`)

`Nsi.Geospatial` and `tests/Nsi.Geospatial.Tests` have no GDAL dependency at all.
If you only need the model, skip this section and build the two projects directly.

The `gdal` 3.11.3 NuGet package referenced by `Nsi.Geospatial.Io` supplies the
**managed** `OSGeo.OGR` / `OSGeo.OSR` assemblies only. Those assemblies P/Invoke
into native code that the package does not carry, so you must install:

- the native GDAL library and its dependency chain (`gdal`, `proj`, `geos`, …);
- the SWIG wrapper binaries the bindings look for by name —
  `gdal_wrap`, `ogr_wrap`, `osr_wrap`, `gdalconst_wrap`
  (`.dll` on Windows, `.so` on Linux, `.dylib` on macOS).

On Linux and in CI the repository pins these with conda-forge
(`.github/gdal.yml`: `gdal=3.11` + `gdal-csharp`). The matching local install is
the least surprising route, because it is the same version pair the dev container
and CI use:

```powershell
# Miniforge / Miniconda, x64
mamba create -y -n gdal -c conda-forge gdal=3.11 gdal-csharp
mamba activate gdal
gdalinfo --version
```

An OSGeo4W Installer install works too. Whichever you pick, the four wrapper
binaries must be present — plain GDAL builds do not include them, and the
resulting failure looks like a missing-DLL error at the first
`SpatialReader.Read`, not a build error.

> The only GDAL recipe pinned in this repository is the conda-forge one used by
> the Linux dev container and CI. Whether the Windows `win-64` packages deliver
> usable `*_wrap.dll` binaries has **not** been verified in-repo; if your Windows
> run fails to load a wrapper, that gap is the first thing to check, and a
> Windows recipe contribution to this README is welcome.

### Environment variables

Set these as **User** or **System** environment variables and then restart
Visual Studio — it does not pick up environment changes on the fly. Let `%PFX%`
be your GDAL prefix (`mamba conda info --base` then `envs\gdal`, or
`C:\OSGeo4W`).

| Variable | Value | Why |
| --- | --- | --- |
| `PATH` | append `%PFX%\bin;%PFX%\Library\bin` | Where the loader finds `gdal`, `proj`, and the `*_wrap` binaries. The Linux equivalent, `LD_LIBRARY_PATH`, is what the dev container sets. |
| `GDAL_DATA` | `%PFX%\share\gdal` | GDAL data files: the `.prj`/projection epoch tables, shapefile `.prj` writing, `gdal` driver metadata. |
| `PROJ_LIB` | `%PFX%\share\proj` | `proj.db`, the EPSG/ESRI authority database. Without it, `CrsInspector` cannot resolve a CRS and `CoordinateTransformer` cannot build a transform. |
| `PROJ_DATA` | `%PFX%\share\proj` | PROJ 9.2+ renamed `PROJ_LIB`; the dev container and CI set **both**, so set both and version differences stop mattering. |
| `DOTNET_ROLL_FORWARD` | `Major` | Only if you have a newer runtime but no .NET 8 runtime installed. The dev container needs it (its image ships only .NET 10); a normal Visual Studio install does not. Leave unset otherwise. |

Verify from a **fresh** shell:

```powershell
gdalinfo --version                                  # GDAL 3.11.x
Get-ChildItem $env:PATH.Split(';') -Filter '*ogr_wrap*' -ErrorAction SilentlyContinue
dotnet test tests/Nsi.Geospatial.Io.Tests/Nsi.Geospatial.Io.Tests.csproj -c Release
```

If the last command's tests pass, the environment is right. Symptom map:

| Symptom | Cause |
| --- | --- |
| `DllNotFoundException` naming `ogr_wrap` / `gdal` | `PATH` is missing the wrapper directory, or your GDAL build has no C# wrappers. |
| `PROJ: ... proj.db ... not found`, or every CRS reads back as unknown | `PROJ_LIB`/`PROJ_DATA` unset or pointing at the wrong `share\proj`. |
| `Warning 1: EPSG:102003 is not a valid CRS code, but ESRI:102003 is` | Expected, printed once per run, **not** a failure. `Projection.AlbersUsa` carries an `EPSG:`-prefixed string for a code that lives in the ESRI registry. Do not chase it; the authority/token model is tracked separately in `Issues.md`. |
| `FileNotFoundException: Could not open: <path>` | The file is missing, or GDAL could not open it (bad path, unsupported driver, unreadable extension). |

### Build and test

```powershell
dotnet restore Geospatial.slnx
dotnet build Geospatial.slnx -c Release
dotnet test Geospatial.slnx -c Release
```

Always pass the solution explicitly. With both a `.sln` and a `.slnx` in the
folder, a bare `dotnet build` refuses to guess.

Incremental builds silently reuse previously compiled assemblies and can hide
warnings. For a verification build, clear the output directories first:

```powershell
Remove-Item -Recurse -Force Nsi.Geospatial*/obj, Nsi.Geospatial*/bin, tests/*/obj, tests/*/bin -ErrorAction SilentlyContinue
dotnet build Geospatial.slnx -c Release
```

**Working without GDAL installed.** Build and test the dependency-free half:

```powershell
dotnet build Nsi.Geospatial/Nsi.Geospatial.csproj -c Release
dotnet test tests/Nsi.Geospatial.Tests/Nsi.Geospatial.Tests.csproj -c Release
```

**In Visual Studio specifically.**

- Open `Geospatial.slnx`. `Nsi.Geospatial` and `Nsi.Geospatial.Tests` are enough
  to work on the model, the R-tree, and the joins; unload the two GDAL projects
  (right-click → Unload Project) if you have no native runtime.
- Run GDAL-backed tests from **Test Explorer**. They carry
  `[Trait("Category", "Gdal")]`, so filter on `Category=Gdal` to isolate them.
  Do not turn on assembly-level parallelism:
  `tests/Nsi.Geospatial.Io.Tests/AssemblyInfo.cs` sets
  `CollectionBehavior(DisableTestParallelization = true)` because
  `Ogr.RegisterAll()` runs on every read and write and is not safe to enter
  concurrently — two threads double-register the drivers and abort the process.
- To debug with F5 against a dataset, prefer setting the variables in the project
  rather than machine-wide: add an `environmentVariables` block to
  `Properties/launchSettings.json`, or set them in the test's run configuration.
- A `Release` build plus **Build → Rebuild Solution** is the closest equivalent
  to the clean build CI performs.

### Formatting

CI fails a pull request on formatting:

```powershell
dotnet format Geospatial.slnx --verify-no-changes --no-restore
```

`.editorconfig` is the contract: 2-space indent, CRLF, UTF-8, final newline,
file-scoped namespaces, `System` directives sorted first. The dev container also
installs CSharpier; the CI check runs `dotnet format`, so treat that command as
authoritative. Note that CI's format step names its directories explicitly
(`Nsi.Geospatial`, `Nsi.Geospatial.Io`, `Nsi.Geospatial.Reprojection`, `tests`),
so a new top-level project is not format-checked until it is added there.

## Conventions

- File-scoped namespaces, nullable reference types enabled,
  `TreatWarningsAsErrors=false` (warnings are still expected to be fixed),
  2-space indentation. Set globally in `Directory.Build.props`.
- No `Microsoft.VisualBasic` dependency.
- `Nsi.Geospatial` core must not gain a GDAL dependency. PROJ is reached only
  through `Nsi.Geospatial.Reprojection` and `Nsi.Geospatial.Io`; the core
  consumes the inspected result (`CrsInfo`) and never inspects anything.
- `Projection` is a *requested transform target*; `CrsInfo` is an *inspected fact
  about data already in hand*. Keep that distinction when adding API.
- `Issues.md` is the work tracker (P-numbered rows); `Issues_longform.md` holds
  the detailed write-ups. Read `Issues.md` before starting on a known defect.

## Testing

| Assembly | Needs GDAL | Contents |
| --- | --- | --- |
| `tests/Nsi.Geospatial.Tests` | No | `BoundingBox`, attributes, CRS and area metrics, R-tree, `SpatialJoinTests`, spherical metrics. |
| `tests/Nsi.Geospatial.Io.Tests` | Yes | Shapefile/GeoJSON round-trips, CRS inspection, field types, hole handling, coordinate transforms. `Category=Gdal`. |

Io tests create their own datasets in a temp directory and clean up afterwards, so
no fixture data is committed.

## License

MIT — see [LICENSE](LICENSE). Contributing? Read
[CONTRIBUTING.md](CONTRIBUTING.md) first.
