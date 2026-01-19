using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus.Protocol;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorUtils;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorParsing;


namespace Tmds.DBus.SourceGenerator;

public class ReadWriteMethodsCache
{
    private readonly ConcurrentDictionary<string, MethodDeclarationSyntax> _readMethodExtensions = new();
    private readonly ConcurrentDictionary<string, MethodDeclarationSyntax> _writeMethodExtensions = new();

    internal SyntaxList<MemberDeclarationSyntax> GetReadMethods() => List<MemberDeclarationSyntax>(_readMethodExtensions.Values);

    internal SyntaxList<MemberDeclarationSyntax> GetWriteMethods() => List<MemberDeclarationSyntax>(_writeMethodExtensions.Values);

    internal string GetOrAddReadMethod(DBusDotnetType dBusDotnetType) =>
        dBusDotnetType.DotnetType switch
        {
            DotnetType.Byte => nameof(Reader.ReadByte),
            DotnetType.Bool => nameof(Reader.ReadBool),
            DotnetType.Int16 => nameof(Reader.ReadInt16),
            DotnetType.UInt16 => nameof(Reader.ReadUInt16),
            DotnetType.Int32 => nameof(Reader.ReadInt32),
            DotnetType.UInt32 => nameof(Reader.ReadUInt32),
            DotnetType.Int64 => nameof(Reader.ReadInt64),
            DotnetType.UInt64 => nameof(Reader.ReadUInt64),
            DotnetType.Double => nameof(Reader.ReadDouble),
            DotnetType.String => nameof(Reader.ReadString),
            DotnetType.ObjectPath => nameof(Reader.ReadObjectPath),
            DotnetType.Signature => nameof(Reader.ReadSignature),
            DotnetType.SafeFileHandle => $"{nameof(Reader.ReadHandle)}<{nameof(SafeFileHandle)}>",
            DotnetType.Variant => nameof(Reader.ReadVariantValue),
            DotnetType.Array => GetOrAddReadArrayMethod(dBusDotnetType),
            DotnetType.Dictionary => GetOrAddReadDictionaryMethod(dBusDotnetType),
            DotnetType.Tuple => GetOrAddReadStructMethod(dBusDotnetType),
            _ => throw new ArgumentOutOfRangeException(nameof(dBusDotnetType.DotnetType), dBusDotnetType.DotnetType, null)
        };

    internal string GetOrAddWriteMethod(DBusDotnetType dBusDotnetType) =>
        dBusDotnetType.DotnetType switch
        {
            DotnetType.Byte => nameof(MessageWriter.WriteByte),
            DotnetType.Bool => nameof(MessageWriter.WriteBool),
            DotnetType.Int16 => nameof(MessageWriter.WriteInt16),
            DotnetType.UInt16 => nameof(MessageWriter.WriteUInt16),
            DotnetType.Int32 => nameof(MessageWriter.WriteInt32),
            DotnetType.UInt32 => nameof(MessageWriter.WriteUInt32),
            DotnetType.Int64 => nameof(MessageWriter.WriteInt64),
            DotnetType.UInt64 => nameof(MessageWriter.WriteUInt64),
            DotnetType.Double => nameof(MessageWriter.WriteDouble),
            DotnetType.String => "WriteNullableString",
            DotnetType.ObjectPath => nameof(MessageWriter.WriteObjectPath),
            DotnetType.Signature => nameof(MessageWriter.WriteSignature),
            DotnetType.SafeFileHandle => nameof(MessageWriter.WriteHandle),
            DotnetType.Variant => nameof(MessageWriter.WriteVariant),
            DotnetType.Array => GetOrAddWriteArrayMethod(dBusDotnetType),
            DotnetType.Dictionary => GetOrAddWriteDictionaryMethod(dBusDotnetType),
            DotnetType.Tuple => GetOrAddWriteStructMethod(dBusDotnetType),
            _ => throw new ArgumentOutOfRangeException(nameof(dBusDotnetType.DotnetType), dBusDotnetType.DotnetType, null)
        };

    internal string GetOrAddReadMessageMethod(IReadOnlyList<DBusValue> dBusValues, bool isVariant = false)
    {
        string identifier = $"ReadMessage_{(isVariant ? "v_" : null)}{SanitizeSignature(ParseSignature(dBusValues))}";
        _readMethodExtensions.GetOrAdd(identifier, id => MakeReadMessageMethod(dBusValues, id, isVariant));
        return identifier;
    }

    internal string GetOrAddWriteVariantMethod(DBusValue dBusValue)
    {
        string identifier = $"WriteVariant_{SanitizeSignature(dBusValue.Type!)}";
        _writeMethodExtensions.GetOrAdd(identifier, id => MakeWriteVariantMethod(dBusValue, id));
        return identifier;
    }

    private string GetOrAddReadArrayMethod(DBusDotnetType dBusDotnetType)
    {
        string identifier = $"ReadArray_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
        _readMethodExtensions.GetOrAdd(identifier, id => MakeReadArrayMethod(dBusDotnetType, id));
        return identifier;
    }

    private string GetOrAddReadDictionaryMethod(DBusDotnetType dBusDotnetType)
    {
        string identifier = $"ReadDictionary_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
        _readMethodExtensions.GetOrAdd(identifier, id => MakeReadDictionaryMethod(dBusDotnetType, id));
        return identifier;
    }

    private string GetOrAddReadStructMethod(DBusDotnetType dBusDotnetType)
    {
        string identifier = $"ReadStruct_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
        _readMethodExtensions.GetOrAdd(identifier, id => MakeReadStructMethod(dBusDotnetType, id));
        return identifier;
    }

    private string GetOrAddWriteArrayMethod(DBusDotnetType dBusDotnetType)
    {
        string identifier = $"WriteArray_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
        _writeMethodExtensions.GetOrAdd(identifier, id => MakeWriteArrayMethod(dBusDotnetType, id));
        return identifier;
    }

    private string GetOrAddWriteDictionaryMethod(DBusDotnetType dBusDotnetType)
    {
        string identifier = $"WriteDictionary_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
        _writeMethodExtensions.GetOrAdd(identifier, id => MakeWriteDictionaryMethod(dBusDotnetType, id));
        return identifier;
    }

    private string GetOrAddWriteStructMethod(DBusDotnetType dBusDotnetType)
    {
        string identifier = $"WriteStruct_{SanitizeSignature(dBusDotnetType.DBusTypeSignature)}";
        _writeMethodExtensions.GetOrAdd(identifier, id => MakeWriteStructMethod(dBusDotnetType, id));
        return identifier;
    }

    private MethodDeclarationSyntax MakeReadArrayMethod(DBusDotnetType dBusDotnetType, string identifier) =>
        MethodDeclaration(dBusDotnetType.ToTypeSyntax(), identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("reader"))
                    .WithType(
                        IdentifierName(nameof(Reader)))
                    .AddModifiers(
                        Token(SyntaxKind.ThisKeyword),
                        Token(SyntaxKind.RefKeyword)))
            .WithBody(
                Block()
                    .AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(
                                    GenericName(nameof(System.Collections.Generic.List<>))
                                        .AddTypeArgumentListArguments(
                                            dBusDotnetType.InnerTypes[0].ToTypeSyntax()))
                                .AddVariables(
                                    VariableDeclarator("items")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                ImplicitObjectCreationExpression())))),
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
                                                            MakeMemberAccessExpression(nameof(DBusType),
                                                                dBusDotnetType.InnerTypes[0].DBusType.ToString()))))))),
                        WhileStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("reader", nameof(Reader.HasNext)))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("headersEnd"))),
                            ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("items", nameof(System.Collections.Generic.List<>.Add)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("reader",
                                                    GetOrAddReadMethod(dBusDotnetType.InnerTypes[0]))))))),
                        ReturnStatement(
                            InvocationExpression(
                                MakeMemberAccessExpression("items", nameof(System.Collections.Generic.List<>.ToArray))))
                    ));

    private MethodDeclarationSyntax MakeReadDictionaryMethod(DBusDotnetType dBusDotnetType, string identifier)
    {
        TypeSyntax type = dBusDotnetType.ToTypeSyntax();
        return MethodDeclaration(type, identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("reader"))
                            .WithType(
                                IdentifierName(nameof(Reader)))
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
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("items", nameof(System.Collections.Generic.List<>.Add)))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusDotnetType.InnerTypes[0])))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader",
                                                            GetOrAddReadMethod(dBusDotnetType.InnerTypes[1]))))))),
                                ReturnStatement(
                                    IdentifierName("items"))));
    }

    private MethodDeclarationSyntax MakeReadStructMethod(DBusDotnetType dBusDotnetType, string identifier)
    {
        TypeSyntax type = dBusDotnetType.ToTypeSyntax();
        return MethodDeclaration(type, identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("reader"))
                    .WithType(
                        IdentifierName(nameof(Reader)))
                    .AddModifiers(
                        Token(SyntaxKind.ThisKeyword),
                        Token(SyntaxKind.RefKeyword)))
            .WithBody(
                Block()
                    .AddStatements(
                        ExpressionStatement(
                            InvocationExpression(
                                MakeMemberAccessExpression("reader", nameof(Reader.AlignStruct)))),
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
                                            .ToArray()))));
    }

    private MethodDeclarationSyntax MakeReadMessageMethod(IReadOnlyList<DBusValue> dBusValues, string identifier, bool isVariant = false)
    {
        BlockSyntax block = Block()
                .AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(
                                IdentifierName(nameof(Reader)))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("message", nameof(Message.GetBodyReader))))))));

            if (isVariant)
            {
                block = block.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("reader", nameof(Reader.ReadSignature)))
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
                                                    MakeMemberAccessExpression("reader",
                                                        GetOrAddReadMethod(dBusValues[i].DBusDotnetType))))))));
                }

                block = block.AddStatements(
                    ReturnStatement(
                        TupleExpression(
                            SeparatedList(
                                dBusValues.Select(static (_, i) =>
                                    Argument(
                                        IdentifierName($"arg{i}")))))));
            }

            return MethodDeclaration(
                    ParseReturnType(dBusValues), identifier)
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
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
                                PredefinedType(
                                    Token(SyntaxKind.ObjectKeyword)))))
                .WithBody(block);
    }

    private MethodDeclarationSyntax MakeWriteVariantMethod(DBusValue dBusValue, string identifier) =>
        MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)), identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("writer"))
                    .WithType(
                        IdentifierName(nameof(MessageWriter)))
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
                                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteSignature)))
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
                                                IdentifierName("value"))))))));

    private MethodDeclarationSyntax MakeWriteArrayMethod(DBusDotnetType dBusDotnetType, string identifier) =>
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
                        IdentifierName(nameof(MessageWriter)))
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
                            VariableDeclaration(
                                    IdentifierName(nameof(ArrayStart)))
                                .AddVariables(
                                    VariableDeclarator("arrayStart")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteArrayStart)))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression(nameof(DBusType),
                                                                dBusDotnetType.InnerTypes[0].DBusType.ToString()))))))),
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
                                            Argument(
                                                IdentifierName("value")))))),
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteArrayEnd)))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("arrayStart"))))));

    private MethodDeclarationSyntax MakeWriteDictionaryMethod(DBusDotnetType dBusDotnetType, string identifier) =>
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
                        IdentifierName(nameof(MessageWriter)))
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
                            VariableDeclaration(
                                    IdentifierName(nameof(ArrayStart)))
                                .AddVariables(
                                    VariableDeclarator("arrayStart")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteArrayStart)))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression(nameof(DBusType), nameof(DBusType.Struct)))))))),
                        IfStatement(
                            IsPatternExpression(
                                IdentifierName("values"),
                                UnaryPattern(
                                    ConstantPattern(
                                        LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                            ForEachStatement(
                                GenericName(nameof(KeyValuePair<,>))
                                    .AddTypeArgumentListArguments(
                                        dBusDotnetType.InnerTypes[0].ToTypeSyntax(true),
                                        dBusDotnetType.InnerTypes[1].ToTypeSyntax(true)),
                                "value",
                                IdentifierName("values"),
                                Block(
                                    ExpressionStatement(
                                        InvocationExpression(
                                            MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteStructureStart)))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusDotnetType.InnerTypes[0])))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeMemberAccessExpression("value", nameof(KeyValuePair<,>.Key))))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusDotnetType.InnerTypes[1])))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeMemberAccessExpression("value", nameof(KeyValuePair<,>.Value)))))))),
                        ExpressionStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteArrayEnd)))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("arrayStart"))))));

    private MethodDeclarationSyntax MakeWriteStructMethod(DBusDotnetType dBusDotnetType, string identifier) =>
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
                        IdentifierName(nameof(MessageWriter)))
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
                                MakeMemberAccessExpression("writer", nameof(MessageWriter.WriteStructureStart)))))
                    .AddStatements(
                        dBusDotnetType.InnerTypes.Select((x, i) => ExpressionStatement(
                                InvocationExpression(
                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(x)))
                                    .AddArgumentListArguments(
                                        Argument(
                                            MakeMemberAccessExpression("value", $"Item{i + 1}")))))
                            .Cast<StatementSyntax>()
                            .ToArray()));
}
