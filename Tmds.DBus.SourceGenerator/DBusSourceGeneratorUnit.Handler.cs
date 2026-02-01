using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tmds.DBus.Protocol;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorUtils;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorParsing;


namespace Tmds.DBus.SourceGenerator;

public partial class DBusSourceGeneratorUnit
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
                        IdentifierName(nameof(SynchronizationContext)))),
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
                                        MakeMemberAccessExpression(nameof(SynchronizationContext), nameof(SynchronizationContext.Current))))))),
                MakeGetSetProperty(
                    NullableType(
                        IdentifierName("PathHandler")),
                    "PathHandler",
                    Token(SyntaxKind.PublicKeyword)),
                MakeGetOnlyProperty(
                    IdentifierName(nameof(DBusConnection)),
                    "Connection",
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AbstractKeyword)),
                PropertyDeclaration(
                        GenericName(nameof(ReadOnlySpan<>))
                            .WithTypeArgumentList(
                                MakeSingletonTypeArgumentList(SyntaxKind.ByteKeyword)),
                        "InterfaceName")
                    .WithModifiers(
                        TokenList(
                            Token(SyntaxKind.PublicKeyword)))
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            MakeUtf8StringLiteralExpression(dBusInterface.Name!)))
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

        LocalFunctionStatementSyntax determineMethodIndex = LocalFunctionStatement(
                PredefinedType(
                    Token(SyntaxKind.IntKeyword)),
                Identifier("DetermineMethodIndex"))
            .WithBody(
                Block(
                    dBusInterface.Methods.Select((dBusMethod, i) =>
                        {
                            DBusArgument[]? inArgs = GetInArgs(dBusMethod.Arguments);
                            return IfStatement(
                                BinaryExpression(SyntaxKind.LogicalAndExpression,
                                    InvocationExpression(
                                            MakeMemberAccessExpression("context", nameof(MethodContext.Request), nameof(Message.Member), nameof(MemoryExtensions.SequenceEqual)))
                                        .WithArgumentList(
                                            MakeSingletonArgumentList(
                                                MakeUtf8StringLiteralExpression(dBusMethod.Name!))),
                                    inArgs?.Length > 0
                                        ? InvocationExpression(
                                                MakeMemberAccessExpression("context", nameof(MethodContext.Request), nameof(Message.Signature), nameof(MemoryExtensions.SequenceEqual)))
                                            .WithArgumentList(
                                                MakeSingletonArgumentList(
                                                    MakeUtf8StringLiteralExpression(
                                                        ParseSignature(inArgs))))
                                        : MakeMemberAccessExpression("context", nameof(MethodContext.Request), nameof(Message.Signature), nameof(ReadOnlySpan<>.IsEmpty))),
                                ReturnStatement(
                                    MakeLiteralExpression(i)));
                        })
                        .Aggregate((current, ifStatement) => ifStatement.WithElse(ElseClause(current))),
                    ReturnStatement(
                        MakeLiteralExpression(-1))));

        for (int i = 0; i < dBusInterface.Methods.Length; i++)
        {
            DBusMethod dBusMethod = dBusInterface.Methods[i];
            DBusArgument[]? inArgs = GetInArgs(dBusMethod.Arguments);
            DBusArgument[]? outArgs = GetOutArgs(dBusMethod.Arguments);

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
                                    IdentifierName(nameof(Message))))))
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
                        VariableDeclaration(
                                IdentifierName(nameof(Reader)))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("context", nameof(MethodContext.Request), nameof(Message.GetBodyReader))))))));

                StatementSyntax[] argFields = new StatementSyntax[inArgs.Length];

                for (int j = 0; j < inArgs.Length; j++)
                {
                    string identifier = inArgs[j].Name is not null
                        ? SanitizeIdentifier(Pascalize(inArgs[j].Name.AsSpan(), true))
                        : $"arg{j}";
                    readParametersMethodBlock = readParametersMethodBlock.AddStatements(
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(identifier), InvocationExpression(
                                MakeMemberAccessExpression("reader", readWriteMethodsCache.GetOrAddReadMethod(inArgs[j].DBusDotnetType))))));
                    argFields[j] = LocalDeclarationStatement(
                        VariableDeclaration(inArgs[j].DBusDotnetType.ToTypeSyntax())
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
                                    MakeMemberAccessExpression("context", nameof(MethodContext.Request))))))
                    .AddArgumentListArguments(
                        inArgs?.Select(static (argument, i) =>
                                Argument(
                                    IdentifierName(argument.Name is not null
                                        ? SanitizeIdentifier(
                                            Pascalize(argument.Name.AsSpan(), true))
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
                                    MakeMemberAccessExpression("_synchronizationContext", nameof(SynchronizationContext.Post)))
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
                                                                        MakeMemberAccessExpression("tsc", nameof(TaskCompletionSource<>.SetResult)))
                                                                    .WithArgumentList(
                                                                        MakeSingletonArgumentList<ExpressionSyntax>(
                                                                            outArgs?.Length > 0
                                                                                ? IdentifierName("ret1")
                                                                                : LiteralExpression(SyntaxKind.TrueLiteralExpression)))))
                                                        .AddCatches(
                                                            CatchClause()
                                                                .WithDeclaration(
                                                                    CatchDeclaration(
                                                                            IdentifierName(nameof(Exception)))
                                                                        .WithIdentifier(
                                                                            Identifier("e")))
                                                                .WithBlock(
                                                                    Block(
                                                                        ExpressionStatement(
                                                                            InvocationExpression(
                                                                                    MakeMemberAccessExpression("tsc", nameof(TaskCompletionSource<>.SetException)))
                                                                                .WithArgumentList(
                                                                                    MakeSingletonArgumentList(
                                                                                        IdentifierName("e")))))))))),
                                    Argument(
                                        LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                        ExpressionStatement(
                            outArgs?.Length > 0
                                ? MakeAssignmentExpression(
                                    IdentifierName("ret"), AwaitExpression(
                                        MakeMemberAccessExpression("tsc", nameof(TaskCompletionSource<>.Task))))
                                : AwaitExpression(
                                    MakeMemberAccessExpression("tsc", nameof(TaskCompletionSource<>.Task))))),
                    ElseClause(
                        Block(
                            ExpressionStatement(
                                outArgs?.Length > 0
                                    ? MakeAssignmentExpression(
                                        IdentifierName("ret"), callAbstractMethod)
                                    : callAbstractMethod)))));

            BlockSyntax replyMethodBlock = Block(
                LocalDeclarationStatement(
                    VariableDeclaration(
                            IdentifierName(nameof(MessageWriter)))
                        .AddVariables(
                            VariableDeclarator("writer")
                                .WithInitializer(
                                    EqualsValueClause(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("context", nameof(MethodContext.CreateReplyWriter)))
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
                                MakeMemberAccessExpression("writer", readWriteMethodsCache.GetOrAddWriteMethod(outArgs[0].DBusDotnetType)))
                            .AddArgumentListArguments(
                                Argument(
                                    IdentifierName("ret")))));
            }
            else if (outArgs?.Length > 1)
            {
                for (int j = 0; j < outArgs.Length; j++)
                {
                    replyMethodBlock = replyMethodBlock.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", readWriteMethodsCache.GetOrAddWriteMethod(outArgs[j].DBusDotnetType)))
                                .WithArgumentList(
                                    MakeSingletonArgumentList(
                                        MakeMemberAccessExpression("ret", outArgs[j].Name is not null
                                            ? SanitizeIdentifier(Pascalize(outArgs[j].Name.AsSpan()))
                                            : $"Item{j + 1}")))));
                }
            }

            replyMethodBlock = replyMethodBlock.AddStatements(
                ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("context", nameof(MethodContext.Reply)))
                        .WithArgumentList(
                            MakeSingletonArgumentList(
                                InvocationExpression(
                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.CreateMessage)))))),
                ExpressionStatement(
                    InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.Dispose)))));

            switchSectionBlock = switchSectionBlock.AddStatements(
                IfStatement(
                    PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, MakeMemberAccessExpression("context", nameof(MethodContext.NoReplyExpected))),
                    ExpressionStatement(
                        InvocationExpression(
                            IdentifierName("Reply")))),
                LocalFunctionStatement(
                        PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Reply"))
                    .WithBody(replyMethodBlock),
                BreakStatement());

            switchSections = switchSections.Add(
                SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            CasePatternSwitchLabel(
                                ConstantPattern(
                                    MakeLiteralExpression(i)),
                                Token(SyntaxKind.ColonToken))))
                    .WithStatements(
                        SingletonList<StatementSyntax>(switchSectionBlock)));
        }

        MethodDeclarationSyntax replyInterfaceRequestMethod = MethodDeclaration(
                IdentifierName(nameof(ValueTask)),
                "ReplyInterfaceRequestAsync")
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("context"))
                    .WithType(
                        IdentifierName(nameof(MethodContext))));

        replyInterfaceRequestMethod = switchSections.Count > 0
            ? replyInterfaceRequestMethod.AddModifiers(
                    Token(SyntaxKind.AsyncKeyword))
                .WithBody(
                    Block(
                        determineMethodIndex,
                        SwitchStatement(
                                InvocationExpression(
                                    IdentifierName(determineMethodIndex.Identifier)))
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
                        GenericName(nameof(ReadOnlySpan<>))
                            .WithTypeArgumentList(
                                MakeSingletonTypeArgumentList(SyntaxKind.ByteKeyword))),
                Parameter(
                        Identifier("context"))
                    .WithType(
                        IdentifierName(nameof(MethodContext))));

        DBusProperty[] readableProperties = dBusInterface.Properties.Where(static property => property.Access is "read" or "readwrite").ToArray();
        BlockSyntax getPropertyBody = readableProperties.Length == 0
            ? Block()
            : Block(
                readableProperties.Select(property => IfStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("name", nameof(MemoryExtensions.SequenceEqual)))
                            .AddArgumentListArguments(
                                Argument(
                                    MakeUtf8StringLiteralExpression(property.Name!))),
                        Block(
                            LocalDeclarationStatement(
                                VariableDeclaration(
                                        IdentifierName(nameof(MessageWriter)))
                                    .AddVariables(
                                        VariableDeclarator("writer")
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    InvocationExpression(
                                                            MakeMemberAccessExpression("context", nameof(MethodContext.CreateReplyWriter)))
                                                        .AddArgumentListArguments(
                                                            Argument(
                                                                MakeLiteralExpression("v"))))))),
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteSignature)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            MakeUtf8StringLiteralExpression(property.Type!)))),
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", readWriteMethodsCache.GetOrAddWriteMethod(property.DBusDotnetType)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            IdentifierName(
                                                Pascalize(property.Name.AsSpan()))))),
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("context", nameof(MethodContext.Reply)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("writer", nameof(MessageWriter.CreateMessage)))))),
                            ExpressionStatement(
                                InvocationExpression(
                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.Dispose)))))
                    ))
                    .Aggregate((current, ifStatement) => ifStatement.WithElse(ElseClause(current)))
            );

        replyGetPropertyMethod = replyGetPropertyMethod.WithBody(getPropertyBody);

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
                        GenericName(nameof(ReadOnlySpan<>))
                            .WithTypeArgumentList(
                                MakeSingletonTypeArgumentList(SyntaxKind.ByteKeyword))),
                Parameter(
                        Identifier("reader"))
                    .WithType(
                        IdentifierName(nameof(Reader)))
                    .AddModifiers(
                        Token(SyntaxKind.RefKeyword)));

        DBusProperty[] settableProperties = dBusInterface.Properties.Where(static property => property.Access is "write" or "readwrite").ToArray();
        BlockSyntax setPropertyBody = readableProperties.Length == 0
            ? Block()
            : Block(
                settableProperties.Select(property => IfStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("name", nameof(MemoryExtensions.SequenceEqual)))
                        .AddArgumentListArguments(
                            Argument(
                                MakeUtf8StringLiteralExpression(property.Name!))),
                    Block(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("reader", nameof(Reader.ReadSignature)))
                                .AddArgumentListArguments(
                                    Argument(
                                        MakeUtf8StringLiteralExpression(property.Type!)))),
                        ExpressionStatement(
                            MakeAssignmentExpression(
                                IdentifierName(
                                    Pascalize(property.Name.AsSpan())),
                                InvocationExpression(
                                    MakeMemberAccessExpression("reader", readWriteMethodsCache.GetOrAddReadMethod(property.DBusDotnetType))))))))
            );

        setPropertyMethod = setPropertyMethod.WithBody(setPropertyBody);

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
                        IdentifierName(nameof(MethodContext))))
            .WithBody(
                Block()
                    .AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    IdentifierName(nameof(MessageWriter)))
                                .AddVariables(
                                    VariableDeclarator("writer")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("context", nameof(MethodContext.CreateReplyWriter)))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeLiteralExpression("a{sv}"))))))),
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    IdentifierName(nameof(ArrayStart)))
                                .AddVariables(
                                    VariableDeclarator("dictStart")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteDictionaryStart))))))))
                    .AddStatements(
                        readableProperties.SelectMany(property =>
                                new StatementSyntax[]
                                {
                                    ExpressionStatement(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteDictionaryEntryStart)))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteString)))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeUtf8StringLiteralExpression(property.Name!)))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteSignature)))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeUtf8StringLiteralExpression(property.Type!)))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", readWriteMethodsCache.GetOrAddWriteMethod(property.DBusDotnetType)))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    IdentifierName(
                                                        Pascalize(property.Name.AsSpan())))))
                                }).ToArray())
                    .AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteDictionaryEnd)))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("dictStart")))),
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("context", nameof(MethodContext.Reply)))
                                .AddArgumentListArguments(
                                    Argument(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", nameof(MessageWriter.CreateMessage))))))));

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
                    GenericName(nameof(ReadOnlyMemory<>))
                        .WithTypeArgumentList(
                            MakeSingletonTypeArgumentList(SyntaxKind.ByteKeyword)),
                    "IntrospectXml",
                    Token(SyntaxKind.PublicKeyword))
                .WithInitializer(
                    EqualsValueClause(
                        InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, MakeUtf8StringLiteralExpression(introspect), IdentifierName("ToArray")))))
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
                            signal.Arguments.Select(static (argument, i) => Parameter(
                                    Identifier(argument.Name is not null
                                        ? SanitizeIdentifier(
                                            Pascalize(argument.Name.AsSpan(), true))
                                        : $"arg{i}"))
                                .WithType(
                                    argument.DBusDotnetType.ToTypeSyntax(true))))));
            }

            BlockSyntax body = Block();

            body = body.AddStatements(
                LocalDeclarationStatement(
                    VariableDeclaration(
                        IdentifierName(nameof(MessageWriter)),
                        SingletonSeparatedList(
                            VariableDeclarator("writer")
                                .WithInitializer(
                                    EqualsValueClause(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("Connection", nameof(Connection.GetMessageWriter)))))))));

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
                            MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteSignalHeader)))
                        .WithArgumentList(args)));

            if (signal.Arguments?.Length > 0)
            {
                for (int i = 0; i < signal.Arguments.Length; i++)
                {
                    body = body.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", readWriteMethodsCache.GetOrAddWriteMethod(signal.Arguments[i].DBusDotnetType)))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName(signal.Arguments[i].Name is not null
                                            ? Pascalize(signal.Arguments[i].Name.AsSpan(), true)
                                            : $"arg{i}")))));
                }
            }

            body = body.AddStatements(
                ExpressionStatement(
                    InvocationExpression(
                            MakeMemberAccessExpression("Connection", nameof(Connection.TrySendMessage)))
                        .AddArgumentListArguments(
                            Argument(
                                InvocationExpression(
                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.CreateMessage)))))),
                ExpressionStatement(
                    InvocationExpression(
                        MakeMemberAccessExpression("writer", nameof(MessageWriter.Dispose)))));

            cl = cl.AddMembers(method.WithBody(body));
        }
    }
}
