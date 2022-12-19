using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Tmds.DBus.Protocol;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static ClassDeclarationSyntax GenerateHandler(ClassDeclarationSyntax declaration, string interfaceName)
        {
            ClassDeclarationSyntax cl = ClassDeclaration(declaration.Identifier)
                .WithModifiers(declaration.Modifiers)
                .AddBaseListTypes(SimpleBaseType(ParseTypeName(nameof(IMethodHandler))));

            MethodDeclarationSyntax runMethodHandlerSynchronously = MethodDeclaration(ParseTypeName("bool"), Identifier(nameof(IMethodHandler.RunMethodHandlerSynchronously)))
                .AddParameterListParameters(
                    Parameter(Identifier("message")).WithType(ParseTypeName(nameof(Message))))
                .WithExpressionBody(
                    ArrowExpressionClause(
                        LiteralExpression(SyntaxKind.TrueLiteralExpression)));

            MethodDeclarationSyntax handleMethodAsync = MethodDeclaration(ParseTypeName(nameof(ValueTask)), Identifier(nameof(IMethodHandler.HandleMethodAsync)))
                    .AddParameterListParameters(
                        Parameter(Identifier("context")).WithType(ParseTypeName(nameof(MethodContext))))
                    .WithBody(
                        Block(
                            IfStatement(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("context"), IdentifierName(nameof(MethodContext.Request))), IdentifierName(nameof(Message.InterfaceAsString))),
                                    MakeLiteralExpression(interfaceName)),
                                ReturnStatement(
                                    LiteralExpression(
                                        SyntaxKind.DefaultLiteralExpression,
                                        Token(SyntaxKind.DefaultKeyword)))),
                            SwitchStatement(
                                    TupleExpression(
                                        SeparatedList<ArgumentSyntax>(
                                            new SyntaxNodeOrToken[]{
                                                Argument(
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("request"), IdentifierName(nameof(Message.MemberAsString)))),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("request"), IdentifierName(nameof(Message.SignatureAsString))))})))
                                .AddSections()));


            return cl.AddMembers(runMethodHandlerSynchronously);
        }
    }
}
