namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.SourceGenerator;

internal readonly struct TemplateData(string commitId, string methodName, string alias, bool baseLine, string? description, int operationsPerInvoke, string rewrittenText) : IEquatable<TemplateData>, IComparable<TemplateData>
{
    public readonly string CommitId = commitId;
    public readonly string MethodName = methodName;
    public readonly string Alias = alias;
    public readonly bool BaseLine = baseLine;
    public readonly string? Description = description;
    public readonly int OperationsPerInvoke = operationsPerInvoke;
    public readonly string RewrittenText = rewrittenText;

    public int CompareTo(TemplateData other)
    {
        if (BaseLine != other.BaseLine)
        {
            return BaseLine ? 1 : -1;
        }

        if (OperationsPerInvoke != other.OperationsPerInvoke)
        {
            return OperationsPerInvoke - other.OperationsPerInvoke;
        }

        var c = CommitId.CompareTo(other.CommitId, StringComparison.Ordinal);
        if (c != 0)
        {
            return c;
        }

        c = Alias.CompareTo(other.Alias, StringComparison.Ordinal);
        if (c != 0)
        {
            return c;
        }

        c = MethodName.CompareTo(other.MethodName, StringComparison.Ordinal);
        if (c != 0)
        {
            return c;
        }

        if (Description is null)
        {
            return other.Description is null ? 0 : -1;
        }
        else
        {
            return other.Description is null ? 1 : Description.CompareTo(other.Description, StringComparison.Ordinal);
        }
    }

    public bool Equals(TemplateData other)
        => CommitId == other.CommitId && Alias == other.Alias && MethodName == other.MethodName
            && BaseLine == other.BaseLine && Description == other.Description && OperationsPerInvoke == other.OperationsPerInvoke;

    public override bool Equals(object? obj)
    {
        return obj is TemplateData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ((OperationsPerInvoke << 1) | (BaseLine ? 1 : 0)) ^ CommitId.GetHashCode() ^ Alias.GetHashCode() ^ MethodName.GetHashCode() ^ (Description?.GetHashCode() ?? 0);
    }
}
