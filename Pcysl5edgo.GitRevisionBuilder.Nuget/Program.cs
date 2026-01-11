using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

public static partial class Program
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
}
