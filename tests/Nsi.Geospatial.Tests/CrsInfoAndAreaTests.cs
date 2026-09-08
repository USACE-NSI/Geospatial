using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Projections;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// CrsInfo, the Part -> Feature -> FeatureCollection CRS chain, and the named-unit
/// accessors. Core-only: no GDAL, so these run anywhere.
///
/// Contract under test: Part.Area/Perimeter stay in the CRS's native units (they are
/// correct planar math for the stored vertices); AreaSquareMeters/LengthMeters are the
/// only place a unit is asserted, and they return null rather than guessing.
/// </summary>
public class CrsInfoAndAreaTests
{
  private static CrsInfo Projected(double unitToMeters) =>
    new() { Kind = CrsKind.Projected, UnitToMeters = unitToMeters };

  private static readonly CrsInfo Geographic = new() { Kind = CrsKind.Geographic };

  /// <summary>
  /// Direction is set before the first AddVertex on purpose: AddVertex derives
  /// IsHole = !Direction only while the ring is still empty, so setting it later
  /// silently leaves IsHole wrong.
  /// </summary>
  private static Part Ring(params (double X, double Y)[] points) => Build(true, points);

  private static Part Hole(params (double X, double Y)[] points) => Build(false, points);

  private static Part Build(bool exterior, params (double X, double Y)[] points)
  {
    var part = new Part { Direction = exterior };
    foreach ((double x, double y) in points)
    {
      part.AddVertex(new Vertex(x, y));
    }
    part.CloseRing();
    return part;
  }

  private static Feature FeatureOf(params Part[] parts)
  {
    var feature = new Feature();
    foreach (Part part in parts)
    {
      feature.AddPart(part);
    }
    return feature;
  }

  private static FeatureCollection CollectionOf(CrsInfo crs, params Feature[] features)
  {
    var fc = new FeatureCollection { Crs = crs };
    foreach (Feature f in features)
    {
      fc.AddFeature(f);
    }
    return fc;
  }

  /// <summary>100 x 50 unit rectangle, CCW.</summary>
  private static (double X, double Y)[] Rect(double w = 100, double h = 50) =>
    [(0, 0), (w, 0), (w, h), (0, h)];

  private static void Rel(double expected, double actual, double tol = 1e-9)
  {
    double diff = Math.Abs(expected - actual);
    Assert.True(
      diff <= Math.Abs(expected) * tol,
      $"expected {expected:R}, actual {actual:R}, rel diff {diff / Math.Abs(expected):E}"
    );
  }

  // ----------------------------------------------------------- CrsInfo

  [Fact]
  public void UnknownIsTheZeroValue()
  {
    var fresh = new CrsInfo();

    Assert.Equal(CrsKind.Unknown, fresh.Kind);
    Assert.Equal(LinearUnit.Unknown, fresh.Unit);
    Assert.Equal(0.0, fresh.UnitToMeters);
    Assert.Null(fresh.Wkt);
    Assert.Null(fresh.EpsgCode);
    Assert.Equal(CrsKind.Unknown, CrsInfo.Unknown.Kind);
  }

  [Fact]
  public void UnitToMetersOrMeterDefaultsToMetreOnlyForProjected()
  {
    // A projected CRS whose unit GDAL reported but that matched no named unit
    // still converts: the double carries the scale, the enum is diagnostics.
    Assert.Equal(1.0, Projected(0).UnitToMetersOrMeter);
    Assert.Equal(0.3048, Projected(CrsInfo.MetersPerFoot).UnitToMetersOrMeter);
    Assert.Equal(
      CrsInfo.MetersPerUsSurveyFoot,
      Projected(CrsInfo.MetersPerUsSurveyFoot).UnitToMetersOrMeter,
      15
    );
  }

  [Fact]
  public void SurveyFootAndInternationalFootAreDifferentConstants()
  {
    // Guard against the two being collapsed to one value: the difference is tiny
    // (2e-6) and systematic, so it must stay visible.
    Assert.True(
      CrsInfo.MetersPerUsSurveyFoot != CrsInfo.MetersPerFoot,
      "US survey foot and international foot must not share a factor"
    );
    Rel(1200.0 / 3937.0, CrsInfo.MetersPerUsSurveyFoot, 1e-15);
    Assert.Equal(0.3048, CrsInfo.MetersPerFoot, 15);
  }

  // ------------------------------------------------------- owner chain

  [Fact]
  public void StandalonePartHasUnknownCrsAndNullNamedMetrics()
  {
    Part part = Ring(Rect());

    Assert.Equal(CrsKind.Unknown, part.Crs.Kind);
    Assert.Null(part.AreaSquareMeters);
    Assert.Null(part.LengthMeters);
  }

  [Fact]
  public void FeatureWithoutCollectionHasUnknownCrsAndNullArea()
  {
    Feature feature = FeatureOf(Ring(Rect()));

    Assert.Equal(CrsKind.Unknown, feature.Crs.Kind);
    Assert.Null(feature.AreaSquareMeters);
  }

  [Fact]
  public void AddFeatureWiresTheChainSoPartsResolveTheCollectionsCrs()
  {
    CrsInfo crs = Projected(1.0);
    Part part = Ring(Rect());
    Feature feature = FeatureOf(part);
    FeatureCollection fc = CollectionOf(crs, feature);

    Assert.Same(crs, fc.Crs);
    Assert.Same(crs, feature.Crs);
    Assert.Same(crs, part.Crs);
  }

  [Fact]
  public void AreaIsResolvedAfterTheChainIsBuiltPartFirstThenAddFeature()
  {
    // The chain is assembled after CloseRing has already run, so AreaSquareMeters
    // must resolve lazily rather than being cached at CloseRing time.
    Part part = Ring(Rect());
    Assert.Null(part.AreaSquareMeters);

    CollectionOf(Projected(1.0), FeatureOf(part));
    Rel(5000.0, part.AreaSquareMeters!.Value);
  }

  [Fact]
  public void RemoveFeatureLeavesTheDetachedFeatureResolvingTheOldCrs()
  {
    // Documents CURRENT behaviour: RemoveFeature renumbers ids but does not clear
    // Owner, so a detached feature still reports the collection's CRS. Deliberately
    // pinned so that clearing Owner becomes an intentional, reviewed change.
    Feature feature = FeatureOf(Ring(Rect()));
    var fc = CollectionOf(Projected(1.0), feature);

    fc.RemoveFeature(0);

    Assert.Same(fc.Crs, feature.Crs);
  }

  // -------------------------------------------- projected named units

  [Fact]
  public void ProjectedMetresAreaAndLengthConvertByUnitFactor()
  {
    Part part = Ring(Rect(100, 50));
    CollectionOf(Projected(1.0), FeatureOf(part));

    Rel(5000.0, part.AreaSquareMeters!.Value);
    Rel(300.0, part.LengthMeters!.Value);
  }

  [Fact]
  public void ProjectedFeetAreaConvertsByTheSquareOfTheFactor()
  {
    Part part = Ring(Rect(100, 50));
    CollectionOf(Projected(CrsInfo.MetersPerFoot), FeatureOf(part));

    Rel(5000.0 * 0.3048 * 0.3048, part.AreaSquareMeters!.Value, 1e-12);
    Rel(300.0 * 0.3048, part.LengthMeters!.Value, 1e-12);
  }

  [Fact]
  public void ProjectedUsSurveyFootDoesNotSilentlyUseTheInternationalFoot()
  {
    Part survey = Ring(Rect(100, 50));
    Part intl = Ring(Rect(100, 50));
    CollectionOf(Projected(CrsInfo.MetersPerUsSurveyFoot), FeatureOf(survey));
    CollectionOf(Projected(CrsInfo.MetersPerFoot), FeatureOf(intl));

    double ratio = survey.AreaSquareMeters!.Value / intl.AreaSquareMeters!.Value;

    // Area, not length, so the factors square. Derived from the literal
    // definitions rather than from CrsInfo's constants, so that a constant
    // drifted to the wrong value fails here instead of cancelling out.
    double expected = (1200.0 / 3937.0 / 0.3048) * (1200.0 / 3937.0 / 0.3048);
    Rel(expected, ratio, 1e-12);
    Assert.True(ratio > 1.000003 && ratio < 1.000005, $"unexpected ratio {ratio:E}");
  }

  [Fact]
  public void NativeAreaStaysInSourceUnitsRegardlessOfCrs()
  {
    // The design decision: Part.Area/Perimeter are honest planar math over the
    // stored vertices. They must not be quietly rescaled when a CRS is attached,
    // or the metric would disagree with the geometry it describes.
    Part metres = Ring(Rect());
    Part feet = Ring(Rect());
    CollectionOf(Projected(1.0), FeatureOf(metres));
    CollectionOf(Projected(CrsInfo.MetersPerFoot), FeatureOf(feet));

    Rel(5000.0, metres.Area);
    Rel(5000.0, feet.Area);
    Rel(300.0, metres.Perimeter);
    Rel(300.0, feet.Perimeter);

    // ...and the rescaled figures really do differ, proving the CRS reached the
    // named accessors rather than both parts sharing one CrsInfo.
    Assert.True(metres.AreaSquareMeters!.Value > feet.AreaSquareMeters!.Value);
  }

  [Fact]
  public void ProjectedWithUnrecognisedUnitStillConvertsViaUnitToMeters()
  {
    Part part = Ring(Rect());
    CollectionOf(new CrsInfo { Kind = CrsKind.Projected, UnitToMeters = 1.8288 }, FeatureOf(part));

    Assert.Equal(LinearUnit.Unknown, part.Crs.Unit);
    Rel(5000.0 * 1.8288 * 1.8288, part.AreaSquareMeters!.Value, 1e-12);
  }

  // --------------------------------------------------- holes and parts

  [Fact]
  public void FeatureAreaSubtractsPartsFlaggedAsHoles()
  {
    Part outer = Ring(Rect(100, 100));
    Part hole = Hole(Rect(20, 20));
    Feature feature = FeatureOf(outer, hole);
    CollectionOf(Projected(1.0), feature);

    Assert.True(hole.IsHole);
    Assert.False(outer.IsHole);
    Rel(10000.0 - 400.0, feature.AreaSquareMeters!.Value);
  }

  [Fact]
  public void FeatureAreaIgnoresNonHoleAdditionalParts()
  {
    // A second exterior part (multi-polygon) adds area; only IsHole subtracts.
    Part outer = Ring(Rect(100, 100));
    Part second = Ring((200, 200), (210, 200), (210, 210), (200, 210));
    Feature feature = FeatureOf(outer, second);
    CollectionOf(Projected(1.0), feature);

    Assert.False(second.IsHole);
    Rel(10000.0, feature.AreaSquareMeters!.Value);
  }

  [Fact]
  public void FeatureAreaFeatureWithoutCollectionReturnsNull()
  {
    Assert.Null(FeatureOf(Ring(Rect())).AreaSquareMeters);
  }

  [Fact]
  public void FeatureAreaCollectionWithNoPartsYetReturnsNull()
  {
    var fc = new FeatureCollection { Crs = Projected(1.0) };
    fc.AddFeature(new Feature());

    Assert.Null(fc[0].AreaSquareMeters);
  }

  // --------------------------------------------------------- geographic

  /// <summary>~30 m x 20 m footprint near Rutland, Vermont.</summary>
  private static (double X, double Y)[] Footprint()
  {
    const double lon = -73.21;
    const double lat = 44.475;
    const double dLon = 0.000378;
    const double dLat = 0.000181;
    return [(lon, lat), (lon + dLon, lat), (lon + dLon, lat + dLat), (lon, lat + dLat)];
  }

  [Fact]
  public void GeographicRingIsMeasuredSphericallyAndPlausible()
  {
    // The whole point of the CrsKind branch: planar shoelace on degrees yields
    // ~1e-7 for this footprint (square degrees), so any plausible value here can
    // only come from the spherical path.
    Part part = Ring(Footprint());
    CollectionOf(Geographic, FeatureOf(part));

    double? area = part.AreaSquareMeters;
    Assert.NotNull(area);
    Assert.True(area!.Value > 300 && area.Value < 900, $"implausible area {area.Value:E}");
  }

  [Fact]
  public void GeographicRingPlanarAreaIsAbsurdlySmallAndNamedAreaIsNot()
  {
    Part part = Ring(Footprint());
    CollectionOf(Geographic, FeatureOf(part));

    Assert.True(part.Area < 1e-6, $"planar area should be square degrees, was {part.Area:E}");
    Assert.True(part.AreaSquareMeters!.Value > 300);
  }

  [Fact]
  public void GeographicRingLengthIsSpherical()
  {
    Part part = Ring(Footprint());
    CollectionOf(Geographic, FeatureOf(part));

    double length = part.LengthMeters!.Value;
    Assert.True(length > 80 && length < 120, $"implausible perimeter {length:E}");
  }

  [Fact]
  public void GeographicFeatureHoleSubtractionUsesSphericalAreas()
  {
    (double X, double Y)[] outer = Footprint();
    (double X, double Y)[] inner =
    [
      (-73.20995, 44.47504),
      (-73.20985, 44.47504),
      (-73.20985, 44.47512),
      (-73.20995, 44.47512),
    ];

    Part outerPart = Ring(outer);
    Part holePart = Hole(inner);
    Feature feature = FeatureOf(outerPart, holePart);
    CollectionOf(Geographic, feature);

    // A second feature carrying only the exterior, attached before anything is
    // read: AreaSquareMeters resolves through the owner chain and is null on a
    // detached feature.
    Part wholePart = Ring(outer);
    CollectionOf(Geographic, FeatureOf(wholePart));

    Assert.True(holePart.IsHole);
    Assert.NotNull(feature.AreaSquareMeters);

    double expected = wholePart.AreaSquareMeters!.Value - holePart.AreaSquareMeters!.Value;
    Rel(expected, feature.AreaSquareMeters!.Value, 1e-9);

    // Closed-form sphere values are ~603.6 and ~70.6 m^2, so the hole removes
    // ~12% of the footprint. Planar shoelace would put the delta at ~6e-8 --
    // this assertion cannot pass on the planar path.
    Assert.True(feature.AreaSquareMeters < wholePart.AreaSquareMeters);
  }

  [Fact]
  public void UnknownCrsReturnsNullForEveryNamedMetric()
  {
    Part part = Ring(Footprint());
    Feature feature = FeatureOf(part);

    Assert.Null(part.AreaSquareMeters);
    Assert.Null(part.LengthMeters);
    Assert.Null(feature.AreaSquareMeters);
    // ...while the native planar numbers remain available and unchanged.
    Assert.True(part.Area > 0);
  }
}

