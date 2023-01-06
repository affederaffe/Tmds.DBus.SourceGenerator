using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static CompilationUnitSyntax MakeDBusInterfaceAttribute() => MakeCompilationUnit(
            NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(
                    ClassDeclaration("DBusInterfaceAttribute")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddBaseListTypes(SimpleBaseType(IdentifierName("Attribute")))
                        .AddAttributeLists(
                            AttributeList()
                                .AddAttributes(
                                    Attribute(IdentifierName("AttributeUsage"))
                                        .AddArgumentListArguments(
                                            AttributeArgument(
                                                MakeMemberAccessExpression("AttributeTargets", "Class")),
                                            AttributeArgument(
                                                LiteralExpression(SyntaxKind.TrueLiteralExpression))
                                                .WithNameEquals(
                                                    NameEquals("AllowMultiple")))))
                        .AddMembers(
                            ConstructorDeclaration("DBusInterfaceAttribute")
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("path")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithBody(
                                    Block(MakeAssignmentExpressionStatement("Path", "path"))),
                            MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "Path", Token(SyntaxKind.PublicKeyword)))));

        private static CompilationUnitSyntax MakeDBusHandlerAttribute() => MakeCompilationUnit(
            NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(
                    ClassDeclaration("DBusHandlerAttribute")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddBaseListTypes(SimpleBaseType(IdentifierName("Attribute")))
                        .AddAttributeLists(
                            AttributeList()
                                .AddAttributes(
                                    Attribute(IdentifierName("AttributeUsage"))
                                        .AddArgumentListArguments(
                                            AttributeArgument(
                                                MakeMemberAccessExpression("AttributeTargets", "Class")),
                                            AttributeArgument(
                                                    LiteralExpression(SyntaxKind.TrueLiteralExpression))
                                                .WithNameEquals(
                                                    NameEquals("AllowMultiple")))))
                        .AddMembers(
                            ConstructorDeclaration("DBusHandlerAttribute")
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("path")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithBody(
                                    Block(MakeAssignmentExpressionStatement("Path", "path"))),
                            MakeGetOnlyProperty(PredefinedType(Token(SyntaxKind.StringKeyword)), "Path", Token(SyntaxKind.PublicKeyword)))));

        private static CompilationUnitSyntax MakePropertyChangesClass() => MakeCompilationUnit(
            NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(
                    RecordDeclaration(Token(SyntaxKind.RecordKeyword), "PropertyChanges")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddTypeParameterListParameters(TypeParameter(Identifier("TProperties")))
                        .AddParameterListParameters(
                            Parameter(Identifier("Properties"))
                                .WithType(IdentifierName("TProperties")),
                            Parameter(Identifier("Invalidated"))
                                .WithType(ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword))).AddRankSpecifiers(ArrayRankSpecifier())),
                            Parameter(Identifier("Changed"))
                                .WithType(ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword))).AddRankSpecifiers(ArrayRankSpecifier())))
                        .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                        .AddMembers(
                            MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier("HasChanged"))
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("property")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithExpressionBody(
                                    ArrowExpressionClause(
                                        BinaryExpression(SyntaxKind.NotEqualsExpression,
                                            InvocationExpression(
                                                    MakeMemberAccessExpression("Array", "IndexOf"))
                                                .AddArgumentListArguments(
                                                    Argument(IdentifierName("Changed")),
                                                    Argument(IdentifierName("property"))),
                                            MakeLiteralExpression(-1))))
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                            MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier("IsInvalidated"))
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .AddParameterListParameters(
                                    Parameter(Identifier("property")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                                .WithExpressionBody(
                                    ArrowExpressionClause(
                                        BinaryExpression(SyntaxKind.NotEqualsExpression,
                                            InvocationExpression(
                                                    MakeMemberAccessExpression("Array", "IndexOf"))
                                                .AddArgumentListArguments(
                                                    Argument(IdentifierName("Invalidated")),
                                                    Argument(IdentifierName("property"))),
                                            MakeLiteralExpression(-1))))
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))
                        .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))));

        private static CompilationUnitSyntax MakeSignalHelperClass()
        {
            MethodDeclarationSyntax watchSignalMethod = MethodDeclaration(ParseTypeName("ValueTask<IDisposable>"), "WatchSignalAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("connection"))
                        .WithType(ParseTypeName("Connection")),
                    Parameter(Identifier("rule"))
                        .WithType(ParseTypeName("MatchRule")),
                    Parameter(Identifier("handler"))
                        .WithType(ParseTypeName("Action<Exception?>")),
                    Parameter(Identifier("emitOnCapturedContext"))
                        .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                        .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("connection", "AddMatchAsync"))
                                .AddArgumentListArguments(
                                    Argument(IdentifierName("rule")),
                                    Argument(
                                        ParenthesizedLambdaExpression()
                                            .AddModifiers(Token(SyntaxKind.StaticKeyword))
                                            .AddParameterListParameters(
                                                Parameter(Identifier("_")),
                                                Parameter(Identifier("_")))
                                            .WithExpressionBody(
                                                PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                    Argument(
                                        ParenthesizedLambdaExpression()
                                            .AddModifiers(Token(SyntaxKind.StaticKeyword))
                                            .AddParameterListParameters(
                                                Parameter(Identifier("e"))
                                                    .WithType(ParseTypeName("Exception")),
                                                Parameter(Identifier("_"))
                                                    .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                                                Parameter(Identifier("_"))
                                                    .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))),
                                                Parameter(Identifier("handlerState"))
                                                    .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                                            .WithExpressionBody(
                                                InvocationExpression(
                                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                            ParenthesizedExpression(
                                                                CastExpression(ParseTypeName("Action<Exception?>"),
                                                                    PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                                                                        IdentifierName("handlerState")))),
                                                            IdentifierName("Invoke")))
                                                    .AddArgumentListArguments(
                                                        Argument(IdentifierName("e"))))),
                                    Argument(LiteralExpression(SyntaxKind.NullLiteralExpression)),
                                    Argument(IdentifierName("handler")),
                                    Argument(IdentifierName("emitOnCapturedContext"))))));

            MethodDeclarationSyntax watchSignalWithReadMethod = MethodDeclaration(ParseTypeName("ValueTask<IDisposable>"), "WatchSignalAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .AddTypeParameterListParameters(
                    TypeParameter("T"))
                .AddParameterListParameters(
                    Parameter(Identifier("connection"))
                        .WithType(ParseTypeName("Connection")),
                    Parameter(Identifier("rule"))
                        .WithType(ParseTypeName("MatchRule")),
                    Parameter(Identifier("reader"))
                        .WithType(ParseTypeName("MessageValueReader<T>")),
                    Parameter(Identifier("handler"))
                        .WithType(ParseTypeName("Action<Exception?, T>")),
                    Parameter(Identifier("emitOnCapturedContext"))
                        .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                        .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("connection", "AddMatchAsync"))
                                .AddArgumentListArguments(
                                    Argument(IdentifierName("rule")),
                                    Argument(IdentifierName("reader")),
                                    Argument(
                                        ParenthesizedLambdaExpression()
                                            .AddModifiers(Token(SyntaxKind.StaticKeyword))
                                            .AddParameterListParameters(
                                                Parameter(Identifier("e")).WithType(ParseTypeName("Exception")),
                                                Parameter(Identifier("arg"))
                                                    .WithType(ParseTypeName("T")),
                                                Parameter(Identifier("readerState"))
                                                    .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))),
                                                Parameter(Identifier("handlerState"))
                                                    .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                                            .WithExpressionBody(
                                                InvocationExpression(
                                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                            ParenthesizedExpression(
                                                                CastExpression(ParseTypeName("Action<Exception?, T>"),
                                                                    PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                                                                        IdentifierName("handlerState")))),
                                                            IdentifierName("Invoke")))
                                                    .AddArgumentListArguments(
                                                        Argument(IdentifierName("e")),
                                                        Argument(IdentifierName("arg"))))),
                                    Argument(LiteralExpression(SyntaxKind.NullLiteralExpression)),
                                    Argument(IdentifierName("handler")),
                                    Argument(IdentifierName("emitOnCapturedContext"))))));

            MethodDeclarationSyntax watchPropertiesMethod = MethodDeclaration(ParseTypeName("ValueTask<IDisposable>"), "WatchPropertiesChangedAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .AddTypeParameterListParameters(
                    TypeParameter("T"))
                .AddParameterListParameters(
                    Parameter(Identifier("connection"))
                        .WithType(ParseTypeName("Connection")),
                    Parameter(Identifier("destination"))
                        .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                    Parameter(Identifier("path"))
                        .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                    Parameter(Identifier("@interface"))
                        .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                    Parameter(Identifier("reader"))
                        .WithType(ParseTypeName("MessageValueReader<PropertyChanges<T>>")),
                    Parameter(Identifier("handler"))
                        .WithType(ParseTypeName("Action<Exception?, PropertyChanges<T>>")),
                    Parameter(Identifier("emitOnCapturedContext"))
                        .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                        .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                .WithBody(
                    Block(
                        LocalDeclarationStatement(
                            VariableDeclaration(ParseTypeName("MatchRule"))
                                .AddVariables(
                                    VariableDeclarator("rule")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                ObjectCreationExpression(ParseTypeName("MatchRule"))
                                                    .WithInitializer(
                                                        InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                                                            .AddExpressions(
                                                                MakeAssignmentExpression(IdentifierName("Type"), MakeMemberAccessExpression("MessageType", "Signal")),
                                                                MakeAssignmentExpression(IdentifierName("Sender"), IdentifierName("destination")),
                                                                MakeAssignmentExpression(IdentifierName("Path"), IdentifierName("path")),
                                                                MakeAssignmentExpression(IdentifierName("Member"), MakeLiteralExpression("PropertiesChanged")),
                                                                MakeAssignmentExpression(IdentifierName("Interface"), MakeLiteralExpression("org.freedesktop.DBus.Properties")),
                                                                MakeAssignmentExpression(IdentifierName("Arg0"), IdentifierName("@interface")))))))),
                        ReturnStatement(
                            InvocationExpression(
                                IdentifierName("WatchSignalAsync"))
                                .AddArgumentListArguments(
                                    Argument(IdentifierName("connection")),
                                    Argument(IdentifierName("rule")),
                                    Argument(IdentifierName("reader")),
                                    Argument(IdentifierName("handler")),
                                    Argument(IdentifierName("emitOnCapturedContext"))))));

            return MakeCompilationUnit(
                NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator"))
                    .AddMembers(
                        ClassDeclaration("SignalHelper")
                            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                            .AddMembers(watchSignalMethod, watchSignalWithReadMethod, watchPropertiesMethod)));
        }
    }
}
