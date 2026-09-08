using Xunit;

// GDAL's Ogr.RegisterAll() is called by SpatialReader.Read and SpatialWriter.Write on
// every invocation and is not safe to enter concurrently: two threads double-register
// the plugin drivers and abort the process. GDAL-backed tests must therefore not run in
// parallel with each other. Remove this together with a registration guard in Io.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
