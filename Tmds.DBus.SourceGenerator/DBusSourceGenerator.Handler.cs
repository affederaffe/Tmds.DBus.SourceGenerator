using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private ClassDeclarationSyntax GenerateHandler(DBusInterface dBusInterface)
        {
            string identifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Handler";

            ClassDeclarationSyntax cl = ClassDeclaration(identifier)
                .AddModifiers(
                    Token(SyntaxKind.InternalKeyword),
                    Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(
                    SimpleBaseType(
                        IdentifierName("IDBusInterfaceHandler")))
                .AddMembers(
                    MakePrivateReadOnlyField(
                        "_synchronizationContext",
                        NullableType(
                            IdentifierName("SynchronizationContext"))),
                    ConstructorDeclaration(identifier)
                        .AddModifiers(
                            Token(SyntaxKind.PublicKeyword))
                        .AddParameterListParameters(
                            Parameter(
                                    Identifier("emitOnCapturedContext"))
                                .WithType(
                                    PredefinedType(
                                        Token(SyntaxKind.BoolKeyword)))
                                .WithDefault(
                                    EqualsValueClause(
                                        LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                        .WithBody(
                            Block(
                                IfStatement(
                                    IdentifierName("emitOnCapturedContext"),
                                    ExpressionStatement(
                                        AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                            IdentifierName("_synchronizationContext"),
                                            MakeMemberAccessExpression("SynchronizationContext", "Current")))))),
                    MakeGetSetProperty(
                        NullableType(
                            IdentifierName("PathHandler")),
                        "PathHandler",
                        Token(SyntaxKind.PublicKeyword)),
                    MakeGetOnlyProperty(
                        IdentifierName("Connection"),
                        "Connection",
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.AbstractKeyword)),
                    MakeGetOnlyProperty(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword)),
                        "InterfaceName",
                        Token(SyntaxKind.PublicKeyword))
                        .WithInitializer(
                            EqualsValueClause(
                                MakeLiteralExpression(dBusInterface.Name!)))
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken)));

            AddHandlerProperties(ref cl, dBusInterface);
            AddHandlerIntrospect(ref cl, dBusInterface);
            AddHandlerMethods(ref cl, dBusInterface);
            AddHandlerSignals(ref cl, dBusInterface);

            return cl;
        }

        private void AddHandlerMethods(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            dBusInterface.Methods ??= [];

            SyntaxList<SwitchSectionSyntax> switchSections = List<SwitchSectionSyntax>();

            foreach (DBusMethod dBusMethod in dBusInterface.Methods)
            {
                DBusArgument[]? inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
                DBusArgument[]? outArgs = dBusMethod.Arguments?.Where(static m => m.Direction == "out").ToArray();

                SwitchSectionSyntax switchSection = SwitchSection()
                    .AddLabels(
                        CasePatternSwitchLabel(
                            RecursivePattern()
                                .WithPositionalPatternClause(
                                    PositionalPatternClause()
                                        .AddSubpatterns(
                                            Subpattern(
                                                ConstantPattern(
                                                    MakeLiteralExpression(dBusMethod.Name!))),
                                            Subpattern(
                                                inArgs?.Length > 0
                                                ? ConstantPattern(
                                                    MakeLiteralExpression(
                                                        ParseSignature(inArgs)))
                                                : BinaryPattern(SyntaxKind.OrPattern,
                                                    ConstantPattern(
                                                        MakeLiteralExpression(string.Empty)),
                                                    ConstantPattern(
                                                        LiteralExpression(SyntaxKind.NullLiteralExpression)))))),
                            Token(SyntaxKind.ColonToken)));

                BlockSyntax switchSectionBlock = Block();

                string abstractMethodName = $"On{Pascalize(dBusMethod.Name.AsSpan())}Async";

                MethodDeclarationSyntax abstractMethod = MethodDeclaration(
                        ParseValueTaskReturnType(outArgs), abstractMethodName)
                    .WithParameterList(
                        ParameterList(
                            SingletonSeparatedList(
                                Parameter(
                                        Identifier("request"))
                                    .WithType(
                                        IdentifierName("Message")))))
                    .AddModifiers(
                        Token(SyntaxKind.ProtectedKeyword),
                        Token(SyntaxKind.AbstractKeyword))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken));

                if (inArgs?.Length > 0)
                {
                    abstractMethod = abstractMethod.AddParameterListParameters(
                        ParseParameterList(inArgs));
                }

                cl = cl.AddMembers(abstractMethod);

                if (inArgs?.Length > 0)
                {
                    BlockSyntax readParametersMethodBlock = Block(
                        LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("Reader"))
                                .AddVariables(
                                    VariableDeclarator("reader")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(MakeMemberAccessExpression("context", "Request", "GetBodyReader")))))));

                    StatementSyntax[] argFields = new StatementSyntax[inArgs.Length];

                    for (int i = 0; i < inArgs.Length; i++)
                    {
                        string identifier = inArgs[i].Name is not null
                            ? SanitizeIdentifier(Camelize(inArgs[i].Name.AsSpan()))
                            : $"arg{i}";
                        readParametersMethodBlock = readParametersMethodBlock.AddStatements(
                            ExpressionStatement(
                                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(identifier), InvocationExpression(
                                    MakeMemberAccessExpression("reader", GetOrAddReadMethod(inArgs[i].DBusDotnetType))))));
                        argFields[i] = LocalDeclarationStatement(
                            VariableDeclaration(inArgs[i].DBusDotnetType.ToTypeSyntax())
                                .AddVariables(
                                    VariableDeclarator(identifier)));
                    }

                    switchSectionBlock = switchSectionBlock.AddStatements(argFields);
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                IdentifierName("ReadParameters"))),
                        LocalFunctionStatement(
                                PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("ReadParameters"))
                            .WithBody(readParametersMethodBlock));
                }

                if (outArgs?.Length > 0)
                {
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    ParseReturnType(outArgs))
                                .AddVariables(
                                    VariableDeclarator("ret"))));
                }

                ExpressionSyntax callAbstractMethod = AwaitExpression(
                    InvocationExpression(
                            IdentifierName(abstractMethodName))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(
                                        MakeMemberAccessExpression("context", "Request")))))
                        .AddArgumentListArguments(
                            inArgs?.Select(static (argument, i) =>
                                    Argument(
                                        IdentifierName(argument.Name is not null
                                            ? SanitizeIdentifier(
                                                Camelize(argument.Name.AsSpan()))
                                            : $"arg{i}")))
                                .ToArray() ?? []));

                switchSectionBlock = switchSectionBlock.AddStatements(
                        IfStatement(
                        IsPatternExpression(
                            IdentifierName("_synchronizationContext"), UnaryPattern(
                                ConstantPattern(
                                    LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                        Block(
                            LocalDeclarationStatement(
                                VariableDeclaration(
                                        ParseTaskCompletionSourceType(outArgs))
                                    .AddVariables(
                                        VariableDeclarator("tsc")
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    ImplicitObjectCreationExpression())))),
                            ExpressionStatement(
                                InvocationExpression(
                                    MakeMemberAccessExpression("_synchronizationContext", "Post"))
                                    .AddArgumentListArguments(
                                        Argument(
                                            SimpleLambdaExpression(
                                                Parameter(
                                                    Identifier("_")))
                                                .WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword))
                                                .WithBlock(
                                                    Block(
                                                        TryStatement()
                                                            .AddBlockStatements(
                                                                outArgs?.Length > 0
                                                                    ? LocalDeclarationStatement(
                                                                    VariableDeclaration(
                                                                            ParseReturnType(outArgs))
                                                                        .AddVariables(
                                                                            VariableDeclarator("ret1")
                                                                                .WithInitializer(
                                                                                    EqualsValueClause(callAbstractMethod))))
                                                                    : ExpressionStatement(callAbstractMethod),
                                                                ExpressionStatement(
                                                                    InvocationExpression(
                                                                            MakeMemberAccessExpression("tsc", "SetResult"))
                                                                        .AddArgumentListArguments(
                                                                            Argument(
                                                                                outArgs?.Length > 0
                                                                                    ? IdentifierName("ret1")
                                                                                    : LiteralExpression(SyntaxKind.TrueLiteralExpression)))))
                                                            .AddCatches(
                                                                CatchClause()
                                                                    .WithDeclaration(
                                                                        CatchDeclaration(IdentifierName("Exception"))
                                                                        .WithIdentifier(Identifier("e")))
                                                                    .WithBlock(
                                                                        Block(
                                                                            ExpressionStatement(
                                                                                InvocationExpression(
                                                                                        MakeMemberAccessExpression("tsc", "SetException"))
                                                                                    .AddArgumentListArguments(
                                                                                        Argument(IdentifierName("e")))))))))),
                                        Argument(
                                            LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                            ExpressionStatement(
                                outArgs?.Length > 0
                                    ? MakeAssignmentExpression(
                                        IdentifierName("ret"), AwaitExpression(
                                            MakeMemberAccessExpression("tsc", "Task")))
                                    : AwaitExpression(
                                        MakeMemberAccessExpression("tsc", "Task")))),
                        ElseClause(
                            Block(
                            ExpressionStatement(
                                outArgs?.Length > 0
                                    ? MakeAssignmentExpression(
                                        IdentifierName("ret"), callAbstractMethod)
                                    : callAbstractMethod)))));

                    BlockSyntax replyMethodBlock = Block(
                        LocalDeclarationStatement(
                                VariableDeclaration(IdentifierName("MessageWriter"))
                                    .AddVariables(
                                        VariableDeclarator("writer")
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    InvocationExpression(
                                                            MakeMemberAccessExpression("context", "CreateReplyWriter"))
                                                        .AddArgumentListArguments(
                                                            Argument(
                                                                outArgs?.Length > 0
                                                                    ? MakeLiteralExpression(
                                                                        ParseSignature(outArgs))
                                                                    : LiteralExpression(SyntaxKind.NullLiteralExpression))))))));

                    if (outArgs?.Length == 1)
                    {
                        replyMethodBlock = replyMethodBlock.AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(outArgs[0].DBusDotnetType)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            IdentifierName("ret")))));
                    }
                    else if (outArgs?.Length > 1)
                    {
                        for (int i = 0; i < outArgs.Length; i++)
                        {
                            replyMethodBlock = replyMethodBlock.AddStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", GetOrAddWriteMethod(outArgs[i].DBusDotnetType)))
                                        .AddArgumentListArguments(
                                            Argument(
                                                MakeMemberAccessExpression("ret", outArgs[i].Name is not null
                                                    ? SanitizeIdentifier(Pascalize(outArgs[i].Name.AsSpan()))
                                                    : $"Item{i + 1}")))));
                        }
                    }

                    replyMethodBlock = replyMethodBlock.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("context", "Reply"))
                                .AddArgumentListArguments(
                                    Argument(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", "CreateMessage"))))),
                        ExpressionStatement(
                            InvocationExpression(
                                MakeMemberAccessExpression("writer", "Dispose"))));

                    switchSectionBlock = switchSectionBlock.AddStatements(
                        IfStatement(
                            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, MakeMemberAccessExpression("context", "NoReplyExpected")),
                            ExpressionStatement(
                                InvocationExpression(
                                    IdentifierName("Reply")))),
                        LocalFunctionStatement(
                                PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Reply"))
                            .WithBody(replyMethodBlock));

                switchSections = switchSections.Add(
                    switchSection.AddStatements(
                        switchSectionBlock.AddStatements(
                            BreakStatement())));
            }

            MethodDeclarationSyntax replyInterfaceRequestMethod = MethodDeclaration(
                    IdentifierName("ValueTask"),
                    "ReplyInterfaceRequest")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("context"))
                        .WithType(
                            IdentifierName("MethodContext")));

            replyInterfaceRequestMethod = switchSections.Count > 0
                ? replyInterfaceRequestMethod.AddModifiers(
                        Token(SyntaxKind.AsyncKeyword))
                    .WithBody(
                        Block(
                            SwitchStatement(
                                    TupleExpression()
                                        .AddArguments(
                                            Argument(
                                                MakeMemberAccessExpression("context", "Request", "MemberAsString")),
                                            Argument(
                                                MakeMemberAccessExpression("context", "Request", "SignatureAsString"))))
                                .WithSections(switchSections)))
                : replyInterfaceRequestMethod.WithExpressionBody(
                    ArrowExpressionClause(
                        LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword))))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken));

            cl = cl.AddMembers(replyInterfaceRequestMethod);
        }

        private void AddHandlerProperties(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            dBusInterface.Properties ??= [];

            MemberDeclarationSyntax[] properties = dBusInterface.Properties.Select(static property =>
                    MakeGetSetProperty(
                        property.DBusDotnetType.ToTypeSyntax(true),
                        Pascalize(property.Name.AsSpan()),
                        property.Access is "write" or "readwrite"
                            ? [Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword)]
                            : [Token(SyntaxKind.PublicKeyword)]))
                .Cast<MemberDeclarationSyntax>()
                .ToArray();

            cl = cl.AddMembers(properties);

            MethodDeclarationSyntax replyGetPropertyMethod = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    "ReplyGetProperty")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("name"))
                        .WithType(
                            PredefinedType(
                                Token(SyntaxKind.StringKeyword))),
                    Parameter(
                            Identifier("context"))
                        .WithType(
                            IdentifierName("MethodContext")));

            SyntaxList<SwitchSectionSyntax> getPropertySwitchSections = List(
                dBusInterface.Properties.Where(static property => property.Access is "read" or "readwrite")
                    .Select(property =>
                        SwitchSection()
                            .AddLabels(
                                CaseSwitchLabel(
                                    MakeLiteralExpression(property.Name!)))
                            .AddStatements(
                                Block(
                                    LocalDeclarationStatement(
                                        VariableDeclaration(IdentifierName("MessageWriter"))
                                            .AddVariables(
                                                VariableDeclarator("writer")
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            InvocationExpression(
                                                                    MakeMemberAccessExpression("context", "CreateReplyWriter"))
                                                                .AddArgumentListArguments(
                                                                    Argument(
                                                                        MakeLiteralExpression("v"))))))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", "WriteSignature"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeLiteralExpression(property.Type!)))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", GetOrAddWriteMethod(property.DBusDotnetType)))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    IdentifierName(
                                                        Pascalize(property.Name.AsSpan()))))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("context", "Reply"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("writer", "CreateMessage"))))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", "Dispose"))),
                                    BreakStatement()))));

            replyGetPropertyMethod = replyGetPropertyMethod.WithBody(
                getPropertySwitchSections.Count > 0
                    ? Block(
                        SwitchStatement(
                                IdentifierName("name"))
                            .WithSections(getPropertySwitchSections))
                    : Block());

            MethodDeclarationSyntax setPropertyMethod = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier("SetProperty"))
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("name"))
                        .WithType(
                            PredefinedType(
                                Token(SyntaxKind.StringKeyword))),
                    Parameter(
                            Identifier("reader"))
                        .WithType(
                            IdentifierName("Reader"))
                        .AddModifiers(
                            Token(SyntaxKind.RefKeyword)));

            SyntaxList<SwitchSectionSyntax> setPropertySwitchSections = List(
                dBusInterface.Properties.Where(static property => property.Access is "write" or "readwrite")
                    .Select(property =>
                        SwitchSection()
                            .AddLabels(
                                CaseSwitchLabel(
                                    MakeLiteralExpression(property.Name!)))
                            .AddStatements(
                                Block(
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("reader", "ReadSignature"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeLiteralExpression(property.Type!)))),
                                    ExpressionStatement(
                                        MakeAssignmentExpression(
                                            IdentifierName(
                                                Pascalize(property.Name.AsSpan())),
                                            InvocationExpression(
                                                MakeMemberAccessExpression("reader", GetOrAddReadMethod(property.DBusDotnetType))))),
                                    BreakStatement()))));

            setPropertyMethod = setPropertyMethod.WithBody(
                setPropertySwitchSections.Count > 0
                    ? Block(
                        SwitchStatement(
                                IdentifierName("name"))
                            .WithSections(setPropertySwitchSections))
                    : Block());

            MemberDeclarationSyntax replyGetAllPropertiesMethod = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    "ReplyGetAllProperties")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("context"))
                        .WithType(
                            IdentifierName("MethodContext")))
                .WithBody(
                    Block(
                        ExpressionStatement(
                            InvocationExpression(
                                IdentifierName("Reply"))),
                        LocalFunctionStatement(
                                PredefinedType(
                                    Token(SyntaxKind.VoidKeyword)),
                                Identifier("Reply"))
                            .AddBodyStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                            IdentifierName("MessageWriter"))
                                        .AddVariables(
                                            VariableDeclarator("writer")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("context", "CreateReplyWriter"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeLiteralExpression("a{sv}"))))))),
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                            IdentifierName("ArrayStart"))
                                        .AddVariables(
                                            VariableDeclarator("dictStart")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                            MakeMemberAccessExpression("writer", "WriteDictionaryStart")))))))
                            .AddBodyStatements(
                                dBusInterface.Properties.Where(static property => property.Access is "read" or "readwrite")
                                    .SelectMany(property =>
                                        new StatementSyntax[]
                                        {
                                            ExpressionStatement(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("writer", "WriteDictionaryEntryStart"))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", "WriteString"))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeLiteralExpression(property.Name!)))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", "WriteSignature"))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeLiteralExpression(property.Type!)))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(property.DBusDotnetType)))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            IdentifierName(
                                                                Pascalize(property.Name.AsSpan())))))
                                        }).ToArray())
                            .AddBodyStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", "WriteDictionaryEnd"))
                                        .AddArgumentListArguments(
                                            Argument(
                                                IdentifierName("dictStart")))),
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("context", "Reply"))
                                        .AddArgumentListArguments(
                                            Argument(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("writer", "CreateMessage"))))))));

            cl = cl.AddMembers(replyGetPropertyMethod, setPropertyMethod, replyGetAllPropertiesMethod);
        }

        private void AddHandlerIntrospect(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            XmlSerializer xmlSerializer = new(typeof(DBusInterface));
            using StringWriter stringWriter = new();
            using XmlWriter xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
            xmlSerializer.Serialize(xmlWriter, dBusInterface);
            string introspect = stringWriter.ToString();

            cl = cl.AddMembers(
                MakeGetOnlyProperty(
                    GenericName("ReadOnlyMemory")
                        .AddTypeArgumentListArguments(
                            PredefinedType(
                                Token(SyntaxKind.ByteKeyword))),
                    "IntrospectXml",
                    Token(SyntaxKind.PublicKeyword))
                    .WithInitializer(
                        EqualsValueClause(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, LiteralExpression(SyntaxKind.Utf8StringLiteralExpression, Utf8Literal(introspect)), IdentifierName("ToArray")))))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)));
        }

        private void AddHandlerSignals(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            if (dBusInterface.Signals is null)
                return;

            foreach (DBusSignal signal in dBusInterface.Signals)
            {
                MethodDeclarationSyntax method = MethodDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.VoidKeyword)),
                        $"Emit{Pascalize(signal.Name.AsSpan())}")
                    .AddModifiers(
                        Token(SyntaxKind.ProtectedKeyword));

                if (signal.Arguments?.Length > 0)
                {
                    method = method.WithParameterList(
                        ParameterList(
                            SeparatedList(
                                signal.Arguments.Select(
                                    static (argument, i) => Parameter(
                                        Identifier(argument.Name is not null
                                            ? SanitizeIdentifier(
                                                Camelize(argument.Name.AsSpan()))
                                            : $"arg{i}"))
                                        .WithType(
                                            argument.DBusDotnetType.ToTypeSyntax(true))))));
                }

                BlockSyntax body = Block();

                body = body.AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("MessageWriter"),
                            SingletonSeparatedList(
                                VariableDeclarator("writer")
                                    .WithInitializer(EqualsValueClause(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("Connection", "GetMessageWriter"))))))));

                ArgumentListSyntax args = ArgumentList()
                    .AddArguments(
                        Argument(
                            LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        Argument(
                            MakeMemberAccessExpression("PathHandler", "Path")),
                        Argument(
                            MakeLiteralExpression(dBusInterface.Name!)),
                        Argument(
                            MakeLiteralExpression(signal.Name!)));

                if (signal.Arguments?.Length > 0)
                {
                    args = args.AddArguments(
                        Argument(
                            MakeLiteralExpression(
                                ParseSignature(signal.Arguments))));
                }

                body = body.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("writer", "WriteSignalHeader"))
                            .WithArgumentList(args)));

                if (signal.Arguments?.Length > 0)
                {
                    for (int i = 0; i < signal.Arguments.Length; i++)
                    {
                        body = body.AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(signal.Arguments[i].DBusDotnetType)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            IdentifierName(signal.Arguments[i].Name is not null
                                                ? Camelize(signal.Arguments[i].Name.AsSpan())
                                                : $"arg{i}")))));
                    }
                }

                body = body.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("Connection", "TrySendMessage"))
                            .AddArgumentListArguments(
                                Argument(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("writer", "CreateMessage"))))),
                    ExpressionStatement(
                        InvocationExpression(
                            MakeMemberAccessExpression("writer", "Dispose"))));

                cl = cl.AddMembers(method.WithBody(body));
            }
        }
    }
}
