using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static string? ParseSignature(IReadOnlyList<DBusValue>? dBusValues)
        {
            if (dBusValues is null || dBusValues.Count == 0) return null;
            StringBuilder sb = new();
            foreach (DBusValue dBusValue in dBusValues.Where(static argument => argument.Type is not null))
                sb.Append(dBusValue.Type);
            return sb.ToString();
        }

        private static string? ParseReturnType(IReadOnlyList<DBusValue>? dBusValues) => dBusValues?.Count switch
        {
            0 or null => null,
            1 => dBusValues[0].DotNetType,
            _ => TupleOf(dBusValues.Select(static (x, i) => $"{x.DotNetType} {x.Name ?? $"Item{i}"}"))
        };

        private static string ParseTaskReturnType(DBusValue dBusValue) => ParseTaskReturnType(new[] { dBusValue });

        private static string ParseTaskReturnType(IReadOnlyList<DBusValue>? dBusValues) => dBusValues?.Count switch
        {
            0 or null => "Task",
            1 => $"Task<{dBusValues[0].DotNetType}>",
            _ => $"Task<{TupleOf(dBusValues.Select(static (x, i) => $"{x.DotNetType} {x.Name ?? $"Item{i}"}"))}>"
        };

        private static ParameterListSyntax ParseParameterList(IEnumerable<DBusValue> inArgs) => ParameterList(
            SeparatedList(
                inArgs.Select(static (x, i) =>
                    Parameter(Identifier(x.Name ?? $"arg{i}")).WithType(ParseTypeName(x.DotNetType)))));


        internal static (string DotnetType, string[] DotnetInnerTypes, DBusType DBusType) ParseDotNetType(string signature) =>
            SignatureReader.Transform<(string, string[], DBusType)>(Encoding.ASCII.GetBytes(signature), MapDBusToDotNet);

        private static string ParseReadWriteMethod(DBusValue dBusValue) => dBusValue.DBusType switch
        {
            DBusType.Byte => "Byte",
            DBusType.Bool => "Bool",
            DBusType.Int16 => "Int16",
            DBusType.UInt16 => "UInt16",
            DBusType.Int32 => "Int32",
            DBusType.UInt32 => "UInt32",
            DBusType.Int64 => "Int64",
            DBusType.UInt64 => "UInt64",
            DBusType.Double => "Double",
            DBusType.String => "String",
            DBusType.ObjectPath => "ObjectPath",
            DBusType.Signature => "Signature",
            DBusType.Variant => "Variant",
            DBusType.UnixFd => "Handle",
            DBusType.Array => $"Array<{dBusValue.DotNetInnerTypes![0]}>",
            DBusType.DictEntry => $"Dictionary<{dBusValue.DotNetInnerTypes![0]}, {dBusValue.DotNetInnerTypes![1]}>",
            DBusType.Struct => $"Struct<{string.Join(", ", dBusValue.DotNetInnerTypes!)}>",
            _ => throw new ArgumentOutOfRangeException(nameof(dBusValue.DBusType), dBusValue.DBusType, null)
        };

        private static (string, string[], DBusType) MapDBusToDotNet(DBusType dBusType, (string, string[], DBusType)[] inner)
        {
            string[] innerTypes = inner.Select(static s => s.Item1).ToArray();
            return dBusType switch
            {
                DBusType.Byte => ("byte", innerTypes, dBusType),
                DBusType.Bool => ("bool", innerTypes, dBusType),
                DBusType.Int16 => ("short", innerTypes, dBusType),
                DBusType.UInt16 => ("ushort", innerTypes, dBusType),
                DBusType.Int32 => ("int", innerTypes, dBusType),
                DBusType.UInt32 => ("uint", innerTypes, dBusType),
                DBusType.Int64 => ("long", innerTypes, dBusType),
                DBusType.UInt64 => ("ulong", innerTypes, dBusType),
                DBusType.Double => ("double", innerTypes, dBusType),
                DBusType.String => ("string", innerTypes, dBusType),
                DBusType.ObjectPath => ("ObjectPath", innerTypes, dBusType),
                DBusType.Signature => ("Signature", innerTypes, dBusType),
                DBusType.Variant => ("object", innerTypes, dBusType),
                DBusType.UnixFd => ("SafeHandle", innerTypes, dBusType),
                DBusType.Array => ($"{innerTypes[0]}[]", innerTypes, dBusType),
                DBusType.DictEntry => ($"Dictionary<{innerTypes[0]}, {innerTypes[1]}>", innerTypes, dBusType),
                DBusType.Struct => ($"{TupleOf(innerTypes)}", innerTypes, dBusType),
                _ => throw new ArgumentOutOfRangeException(nameof(dBusType), dBusType, null)
            };
        }
    }
}
