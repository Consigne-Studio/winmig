using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MailKit.Net.Smtp;

namespace Racine;

public interface IFabrique { object CreateObject(string nom); }

public static class Code
{
    // Ancienne chaîne : Provider=SQLOLEDB.1 -- remplacée en 2019
    public static void Envoyer(IFabrique fabrique)
    {
        var o = fabrique.CreateObject("x");
        using var client = new SmtpClient();
        client.Connect("smtp.exemple.fr", 587);
    }

    public static object Relire(Stream s) => new BinaryFormatter().Deserialize(s);
}
