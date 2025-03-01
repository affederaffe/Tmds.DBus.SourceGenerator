using System;
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
        private static CompilationUnitSyntax MakeCompilationUnit(NamespaceDeclarationSyntax namespaceDeclaration) =>
            CompilationUnit()
                .AddUsings(
                    UsingDirective(
                        IdentifierName("System")),
                    UsingDirective(
                        IdentifierName("System.Collections.Generic")),
                    UsingDirective(
                        IdentifierName("System.Linq")),
                    UsingDirective(
                        IdentifierName("System.Runtime.InteropServices")),
                    UsingDirective(
                        IdentifierName("System.Threading")),
                    UsingDirective(
                        IdentifierName("System.Threading.Tasks")),
                    UsingDirective(
                        IdentifierName("Microsoft.Win32.SafeHandles")),
                    UsingDirective(
                        IdentifierName("Tmds.DBus.Protocol")))
                .WithLeadingTrivia(
                    Comment("// <auto-generated>"))
                .AddMembers(namespaceDeclaration
                    .WithLeadingTrivia(
                        TriviaList(
                            Trivia(
                                PragmaWarningDirectiveTrivia(
                                    Token(SyntaxKind.DisableKeyword), true)),
                            Trivia(
                                NullableDirectiveTrivia(
                                    Token(SyntaxKind.EnableKeyword), true)))))
                .NormalizeWhitespace();

        private static FieldDeclarationSyntax MakePrivateStringConst(string identifier, string value, TypeSyntax type) =>
            FieldDeclaration(
                    VariableDeclaration(type)
                    .AddVariables(
                        VariableDeclarator(identifier)
                        .WithInitializer(
                            EqualsValueClause(
                                MakeLiteralExpression(value)))))
                .AddModifiers(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.ConstKeyword));

        private static FieldDeclarationSyntax MakePrivateReadOnlyField(string identifier, TypeSyntax type) =>
            FieldDeclaration(
                    VariableDeclaration(type)
                    .AddVariables(
                        VariableDeclarator(identifier)))
                .AddModifiers(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.ReadOnlyKeyword));

        private static PropertyDeclarationSyntax MakeGetOnlyProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
            PropertyDeclaration(type, identifier)
                .AddModifiers(modifiers)
                .AddAccessorListAccessors(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken)));

        private static PropertyDeclarationSyntax MakeGetSetProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
            PropertyDeclaration(type, identifier)
                .AddModifiers(modifiers)
                .AddAccessorListAccessors(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken)),
                    AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken)));

        private static ExpressionStatementSyntax MakeAssignmentExpressionStatement(string left, string right) =>
            ExpressionStatement(
                MakeAssignmentExpression(
                    IdentifierName(left),
                    IdentifierName(right)));

        private static AssignmentExpressionSyntax MakeAssignmentExpression(ExpressionSyntax left, ExpressionSyntax right) =>
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);

        private static MemberAccessExpressionSyntax MakeMemberAccessExpression(string left, string right) =>
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(left), IdentifierName(right));

        private static MemberAccessExpressionSyntax MakeMemberAccessExpression(string left, string middle, string right) =>
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, MakeMemberAccessExpression(left, middle), IdentifierName(right));

        private static LiteralExpressionSyntax MakeLiteralExpression(string literal) => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal));

        private static SyntaxToken Utf8Literal(string value) =>
            Token(
                TriviaList(ElasticMarker),
                SyntaxKind.Utf8StringLiteralToken,
                SymbolDisplay.FormatLiteral(value, true) + "u8",
                value,
                TriviaList(ElasticMarker));

        private string GetOrAddWriteMethod(DBusDotnetType dBusDotnetType) =>
            dBusDotnetType.DotnetType switch
            {
                DotnetType.Byte => "WriteByte",
                DotnetType.Bool => "WriteBool",
                DotnetType.Int16 => "WriteInt16",
                DotnetType.UInt16 => "WriteUInt16",
                DotnetType.Int32 => "WriteInt32",
                DotnetType.UInt32 => "WriteUInt32",
                DotnetType.Int64 => "WriteInt64",
                DotnetType.UInt64 => "WriteUInt64",
                DotnetType.Double => "WriteDouble",
                DotnetType.String => "WriteNullableString",
                DotnetType.ObjectPath => "WriteObjectPathSafe",
                DotnetType.Signature => "WriteSignature",
                DotnetType.SafeFileHandle => "WriteHandle",
                DotnetType.Variant => "WriteVariant",
                DotnetType.Array => GetOrAddWriteArrayMethod(dBusDotnetType),
                DotnetType.Dictionary => GetOrAddWriteDictionaryMethod(dBusDotnetType),
                DotnetType.Tuple => GetOrAddWriteStructMethod(dBusDotnetType),
                _ => throw new ArgumentOutOfRangeException(nameof(dBusDotnetType.DotnetType), dBusDotnetType.DotnetType, null)
            };

        private string GetOrAddReadMethod(DBusDotnetType dBusDotnetType) =>
            dBusDotnetType.DotnetType switch
            {
                DotnetType.Byte => "ReadByte",
                DotnetType.Bool => "ReadBool",
                DotnetType.Int16 => "ReadInt16",
                DotnetType.UInt16 => "ReadUInt16",
                DotnetType.Int32 => "ReadInt32",
                DotnetType.UInt32 => "ReadUInt32",
                DotnetType.Int64 => "ReadInt64",
                DotnetType.UInt64 => "ReadUInt64",
                DotnetType.Double => "ReadDouble",
                DotnetType.String => "ReadString",
                DotnetType.ObjectPath => "ReadObjectPath",
                DotnetType.Signature => "ReadSignature",
                DotnetType.SafeFileHandle => "ReadHandle<SafeFileHandle>",
                DotnetType.Variant => "ReadVariantValue",
                DotnetType.Array => GetOrAddReadArrayMethod(dBusDotnetType),
                DotnetType.Dictionary => GetOrAddReadDictionaryMethod(dBusDotnetType),
                DotnetType.Tuple => GetOrAddReadStructMethod(dBusDotnetType),
                _ => throw new ArgumentOutOfRangeException(nameof(dBusDotnetType.DotnetType), dBusDotnetType.DotnetType, null)
            };

        private string GetOrAddReadArrayMethod(DBusDotnetType dBusDotnetType)
        {
            string identifier = $"ReadArray_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(dBusDotnetType.ToTypeSyntax(), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("reader"))
                            .WithType(
                                IdentifierName("Reader"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                            GenericName("List")
                                            .AddTypeArgumentListArguments(
                                                dBusDotnetType.InnerTypes[0].ToTypeSyntax()))
                                        .AddVariables(
                                            VariableDeclarator("items")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        ImplicitObjectCreationExpression())))),
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                            IdentifierName("ArrayEnd"))
                                        .AddVariables(
                                            VariableDeclarator("headersEnd")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("reader", "ReadArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", dBusDotnetType.InnerTypes[0].DBusType.ToString()))))))),
                                WhileStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("reader", "HasNext"))
                                        .AddArgumentListArguments(
                                            Argument(IdentifierName("headersEnd"))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("items", "Add"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusDotnetType.InnerTypes[0]))))))),
                                ReturnStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("items", "ToArray")))
                            )));

            return identifier;
        }

        private string GetOrAddReadDictionaryMethod(DBusDotnetType dBusDotnetType)
        {
            string identifier = $"ReadDictionary_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            TypeSyntax type = dBusDotnetType.ToTypeSyntax();

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(type, identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("reader"))
                            .WithType(
                                IdentifierName("Reader"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(type)
                                        .AddVariables(
                                            VariableDeclarator("items")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        ImplicitObjectCreationExpression())))),
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                            IdentifierName("ArrayEnd"))
                                        .AddVariables(
                                            VariableDeclarator("headersEnd")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("reader", "ReadArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", "Struct"))))))),
                                WhileStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("reader", "HasNext"))
                                        .AddArgumentListArguments(
                                            Argument(
                                                IdentifierName("headersEnd"))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("items", "Add"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusDotnetType.InnerTypes[0])))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusDotnetType.InnerTypes[1]))))))),
                                ReturnStatement(
                                    IdentifierName("items")))));

            return identifier;
        }

        private string GetOrAddReadStructMethod(DBusDotnetType dBusDotnetType)
        {
            string identifier = $"ReadStruct_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            TypeSyntax type = dBusDotnetType.ToTypeSyntax();

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(type, identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("reader"))
                            .WithType(
                                IdentifierName("Reader"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("reader", "AlignStruct"))),
                                ReturnStatement(
                                    dBusDotnetType.InnerTypes.Length == 1
                                        ? ObjectCreationExpression(type)
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusDotnetType.InnerTypes[0])))))
                                        : TupleExpression()
                                            .AddArguments(
                                                dBusDotnetType.InnerTypes.Select(innerDBusValue => Argument(
                                                        InvocationExpression(
                                                            MakeMemberAccessExpression("reader", GetOrAddReadMethod(innerDBusValue)))))
                                                    .ToArray())))));

            return identifier;
        }

        private string GetOrAddReadMessageMethod(DBusValue dBusValue, bool isVariant = false) => GetOrAddReadMessageMethod([dBusValue], isVariant);

        private string GetOrAddReadMessageMethod(IReadOnlyList<DBusValue> dBusValues, bool isVariant = false)
        {
            string identifier = $"ReadMessage_{(isVariant ? "v_" : null)}{SanitizeSignature(ParseSignature(dBusValues))}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            BlockSyntax block = Block()
                .AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(
                                IdentifierName("Reader"))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("message", "GetBodyReader")))))));

            if (isVariant)
            {
                block = block.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("reader", "ReadSignature"))
                            .AddArgumentListArguments(
                                Argument(
                                    MakeLiteralExpression(dBusValues[0].Type!)))));
            }

            if (dBusValues.Count == 1)
            {
                block = block.AddStatements(
                    ReturnStatement(
                        InvocationExpression(
                            MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValues[0].DBusDotnetType)))));
            }
            else
            {
                for (int i = 0; i < dBusValues.Count; i++)
                {
                    block = block.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    dBusValues[i].DBusDotnetType.ToTypeSyntax())
                                .AddVariables(
                                    VariableDeclarator($"arg{i}")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValues[i].DBusDotnetType))))))));
                }

                block = block.AddStatements(
                    ReturnStatement(
                        TupleExpression(
                            SeparatedList(
                                dBusValues.Select(static (_, i) =>
                                    Argument(
                                        IdentifierName($"arg{i}")))))));
            }

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(
                        ParseReturnType(dBusValues), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("message"))
                            .WithType(
                                IdentifierName("Message")),
                        Parameter(
                                Identifier("_"))
                            .WithType(
                                NullableType(
                                    PredefinedType(
                                        Token(SyntaxKind.ObjectKeyword)))))
                    .WithBody(block));

            return identifier;
        }

        private string GetOrAddWriteVariantMethod(DBusValue dBusValue)
        {
            string identifier = $"WriteVariant_{SanitizeSignature(dBusValue.Type!)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(
                        PredefinedType(Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("writer"))
                            .WithType(
                                IdentifierName("MessageWriter"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)),
                        Parameter(
                                Identifier("value"))
                            .WithType(
                                dBusValue.DBusDotnetType.ToTypeSyntax()))
                    .WithBody(
                        Block(
                                SingletonList(
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", "WriteSignature"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeLiteralExpression(dBusValue.Type!))))))
                            .AddStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusValue.DBusDotnetType)))
                                        .WithArgumentList(
                                            ArgumentList(
                                                SingletonSeparatedList(
                                                    Argument(
                                                        IdentifierName("value")))))))));

            return identifier;
        }

        private string GetOrAddWriteArrayMethod(DBusDotnetType dBusDotnetType)
        {
            string identifier = $"WriteArray_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("writer"))
                            .WithType(
                                IdentifierName("MessageWriter"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)),
                        Parameter(
                                Identifier("values"))
                            .WithType(dBusDotnetType.ToTypeSyntax(true)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("ArrayStart"))
                                        .AddVariables(
                                            VariableDeclarator("arrayStart")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("writer", "WriteArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", dBusDotnetType.InnerTypes[0].DBusType.ToString()))))))),
                                IfStatement(
                                    IsPatternExpression(
                                        IdentifierName("values"),
                                        UnaryPattern(
                                            ConstantPattern(
                                                LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                    ForEachStatement(
                                        dBusDotnetType.InnerTypes[0].ToTypeSyntax(true),
                                        "value",
                                        IdentifierName("values"),
                                        ExpressionStatement(
                                            InvocationExpression(
                                                    MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusDotnetType.InnerTypes[0])))
                                                .AddArgumentListArguments(
                                                    Argument(IdentifierName("value")))))),
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", "WriteArrayEnd"))
                                        .AddArgumentListArguments(
                                            Argument(IdentifierName("arrayStart")))))));

            return identifier;
        }

        private string GetOrAddWriteDictionaryMethod(DBusDotnetType dBusDotnetType)
        {
            string identifier = $"WriteDictionary_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("writer"))
                            .WithType(
                                IdentifierName("MessageWriter"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)),
                        Parameter(
                                Identifier("values"))
                            .WithType(dBusDotnetType.ToTypeSyntax(true)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("ArrayStart"))
                                        .AddVariables(
                                            VariableDeclarator("arrayStart")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("writer", "WriteArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", "Struct"))))))),
                                IfStatement(
                                    IsPatternExpression(
                                        IdentifierName("values"),
                                        UnaryPattern(
                                            ConstantPattern(
                                                LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                    ForEachStatement(
                                        GenericName("KeyValuePair")
                                            .AddTypeArgumentListArguments(
                                                dBusDotnetType.InnerTypes[0].ToTypeSyntax(true),
                                                dBusDotnetType.InnerTypes[1].ToTypeSyntax(true)),
                                        "value",
                                        IdentifierName("values"),
                                        Block(
                                            ExpressionStatement(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("writer", "WriteStructureStart"))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusDotnetType.InnerTypes[0])))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression("value", "Key")))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusDotnetType.InnerTypes[1])))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression("value", "Value"))))))),
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", "WriteArrayEnd"))
                                        .AddArgumentListArguments(
                                            Argument(
                                                IdentifierName("arrayStart")))))));

            return identifier;
        }

        private string GetOrAddWriteStructMethod(DBusDotnetType dBusDotnetType)
        {
            string identifier = $"WriteStruct_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("writer"))
                            .WithType(
                                IdentifierName("MessageWriter"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)),
                        Parameter(
                                Identifier("value"))
                            .WithType(dBusDotnetType.ToTypeSyntax(true)))
                    .WithBody(
                        Block(
                                ExpressionStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("writer", "WriteStructureStart"))))
                            .AddStatements(
                                dBusDotnetType.InnerTypes.Select(
                                        (x, i) => ExpressionStatement(
                                            InvocationExpression(
                                                    MakeMemberAccessExpression("writer", GetOrAddWriteMethod(x)))
                                                .AddArgumentListArguments(
                                                    Argument(
                                                        MakeMemberAccessExpression("value", $"Item{i + 1}")))))
                                    .Cast<StatementSyntax>()
                                    .ToArray())));

            return identifier;
        }
    }
}
