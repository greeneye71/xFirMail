# xFirMail

Servizio in sviluppo per convertire formulari XFIR in PDF su Linux e restituirli via posta elettronica, distribuito in un container Podman su Fedora.

**Stato attuale:** repository separato e piano di sviluppo predisposti. I sorgenti ereditati sono ancora quelli della versione Windows; il porting Linux, il servizio IMAP/SMTP e il container non sono ancora implementati. Questa copia non costituisce una release Linux utilizzabile.

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

Criteri di accettazione e decisioni ancora da prendere sono nel [piano xFirMail](docs/PIANO-XFIRMAIL.md).

## Codice ereditato

La documentazione della base Windows è conservata in [README-WINDOWS.md](README-WINDOWS.md). Changelog, report di revisione e script Store descrivono quella base storica: non attestano funzionalità Linux o mail già disponibili.

Il workflow Windows ereditato è disponibile soltanto su avvio manuale e serve come controllo della base durante la migrazione. La CI Linux e la pubblicazione dell'immagine container saranno aggiunte con il porting; non vengono pubblicati pacchetti Windows automaticamente da questo repository.

## Licenza e contributi

Copyright (C) 2026 Giovanni Bergamaschi e i contributori. Codice e documentazione originali sono distribuiti sotto EUPL-1.2: [LICENSE](LICENSE), [testo italiano](LICENSE-IT.txt). Sono conservati gli avvisi delle dipendenze in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Usare soltanto documenti sintetici o anonimizzati per prove e contributi. Non inserire credenziali IMAP/SMTP, formulari reali o PDF dei clienti nel repository. Il servizio conserverà i limiti dichiarati del convertitore: la copia PDF non sostituisce l'XFIR originale e non certifica le firme.
