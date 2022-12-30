using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Tmds.DBus.Protocol;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static CompilationUnitSyntax MakeDBusInterfaceAttribute() => MakeCompilationUnit(
            NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(
                    ClassDeclaration("DBusInterfaceAttribute")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddBaseListTypes(SimpleBaseType(IdentifierName("Attribute")))
                        .AddAttributeLists(
                            AttributeList()
                                .AddAttributes(
                                    Attribute(IdentifierName("AttributeUsage"))
                                        .AddArgumentListArguments(
                                            AttributeArgument(
                                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("AttributeTargets"), IdentifierName("Class"))))))
                        .AddMembers(
                            ConstructorDeclaration("DBusInterfaceAttribute")
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("serviceName")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithBody(
                                    Block().AddStatements(MakeAssignmentExpressionStatement("ServiceName", "serviceName"))),
                            MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "ServiceName", Token(SyntaxKind.PublicKeyword)))));

        private static CompilationUnitSyntax MakeDBusObjectClass() => MakeCompilationUnit(
            NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(
                    ClassDeclaration("DBusObject")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddMembers(
                            ConstructorDeclaration("DBusObject")
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("connection")).WithType(ParseTypeName(nameof(Connection))),
                                    Parameter(Identifier("destination")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                                    Parameter(Identifier("path")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithBody(
                                    Block(
                                        MakeAssignmentExpressionStatement("Connection", "connection"),
                                        MakeAssignmentExpressionStatement("Destination", "destination"),
                                        MakeAssignmentExpressionStatement("Path", "path"))),
                            MakeGetOnlyProperty(ParseTypeName(nameof(Connection)), "Connection", Token(SyntaxKind.ProtectedKeyword)),
                            MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "Destination", Token(SyntaxKind.ProtectedKeyword)),
                            MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "Path", Token(SyntaxKind.ProtectedKeyword)))
                ));

        private static CompilationUnitSyntax MakePropertyChangesClass() => MakeCompilationUnit(
            NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(
                    RecordDeclaration(Token(SyntaxKind.RecordKeyword), "PropertyChanges")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddTypeParameterListParameters(TypeParameter(Identifier("TProperties")))
                        .AddParameterListParameters(
                            Parameter(Identifier("Properties"))
                                .WithType(IdentifierName("TProperties")),
                            Parameter(Identifier("Invalidated"))
                                .WithType(ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword))).AddRankSpecifiers(ArrayRankSpecifier())),
                            Parameter(Identifier("Changed"))
                                .WithType(ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword))).AddRankSpecifiers(ArrayRankSpecifier())))
                        .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                        .AddMembers(
                            MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier("HasChanged"))
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("property")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithExpressionBody(
                                    ArrowExpressionClause(
                                        BinaryExpression(SyntaxKind.NotEqualsExpression,
                                            InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("Array"), IdentifierName("IndexOf")))
                                                .AddArgumentListArguments(
                                                    Argument(IdentifierName("Changed")),
                                                    Argument(IdentifierName("property"))),
                                            MakeLiteralExpression(-1))))
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                            MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier("IsInvalidated"))
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("property")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithExpressionBody(
                                    ArrowExpressionClause(
                                        BinaryExpression(SyntaxKind.NotEqualsExpression,
                                            InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("Array"), IdentifierName("IndexOf")))
                                                .AddArgumentListArguments(
                                                    Argument(IdentifierName("Invalidated")),
                                                    Argument(IdentifierName("property"))),
                                            MakeLiteralExpression(-1))))
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))
                        .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))));
    }
}
