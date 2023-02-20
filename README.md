# Tmds.DBus.SourceGenerator
A roslyn source generator targeting the Tmds.DBus.Protocol API

### Introduction
This source generator completely eliminates the usage of reflection in order to be trimmer- and AOT-friendly.
For further documentation of Tmds.DBus and DBus in general, see https://github.com/tmds/Tmds.DBus#readme.

### Usage
Either install the NuGet package `Tmds.DBus.SourceGenerator` or clone the git repository and add a project reference to the source generator in your `.csproj`

```xml
<ItemGroup>
    <ProjectReference Include="./Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
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
