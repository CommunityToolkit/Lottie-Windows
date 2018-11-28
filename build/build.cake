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

//////////////////////////////////////////////////////////////////////
// VERSIONS
//////////////////////////////////////////////////////////////////////

var gitVersioningVersion = "2.1.65";
var inheritDocVersion = "1.1.1.1";

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var baseDir = MakeAbsolute(Directory("../")).ToString();
var buildDir = $"{baseDir}/build";
var toolsDir = $"{buildDir}/tools";

var allConfigurations = new[] {"Lottie-Windows", "LottieGen", "dlls"};
var configurationsProducingNugets = new[] {"Lottie-Windows", "LottieGen"};

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

// Builds the solution with the given target, once for each
// configuration, setting the given build properties.
void MSBuildSolution(
    string target,
    string[] configurations,
    params (string Name, string Value)[] properties)
{
    MSBuildSettings SettingsWithTarget() => 
        new MSBuildSettings
        {
            MaxCpuCount = 0,
        }.WithTarget(target);

    MSBuildSettings SetProperties(MSBuildSettings settings)
    {
        foreach(var property in properties)
        {
            settings = settings.WithProperty(property.Name, property.Value);
        }
        return settings;
    }

    foreach (var configuration in configurations)
    {
        var msBuildSettings = SetProperties(SettingsWithTarget().SetConfiguration(configuration));
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
    MSBuildSolution("Clean", allConfigurations);
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
    MSBuildSolution("Restore", allConfigurations);
    
    EnsureDirectoryExists(nupkgDir);

    // Build.
    MSBuildSolution("Build", allConfigurations, ("GenerateLibraryLayout", "true"));
});

Task("InheritDoc")
    .Description("Updates <inheritdoc /> tags from base classes, interfaces, and similar methods")
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
                .AppendSwitchQuoted("-b", baseDir)
                .AppendSwitch("-o", "")
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
    MSBuildSolution("Pack", configurationsProducingNugets, ("GenerateLibraryLayout", "true"), ("PackageOutputPath", nupkgDir));
    
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
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
