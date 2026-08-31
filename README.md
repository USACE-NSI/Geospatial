# Nsi.Geospatial
Geospatial model + spatial-index + IO library for HEC/USACE-NSI workflows.

## Layout
- **Nsi.Geospatial.Core** — pure geometry/attribute/spatial-index model. No GDAL. Unit-testable on any OS.
- **Nsi.Geospatial.Io** — GDAL-backed shapefile + CSV readers/writers behind `IFeatureSource`/`IFeatureSink`.
- **Nsi.Geospatial.Reprojection** — OSR-backed coordinate transforms.

## Build
`dotnet build` — Core and tests need only .NET 8. The Io/Reprojection projects need the
GDAL managed assemblies plus the native GDAL runtime on `PATH` (or `GDAL_DATA`/`PROJ_LIB` set).

## Conventions
File-scoped namespaces, nullable enabled, `TreatWarningsAsErrors`. No `Microsoft.VisualBasic`.