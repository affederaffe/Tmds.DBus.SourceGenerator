using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorUtils;


namespace Tmds.DBus.SourceGenerator;

[Generator]
public class DBusSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        XmlSerializer xmlSerializer = new(typeof(DBusNode));
        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true,
            IgnoreComments = true
        };

        ReadWriteMethodsCache readWriteMethodsCache = new();

        context.RegisterPostInitializationOutput(initializationContext =>
        {
            initializationContext.AddSource("Tmds.DBus.SourceGenerator.SignalHelper.cs", DBusSourceGeneratorClasses.SignalHelperClass);
            initializationContext.AddSource("Tmds.DBus.SourceGenerator.PathHandler.cs", DBusSourceGeneratorClasses.PathHandlerClass);
            initializationContext.AddSource("Tmds.DBus.SourceGenerator.IDBusInterfaceHandler.cs", DBusSourceGeneratorClasses.DBusInterfaceHandlerInterface);
            initializationContext.AddSource("Tmds.DBus.SourceGenerator.WriterExtensions.WriteNullableString.cs", DBusSourceGeneratorClasses.WriteNullableStringWriterExtension);
        });

        IncrementalValuesProvider<(DBusNode Node, string GeneratorMode)> generatorProvider = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".xml", StringComparison.Ordinal))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((x, _) =>
            {
                if (!x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.DBusGeneratorMode", out string? generatorMode))
                    return default;
                StringReader sr = new(x.Left.GetText()!.ToString());
                XmlReader xmlReader = XmlReader.Create(sr, xmlReaderSettings);
                if (xmlSerializer.Deserialize(xmlReader) is not DBusNode dBusNode)
                    return default;
                return dBusNode.Interfaces is null ? default : ValueTuple.Create(dBusNode, generatorMode);
            })
            .Where(static x => x is { Item1: not null, Item2: not null });

        context.RegisterSourceOutput(generatorProvider.Collect(), (productionContext, provider) =>
        {
            DBusSourceGeneratorUnit unit = new(productionContext, readWriteMethodsCache);
            foreach ((DBusNode node, string generatorMode) in provider)
            {
                switch (generatorMode)
                {
                    case "Proxy":
                        unit.GenerateProxyFromNode(node);
                        break;
                    case "Handler":
                        unit.GenerateHandlerFromNode(node);
                        break;
                }
            }

            CompilationUnitSyntax readerExtensions = MakeCompilationUnit(
                NamespaceDeclaration(
                        IdentifierName("Tmds.DBus.SourceGenerator"))
                    .AddMembers(
                        ClassDeclaration("ReaderExtensions")
                            .AddModifiers(
                                Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword))
                            .WithMembers(readWriteMethodsCache.GetReadMethods())));

            CompilationUnitSyntax writerExtensions = MakeCompilationUnit(
                NamespaceDeclaration(
                        IdentifierName("Tmds.DBus.SourceGenerator"))
                    .AddMembers(
                        ClassDeclaration("WriterExtensions")
                            .AddModifiers(
                                Token(SyntaxKind.InternalKeyword),
                                Token(SyntaxKind.StaticKeyword),
                                Token(SyntaxKind.PartialKeyword))
                            .WithMembers(readWriteMethodsCache.GetWriteMethods())));

            productionContext.AddSource("Tmds.DBus.SourceGenerator.ReaderExtensions.cs", readerExtensions.GetText(Encoding.UTF8));
            productionContext.AddSource("Tmds.DBus.SourceGenerator.WriterExtensions.cs", writerExtensions.GetText(Encoding.UTF8));
        });
    }
}
