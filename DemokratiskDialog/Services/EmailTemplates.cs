using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public static class EmailTemplates
    {
        public static (string, string) UnauthorizedError(string username, string signinUrl)
            => (
                "Vi kunne desværre ikke gennemføre blokeringskontrollen",
                $"<p>Kære {username},</p>" +
                "<p>Vi forsøgte at gennemføre blokeringskontrollen med den adgang, du har givet os, men blev afvist af Twitter.<br />" +
                "Det kan eksempelvis ske, hvis du har tilbagekaldt tilladelsen før vi blev færdige.</p>" +
                $"<p>Du kan prøve at <a href=\"{signinUrl}\" title=\"Log ind\">logge ind</a> igen og vente med at tilbagekalde tilladelsen, til du har modtaget mailen med oversigten.</p>" +
                "<p>Mange hilsener,<br />Demokratisk Dialog</p>"
            );
    }
}
