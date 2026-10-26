// SPDX-License-Identifier: EUPL-1.2
using System.Text.Json;
using System.Text.Json.Serialization;
using MimeKit;

namespace Xfir.Mail;

public sealed class MailEndpoint
{
    public string Server { get; init; } = "";
    public int Port { get; init; }
    public string Account { get; init; } = "";
    public string Password { get; init; } = "";
}

public sealed class Settings
{
    public MailEndpoint Imap { get; init; } = new() { Port = 993 };
    public MailEndpoint Smtp { get; init; } = new() { Port = 465 };
    public string[] AllowedSenders { get; init; } = [];
    public int PollSeconds { get; init; } = 60;
    public string StatePath { get; init; } = "/data";
    public string WorkPath { get; init; } = "/work";
    public string ConverterPath { get; init; } = "/app/cli/Xfir.Cli";
    // Advanced: defaults work for a dedicated INBOX and a receiving server with the same name as IMAP.
    public string TrustedAuthservId { get; init; } = "";
    public bool RequireDmarc { get; init; } = true;
    public int DailyQuota { get; init; } = 100;
    public int RetentionDays { get; init; } = 30;
    public const int MaxMessageBytes = 24 * 1024 * 1024;
    public const int MaxAttachmentBytes = 16 * 1024 * 1024;
    public const int MaxAttachments = 3;
    public const int MaxAttempts = 3;

    public static Settings Load(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new ConfigurationException("File configurazione troppo grande.");
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) &
                (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                 UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new ConfigurationException("appsettings.json deve essere accessibile soltanto al proprietario (chmod 600).");
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(path), new JsonSerializerOptions
            { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false })
                ?? throw new ConfigurationException("Configurazione vuota.");
            value.Validate();
            return value;
        }
        catch (JsonException) { throw new ConfigurationException("appsettings.json non valido: controllare sintassi e nomi delle opzioni."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new ConfigurationException("appsettings.json non leggibile: controllare percorso e permessi."); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        { throw new ConfigurationException("Configurazione non valida: controllare server, indirizzi e percorsi."); }
    }

    public void Validate()
    {
        CheckEndpoint(Imap, "Imap"); CheckEndpoint(Smtp, "Smtp");
        if (!IsAddress(Smtp.Account)) throw new ConfigurationException("Smtp.Account deve essere l'indirizzo della casella del servizio.");
        if (AllowedSenders is null || AllowedSenders.Length is < 1 or > 100 || AllowedSenders.Any(x => x != "*" && !IsAddress(x)))
            throw new ConfigurationException("AllowedSenders richiede indirizzi completi oppure * per accettare tutti i mittenti.");
        if (AllowedSenders.Contains(Smtp.Account, StringComparer.OrdinalIgnoreCase))
            throw new ConfigurationException("La casella del servizio non può essere un mittente autorizzato.");
        if (PollSeconds is < 5 or > 3600 || DailyQuota is < 1 or > 10000 || RetentionDays is < 1 or > 365)
            throw new ConfigurationException("Intervallo, quota o conservazione fuori limite.");
        if (TrustedAuthservId is null || (TrustedAuthservId.Length > 0 && Uri.CheckHostName(TrustedAuthservId) == UriHostNameType.Unknown))
            throw new ConfigurationException("TrustedAuthservId non valido.");
        if (string.IsNullOrWhiteSpace(StatePath) || string.IsNullOrWhiteSpace(WorkPath) || string.IsNullOrWhiteSpace(ConverterPath) ||
            !Path.IsPathFullyQualified(StatePath) || !Path.IsPathFullyQualified(WorkPath) || !Path.IsPathFullyQualified(ConverterPath))
            throw new ConfigurationException("I percorsi devono essere assoluti.");
        var state = Path.TrimEndingDirectorySeparator(Path.GetFullPath(StatePath));
        var work = Path.TrimEndingDirectorySeparator(Path.GetFullPath(WorkPath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (state.Equals(Path.GetPathRoot(state), comparison) || work.Equals(Path.GetPathRoot(work), comparison))
            throw new ConfigurationException("StatePath e WorkPath devono indicare directory dedicate, non la radice.");
        if (state.Equals(work, comparison) || state.StartsWith(work + Path.DirectorySeparatorChar, comparison) ||
            work.StartsWith(state + Path.DirectorySeparatorChar, comparison))
            throw new ConfigurationException("StatePath e WorkPath devono essere separati e non annidati.");
        if (!File.Exists(ConverterPath)) throw new ConfigurationException("ConverterPath: eseguibile non trovato.");
    }

    private static void CheckEndpoint(MailEndpoint? value, string name)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Server) || Uri.CheckHostName(value.Server) == UriHostNameType.Unknown ||
            value.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(value.Account) || string.IsNullOrWhiteSpace(value.Password) ||
            value.Account.Any(char.IsControl))
            throw new ConfigurationException($"{name}: specificare server, porta, account e password validi.");
    }

    public bool IsSenderAllowed(string? sender) => IsAddress(sender) &&
        !string.Equals(sender, Smtp.Account, StringComparison.OrdinalIgnoreCase) &&
        (AllowedSenders.Contains("*") || AllowedSenders.Contains(sender!, StringComparer.OrdinalIgnoreCase));

    public static bool IsAddress(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl) &&
        MailboxAddress.TryParse(value, out var address) && string.Equals(address.Address, value, StringComparison.Ordinal) && value.Contains('@');
}

public sealed class ConfigurationException(string message) : Exception(message);
