using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoopCastsWeb.Tests;

public class LoginEmailSenderTests
{
    private const string IdentityReset = "Please reset your password by <a href='https://shop.test/reset?code=abc'>clicking here</a>.";

    private static (LoginEmailSender Sender, HttpContext Http, ITempDataDictionaryFactory TempData) Sender(int port,
        bool loggedIn = false, string? host = "localhost")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = host,
            ["Smtp:Port"] = port.ToString(),
            ["Smtp:From"] = "LoopCasts <support@loopcasts.example>",
            ["Seller:SupportEmail"] = "support@loopcasts.example",
        }).Build();
        var http = new DefaultHttpContext();
        if (loggedIn) http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann")], "test"));
        var tempData = new TempDataDictionaryFactory(new NoTempDataStore());
        return (new LoginEmailSender(config, NullLogger<LoginEmailSender>.Instance,
            new HttpContextAccessor { HttpContext = http }, tempData), http, tempData);
    }

    [Fact]
    public async Task ForgotPassword_SentOverSmtp_InOurWords()
    {
        using var smtp = new FakeSmtp();

        await Sender(smtp.Port).Sender.SendEmailAsync("ann@example.com", "Reset your password", IdentityReset);

        var received = await smtp.Received;
        Assert.Contains("RCPT TO:<ann@example.com>", received);
        Assert.Contains("Subject: Choose a new LoopCasts password", received);
    }

    [Theory]
    [InlineData("Reset your password")]   // Identity UI 10 (the real one, seen in Mailpit)
    [InlineData("Reset Password")]        // older Identity versions
    public void IdentityEmail_InOurWords_KeepsTheLink(string identitySubject)
    {
        var (subject, html) = Sender(0).Sender.InOurWords(identitySubject, IdentityReset);

        Assert.Equal("Choose a new LoopCasts password", subject);
        Assert.Contains("https://shop.test/reset?code=abc", html);
        Assert.DoesNotContain("clicking here", html);
    }

    [Fact]
    public void UnknownEmail_Unchanged()
    {
        Assert.Equal(("Hello", "plain text"), Sender(0).Sender.InOurWords("Hello", "plain text"));
    }

    [Theory]
    [InlineData(true, true)]     // Change e-mail (logged in): tell the user
    [InlineData(false, false)]   // Forgot password (anonymous): no hint which addresses we know
    public async Task SmtpDown_NoException_FlagOnlyWhenLoggedIn(bool loggedIn, bool flagged)
    {
        var (sender, http, tempData) = Sender(UnusedPort(), loggedIn);

        await sender.SendEmailAsync("ann@example.com", "Reset your password", IdentityReset);   // must not throw

        Assert.Equal(flagged, tempData.GetTempData(http).ContainsKey(LoginEmailSender.NotSentKey));
    }

    [Fact]
    public async Task HostNotSet_NoException_Flagged()
    {
        var (sender, http, tempData) = Sender(25, loggedIn: true, host: null);

        await sender.SendEmailAsync("ann@example.com", "Hello", "plain text");

        Assert.True(tempData.GetTempData(http).ContainsKey(LoginEmailSender.NotSentKey));
    }

    private static int UnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();   // nobody listens there now → "connection refused", like Mailpit stopped
        return port;
    }

    private class NoTempDataStore : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    /// Just enough of SMTP on localhost for SmtpClient: answers every command with OK and keeps what it got.
    private sealed class FakeSmtp : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        public int Port { get; }
        public Task<string> Received { get; }

        public FakeSmtp()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Received = ServeOneMail();
        }

        private async Task<string> ServeOneMail()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            var got = new StringBuilder();
            await writer.WriteLineAsync("220 fake smtp");
            while (await reader.ReadLineAsync() is { } line)
            {
                got.AppendLine(line);
                if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 go on");
                    while (await reader.ReadLineAsync() is { } data && data != ".") got.AppendLine(data);
                }
                if (line == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
                await writer.WriteLineAsync("250 ok");
            }
            return got.ToString();
        }

        public void Dispose() => _listener.Stop();
    }
}
