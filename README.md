# Tmds.DBus.SourceGenerator
A roslyn source generator targeting the Tmds.DBus.Protocol API

### Introduction
This source generator completely eliminates the usage of reflection in order to be trimmer- and AOT-friendly.
For further documentation of Tmds.DBus and DBus in general, see https://github.com/tmds/Tmds.DBus#readme.

### Usage

##### Note
> This Source Generator targets the `Tmds.DBus.Protocol` API, which means you have to explicitly install said package.

Either install the NuGet package `Tmds.DBus.SourceGenerator` or clone the git repository and add a project reference to the source generator in your `.csproj`

```xml
<ItemGroup>
    <ProjectReference Include="./Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>

<Import Project="./Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator.props" />
```

Then add the xml definitions as `AdditionalFile`s to your project.
Depending on whether you want to generate a Proxy or a handler, set the `DBusGeneratorMode` to either `Proxy` or `Handler`, respectively.

```xml
<ItemGroup>
    <AdditionalFiles Include="DBusXml/DBus.xml" DBusGeneratorMode="Proxy" />
    <AdditionalFiles Include="DBusXml/StatusNotifierItem.xml" DBusGeneratorMode="Handler" />
</ItemGroup>
```

Now you can instantiate the generated proxy class and use it like with the traditional Tmds.DBus.
For handlers, create a new class and inherit from the generated one and implement its abstract methods.

### Examples
For examples, you may take a look at some projects that use this source generator:
* [Avalonia](https://github.com/AvaloniaUI/Avalonia/tree/master/src/Avalonia.FreeDesktop)
* [DBus.Services.Secrets](https://github.com/Ace4896/DBus.Services.Secrets)

### How to obtain DBus interface definitions
DBus interface definitions are written in XML.
There are mainly 2 ways for obtaining those:
1. Finding the the source online _somewhere..._
   This could be a GitHub repo or some obscure website from the mid 2000s (at your own risk).
2. Dumping the definition via introspection
   If you have a service running on your system from which you want to extract the definition, you may use `busctl`, e.g.:\
   To list available services:
   ```
   busctl list
   ```
   To inspect the object tree of a service:
   ```
   busctl tree <service name>
   ```
   To dump the xml definition of the interface:
   ```
   busctl introspect <service name> /path/to/object --xml-interface
   ```
   
