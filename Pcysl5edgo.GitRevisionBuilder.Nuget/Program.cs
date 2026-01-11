using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

internal static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancellationTokenSource = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        const string help = """
            pack : Manually execution mode
            benchmarkdotnet-analyzer : Called by custom task
            """;
        if (args.Length == 0)
        {
            Console.Error.WriteLine(help);
        }

        try
        {
            switch (args[0])
            {
                case "cs":
                case "csproj":
                    return await CommandCsprojAsync(args, cancellationTokenSource.Token);
                case "pack":
                    return await CommandPackAsync(args, cancellationTokenSource.Token);
                default:
                    Console.Error.WriteLine(help);
                    break;
            }
        }
        catch (InvalidDataException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Find the commit corresponding to the provided <see cref="CheckoutPair"/> within the repository.
    /// </summary>
    /// <param name="repository">Repository to search.</param>
    /// <param name="checkoutPair">Checkout information that selects a commit, branch, or tag.</param>
    /// <returns>The resolved commit object.</returns>
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

    /// <summary>
    /// Returns a single .csproj file path in the provided directory or the path itself if already a .csproj.
    /// </summary>
    /// <param name="path">Directory or project file path.</param>
    /// <returns>Path to the .csproj file or null if not found.</returns>
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

    /// <summary>
    /// Returns a dictionary containing environment variables as strings.
    /// </summary>
    /// <returns>A dictionary mapping environment variable names to their values.</returns>
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
}
