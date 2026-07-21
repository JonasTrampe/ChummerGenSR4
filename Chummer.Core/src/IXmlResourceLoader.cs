using System.Xml;

namespace Chummer.Core
{
    /// <summary>Loads Chummer XML resources for platform-neutral services.</summary>
    public interface IXmlResourceLoader
    {
        XmlDocument Load(string strFileName);
    }
}