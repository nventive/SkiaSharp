DirectoryPath ROOT_PATH = MakeAbsolute(Directory("../.."));
DirectoryPath OUTPUT_PATH = MakeAbsolute(ROOT_PATH.Combine("output/native/tvos"));

#load "../../../scripts/cake/native-shared.cake"
#load "../../../scripts/cake/xcode.cake"

string GetDeploymentTarget(string arch)
{
    return "9.0";
}

Task("libSkiaSharp")
    .IsDependentOn("git-sync-deps")
    .WithCriteria(IsRunningOnMacOs())
    .Does(() =>
{
    Build("appletvsimulator", "x86_64", "x64");
    Build("appletvos", "arm64", "arm64");

    CreateFatFramework(OUTPUT_PATH.Combine("libSkiaSharp"));

    void Build(string sdk, string arch, string skiaArch)
    {
        if (Skip(arch)) return;

        GnNinja($"tvos/{arch}", "skia modules/skottie",
            $"target_os='tvos' " +
            $"target_cpu='{skiaArch}' " +
            $"min_ios_version='{GetDeploymentTarget(arch)}' " +
            $"skia_use_icu=false " +
            $"skia_use_metal=false " +
            $"skia_use_piex=true " +
            $"skia_use_sfntly=false " +
            $"skia_use_system_expat=false " +
            $"skia_use_system_libjpeg_turbo=false " +
            $"skia_use_system_libpng=false " +
            $"skia_use_system_libwebp=false " +
            $"skia_use_system_zlib=false " +
            $"skia_enable_skottie=true " +
            $"extra_cflags=[ '-DSKIA_C_DLL', '-DHAVE_ARC4RANDOM_BUF' ] ");

        RunXCodeBuild("libSkiaSharp/libSkiaSharp.xcodeproj", "libSkiaSharp", sdk, arch, properties: new Dictionary<string, string> {
            { "TVOS_DEPLOYMENT_TARGET", GetDeploymentTarget(arch) },
        });

        SafeCopy(
            $"libSkiaSharp/bin/{CONFIGURATION}/{sdk}/{arch}.xcarchive",
            OUTPUT_PATH.Combine($"libSkiaSharp/{arch}.xcarchive"));
    }
});

Task("libHarfBuzzSharp")
    .WithCriteria(IsRunningOnMacOs())
    .Does(() =>
{
    Build("appletvsimulator", "x86_64");
    Build("appletvos", "arm64");

    CreateFatFramework(OUTPUT_PATH.Combine("libHarfBuzzSharp"));

    void Build(string sdk, string arch)
    {
        if (Skip(arch)) return;

        RunXCodeBuild("libHarfBuzzSharp/libHarfBuzzSharp.xcodeproj", "libHarfBuzzSharp", sdk, arch, properties: new Dictionary<string, string> {
            { "TVOS_DEPLOYMENT_TARGET", GetDeploymentTarget(arch) },
        });

        SafeCopy(
            $"libHarfBuzzSharp/bin/{CONFIGURATION}/{sdk}/{arch}.xcarchive",
            OUTPUT_PATH.Combine($"libHarfBuzzSharp/{arch}.xcarchive"));
    }
});

Task("Default")
    .IsDependentOn("libSkiaSharp")
    .IsDependentOn("libHarfBuzzSharp");

RunTarget(TARGET);
