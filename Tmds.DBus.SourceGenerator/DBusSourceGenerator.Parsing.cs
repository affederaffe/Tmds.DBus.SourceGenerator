using System;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static string? ParseWriteMethod(SemanticModel semanticModel, ParameterSyntax parameter)
        {
            if (parameter.Type is null) return null;
            TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
            return typeInfo.Type is null ? null : $"Write{ParseTypeForReadWriteMethod(typeInfo.Type, semanticModel)}";
        }

        private static string? ParseReadMethod(SemanticModel semanticModel, TypeSyntax returnTypeSyntax)
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(returnTypeSyntax);
            if (typeInfo.Type is not INamedTypeSymbol taskType) return null;
            return taskType.Arity == 0 ? null : $"Read{ParseTypeForReadWriteMethod(taskType.TypeArguments[0], semanticModel)}";
        }

        private static string ParseSignature(SemanticModel semanticModel, BaseMethodDeclarationSyntax methodDeclarationSyntax)
        {
            StringBuilder sb = new();

            foreach (ParameterSyntax parameter in methodDeclarationSyntax.ParameterList.Parameters)
            {
                if (parameter.Type is null) continue;
                TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
                if (typeInfo.Type is null) continue;
                sb.Append(ParseTypeForSignature(typeInfo.Type, semanticModel));
            }

            return sb.ToString();
        }

        private static string ParseTypeForReadWriteMethod(ITypeSymbol type, SemanticModel semanticModel) => type.SpecialType switch
        {
            SpecialType.System_Byte => "Byte",
            SpecialType.System_Boolean => "Bool",
            SpecialType.System_Int16 => "Int16",
            SpecialType.System_UInt16 => "UInt16",
            SpecialType.System_Int32 => "Int32",
            SpecialType.System_UInt32 => "UInt32",
            SpecialType.System_Int64 => "Int64",
            SpecialType.System_UInt64 => "UInt64",
            //SpecialType.System_Single => "Single",
            SpecialType.System_Double => "Double",
            SpecialType.System_String => "String",
            SpecialType.System_Object => "Variant",
            SpecialType.None when InheritsFrom(type, semanticModel.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle")) => $"Handle<{type.ToDisplayString()}>",
            SpecialType.None when SymbolEqualityComparer.Default.Equals(type, semanticModel.Compilation.GetTypeByMetadataName("Tmds.DBus.Protocol.ObjectPath")) => "ObjectPath",
            SpecialType.None when type is IArrayTypeSymbol arrayTypeSymbol => $"Array<{arrayTypeSymbol.BaseType!.ToDisplayString()}>",
            SpecialType.None when type is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol && SymbolEqualityComparer.Default.Equals(namedTypeSymbol.ConstructedFrom, semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")) => $"Dictionary<{namedTypeSymbol.TypeArguments[0].ToDisplayString()}, {namedTypeSymbol.TypeArguments[1].ToDisplayString()}>",
            _ => throw new ArgumentOutOfRangeException($"Cannot parse type {type} for signature.")
        };

        private static string ParseTypeForSignature(ITypeSymbol type, SemanticModel semanticModel) => type.SpecialType switch
            {
                SpecialType.System_Byte => "y",
                SpecialType.System_Boolean => "b",
                SpecialType.System_Int16 => "n",
                SpecialType.System_UInt16 => "q",
                SpecialType.System_Int32 => "i",
                SpecialType.System_UInt32 => "u",
                SpecialType.System_Int64 => "x",
                SpecialType.System_UInt64 => "t",
                SpecialType.System_Single => "f",
                SpecialType.System_Double => "d",
                SpecialType.System_String => "s",
                SpecialType.System_Object => "v",
                SpecialType.None when InheritsFrom(type, semanticModel.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle")) => "h",
                SpecialType.None when SymbolEqualityComparer.Default.Equals(type, semanticModel.Compilation.GetTypeByMetadataName("Tmds.DBus.Protocol.ObjectPath")) => "o",
                SpecialType.None when type is IArrayTypeSymbol arrayTypeSymbol => $"a{ParseTypeForSignature(arrayTypeSymbol.ElementType, semanticModel)}",
                SpecialType.None when type is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol && SymbolEqualityComparer.Default.Equals(namedTypeSymbol.ConstructedFrom, semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")) => $"a{{{ParseTypeForSignature(namedTypeSymbol.TypeArguments[0], semanticModel)}{ParseTypeForSignature(namedTypeSymbol.TypeArguments[1], semanticModel)}}}",
                _ => throw new ArgumentOutOfRangeException($"Cannot parse type {type} for signature.")
            };
    }
}
