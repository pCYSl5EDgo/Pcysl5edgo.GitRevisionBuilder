using System.Collections.Immutable;

namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.SourceGenerator;

internal readonly struct BenchmarkData(string? @namespace, string name, ImmutableArray<TemplateData> templateDatas, ImmutableArray<string> aliases) : IEquatable<BenchmarkData>
{
    public readonly string? Namespace = @namespace;
    public readonly string Name = name;
    public readonly ImmutableArray<TemplateData> TemplateDatas = templateDatas;
    public readonly ImmutableArray<string> Aliases = aliases;

    public bool Equals(BenchmarkData other) => Name == other.Name && Namespace == other.Namespace && TemplateDatas == other.TemplateDatas && Aliases == other.Aliases;
    public override bool Equals(object? obj) => obj is BenchmarkData other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = -1732128042 * (-1521134295 + (Namespace?.GetHashCode() ?? 0));
        hashCode = hashCode * -1521134295 + Name.GetHashCode();
        hashCode = hashCode * -1521134295 + TemplateDatas.GetHashCode();
        hashCode = hashCode * -1521134295 + Aliases.GetHashCode();
        return hashCode;
    }
}
