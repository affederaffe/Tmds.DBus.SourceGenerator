using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator;

public partial class DBusSourceGenerator
{
    public enum DotnetType
    {
        Byte,
        Bool,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Double,
        String,
        ObjectPath,
        Signature,
        Array,
        Tuple,
        Variant,
        Dictionary,
        SafeFileHandle
    }

    private static string Pascalize(ReadOnlySpan<char> name, bool camel = false)
    {
        bool upperizeNext = !camel;
        StringBuilder sb = new(name.Length);
        foreach (char och in name)
        {
            char ch = och;
            if (ch is '_' or '.')
                upperizeNext = true;
            else
            {
                if (upperizeNext)
                {
                    ch = char.ToUpperInvariant(ch);
                    upperizeNext = false;
                }

                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string Camelize(ReadOnlySpan<char> name)
    {
        StringBuilder sb = new(Pascalize(name));
        sb[0] = char.ToLowerInvariant(sb[0]);
        return sb.ToString();
    }

    [return: NotNullIfNotNull(nameof(dbusValues))]
    private static string? ParseSignature(IReadOnlyList<DBusValue>? dbusValues)
    {
        if (dbusValues is null || dbusValues.Count == 0)
            return null;

        StringBuilder sb = new();
        foreach (DBusValue? dBusValue in dbusValues.Where(static argument => argument.Type is not null))
            sb.Append(dBusValue.Type);

        return sb.ToString();
    }

    [return: NotNullIfNotNull(nameof(dbusValues))]
    private static TypeSyntax? ParseReturnType(IReadOnlyList<DBusValue>? dbusValues)
    {
        return dbusValues?.Count switch
        {
            0 or null => null,
            1 => dbusValues[0].DBusDotnetType.ToTypeSyntax(),
            _ => TupleType()
                .AddElements(
                    dbusValues.Select((dbusValue, i) => TupleElement(
                                dbusValue.DBusDotnetType.ToTypeSyntax())
                            .WithIdentifier(
                                Identifier(dbusValue.Name is not null
                                    ? SanitizeIdentifier(
                                        Pascalize(dbusValue.Name.AsSpan()))
                                    : $"Item{i + 1}")))
                        .ToArray())
        };
    }

    private static TypeSyntax ParseTaskReturnType(IReadOnlyList<DBusValue>? dbusValues)
    {
        return dbusValues?.Count switch
        {
            0 or null => IdentifierName("Task"),
            _ => GenericName("Task")
                .AddTypeArgumentListArguments(
                    ParseReturnType(dbusValues))
        };
    }

    private static TypeSyntax ParseValueTaskReturnType(IReadOnlyList<DBusValue>? dbusValues)
    {
        return dbusValues?.Count switch
        {
            0 or null => IdentifierName("ValueTask"),
            _ => GenericName("ValueTask")
                .AddTypeArgumentListArguments(
                    ParseReturnType(dbusValues))
        };
    }

    private static TypeSyntax ParseTaskCompletionSourceType(IReadOnlyList<DBusValue>? dbusValues)
    {
        return dbusValues?.Count switch
        {
            0 or null => GenericName("TaskCompletionSource")
                .AddTypeArgumentListArguments(
                    PredefinedType(
                        Token(SyntaxKind.BoolKeyword))),
            _ => GenericName("TaskCompletionSource")
                .AddTypeArgumentListArguments(
                    ParseReturnType(dbusValues))
        };
    }

    private static ParameterSyntax[] ParseParameterList(IReadOnlyList<DBusValue> inArgs)
    {
        return inArgs.Select((dbusValue, i) =>
                Parameter(
                        Identifier(dbusValue.Name is not null
                            ? SanitizeIdentifier(
                                Camelize(dbusValue.Name.AsSpan()))
                            : $"arg{i}"))
                    .WithType(
                        dbusValue.DBusDotnetType.ToTypeSyntax()))
            .ToArray();
    }

    private static string SanitizeSignature(in string signature) =>
        signature.Replace('{', 'e')
            .Replace("}", null)
            .Replace('(', 'r')
            .Replace(')', 'z');

    private static string SanitizeIdentifier(in string identifier)
    {
        bool isAnyKeyword = SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;
        return isAnyKeyword ? $"@{identifier}" : identifier;
    }

    private static string GetPropertiesClassIdentifier(DBusInterface dBusInterface) => $"{Pascalize(dBusInterface.Name.AsSpan())}Properties";

    public class DBusDotnetType
    {
        private static readonly DBusDotnetType ByteType = new(DotnetType.Byte, DBusType.Byte, "y", []);
        private static readonly DBusDotnetType BoolType = new(DotnetType.Bool, DBusType.Bool, "b", []);
        private static readonly DBusDotnetType Int16Type = new(DotnetType.Int16, DBusType.Int16, "n", []);
        private static readonly DBusDotnetType UInt16Type = new(DotnetType.UInt16, DBusType.UInt16, "q", []);
        private static readonly DBusDotnetType Int32Type = new(DotnetType.Int32, DBusType.Int32, "i", []);
        private static readonly DBusDotnetType UInt32Type = new(DotnetType.UInt32, DBusType.UInt32, "u", []);
        private static readonly DBusDotnetType Int64Type = new(DotnetType.Int64, DBusType.Int64, "x", []);
        private static readonly DBusDotnetType UInt64Type = new(DotnetType.UInt64, DBusType.UInt64, "t", []);
        private static readonly DBusDotnetType DoubleType = new(DotnetType.Double, DBusType.Double, "d", []);
        private static readonly DBusDotnetType StringType = new(DotnetType.String, DBusType.String, "s", []);
        private static readonly DBusDotnetType ObjectPathType = new(DotnetType.ObjectPath, DBusType.ObjectPath, "o", []);
        private static readonly DBusDotnetType SignatureType = new(DotnetType.Signature, DBusType.Signature, "g", []);
        private static readonly DBusDotnetType UnixFdType = new(DotnetType.SafeFileHandle, DBusType.UnixFd, "h", []);

        internal static readonly DBusDotnetType StringArrayType = new(DotnetType.Array, DBusType.Array, "as", [StringType]);

        private DBusDotnetType(DotnetType dotnetType, DBusType dBusType, string dBusTypeSignature, DBusDotnetType[] innerTypes)
        {
            DotnetType = dotnetType;
            InnerTypes = innerTypes;
            DBusTypeSignature = dBusTypeSignature;
            DBusType = dBusType;
        }

        public DotnetType DotnetType { get; }

        public DBusType DBusType { get; }

        public string DBusTypeSignature { get; }

        public DBusDotnetType[] InnerTypes { get; }

        public TypeSyntax ToTypeSyntax(bool nullable = false) => ToTypeSyntax(DotnetType, InnerTypes, nullable);

        public static DBusDotnetType FromDBusValue(DBusValue dbusValue)
        {
            ReadOnlySpan<byte> signature = Encoding.ASCII.GetBytes(dbusValue.Type!).AsSpan();
            return SignatureReader.Transform<DBusDotnetType>(signature, Map);
        }

        private static DBusDotnetType Map(DBusType dbusType, string? signature, DBusDotnetType[] innerTypes)
        {
            switch (dbusType)
            {
                case DBusType.Byte:
                    return ByteType;
                case DBusType.Bool:
                    return BoolType;
                case DBusType.Int16:
                    return Int16Type;
                case DBusType.UInt16:
                    return UInt16Type;
                case DBusType.Int32:
                    return Int32Type;
                case DBusType.UInt32:
                    return UInt32Type;
                case DBusType.Int64:
                    return Int64Type;
                case DBusType.UInt64:
                    return UInt64Type;
                case DBusType.Double:
                    return DoubleType;
                case DBusType.String:
                    return StringType;
                case DBusType.ObjectPath:
                    return ObjectPathType;
                case DBusType.Signature:
                    return SignatureType;
                case DBusType.Array when innerTypes.Length == 1:
                    return new DBusDotnetType(DotnetType.Array, DBusType.Array, signature!, innerTypes);
                case DBusType.Struct when innerTypes.Length > 0:
                    return new DBusDotnetType(DotnetType.Tuple, DBusType.Struct, signature!, innerTypes);
                case DBusType.Variant:
                    return new DBusDotnetType(DotnetType.Variant, DBusType.Variant, signature!, innerTypes);
                case DBusType.DictEntry when innerTypes.Length == 2:
                    return new DBusDotnetType(DotnetType.Dictionary, DBusType.Array, signature!, innerTypes);
                case DBusType.UnixFd:
                    return UnixFdType;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dbusType), dbusType, null);
            }
        }

        private static TypeSyntax ToTypeSyntax(DotnetType type, DBusDotnetType[]? innerTypes, bool nullable = false)
        {
            switch (type)
            {
                case DotnetType.Byte:
                    return PredefinedType(
                        Token(SyntaxKind.ByteKeyword));
                case DotnetType.Bool:
                    return PredefinedType(
                        Token(SyntaxKind.BoolKeyword));
                case DotnetType.Int16:
                    return PredefinedType(
                        Token(SyntaxKind.ShortKeyword));
                case DotnetType.UInt16:
                    return PredefinedType(
                        Token(SyntaxKind.UShortKeyword));
                case DotnetType.Int32:
                    return PredefinedType(
                        Token(SyntaxKind.IntKeyword));
                case DotnetType.UInt32:
                    return PredefinedType(
                        Token(SyntaxKind.UIntKeyword));
                case DotnetType.Int64:
                    return PredefinedType(
                        Token(SyntaxKind.LongKeyword));
                case DotnetType.UInt64:
                    return PredefinedType(
                        Token(SyntaxKind.ULongKeyword));
                case DotnetType.Double:
                    return PredefinedType(
                        Token(SyntaxKind.DoubleKeyword));
                case DotnetType.String:
                    TypeSyntax str = PredefinedType(
                        Token(SyntaxKind.StringKeyword));
                    if (nullable)
                        str = NullableType(str);
                    return str;
                case DotnetType.ObjectPath:
                    return IdentifierName("ObjectPath");
                case DotnetType.Signature:
                    return IdentifierName("Signature");
                case DotnetType.Variant:
                    return IdentifierName("VariantValue");
                case DotnetType.SafeFileHandle:
                    return IdentifierName("SafeFileHandle");
                case DotnetType.Array:
                    TypeSyntax arr = ArrayType(
                            innerTypes![0].ToTypeSyntax(nullable))
                        .AddRankSpecifiers(
                            ArrayRankSpecifier()
                                .AddSizes(
                                    OmittedArraySizeExpression()));
                    if (nullable)
                        arr = NullableType(arr);
                    return arr;
                case DotnetType.Dictionary:
                    TypeSyntax dict = GenericName("Dictionary")
                        .AddTypeArgumentListArguments(
                            innerTypes![0].ToTypeSyntax(),
                            innerTypes[1].ToTypeSyntax(nullable));
                    if (nullable)
                        dict = NullableType(dict);
                    return dict;
                case DotnetType.Tuple when innerTypes!.Length == 1:
                    return GenericName("ValueTuple")
                        .AddTypeArgumentListArguments(
                            innerTypes[0].ToTypeSyntax(nullable));
                case DotnetType.Tuple:
                    return TupleType()
                        .AddElements(
                            innerTypes.Select(innerType => TupleElement(
                                    innerType.ToTypeSyntax(nullable)))
                                .ToArray());
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, $"Cannot parse DotnetType with value {type}");
            }
        }
    }
}
