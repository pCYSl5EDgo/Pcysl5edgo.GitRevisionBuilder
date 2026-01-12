using Cysharp.Diagnostics;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

partial class Program
{
    private const string CommandCsprojError = """
        [0]: csproj file path or project folder path.
        [1]: local nuget source folder path.
        -o/--overwrite []: overwrite target/props file. Default is [0] csproj file.
        -d/--define-constants []: define constants.
        """;

    private static async ValueTask<int> CommandCsprojAsync(string[] args, CancellationToken cancellationToken)
    {
        PraseArgs(args, out var projectFolder, out var nugetSourceFolder, out var overwriteTargetPath, out var defineConstants);
        using var info = new FileProcessInfo(Environment.CurrentDirectory, nugetSourceFolder, defineConstants);
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(info.ErrorSource.Token, cancellationToken))
        {
            await Parallel.ForEachAsync(Directory.EnumerateFiles(projectFolder, "*.cs", SearchOption.AllDirectories), cancellationToken, info.ProcessEachFileAsync).ConfigureAwait(false);
            var dictionary = info.CalculateDictionary();
            if (dictionary.Count > 0)
            {
                var temporaryDirectory = Directory.CreateTempSubdirectory();
                try
                {
                    foreach (var (gitRepositoryPath, csprojDictionary) in dictionary)
                    {
                        await info.ProcessEachGitFolderAsync(gitRepositoryPath, csprojDictionary, temporaryDirectory.FullName, linked.Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    temporaryDirectory.Delete(true);
                }
            }
        }

        if (!info.PackageReferenceBag.IsEmpty)
        {
            OverwriteProjectSettings(overwriteTargetPath, info, cancellationToken);
        }

        return 0;
    }

    private static void OverwriteProjectSettings(string overwriteTargetPath, FileProcessInfo info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectRootElement = File.Exists(overwriteTargetPath) ? ProjectRootElement.Open(overwriteTargetPath) : ProjectRootElement.Create(overwriteTargetPath);
        var itemGroupElement = GetOrAddItemGroup(projectRootElement);
        foreach (var (packageName, aliases) in info.PackageReferenceBag)
        {
            cancellationToken.ThrowIfCancellationRequested();
            itemGroupElement.AddItem("PackageReference", packageName, [new("Version", "0.0.1"), new("Aliases", aliases)]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        projectRootElement.Save(Encoding.UTF8);
    }

    private static void PraseArgs(string[] args, out string projectFolder, out string nugetSourceFolder, out string overwriteTargetPath, out HashSet<string>? defineConstants)
    {
        defineConstants = default;
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            throw new InvalidDataException(CommandCsprojError);
        }

        var input = args[1];
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidDataException(CommandCsprojError + "\n    [0] must not be empty.");
        }
        else if (input.EndsWith(".csproj"))
        {
            // csproj file
            projectFolder = Path.GetDirectoryName(overwriteTargetPath = input) ?? throw new NullReferenceException();
        }
        else
        {
            // project folder
            overwriteTargetPath = FindCsprojFilePath(projectFolder = input) ?? throw new NullReferenceException();
        }

        nugetSourceFolder = args[2];

        for (int index = 3; index + 2 <= args.Length; index += 2)
        {
            switch (args[index])
            {
                case "-o":
                case "--overwrite":
                    if (string.IsNullOrWhiteSpace(overwriteTargetPath = args[index + 1]))
                    {
                        throw new InvalidDataException(CommandCsprojError);
                    }
                    break;
                case "-d":
                case "--define-constants":
                    (defineConstants ??= []).Add(args[index + 1]);
                    break;
            }
        }
    }

    private sealed class FileProcessInfo(string relativeRootPath, string nugetSourceDirectoryPath, IEnumerable<string>? defineConstants) : IDisposable
    {
        private readonly ConcurrentBag<(string CsprojFolderPath, string CommitId, string? Option)> bag = [];
        private static readonly CSharpParseOptions parseOptions = new(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Regular, ["RELEASE"]);
        public readonly CancellationTokenSource ErrorSource = new();
        private readonly string relativeRootPath = relativeRootPath;
        private readonly string nugetSourceDirectoryPath = nugetSourceDirectoryPath;
        private readonly IEnumerable<string>? defineConstants = defineConstants;
        public readonly ConcurrentBag<(string PackageName, string Aliases)> PackageReferenceBag = [];

        public Dictionary<string, Dictionary<string, HashSet<(string CommitId, string? Option)>>> CalculateDictionary()
        {
            var answer = new Dictionary<string, Dictionary<string, HashSet<(string, string?)>>>();
            foreach (var (CsprojFolderPath, CommitId, Option) in bag)
            {
                ref var dictionary = ref CollectionsMarshal.GetValueRefOrAddDefault(answer, Repository.Discover(CsprojFolderPath), out _);
                ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary ??= [], CsprojFolderPath, out _);
                (set ??= []).Add((CommitId, Option));
            }

            return answer;
        }

        public async ValueTask ProcessEachFileAsync(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(filePath))
            {
                return;
            }

            var sourceBinary = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var index = sourceBinary.IndexOf("BenchmarkTemplate"u8);
            if (index < 0)
            {
                return;
            }

            Console.Error.WriteLine($"Processing C# file: {filePath}...");
            var sourceText = SourceText.From(sourceBinary, sourceBinary.Length, Encoding.UTF8);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, parseOptions.WithPreprocessorSymbols(defineConstants), filePath, cancellationToken);
            var root = syntaxTree.GetRoot(cancellationToken);
            foreach (var methodDeclaration in root.DescendantNodes(static node => !node.IsKind(SyntaxKind.MethodDeclaration)).OfType<MethodDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.Error.WriteLine($" method {methodDeclaration.Identifier.Text} @ line {methodDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line}...");
                foreach (var attributeList in methodDeclaration.AttributeLists)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var attribute in attributeList.Attributes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (attribute.ArgumentList == null || !IsAppropriateAttributeName(attribute.Name))
                        {
                            continue;
                        }

                        var arguments = attribute.ArgumentList.Arguments;
                        if (arguments.Count == 0)
                        {
                            continue;
                        }

                        Console.Error.WriteLine($"Processing attribute in {filePath} @ line {methodDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line}...");
                        var projectPath = default(string);
                        var commitId = default(string);
                        var packOption = default(string);
                        foreach (var argumentSyntax in arguments)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (argumentSyntax.NameEquals == null)
                            {
                                if (argumentSyntax.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                                {
                                    commitId = literal.Token.ValueText;
                                }
                            }
                            else
                            {
                                switch (argumentSyntax.NameEquals.Name.Identifier.ValueText)
                                {
                                    case "ProjectPath":
                                        {
                                            if (argumentSyntax.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                                            {
                                                projectPath = literal.Token.ValueText;
                                            }
                                        }
                                        break;
                                    case "PackOption":
                                        {
                                            if (argumentSyntax.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                                            {
                                                packOption = literal.Token.ValueText;
                                            }
                                        }
                                        break;
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(commitId))
                        {
                            ErrorSource.Cancel();
                        }

                        bag.Add((projectPath ?? relativeRootPath, commitId!, packOption));
                    }
                }
            }
        }

        private static bool IsAppropriateAttributeName(NameSyntax name)
        {
            Console.Error.WriteLine($"  attribute name {name}");
            return name.ToString() switch
            {
                "BenchmarkTemplate" or "BenchmarkTemplateAttribute" or "Attributes.BenchmarkTemplate" or "Attributes.BenchmarkTemplateAttribute" or "BenchmarkDotNet.Attributes.BenchmarkTemplate" or "BenchmarkDotNet.Attributes.BenchmarkTemplateAttribute" or "GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplate" or "GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplateAttribute" or "Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplate" or "Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplateAttribute" or "global::Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplate" or "global::Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplateAttribute" => true,
                _ => false,
            };
        }

        public void Dispose()
        {
            ErrorSource.Dispose();
            bag.Clear();
        }

        public async ValueTask ProcessEachGitFolderAsync(string gitRepositoryPath, Dictionary<string, HashSet<(string CommitId, string? PackOption)>> csprojDictionary, string tempDirectoryPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string leadingArguments;
            {
                DefaultInterpolatedStringHandler handler = $"pack --version 0.0.1 --output \"";
                AppendEscapedText(ref handler, tempDirectoryPath);
                handler.AppendFormatted("\" ");
                leadingArguments = handler.ToStringAndClear();
            }

            using var repository = new Repository(gitRepositoryPath);
            var origin = repository.Head;
            try
            {
                var envs = GetEnvironmentVariableDictionary();
                foreach (var (csprojFilePath, dictionary) in csprojDictionary)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var csprojDirectoryPath = Path.GetDirectoryName(csprojFilePath) ?? throw new NullReferenceException();
                    var csprojNameWithoutExtension = Path.GetFileNameWithoutExtension(csprojFilePath);
                    foreach (var (CommitId, PackOption) in dictionary)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var commit = repository.Lookup<Commit>(CommitId) ?? throw new NullReferenceException($"commitId : {CommitId} not found.");
                        Commands.Checkout(repository, commit);
                        var assemblyName = $"{csprojNameWithoutExtension}.{CommitId}";
                        {
                            // pack
                            EnsurePackable(csprojDirectoryPath, assemblyName, cancellationToken);
                            var process = ProcessX.StartAsync("dotnet",
                                string.IsNullOrWhiteSpace(PackOption) ? leadingArguments : leadingArguments + PackOption,
                                csprojDirectoryPath,
                                envs,
                                Encoding.UTF8);
                            var waitTask = process.WaitAsync(cancellationToken);
                            Console.Error.WriteLine($"Packing {assemblyName}...");
                            await waitTask.ConfigureAwait(false);
                        }
                        {
                            // add nupkg to source directory
                            string arguments;
                            {
                                DefaultInterpolatedStringHandler handler = $"nuget push \"";
                                AppendEscapedText(ref handler, Path.Combine(tempDirectoryPath, assemblyName + ".0.0.1.nupkg"));
                                handler.AppendFormatted("\" --source ");
                                AppendEscapedText(ref handler, nugetSourceDirectoryPath);
                                handler.AppendFormatted("\" --skip-duplicate");
                                arguments = handler.ToStringAndClear();
                            }

                            var process = ProcessX.StartAsync("dotnet",
                                arguments,
                                Environment.CurrentDirectory,
                                envs,
                                Encoding.UTF8);
                            var waitTask = process.WaitAsync(cancellationToken);
                            Console.Error.WriteLine($"Nuget pushing {assemblyName}.0.0.1.nupkg to {nugetSourceDirectoryPath}");
                            await waitTask.ConfigureAwait(false);
                        }

                        PackageReferenceBag.Add((assemblyName, CommitId));
                    }
                }
            }
            finally
            {
                Commands.Checkout(repository, origin);
            }
        }

        private static void AppendEscapedText(ref DefaultInterpolatedStringHandler handler, ReadOnlySpan<char> value)
        {
            while (!value.IsEmpty)
            {
                var index = value.IndexOfAny('\\', '"');
                if (index < 0)
                {
                    handler.AppendFormatted(value);
                    return;
                }
                else if (index == 0)
                {
                    handler.AppendFormatted('\\');
                    handler.AppendFormatted(value[0]);
                    value = value[1..];
                }
                else
                {
                    handler.AppendFormatted(value[..index]);
                    handler.AppendFormatted('\\');
                    handler.AppendFormatted(value[index]);
                    value = value[(index + 1)..];
                }
            }
        }
    }
}
