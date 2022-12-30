using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<string, string> _readMethodForSignature = new();
        private static readonly Dictionary<string, string> _writeMethodForSignature = new();

        private static TypeDeclarationSyntax? GenerateProxy(SemanticModel semanticModel, TypeDeclarationSyntax declaration, AttributeData attributeData)
        {
            if (attributeData.ConstructorArguments.Length != 1 || attributeData.ConstructorArguments[0].Value is not string @interface)
                return null;

            ClassDeclarationSyntax cl = ClassDeclaration(declaration.Identifier)
                .WithModifiers(GetAccessibilityModifiers(declaration.Modifiers));

            FieldDeclarationSyntax interfaceConst = MakePrivateStringConst("Interface", @interface, PredefinedType(Token(SyntaxKind.StringKeyword)));
            FieldDeclarationSyntax connectionField = MakePrivateReadOnlyField("_connection", ParseTypeName(nameof(Connection)));
            FieldDeclarationSyntax destinationField = MakePrivateReadOnlyField("_destination", PredefinedType(Token(SyntaxKind.StringKeyword)));
            FieldDeclarationSyntax pathField = MakePrivateReadOnlyField("_path", PredefinedType(Token(SyntaxKind.StringKeyword)));

            ConstructorDeclarationSyntax ctor = ConstructorDeclaration(declaration.Identifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("connection")).WithType(ParseTypeName(nameof(Connection))),
                    Parameter(Identifier("destination")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                    Parameter(Identifier("path")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                .WithBody(
                    Block(
                        MakeAssignmentExpressionStatement("_connection", "connection"),
                        MakeAssignmentExpressionStatement("_destination", "destination"),
                        MakeAssignmentExpressionStatement("_path", "path")));

            cl = cl.AddMembers(interfaceConst, connectionField, destinationField, pathField, ctor);

            MethodDeclarationSyntax[] methodDeclarations = declaration.Members.OfType<MethodDeclarationSyntax>().ToArray();

            IEnumerable<MethodDeclarationSyntax> methods = methodDeclarations.Where(static x => !x.Identifier.Text.StartsWith("Watch", StringComparison.Ordinal));
            cl = AddMethods(cl, semanticModel, methods);

            IEnumerable<MethodDeclarationSyntax> signals = methodDeclarations.Where(static x => x.Identifier.Text.StartsWith("Watch", StringComparison.Ordinal));
            cl = AddSignals(cl, semanticModel, signals);

            ClassDeclarationSyntax? properties = declaration.Members.OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(static x => x.Identifier.Text.EndsWith("Properties", StringComparison.Ordinal));
            if (properties is not null)
                cl = AddProperties(cl, semanticModel, properties);

            return cl;
        }

        private static ClassDeclarationSyntax AddMethods(ClassDeclarationSyntax cl, SemanticModel semanticModel, IEnumerable<MethodDeclarationSyntax> methodDeclarations)
        {
            foreach (MethodDeclarationSyntax methodDeclaration in methodDeclarations)
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
                                Argument(IdentifierName("_destination")),
                                Argument(IdentifierName("_path")),
                                Argument(IdentifierName("Interface")),
                                Argument(MakeLiteralExpression(methodName)),
                                Argument(MakeLiteralExpression(ParseSignature(semanticModel, methodDeclaration))))));

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

                string? returnType = ParseReadMethod(semanticModel, methodDeclaration.ReturnType);

                ParenthesizedLambdaExpressionSyntax? messageValueReaderLambda = returnType is null
                    ? null
                    : ParenthesizedLambdaExpression()
                        .AddParameterListParameters(
                            Parameter(Identifier("message")).WithType(ParseTypeName(nameof(Message))),
                            Parameter(Identifier("state")).WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                        . WithExpressionBody(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("message"), IdentifierName(nameof(Message.GetBodyReader)))),
                                    IdentifierName(returnType))));

                ArgumentListSyntax args = ArgumentList(SingletonSeparatedList(Argument(InvocationExpression(IdentifierName(createMethodIdentifier)))));
                if (messageValueReaderLambda is not null)
                    args = args.AddArguments(Argument(messageValueReaderLambda));

                MethodDeclarationSyntax proxyMethod = MethodDeclaration(methodDeclaration.ReturnType, methodDeclaration.Identifier)
                    .WithModifiers(GetAccessibilityModifiers(methodDeclaration.Modifiers))
                    .WithParameterList(methodDeclaration.ParameterList)
                    .WithBody(
                        Block(
                            ReturnStatement(
                                InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"),
                                            IdentifierName(nameof(Connection.CallMethodAsync))))
                                    .WithArgumentList(args)),
                            LocalFunctionStatement(ParseTypeName(nameof(MessageBuffer)), createMethodIdentifier)
                                .WithBody(createMessageBody)));

                cl = cl.AddMembers(proxyMethod);
            }

            return cl;
        }

        private static ClassDeclarationSyntax AddSignals(ClassDeclarationSyntax cl, SemanticModel semanticModel, IEnumerable<MethodDeclarationSyntax> signals)
        {


            return cl;
        }

        private static ClassDeclarationSyntax AddProperties(ClassDeclarationSyntax cl, SemanticModel semanticModel, ClassDeclarationSyntax propertiesDeclaration)
        {
            foreach (PropertyDeclarationSyntax propertyDeclaration in propertiesDeclaration.Members.OfType<PropertyDeclarationSyntax>())
            {
                
            }

            return AddGetAllMethod(cl, semanticModel, propertiesDeclaration);
        }

        private static ClassDeclarationSyntax AddGetAllMethod(ClassDeclarationSyntax cl, SemanticModel semanticModel, ClassDeclarationSyntax propertiesDeclaration)
        {
            BlockSyntax createGetAllMessageBody = Block().AddStatements(
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
                            Argument(IdentifierName("_destination")),
                            Argument(IdentifierName("_path")),
                            Argument(MakeLiteralExpression("org.freedesktop.DBus.Properties")),
                            Argument(MakeLiteralExpression("GetAll")),
                            Argument(MakeLiteralExpression("s")))),
                ExpressionStatement(
                    InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteString")))
                        .AddArgumentListArguments(Argument(IdentifierName("Interface")))),
                ReturnStatement(
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"),
                            IdentifierName(nameof(MessageWriter.CreateMessage))))));

            SyntaxList<SwitchSectionSyntax> switchSections = List<SwitchSectionSyntax>();
            foreach (PropertyDeclarationSyntax propertyDeclaration in propertiesDeclaration.Members.OfType<PropertyDeclarationSyntax>())
            {
                IPropertySymbol? declaredProperty = semanticModel.GetDeclaredSymbol(propertyDeclaration);
                if (declaredProperty is null) continue;
                string signature = ParseTypeForSignature(declaredProperty.Type, semanticModel);
                string readMethod = $"Read{ParseTypeForReadWriteMethod(declaredProperty.Type, semanticModel)}";
                switchSections = switchSections.Add(
                    SwitchSection()
                        .AddLabels(CaseSwitchLabel(MakeLiteralExpression(declaredProperty.Name)))
                        .AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName(nameof(Reader.ReadSignature))))
                                .AddArgumentListArguments(Argument(MakeLiteralExpression(signature)))),
                            ExpressionStatement(
                                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("props"), IdentifierName(declaredProperty.Name)),
                                    InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName(readMethod))))),
                            BreakStatement()));
            }

            ParenthesizedLambdaExpressionSyntax messageValueReaderLambda = ParenthesizedLambdaExpression()
                .AddParameterListParameters(
                    Parameter(Identifier("message")).WithType(ParseTypeName(nameof(Message))),
                    Parameter(Identifier("state")).WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                .WithBody(
                    Block(
                        LocalDeclarationStatement(VariableDeclaration(ParseTypeName(nameof(Reader)))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("message"),
                                                    IdentifierName(nameof(Message.GetBodyReader)))))))),
                        LocalDeclarationStatement(VariableDeclaration(ParseTypeName(propertiesDeclaration.Identifier.Text))
                            .AddVariables(
                                VariableDeclarator("props")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                ObjectCreationExpression(ParseTypeName(propertiesDeclaration.Identifier.Text))))))),
                        LocalDeclarationStatement(VariableDeclaration(ParseTypeName(nameof(ArrayEnd)))
                            .AddVariables(
                                VariableDeclarator("headersEnd")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName(nameof(Reader.ReadArrayStart))))
                                                .AddArgumentListArguments(
                                                    Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(nameof(DBusType)), IdentifierName(nameof(DBusType.Struct))))))))),
                        WhileStatement(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName(nameof(Reader.HasNext))))
                                .AddArgumentListArguments(Argument(IdentifierName("headersEnd"))),
                            Block(
                                SwitchStatement(
                                    InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName(nameof(Reader.ReadString)))))
                                    .WithSections(switchSections))),
                        ReturnStatement(IdentifierName("props"))));

            return cl.AddMembers(
                MethodDeclaration(ParseTypeName($"Task<{propertiesDeclaration.Identifier.Text}>"), "GetAllAsync")
                    .WithModifiers(GetAccessibilityModifiers(propertiesDeclaration.Modifiers))
                    .WithBody(
                        Block(
                            ReturnStatement(
                                InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"), IdentifierName(nameof(Connection.CallMethodAsync))))
                                    .AddArgumentListArguments(
                                        Argument(InvocationExpression(IdentifierName("CreateGetAllMessage"))),
                                        Argument(messageValueReaderLambda))),
                            LocalFunctionStatement(ParseTypeName(nameof(MessageBuffer)), "CreateGetAllMessage")
                                .WithBody(createGetAllMessageBody))
                        )
                    )
            ;
        }
    }
}
