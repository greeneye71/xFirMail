# Componenti di terzi

Il codice originale di xFirW è distribuito sotto EUPL-1.2 (testo in `LICENSE`, versione italiana in `LICENSE-IT.txt`). Le dipendenze mantengono le loro licenze. I pacchetti sono identificati dai file di progetto e dai `packages.lock.json`.

| Componente | Versione iniziale | Licenza / termini |
| --- | --- | --- |
| PDFsharp | 6.2.4 | MIT, empira Software GmbH |
| QRCoder | 1.6.0 | MIT, Raffael Herrmann |
| Microsoft.Web.WebView2 | 1.0.3537.50 | Termini Microsoft del pacchetto NuGet e del runtime |
| .NET / WPF | 10 | Licenze e avvisi della distribuzione Microsoft .NET |
| Liberation Sans | 2.1.5 | SIL OFL-1.1, Google Corporation / Red Hat, Inc. |
| MailKit / MimeKit | 4.18.1 | MIT, .NET Foundation and Contributors / Jeffrey Stedfast |
| BouncyCastle.Cryptography | 2.7.0 | MIT, The Legion of the Bouncy Castle Inc. |
| System.Security.Cryptography.Pkcs | 10.0.13 (worker), 8.0.2 (rendering) | MIT, Microsoft / .NET Foundation |

La cartella `third-party` contiene le licenze e gli avvisi dei componenti distribuiti, incluse le dipendenze transitive Microsoft.Extensions.DependencyInjection.Abstractions e Microsoft.Extensions.Logging.Abstractions. Questa cartella viene copiata accanto all'eseguibile e deve essere mantenuta nelle distribuzioni. Il runtime WebView2 Evergreen è un prerequisito separato e non viene incluso nel pacchetto.

Il generatore PDF usa Liberation Sans 2.1.5 incorporato, senza modifiche, nei quattro stili. Provenienza: installazione locale del font libero, versione e copyright verificati nella tabella `name`; progetto ufficiale https://github.com/liberationfonts/liberation-fonts. Licenza completa e nomi riservati in `third-party/LiberationFonts-LICENSE.txt`; il font resta sotto OFL-1.1, separatamente dal codice EUPL. Il resolver Arial storico è conservato soltanto come riferimento. I formulari e i dati degli utenti non sono parte del codice del progetto.

MailKit gestisce IMAP/SMTP con TLS e autenticazione; MimeKit analizza e costruisce messaggi MIME e risultati di autenticazione; BouncyCastle è una dipendenza crittografica transitiva. Le licenze MIT permettono la distribuzione insieme al codice EUPL mantenendo gli avvisi inclusi in `third-party`. L'override Pkcs 10.0.13 del worker corregge le segnalazioni dell'audit sulla versione transitiva 10.0.0. I pacchetti di test non entrano nell'immagine runtime.

I test utilizzano xUnit, xunit.runner.visualstudio e Microsoft.NET.Test.Sdk; non sono componenti dell'applicazione distribuita.

Prima di pubblicare un pacchetto destinato allo Store vanno verificati i termini delle versioni effettive e inclusi tutti gli avvisi richiesti.
