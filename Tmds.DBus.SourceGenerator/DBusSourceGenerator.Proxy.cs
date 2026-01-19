using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tmds.DBus.Protocol;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator;

public partial class DBusSourceGenerator
{
    private ClassDeclarationSyntax GenerateProxy(DBusInterface dBusInterface)
    {
        string identifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Proxy";
        ClassDeclarationSyntax cl = ClassDeclaration(identifier)
            .AddModifiers(
                Token(SyntaxKind.InternalKeyword));

        FieldDeclarationSyntax connectionField = MakePrivateReadOnlyField("_connection", IdentifierName(nameof(Connection)));
        FieldDeclarationSyntax destinationField = MakePrivateReadOnlyField("_destination", PredefinedType(Token(SyntaxKind.StringKeyword)));
        FieldDeclarationSyntax pathField = MakePrivateReadOnlyField("_path", PredefinedType(Token(SyntaxKind.StringKeyword)));

        ConstructorDeclarationSyntax ctor = ConstructorDeclaration(identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("connection"))
                    .WithType(
                        IdentifierName(nameof(Connection))),
                Parameter(
                        Identifier("destination"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))),
                Parameter(
                        Identifier("path"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))))
            .WithBody(
                Block(
                    MakeAssignmentExpressionStatement("_connection", "connection"),
                    MakeAssignmentExpressionStatement("_destination", "destination"),
                    MakeAssignmentExpressionStatement("_path", "path")));

        cl = cl.AddMembers(connectionField, destinationField, pathField, ctor);

        AddProperties(ref cl, dBusInterface);
        AddProxyMethods(ref cl, dBusInterface);
        AddProxySignals(ref cl, dBusInterface);

        return cl;
    }

    private void AddProxyMethods(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Methods is null)
            return;

        foreach (DBusMethod dBusMethod in dBusInterface.Methods)
        {
            DBusArgument[]? inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
            DBusArgument[]? outArgs = dBusMethod.Arguments?.Where(static m => m.Direction is "out").ToArray();

            ArgumentListSyntax args = ArgumentList(
                SingletonSeparatedList(
                    Argument(
                        InvocationExpression(
                            IdentifierName("CreateMessage")))));

            if (outArgs?.Length > 0)
            {
                args = args.AddArguments(
                    Argument(
                        MakeMemberAccessExpression("ReaderExtensions", GetOrAddReadMessageMethod(outArgs))));
            }

            StatementSyntax[] statements = inArgs?.Select((arg, i) => ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("writer", GetOrAddWriteMethod(arg.DBusDotnetType)))
                        .AddArgumentListArguments(
                            Argument(
                                IdentifierName(arg.Name is not null
                                    ? SanitizeIdentifier(
                                        Pascalize(arg.Name.AsSpan(), true))
                                    : $"arg{i}")))))
                .Cast<StatementSyntax>()
                .ToArray() ?? [];

            BlockSyntax createMessageBody = MakeCreateMessageBody(
                MakeLiteralExpression(dBusInterface.Name!), dBusMethod.Name!, ParseSignature(inArgs), statements);

            MethodDeclarationSyntax proxyMethod = MethodDeclaration(
                    ParseTaskReturnType(outArgs), $"{Pascalize(dBusMethod.Name.AsSpan())}Async")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword));

            if (inArgs is not null)
            {
                proxyMethod = proxyMethod.WithParameterList(
                    ParameterList(
                        SeparatedList(
                            ParseParameterList(inArgs))));
            }

            cl = cl.AddMembers(proxyMethod.WithBody(MakeCallMethodReturnBody(args, createMessageBody)));
        }
    }

    private void AddProxySignals(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Signals is null)
            return;

        foreach (DBusSignal dBusSignal in dBusInterface.Signals)
        {
            DBusArgument[]? outArgs = dBusSignal.Arguments?.Where(static x => x.Direction is null or "out").ToArray();
            TypeSyntax? returnType = ParseReturnType(outArgs);

            ParameterListSyntax parameters = ParameterList();

            parameters = returnType is not null
                ? parameters.AddParameters(
                    Parameter(
                            Identifier("handler"))
                        .WithType(
                            GenericName(nameof(Action))
                                .AddTypeArgumentListArguments(
                                    NullableType(
                                        IdentifierName(nameof(Exception))),
                                    returnType)))
                : parameters.AddParameters(
                    Parameter(
                            Identifier("handler"))
                        .WithType(
                            GenericName(nameof(Action))
                                .AddTypeArgumentListArguments(
                                    NullableType(
                                        IdentifierName(nameof(Exception))))));

            parameters = parameters.AddParameters(
                Parameter(
                        Identifier("emitOnCapturedContext"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(
                        EqualsValueClause(
                            LiteralExpression(SyntaxKind.TrueLiteralExpression))),
                Parameter(
                        Identifier("flags"))
                    .WithType(
                        IdentifierName(nameof(ObserverFlags)))
                    .WithDefault(
                        EqualsValueClause(
                            MakeMemberAccessExpression(nameof(ObserverFlags), nameof(ObserverFlags.None)))));

            ArgumentListSyntax arguments = ArgumentList()
                .AddArguments(
                    Argument(
                        IdentifierName("_connection")),
                    Argument(
                        IdentifierName("rule")));

            if (outArgs is not null)
            {
                arguments = arguments.AddArguments(
                    Argument(
                        MakeMemberAccessExpression("ReaderExtensions", GetOrAddReadMessageMethod(outArgs))));
            }

            arguments = arguments.AddArguments(
                Argument(
                    IdentifierName("handler")),
                Argument(
                    IdentifierName("emitOnCapturedContext")),
                Argument(
                    IdentifierName("flags")));

            MethodDeclarationSyntax watchSignalMethod = MethodDeclaration(
                    GenericName(nameof(ValueTask))
                        .AddTypeArgumentListArguments(
                            IdentifierName(nameof(IDisposable))),
                    $"Watch{Pascalize(dBusSignal.Name.AsSpan())}Async")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .WithParameterList(parameters)
                .WithBody(
                    Block(
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    IdentifierName(nameof(MatchRule)))
                                .AddVariables(
                                    VariableDeclarator("rule")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                MakeMatchRule(dBusSignal, dBusInterface))))),
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("SignalHelper", "WatchSignalAsync"))
                                .WithArgumentList(arguments))));

            cl = cl.AddMembers(watchSignalMethod);
        }
    }

    private static ObjectCreationExpressionSyntax MakeMatchRule(DBusSignal dBusSignal, DBusInterface dBusInterface) =>
        ObjectCreationExpression(
                IdentifierName(nameof(MatchRule)))
            .WithInitializer(
                InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                    .AddExpressions(
                        MakeAssignmentExpression(
                            IdentifierName(nameof(MatchRule.Type)), MakeMemberAccessExpression(nameof(MessageType), nameof(MessageType.Signal))),
                        MakeAssignmentExpression(
                            IdentifierName(nameof(MatchRule.Sender)), IdentifierName("_destination")),
                        MakeAssignmentExpression(
                            IdentifierName(nameof(MatchRule.Path)), IdentifierName("_path")),
                        MakeAssignmentExpression(
                            IdentifierName(nameof(MatchRule.Member)), MakeLiteralExpression(dBusSignal.Name!)),
                        MakeAssignmentExpression(
                            IdentifierName(nameof(MatchRule.Interface)), MakeLiteralExpression(dBusInterface.Name!))));

    private void AddWatchPropertiesChanged(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        cl = cl.AddMembers(
            MethodDeclaration(
                    GenericName(nameof(ValueTask))
                        .AddTypeArgumentListArguments(
                            IdentifierName(nameof(IDisposable))),
                    "WatchPropertiesChangedAsync")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("handler"))
                        .WithType(
                            GenericName(nameof(Action))
                                .AddTypeArgumentListArguments(
                                    NullableType(
                                        IdentifierName(nameof(Exception))),
                                    GenericName("PropertyChanges")
                                        .AddTypeArgumentListArguments(
                                            IdentifierName(
                                                GetPropertiesClassIdentifier(dBusInterface))))),
                    Parameter(
                            Identifier("emitOnCapturedContext"))
                        .WithType(
                            PredefinedType(
                                Token(SyntaxKind.BoolKeyword)))
                        .WithDefault(
                            EqualsValueClause(
                                LiteralExpression(SyntaxKind.TrueLiteralExpression))),
                    Parameter(
                            Identifier("flags"))
                        .WithType(
                            IdentifierName(nameof(ObserverFlags)))
                        .WithDefault(
                            EqualsValueClause(
                                MakeMemberAccessExpression(nameof(ObserverFlags), nameof(ObserverFlags.None)))))
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("SignalHelper", "WatchPropertiesChangedAsync"))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("_connection")),
                                    Argument(
                                        IdentifierName("_destination")),
                                    Argument(IdentifierName("_path")),
                                    Argument(
                                        MakeLiteralExpression(dBusInterface.Name!)),
                                    Argument(
                                        IdentifierName("ReadMessage")),
                                    Argument(
                                        IdentifierName("handler")),
                                    Argument(
                                        IdentifierName("emitOnCapturedContext")),
                                    Argument(
                                        IdentifierName("flags")))),
                        LocalFunctionStatement(
                                GenericName("PropertyChanges")
                                    .AddTypeArgumentListArguments(
                                        IdentifierName(
                                            GetPropertiesClassIdentifier(dBusInterface))),
                                "ReadMessage")
                            .AddModifiers(
                                Token(SyntaxKind.StaticKeyword))
                            .AddParameterListParameters(
                                Parameter(
                                        Identifier("message"))
                                    .WithType(
                                        IdentifierName(nameof(Message))),
                                Parameter(
                                        Identifier("_"))
                                    .WithType(
                                        NullableType(
                                            PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                            .WithBody(
                                Block(
                                    LocalDeclarationStatement(
                                        VariableDeclaration(
                                                IdentifierName(nameof(Reader)))
                                            .AddVariables(
                                                VariableDeclarator("reader")
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            InvocationExpression(
                                                                MakeMemberAccessExpression("message", nameof(Message.GetBodyReader))))))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("reader", nameof(Reader.ReadString)))),
                                    LocalDeclarationStatement(
                                        VariableDeclaration(
                                                GenericName(nameof(System.Collections.Generic.List<>))
                                                    .AddTypeArgumentListArguments(
                                                        PredefinedType(Token(SyntaxKind.StringKeyword))))
                                            .AddVariables(
                                                VariableDeclarator("changed")
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            ImplicitObjectCreationExpression())))),
                                    ReturnStatement(
                                        InvocationExpression(
                                                ObjectCreationExpression(
                                                    GenericName("PropertyChanges")
                                                        .AddTypeArgumentListArguments(
                                                            IdentifierName(
                                                                GetPropertiesClassIdentifier(dBusInterface)))))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                            IdentifierName("ReadProperties"))
                                                        .AddArgumentListArguments(
                                                            Argument(
                                                                    IdentifierName("reader"))
                                                                .WithRefKindKeyword(
                                                                    Token(SyntaxKind.RefKeyword)),
                                                            Argument(
                                                                IdentifierName("changed")))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("changed", nameof(System.Collections.Generic.List<>.ToArray)))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader",
                                                            GetOrAddReadMethod(DBusDotnetType.StringArrayType)))))))))));
    }

    private void AddProperties(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Properties is null || dBusInterface.Properties.Length == 0)
            return;

        cl = dBusInterface.Properties!.Aggregate(cl, (current, dBusProperty) => dBusProperty.Access switch
        {
            "read" => current.AddMembers(MakeGetMethod(dBusInterface, dBusProperty)),
            "write" => current.AddMembers(MakeSetMethod(dBusInterface, dBusProperty)),
            "readwrite" => current.AddMembers(MakeGetMethod(dBusInterface, dBusProperty), MakeSetMethod(dBusInterface, dBusProperty)),
            _ => current
        });

        AddGetAllMethod(ref cl, dBusInterface);
        AddReadProperties(ref cl, dBusInterface);
        AddPropertiesClass(ref cl, dBusInterface);
        AddWatchPropertiesChanged(ref cl, dBusInterface);
    }

    private MethodDeclarationSyntax MakeGetMethod(DBusInterface dBusInterface, DBusProperty dBusProperty)
    {
        BlockSyntax createMessageBody = MakeCreateMessageBody(
            MakeLiteralExpression("org.freedesktop.DBus.Properties"), "Get", "ss",
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteString)))
                    .AddArgumentListArguments(
                        Argument(
                            MakeUtf8StringLiteralExpression(dBusInterface.Name!)))),
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteString)))
                    .AddArgumentListArguments(
                        Argument(
                            MakeUtf8StringLiteralExpression(dBusProperty.Name!)))));

        ArgumentListSyntax args = ArgumentList()
            .AddArguments(
                Argument(
                    InvocationExpression(
                        IdentifierName("CreateMessage"))),
                Argument(
                    MakeMemberAccessExpression(
                        "ReaderExtensions", GetOrAddReadMessageMethod(dBusProperty, true))));

        return MethodDeclaration(
                ParseTaskReturnType([dBusProperty]), $"Get{Pascalize(dBusProperty.Name.AsSpan())}PropertyAsync")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithBody(
                MakeCallMethodReturnBody(args, createMessageBody));
    }

    private MethodDeclarationSyntax MakeSetMethod(DBusInterface dBusInterface, DBusProperty dBusProperty)
    {
        BlockSyntax createMessageBody = MakeCreateMessageBody(
            MakeLiteralExpression("org.freedesktop.DBus.Properties"), "Set", "ssv",
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteString)))
                    .AddArgumentListArguments(
                        Argument(
                            MakeUtf8StringLiteralExpression(dBusInterface.Name!)))),
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteString)))
                    .AddArgumentListArguments(
                        Argument(
                            MakeUtf8StringLiteralExpression(
                                Pascalize(dBusProperty.Name.AsSpan()))))),
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", GetOrAddWriteVariantMethod(dBusProperty)))
                    .AddArgumentListArguments(
                        Argument(
                            IdentifierName("value")))));

        ArgumentListSyntax args = ArgumentList(
            SingletonSeparatedList(
                Argument(
                    InvocationExpression(
                        IdentifierName("CreateMessage")))));

        return MethodDeclaration(
                IdentifierName(nameof(Task)),
                $"Set{Pascalize(dBusProperty.Name.AsSpan())}PropertyAsync")
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("value"))
                    .WithType(
                        dBusProperty.DBusDotnetType.ToTypeSyntax()))
            .WithBody(
                MakeCallMethodReturnBody(args, createMessageBody));
    }

    private static void AddGetAllMethod(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        BlockSyntax createGetAllMessageBody = MakeCreateMessageBody(
            MakeLiteralExpression("org.freedesktop.DBus.Properties"), "GetAll", "s",
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteString)))
                    .AddArgumentListArguments(
                        Argument(
                            MakeUtf8StringLiteralExpression(dBusInterface.Name!)))));

        ParenthesizedLambdaExpressionSyntax messageValueReaderLambda = ParenthesizedLambdaExpression()
            .AddParameterListParameters(
                Parameter(
                        Identifier("message"))
                    .WithType(
                        IdentifierName(nameof(Message))),
                Parameter(
                        Identifier("state"))
                    .WithType(
                        NullableType(
                            PredefinedType(
                                Token(SyntaxKind.ObjectKeyword)))))
            .WithBody(
                Block(
                    LocalDeclarationStatement(
                        VariableDeclaration(
                                IdentifierName(nameof(Reader)))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("message", nameof(Message.GetBodyReader))))))),
                    ReturnStatement(
                        InvocationExpression(
                                IdentifierName("ReadProperties"))
                            .AddArgumentListArguments(
                                Argument(
                                        IdentifierName("reader"))
                                    .WithRefKindKeyword(
                                        Token(SyntaxKind.RefKeyword))))));

        cl = cl.AddMembers(
            MethodDeclaration(
                    GenericName(nameof(Task))
                        .AddTypeArgumentListArguments(
                            IdentifierName(
                                GetPropertiesClassIdentifier(dBusInterface))),
                    "GetAllPropertiesAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("_connection", nameof(Connection.CallMethodAsync)))
                                .AddArgumentListArguments(
                                    Argument(
                                        InvocationExpression(IdentifierName("CreateGetAllMessage"))),
                                    Argument(messageValueReaderLambda))),
                        LocalFunctionStatement(IdentifierName(nameof(MessageBuffer)), "CreateGetAllMessage")
                            .WithBody(createGetAllMessageBody))));
    }

    private static void AddPropertiesClass(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        ClassDeclarationSyntax propertiesClass = ClassDeclaration(
                GetPropertiesClassIdentifier(dBusInterface))
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword));

        propertiesClass = dBusInterface.Properties!.Aggregate(propertiesClass, static (current, property) =>
            current.AddMembers(
                MakeGetSetProperty(
                    DBusDotnetType.FromDBusValue(property)
                        .ToTypeSyntax(),
                    Pascalize(property.Name.AsSpan()),
                    Token(SyntaxKind.PublicKeyword))));

        cl = cl.AddMembers(propertiesClass);
    }

    private void AddReadProperties(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        cl = cl.AddMembers(
            MethodDeclaration(
                    IdentifierName(
                        GetPropertiesClassIdentifier(dBusInterface)),
                    "ReadProperties")
                .AddModifiers(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("reader"))
                        .WithType(
                            IdentifierName(nameof(Reader)))
                        .AddModifiers(
                            Token(SyntaxKind.RefKeyword)),
                    Parameter
                            (Identifier("changed"))
                        .WithType(
                            NullableType(
                                GenericName(nameof(System.Collections.Generic.List<>))
                                    .AddTypeArgumentListArguments(
                                        PredefinedType(
                                            Token(SyntaxKind.StringKeyword)))))
                        .WithDefault(
                            EqualsValueClause(
                                LiteralExpression(SyntaxKind.NullLiteralExpression))))
                .WithBody(
                    Block(
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    IdentifierName(
                                        GetPropertiesClassIdentifier(dBusInterface)))
                                .AddVariables(
                                    VariableDeclarator("props")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                    ObjectCreationExpression(
                                                        IdentifierName(
                                                            GetPropertiesClassIdentifier(dBusInterface)))))))),
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    IdentifierName(nameof(ArrayEnd)))
                                .AddVariables(
                                    VariableDeclarator("headersEnd")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("reader", nameof(Reader.ReadArrayStart)))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression(nameof(DBusType), nameof(DBusType.Struct)))))))),
                        WhileStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("reader", nameof(Reader.HasNext)))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("headersEnd"))),
                            Block(
                                SwitchStatement(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("reader", nameof(Reader.ReadString))))
                                    .WithSections(
                                        List(
                                            dBusInterface.Properties!.Select(property => SwitchSection()
                                                .AddLabels(
                                                    CaseSwitchLabel(
                                                        MakeLiteralExpression(property.Name!)))
                                                .AddStatements(
                                                    ExpressionStatement(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("reader", nameof(Reader.ReadSignature)))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeLiteralExpression(property.Type!)))),
                                                    ExpressionStatement(
                                                        AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                                            MakeMemberAccessExpression("props", Pascalize(property.Name.AsSpan())),
                                                            InvocationExpression(
                                                                MakeMemberAccessExpression("reader",
                                                                    GetOrAddReadMethod(DBusDotnetType
                                                                        .FromDBusValue(property)))))),
                                                    ExpressionStatement(
                                                        ConditionalAccessExpression(
                                                            IdentifierName("changed"), InvocationExpression(
                                                                    MemberBindingExpression(
                                                                        IdentifierName("Add")))
                                                                .AddArgumentListArguments(
                                                                    Argument(
                                                                        MakeLiteralExpression(
                                                                            Pascalize(property.Name.AsSpan())))))),
                                                    BreakStatement())))))),
                        ReturnStatement(
                            IdentifierName("props")))));
    }

    private static BlockSyntax MakeCallMethodReturnBody(ArgumentListSyntax args, BlockSyntax createMessageBody) =>
        Block(
            ReturnStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("_connection", nameof(Connection.CallMethodAsync)))
                    .WithArgumentList(args)),
            LocalFunctionStatement(IdentifierName(nameof(MessageBuffer)), "CreateMessage")
                .WithBody(createMessageBody));

    private static BlockSyntax MakeCreateMessageBody(ExpressionSyntax interfaceExpression, string methodName, string? signature, params StatementSyntax[] statements)
    {
        ArgumentListSyntax args = ArgumentList()
            .AddArguments(
                Argument(
                    IdentifierName("_destination")),
                Argument(
                    IdentifierName("_path")),
                Argument(interfaceExpression),
                Argument(
                    MakeLiteralExpression(methodName)));

        if (signature is not null)
        {
            args = args.AddArguments(
                Argument
                    (MakeLiteralExpression(signature)));
        }

        return Block(
                LocalDeclarationStatement(
                    VariableDeclaration(
                        IdentifierName(nameof(MessageWriter)),
                        SingletonSeparatedList(
                            VariableDeclarator("writer")
                                .WithInitializer(
                                    EqualsValueClause(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("_connection", "GetMessageWriter"))))))),
                ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteMethodCallHeader)))
                        .WithArgumentList(args)))
            .AddStatements(statements)
            .AddStatements(
                LocalDeclarationStatement(
                    VariableDeclaration(
                            IdentifierName(nameof(MessageBuffer)))
                        .AddVariables(
                            VariableDeclarator("message")
                                .WithInitializer(
                                    EqualsValueClause(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", nameof(MessageWriter.CreateMessage))))))),
                ExpressionStatement(
                    InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.Dispose)))),
                ReturnStatement(
                    IdentifierName("message")));
    }
}
