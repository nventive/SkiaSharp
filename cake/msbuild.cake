using System.Xml.Linq;

FilePath NUGET_CONFIG_PATH = MakeAbsolute(ROOT_PATH.CombineWithFilePath("nuget.config"));
DirectoryPath PACKAGE_CACHE_PATH = MakeAbsolute(ROOT_PATH.Combine("externals/package_cache"));
DirectoryPath OUTPUT_NUGETS_PATH = MakeAbsolute(ROOT_PATH.Combine("output/nugets"));
DirectoryPath OUTPUT_SPECIAL_NUGETS_PATH = MakeAbsolute(ROOT_PATH.Combine("output/nugets-special"));
DirectoryPath OUTPUT_SYMBOLS_NUGETS_PATH = MakeAbsolute(ROOT_PATH.Combine("output/nugets-symbols"));

var NUGETS_SOURCES = new [] {
    OUTPUT_NUGETS_PATH.FullPath,
};

string[] GetNuGetSources()
{
    // load all the sources from nuget.config
    var xdoc = XDocument.Load(NUGET_CONFIG_PATH.FullPath);
    var xmlns = xdoc.Root.Name.Namespace;
    var adds = xdoc.Elements(xmlns + "configuration")
        .Elements(xmlns + "packageSources")
        .Elements(xmlns + "add")
        .Select(x => x.Attribute("value").Value)
        .ToList();

    // add the NUGETS_SOURCES because it may contain local folders
    adds.AddRange(NUGETS_SOURCES);

    // return all
    return adds.ToArray();
}

void RunNuGetRestorePackagesConfig(FilePath sln)
{
    var dir = sln.GetDirectory();

    EnsureDirectoryExists(OUTPUT_NUGETS_PATH);

    var settings = new NuGetRestoreSettings {
        Source = GetNuGetSources(),
        NoCache = true,
        PackagesDirectory = dir.Combine("packages"),
    };

    foreach (var config in GetFiles(dir + "/**/packages.config"))
        NuGetRestore(config, settings);
}

void RunMSBuild(
    FilePath solution,
    string platform = "Any CPU",
    string platformTarget = null,
    bool restore = true,
    string[] targets = null,
    string configuration = null,
    Dictionary<string, string> properties = null)
{
    EnsureDirectoryExists(OUTPUT_NUGETS_PATH);

    MSBuild(solution, c => {
        c.Configuration = configuration ?? CONFIGURATION;
        c.Verbosity = VERBOSITY;

        if (IsRunningOnWindows())
            c.MaxCpuCount = 0;
        else
            c.MaxCpuCount = 1;

        var relativeSolution = MakeAbsolute(ROOT_PATH).GetRelativePath(MakeAbsolute(solution));
        var blPath = ROOT_PATH.Combine("output/logs/binlogs").CombineWithFilePath(relativeSolution + ".binlog");
        c.BinaryLogger = new MSBuildBinaryLogSettings {
            Enabled = true,
            FileName = blPath.FullPath,
        };

        if (!string.IsNullOrEmpty(MSBUILD_EXE)) {
            c.ToolPath = MSBUILD_EXE;
        } else if (IsRunningOnWindows() && !string.IsNullOrEmpty(VS_INSTALL)) {
            c.ToolPath = ((DirectoryPath)VS_INSTALL).CombineWithFilePath("MSBuild/Current/Bin/MSBuild.exe");
        }

        c.NoLogo = VERBOSITY == Verbosity.Minimal;
        c.Restore = restore;

        if (targets?.Length > 0) {
            c.Targets.Clear();
            foreach (var target in targets) {
                c.Targets.Add(target);
            }
        }

        if (!string.IsNullOrEmpty(platformTarget)) {
            platform = null;
            c.PlatformTarget = (PlatformTarget)Enum.Parse(typeof(PlatformTarget), platformTarget);
        } else {
            c.PlatformTarget = PlatformTarget.MSIL;
            c.MSBuildPlatform = MSBuildPlatform.x86;
        }

        if (!string.IsNullOrEmpty(platform)) {
            c.Properties ["Platform"] = new [] { $"\"{platform}\"" };
        }

        c.Properties ["RestoreNoCache"] = new [] { "true" };
        c.Properties ["RestorePackagesPath"] = new [] { PACKAGE_CACHE_PATH.FullPath };

        if (properties != null) {
            foreach (var prop in properties) {
                c.Properties [prop.Key] = new [] { prop.Value };
            }
        }
        // c.Properties ["RestoreSources"] = GetNuGetSources();
        var sep = IsRunningOnWindows() ? ";" : "%3B";
        c.ArgumentCustomization = args => args.Append($"/p:RestoreSources=\"{string.Join(sep, GetNuGetSources())}\"");
    });
}

void RunDotNetBuild(
    FilePath solution,
    string[] targets = null,
    string configuration = null,
    Dictionary<string, string> properties = null)
{
    EnsureDirectoryExists(OUTPUT_NUGETS_PATH);

    var c = new DotNetBuildSettings();
    var msb = new DotNetMSBuildSettings();
    c.MSBuildSettings = msb;

    c.Configuration = configuration ?? CONFIGURATION;
    c.Verbosity = (DotNetVerbosity)VERBOSITY;

    var relativeSolution = MakeAbsolute(ROOT_PATH).GetRelativePath(MakeAbsolute(solution));
    var blPath = ROOT_PATH.Combine("output/logs/binlogs").CombineWithFilePath(relativeSolution + ".binlog");
    msb.BinaryLogger = new MSBuildBinaryLoggerSettings {
        Enabled = true,
        FileName = blPath.FullPath,
    };
      
    c.NoLogo = VERBOSITY == Verbosity.Minimal;

    if (targets?.Length > 0) {
        msb.Targets.Clear();
        foreach (var target in targets) {
            msb.Targets.Add(target);
        }
    }

    msb.Properties ["RestoreNoCache"] = new [] { "true" };
    msb.Properties ["RestorePackagesPath"] = new [] { PACKAGE_CACHE_PATH.FullPath };

    if (properties != null) {
        foreach (var prop in properties) {
            msb.Properties [prop.Key] = new [] { prop.Value };
        }
    }
    c.Sources = GetNuGetSources();
    
    DotNetBuild(solution.FullPath, c);
}

void RunDotNetPack(
    FilePath solution,
    DirectoryPath outputPath = null,
    string configuration = null,
    Dictionary<string, string> properties = null,
    string versionSuffix = null)
{
    EnsureDirectoryExists(OUTPUT_NUGETS_PATH);

    var c = new DotNetPackSettings();
    var msb = new DotNetMSBuildSettings();
    c.MSBuildSettings = msb;

    c.Configuration = configuration ?? CONFIGURATION;
    c.Verbosity = (DotNetVerbosity)VERBOSITY;

    var relativeSolution = MakeAbsolute(ROOT_PATH).GetRelativePath(MakeAbsolute(solution));
    var blPath = ROOT_PATH.Combine("output/logs/binlogs").CombineWithFilePath(relativeSolution + ".pack.binlog");
    msb.BinaryLogger = new MSBuildBinaryLoggerSettings {
        Enabled = true,
        FileName = blPath.FullPath,
    };
      
    c.NoLogo = VERBOSITY == Verbosity.Minimal;
    // c.NoBuild = true;

    c.OutputDirectory = outputPath ?? OUTPUT_NUGETS_PATH;

    msb.Properties ["NoDefaultExcludes"] = new [] { "true" };

    if (!string.IsNullOrEmpty(GIT_SHA))
        msb.Properties ["GIT_SHA"] = new [] { GIT_SHA };
    if (!string.IsNullOrEmpty(GIT_BRANCH_NAME))
        msb.Properties ["GIT_BRANCH_NAME"] = new [] { GIT_BRANCH_NAME };
    if (!string.IsNullOrEmpty(GIT_URL))
        msb.Properties ["GIT_URL"] = new [] { GIT_URL };
    if (!string.IsNullOrEmpty(BUILD_COUNTER))
        msb.Properties ["BUILD_COUNTER"] = new [] { BUILD_COUNTER };
    if (!string.IsNullOrEmpty(BUILD_NUMBER))
        msb.Properties ["BUILD_NUMBER"] = new [] { BUILD_NUMBER };
    if (!string.IsNullOrEmpty(FEATURE_NAME))
        msb.Properties ["FEATURE_NAME"] = new [] { FEATURE_NAME };
    if (!string.IsNullOrEmpty(PREVIEW_LABEL))
        msb.Properties ["PREVIEW_LABEL"] = new [] { PREVIEW_LABEL };
    if (!string.IsNullOrEmpty(versionSuffix))
        msb.Properties ["VersionSuffix"] = new [] { versionSuffix };

    if (properties != null) {
        foreach (var prop in properties) {
            msb.Properties [prop.Key] = new [] { prop.Value };
        }
    }
    
    DotNetPack(solution.FullPath, c);
}
