using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Xml.Serialization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    [Generator]
    public partial class DBusSourceGenerator : IIncrementalGenerator
    {
        private Dictionary<string, string> _readMethodForSignature = null!;
        private ClassDeclarationSyntax _readerExtensions = null!;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            _readMethodForSignature = new Dictionary<string, string>();
            _readerExtensions = ClassDeclaration("ReaderExtensions")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword));

            IncrementalValueProvider<string?> projectPathProvider = context.AnalyzerConfigOptionsProvider
                .Select(static (provider, _) => provider.GlobalOptions.TryGetValue("build_property.projectdir", out string? value) ? value : null);

            IncrementalValuesProvider<GeneratorAttributeSyntaxContext> classWithInterfaceAttribute = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Tmds.DBus.SourceGenerator.DBusInterfaceAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ctx);

            IncrementalValuesProvider<GeneratorAttributeSyntaxContext> classWithHandlerAttribute = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Tmds.DBus.SourceGenerator.DBusHandlerAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ctx);

            IncrementalValueProvider<((ImmutableArray<GeneratorAttributeSyntaxContext> Interfaces, ImmutableArray<GeneratorAttributeSyntaxContext> Handlers) ClassesWithAttribute, string? ProjectPath)> combinedProvider = classWithInterfaceAttribute.Collect().Combine(classWithHandlerAttribute.Collect()).Combine(projectPathProvider);

            context.RegisterPostInitializationOutput(static initializationContext =>
            {
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.DBusInterfaceAttribute.cs", MakeDBusInterfaceAttribute().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.DBusHandlerAttribute.cs", MakeDBusHandlerAttribute().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.PropertyChanges.cs", MakePropertyChangesClass().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.SignalHelper.cs", MakeSignalHelperClass().GetText(Encoding.UTF8));
            });

            context.RegisterSourceOutput(combinedProvider, (productionContext, providers) =>
            {
                if (providers.ProjectPath is null) return;

                foreach (GeneratorAttributeSyntaxContext syntaxContext in providers.ClassesWithAttribute.Interfaces)
                {
                    if (syntaxContext.Attributes[0].ConstructorArguments[0].Value is not string xmlPath) continue;
                    string path = Path.Combine(providers.ProjectPath, xmlPath);
                    if (new XmlSerializer(typeof(DBusNode)).Deserialize(File.OpenRead(path)) is not DBusNode dBusNode) continue;
                    if (dBusNode.Interfaces is null) continue;
                    ClassDeclarationSyntax classNode = (ClassDeclarationSyntax)syntaxContext.TargetNode;
                    INamedTypeSymbol? declaredClass = syntaxContext.SemanticModel.GetDeclaredSymbol(classNode);
                    if (declaredClass is null) continue;
                    string @namespace = declaredClass.ContainingNamespace.ToDisplayString();
                    foreach (DBusInterface dBusInterface in dBusNode.Interfaces)
                    {
                        TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(dBusInterface);
                        NamespaceDeclarationSyntax namespaceDeclaration = NamespaceDeclaration(IdentifierName(@namespace)).AddMembers(typeDeclarationSyntax);
                        CompilationUnitSyntax compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                        productionContext.AddSource($"{@namespace}.{Pascalize(dBusInterface.Name!)}.g.cs", compilationUnit.GetText(Encoding.UTF8));
                    }
                }

                foreach (GeneratorAttributeSyntaxContext syntaxContext in providers.ClassesWithAttribute.Handlers)
                {
                    if (syntaxContext.Attributes[0].ConstructorArguments[0].Value is not string xmlPath) continue;
                    string path = Path.Combine(providers.ProjectPath, xmlPath);
                    if (new XmlSerializer(typeof(DBusNode)).Deserialize(File.OpenRead(path)) is not DBusNode dBusNode) continue;
                    if (dBusNode.Interfaces is null) continue;
                    ClassDeclarationSyntax classNode = (ClassDeclarationSyntax)syntaxContext.TargetNode;
                    INamedTypeSymbol? declaredClass = syntaxContext.SemanticModel.GetDeclaredSymbol(classNode);
                    if (declaredClass is null) continue;
                    string @namespace = declaredClass.ContainingNamespace.ToDisplayString();
                    foreach (DBusInterface dBusInterface in dBusNode.Interfaces)
                    {
                        TypeDeclarationSyntax typeDeclarationSyntax = GenerateHandler(dBusInterface);
                        NamespaceDeclarationSyntax namespaceDeclaration = NamespaceDeclaration(IdentifierName(@namespace)).AddMembers(typeDeclarationSyntax);
                        CompilationUnitSyntax compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                        productionContext.AddSource($"{@namespace}.{Pascalize(dBusInterface.Name!)}.g.cs", compilationUnit.GetText(Encoding.UTF8));
                    }
                }

                productionContext.AddSource("Tmds.DBus.SourceGenerator.ReaderExtensions.cs", MakeCompilationUnit(NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator")).AddMembers(_readerExtensions)).GetText(Encoding.UTF8));
            });
        }
    }
}
