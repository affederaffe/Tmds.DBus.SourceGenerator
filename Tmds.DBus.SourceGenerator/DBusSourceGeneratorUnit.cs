using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorUtils;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorParsing;


namespace Tmds.DBus.SourceGenerator;

public partial class DBusSourceGeneratorUnit(SourceProductionContext productionContext, ReadWriteMethodsCache readWriteMethodsCache)
{
    public void GenerateProxyFromNode(DBusNode node)
    {
        foreach (DBusInterface dBusInterface in node.Interfaces!)
        {
            TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(dBusInterface);
            NamespaceDeclarationSyntax namespaceDeclaration = NamespaceDeclaration(
                    IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(typeDeclarationSyntax);
            CompilationUnitSyntax compilationUnit = MakeCompilationUnit(namespaceDeclaration);
            productionContext.AddSource($"Tmds.DBus.SourceGenerator.{Pascalize(dBusInterface.Name.AsSpan())}Proxy.g.cs", compilationUnit.GetText(Encoding.UTF8));
        }
    }

    public void GenerateHandlerFromNode(DBusNode node)
    {
        foreach (DBusInterface dBusInterface in node.Interfaces!)
        {
            TypeDeclarationSyntax typeDeclarationSyntax = GenerateHandler(dBusInterface);
            NamespaceDeclarationSyntax namespaceDeclaration = NamespaceDeclaration(
                    IdentifierName("Tmds.DBus.SourceGenerator"))
                .AddMembers(typeDeclarationSyntax);
            CompilationUnitSyntax compilationUnit = MakeCompilationUnit(namespaceDeclaration);
            productionContext.AddSource($"Tmds.DBus.SourceGenerator.{Pascalize(dBusInterface.Name.AsSpan())}Handler.g.cs", compilationUnit.GetText(Encoding.UTF8));
        }
    }

    private static DBusArgument[]? GetInArgs(DBusArgument[]? dBusArguments) => dBusArguments?.Where(static m => m.Direction is null or "in").ToArray();

    private static DBusArgument[]? GetOutArgs(DBusArgument[]? dBusArguments) => dBusArguments?.Where(static m => m.Direction == "out").ToArray();
}
