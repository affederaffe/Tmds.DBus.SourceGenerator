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
        private TypeDeclarationSyntax GenerateProxy(TypeDeclarationSyntax declaration, DBusInterface dBusInterface)
        {
            ClassDeclarationSyntax cl = ClassDeclaration(declaration.Identifier)
                .WithModifiers(declaration.Modifiers);

            FieldDeclarationSyntax interfaceConst = MakePrivateStringConst("Interface", dBusInterface.Name!, PredefinedType(Token(SyntaxKind.StringKeyword)));
            FieldDeclarationSyntax connectionField = MakePrivateReadOnlyField("_connection", ParseTypeName("Connection"));
            FieldDeclarationSyntax destinationField = MakePrivateReadOnlyField("_destination", PredefinedType(Token(SyntaxKind.StringKeyword)));
            FieldDeclarationSyntax pathField = MakePrivateReadOnlyField("_path", PredefinedType(Token(SyntaxKind.StringKeyword)));

            ConstructorDeclarationSyntax ctor = ConstructorDeclaration(declaration.Identifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("connection")).WithType(ParseTypeName("Connection")),
                    Parameter(Identifier("destination")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                    Parameter(Identifier("path")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                .WithBody(
                    Block(
                        MakeAssignmentExpressionStatement("_connection", "connection"),
                        MakeAssignmentExpressionStatement("_destination", "destination"),
                        MakeAssignmentExpressionStatement("_path", "path")));

            cl = cl.AddMembers(interfaceConst, connectionField, destinationField, pathField, ctor);

            if (dBusInterface.Methods is not null)
                cl = AddMethods(cl, dBusInterface.Methods);
            if (dBusInterface.Properties is not null)
                cl = AddProperties(cl, dBusInterface);

            return cl;
        }

        private ClassDeclarationSyntax AddMethods(ClassDeclarationSyntax cl, IEnumerable<DBusMethod>? methods) => methods is null ? cl : methods.Aggregate(cl, AddMethod);

        private ClassDeclarationSyntax AddMethod(ClassDeclarationSyntax cl, DBusMethod dBusMethod)
        {
            string createMethodIdentifier = $"Create{dBusMethod.Name}Message";
                DBusArgument[]? inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
                DBusArgument[]? outArgs = dBusMethod.Arguments?.Where(static m => m.Direction == "out").ToArray();

                string returnType = ParseTaskReturnType(outArgs);

                ArgumentListSyntax args = ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            InvocationExpression(
                                IdentifierName(createMethodIdentifier)))));

                if (outArgs?.Length > 0)
                    args = args.AddArguments(
                        Argument(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("ReaderExtensions"), IdentifierName(GetOrAddReadMessageMethod(outArgs)))));

                BlockSyntax createMessageBody = MakeCreateMessageBody(IdentifierName("Interface"), dBusMethod.Name!, ParseSignature(inArgs));
                if (inArgs is not null)
                    createMessageBody = createMessageBody.WithStatements(
                        createMessageBody.Statements.AddRange(
                            inArgs.Select(static (x, i) => ExpressionStatement(
                                InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"),
                                            IdentifierName($"Write{ParseReadWriteMethod(x)}")))
                                    .AddArgumentListArguments(
                                        Argument(IdentifierName(x.Name ?? $"arg{i}")))))));
                createMessageBody = AddCreateMessageBodyReturnStatement(createMessageBody);

                MethodDeclarationSyntax proxyMethod = MethodDeclaration(ParseTypeName(returnType), $"{dBusMethod.Name}Async")
                    .WithModifiers(GetAccessibilityModifiers(cl.Modifiers));

                if (inArgs is not null)
                    proxyMethod = proxyMethod
                        .WithParameterList(
                            ParameterList(
                                SeparatedList(
                                    inArgs.Select(static (x, i) =>
                                        Parameter(Identifier(x.Name ?? $"arg{i}")).WithType(ParseTypeName(x.DotNetType))))));

                proxyMethod = proxyMethod.WithBody(MakeCallMethodReturnBody(args, createMessageBody, createMethodIdentifier));

                return cl.AddMembers(proxyMethod);
        }

        private static ClassDeclarationSyntax AddSignals(ClassDeclarationSyntax cl, SemanticModel semanticModel, IEnumerable<MethodDeclarationSyntax> signals)
        {


            return cl;
        }

        private ClassDeclarationSyntax AddProperties(ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            if (dBusInterface.Properties?.Length == 0)
                return cl;

            cl = dBusInterface.Properties!.Aggregate(cl, (current, dBusProperty) => dBusProperty.Access switch
            {
                "read" => current.AddMembers(CreateGetMethod(dBusProperty)),
                "write" => current.AddMembers(CreateSetMethod(dBusProperty)),
                "readwrite" => current.AddMembers(CreateGetMethod(dBusProperty), CreateSetMethod(dBusProperty)),
                _ => current
            });

            cl = AddGetAllMethod(cl, dBusInterface);
            cl = AddPropertiesClass(cl, dBusInterface);

            return cl;
        }

        private MethodDeclarationSyntax CreateGetMethod(DBusProperty dBusProperty)
        {
            string createMethodIdentifier = $"CreateGet{dBusProperty.Name}Message";

            BlockSyntax createGetMessageBody = MakeCreateMessageBody(MakeLiteralExpression("org.freedesktop.DBus.Properties"), "Get", "ss")
                .AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteString")))
                            .AddArgumentListArguments(Argument(IdentifierName("Interface")))),
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteString")))
                            .AddArgumentListArguments(Argument(MakeLiteralExpression(dBusProperty.Name!)))));
            createGetMessageBody = AddCreateMessageBodyReturnStatement(createGetMessageBody);

            ArgumentListSyntax args = ArgumentList()
                .AddArguments(
                    Argument(
                        InvocationExpression(
                            IdentifierName(createMethodIdentifier))),
                        Argument(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("ReaderExtensions"), IdentifierName(GetOrAddReadMessageMethod(dBusProperty)))));

                return MethodDeclaration(ParseTypeName(ParseTaskReturnType(dBusProperty)), $"Get{dBusProperty.Name}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithBody(
                    MakeCallMethodReturnBody(args, createGetMessageBody, createMethodIdentifier));
        }

        private static MethodDeclarationSyntax CreateSetMethod(DBusProperty dBusProperty)
        {
            BlockSyntax createGetMessageBody = MakeCreateMessageBody(MakeLiteralExpression("org.freedesktop.DBus.Properties"), "Set", "ssv")
                .AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteString")))
                            .AddArgumentListArguments(Argument(IdentifierName("Interface")))),
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteString")))
                            .AddArgumentListArguments(Argument(MakeLiteralExpression(dBusProperty.Name!)))),
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteVariant")))
                            .AddArgumentListArguments(Argument(IdentifierName("value")))));
            createGetMessageBody = AddCreateMessageBodyReturnStatement(createGetMessageBody);

            return MethodDeclaration(ParseTypeName(dBusProperty.DotNetType), $"Get{dBusProperty.Name}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("value")).WithType(ParseTypeName(dBusProperty.DotNetType)))
                .WithBody(createGetMessageBody);
        }

        private static ClassDeclarationSyntax AddGetAllMethod(ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            BlockSyntax createGetAllMessageBody = MakeCreateMessageBody(MakeLiteralExpression("org.freedesktop.DBus.Properties"), "GetAll", "s")
                .AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"), IdentifierName("WriteString")))
                            .AddArgumentListArguments(Argument(IdentifierName("Interface")))));

            createGetAllMessageBody = AddCreateMessageBodyReturnStatement(createGetAllMessageBody);

            SyntaxList<SwitchSectionSyntax> switchSections = List<SwitchSectionSyntax>();
            switchSections = dBusInterface.Properties!.Aggregate(switchSections, static (current, dBusProperty) => current.Add(SwitchSection()
                .AddLabels(
                    CaseSwitchLabel(
                        MakeLiteralExpression(dBusProperty.Name!)))
                .AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName("ReadSignature")))
                    .AddArgumentListArguments(
                        Argument(MakeLiteralExpression(dBusProperty.Type!)))),
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("props"), IdentifierName(dBusProperty.Name!)),
                    InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName($"Read{ParseReadWriteMethod(dBusProperty)}"))))),
                    BreakStatement())));

            string propertiesType = $"{cl.Identifier.Text}Properties";

            ParenthesizedLambdaExpressionSyntax messageValueReaderLambda = ParenthesizedLambdaExpression()
                .AddParameterListParameters(
                    Parameter(Identifier("message")).WithType(ParseTypeName("Message")),
                    Parameter(Identifier("state")).WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                .WithBody(
                    Block(
                        LocalDeclarationStatement(VariableDeclaration(ParseTypeName("Reader"))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("message"),
                                                    IdentifierName("GetBodyReader"))))))),
                        LocalDeclarationStatement(VariableDeclaration(ParseTypeName(propertiesType))
                            .AddVariables(
                                VariableDeclarator("props")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                ObjectCreationExpression(ParseTypeName(propertiesType))))))),
                        LocalDeclarationStatement(VariableDeclaration(ParseTypeName("ArrayEnd"))
                            .AddVariables(
                                VariableDeclarator("headersEnd")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName("ReadArrayStart")))
                                                .AddArgumentListArguments(
                                                    Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("DBusType"), IdentifierName("Struct")))))))),
                        WhileStatement(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName("HasNext")))
                                .AddArgumentListArguments(Argument(IdentifierName("headersEnd"))),
                            Block(
                                SwitchStatement(
                                    InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName("ReadString"))))
                                    .WithSections(switchSections))),
                        ReturnStatement(IdentifierName("props"))));

            return cl.AddMembers(
                MethodDeclaration(ParseTypeName($"Task<{propertiesType}>"), "GetAllAsync")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithBody(
                        Block(
                            ReturnStatement(
                                InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"), IdentifierName("CallMethodAsync")))
                                    .AddArgumentListArguments(
                                        Argument(InvocationExpression(IdentifierName("CreateGetAllMessage"))),
                                        Argument(messageValueReaderLambda))),
                            LocalFunctionStatement(ParseTypeName("MessageBuffer"), "CreateGetAllMessage")
                                .WithBody(createGetAllMessageBody))));
        }

        private static ClassDeclarationSyntax AddPropertiesClass(ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            ClassDeclarationSyntax record = ClassDeclaration($"{cl.Identifier.Text}Properties")
                .AddModifiers(Token(SyntaxKind.PublicKeyword));

            record = dBusInterface.Properties!.Aggregate(record, static (current, property)
                => current.AddMembers(MakeGetSetProperty(ParseTypeName(property.DotNetType), property.Name!, Token(SyntaxKind.PublicKeyword))));

            return cl.AddMembers(record);
        }

        private static BlockSyntax MakeCallMethodReturnBody(ArgumentListSyntax args, BlockSyntax createMessageBody, string createMethodIdentifier) =>
            Block(
                ReturnStatement(
                    InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"),
                                IdentifierName("CallMethodAsync")))
                        .WithArgumentList(args)),
                LocalFunctionStatement(ParseTypeName("MessageBuffer"), createMethodIdentifier)
                    .WithBody(createMessageBody));

        private static BlockSyntax MakeCreateMessageBody(ExpressionSyntax interfaceExpression, string methodName, string? signature)
        {
            ArgumentListSyntax args = ArgumentList()
                .AddArguments(
                    Argument(IdentifierName("_destination")),
                    Argument(IdentifierName("_path")),
                    Argument(interfaceExpression),
                    Argument(MakeLiteralExpression(methodName)));

            if (signature is not null)
                args = args.AddArguments(Argument(MakeLiteralExpression(signature)));

            return Block(
                LocalDeclarationStatement(VariableDeclaration(ParseTypeName("MessageWriter"),
                        SingletonSeparatedList(
                            VariableDeclarator("writer")
                                .WithInitializer(EqualsValueClause(
                                    InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"),
                                            IdentifierName("GetMessageWriter"))))))))
                    .WithUsingKeyword(Token(SyntaxKind.UsingKeyword)),
                ExpressionStatement(
                    InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"),
                                IdentifierName("WriteMethodCallHeader")))
                        .WithArgumentList(args)));
        }

        private static BlockSyntax AddCreateMessageBodyReturnStatement(BlockSyntax createMessageBody) => createMessageBody.AddStatements(
            ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("writer"),
                        IdentifierName("CreateMessage")))));

        private string GetOrAddReadMessageMethod(DBusValue dBusValue) => GetOrAddReadMessageMethod(new[] { dBusValue });

        private string GetOrAddReadMessageMethod(IReadOnlyList<DBusValue> dBusValues)
        {
            string signature = ParseSignature(dBusValues)!.Replace('{', 'e').Replace("}", null).Replace('(', 'r').Replace(')', 'z');
            if (_readMethodForSignature.TryGetValue(signature, out string identifier))
                return identifier;

            identifier = $"ReadMessage_{signature}";
            string returnType = ParseReturnType(dBusValues)!;

            BlockSyntax block = Block()
                .AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(ParseTypeName("Reader"))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("message"), IdentifierName("GetBodyReader"))))))));

            if (dBusValues.Count == 1)
            {
                block = block.AddStatements(
                    ReturnStatement(
                        InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName($"Read{ParseReadWriteMethod(dBusValues[0])}")))));
            }
            else
            {
                for (int i = 0; i < dBusValues.Count; i++)
                {
                    block = block.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName(dBusValues[i].DotNetType))
                                .AddVariables(
                                    VariableDeclarator($"arg{i}")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("reader"), IdentifierName($"Read{ParseReadWriteMethod(dBusValues[i])}"))))))));
                }

                block = block.AddStatements(
                    ReturnStatement(
                        TupleExpression(
                            SeparatedList(
                                dBusValues.Select(static (_, i) => Argument(IdentifierName($"arg{i}")))))));
            }

            _readerExtensions = _readerExtensions.AddMembers(
                MethodDeclaration(ParseTypeName(returnType), identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("message")).WithType(ParseTypeName("Message")),
                        Parameter(Identifier("_")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
                    .WithBody(block));

            _readMethodForSignature.Add(signature, identifier);

            return identifier;
        }
    }
}
