using System;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Tmds.DBus.Protocol;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static string ParseWriteMethod(SemanticModel semanticModel, ParameterSyntax typeSyntax)
        {
            ITypeSymbol safeHandleType = semanticModel.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle")!;
            ITypeSymbol objectPathType = semanticModel.Compilation.GetTypeByMetadataName("Tmds.DBus.Protocol.ObjectPath")!;
            ITypeSymbol dictionaryType = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")!;

            if (typeSyntax.Type is null)
                throw new Exception($"Unable to parse type of {typeSyntax.Identifier} for write method.");
            TypeInfo typeInfo = semanticModel.GetTypeInfo(typeSyntax.Type);
            if (typeInfo.Type is null)
                throw new Exception($"Unable to parse type of {typeSyntax.Identifier} for write method.");

            INamedTypeSymbol type = (INamedTypeSymbol)typeInfo.Type;

            if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, dictionaryType))
                return $"{nameof(MessageWriter.WriteDictionary)}<{type.TypeArguments[0]}, {type.TypeArguments[1]}>";
            if (type.TypeKind == TypeKind.Array)
                return $"{nameof(MessageWriter.WriteArray)}<{type.BaseType}>";
            if (InheritsFrom(type, safeHandleType))
                return $"{nameof(MessageWriter.WriteHandle)}<{type}>";
            if (SymbolEqualityComparer.Default.Equals(type, objectPathType))
                return nameof(MessageWriter.WriteObjectPath);
            return type.SpecialType switch
            {
                SpecialType.System_Byte => nameof(MessageWriter.WriteByte),
                SpecialType.System_Boolean => nameof(MessageWriter.WriteBool),
                SpecialType.System_Int16 => nameof(MessageWriter.WriteInt16),
                SpecialType.System_UInt16 => nameof(MessageWriter.WriteUInt16),
                SpecialType.System_Int32 => nameof(MessageWriter.WriteInt32),
                SpecialType.System_UInt32 => nameof(MessageWriter.WriteUInt32),
                SpecialType.System_Int64 => nameof(MessageWriter.WriteInt64),
                SpecialType.System_UInt64 => nameof(MessageWriter.WriteUInt64),
                //SpecialType.System_Single => nameof(MessageWriter.WriteSingle),
                SpecialType.System_Double => nameof(MessageWriter.WriteDouble),
                SpecialType.System_String => nameof(MessageWriter.WriteString),
                SpecialType.System_Object => nameof(MessageWriter.WriteVariant),
                _ => throw new ArgumentException($"Cannot parse type {type.Name}.")
            };
        }

        private static string ParseReadMethod(SemanticModel semanticModel, TypeSyntax returnTypeSyntax)
        {
            ITypeSymbol safeHandleType = semanticModel.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle")!;
            ITypeSymbol objectPathType = semanticModel.Compilation.GetTypeByMetadataName("Tmds.DBus.Protocol.ObjectPath")!;
            ITypeSymbol dictionaryType = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")!;

            TypeInfo typeInfo = semanticModel.GetTypeInfo(returnTypeSyntax);
            if (typeInfo.Type is null)
                throw new Exception("Unable to parse type for read method.");

            INamedTypeSymbol returnType = (INamedTypeSymbol)((INamedTypeSymbol)typeInfo.Type).TypeArguments[0];

            if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, dictionaryType))
                return $"{nameof(Reader.ReadDictionary)}<{returnType.TypeArguments[0]}, {returnType.TypeArguments[1]}>";
            if (returnType.TypeKind == TypeKind.Array)
                return $"{nameof(Reader.ReadArray)}<{returnType.BaseType}>";
            if (InheritsFrom(returnType, safeHandleType))
                return $"{nameof(Reader.ReadHandle)}<{returnType}>";
            if (SymbolEqualityComparer.Default.Equals(returnType, objectPathType))
                return nameof(Reader.ReadObjectPath);
            return returnType.SpecialType switch
            {
                SpecialType.System_Byte => nameof(Reader.ReadByte),
                SpecialType.System_Boolean => nameof(Reader.ReadBool),
                SpecialType.System_Int16 => nameof(Reader.ReadInt16),
                SpecialType.System_UInt16 => nameof(Reader.ReadUInt16),
                SpecialType.System_Int32 => nameof(Reader.ReadInt32),
                SpecialType.System_UInt32 => nameof(Reader.ReadUInt32),
                SpecialType.System_Int64 => nameof(Reader.ReadInt64),
                SpecialType.System_UInt64 => nameof(Reader.ReadUInt64),
                //SpecialType.System_Single => nameof(Reader.ReadSingle),
                SpecialType.System_Double => nameof(Reader.ReadDouble),
                SpecialType.System_String => nameof(Reader.ReadString),
                SpecialType.System_Object => nameof(Reader.ReadVariant),
                _ => throw new ArgumentException($"Cannot parse return type {returnType.Name}.")
            };
        }

        private static string ParseSignature(SemanticModel semanticModel, BaseMethodDeclarationSyntax methodDeclarationSyntax)
        {
            ITypeSymbol safeHandleType = semanticModel.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle")!;
            ITypeSymbol objectPathType = semanticModel.Compilation.GetTypeByMetadataName("Tmds.DBus.Protocol.ObjectPath")!;
            ITypeSymbol dictionaryType = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")!;

            StringBuilder sb = new();

            foreach (ParameterSyntax parameter in methodDeclarationSyntax.ParameterList.Parameters)
            {
                if (parameter.Type is null) continue;
                TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
                if (typeInfo.Type is null)
                    throw new Exception($"Unable to parse type for signature. {parameter.Type}");

                if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, dictionaryType))
                {
                    sb.Append("a{");
                    ImmutableArray<ITypeSymbol> typeArguments = ((INamedTypeSymbol)typeInfo.Type).TypeArguments;
                    sb.Append(ParsePrimitiveSignature(typeArguments[0]));
                    sb.Append(ParsePrimitiveSignature(typeArguments[1]));
                    sb.Append('}');
                }
                else if (typeInfo.Type.TypeKind == TypeKind.Array)
                {
                    sb.Append('a');
                    sb.Append(ParsePrimitiveSignature(typeInfo.Type.BaseType!));
                }
                else
                {
                    sb.Append(ParsePrimitiveSignature(typeInfo.Type));
                }
            }

            return sb.ToString();

            char ParsePrimitiveSignature(ITypeSymbol type)
            {
                switch (type.SpecialType)
                {
                    case SpecialType.System_Byte:
                        return 'y';
                    case SpecialType.System_Boolean:
                        return 'b';
                    case SpecialType.System_Int16:
                        return 'n';
                    case SpecialType.System_UInt16:
                        return 'q';
                    case SpecialType.System_Int32:
                        return 'i';
                    case SpecialType.System_UInt32:
                        return 'u';
                    case SpecialType.System_Int64:
                        return 'x';
                    case SpecialType.System_UInt64:
                        return 't';
                    case SpecialType.System_Single:
                        return 'f';
                    case SpecialType.System_Double:
                        return 'd';
                    case SpecialType.System_String:
                        return 's';
                    case SpecialType.System_Object:
                        return 'v';
                }

                if (SymbolEqualityComparer.Default.Equals(type, objectPathType))
                    return 'o';
                if (InheritsFrom(type, safeHandleType))
                    return 'h';
                throw new ArgumentException($"Cannot parse type {type.Name}.");
            }
        }
    }
}
