# xFirMail

Servizio in sviluppo per convertire formulari XFIR in PDF su Linux e restituirli via posta elettronica, distribuito in un container Podman su Fedora.

**Stato attuale:** implementati motore/CLI multipiattaforma, container Fedora rootless, worker IMAP/SMTP e registro persistente. Test disponibili su Windows; collaudo Linux/Fedora/Podman ancora da eseguire. Non è una release di produzione. Risultati e lavoro residuo in [STATO-SVILUPPO](docs/STATO-SVILUPPO.md).

## Uso e sviluppo

Un solo `appsettings.json` esterno, montato in sola lettura: copiare [l'esempio fittizio](appsettings.example.json), modificarlo con un editor, limitarne i permessi al proprietario e riavviare dopo le modifiche. Avvio Podman e regole della casella in [ESERCIZIO](docs/ESERCIZIO.md); controlli ancora necessari in [VERIFICA-FEDORA](docs/VERIFICA-FEDORA.md).

Con l'SDK di `global.json`:

```sh
dotnet restore xFirMail.slnx --locked-mode -p:SecurityAuditRequired=true
dotnet test tests/Xfir.Portable.Tests -c Release --no-restore
dotnet run --project src/Xfir.Cli -- esempio-sintetico.xfir output.pdf
dotnet run --project src/Xfir.Mail -- /percorso/appsettings.json
```

La suite usa documenti sintetici e server di posta locali su loopback. La soluzione `xFirMail.slnx` non richiede WPF; la soluzione storica Windows resta separata. Le dipendenze di produzione sono bloccate nei lock file e verificate dall'audit NuGet.

## Origine e separazione dei progetti

xFirMail deriva da [xFirW](https://github.com/greeneye71/xFirW), conservandone cronologia Git, attribuzioni e licenza EUPL-1.2. La base è il commit `7ed2208890656ef4df56237e3d52729e0ebaceb1`, successivo alle correzioni R01-R11 e alla preparazione del pacchetto Store Windows 1.0.2.0.

- **xFirW:** applicazione Windows esistente, mantenuta con correzioni di bug e vulnerabilità.
- **xFirMail:** nuova linea di sviluppo per conversione Linux, servizio IMAP/SMTP e distribuzione Podman.

Il repository è una derivazione indipendente nello stesso account GitHub, non un fork collegato nella rete dei fork GitHub. La copia locale usa `origin` per xFirMail e `upstream` per xFirW. Le future correzioni dell'origine saranno valutate e riportate singolarmente; non è prevista una sincronizzazione automatica che sovrascriva lo sviluppo.

## Percorso di sviluppo

1. Rendere portabili `Xfir.Core`, `Xfir.Rendering` e `Xfir.Cli`, separando impostazioni e test Windows e predisponendo font liberi adatti al PDF.
2. Preparare e verificare il convertitore in un container Podman su Fedora, senza privilegi.
3. Aggiungere il servizio di posta con casella IMAP dedicata, conversione in un processo separato e risposta SMTP al mittente autorizzato.
4. Verificare persistenza delle richieste, riavvii, tentativi di invio, limiti delle risorse e cancellazione dei documenti temporanei.

Criteri di accettazione sono nel [piano xFirMail](docs/PIANO-XFIRMAIL.md); implementazione e verifiche sono registrate separatamente nello stato sviluppo.

## Codice ereditato

La documentazione della base Windows è conservata in [README-WINDOWS.md](README-WINDOWS.md). Changelog, report di revisione e script Store descrivono quella base storica: non attestano funzionalità Linux o mail già disponibili.

Il workflow Windows ereditato resta manuale. Il workflow Linux compila ed esegue i test portabili e costruisce il container su avvio manuale; nessun workflow pubblica immagini o pacchetti automaticamente. La sua presenza non attesta un'esecuzione riuscita.

## Licenza e contributi

Copyright (C) 2026 Giovanni Bergamaschi e i contributori. Codice e documentazione originali sono distribuiti sotto EUPL-1.2: [LICENSE](LICENSE), [testo italiano](LICENSE-IT.txt). Sono conservati gli avvisi delle dipendenze in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Usare soltanto documenti sintetici o anonimizzati per prove e contributi. Non inserire credenziali IMAP/SMTP, formulari reali o PDF dei clienti nel repository. Il servizio conserverà i limiti dichiarati del convertitore: la copia PDF non sostituisce l'XFIR originale e non certifica le firme.
