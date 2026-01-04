using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pcysl5edgo.GitRevisionBuilder.BenchmarkDotNet.SourceGenerator;

internal sealed class Rewriter : CSharpSyntaxRewriter
{
    public string From = "global";
    public string To = "global";

    public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
    {
        var visited = (AliasQualifiedNameSyntax)base.VisitAliasQualifiedName(node)!;
        if (node.Alias.Identifier.Text == From)
        {
            var newAlias = SyntaxFactory.IdentifierName(To).WithTriviaFrom(visited.Alias);
            return visited.WithAlias(newAlias);
        }

        return visited;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        return visited
            .WithAttributeLists(default)
            .WithIdentifier(SyntaxFactory.Identifier(node.Identifier.Text + '_' + To).WithTriviaFrom(node.Identifier));
    }
}
