using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace FnSync
{
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    [Serializable]
    public class SerializableStringDictionary : StringDictionary, IXmlSerializable
    {
        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            while (reader.Read() &&
                !(reader.NodeType == XmlNodeType.EndElement && reader.LocalName == this.GetType().Name))
            {
                string name = reader["Key"];
                if (name == null)
                    throw new FormatException();

                string value = reader.ReadElementContentAsString();
                this[name] = value;
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (DictionaryEntry entry in this)
            {
                writer.WriteStartElement("Item");
                writer.WriteAttributeString("Key", (string)entry.Key);
                writer.WriteString((string)entry.Value);
                writer.WriteEndElement();
            }
        }
    }
}
