using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static bool InheritsFrom(ITypeSymbol? symbol, ISymbol type)
        {
            ITypeSymbol? baseType = symbol?.BaseType;
            while (baseType is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, type))
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }

        private static LiteralExpressionSyntax MakeLiteralExpression(string literal) => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal));
    }
}
