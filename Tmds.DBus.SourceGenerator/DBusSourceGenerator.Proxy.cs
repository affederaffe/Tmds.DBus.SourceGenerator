using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private ClassDeclarationSyntax GenerateProxy(ClassDeclarationSyntax declaration, DBusInterface dBusInterface)
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
            cl = AddMethods(cl, dBusInterface.Methods);
            cl = AddSignals(cl, dBusInterface.Signals);
            cl = AddProperties(cl, dBusInterface);

            return cl;
        }

        private ClassDeclarationSyntax AddMethods(ClassDeclarationSyntax cl, IEnumerable<DBusMethod>? dBusMethods) =>
            dBusMethods is null ? cl : dBusMethods.Aggregate(cl, (current, dBusMethod) => current.AddMembers(MakeMethod(dBusMethod)));

        private MethodDeclarationSyntax MakeMethod(DBusMethod dBusMethod)
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
                        MakeMemberAccessExpression("ReaderExtensions", GetOrAddReadMessageMethod(outArgs))));

            ExpressionStatementSyntax[] statements = inArgs?.Select(static (x, i) => ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", $"Write{ParseReadWriteMethod(x)}"))
                    .AddArgumentListArguments(
                        Argument(IdentifierName(x.Name ?? $"arg{i}")))))
                .ToArray() ?? Array.Empty<ExpressionStatementSyntax>();

            BlockSyntax createMessageBody = MakeCreateMessageBody(IdentifierName("Interface"), dBusMethod.Name!, ParseSignature(inArgs), statements);

            MethodDeclarationSyntax proxyMethod = MethodDeclaration(ParseTypeName(returnType), $"{dBusMethod.Name}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword));

            if (inArgs is not null)
                proxyMethod = proxyMethod
                    .WithParameterList(ParseParameterList(inArgs));

            return proxyMethod.WithBody(MakeCallMethodReturnBody(args, createMessageBody, createMethodIdentifier));
        }

        private ClassDeclarationSyntax AddSignals(ClassDeclarationSyntax cl, IEnumerable<DBusSignal>? dBusSignals) =>
            dBusSignals is null ? cl : dBusSignals.Aggregate(cl, (current, dBusSignal) => current.AddMembers(MakeSignal(dBusSignal)));

        private MethodDeclarationSyntax MakeSignal(DBusSignal dBusSignal)
        {
            DBusArgument[]? outArgs = dBusSignal.Arguments?.Where(static x => x.Direction is null or "out").ToArray();
            string? returnType = ParseReturnType(outArgs);

            ParameterListSyntax parameters = ParameterList();

            parameters = returnType is not null
                ? parameters.AddParameters(
                    Parameter(Identifier("handler"))
                        .WithType(ParseTypeName($"Action<Exception?, {returnType}>")))
                : parameters.AddParameters(
                    Parameter(Identifier("handler"))
                        .WithType(ParseTypeName("Action<Exception?>")));

            parameters = parameters.AddParameters(
                Parameter(Identifier("emitOnCapturedContext"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression))));

            ArgumentListSyntax arguments = ArgumentList()
                .AddArguments(Argument(IdentifierName("_connection")),
                    Argument(IdentifierName("rule")));

            if (outArgs is not null)
                arguments = arguments.AddArguments(
                    Argument(MakeMemberAccessExpression("ReaderExtensions", GetOrAddReadMessageMethod(outArgs))));

            arguments = arguments.AddArguments(
                Argument(IdentifierName("handler")),
                Argument(IdentifierName("emitOnCapturedContext")));

            MethodDeclarationSyntax watchSignalMethod = MethodDeclaration(ParseTypeName("ValueTask<IDisposable>"), $"Watch{dBusSignal.Name}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithParameterList(parameters)
                .WithBody(
                    Block(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName("MatchRule"))
                                .AddVariables(
                                    VariableDeclarator("rule")
                                        .WithInitializer(
                                            EqualsValueClause(MakeMatchRule(dBusSignal))))),
                        ReturnStatement(
                            InvocationExpression(
                                MakeMemberAccessExpression("SignalHelper", "WatchSignalAsync"))
                                .WithArgumentList(arguments))));

            return watchSignalMethod;
        }

        private static ObjectCreationExpressionSyntax MakeMatchRule(DBusSignal dBusSignal) =>
            ObjectCreationExpression(ParseTypeName("MatchRule"))
                .WithInitializer(
                    InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                        .AddExpressions(
                            MakeAssignmentExpression(IdentifierName("Type"), MakeMemberAccessExpression("MessageType", "Signal")),
                            MakeAssignmentExpression(IdentifierName("Sender"), IdentifierName("_destination")),
                            MakeAssignmentExpression(IdentifierName("Path"), IdentifierName("_path")),
                            MakeAssignmentExpression(IdentifierName("Member"), MakeLiteralExpression(dBusSignal.Name!)),
                            MakeAssignmentExpression(IdentifierName("Interface"), IdentifierName("Interface"))));

        private static ClassDeclarationSyntax AddWatchPropertiesChanged(ClassDeclarationSyntax cl) =>
            cl.AddMembers(MethodDeclaration(ParseTypeName("ValueTask<IDisposable>"), "WatchPropertiesChangedAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("handler"))
                        .WithType(ParseTypeName("Action<Exception?, PropertyChanges<Properties>>")),
                    Parameter(Identifier("emitOnCapturedContext"))
                        .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                        .WithDefault(
                            EqualsValueClause(
                                LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("SignalHelper", "WatchPropertiesChangedAsync"))
                                .AddArgumentListArguments(
                                    Argument(IdentifierName("_connection")),
                                    Argument(IdentifierName("_destination")),
                                    Argument(IdentifierName("_path")),
                                    Argument(IdentifierName("Interface")),
                                    Argument(IdentifierName("ReadMessage")),
                                    Argument(IdentifierName("handler")),
                                    Argument(IdentifierName("emitOnCapturedContext")))),
                        LocalFunctionStatement(ParseTypeName("PropertyChanges<Properties>"), "ReadMessage")
                            .AddModifiers(Token(SyntaxKind.StaticKeyword))
                            .AddParameterListParameters(
                                Parameter(Identifier("message"))
                                    .WithType(ParseTypeName("Message")),
                                Parameter(Identifier("_"))
                                    .WithType(
                                        NullableType(
                                            PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                            .WithBody(
                                Block(
                                    LocalDeclarationStatement(
                                        VariableDeclaration(ParseTypeName("Reader"))
                                            .AddVariables(
                                                VariableDeclarator("reader")
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            InvocationExpression(
                                                                MakeMemberAccessExpression("message", "GetBodyReader")))))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("reader", "ReadString"))),
                                    LocalDeclarationStatement(
                                        VariableDeclaration(ParseTypeName("List<string>"))
                                            .AddVariables(
                                                VariableDeclarator("changed")
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            ImplicitObjectCreationExpression())))),
                                    ReturnStatement(
                                        InvocationExpression(
                                                ObjectCreationExpression(ParseTypeName("PropertyChanges<Properties>")))
                                            .AddArgumentListArguments(
                                                Argument(InvocationExpression(
                                                        IdentifierName("ReadProperties"))
                                                    .AddArgumentListArguments(
                                                        Argument(IdentifierName("reader"))
                                                            .WithRefKindKeyword(Token(SyntaxKind.RefKeyword)),
                                                        Argument(IdentifierName("changed")))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("changed", "ToArray"))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", "ReadArray<string>"))))))))));

        private ClassDeclarationSyntax AddProperties(ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            if (dBusInterface.Properties is null || dBusInterface.Properties.Length == 0)
                return cl;

            cl = dBusInterface.Properties!.Aggregate(cl, (current, dBusProperty) => dBusProperty.Access switch
            {
                "read" => current.AddMembers(MakeGetMethod(dBusProperty)),
                "write" => current.AddMembers(MakeSetMethod(dBusProperty)),
                "readwrite" => current.AddMembers(MakeGetMethod(dBusProperty), MakeSetMethod(dBusProperty)),
                _ => current
            });

            cl = AddGetAllMethod(cl);
            cl = AddReadProperties(cl, dBusInterface.Properties);
            cl = AddPropertiesClass(cl, dBusInterface);
            cl = AddWatchPropertiesChanged(cl);

            return cl;
        }

        private MethodDeclarationSyntax MakeGetMethod(DBusProperty dBusProperty)
        {
            string createMethodIdentifier = $"CreateGet{dBusProperty.Name}Message";

            BlockSyntax createGetMessageBody = MakeCreateMessageBody(MakeLiteralExpression("org.freedesktop.DBus.Properties"), "Get", "ss",
                ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("writer", "WriteString"))
                        .AddArgumentListArguments(Argument(IdentifierName("Interface")))),
                ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("writer", "WriteString"))
                        .AddArgumentListArguments(Argument(MakeLiteralExpression(dBusProperty.Name!)))));

            ArgumentListSyntax args = ArgumentList()
                .AddArguments(
                    Argument(
                        InvocationExpression(
                            IdentifierName(createMethodIdentifier))),
                        Argument(
                            MakeMemberAccessExpression("ReaderExtensions", GetOrAddReadMessageMethod(dBusProperty))));

                return MethodDeclaration(ParseTypeName(ParseTaskReturnType(dBusProperty)), $"Get{dBusProperty.Name}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithBody(
                    MakeCallMethodReturnBody(args, createGetMessageBody, createMethodIdentifier));
        }

        private static MethodDeclarationSyntax MakeSetMethod(DBusProperty dBusProperty) =>
            MethodDeclaration(ParseTypeName(dBusProperty.DotNetType), $"Get{dBusProperty.Name}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("value")).WithType(ParseTypeName(dBusProperty.DotNetType)))
                .WithBody(
                    MakeCreateMessageBody(MakeLiteralExpression("org.freedesktop.DBus.Properties"), "Set", "ssv",
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", "WriteString"))
                                .AddArgumentListArguments(Argument(IdentifierName("Interface")))),
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", "WriteString"))
                                .AddArgumentListArguments(Argument(MakeLiteralExpression(dBusProperty.Name!)))),
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", "WriteVariant"))
                                .AddArgumentListArguments(Argument(IdentifierName("value"))))));

        private static ClassDeclarationSyntax AddGetAllMethod(ClassDeclarationSyntax cl)
        {
            BlockSyntax createGetAllMessageBody = MakeCreateMessageBody(MakeLiteralExpression("org.freedesktop.DBus.Properties"), "GetAll", "s",
                ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("writer", "WriteString"))
                        .AddArgumentListArguments(Argument(IdentifierName("Interface")))));

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
                                                MakeMemberAccessExpression("message", "GetBodyReader")))))),
                        ReturnStatement(
                            InvocationExpression(
                                IdentifierName("ReadProperties"))
                                .AddArgumentListArguments(
                                    Argument(IdentifierName("reader"))
                                        .WithRefKindKeyword(Token(SyntaxKind.RefKeyword))))));

            return cl.AddMembers(
                MethodDeclaration(ParseTypeName("Task<Properties>"), "GetAllAsync")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithBody(
                        Block(
                            ReturnStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("_connection", "CallMethodAsync"))
                                    .AddArgumentListArguments(
                                        Argument(InvocationExpression(IdentifierName("CreateGetAllMessage"))),
                                        Argument(messageValueReaderLambda))),
                            LocalFunctionStatement(ParseTypeName("MessageBuffer"), "CreateGetAllMessage")
                                .WithBody(createGetAllMessageBody))));
        }

        private static ClassDeclarationSyntax AddPropertiesClass(ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            ClassDeclarationSyntax propertiesClass = ClassDeclaration("Properties")
                .AddModifiers(Token(SyntaxKind.PublicKeyword));

            propertiesClass = dBusInterface.Properties!.Aggregate(propertiesClass, static (current, property)
                => current.AddMembers(
                    MakeGetSetProperty(ParseTypeName(property.DotNetType), property.Name!, Token(SyntaxKind.PublicKeyword))));

            return cl.AddMembers(propertiesClass);
        }

        private static ClassDeclarationSyntax AddReadProperties(ClassDeclarationSyntax cl, IEnumerable<DBusProperty> dBusProperties) =>
            cl.AddMembers(
                MethodDeclaration(ParseTypeName("Properties"), "ReadProperties")
                    .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("reader"))
                            .WithType(ParseTypeName("Reader"))
                            .AddModifiers(Token(SyntaxKind.RefKeyword)),
                        Parameter(Identifier("changed"))
                            .WithType(NullableType(ParseTypeName("List<string>")))
                            .WithDefault(
                                EqualsValueClause(
                                    LiteralExpression(SyntaxKind.NullLiteralExpression))))
                    .WithBody(
                        Block(
                            LocalDeclarationStatement(VariableDeclaration(ParseTypeName("Properties"))
                                .AddVariables(
                                    VariableDeclarator("props")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                    ObjectCreationExpression(ParseTypeName("Properties"))))))),
                            LocalDeclarationStatement(VariableDeclaration(ParseTypeName("ArrayEnd"))
                                .AddVariables(
                                    VariableDeclarator("headersEnd")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("reader", "ReadArrayStart"))
                                                    .AddArgumentListArguments(
                                                        Argument(MakeMemberAccessExpression("DBusType", "Struct"))))))),
                            WhileStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("reader", "HasNext"))
                                    .AddArgumentListArguments(Argument(IdentifierName("headersEnd"))),
                                Block(
                                    SwitchStatement(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("reader", "ReadString")))
                                        .WithSections(
                                            List(
                                                dBusProperties.Select(static x => SwitchSection()
                                                    .AddLabels(
                                                        CaseSwitchLabel(
                                                            MakeLiteralExpression(x.Name!)))
                                                    .AddStatements(
                                                        ExpressionStatement(
                                                            InvocationExpression(
                                                                    MakeMemberAccessExpression("reader", "ReadSignature"))
                                                                .AddArgumentListArguments(
                                                                    Argument(MakeLiteralExpression(x.Type!)))),
                                                        ExpressionStatement(
                                                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                                                MakeMemberAccessExpression("props", x.Name!),
                                                                InvocationExpression(
                                                                    MakeMemberAccessExpression("reader", $"Read{ParseReadWriteMethod(x)}")))),
                                                        ExpressionStatement(
                                                            ConditionalAccessExpression(
                                                                IdentifierName("changed"), InvocationExpression(
                                                                    MemberBindingExpression(
                                                                        IdentifierName("Add")))
                                                                    .AddArgumentListArguments(
                                                                        Argument(MakeLiteralExpression(x.Name!))))),
                                                        BreakStatement())))))),
                            ReturnStatement(IdentifierName("props")))));

        private static BlockSyntax MakeCallMethodReturnBody(ArgumentListSyntax args, BlockSyntax createMessageBody, string createMethodIdentifier) =>
            Block(
                ReturnStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("_connection", "CallMethodAsync"))
                        .WithArgumentList(args)),
                LocalFunctionStatement(ParseTypeName("MessageBuffer"), createMethodIdentifier)
                    .WithBody(createMessageBody));

        private static BlockSyntax MakeCreateMessageBody(ExpressionSyntax interfaceExpression, string methodName, string? signature, params StatementSyntax[] statements)
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
                                            MakeMemberAccessExpression("_connection", "GetMessageWriter")))))))
                        .WithUsingKeyword(Token(SyntaxKind.UsingKeyword)),
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("writer", "WriteMethodCallHeader"))
                            .WithArgumentList(args)))
                .AddStatements(statements)
                .AddStatements(ReturnStatement(
                    InvocationExpression(
                        MakeMemberAccessExpression("writer", "CreateMessage"))));
        }

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
                                                MakeMemberAccessExpression("message", "GetBodyReader")))))));

            if (dBusValues.Count == 1)
            {
                block = block.AddStatements(
                    ReturnStatement(
                        InvocationExpression(
                            MakeMemberAccessExpression("reader", $"Read{ParseReadWriteMethod(dBusValues[0])}"))));
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
                                                    MakeMemberAccessExpression("reader", $"Read{ParseReadWriteMethod(dBusValues[i])}")))))));
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
                        Parameter(Identifier("_")).WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                    .WithBody(block));

            _readMethodForSignature.Add(signature, identifier);

            return identifier;
        }
    }
}
