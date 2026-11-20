using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LoopCastsWeb.Services;

/// <summary>
/// Identity's e-mail sender: "Forgot password?" and "Change e-mail". The site sends these itself over SMTP
/// (config Smtp:Host / Smtp:Port / Smtp:From, + Smtp:User / Smtp:Password on the live site; locally Mailpit),
/// NOT via n8n: getting back into your account must work
/// even when n8n is down (#27, docs/decisions/0001-n8n-or-code.md). Built-in System.Net.Mail, no package.
/// SMTP down? Logged + a message on the next page, never thrown: Identity's pages (in the package) don't catch it.
/// </summary>
public class LoginEmailSender : IEmailSender
{
    public const string NotSentKey = "LoginEmailNotSent";   // TempData flag, shown by Account/_Flash
    public const string NotSentText = "We couldn't send the e-mail just now. Try again in a few minutes, or mail support.";

    private readonly IConfiguration _config;
    private readonly ILogger<LoginEmailSender> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly ITempDataDictionaryFactory _tempData;

    public LoginEmailSender(IConfiguration config, ILogger<LoginEmailSender> logger,
        IHttpContextAccessor http, ITempDataDictionaryFactory tempData)
    {
        _config = config;
        _logger = logger;
        _http = http;
        _tempData = tempData;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        (subject, htmlMessage) = InOurWords(subject, htmlMessage);
        try
        {
            if (_config["Smtp:Host"] is not { Length: > 0 } host) throw new InvalidOperationException("Smtp:Host is not set");
            using var message = new MailMessage(_config["Smtp:From"] ?? _config["Seller:SupportEmail"] ?? "", email, subject, htmlMessage)
            {
                IsBodyHtml = true,
            };
            using var smtp = Client(_config, host);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await smtp.SendMailAsync(message, timeout.Token);
        }
        catch (Exception ex)   // SmtpException, timeout, bad address, not configured, ...
        {
            _logger.LogWarning(ex, "Login e-mail '{Subject}' could not be sent", subject);   // no address in the log
            // Only a logged-in user (Change e-mail) gets the message. On "Forgot password?" it would appear only for
            // addresses we know: a stranger could test who is a customer. That page always says what to do instead.
            if (_http.HttpContext is { User.Identity.IsAuthenticated: true } context)
                _tempData.GetTempData(context)[NotSentKey] = true;
        }
    }

    // Smtp:User set (Mailtrap sandbox on the live site, #64) → log in, and only over TLS (STARTTLS): a password is never
    // sent in plain text. No user (Mailpit locally) → no login, no TLS, as before.
    public static SmtpClient Client(IConfiguration config, string host)
    {
        var smtp = new SmtpClient(host, config.GetValue("Smtp:Port", 25));
        if (config["Smtp:User"] is { Length: > 0 } user)
        {
            smtp.Credentials = new NetworkCredential(user, config["Smtp:Password"]);
            smtp.EnableSsl = true;
        }
        return smtp;
    }

    // Identity writes its own e-mails ("Please reset your password by <a href='…'>clicking here</a>.").
    // We keep only its link and say it in our words (loopcasts-ui §6). Any other e-mail goes out unchanged.
    // Recognised by the start of Identity's text, not the subject: the subject differs per Identity version (#21).
    private static readonly Dictionary<string, (string Subject, string Text, string Button)> OurWords = new()
    {
        ["Please reset your password"] = ("Choose a new LoopCasts password",
            "You asked to choose a new password for your LoopCasts login. The button works for 24 hours. Didn't ask? Then ignore this e-mail; your password stays the same.",
            "Choose a new password"),
        ["Please confirm your account"] = ("Confirm your LoopCasts e-mail",
            "Click the button to use this address for your LoopCasts login and invoices. Didn't ask? Then ignore this e-mail; nothing changes.",
            "Confirm this e-mail"),
    };
    private static readonly Regex Href = new("href='([^']*)'");

    public (string Subject, string Html) InOurWords(string subject, string html)
    {
        var known = OurWords.FirstOrDefault(x => html.StartsWith(x.Key, StringComparison.Ordinal));
        if (known.Key is null || Href.Match(html) is not { Success: true } link) return (subject, html);
        var w = known.Value;
        var url = link.Groups[1].Value;   // already HTML-encoded by Identity
        return (w.Subject,
            $"<p>Hi,</p><p>{w.Text}</p><p><a href=\"{url}\">{w.Button}</a></p>" +
            $"<p>The LoopCasts team<br>Questions? Mail {WebUtility.HtmlEncode(_config["Seller:SupportEmail"])}</p>");
    }
}
