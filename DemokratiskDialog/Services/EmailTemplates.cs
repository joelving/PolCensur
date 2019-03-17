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
                "<p>Mange hilsener,<br />polcensur.dk</p>"
            );
        public static (string, string) Completed(string username, string blocksUrl)
            => (
                "Blokeringskontrollen er gennemført",
                $"<p>Kære {username},</p>" +
                "<p>Vi her netop gennemført blokeringskontrollen for din Twitter-konto.</p>" +
                $"<p>Du kan se resultatet under din profil ved at klikke på <a href=\"{blocksUrl}\" title=\"Dine blokeringer\">blokeringer</a>.</p>" +
                "<p>Vi håber, at du kan bruge det til noget.</p>" +
                "<p>Mange hilsener,<br />polcensur.dk</p>"
            );
        public static (string, string) Failed(string username, string indexUrl)
            => (
                "Vi kunne desværre ikke gennemføre blokeringskontrollen",
                $"<p>Kære {username},</p>" +
                "<p>Vi forsøgte at gennemføre blokeringskontrollen med den adgang, du har givet os, men mødte en fejl, vi ikke kunne arbejde os rundt om.<br />" +
                "Det kan der være mange grunde til, men typisk er det en teknisk fejl i vores opsætning. Det beklager vi mange gange.</p>" +
                $"<p>Du kan prøve at <a href=\"{indexUrl}\" title=\"Kontrollér blokeringer\">kontrollere dine blokeringer</a> endnu en gang. " +
                "Oplever du stadig fejlen, er du meget velkommen til at skrive til os på <a href=\"mailto:support@polcensur.dk\" title=\"Skriv til supporten\">support@polcensur.dk</a>.</p>" +
                "<p>Mange hilsener,<br />polcensur.dk</p>"
            );
        public static (string, string) BlocksUpdated(string username, string blocksUrl)
            => (
                "Blokeringskontrollen er gennemført",
                $"<p>Kære {username},</p>" +
                "<p>Vi her netop opdaget en ændring i dine blokeringer.</p>" +
                $"<p>Du kan se en opdateret liste af blokeringer under din profil ved at klikke på <a href=\"{blocksUrl}\" title=\"Dine blokeringer\">blokeringer</a>.</p>" +
                "<p>Vi håber, at du kan bruge det til noget.</p>" +
                "<p>Mange hilsener,<br />polcensur.dk</p>"
            );
    }
}
