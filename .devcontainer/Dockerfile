# Dev container for USACE-NSI/Geospatial
# Base: mcr.microsoft.com/devcontainers/dotnet:2-10.0-noble (Ubuntu 24.04, .NET 10 SDK, user: vscode)
#
# Adds the native GDAL runtime that the managed `gdal` 3.11.3 NuGet
# (OSGeo.OGR/OSR) P/Invokes into:
#   - libgdal.so (+ proj/geos/... deps)
#   - SWIG C# wrapper DSOs: libgdal_wrap.so / libogr_wrap.so /
#     libosr_wrap.so / libgdalconst_wrap.so   (conda-forge gdal-csharp;
#     not in Ubuntu apt; available for both amd64 and arm64)
#
# Arch-aware: builds for amd64 or arm64 targets.

FROM mcr.microsoft.com/devcontainers/dotnet:2-10.0-noble

# ---- micromamba (architecture-aware static binary) ----
# TARGETARCH is provided by BuildKit (amd64 | arm64).
# NOTE: the micro.mamba.pm "/latest" endpoint is a bzip2 TARBALL, not an
# executable — running it gives "Exec format error". Use the GitHub release
# asset (a raw static binary); fall back to the tarball if GitHub is blocked.
ARG TARGETARCH
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl \
 && rm -rf /var/lib/apt/lists/* \
 && MAMBA_SUBDIR=$(case "$TARGETARCH" in \
      amd64) echo linux-64 ;; \
      arm64) echo linux-aarch64 ;; \
      *) echo unsupported ;; esac) \
 && [ "$MAMBA_SUBDIR" != "unsupported" ] \
 && { curl -LfsS "https://github.com/mamba-org/micromamba-releases/releases/latest/download/micromamba-${MAMBA_SUBDIR}" \
        -o /usr/local/bin/micromamba \
      || { curl -LfsS "https://micro.mamba.pm/api/micromamba/${MAMBA_SUBDIR}/latest" -o /tmp/mamba.tar.bz2 \
           && tar -xjf /tmp/mamba.tar.bz2 -C /usr/local/bin --strip-components=1 bin/micromamba; } } \
 && chmod +x /usr/local/bin/micromamba \
 && /usr/local/bin/micromamba --version

# ---- GDAL native runtime, fixed prefix so the env is deterministic for every user ----
# Prefer the version-matched pin (gdal 3.11 == managed wrapper 3.11.3). If the
# solver can't satisfy both, fall back to the latest gdal-csharp (pulls its own
# gdal); the OGR C API is stable across 3.x minor versions.
RUN set -e \
 && ( /usr/local/bin/micromamba create -y -r /usr/local/bin/micromamba -p /opt/gdal -c conda-forge \
        "gdal-csharp" "gdal=3.11" \
      || { rm -rf /opt/gdal; \
          /usr/local/bin/micromamba create -y -r /usr/local/bin/micromamba -p /opt/gdal -c conda-forge \
        "gdal-csharp"; } )

# The OSGeo.OGR assembly resolves "ogr_wrap" -> $LD_LIBRARY_PATH/libogr_wrap.so
ENV LD_LIBRARY_PATH="/opt/gdal/lib"
ENV GDAL_DATA="/opt/gdal/share/gdal"
ENV PROJ_DATA="/opt/gdal/share/proj"
ENV PROJ_LIB="/opt/gdal/share/proj"

# Projects target net8.0 but this image ships only the .NET 10 runtime.
ENV DOTNET_ROLL_FORWARD=Major

# Smoke tests: wrapper lib must resolve all deps, and the gdal CLI must run.
RUN set -eu \
 && ldd /opt/gdal/lib/libogr_wrap.so > /tmp/ldd-wrap.txt \
 && ! grep -q "not found" /tmp/ldd-wrap.txt \
 && ls /opt/gdal/lib | grep -E '(gdal|ogr|osr|gdalconst)_wrap\.so' \
 && /opt/gdal/bin/gdalinfo --version