using LibGit2Sharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Cysharp.Diagnostics;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

public static class Program
{
    private static string? FindCsprojFilePath(string path)
    {
        if (path.EndsWith(".csproj"))
        {
            return path;
        }
        else
        {
            return Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }
    }

    public static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancellationTokenSource = new();
        TaskCompletionSource taskCompletionSource = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
            taskCompletionSource.SetResult();
        };

        if (args.Length < 4)
        {
            Console.Error.WriteLine("""
                [0]: input csproj file path or project root directory path
                [1]: "branch"/"tag/"commit"
                [2]: checkout name
                [3]: nupkg output destination directory path
                [4]?: additional option passed to `dotnet pack --configuration Release -p:PackageVersion=0.0.1 --output [3]`
                """);
            return 1;
        }

        try
        {
            await PackAsync(args[0], args[1], args[2], args[3], args.Length >= 5 ? args[4] : "", cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.ToString());
            return 1;
        }

        Console.Error.WriteLine("Successfully ends. Press Ctrl+C");
        await taskCompletionSource.Task;
        return 0;
    }

    public static async ValueTask PackAsync(string inputPath, string checkoutTypeText, string checkoutNameText, string outputDirectoryPath, string additionalPackParam, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var csprojPath = FindCsprojFilePath(inputPath);
        ArgumentNullException.ThrowIfNull(csprojPath);
        var checkoutType = CheckoutType.Parse(checkoutTypeText);
        var gitFolderPath = Repository.Discover(inputPath);
        var assemblySuffix = $"_{checkoutType}_{checkoutNameText}";
        var outputNupkgFilePath = Path.Combine(outputDirectoryPath, $"{Path.GetFileNameWithoutExtension(csprojPath)}{assemblySuffix}.0.0.1.nupkg");
        if (File.Exists(outputNupkgFilePath))
        {
            Console.Error.WriteLine($"nupkg file already exists. {outputNupkgFilePath}");
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutNameText);
        LockFile? lockFile = default;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            lockFile = LockFile.Lock(Path.Combine(gitFolderPath, ".lock.file"));
            cancellationToken.ThrowIfCancellationRequested();
            using var repository = new Repository(gitFolderPath);
            cancellationToken.ThrowIfCancellationRequested();
            var originalHeadBranch = repository.Head;
            try
            {
                if (checkoutType == CheckoutType.Branch)
                {
                    Commands.Checkout(repository, repository.Branches[checkoutNameText]);
                }
                else if (checkoutType == CheckoutType.Tag)
                {
                    Commands.Checkout(repository, (Commit)repository.Tags[checkoutNameText].PeeledTarget);
                }
                else
                {
                    Debug.Assert(checkoutType == CheckoutType.Commit);
                    Commands.Checkout(repository, repository.Lookup<Commit>(checkoutNameText));
                }

                EnsurePackable(csprojPath, outputDirectoryPath, assemblySuffix, cancellationToken);
                await StartPackProcessAsync(csprojPath, additionalPackParam, cancellationToken);
            }
            finally
            {
                Commands.Checkout(repository, originalHeadBranch, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force });
            }
        }
        finally
        {
            lockFile?.Dispose();
        }
    }

    private static async ValueTask StartPackProcessAsync(string csprojPath, string additionalPackParam, CancellationToken cancellationToken)
    {
        var start = ProcessX.StartAsync("dotnet",
                additionalPackParam.Length != 0 ? $"pack --configuration Release {additionalPackParam}" : "pack --configuration Release",
                Path.GetDirectoryName(csprojPath),
                encoding: System.Text.Encoding.UTF8);
        await foreach (var line in start.WithCancellation(cancellationToken))
        {
            Console.Error.WriteLine(line);
        }
    }

    private static void EnsurePackable(string csprojPath, string outputDirectoryPath, string assemblySuffix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(outputDirectoryPath))
        {
            throw new ArgumentException($"Output directory does not exist. {outputDirectoryPath}", nameof(outputDirectoryPath));
        }

        var targets = Path.GetFullPath(csprojPath + "/../Directory.build.targets");
        var projectRootElement = File.Exists(targets) ? ProjectRootElement.Open(targets) : ProjectRootElement.Create(targets);
        cancellationToken.ThrowIfCancellationRequested();
        var propertyGroupElement = FindPropertyGroup(projectRootElement);
        cancellationToken.ThrowIfCancellationRequested();
        cancellationToken.ThrowIfCancellationRequested();
        propertyGroupElement.SetProperty("AssemblyName", "$(MSBuildProjectName)" + assemblySuffix);
        cancellationToken.ThrowIfCancellationRequested();
        propertyGroupElement.SetProperty("IsPackable", "True");
        cancellationToken.ThrowIfCancellationRequested();
        propertyGroupElement.SetProperty("PackageId", "$(AssemblyName)");
        cancellationToken.ThrowIfCancellationRequested();
        propertyGroupElement.SetProperty("PackageVersion", "0.0.1");
        cancellationToken.ThrowIfCancellationRequested();
        propertyGroupElement.SetProperty("PackageOutputPath", outputDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        projectRootElement.Save();
    }

    private static ProjectPropertyGroupElement FindPropertyGroup(ProjectRootElement projectRootElement)
    {
        foreach (var propertyGroupElement in projectRootElement.PropertyGroups)
        {
            if (string.IsNullOrWhiteSpace(propertyGroupElement.Condition))
            {
                return propertyGroupElement;
            }
        }

        return projectRootElement.AddPropertyGroup();
    }
}
