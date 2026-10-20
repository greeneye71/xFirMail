// SPDX-License-Identifier: EUPL-1.2
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Xfir.Tests;

/// <summary>Small protocol fixture, binds ONLY to loopback. No real accounts or network mail delivery.</summary>
internal sealed class LoopbackMailServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(30));
    private readonly Task serving;
    public X509Certificate2 Certificate { get; }
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public byte[] Input { get; init; } = [];
    public byte[]? Delivered { get; private set; }
    public int DataStatus { get; init; } = 250;
    public bool DropAfterData { get; init; }
    public bool RejectAuthentication { get; init; }
    public bool OfferTls { get; init; } = true;
    public string? EnvelopeRecipient { get; private set; }
    public string? ServerError { get; private set; }
    private readonly bool imap;
    public LoopbackMailServer(bool imap)
    {
        this.imap = imap;
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        // DefaultKeySet imports a temporary key compatible with Windows Schannel, removed on disposal.
        // No certificate store or trust store is modified. Test clients pin only this generated certificate.
        Certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12), null);
        listener.Start(); serving = Serve();
    }
    private async Task Serve()
    {
        try
        {
            using var socket = await listener.AcceptTcpClientAsync(stop.Token);
            Stream stream = socket.GetStream();
            StreamReader reader = null!; StreamWriter writer = null!;
            void Wrap()
            {
                reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
            }
            async Task Upgrade()
            {
                var tls = new SslStream(stream, leaveInnerStreamOpen: false);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                { ServerCertificate = Certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, stop.Token);
                stream = tls; Wrap();
            }
            Wrap();
            await writer.WriteLineAsync(imap ? "* OK synthetic IMAP server" : "220 localhost synthetic SMTP");
            var encrypted = false;
            string Capabilities() => "IMAP4rev1 " + (encrypted ? "AUTH=PLAIN SASL-IR" : OfferTls ? "STARTTLS LOGINDISABLED" : "LOGINDISABLED");
            while (await reader.ReadLineAsync(stop.Token) is { } line)
            {
                var tag = imap ? line.Split(' ')[0] : "";
                var command = imap ? line[(tag.Length + 1)..] : line;
                if (command.StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync(imap ? tag + " OK begin TLS" : "220 begin TLS");
                    await Upgrade(); encrypted = true; continue;
                }
                if (imap)
                {
                    if (command.StartsWith("CAPABILITY"))
                        await writer.WriteLineAsync("* CAPABILITY " + Capabilities() + "\r\n" + tag + " OK capability");
                    else if (command.StartsWith("AUTHENTICATE"))
                    {
                        if (command.Split(' ').Length < 3) { await writer.WriteLineAsync("+ "); await reader.ReadLineAsync(stop.Token); }
                        await writer.WriteLineAsync(tag + (RejectAuthentication ? " NO invalid credentials" : " OK authenticated"));
                    }
                    else if (command.StartsWith("LOGIN"))
                        await writer.WriteLineAsync(tag + (RejectAuthentication ? " NO invalid credentials" : " OK authenticated"));
                    else if (command.StartsWith("LIST"))
                        await writer.WriteLineAsync("* LIST () \"/\" \"INBOX\"\r\n" + tag + " OK list");
                    else if (command.StartsWith("EXAMINE") || command.StartsWith("SELECT"))
                        await writer.WriteLineAsync("* FLAGS (\\Seen)\r\n* 1 EXISTS\r\n* 0 RECENT\r\n* OK [UIDVALIDITY 123] valid\r\n* OK [UIDNEXT 2] next\r\n" + tag + " OK [READ-ONLY] opened");
                    else if (command.StartsWith("UID SEARCH"))
                        await writer.WriteLineAsync("* SEARCH 1\r\n" + tag + " OK search");
                    else if (command.StartsWith("UID FETCH") && command.Contains("BODY"))
                    {
                        await writer.WriteLineAsync($"* 1 FETCH (UID 1 BODY[] {{{Input.Length}}}");
                        await stream.WriteAsync(Input, stop.Token);
                        await writer.WriteLineAsync(")\r\n" + tag + " OK fetch");
                    }
                    else if (command.StartsWith("UID FETCH"))
                        await writer.WriteLineAsync($"* 1 FETCH (UID 1 RFC822.SIZE {Input.Length})\r\n{tag} OK fetch");
                    else if (command.StartsWith("LOGOUT")) { await writer.WriteLineAsync("* BYE\r\n" + tag + " OK logout"); break; }
                    else throw new InvalidOperationException("Unexpected synthetic IMAP command: " + command.Split(' ')[0]);
                }
                else
                {
                    if (command.StartsWith("EHLO"))
                        await writer.WriteLineAsync("250-localhost\r\n" + (!encrypted && OfferTls ? "250-STARTTLS\r\n" : "") + "250 AUTH PLAIN");
                    else if (command.StartsWith("AUTH PLAIN"))
                    {
                        if (command == "AUTH PLAIN") { await writer.WriteLineAsync("334 "); await reader.ReadLineAsync(stop.Token); }
                        await writer.WriteLineAsync(RejectAuthentication ? "535 invalid credentials" : "235 authenticated");
                    }
                    else if (command.StartsWith("MAIL FROM")) await writer.WriteLineAsync("250 sender OK");
                    else if (command.StartsWith("RCPT TO")) { EnvelopeRecipient = command; await writer.WriteLineAsync("250 recipient OK"); }
                    else if (command == "DATA")
                    {
                        await writer.WriteLineAsync("354 send data"); var data = new StringBuilder();
                        while (await reader.ReadLineAsync(stop.Token) is { } body && body != ".")
                            data.Append(body.StartsWith("..") ? body[1..] : body).Append("\r\n");
                        Delivered = Encoding.UTF8.GetBytes(data.ToString());
                        if (DropAfterData) break;
                        await writer.WriteLineAsync(DataStatus + " synthetic delivery result");
                    }
                    else if (command == "RSET") await writer.WriteLineAsync("250 reset");
                    else if (command == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
                    else throw new InvalidOperationException("Unexpected synthetic SMTP command: " + command.Split(' ')[0]);
                }
            }
            await stream.DisposeAsync();
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or AuthenticationException or ObjectDisposedException)
        { ServerError = e.ToString(); }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop(); await serving; Certificate.Dispose(); stop.Dispose();
    }
}
