using System.Collections.Immutable;

namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.SourceGenerator;

internal readonly struct BenchmarkData(string? @namespace, string name, ImmutableArray<TemplateData> templateDatas) : IEquatable<BenchmarkData>
{
    public readonly string? Namespace = @namespace;
    public readonly string Name = name;
    public readonly ImmutableArray<TemplateData> TemplateDatas = templateDatas;

    public bool Equals(BenchmarkData other) => Name == other.Name && Namespace == other.Namespace && TemplateDatas == other.TemplateDatas;
    public override bool Equals(object? obj) => obj is BenchmarkData other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = -1732128042 * (-1521134295 + (Namespace?.GetHashCode() ?? 0));
        hashCode = hashCode * -1521134295 + Name.GetHashCode();
        hashCode = hashCode * -1521134295 + TemplateDatas.GetHashCode();
        return hashCode;
    }
}
