using Cysharp.Diagnostics;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using System.Text;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

partial class Program
{
    private static async ValueTask<int> CommandPackAsync(string[] args, CancellationToken cancellationToken)
    {
        const string Error = """
            [0]: input csproj file path or project root directory path
            -c/--commit [commit id]: commit id
            -b/--branch [branch name]: branch name
            -t/--tag [tag name]: tag name
            -o/--option [option command]: additional option passed to `dotnet pack`
            """;
        if (args.Length < 5)
        {
            Console.Error.WriteLine(Error);
            return 1;
        }

        try
        {
            var csprojFilePath = FindCsprojFilePath(args[1]);
            if (string.IsNullOrWhiteSpace(csprojFilePath))
            {
                throw new NullReferenceException(csprojFilePath);
            }

            var checkoutPairs = new List<CheckoutPair>();
            for (var i = 2; i + 2 <= args.Length;)
            {
                var checkoutType = args[i++] switch
                {
                    "-b" or "--branch" => CheckoutType.Branch,
                    "-c" or "--commit" => CheckoutType.Commit,
                    "-t" or "--tag" => CheckoutType.Tag,
                    _ => throw new InvalidDataException(Error),
                };

                var checkoutName = args[i++];
                var option = default(string);
                if (i + 2 <= args.Length && (args[i] == "--option" || args[i] == "-o"))
                {
                    option = args[i + 1];
                    i += 2;
                }

                checkoutPairs.Add(new(checkoutType, checkoutName, option));
            }

            var lockFile = default(LockFile);
            try
            {
                var gitFolder = Repository.Discover(args[1]);
                using var repository = new Repository(gitFolder);
                await PackAsync(repository, csprojFilePath, checkoutPairs, cancellationToken);
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

        return 0;
    }

    private static async ValueTask PackAsync(Repository repository, string csprojFilePath, IEnumerable<CheckoutPair> checkoutPairs, CancellationToken cancellationToken)
    {
        Branch originalHead = repository.Head;
        try
        {
            var csprojNameWithoutExtension = Path.GetFileNameWithoutExtension(csprojFilePath);
            var csprojDirectoryPath = Path.GetDirectoryName(csprojFilePath) ?? throw new NullReferenceException();
            foreach (var checkoutPair in checkoutPairs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Commit targetCommit = FindCommit(repository, checkoutPair);
                Commands.Checkout(repository, targetCommit);
                EnsurePackable(csprojDirectoryPath, $"{csprojNameWithoutExtension}.{targetCommit.Id.Sha}", cancellationToken);
                var start = ProcessX.StartAsync("dotnet",
                    string.IsNullOrWhiteSpace(checkoutPair.AdditionalOption) ? "pack --configuration Release --version 0.0.1" : $"pack --configuration Release --version 0.0.1 {checkoutPair.AdditionalOption}",
                    csprojDirectoryPath,
                    environmentVariable: GetEnvironmentVariableDictionary(),
                    encoding: Encoding.UTF8);
                await start.WaitAsync(cancellationToken);
            }
        }
        finally
        {
            Commands.Checkout(repository, originalHead, new() { CheckoutModifiers = CheckoutModifiers.Force });
        }
    }

    private static void EnsurePackable(string csprojDirectoryPath, string assemblyName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targets = Path.GetFullPath(csprojDirectoryPath + "/Directory.build.targets");
        var projectRootElement = File.Exists(targets) ? ProjectRootElement.Open(targets) : ProjectRootElement.Create(targets);
        var propertyGroupElement = GetOrAddPropertyGroup(projectRootElement);
        propertyGroupElement.SetProperty("AssemblyName", assemblyName);
        propertyGroupElement.SetProperty("IsPackable", "True");
        propertyGroupElement.SetProperty("PackageId", "$(AssemblyName)");
        cancellationToken.ThrowIfCancellationRequested();
        projectRootElement.Save(Encoding.UTF8);
    }

    private static ProjectPropertyGroupElement GetOrAddPropertyGroup(ProjectRootElement projectRootElement)
    {
        foreach (var element in projectRootElement.PropertyGroups)
        {
            if (string.IsNullOrWhiteSpace(element.Condition))
            {
                return element;
            }
        }

        return projectRootElement.AddPropertyGroup();
    }

    private static ProjectItemGroupElement GetOrAddItemGroup(ProjectRootElement projectRootElement)
    {
        foreach (var element in projectRootElement.ItemGroups)
        {
            if (string.IsNullOrWhiteSpace(element.Condition))
            {
                return element;
            }
        }

        return projectRootElement.AddItemGroup();
    }
}
