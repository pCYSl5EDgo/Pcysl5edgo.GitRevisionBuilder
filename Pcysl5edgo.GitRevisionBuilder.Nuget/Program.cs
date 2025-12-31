using Cysharp.Diagnostics;
using LibGit2Sharp;
using Microsoft.Build.Construction;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

public static partial class Program
{
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
                [1]: nupkg output destination directory path
                -c/--commit [commit id]: commit id
                -b/--branch [branch name]: branch name
                -t/--tag [tag name]: tag name
                --option [option command]: additional option passed to `dotnet pack`
                """);
            return 1;
        }

        try
        {
            var csprojFilePath = FindCsprojFilePath(args[0]);
            if (string.IsNullOrWhiteSpace(csprojFilePath))
            {
                throw new NullReferenceException(csprojFilePath);
            }

            var outputDestinationDirectoryPath = args[1];
            var checkoutPairs = new List<CheckoutPair>();
            for (var i = 2; i + 2 <= args.Length;)
            {
                var checkoutType = args[i++] switch
                {
                    "-b" or "--branch" => CheckoutType.Branch,
                    "-c" or "--commit" => CheckoutType.Commit,
                    "-t" or "--tag" => CheckoutType.Tag,
                    _ => throw new InvalidDataException(),
                };

                var checkoutName = args[i++];
                var option = default(string);
                if (i + 2 <= args.Length && args[i] == "--option")
                {
                    option = args[i + 1];
                    i += 2;
                }

                checkoutPairs.Add(new(checkoutType, checkoutName, option));
            }

            var lockFile = default(LockFile);
            try
            {
                var gitFolder = Repository.Discover(args[0]);
                using var repository = new Repository(gitFolder);
                await PackAsync(repository, csprojFilePath, outputDestinationDirectoryPath, checkoutPairs, cancellationTokenSource.Token);
            }
            finally
            {
                lockFile?.Dispose();
            }

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

    public static async ValueTask PackAsync(Repository repository, string csprojPath, string outputDirectoryPath, IEnumerable<CheckoutPair> checkoutPairs, CancellationToken cancellationToken)
    {
        Branch originalHead = repository.Head;
        try
        {
            var csprojName = Path.GetFileName(csprojPath);
            foreach (var checkoutPair in checkoutPairs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Commit targetCommit = FindCommit(repository, checkoutPair);
                var assemblySuffix = $".{targetCommit.Id.Sha}";
                var outputNupkgFilePath = Path.Combine(outputDirectoryPath, $"{csprojName}{assemblySuffix}.0.0.1.nupkg");
                if (File.Exists(outputNupkgFilePath))
                {
                    Console.Error.WriteLine($"nupkg file already exists. {outputNupkgFilePath}");
                    continue;
                }

                Commands.Checkout(repository, targetCommit);
                EnsurePackable(csprojPath, outputDirectoryPath, assemblySuffix, cancellationToken);
                await StartPackProcessAsync(csprojPath, checkoutPair.AdditionalOption, cancellationToken);
            }
        }
        finally
        {
            Commands.Checkout(repository, originalHead, new() { CheckoutModifiers = CheckoutModifiers.Force });
        }
    }

    private static Commit FindCommit(Repository repository, in CheckoutPair checkoutPair)
    {
        Commit targetCommit;
        if (checkoutPair.Type == CheckoutType.Commit)
        {
            targetCommit = repository.Lookup<Commit>(checkoutPair.Name);
        }
        else if (checkoutPair.Type == CheckoutType.Branch)
        {
            targetCommit = repository.Branches[checkoutPair.Name].Tip;
        }
        else
        {
            Debug.Assert(checkoutPair.Type == CheckoutType.Tag);
            targetCommit = (Commit)repository.Tags[checkoutPair.Name].PeeledTarget;
        }

        return targetCommit;
    }

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

    private static Dictionary<string, string> GetEnvironmentVariableDictionary()
    {
        var answer = new Dictionary<string, string>();
        foreach (var pair in Environment.GetEnvironmentVariables())
        {
            var entry = (DictionaryEntry)pair;
            answer.Add((string)entry.Key, (string)(entry.Value ?? ""));
        }

        return answer;
    }

    private static async ValueTask StartPackProcessAsync(string csprojPath, string? additionalPackParam, CancellationToken cancellationToken)
    {
        var start = ProcessX.StartAsync("dotnet",
                string.IsNullOrWhiteSpace(additionalPackParam) ? "pack --configuration Release" : $"pack --configuration Release {additionalPackParam}",
                Path.GetDirectoryName(csprojPath),
                environmentVariable: GetEnvironmentVariableDictionary(),
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
