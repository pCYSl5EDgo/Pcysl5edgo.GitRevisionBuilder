namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes;

/// <summary>
/// Specifies a benchmark template to generate an external-aliased benchmark method from another assembly commit.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class BenchmarkTemplateAttribute(string commitId) : Attribute
{
    /// <summary>
    /// Gets or sets a value that indicates whether the generated benchmark is the baseline.
    /// </summary>
    public bool Baseline { get; set; }

    /// <summary>
    /// Gets or sets an optional description for the generated benchmark method.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the method name to use for the generated benchmark. If not provided, a name is derived.
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// Gets or sets the alias used when referencing the external assembly. Default is "global".
    /// </summary>
    public string Alias { get; set; } = "global";

    /// <summary>
    /// Gets or sets the number of operations per invoke for the generated benchmark.
    /// </summary>
    public int OperationsPerInvoke { get; set; }

    /// <summary>
    /// Absolute path or relative path from current benchmark project folder.
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// Gets the commit id that identifies the source assembly commit to reference.
    /// </summary>
    public string CommitId { get; } = commitId;

    /// <summary>
    /// Gets or sets optional pack options to pass to dotnet pack when creating the referenced package.
    /// </summary>
    public string? PackOption { get; set; }
}
