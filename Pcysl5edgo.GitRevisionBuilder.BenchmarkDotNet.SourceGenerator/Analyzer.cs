using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.SourceGenerator;

/// <summary>
/// Analyzer for validating usage of <see cref="Generator.FullyQualifiedMetadataName"/> attributed benchmark methods.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Analyzer : DiagnosticAnalyzer
{
#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor descriptorMethodMustHaveBody = new("PGBS001", "Benchmark method must have body or expression body", "Benchmark method must have body or expression body", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor descriptorContainerMustBeClass = new("PGBS002", "Benchmark method must be contained in non-generic class", "Benchmark method must be contained in non-generic class", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor descriptorContainingClassMustBePartial = new("PGBS003", "Class containing benchmark method must be partial", "Class containing benchmark method must be partial", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor descriptorContainingClassMustNotBeContained = new("PGBS004", "Class containing benchmark method must not be contained by other type", "Class containing benchmark method must not be contained by other type", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor descriptorCommitIdMustNotBeNullOrWhitespace = new("PGBS005", "CommitId must not be null nor whitespace", "CommitId must not be null nor whitespace", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor descriptorCommitIdIsInvalid = new("PGBS006", "CommitId must be sha", "CommitId must be sha", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor descriptorAliasNeverAppear = new("PGBS007", "Alias never appears", "Alias never appears: {0}", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);
#pragma warning restore RS2008

    /// <summary>
    /// Gets the diagnostics supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [
        descriptorMethodMustHaveBody,
        descriptorContainerMustBeClass,
        descriptorContainingClassMustBePartial,
        descriptorContainingClassMustNotBeContained,
        descriptorCommitIdMustNotBeNullOrWhitespace,
        descriptorCommitIdIsInvalid,
        descriptorAliasNeverAppear,
    ];

    /// <summary>
    /// Initialize analyzer execution.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(ForAttributeWithMetadataName);
    }

    private static void ForAttributeWithMetadataName(CompilationStartAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var attributes = context.Compilation.GetTypesByMetadataName(Generator.FullyQualifiedMetadataName);
        if (attributes.IsDefaultOrEmpty)
        {
            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        context.RegisterSyntaxNodeAction(context => ValidateAllMethod(context, attributes), ImmutableArray.Create(SyntaxKind.MethodDeclaration));
    }

    private static void ValidateAllMethod(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> attributes)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var method = (IMethodSymbol)context.ContainingSymbol!;
        var attributeDatas = method.GetAttributes();
        if (attributeDatas.IsDefaultOrEmpty)
        {
            return;
        }

        var shouldCheckGlobalAlias = false;
        var otherAliases = default(HashSet<string>);
        var isFirstCheck = true;
        foreach (var attributeData in attributeDatas)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!Any(attributes, attributeData.AttributeClass, context.CancellationToken))
            {
                continue;
            }

            if (isFirstCheck)
            {
                isFirstCheck = false;
                if (method.IsPartialDefinition || method.IsAbstract)
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptorMethodMustHaveBody, method.Locations[0]));
                    return;
                }

                context.CancellationToken.ThrowIfCancellationRequested();
                var @class = method.ContainingType;
                if (@class.IsStatic || @class.IsAbstract || @class.IsValueType || @class.IsRecord || @class.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptorContainerMustBeClass, @class.Locations[0]));
                    return;
                }

                context.CancellationToken.ThrowIfCancellationRequested();
                if (@class.ContainingType != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptorContainingClassMustNotBeContained, @class.Locations[0]));
                    return;
                }

                context.CancellationToken.ThrowIfCancellationRequested();
                if (!IsPartialClass(@class, context.CancellationToken))
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptorContainingClassMustBePartial, @class.Locations[0]));
                    return;
                }
            }

            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var arg = attributeData.ConstructorArguments[0];
                if (arg.IsNull || arg.Kind != TypedConstantKind.Primitive || arg.Value is not string commitId || string.IsNullOrWhiteSpace(commitId))
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptorCommitIdMustNotBeNullOrWhitespace, attributeData.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation() ?? method.Locations[0]));
                    return;
                }
                else if (!IsValidShaHash(commitId))
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptorCommitIdIsInvalid, attributeData.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation() ?? method.Locations[0]));
                    return;
                }
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            var alias = "global";
            foreach (var arg in attributeData.NamedArguments)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (arg.Key == "Alias" && !arg.Value.IsNull)
                {
                    alias = (string)arg.Value.Value!;
                    break;
                }
            }

            if (alias == "global")
            {
                shouldCheckGlobalAlias = true;
            }
            else
            {
                (otherAliases ??= []).Add(alias);
            }
        }

        if (isFirstCheck)
        {
            return;
        }

        Debug.Assert(shouldCheckGlobalAlias || otherAliases != null);
        var codeBlock = TryGetCodeBlock(method, context.CancellationToken);
        if (codeBlock == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptorMethodMustHaveBody, method.Locations[0]));
            return;
        }

        foreach (var node in codeBlock.DescendantNodes().OfType<AliasQualifiedNameSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (shouldCheckGlobalAlias)
            {
                if (node.Alias.Identifier.Text == "global")
                {
                    shouldCheckGlobalAlias = false;
                }
            }
            else
            {
                otherAliases?.Remove(node.Alias.Identifier.Text);
            }

            if (shouldCheckGlobalAlias == false && (otherAliases == null || otherAliases.Count == 0))
            {
                break;
            }
        }

        if (shouldCheckGlobalAlias)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptorAliasNeverAppear, method.Locations[0], ["global"]));
        }

        if (otherAliases != null && otherAliases.Count > 0)
        {
            foreach (var alias in otherAliases)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                context.ReportDiagnostic(Diagnostic.Create(descriptorAliasNeverAppear, method.Locations[0], [alias]));
            }
        }
    }

    private static SyntaxNode? TryGetCodeBlock(IMethodSymbol method, CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (syntaxReference.GetSyntax(cancellationToken) is not MethodDeclarationSyntax declarationSyntax)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (declarationSyntax.Body != null)
            {
                return declarationSyntax.Body;
            }
            else if (declarationSyntax.ExpressionBody != null)
            {
                return declarationSyntax.ExpressionBody.Expression;
            }
        }

        return default;
    }

    private static bool Any(ImmutableArray<INamedTypeSymbol> comparison, INamedTypeSymbol? target, CancellationToken cancellationToken)
    {
        if (target == null)
        {
            return false;
        }

        foreach (var type in comparison)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(type, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartialClass(INamedTypeSymbol symbol, CancellationToken cancellationToken)
    {
        var refs = symbol.DeclaringSyntaxReferences;
        if (refs.Length != 1)
        {
            return true;
        }

        var node = (ClassDeclarationSyntax)refs[0].GetSyntax(cancellationToken);
        return node.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// Determines whether the specified string is a valid SHA hash consisting only of hexadecimal characters.
    /// </summary>
    /// <param name="hash">The string to validate as a SHA hash. May be empty or contain any characters.</param>
    /// <returns>true if the string is valid SHA-1 or SHA-256.</returns>
    private static bool IsValidShaHash(string hash)
    {
        if (hash.Length == 0 || hash.Length > 64)
        {
            return false;
        }

        foreach (var item in hash)
        {
            if ((item >= '0' && item <= '9') || (item >= 'A' && item <= 'F') || (item >= 'a' && item <= 'f'))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
