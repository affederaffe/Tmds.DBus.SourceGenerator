using System;
using System.ComponentModel;
using System.Xml.Serialization;


namespace Tmds.DBus.SourceGenerator
{
    [Serializable]
    [DesignerCategory("code")]
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false, ElementName = "node")]
    public class DBusNode
    {
        [XmlElement("interface")]
        public DBusInterface? Interface { get; set; }
    }

    [Serializable]
    [DesignerCategory("code")]
    [XmlType(AnonymousType = true)]
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
        private string? _dotNetType;

        [XmlIgnore]
        private string[]? _dotNetInnerTypes;

        [XmlIgnore]
        private DBusType _dBusType;

        [XmlAttribute("name")]
        public string? Name { get; set; }

        [XmlAttribute("type")]
        public string? Type { get; set; }

        [XmlIgnore]
        public string DotNetType
        {
            get
            {
                if (_dotNetType is not null)
                    return _dotNetType;
                (_dotNetType, _dotNetInnerTypes, _dBusType) = DBusSourceGenerator.ParseDotNetType(Type!);
                return _dotNetType;
            }
        }

        [XmlIgnore]
        public string[]? DotNetInnerTypes
        {
            get
            {
                if (_dotNetInnerTypes is not null)
                    return _dotNetInnerTypes;
                (_dotNetType, _dotNetInnerTypes, _dBusType) = DBusSourceGenerator.ParseDotNetType(Type!);
                return _dotNetInnerTypes;
            }
        }

        [XmlIgnore]
        public DBusType DBusType
        {
            get
            {
                if (_dBusType != 0)
                    return _dBusType;
                (_dotNetType, _dotNetInnerTypes, _dBusType) = DBusSourceGenerator.ParseDotNetType(Type!);
                return _dBusType;
            }
        }
    }
}
