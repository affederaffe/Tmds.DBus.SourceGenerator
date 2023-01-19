# Tmds.DBus.SourceGenerator
A roslyn source generator for creating proxies targeting the Tmds.DBus.Protocol API

### Introduction
This source generator completely eliminates the usage of reflection in order to be trimmer- and AOT-friendly.
For furhter documentation of Tmds.DBus and DBus in general, see https://github.com/tmds/Tmds.DBus#readme.

### Usage
Add a project reference in your `.csproj`
```xml
<ItemGroup>
  <ProjectReference Include="./Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator/Tmds.DBus.SourceGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<ItemGroup/>
```


Then create a new, empty class and add `DBusInterface` attributes for proxy generation and `DBusHandler` attributes for handler generation and pass the path of the xml definition relative to your `.csproj` file.

```cs
using Tmds.DBus.SourceGenerator;

namespace DBusSourceGenerationExample
{
    [DBusInterface("./DBusXml/ExampleInterface.xml")]
    [DBusHandler("./DBusXml/ExampleService.xml")]
    public class DBusInterfaces { }
}
```

Now you can instantiate the generated proxy class and use it like with the traditional Tmds.DBus.
For handlers, create a new class again and inherit from the generated one and implement its abstract methods.
