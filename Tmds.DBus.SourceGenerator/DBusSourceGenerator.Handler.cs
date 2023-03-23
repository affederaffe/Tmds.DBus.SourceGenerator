using System;
using System.Linq;

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
            ClassDeclarationSyntax cl = ClassDeclaration(Pascalize(dBusInterface.Name!))
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(
                    SimpleBaseType(ParseTypeName("IMethodHandler")))
                .AddMembers(
                    FieldDeclaration(
                            VariableDeclaration(NullableType(ParseTypeName("SynchronizationContext")))
                                .AddVariables(
                                    VariableDeclarator("_synchronizationContext")))
                        .AddModifiers(Token(SyntaxKind.PrivateKeyword)),
                    ConstructorDeclaration(Pascalize(dBusInterface.Name!))
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddParameterListParameters(
                            Parameter(Identifier("emitOnCapturedContext"))
                                .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                                .WithDefault(
                                    EqualsValueClause(
                                        LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                        .WithBody(
                            Block(
                                IfStatement(
                                    IdentifierName("emitOnCapturedContext"),
                                    ExpressionStatement(
                                        AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName("_synchronizationContext"),
                                            MakeMemberAccessExpression("SynchronizationContext", "Current")))))),
                    MakeGetOnlyProperty(ParseTypeName("Connection"), "Connection", Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword)),
                    MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "Path", Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword)));

            MethodDeclarationSyntax handleMethod = MethodDeclaration(ParseTypeName("ValueTask"), "HandleMethodAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("context"))
                        .WithType(ParseTypeName("MethodContext")));

            SwitchStatementSyntax switchStatement = SwitchStatement(MakeMemberAccessExpression("context", "Request", "InterfaceAsString"));

            AddHandlerMethods(ref cl, ref switchStatement, dBusInterface);
            AddHandlerSignals(ref cl, dBusInterface);
            AddHandlerProperties(ref cl, ref switchStatement, dBusInterface);

            if (dBusInterface.Properties?.Length > 0)
                cl = cl.AddMembers(
                    MakeGetOnlyProperty(ParseTypeName("Properties"), "BackingProperties", Token(SyntaxKind.PublicKeyword))
                        .WithInitializer(
                            EqualsValueClause(
                                InvocationExpression(
                                    ObjectCreationExpression(ParseTypeName("Properties")))))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            cl = cl.AddMembers(
                MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), "RunMethodHandlerSynchronously")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("message")).WithType(ParseTypeName("Message")))
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            LiteralExpression(SyntaxKind.TrueLiteralExpression)))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                handleMethod.WithBody(
                    Block(switchStatement)));

            return cl;
        }

        private void AddHandlerMethods(ref ClassDeclarationSyntax cl, ref SwitchStatementSyntax sw, DBusInterface dBusInterface)
        {
            if (dBusInterface.Methods is null) return;

            SyntaxList<SwitchSectionSyntax> switchSections = List<SwitchSectionSyntax>();

            foreach (DBusMethod dBusMethod in dBusInterface.Methods)
            {
                DBusArgument[]? inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
                DBusArgument[]? outArgs = dBusMethod.Arguments?.Where(static m => m.Direction == "out").ToArray();

                SwitchSectionSyntax switchSection = SwitchSection()
                    .AddLabels(
                        CaseSwitchLabel(
                            TupleExpression()
                                .AddArguments(
                                    Argument(MakeLiteralExpression(dBusMethod.Name!)),
                                    Argument(inArgs?.Length > 0 ? MakeLiteralExpression(ParseSignature(inArgs)!) : LiteralExpression(SyntaxKind.NullLiteralExpression)))));

                BlockSyntax switchSectionBlock = Block();

                string abstractMethodName = $"On{Pascalize(dBusMethod.Name!)}Async";

                MethodDeclarationSyntax abstractMethod = outArgs?.Length > 0
                    ? MethodDeclaration(ParseTypeName($"ValueTask<{ParseReturnType(outArgs)!}>"), abstractMethodName)
                    : MethodDeclaration(ParseTypeName("ValueTask"), abstractMethodName);

                if (inArgs?.Length > 0)
                    abstractMethod = abstractMethod.WithParameterList(ParseParameterList(inArgs));

                abstractMethod = abstractMethod
                    .AddModifiers(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                cl = cl.AddMembers(abstractMethod);

                if (inArgs?.Length > 0)
                {
                    BlockSyntax readParametersMethodBlock = Block(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName("Reader"))
                                .AddVariables(
                                    VariableDeclarator("reader")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(MakeMemberAccessExpression("context", "Request", "GetBodyReader")))))));

                    SyntaxList<StatementSyntax> argFields = List<StatementSyntax>();

                    for (int i = 0; i < inArgs.Length; i++)
                    {
                        string identifier = inArgs[i].Name is not null ? SanitizeIdentifier(inArgs[i].Name!) : $"arg{i}";
                        readParametersMethodBlock = readParametersMethodBlock.AddStatements(
                            ExpressionStatement(
                                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(identifier), InvocationExpression(
                                    MakeMemberAccessExpression("reader", GetOrAddReadMethod(inArgs[i]))))));
                        argFields = argFields.Add(
                            LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName(inArgs[i].DotNetType))
                                    .AddVariables(
                                        VariableDeclarator(identifier))));
                    }

                    switchSectionBlock = switchSectionBlock.AddStatements(argFields.ToArray());
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                IdentifierName("ReadParameters"))),
                        LocalFunctionStatement(
                                PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("ReadParameters"))
                            .WithBody(readParametersMethodBlock));

                    if (outArgs is null || outArgs.Length == 0)
                    {
                        switchSectionBlock = switchSectionBlock.AddStatements(
                            IfStatement(
                        IsPatternExpression(
                            IdentifierName("_synchronizationContext"), UnaryPattern(ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                        Block(
                            LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName("TaskCompletionSource<bool>"))
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
                                                                ExpressionStatement(
                                                                    AwaitExpression(
                                                                            InvocationExpression(
                                                                                    IdentifierName(abstractMethodName))
                                                                                .AddArgumentListArguments(
                                                                                    inArgs.Select(static (x, i) =>
                                                                                        Argument(
                                                                                            IdentifierName(x.Name is not null ? SanitizeIdentifier(x.Name) : $"arg{i}"))).ToArray()))),
                                                                ExpressionStatement(
                                                                    InvocationExpression(
                                                                            MakeMemberAccessExpression("tsc", "SetResult"))
                                                                        .AddArgumentListArguments(
                                                                            Argument(
                                                                                LiteralExpression(SyntaxKind.TrueLiteralExpression)))))
                                                            .AddCatches(
                                                                CatchClause()
                                                                    .WithDeclaration(CatchDeclaration(ParseTypeName("Exception"))
                                                                        .WithIdentifier(Identifier("e")))
                                                                    .AddBlockStatements(
                                                                        ExpressionStatement(
                                                                            InvocationExpression(
                                                                                    MakeMemberAccessExpression("tsc", "SetException"))
                                                                                .AddArgumentListArguments(
                                                                                    Argument(IdentifierName("e"))))))))),
                                        Argument(
                                            LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                            ExpressionStatement(
                                AwaitExpression(
                                    MakeMemberAccessExpression("tsc", "Task")))),
                        ElseClause(
                            Block(
                            ExpressionStatement(
                                AwaitExpression(
                                    InvocationExpression(
                                            IdentifierName(abstractMethodName))
                                        .AddArgumentListArguments(
                                            inArgs.Select(static (x, i) =>
                                                Argument(
                                                    IdentifierName(x.Name is not null ? SanitizeIdentifier(x.Name) : $"arg{i}"))).ToArray())))))));
                    }
                }

                if (outArgs?.Length > 0)
                {
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName(ParseReturnType(outArgs)!))
                                .AddVariables(
                                    VariableDeclarator("ret"))),
                        IfStatement(
                        IsPatternExpression(
                            IdentifierName("_synchronizationContext"), UnaryPattern(ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                        Block(
                            LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName($"TaskCompletionSource<{ParseReturnType(outArgs)!}>"))
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
                                                                LocalDeclarationStatement(
                                                                    VariableDeclaration(
                                                                            ParseTypeName(ParseReturnType(outArgs)!))
                                                                        .AddVariables(
                                                                            VariableDeclarator("ret1")
                                                                                .WithInitializer(
                                                                                    EqualsValueClause(
                                                                                        AwaitExpression(
                                                                                            InvocationExpression(
                                                                                                    IdentifierName(abstractMethodName))
                                                                                                .AddArgumentListArguments(
                                                                                                    inArgs?.Select(static (x, i) =>
                                                                                                        Argument(
                                                                                                            IdentifierName(x.Name is not null ? SanitizeIdentifier(x.Name) : $"arg{i}"))).ToArray() ?? Array.Empty<ArgumentSyntax>())))))),
                                                                ExpressionStatement(
                                                                    InvocationExpression(
                                                                            MakeMemberAccessExpression("tsc", "SetResult"))
                                                                        .AddArgumentListArguments(
                                                                            Argument(IdentifierName("ret1")))))
                                                            .AddCatches(
                                                                CatchClause()
                                                                    .WithDeclaration(CatchDeclaration(ParseTypeName("Exception"))
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
                                MakeAssignmentExpression(IdentifierName("ret"), AwaitExpression(
                                    MakeMemberAccessExpression("tsc", "Task"))))),
                        ElseClause(
                            Block(
                            ExpressionStatement(
                                MakeAssignmentExpression(IdentifierName("ret"), AwaitExpression(
                                    InvocationExpression(
                                            IdentifierName(abstractMethodName))
                                        .AddArgumentListArguments(
                                            inArgs?.Select(static (x, i) =>
                                                Argument(
                                                    IdentifierName(x.Name is not null ? SanitizeIdentifier(x.Name) : $"arg{i}"))).ToArray() ?? Array.Empty<ArgumentSyntax>()))))))));

                    BlockSyntax replyMethodBlock = Block(
                        LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName("MessageWriter"))
                                    .AddVariables(
                                        VariableDeclarator("writer")
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    InvocationExpression(
                                                            MakeMemberAccessExpression("context", "CreateReplyWriter"))
                                                        .AddArgumentListArguments(
                                                            Argument(MakeLiteralExpression(ParseSignature(outArgs)!))))))));

                    if (outArgs.Length == 1)
                    {
                        replyMethodBlock = replyMethodBlock.AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(outArgs[0])))
                                    .AddArgumentListArguments(
                                        Argument(
                                            IdentifierName("ret")))));
                    }
                    else
                    {
                        for (int i = 0; i < outArgs.Length; i++)
                        {
                            replyMethodBlock = replyMethodBlock.AddStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", GetOrAddWriteMethod(outArgs[i])))
                                        .AddArgumentListArguments(
                                            Argument(
                                                MakeMemberAccessExpression("ret", outArgs[i].Name is not null ? SanitizeIdentifier(outArgs[i].Name!) : $"Item{i + 1}")))));
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
                        ExpressionStatement(
                            InvocationExpression(
                                IdentifierName("Reply"))),
                        LocalFunctionStatement(
                                PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Reply"))
                            .WithBody(replyMethodBlock));
                }

                switchSectionBlock = switchSectionBlock.AddStatements(BreakStatement());

                switchSections = switchSections.Add(switchSection.AddStatements(switchSectionBlock));
            }

            sw = sw.AddSections(
                SwitchSection()
                    .AddLabels(
                        CaseSwitchLabel(MakeLiteralExpression(dBusInterface.Name!)))
                    .AddStatements(
                        SwitchStatement(
                                TupleExpression()
                                    .AddArguments(
                                        Argument(MakeMemberAccessExpression("context", "Request", "MemberAsString")),
                                        Argument(MakeMemberAccessExpression("context", "Request", "SignatureAsString"))))
                            .WithSections(switchSections),
                        BreakStatement()));
        }

        private void AddHandlerProperties(ref ClassDeclarationSyntax cl, ref SwitchStatementSyntax sw, DBusInterface dBusInterface)
        {
            if (dBusInterface.Properties is null) return;

            SyntaxList<StatementSyntax> addPropertiesStatements = List<StatementSyntax>();
            foreach (DBusProperty dBusProperty in dBusInterface.Properties)
            {
                if (dBusProperty.DBusType is DBusType.Bool or DBusType.Byte or DBusType.Double or DBusType.Int16 or DBusType.UInt16 or DBusType.Int32 or DBusType.UInt32 or DBusType.Int64 or DBusType.UInt64 or DBusType.Signature or DBusType.ObjectPath or DBusType.Struct)
                    addPropertiesStatements = addPropertiesStatements.Add(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("dict", "Add"))
                                .AddArgumentListArguments(
                                    Argument(
                                        MakeLiteralExpression(dBusProperty.Name!)),
                                    Argument(
                                        InvocationExpression(
                                                ObjectCreationExpression(
                                                    ParseTypeName("DBusVariantItem")))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeLiteralExpression(ParseSignature(new[] { dBusProperty })!)),
                                                Argument(
                                                    MakeGetDBusVariantExpression(dBusProperty,
                                                        MakeMemberAccessExpression("BackingProperties", dBusProperty.Name!))))))));
                else
                    addPropertiesStatements = addPropertiesStatements.Add(
                        IfStatement(
                            IsPatternExpression(
                                MakeMemberAccessExpression("BackingProperties", dBusProperty.Name!), UnaryPattern(
                                    ConstantPattern(
                                        LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("dict", "Add"))
                                    .AddArgumentListArguments(
                                        Argument(
                                            MakeLiteralExpression(dBusProperty.Name!)),
                                        Argument(
                                            InvocationExpression(
                                                    ObjectCreationExpression(
                                                        ParseTypeName("DBusVariantItem")))
                                                .AddArgumentListArguments(
                                                    Argument(
                                                        MakeLiteralExpression(ParseSignature(new[] { dBusProperty })!)),
                                                    Argument(
                                                        MakeGetDBusVariantExpression(dBusProperty,
                                                            MakeMemberAccessExpression("BackingProperties", dBusProperty.Name!)))))))));
            }

            sw = sw.AddSections(
                SwitchSection()
                    .AddLabels(
                        CaseSwitchLabel(MakeLiteralExpression("org.freedesktop.DBus.Properties")))
                    .AddStatements(
                        SwitchStatement(
                                TupleExpression()
                                    .AddArguments(
                                        Argument(MakeMemberAccessExpression("context", "Request", "MemberAsString")),
                                        Argument(MakeMemberAccessExpression("context", "Request", "SignatureAsString"))))
                            .AddSections(
                                SwitchSection()
                                    .AddLabels(
                                        CaseSwitchLabel(
                                            TupleExpression()
                                                .AddArguments(
                                                    Argument(MakeLiteralExpression("Get")),
                                                    Argument(MakeLiteralExpression("ss")))))
                                    .AddStatements(
                                        Block(
                                            ExpressionStatement(
                                                InvocationExpression(IdentifierName("Reply"))),
                                            LocalFunctionStatement(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Reply"))
                                                .AddBodyStatements(
                                                    LocalDeclarationStatement(
                                                        VariableDeclaration(ParseTypeName("Reader"))
                                                            .AddVariables(
                                                                VariableDeclarator("reader")
                                                                    .WithInitializer(
                                                                        EqualsValueClause(
                                                                            InvocationExpression(
                                                                                MakeMemberAccessExpression("context", "Request",
                                                                                    "GetBodyReader")))))),
                                                    ExpressionStatement(
                                                        InvocationExpression(
                                                            MakeMemberAccessExpression("reader", "ReadString"))),
                                                    LocalDeclarationStatement(
                                                        VariableDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)))
                                                            .AddVariables(
                                                                VariableDeclarator("member")
                                                                    .WithInitializer(
                                                                        EqualsValueClause(
                                                                            InvocationExpression(
                                                                                MakeMemberAccessExpression("reader", "ReadString")))))),
                                                    SwitchStatement(IdentifierName("member"))
                                                        .WithSections(
                                                            List(
                                                                dBusInterface.Properties.Select(static dBusProperty =>
                                                                    SwitchSection()
                                                                        .AddLabels(
                                                                            CaseSwitchLabel(
                                                                                MakeLiteralExpression(dBusProperty.Name!)))
                                                                        .AddStatements(
                                                                            Block(
                                                                                LocalDeclarationStatement(
                                                                                    VariableDeclaration(ParseTypeName("MessageWriter"))
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
                                                                                            MakeMemberAccessExpression("writer", "WriteDBusVariant"))
                                                                                        .AddArgumentListArguments(
                                                                                            Argument(
                                                                                                InvocationExpression(
                                                                                                        ObjectCreationExpression(
                                                                                                            ParseTypeName("DBusVariantItem")))
                                                                                                    .AddArgumentListArguments(
                                                                                                        Argument(
                                                                                                            MakeLiteralExpression(ParseSignature(new[] { dBusProperty })!)),
                                                                                                        Argument(
                                                                                                            MakeGetDBusVariantExpression(dBusProperty,
                                                                                                                MakeMemberAccessExpression("BackingProperties", dBusProperty.Name!))))))),
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
                                                                                BreakStatement())))))),
                                            BreakStatement())),
                                SwitchSection()
                                    .AddLabels(
                                        CaseSwitchLabel(
                                            TupleExpression()
                                                .AddArguments(
                                                    Argument(MakeLiteralExpression("GetAll")),
                                                    Argument(MakeLiteralExpression("s")))))
                                    .AddStatements(
                                        Block(
                                            ExpressionStatement(
                                                InvocationExpression(IdentifierName("Reply"))),
                                            LocalFunctionStatement(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Reply"))
                                                .AddBodyStatements(
                                                    LocalDeclarationStatement(
                                                        VariableDeclaration(ParseTypeName("MessageWriter"))
                                                            .AddVariables(
                                                                VariableDeclarator("writer")
                                                                    .WithInitializer(
                                                                        EqualsValueClause(
                                                                            InvocationExpression(
                                                                                    MakeMemberAccessExpression("context", "CreateReplyWriter"))
                                                                                .AddArgumentListArguments(
                                                                                    Argument(MakeLiteralExpression("a{sv}"))))))),
                                                    LocalDeclarationStatement(
                                                        VariableDeclaration(ParseTypeName("Dictionary<string, DBusVariantItem>"))
                                                            .AddVariables(
                                                                VariableDeclarator("dict")
                                                                    .WithInitializer(
                                                                        EqualsValueClause(
                                                                            InvocationExpression(
                                                                                ObjectCreationExpression(
                                                                                    ParseTypeName("Dictionary<string, DBusVariantItem>"))))))),
                                                    Block(addPropertiesStatements),
                                                    ExpressionStatement(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("writer", GetOrAddWriteMethod(new DBusValue { Type = "a{sv}" })))
                                                            .AddArgumentListArguments(
                                                                Argument(IdentifierName("dict")))),
                                                    ExpressionStatement(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("context", "Reply"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    InvocationExpression(MakeMemberAccessExpression("writer", "CreateMessage")))))),
                                            BreakStatement()))),
                        BreakStatement()));

            AddPropertiesClass(ref cl, dBusInterface);
        }

        private void AddHandlerSignals(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
        {
            if (dBusInterface.Signals is null) return;
            foreach (DBusSignal dBusSignal in dBusInterface.Signals)
            {
                MethodDeclarationSyntax method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), $"Emit{Pascalize(dBusSignal.Name!)}")
                    .AddModifiers(Token(SyntaxKind.ProtectedKeyword));

                if (dBusSignal.Arguments?.Length > 0)
                    method = method.WithParameterList(
                        ParameterList(
                            SeparatedList(
                                dBusSignal.Arguments.Select(
                                    static (x, i) => Parameter(Identifier(x.Name is not null ? SanitizeIdentifier(x.Name) : $"arg{i}")).WithType(ParseTypeName(x.DotNetType))))));

                BlockSyntax body = Block();

                body = body.AddStatements(
                    LocalDeclarationStatement(VariableDeclaration(ParseTypeName("MessageWriter"),
                            SingletonSeparatedList(
                                VariableDeclarator("writer")
                                    .WithInitializer(EqualsValueClause(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("Connection", "GetMessageWriter"))))))));

                ArgumentListSyntax args = ArgumentList()
                    .AddArguments(
                        Argument(LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        Argument(IdentifierName("Path")),
                        Argument(MakeLiteralExpression(dBusInterface.Name!)),
                        Argument(MakeLiteralExpression(dBusSignal.Name!)));

                if (dBusSignal.Arguments?.Length > 0)
                    args = args.AddArguments(
                        Argument(MakeLiteralExpression(ParseSignature(dBusSignal.Arguments)!)));

                body = body.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("writer", "WriteSignalHeader"))
                            .WithArgumentList(args)));

                if (dBusSignal.Arguments?.Length > 0)
                {
                    for (int i = 0; i < dBusSignal.Arguments.Length; i++)
                    {
                        body = body.AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusSignal.Arguments[i])))
                                    .AddArgumentListArguments(
                                        Argument(
                                            IdentifierName(dBusSignal.Arguments[i].Name ?? $"arg{i}")))));
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
