namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class BenchmarkTemplateAttribute(string commitId) : Attribute
{
    public bool Baseline { get; set; }

    public string? Description { get; set; }

    public string? MethodName { get; set; }

    public string Alias { get; set; } = "global";

    public int OperationsPerInvoke { get; set; }

    /// <summary>
    /// Absolute path or relative path from current benchmark project folder.
    /// </summary>
    public string? ProjectPath { get; set; }

    public string CommitId { get; } = commitId;

    public string? PackOption { get; set; }
}
