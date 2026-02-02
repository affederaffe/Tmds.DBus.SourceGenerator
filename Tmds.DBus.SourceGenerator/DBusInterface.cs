using System;
using System.ComponentModel;
using System.Xml.Serialization;
using static Tmds.DBus.SourceGenerator.DBusSourceGeneratorParsing;


namespace Tmds.DBus.SourceGenerator;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false, ElementName = "node")]
public class DBusNode
{
    [XmlElement("interface")]
    public DBusInterface[]? Interfaces { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false, ElementName = "interface")]
public class DBusInterface
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("method")]
    public DBusMethod[]? Methods { get; set; }

    [XmlElement("signal")]
    public DBusSignal[]? Signals { get; set; }

    [XmlElement("property")]
    public DBusProperty[]? Properties { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusMethod
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("arg")]
    public DBusArgument[]? Arguments { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusSignal
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("arg")]
    public DBusArgument[]? Arguments { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusProperty : DBusValue
{
    [XmlAttribute("access")]
    public string? Access { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusArgument : DBusValue
{
    [XmlAttribute("direction")]
    public string? Direction { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusValue
{
    [XmlIgnore]
    private DBusDotnetType? _dbusDotnetType;

    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlAttribute("type")]
    public string? Type { get; set; }

    [XmlIgnore]
    public DBusDotnetType DBusDotnetType => _dbusDotnetType ??= DBusDotnetType.FromDBusValue(this);
}
