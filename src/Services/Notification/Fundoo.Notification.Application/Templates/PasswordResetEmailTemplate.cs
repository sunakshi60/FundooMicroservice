using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notification.Application.Templates
{
    public static class PasswordResetEmailTemplate
    {
        public static string Build(
            string recipientName,
            string resetToken)
        {
            var safeName = WebUtility.HtmlEncode(recipientName);
            var safeToken = WebUtility.HtmlEncode(resetToken);

                            return $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="UTF-8">
                    <title>Fundoo Password Reset</title>
                </head>
                <body style="margin:0; padding:24px; font-family: Arial, sans-serif; color:#222;">
                    <h2 style="margin:0 0 20px 0;">Password Reset Request</h2>

                    <p>Hello {safeName},</p>

                    <p>We received a request to reset your Fundoo password.</p>

                    <p>Your password reset token is:</p>

                    <div style="margin:16px 0; padding:14px 16px; background:#f4f6f8;
                                border:1px solid #dde1e6; border-radius:6px;
                                font-family: Consolas, 'Courier New', monospace;
                                font-size:15px; font-weight:bold; letter-spacing:1px;
                                word-break:break-all;">
                        {safeToken}
                    </div>

                    <p style="color:#666; font-size:13px;">
                        This token expires in 15 minutes.
                    </p>

                    <p style="color:#666; font-size:13px;">
                        If you did not request a password reset, you can safely ignore this email.
                    </p>

                    <p>Regards,<br>Fundoo Notes Team</p>
                </body>
                </html>
                """;
        }
    }
}