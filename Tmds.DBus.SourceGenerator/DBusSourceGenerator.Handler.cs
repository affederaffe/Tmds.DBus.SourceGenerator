/*using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static ClassDeclarationSyntax GenerateHandler(ClassDeclarationSyntax declaration, string interfaceName)
        {
            ClassDeclarationSyntax cl = ClassDeclaration(declaration.Identifier)
                .WithModifiers(declaration.Modifiers)
                .AddBaseListTypes(SimpleBaseType(ParseTypeName("IMethodHandler")));

            MethodDeclarationSyntax runMethodHandlerSynchronously = MethodDeclaration(ParseTypeName("bool"), Identifier("IMethodHandler.RunMethodHandlerSynchronously)))
                .AddParameterListParameters(
                    Parameter(Identifier("message")).WithType(ParseTypeName("Message))))
                .WithExpressionBody(
                    ArrowExpressionClause(
                        LiteralExpression(SyntaxKind.TrueLiteralExpression)));

            MethodDeclarationSyntax handleMethodAsync = MethodDeclaration(ParseTypeName("ValueTask)), Identifier("IMethodHandler.HandleMethodAsync)))
                    .AddParameterListParameters(
                        Parameter(Identifier("context")).WithType(ParseTypeName("MethodContext))))
                    .WithBody(
                        Block(
                            IfStatement(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("context"), IdentifierName("MethodContext.Request))), IdentifierName("Message.InterfaceAsString))),
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
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("request"), IdentifierName("Message.MemberAsString)))),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("request"), IdentifierName("Message.SignatureAsString))))})))
                                .AddSections()));


            return cl.AddMembers(runMethodHandlerSynchronously);
        }
    }
}
*/
