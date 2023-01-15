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
                                                PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                                                    LiteralExpression(SyntaxKind.NullLiteralExpression)))),
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
                                                                MakeAssignmentExpression(IdentifierName("Type"),
                                                                    MakeMemberAccessExpression("MessageType", "Signal")),
                                                                MakeAssignmentExpression(IdentifierName("Sender"), IdentifierName("destination")),
                                                                MakeAssignmentExpression(IdentifierName("Path"), IdentifierName("path")),
                                                                MakeAssignmentExpression(IdentifierName("Member"),
                                                                    MakeLiteralExpression("PropertiesChanged")),
                                                                MakeAssignmentExpression(IdentifierName("Interface"),
                                                                    MakeLiteralExpression("org.freedesktop.DBus.Properties")),
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

        private const string VariantExtensions = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Tmds.DBus.Protocol;

// <auto-generated/>
#pragma warning disable
#nullable enable
namespace Tmds.DBus.SourceGenerator
{
    public static class VariantReader
    {
        public static DBusItem? ReadDBusVariant(this ref Reader reader)
        {
            ReadOnlySpan<byte> signature = reader.ReadSignature();
            SignatureReader signatureReader = new(signature);
            return !signatureReader.TryRead(out DBusType dBusType, out ReadOnlySpan<byte> innerSignature) ? null : reader.ReadDBusItem(dBusType, innerSignature);
        }

        private static DBusBasicItem ReadDBusBasicItem(this ref Reader reader, DBusType dBusType) =>
            dBusType switch
            {
                DBusType.Byte => new DBusByteItem(reader.ReadByte()),
                DBusType.Bool => new DBusBoolItem(reader.ReadBool()),
                DBusType.Int16 => new DBusInt16Item(reader.ReadInt16()),
                DBusType.UInt16 => new DBusUInt16Item(reader.ReadUInt16()),
                DBusType.Int32 => new DBusInt32Item(reader.ReadInt32()),
                DBusType.UInt32 => new DBusUInt32Item(reader.ReadUInt32()),
                DBusType.Int64 => new DBusInt64Item(reader.ReadInt64()),
                DBusType.UInt64 => new DBusUInt64Item(reader.ReadUInt64()),
                DBusType.Double => new DBusDoubleItem(reader.ReadDouble()),
                DBusType.String => new DBusStringItem(reader.ReadString()),
                DBusType.ObjectPath => new DBusObjectPathItem(reader.ReadObjectPath()),
                DBusType.Signature => new DBusSignatureItem(new Signature(reader.ReadSignature().ToString())),
                _ => throw new ArgumentOutOfRangeException(nameof(dBusType))
            };

        private static DBusItem ReadDBusItem(this ref Reader reader, DBusType dBusType, ReadOnlySpan<byte> innerSignature)
        {
            switch (dBusType)
            {
                case DBusType.Byte:
                    return new DBusByteItem(reader.ReadByte());
                case DBusType.Bool:
                    return new DBusBoolItem(reader.ReadBool());
                case DBusType.Int16:
                    return new DBusInt16Item(reader.ReadInt16());
                case DBusType.UInt16:
                    return new DBusUInt16Item(reader.ReadUInt16());
                case DBusType.Int32:
                    return new DBusInt32Item(reader.ReadInt32());
                case DBusType.UInt32:
                    return new DBusUInt32Item(reader.ReadUInt32());
                case DBusType.Int64:
                    return new DBusInt64Item(reader.ReadInt64());
                case DBusType.UInt64:
                    return new DBusUInt64Item(reader.ReadUInt64());
                case DBusType.Double:
                    return new DBusDoubleItem(reader.ReadDouble());
                case DBusType.String:
                    return new DBusStringItem(reader.ReadString());
                case DBusType.ObjectPath:
                    return new DBusObjectPathItem(reader.ReadObjectPath());
                case DBusType.Signature:
                    return new DBusSignatureItem(new Signature(reader.ReadSignature().ToString()));
                case DBusType.Array:
                {
                    SignatureReader innerSignatureReader = new(innerSignature);
                    if (!innerSignatureReader.TryRead(out DBusType innerDBusType, out ReadOnlySpan<byte> innerArraySignature))
                        return new DBusArrayItem(Enumerable.Empty<DBusItem>());
                    List<DBusItem> items = new();
                    ArrayEnd arrayEnd = reader.ReadArrayStart(innerDBusType);
                    while (reader.HasNext(arrayEnd))
                        items.Add(reader.ReadDBusItem(innerDBusType, innerArraySignature));
                    return new DBusArrayItem(items);
                }
                case DBusType.DictEntry:
                {
                    SignatureReader innerSignatureReader = new(innerSignature);
                    if (!innerSignatureReader.TryRead(out DBusType innerKeyType, out _) ||
                        !innerSignatureReader.TryRead(out DBusType innerValueType, out ReadOnlySpan<byte> innerValueSignature))
                        throw new InvalidOperationException($"Expected 2 inner types for DictEntry, got {Encoding.UTF8.GetString(innerSignature.ToArray())}");
                    DBusBasicItem key = reader.ReadDBusBasicItem(innerKeyType);
                    DBusItem value = reader.ReadDBusItem(innerValueType, innerValueSignature);
                    return new DBusDictEntryItem(key, value);
                }
                case DBusType.Struct:
                {
                    reader.AlignStruct();
                    List<DBusItem> items = new();
                    SignatureReader innerSignatureReader = new(innerSignature);
                    while (innerSignatureReader.TryRead(out DBusType innerDBusType, out ReadOnlySpan<byte> innerStructSignature))
                        items.Add(reader.ReadDBusItem(innerDBusType, innerStructSignature));
                    return new DBusStructItem(items);
                }
                case DBusType.Variant:
                    return reader.ReadDBusVariant();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public static class VariantWriter
    {
        public static void WriteDBusVariant(this MessageWriter writer, DBusItem value)
        {
            switch (value)
            {
                case DBusByteItem byteItem:
                    writer.WriteVariantByte(byteItem.Value);
                    break;
                case DBusBoolItem boolItem:
                    writer.WriteVariantBool(boolItem.Value);
                    break;
                case DBusInt16Item int16Item:
                    writer.WriteVariantInt16(int16Item.Value);
                    break;
                case DBusUInt16Item uInt16Item:
                    writer.WriteVariantUInt16(uInt16Item.Value);
                    break;
                case DBusInt32Item int32Item:
                    writer.WriteVariantInt32(int32Item.Value);
                    break;
                case DBusUInt32Item uInt32Item:
                    writer.WriteVariantUInt32(uInt32Item.Value);
                    break;
                case DBusInt64Item int64Item:
                    writer.WriteVariantInt64(int64Item.Value);
                    break;
                case DBusUInt64Item uInt64Item:
                    writer.WriteVariantUInt64(uInt64Item.Value);
                    break;
                case DBusDoubleItem doubleItem:
                    writer.WriteVariantDouble(doubleItem.Value);
                    break;
                case DBusStringItem stringItem:
                    writer.WriteVariantString(stringItem.Value);
                    break;
                case DBusObjectPathItem objectPathItem:
                    writer.WriteVariantObjectPath(objectPathItem.Value);
                    break;
                case DBusSignatureItem signatureItem:
                    writer.WriteVariantSignature(signatureItem.Value.ToString());
                    break;
                case DBusArrayItem arrayItem:
                    writer.WriteString(arrayItem.Signature);
                    ArrayStart arrayStart = writer.WriteArrayStart(DBusType.Array);
                    foreach (DBusItem item in arrayItem)
                        writer.WriteDBusItem(item);
                    writer.WriteArrayEnd(arrayStart);
                    break;
                case DBusDictEntryItem dictEntryItem:
                    writer.WriteString(dictEntryItem.Signature);
                    writer.WriteStructureStart();
                    writer.WriteDBusItem(dictEntryItem.Key);
                    writer.WriteDBusItem(dictEntryItem.Value);
                    break;
                case DBusStructItem structItem:
                    writer.WriteString(structItem.Signature);
                    ArrayStart structStart = writer.WriteArrayStart(DBusType.Struct);
                    foreach (DBusItem item in structItem)
                        writer.WriteDBusItem(item);
                    writer.WriteArrayEnd(structStart);
                    break;
            }
        }

        private static void WriteDBusItem(this MessageWriter writer, DBusItem value)
        {
            switch (value)
            {
                case DBusByteItem byteItem:
                    writer.WriteByte(byteItem.Value);
                    break;
                case DBusBoolItem boolItem:
                    writer.WriteBool(boolItem.Value);
                    break;
                case DBusInt16Item int16Item:
                    writer.WriteInt16(int16Item.Value);
                    break;
                case DBusUInt16Item uInt16Item:
                    writer.WriteUInt16(uInt16Item.Value);
                    break;
                case DBusInt32Item int32Item:
                    writer.WriteInt32(int32Item.Value);
                    break;
                case DBusUInt32Item uInt32Item:
                    writer.WriteUInt32(uInt32Item.Value);
                    break;
                case DBusInt64Item int64Item:
                    writer.WriteInt64(int64Item.Value);
                    break;
                case DBusUInt64Item uInt64Item:
                    writer.WriteUInt64(uInt64Item.Value);
                    break;
                case DBusDoubleItem doubleItem:
                    writer.WriteDouble(doubleItem.Value);
                    break;
                case DBusStringItem stringItem:
                    writer.WriteString(stringItem.Value);
                    break;
                case DBusObjectPathItem objectPathItem:
                    writer.WriteObjectPath(objectPathItem.Value);
                    break;
                case DBusSignatureItem signatureItem:
                    writer.WriteSignature(signatureItem.Value.ToString());
                    break;
                case DBusArrayItem arrayItem:
                    ArrayStart arrayStart = writer.WriteArrayStart(DBusType.Array);
                    foreach (DBusItem item in arrayItem)
                        writer.WriteDBusItem(item);
                    writer.WriteArrayEnd(arrayStart);
                    break;
                case DBusDictEntryItem dictEntryItem:
                    writer.WriteStructureStart();
                    writer.WriteDBusItem(dictEntryItem.Key);
                    writer.WriteDBusItem(dictEntryItem.Value);
                    break;
                case DBusStructItem structItem:
                    ArrayStart structStart = writer.WriteArrayStart(DBusType.Struct);
                    foreach (DBusItem item in structItem)
                        writer.WriteDBusItem(item);
                    writer.WriteArrayEnd(structStart);
                    break;
            }
        }
    }

    public abstract class DBusItem
    {
        protected DBusItem(string signature)
        {
            Signature = signature;
        }

        public string Signature { get; }
    }

    public abstract class DBusBasicItem : DBusItem
    {
        protected DBusBasicItem(string signature) : base(signature) { }
    }

    public class DBusByteItem : DBusBasicItem
    {
        public DBusByteItem(byte value) : base("y")
        {
            Value = value;
        }

        public byte Value { get; }
    }

    public class DBusBoolItem : DBusBasicItem
    {
        public DBusBoolItem(bool value) : base("b")
        {
            Value = value;
        }

        public bool Value { get; }
    }

    public class DBusInt16Item : DBusBasicItem
    {
        public DBusInt16Item(short value) : base("n")
        {
            Value = value;
        }

        public short Value { get; }
    }

    public class DBusUInt16Item : DBusBasicItem
    {
        public DBusUInt16Item(ushort value) : base("q")
        {
            Value = value;
        }

        public ushort Value { get; }
    }

    public class DBusInt32Item : DBusBasicItem
    {
        public DBusInt32Item(int value) : base("i")
        {
            Value = value;
        }

        public int Value { get; }
    }

    public class DBusUInt32Item : DBusBasicItem
    {
        public DBusUInt32Item(uint value) : base("u")
        {
            Value = value;
        }

        public uint Value { get; }
    }

    public class DBusInt64Item : DBusBasicItem
    {
        public DBusInt64Item(long value) : base("x")
        {
            Value = value;
        }

        public long Value { get; }
    }

    public class DBusUInt64Item : DBusBasicItem
    {
        public DBusUInt64Item(ulong value) : base("t")
        {
            Value = value;
        }

        public ulong Value { get; }
    }

    public class DBusDoubleItem : DBusBasicItem
    {
        public DBusDoubleItem(double value) : base("d")
        {
            Value = value;
        }

        public double Value { get; }
    }

    public class DBusStringItem : DBusBasicItem
    {
        public DBusStringItem(string value) : base("s")
        {
            Value = value;
        }

        public string Value { get; }
    }

    public class DBusObjectPathItem : DBusBasicItem
    {
        public DBusObjectPathItem(ObjectPath value) : base("o")
        {
            Value = value;
        }

        public ObjectPath Value { get; }
    }

    public class DBusSignatureItem : DBusBasicItem
    {
        public DBusSignatureItem(Signature value) : base("g")
        {
            Value = value;
        }

        public Signature Value { get; }
    }

    public class DBusArrayItem : DBusItem, IList<DBusItem>
    {
        private readonly IList<DBusItem> _value;

        public DBusArrayItem(IEnumerable<DBusItem> value) : base($"a{string.Concat(value.Select(static x => x.Signature))}")
        {
            _value = value.ToList();
        }

        public IEnumerator<DBusItem> GetEnumerator() => _value.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_value).GetEnumerator();

        public void Add(DBusItem item) => _value.Add(item);

        public void Clear() => _value.Clear();

        public bool Contains(DBusItem item) => _value.Contains(item);

        public void CopyTo(DBusItem[] array, int arrayIndex) => _value.CopyTo(array, arrayIndex);

        public bool Remove(DBusItem item) => _value.Remove(item);

        public int Count => _value.Count;

        public bool IsReadOnly => _value.IsReadOnly;

        public int IndexOf(DBusItem item) => _value.IndexOf(item);

        public void Insert(int index, DBusItem item) => _value.Insert(index, item);

        public void RemoveAt(int index) => _value.RemoveAt(index);

        public DBusItem this[int index]
        {
            get => _value[index];
            set => _value[index] = value;
        }
    }

    public class DBusDictEntryItem : DBusItem
    {
        public DBusDictEntryItem(DBusBasicItem key, DBusItem value) : base($"{{{key.Signature}{value.Signature}}}")
        {
            Key = key;
            Value = value;
        }

        public DBusBasicItem Key { get; }

        public DBusItem Value { get; }
    }

    public class DBusStructItem : DBusItem, IList<DBusItem>
    {
        private readonly IList<DBusItem> _value;

        public DBusStructItem(DBusItem value) : base($"({value.Signature})")
        {
            _value = new List<DBusItem>();
            _value.Add(value);
        }

        public DBusStructItem(IEnumerable<DBusItem> value) : base($"({string.Concat(value.Select(static x => x.Signature))})")
        {
            _value = value.ToList();
        }

        public IEnumerator<DBusItem> GetEnumerator() => _value.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_value).GetEnumerator();

        public void Add(DBusItem item) => _value.Add(item);

        public void Clear() => _value.Clear();

        public bool Contains(DBusItem item) => _value.Contains(item);

        public void CopyTo(DBusItem[] array, int arrayIndex) => _value.CopyTo(array, arrayIndex);

        public bool Remove(DBusItem item) => _value.Remove(item);

        public int Count => _value.Count;

        public bool IsReadOnly => _value.IsReadOnly;

        public int IndexOf(DBusItem item) => _value.IndexOf(item);

        public void Insert(int index, DBusItem item) => _value.Insert(index, item);

        public void RemoveAt(int index) => _value.RemoveAt(index);

        public DBusItem this[int index]
        {
            get => _value[index];
            set => _value[index] = value;
        }
    }
}
""";
    }
}
