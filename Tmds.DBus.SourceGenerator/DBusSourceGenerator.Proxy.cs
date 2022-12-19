using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

using Tmds.DBus.Protocol;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static TypeDeclarationSyntax? GenerateProxy(SemanticModel semanticModel, TypeDeclarationSyntax declaration, AttributeData attributeData)
        {
            if (attributeData.ConstructorArguments.Length != 2 || attributeData.ConstructorArguments[0].Value is not string path || attributeData.ConstructorArguments[1].Value is not string @interface)
                return null;
            ClassDeclarationSyntax cl = ClassDeclaration(declaration.Identifier)
                .WithModifiers(declaration.Modifiers);

            FieldDeclarationSyntax connectionField = FieldDeclaration(VariableDeclaration(ParseTypeName(nameof(Connection)))
                    .AddVariables(VariableDeclarator("_connection")))
                .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword));

            ConstructorDeclarationSyntax ctor = ConstructorDeclaration(declaration.Identifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("connection")).WithType(ParseTypeName(nameof(Connection))))
                .WithBody(Block(
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName("_connection"),
                            IdentifierName("connection")))));

            cl = cl.AddMembers(connectionField, ctor);
            return AddMethods(cl, semanticModel, declaration, path, @interface);
        }

        private static ClassDeclarationSyntax AddMethods(ClassDeclarationSyntax cl, SemanticModel semanticModel, TypeDeclarationSyntax declaration, string path, string @interface)
        {
            foreach (MethodDeclarationSyntax methodDeclaration in declaration.Members.OfType<MethodDeclarationSyntax>())
            {
                string methodName = methodDeclaration.Identifier.Text.Substring(0, methodDeclaration.Identifier.Text.Length - 5);
                string createMethodIdentifier = $"Create{methodName}Message";

                BlockSyntax createMessageBody = Block().AddStatements(
                    LocalDeclarationStatement(VariableDeclaration(ParseTypeName(nameof(MessageWriter)),
                        SingletonSeparatedList(
                            VariableDeclarator("writer")
                                .WithInitializer(EqualsValueClause(
                                    InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"), IdentifierName(nameof(Connection.GetMessageWriter)))))))))
                    .WithUsingKeyword(Token(SyntaxKind.UsingKeyword)),
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName(nameof(MessageWriter.WriteMethodCallHeader))))
                            .AddArgumentListArguments(
                                Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"), IdentifierName(nameof(Connection.UniqueName)))),
                                Argument(MakeLiteralExpression(path)),
                                Argument(MakeLiteralExpression(@interface)),
                                Argument(MakeLiteralExpression(ParseSignature(semanticModel, methodDeclaration))),
                                Argument(MakeLiteralExpression(methodName)))));

                foreach (ParameterSyntax parameter in methodDeclaration.ParameterList.Parameters)
                {
                    if (parameter.Type is null)
                        throw new ArgumentException("Cannot parse parameter.");
                    string? writeMethod = ParseWriteMethod(semanticModel, parameter);
                    if (writeMethod is null)
                        throw new Exception($"WriteMethod is null {parameter.Type}");
                    createMessageBody = createMessageBody.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName(writeMethod)))
                                .AddArgumentListArguments(Argument(IdentifierName(parameter.Identifier)))));
                }

                createMessageBody = createMessageBody.AddStatements(
                    ReturnStatement(
                        InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"),
                                IdentifierName(nameof(MessageWriter.CreateMessage))))));

                ParenthesizedLambdaExpressionSyntax messageValueReaderLambda = ParenthesizedLambdaExpression()
                    .AddParameterListParameters(
                        Parameter(Identifier("message")).WithType(ParseTypeName(nameof(Message))),
                        Parameter(Identifier("state")).WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))));

                string? returnType = ParseReadMethod(semanticModel, methodDeclaration.ReturnType);
                    messageValueReaderLambda = returnType is null
                        ? messageValueReaderLambda.WithBody(Block())
                        : messageValueReaderLambda.WithExpressionBody(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("message"), IdentifierName(nameof(Message.GetBodyReader)))),
                                    IdentifierName(returnType))));

                MethodDeclarationSyntax proxyMethod = MethodDeclaration(methodDeclaration.ReturnType, methodDeclaration.Identifier)
                    .WithModifiers(methodDeclaration.Modifiers)
                    .WithParameterList(methodDeclaration.ParameterList)
                    .WithBody(Block(
                    ReturnStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"),
                                    IdentifierName(nameof(Connection.CallMethodAsync))))
                            .AddArgumentListArguments(
                                Argument(
                                    InvocationExpression(IdentifierName(createMethodIdentifier))),
                                Argument(messageValueReaderLambda))),
                    LocalFunctionStatement(ParseTypeName(nameof(MessageBuffer)), createMethodIdentifier)
                        .WithBody(createMessageBody)));

                cl = cl.AddMembers(proxyMethod);
            }

            return cl;
        }
    }
}
