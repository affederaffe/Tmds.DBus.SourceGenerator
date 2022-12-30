using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    [Generator]
    public partial class DBusSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<GeneratorAttributeSyntaxContext> classWithAttributeProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Tmds.DBus.SourceGenerator.DBusInterfaceAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ctx);

            context.RegisterPostInitializationOutput(static initializationContext =>
            {
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.DBusInterfaceAttribute.cs", MakeDBusInterfaceAttribute().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.DBusObject.cs", MakeDBusObjectClass().GetText(Encoding.UTF8));
                initializationContext.AddSource("Tmds.DBus.SourceGenerator.PropertyChanges.cs", MakePropertyChangesClass().GetText(Encoding.UTF8));
            });

            context.RegisterSourceOutput(classWithAttributeProvider, static (productionContext, syntaxContext) =>
            {
                ClassDeclarationSyntax classNode = (ClassDeclarationSyntax)syntaxContext.TargetNode;
                INamedTypeSymbol? declaredClass = syntaxContext.SemanticModel.GetDeclaredSymbol(classNode);
                if (declaredClass is null) return;
                string @namespace = declaredClass.ContainingNamespace.ToDisplayString();
                TypeDeclarationSyntax? typeDeclarationSyntax = GenerateProxy(syntaxContext.SemanticModel, classNode, syntaxContext.Attributes[0]);
                if (typeDeclarationSyntax is null) return;
                NamespaceDeclarationSyntax namespaceDeclaration = NamespaceDeclaration(IdentifierName(@namespace)).AddMembers(typeDeclarationSyntax);
                CompilationUnitSyntax compilationUnitSyntax = MakeCompilationUnit(namespaceDeclaration);
                productionContext.AddSource($"{@namespace}.{declaredClass.Name}.g.cs", compilationUnitSyntax.GetText(Encoding.UTF8));
            });
        }
    }
}
