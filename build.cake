// Install addins.
#addin "nuget:?package=Cake.Codecov&version=1.0.1"
#addin "nuget:?package=Cake.Compression&version=0.2.6"
#addin "nuget:?package=Cake.Coverlet&version=2.5.4"
#addin "nuget:?package=Cake.Docker&version=1.0.0"
#addin "nuget:?package=Cake.Git&version=1.0.1"
#addin "nuget:?package=Cake.Gitter&version=1.1.0"
#addin "nuget:?package=Cake.Incubator&version=6.0.0"
#addin "nuget:?package=Cake.Json&version=6.0.1"
#addin "nuget:?package=Cake.Kudu&version=1.0.1"
#addin "nuget:?package=Cake.Wyam&version=2.2.12"

#addin "nuget:?package=Newtonsoft.Json&version=12.0.3"
#addin "nuget:?package=SharpZipLib&version=1.3.2"
#addin "nuget:?package=xunit.assert&version=2.4.1"

// Install tools.
#tool "nuget:?package=NUnit.ConsoleRunner&version=3.12.0"
#tool "nuget:?package=nuget.commandline&version=5.9.1"
#tool "nuget:?package=KuduSync.NET&version=1.5.3"

// Install .NET Core Global tools.
#tool "dotnet:?package=Codecov.Tool&version=1.13.0"
#tool "dotnet:?package=dotnet-format&version=5.1.225507"
#tool "dotnet:?package=GitReleaseManager.Tool&version=0.12.0"
#tool "dotnet:?package=Wyam.Tool&version=2.2.9"

// Load other scripts.
#load "./build/utils/parameters.cake"
#load "./build/utils/utils.cake"

#load "./build/build.cake"
#load "./build/test.cake"
#load "./build/pack.cake"
#load "./build/artifacts-test.cake"
#load "./build/docker-build.cake"
#load "./build/publish.cake"
#load "./build/release.cake"
#load "./build/docs.cake"

using Xunit;
using System.Diagnostics;
//////////////////////////////////////////////////////////////////////
// PARAMETERS
//////////////////////////////////////////////////////////////////////
bool publishingError = false;
bool singleStageRun = true;
///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup<BuildParameters>(context =>
{
    return LogGroup<BuildParameters>("Cake Setup", () =>
    {
        try
        {
            EnsureDirectoryExists("artifacts");
            var parameters = BuildParameters.GetParameters(context);
            var gitVersion = GetVersion(parameters);
            parameters.Initialize(context, gitVersion);

            // Increase verbosity?
            if (parameters.IsMainBranch && (context.Log.Verbosity != Verbosity.Diagnostic)) {
                Information("Increasing verbosity to diagnostic.");
                context.Log.Verbosity = Verbosity.Diagnostic;
            }

            if (parameters.IsLocalBuild)             Information("Building locally");
            if (parameters.IsRunningOnAppVeyor)      Information("Building on AppVeyor");
            if (parameters.IsRunningOnTravis)        Information("Building on Travis");
            if (parameters.IsRunningOnAzurePipeline) Information("Building on AzurePipeline");
            if (parameters.IsRunningOnGitHubActions) Information("Building on GitHubActions");

            Information("Building version {0} of GitVersion ({1}, {2})",
                parameters.Version.SemVersion,
                parameters.Configuration,
                parameters.Target);

            Information("Repository info : IsMainRepo {0}, IsMainBranch {1}, IsTagged: {2}, IsPullRequest: {3}",
                parameters.IsMainRepo,
                parameters.IsMainBranch,
                parameters.IsTagged,
                parameters.IsPullRequest);

            return parameters;
        }
        catch (Exception exception)
        {
            Error(exception.Dump());
            return null;
        }
    });
});

Teardown<BuildParameters>((context, parameters) =>
{
    LogGroup("Cake Teardown", () =>
    {
        try
        {
            Information("Starting Teardown...");

            Information("Repository info : IsMainRepo {0}, IsMainBranch {1}, IsTagged: {2}, IsPullRequest: {3}",
                parameters.IsMainRepo,
                parameters.IsMainBranch,
                parameters.IsTagged,
                parameters.IsPullRequest);

            Information("Finished running tasks.");
        }
        catch (Exception exception)
        {
            Error(exception.Dump());
        }
    });
});

TaskSetup(setupContext =>
{
    var message = string.Format("Task: {0}", setupContext.Task.Name);
    StartGroup(message);
});

TaskTeardown(teardownContext =>
{
    var message = string.Format("Task: {0}", teardownContext.Task.Name);
    EndGroup();
});


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Pack")
    .IsDependentOn("Pack-Nuget")
    .IsDependentOn("Pack-Chocolatey")
    .IsDependentOn("Zip-Files")
    .Does<BuildParameters>((parameters) =>
{
    Information("The build artifacts: \n");
    foreach(var artifact in parameters.Artifacts.All)
    {
        if (FileExists(artifact.ArtifactPath)) { Information("Artifact: {0}", artifact.ArtifactPath); }
    }

    foreach(var package in parameters.Packages.All)
    {
        if (FileExists(package.PackagePath)) { Information("Artifact: {0}", package.PackagePath); }
    }
})
    .ReportError(exception =>
{
    Error(exception.Dump());
});

Task("Test")
    .IsDependentOn("Publish-Coverage")
    .Finally(() =>
{
});

Task("Publish-CI")
    .IsDependentOn("Publish-AppVeyor")
    .IsDependentOn("Publish-AzurePipeline")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of GitVersion.");
    }
});

Task("Publish-NuGet")
    .IsDependentOn("Publish-NuGet-Internal")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of GitVersion.");
    }
});

Task("Publish-Chocolatey")
    .IsDependentOn("Publish-Chocolatey-Internal")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of GitVersion.");
    }
});

Task("Publish-Documentation")
    .IsDependentOn("Publish-Documentation-Internal")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of GitVersion.");
    }
});

Task("Publish")
    .IsDependentOn("Publish-AppVeyor")
    .IsDependentOn("Publish-AzurePipeline")
    .IsDependentOn("Publish-NuGet-Internal")
    .IsDependentOn("Publish-Chocolatey-Internal")
    .IsDependentOn("Publish-Documentation-Internal")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of GitVersion. All publishing tasks have been attempted.");
    }
});

Task("Publish-DockerHub")
    .IsDependentOn("Docker-Publish")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of GitVersion. All publishing tasks have been attempted.");
    }
});

Task("Release")
    .IsDependentOn("Release-Notes")
    .Finally(() =>
{
});

Task("Format")
    .Does<BuildParameters>((parameters) =>
{
    var dotnetFormatExe = Context.Tools.Resolve("dotnet-format.exe");
    var args = $"{parameters.Paths.Directories.Root} --folder --exclude **/AddFormats/";
    Context.ExecuteCommand(dotnetFormatExe, args);
});

Task("Default")
    .IsDependentOn("Publish")
    .IsDependentOn("Publish-DockerHub")
    .IsDependentOn("Release");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
RunTarget(target);
