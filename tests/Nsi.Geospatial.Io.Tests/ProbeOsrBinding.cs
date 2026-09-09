// using System.Reflection;
// using System.Text;
// using OSGeo.OSR;
// using Xunit;

// namespace Nsi.Geospatial.Io.Tests;

// /// <summary>
// /// TEMPORARY diagnostic for P1-10. Deliberately fails so the binding's real surface
// /// prints. Delete once settled.
// /// </summary>
// public class ProbeOsrBinding
// {
//   [Fact]
//   public void DumpOsrBindingSurface()
//   {
//     // typeof(...) forces these assemblies to load, which the previous version of
//     // this probe never did -- hence a misleading "(none loaded)".
//     var anchors = new[]
//     {
//       ("OSR", typeof(SpatialReference)),
//       ("OGR", typeof(OSGeo.OGR.Geometry)),
//       ("GDAL", typeof(OSGeo.GDAL.Gdal)),
//     };

//     var sb = new StringBuilder();
//     var loaded = new List<Assembly>();

//     foreach (var (label, anchor) in anchors)
//     {
//       Assembly asm = anchor.Assembly;
//       loaded.Add(asm);
//       sb.AppendLine($"{label}: {asm.GetName().Name} {asm.GetName().Version} @ {asm.Location}");

//       foreach (
//         Type t in SafeTypes(asm)
//           .Where(t => t.Name.Contains("Transform", StringComparison.OrdinalIgnoreCase))
//       )
//       {
//         sb.AppendLine($"  TYPE {t.FullName}");
//         foreach (string m in Describe(t))
//         {
//           sb.AppendLine($"      {m}");
//         }
//       }
//     }

//     // The axis-order half of the fix depends on this one existing too.
//     sb.AppendLine("\nSpatialReference axis-mapping methods:");
//     foreach (
//       string m in typeof(SpatialReference)
//         .GetMethods()
//         .Where(m => m.Name.Contains("AxisMapping", StringComparison.OrdinalIgnoreCase))
//         .Select(Describe)
//     )
//     {
//       sb.AppendLine($"  {m}");
//     }

//     // Catch a CoordinateTransformation hiding in an assembly none of the anchors is in.
//     var strays = AppDomain
//       .CurrentDomain.GetAssemblies()
//       .Where(a => a.GetName().Name?.StartsWith("OSGeo") == true && !loaded.Contains(a))
//       .SelectMany(a =>
//         SafeTypes(a)
//           .Where(t => t.Name.Contains("Transform", StringComparison.OrdinalIgnoreCase))
//           .Select(t => $"{a.GetName().Name}: {t.FullName}")
//       );

//     sb.AppendLine(
//       "\nTransform types in other OSGeo assemblies: "
//         + string.Join(", ", strays.DefaultIfEmpty("(none)"))
//     );

//     throw new Exception(sb.ToString());
//   }

//   private static IEnumerable<Type> SafeTypes(Assembly asm)
//   {
//     try
//     {
//       return asm.GetTypes();
//     }
//     catch (ReflectionTypeLoadException ex)
//     {
//       return ex.Types.Where(t => t is not null)!;
//     }
//   }

//   private static IEnumerable<string> Describe(Type t) =>
//     t.GetMembers(
//         BindingFlags.Public
//           | BindingFlags.Instance
//           | BindingFlags.Static
//           | BindingFlags.DeclaredOnly
//       )
//       .Select(m =>
//         m switch
//         {
//           MethodInfo mi => $"{mi.ReturnType.Name} {mi.Name}("
//             + string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))
//             + ")",
//           ConstructorInfo ci =>
//             $"ctor({string.Join(", ", ci.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})",
//           _ => m.Name,
//         }
//       )
//       .OrderBy(s => s, StringComparer.Ordinal);

//   private static string Describe(MethodInfo mi) =>
//     $"{mi.ReturnType.Name} {mi.Name}("
//     + string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))
//     + ")";
// }

