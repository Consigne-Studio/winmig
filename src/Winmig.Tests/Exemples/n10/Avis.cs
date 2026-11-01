using System.Net.Mail;

namespace Expedition;

public class Avis
{
    public void Envoyer(string a, string texte)
    {
        using var smtp = new SmtpClient("smtp.office365.com", 587) { EnableSsl = true };
        smtp.Send("expedition@exemple.fr", a, "Expédition", texte);
    }
}
