#module "Cake.Longpath.Module"

#addin "Cake.FileHelpers"
#addin "Cake.Powershell"

using System;
using System.Linq;
using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// VERSIONS
//////////////////////////////////////////////////////////////////////

var gitVersioningVersion = "2.1.65";
var inheritDocVersion = "2.0.2";

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var baseDir = MakeAbsolute(Directory("../")).ToString();
var buildDir = $"{baseDir}/build";
var toolsDir = $"{buildDir}/tools";

var binDir = $"{baseDir}/bin";
var nupkgDir =$"{binDir}/nupkg";

var styler = $"{toolsDir}/XamlStyler.Console/tools/xstyler.exe";
var stylerFile = $"{baseDir}/settings.xamlstyler";

var versionClient = $"{toolsDir}/nerdbank.gitversioning/tools/Get-Version.ps1";
string Version = null;

var inheritDoc = $"{toolsDir}/InheritDoc/tools/InheritDoc.exe";
var inheritDocExclude = "Foo.*";

var verifyHeadersExclude = "internal/**";

//////////////////////////////////////////////////////////////////////
// METHODS
//////////////////////////////////////////////////////////////////////

// Builds the solution with the given target setting the given build properties.
void MSBuildSolution(
    string target,
    params (string Name, string Value)[] properties)
{
    MSBuildSettings SettingsWithTarget() =>
        new MSBuildSettings
        {
            // Restrict to a single CPU. There is some
            // race condition that is causing files
            // to be in use when they need to be
            // overwriten. Restricting to a single
            // CPU fixes this.
            MaxCpuCount = 1,
        }.WithTarget(target);

    MSBuildSettings SetProperties(MSBuildSettings settings)
    {
        foreach(var property in properties)
        {
            settings = settings.WithProperty(property.Name, property.Value);
        }
        return settings;
    }

    var msBuildSettings = SetProperties(SettingsWithTarget().SetConfiguration(configuration));

    foreach (var platformTarget in new []
    {
        PlatformTarget.x86,
        PlatformTarget.MSIL,
    })
    {
        msBuildSettings.PlatformTarget = platformTarget;
        MSBuild($"{baseDir}/Lottie-Windows.sln", msBuildSettings);
    }
}

// Returns true if the given file has a name that indicates it is
// generated code.
static bool IsAutoGenerated(FilePath path)
{
    var fileName = path.GetFilename().ToString();
    // Exclude these auto-generated files.
    return  fileName.EndsWith(".g.cs") ||
            fileName.EndsWith(".i.cs") ||
            fileName.Contains("TemporaryGeneratedFile");
}

static bool IsExcludedDirectory(FilePath path)
{
    var segments = path.Segments;

    return
        segments.Contains("bin") ||
        segments.Contains("internal") ||
        segments.Contains("obj");
}

// Returns true if the given file is source that the build system
// should use directly i.e. it is not generated in the build and
// is not being excluded for some reason.
static bool IsBuildInput(FilePath path)
{
    return !IsExcludedDirectory(path) && !IsAutoGenerated(path);
}

void VerifyHeaders(bool updateHeaders)
{
    var header = FileReadText("header.txt") + "\r\n";
    bool hasMissing = false;

    // .cs files need copyright headers.
    var files = GetFiles($"{baseDir}/**/*.cs").Where(IsBuildInput);

    Information($"\r\nChecking {files.Count()} file header(s)");
    foreach(var file in files)
    {
        var oldContent = FileReadText(file);
        if(oldContent.Contains("// <auto-generated>"))
        {
           continue;
        }
        var rgx = new Regex("^(//.*\r?\n)*\r?\n");
        var newContent = header + rgx.Replace(oldContent, "");

        if(!newContent.Equals(oldContent, StringComparison.Ordinal))
        {
            if(updateHeaders)
            {
                Information($"\r\nUpdating {file} header...");
                FileWriteText(file, newContent);
            }
            else
            {
                Error($"\r\nWrong/missing header on {file}");
                hasMissing = true;
            }
        }
    }

    if(!updateHeaders && hasMissing)
    {
        throw new Exception("Please run UpdateHeaders.bat or '.\\build.ps1 -target=UpdateHeaders' and commit the changes.");
    }
}

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Clean the output folder and run the solution Clean target")
    .Does(() =>
{
    if(DirectoryExists(binDir))
    {
        Information("\r\nCleaning Working Directory");
        CleanDirectory(binDir);
    }
    else
    {
        CreateDirectory(binDir);
    }

    // Run the clean target on the solution.
    MSBuildSolution("Clean");
});

Task("Verify")
    .Description("Run pre-build verifications")
    .IsDependentOn("Clean")
    .Does(() =>
{
    // Source code needs to have appropriate licensing headers.
    VerifyHeaders(false);

    // SDK needs to be installed.
    StartPowershellFile("./Find-WindowsSDKVersions.ps1");
});

Task("Version")
    .Description("Updates the version information in all Projects")
    .IsDependentOn("Verify")
    .Does(() =>
{
    Information("\r\nDownloading NerdBank GitVersioning...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion  = true,
        Version = gitVersioningVersion,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"nerdbank.gitversioning"}, installSettings);

    Information("\r\nRetrieving version...");
    var results = StartPowershellFile(versionClient);
    Version = results[1].Properties["NuGetPackageVersion"].Value.ToString();
    Information($"\r\nBuild Version: {Version}");
});

Task("Build")
    .Description("Build all projects and get the assemblies")
    .IsDependentOn("Version")
    .Does(() =>
{
    Information("\r\nBuilding Solution");

    // Restore NuGet packages.
    MSBuildSolution("Restore");

    EnsureDirectoryExists(nupkgDir);

    // Build.
    MSBuildSolution("Build", ("GenerateLibraryLayout", "true"));
});

Task("InheritDoc")
    .Description("Replaces <inheritdoc /> tags in xml comments with content from inherited members")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("\r\nDownloading InheritDoc...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion = true,
        Version = inheritDocVersion,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"InheritDoc"}, installSettings);

    var args = new ProcessArgumentBuilder()
                // Only process xml comments from the Lottie-Windows project. No other project
                // requires documentation.
                .AppendSwitchQuoted("-b", $"{baseDir}/Lottie-Windows")
                // Overwrite the xml files.
                .AppendSwitch("-o", "")
                // Exclude these types.
                .AppendSwitchQuoted("-x", inheritDocExclude);

    var result = StartProcess(inheritDoc, new ProcessSettings { Arguments = args });

    if (result != 0)
    {
        throw new InvalidOperationException("InheritDoc failed!");
    }

    Information("\r\nFinished generating documentation with InheritDoc");
});

Task("Package")
    .Description("Pack the NuPkg")
    .IsDependentOn("InheritDoc")
    .Does(() =>
{
    // Invoke the pack target to generate the code to be packed.
    MSBuildSolution("Pack", ("GenerateLibraryLayout", "true"), ("PackageOutputPath", nupkgDir));

    foreach (var nuspec in GetFiles("./*.nuspec"))
    {
        var nuGetPackSettings = new NuGetPackSettings
        {
            OutputDirectory = nupkgDir,
            Version = Version
        };

        NuGetPack(nuspec, nuGetPackSettings);
    }
});

Task("UpdateHeaders")
    .Description("Updates the headers in *.cs files")
    .Does(() =>
{
    VerifyHeaders(true);
});

Task("StyleXaml")
    .Description("Ensures XAML Formatting is clean")
    .Does(() =>
{
    Information("\r\nDownloading XamlStyler...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion  = true,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"xamlstyler.console"}, installSettings);

    var files = GetFiles($"{baseDir}/**/*.xaml").Where(IsBuildInput);
    Information($"\r\nChecking {files.Count()} file(s) for XAML Structure");
    foreach(var file in files)
    {
        StartProcess(styler, $"-f \"{file}\" -c \"{stylerFile}\"");
    }
});

Task("Default")
    .IsDependentOn("Package");


//////////////////////////////////////////////////////////////////////
// Dependency tree
//////////////////////////////////////////////////////////////////////
//
// Default
//   Package
//     InheritDoc
//       Build
//         Version
//           Verify
//             Clean
//
//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
