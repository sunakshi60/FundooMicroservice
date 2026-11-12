using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notification.Application.Templates
{
    public static class WelcomeEmailTemplate
    {
        public static string Build(string recipientName)
        {
            var safeName = WebUtility.HtmlEncode(recipientName);

            return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <title>Welcome to Fundoo Notes</title>
            </head>
            <body style="font-family: Arial, sans-serif;">
                <h2>Welcome to Fundoo Notes!</h2>

                <p>Hello {safeName},</p>

                <p>
                    Your Fundoo Notes account has been created
                    successfully.
                </p>

                <p>
                    You can now organize your notes and
                    manage them in one place.
                </p>

                <p>Regards,<br>Fundoo Notes Team</p>
            </body>
            </html>
            """;
        }
    }
}
