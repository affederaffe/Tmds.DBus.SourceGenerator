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

            IncrementalValuesProvider<GeneratorAttributeSyntaxContext> classWithAttributeProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Tmds.DBus.SourceGenerator.DBusInterfaceAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ctx);

            IncrementalValueProvider<(ImmutableArray<GeneratorAttributeSyntaxContext> ClassesWithAttribute, string? ProjectDir)> combinedProvider = classWithAttributeProvider.Collect().Combine(projectPathProvider);

            context.RegisterPostInitializationOutput(static initializationContext =>
            {
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.DBusInterfaceAttribute.cs", MakeDBusInterfaceAttribute().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.PropertyChanges.cs", MakePropertyChangesClass().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.SignalHelper.cs", MakeSignalHelperClass().GetText(Encoding.UTF8));
            });

            context.RegisterSourceOutput(combinedProvider, (productionContext, providers) =>
            {
                foreach (GeneratorAttributeSyntaxContext syntaxContext in providers.ClassesWithAttribute)
                {
                    if (providers.ProjectDir is null || syntaxContext.Attributes[0].ConstructorArguments[0].Value is not string xmlPath) continue;
                    string path = Path.Combine(providers.ProjectDir, xmlPath);
                    if (new XmlSerializer(typeof(DBusNode)).Deserialize(new StringReader(File.ReadAllText(path))) is not DBusNode dBusNode) continue;
                    if (dBusNode.Interface is null) continue;
                    ClassDeclarationSyntax classNode = (ClassDeclarationSyntax)syntaxContext.TargetNode;
                    INamedTypeSymbol? declaredClass = syntaxContext.SemanticModel.GetDeclaredSymbol(classNode);
                    if (declaredClass is null) continue;
                    string @namespace = declaredClass.ContainingNamespace.ToDisplayString();
                    TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(classNode, dBusNode.Interface);
                    NamespaceDeclarationSyntax namespaceDeclaration = NamespaceDeclaration(IdentifierName(@namespace)).AddMembers(typeDeclarationSyntax);
                    CompilationUnitSyntax compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                    productionContext.AddSource($"{@namespace}.{declaredClass.Name}.g.cs", compilationUnit.GetText(Encoding.UTF8));
                }

                productionContext.AddSource("Tmds.DBus.SourceGenerator.ReaderExtensions.cs", MakeCompilationUnit(NamespaceDeclaration(IdentifierName("Tmds.DBus.SourceGenerator")).AddMembers(_readerExtensions)).GetText(Encoding.UTF8));
            });
        }
    }
}
