using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// One relative-tolerance compare (P-53). Three copies existed; two could not compare against
/// zero, because diff <= |expected| * tol becomes diff <= 0 when expected is 0 -- and the failure
/// message then divided by zero on the way out. The absolute floor is what fixes that.
/// absTol is a parameter and not one constant because the suite compares metres and degrees from
/// the same helper: 1e-9 is a metre-scale floor, and a degree assertion that wants 1e-12 must say
/// so or the floor silently dominates the tolerance it asked for.
/// </summary>
internal static class Tolerance
{
  internal static void Rel(
    double expected,
    double? actual,
    double relTol = 1e-9,
    string what = "",
    double absTol = 1e-9
  )
  {
    // Early return rather than Assert.True(actual.HasValue) then dereferencing: the two-
    // argument Assert.True overload carries no nullability annotation, so the flow analysis
    // does not narrow and actual.Value warns CS8629. This pattern narrows for the compiler.
    if (actual is null)
    {
      Assert.Fail(
        $"{Describe(what)}: expected {expected:R}, got null (no such measure for this PartType)"
      );
      return;
    }

    RelD(expected, actual.Value, relTol, what, absTol);
  }

  internal static void RelD(
    double expected,
    double actual,
    double relTol,
    string what = "",
    double absTol = 1e-9
  )
  {
    double diff = Math.Abs(expected - actual);
    Assert.True(
      diff <= Math.Max(Math.Abs(expected) * relTol, absTol),
      $"{Describe(what)}: expected {expected:R}, actual {actual:R}, diff {diff:E}, rel {diff / Math.Max(Math.Abs(expected), absTol):E}"
    );
  }

  private static string Describe(string what) => string.IsNullOrEmpty(what) ? "assertion" : what;
}

