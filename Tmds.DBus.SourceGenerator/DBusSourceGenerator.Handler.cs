using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static ClassDeclarationSyntax GenerateHandler(DBusInterface dBusInterface)
        {
            ClassDeclarationSyntax cl = ClassDeclaration(Pascalize(dBusInterface.Name!))
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(
                    SimpleBaseType(ParseTypeName("IMethodHandler")));

            MethodDeclarationSyntax handleMethod = MethodDeclaration(ParseTypeName("ValueTask"), "HandleMethodAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("context"))
                        .WithType(ParseTypeName("MethodContext")));

            SwitchStatementSyntax switchStatement = SwitchStatement(MakeMemberAccessExpression("context", "Request", "InterfaceAsString"));

            AddMethods(ref cl, ref switchStatement, dBusInterface);
            AddProperties(ref cl, ref switchStatement, dBusInterface);

            cl = cl.AddMembers(
                MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "Path", Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword)));

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
                    Block(switchStatement, ReturnStatement(DefaultExpression(ParseTypeName("ValueTask"))))));

            return cl;
        }

        private static void AddMethods(ref ClassDeclarationSyntax cl, ref SwitchStatementSyntax sw, DBusInterface dBusInterface)
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
                                    Argument(MakeLiteralExpression(ParseSignature(inArgs) ?? string.Empty)))));

                BlockSyntax switchSectionBlock = Block();

                string abstractMethodName = $"On{dBusMethod.Name}";

                MethodDeclarationSyntax abstractMethod = outArgs?.Length > 0
                    ? MethodDeclaration(ParseTypeName(ParseReturnType(outArgs)!), abstractMethodName)
                    : MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), abstractMethodName);

                if (inArgs?.Length > 0)
                    abstractMethod = abstractMethod.WithParameterList(ParseParameterList(inArgs));

                abstractMethod = abstractMethod
                    .AddModifiers(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                cl = cl.AddMembers(abstractMethod);

                if (inArgs?.Length > 0)
                {
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName("Reader"))
                                .AddVariables(
                                    VariableDeclarator("reader")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(MakeMemberAccessExpression("context", "Request", "GetBodyReader")))))));

                    for (int i = 0; i < inArgs.Length; i++)
                    {
                        switchSectionBlock = switchSectionBlock.AddStatements(
                            LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName(inArgs[i].DotNetType))
                                    .AddVariables(
                                        VariableDeclarator(inArgs[i].Name ?? $"arg{i}")
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", $"Read{ParseReadWriteMethod(inArgs[i])}")))))));
                    }

                    if (outArgs is null || outArgs.Length == 0)
                        switchSectionBlock = switchSectionBlock.AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        IdentifierName(abstractMethodName))
                                    .AddArgumentListArguments(
                                        inArgs.Select(static (x, i) =>
                                            Argument(IdentifierName(x.Name ?? $"arg{i}"))).ToArray())));
                }

                if (outArgs?.Length > 0)
                {
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName(ParseReturnType(outArgs)!))
                                .AddVariables(
                                    VariableDeclarator("ret")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                inArgs?.Length > 0
                                                    ? InvocationExpression(
                                                            IdentifierName(abstractMethodName))
                                                        .AddArgumentListArguments(
                                                            inArgs.Select(static (x, i) =>
                                                                Argument(IdentifierName(x.Name ?? $"arg{i}"))).ToArray())
                                                    : InvocationExpression(
                                                        IdentifierName(abstractMethodName)))))),
                        LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName("MessageWriter"))
                                    .AddVariables(
                                        VariableDeclarator("writer")
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    InvocationExpression(
                                                            MakeMemberAccessExpression("context", "CreateReplyWriter"))
                                                        .AddArgumentListArguments(
                                                            Argument(MakeLiteralExpression(ParseSignature(outArgs)!)))))))
                            .WithUsingKeyword(Token(SyntaxKind.UsingKeyword)));

                    if (outArgs.Length == 1)
                    {
                        switchSectionBlock = switchSectionBlock.AddStatements(
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", $"Write{ParseReadWriteMethod(outArgs[0])}"))
                                    .AddArgumentListArguments(
                                        Argument(
                                            IdentifierName("ret")))));
                    }
                    else
                    {
                        for (int i = 0; i < outArgs.Length; i++)
                        {
                            switchSectionBlock = switchSectionBlock.AddStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", $"Write{ParseReadWriteMethod(outArgs[i])}"))
                                        .AddArgumentListArguments(
                                            Argument(
                                                MakeMemberAccessExpression("ret", outArgs[i].Name ?? $"Item{i + 1}")))));
                        }
                    }

                    switchSectionBlock = switchSectionBlock.AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("context", "Reply"))
                                .AddArgumentListArguments(
                                    Argument(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", "CreateMessage"))))));
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

        private static void AddProperties(ref ClassDeclarationSyntax cl, ref SwitchStatementSyntax sw, DBusInterface dBusInterface)
        {
            if (dBusInterface.Properties is null) return;

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
                                        LocalDeclarationStatement(
                                            VariableDeclaration(ParseTypeName("Reader"))
                                                .AddVariables(
                                                    VariableDeclarator("reader")
                                                        .WithInitializer(
                                                            EqualsValueClause(
                                                                InvocationExpression(
                                                                    MakeMemberAccessExpression("context", "Request", "GetBodyReader")))))),
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
                                                                CaseSwitchLabel(MakeLiteralExpression(dBusProperty.Name!)))
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
                                                                                                        Argument(MakeLiteralExpression(dBusProperty.Type!)))))))
                                                                        .WithUsingKeyword(Token(SyntaxKind.UsingKeyword)),
                                                                    ExpressionStatement(
                                                                        InvocationExpression(
                                                                                MakeMemberAccessExpression("writer", $"Write{ParseReadWriteMethod(dBusProperty)}"))
                                                                            .AddArgumentListArguments(
                                                                                Argument(MakeMemberAccessExpression("BackingProperties", dBusProperty.Name!)))),
                                                                    ExpressionStatement(
                                                                        InvocationExpression(
                                                                                MakeMemberAccessExpression("context", "Reply"))
                                                                            .AddArgumentListArguments(
                                                                                Argument(
                                                                                    InvocationExpression(MakeMemberAccessExpression("writer", "CreateMessage"))))),
                                                                    BreakStatement()))))),
                                        BreakStatement()),
                                SwitchSection()
                                    .AddLabels(
                                        CaseSwitchLabel(
                                            TupleExpression()
                                                .AddArguments(
                                                    Argument(MakeLiteralExpression("GetAll")),
                                                    Argument(MakeLiteralExpression("s")))))
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
                                                                            Argument(MakeLiteralExpression("a{sv}"))))))),
                                            LocalDeclarationStatement(
                                                VariableDeclaration(ParseTypeName("Dictionary<string, object>"))
                                                    .AddVariables(
                                                        VariableDeclarator("dict")
                                                            .WithInitializer(
                                                                EqualsValueClause(
                                                                    ObjectCreationExpression(ParseTypeName("Dictionary<string, object>"))
                                                                        .WithInitializer(
                                                                            InitializerExpression(SyntaxKind.CollectionInitializerExpression)
                                                                                .WithExpressions(
                                                                                    SeparatedList<ExpressionSyntax>(
                                                                                        dBusInterface.Properties.Select(static dBusProperty =>
                                                                                            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression)
                                                                                                .AddExpressions(
                                                                                                    MakeLiteralExpression(dBusProperty.Name!),
                                                                                                    MakeMemberAccessExpression("BackingProperties", dBusProperty.Name!)))))))))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", "WriteDictionary"))
                                                    .AddArgumentListArguments(
                                                        Argument(IdentifierName("dict")))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("context", "Reply"))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            InvocationExpression(MakeMemberAccessExpression("writer", "CreateMessage"))))),
                                            BreakStatement()))),
                        BreakStatement()));

            cl = AddPropertiesClass(cl, dBusInterface);
        }
    }
}
