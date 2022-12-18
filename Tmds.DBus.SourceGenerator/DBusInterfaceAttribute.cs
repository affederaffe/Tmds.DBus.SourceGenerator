using System;


namespace Tmds.DBus.SourceGenerator
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DBusInterfaceAttribute : Attribute
    {
        public DBusInterfaceAttribute(string path, string serviceName)
        {
            Path = path;
            ServiceName = serviceName;
        }

        public string Path { get; }

        public string ServiceName { get; }
    }
}
