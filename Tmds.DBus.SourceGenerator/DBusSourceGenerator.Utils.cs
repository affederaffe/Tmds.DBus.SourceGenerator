using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static CompilationUnitSyntax MakeCompilationUnit(NamespaceDeclarationSyntax namespaceDeclaration) =>
            CompilationUnit()
                .AddUsings(
                    UsingDirective(IdentifierName("System")),
                    UsingDirective(IdentifierName("System.Collections.Generic")),
                    UsingDirective(IdentifierName("System.Runtime.InteropServices")),
                    UsingDirective(IdentifierName("System.Threading.Tasks")),
                    UsingDirective(IdentifierName("Tmds.DBus.Protocol")),
                    UsingDirective(IdentifierName("Tmds.DBus.SourceGenerator")))
                .AddMembers(namespaceDeclaration
                    .WithLeadingTrivia(
                        TriviaList(
                            Comment("// <auto-generated/>"),
                            Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)),
                            Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true)))
                    )
                ).NormalizeWhitespace();

        private static FieldDeclarationSyntax MakePrivateStringConst(string identifier, string value, TypeSyntax type) =>
            FieldDeclaration(VariableDeclaration(type)
                    .AddVariables(VariableDeclarator(identifier)
                        .WithInitializer(EqualsValueClause(MakeLiteralExpression(value)))))
                .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ConstKeyword));

        private static FieldDeclarationSyntax MakePrivateReadOnlyField(string identifier, TypeSyntax type) =>
            FieldDeclaration(VariableDeclaration(type)
                    .AddVariables(VariableDeclarator(identifier)))
                .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword));

        private static PropertyDeclarationSyntax MakeGetOnlyProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
            PropertyDeclaration(type, identifier)
                .AddModifiers(modifiers)
                .AddAccessorListAccessors(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        private static PropertyDeclarationSyntax MakeGetSetProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
            PropertyDeclaration(type, identifier)
                .AddModifiers(modifiers)
                .AddAccessorListAccessors(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                    AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)
                        ));

        private static ExpressionStatementSyntax MakeAssignmentExpressionStatement(string left, string right) =>
            ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(left),
                    IdentifierName(right)));

        private static LiteralExpressionSyntax MakeLiteralExpression(string literal) => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal));

        private static LiteralExpressionSyntax MakeLiteralExpression(int literal) => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(literal));

        private static SyntaxTokenList GetAccessibilityModifiers(SyntaxTokenList modifiers) => TokenList(
            modifiers.Where(static x =>
                x.IsKind(SyntaxKind.PublicKeyword) || x.IsKind(SyntaxKind.InternalKeyword) || x.IsKind(SyntaxKind.PrivateKeyword) || x.IsKind(SyntaxKind.ProtectedKeyword)));

        private static string TupleOf(IEnumerable<string> elements) => $"({string.Join(", ", elements)})";
    }
}
