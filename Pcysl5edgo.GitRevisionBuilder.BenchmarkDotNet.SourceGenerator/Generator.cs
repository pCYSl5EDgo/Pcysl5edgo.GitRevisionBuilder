using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.SourceGenerator;

/// <summary>
/// Source generator that produces benchmark clone methods from BenchmarkTemplateAttribute usages.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class Generator : IIncrementalGenerator
{
    /// <summary>
    /// Fully qualified metadata name for the benchmark template attribute.
    /// </summary>
    public const string FullyQualifiedMetadataName = "Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.Attributes.BenchmarkTemplateAttribute";

    /// <summary>
    /// Initialize the incremental generator.
    /// </summary>
    /// <param name="context">The initialization context provided by the compiler.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var benchmarkDatas = context.SyntaxProvider.ForAttributeWithMetadataName(FullyQualifiedMetadataName, Filter, Converter);
        context.RegisterSourceOutput(benchmarkDatas, Generate);
    }

    private static void Generate(SourceProductionContext context, BenchmarkData benchmarkData)
    {
        var builder = new StringBuilder();
        foreach (var alias in benchmarkData.Aliases)
        {
            if (alias != "global")
            {
                builder.Append("extern alias ").AppendLine(alias);
            }
        }

        if (!string.IsNullOrWhiteSpace(benchmarkData.Namespace))
        {
            builder.AppendLine().Append("namespace ").Append(benchmarkData.Namespace).AppendLine(";");
        }

        builder.AppendLine().Append("partial class ").AppendLine(benchmarkData.Name).AppendLine("{");
        foreach (var templateData in benchmarkData.TemplateDatas)
        {
            builder.Append("    [global::BenchmarkDotNet.Attributes.BenchmarkAttribute(BaseLine = ").Append(templateData.BaseLine ? "true" : "false");
            if (!string.IsNullOrWhiteSpace(templateData.Description))
            {
                builder.Append(", Description=\"");
                foreach (var c in templateData.Description!)
                {
                    if (c == '\\')
                    {
                        builder.Append("\\\\");
                    }
                    else if (c == '"')
                    {
                        builder.Append("\\\"");
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }
            }

            if (templateData.OperationsPerInvoke != 0)
            {
                builder.Append(", OperationsPerInvoke = ").Append(templateData.OperationsPerInvoke);
            }

            builder.AppendLine(")]").Append("    ").AppendLine(templateData.RewrittenText);
        }

        builder.AppendLine().AppendLine("}");
        var sourceText = SourceText.From(builder.ToString(), Encoding.UTF8);
        context.AddSource(benchmarkData.Name + "_Benchmarks.g.cs", sourceText);
    }

    private static bool Filter(SyntaxNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (node is not MethodDeclarationSyntax method || (method.Body is null && method.ExpressionBody is null))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (method.Parent is null)
        {
            return false;
        }

        if (method.Parent is not ClassDeclarationSyntax @class || !@class.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return @class.Parent is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax or CompilationUnitSyntax;
    }

    private BenchmarkData Converter(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (@namespace, name) = CollectClassName((ClassDeclarationSyntax)context.TargetNode.Parent!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Attributes.Length == 0)
        {
            return new(@namespace, name, [], []);
        }

        var (templateDatas, aliases) = CollectTemplateData(context, cancellationToken);
        return new(@namespace, name, templateDatas, aliases);
    }

    private static (ImmutableArray<TemplateData>, ImmutableArray<string>) CollectTemplateData(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var rewriter = new Rewriter();
        var answer = new TemplateData[context.Attributes.Length];
        int answerIndex = 0;
        foreach (var attributeData in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attributeData.ConstructorArguments.Length == 0)
            {
                throw new InvalidDataException();
            }

            rewriter.From = "global";
            bool baseLine = default;
            string? description = default;
            string? methodName = default;
            int operationsPerInvoke = default;
            var commitIdTypedContant = attributeData.ConstructorArguments[0];
            if (commitIdTypedContant.Kind != TypedConstantKind.Primitive || commitIdTypedContant.IsNull)
            {
                throw new InvalidDataException();
            }

            rewriter.To = (string)commitIdTypedContant.Value!;
            if (string.IsNullOrWhiteSpace(rewriter.To))
            {
                throw new InvalidDataException();
            }

            foreach (var argument in attributeData.NamedArguments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (argument.Value.Kind != TypedConstantKind.Primitive)
                {
                    throw new InvalidDataException();
                }

                switch (argument.Key)
                {
                    case nameof(TemplateData.Alias):
                        if (argument.Value.IsNull)
                        {
                            throw new InvalidDataException();
                        }

                        rewriter.From = (string)argument.Value.Value!;
                        break;
                    case nameof(TemplateData.BaseLine):
                        if (argument.Value.IsNull)
                        {
                            throw new InvalidDataException();
                        }

                        baseLine = (bool)argument.Value.Value!;
                        break;
                    case nameof(TemplateData.Description):
                        description = argument.Value.Value as string;
                        break;
                    case nameof(TemplateData.OperationsPerInvoke):
                        if (argument.Value.IsNull)
                        {
                            throw new InvalidDataException();
                        }

                        operationsPerInvoke = (int)argument.Value.Value!;
                        break;
                    case nameof(TemplateData.MethodName):
                        if (argument.Value.IsNull)
                        {
                            throw new InvalidDataException();
                        }

                        methodName = (string)argument.Value.Value!;
                        break;
                }
            }

            var rewrittenText = rewriter.Visit(context.TargetNode).ToString();
            if (string.IsNullOrWhiteSpace(methodName))
            {
                rewriter.MethodName = (((MethodDeclarationSyntax)context.TargetNode).Identifier.Text) + rewriter.To;
            }
            else
            {
                rewriter.MethodName = methodName!;
            }

            answer[answerIndex++] = new(rewriter.To, rewriter.MethodName, rewriter.From, baseLine, description, operationsPerInvoke, rewrittenText);
        }

        Array.Sort(answer);
        rewriter.Aliases.Remove("global");
        return (ImmutableArray.Create(answer), [.. rewriter.Aliases]);
    }

    private (string? Namespace, string Name) CollectClassName(ClassDeclarationSyntax @class, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = @class.Identifier.ToString();
        switch (@class.Parent)
        {
            case CompilationUnitSyntax:
                return (default, name);
            case FileScopedNamespaceDeclarationSyntax fileScoped:
                return (fileScoped.Name.ToString(), name);
        }

        var syntax = (NamespaceDeclarationSyntax)@class.Parent!;
        var @namespace = syntax.Name.ToString();
        while (syntax.Parent is NamespaceDeclarationSyntax nextSyntax)
        {
            cancellationToken.ThrowIfCancellationRequested();
            @namespace = nextSyntax.Name.ToString() + "." + @namespace;
            syntax = nextSyntax;
        }

        return (@namespace, name);
    }
}
