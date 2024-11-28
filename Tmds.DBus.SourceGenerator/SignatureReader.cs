using System;
using System.Text;


namespace Tmds.DBus.SourceGenerator
{
    internal ref struct SignatureReader
    {
        private static int DetermineLength(ReadOnlySpan<byte> span, byte startChar, byte endChar)
        {
            int length = 1;
            int count = 1;
            do
            {
                int offset = span.IndexOfAny(startChar, endChar);
                if (offset == -1)
                    return 0;

                if (span[offset] == startChar)
                    count++;
                else
                    count--;

                length += offset + 1;
                span = span.Slice(offset + 1);
            } while (count > 0);

            return length;
        }

        private static ReadOnlySpan<byte> ReadSingleType(ref ReadOnlySpan<byte> signature)
        {
            if (signature.Length == 0)
                return default;

            int length;
            DBusType type = (DBusType)signature[0];
            switch (type)
            {
                case DBusType.Struct:
                    length = DetermineLength(signature.Slice(1), (byte)'(', (byte)')');
                    break;
                case DBusType.DictEntry:
                    length = DetermineLength(signature.Slice(1), (byte)'{', (byte)'}');
                    break;
                case DBusType.Array:
                    ReadOnlySpan<byte> remainder = signature.Slice(1);
                    length = 1 + ReadSingleType(ref remainder).Length;
                    break;
                default:
                    length = 1;
                    break;
            }

            ReadOnlySpan<byte> rv = signature.Slice(0, length);
            signature = signature.Slice(length);
            return rv;
        }

        public static T Transform<T>(ReadOnlySpan<byte> signature, Func<DBusType, string?, T[], T> map)
        {
            DBusType dbusType = signature.Length == 0 ? DBusType.Invalid : (DBusType)signature[0];

            switch (dbusType)
            {
                case DBusType.Array when (DBusType)signature[1] == DBusType.DictEntry:
                    string sig = Encoding.UTF8.GetString(signature.ToArray());
                    signature = signature.Slice(2);
                    ReadOnlySpan<byte> keySignature = ReadSingleType(ref signature);
                    ReadOnlySpan<byte> valueSignature = ReadSingleType(ref signature);
                    signature = signature.Slice(1);
                    T keyType = Transform(keySignature, map);
                    T valueType = Transform(valueSignature, map);
                    return map(DBusType.DictEntry, sig, [keyType, valueType]);
                case DBusType.Array:
                    sig = Encoding.UTF8.GetString(signature.ToArray());
                    signature = signature.Slice(1);
                    T elementType = Transform(signature, map);
                    //signature = signature.Slice(1);
                    return map(DBusType.Array, sig, [elementType]);
                case DBusType.Struct:
                    sig = Encoding.UTF8.GetString(signature.ToArray());
                    signature = signature.Slice(1, signature.Length - 2);
                    int typeCount = CountTypes(signature);
                    T[] innerTypes = new T[typeCount];
                    for (int i = 0; i < innerTypes.Length; i++)
                    {
                        ReadOnlySpan<byte> innerTypeSignature = ReadSingleType(ref signature);
                        innerTypes[i] = Transform(innerTypeSignature, map);
                    }

                    return map(DBusType.Struct, sig, innerTypes);
                default:
                    return map(dbusType, null, []);
            }
        }

        // Counts the number of single types in a signature.
        private static int CountTypes(ReadOnlySpan<byte> signature)
        {
            if (signature.Length is 0 or 1)
                return signature.Length;

            DBusType type = (DBusType)signature[0];
            signature = signature.Slice(1);

            if (type == DBusType.Struct)
                ReadToEnd(ref signature, (byte)'(', (byte)')');
            else if (type == DBusType.DictEntry)
                ReadToEnd(ref signature, (byte)'{', (byte)'}');

            return (type == DBusType.Array ? 0 : 1) + CountTypes(signature);

            static void ReadToEnd(ref ReadOnlySpan<byte> span, byte startChar, byte endChar)
            {
                int count = 1;
                do
                {
                    int offset = span.IndexOfAny(startChar, endChar);
                    if (span[offset] == startChar)
                        count++;
                    else
                        count--;
                    span = span.Slice(offset + 1);
                } while (count > 0);
            }
        }
    }
}
