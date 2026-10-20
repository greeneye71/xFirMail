# Stato sviluppo xFirMail

Avvio autorizzato: 20 ottobre 2026, 18:30 Europe/Rome. Lavoro soltanto in questo repository; nessun push, pubblicazione o collegamento a caselle reali. Stato Git iniziale pulito. Nessun AGENTS.md applicabile trovato.

## Progressi

1. Motore/CLI: target net10.0 separati da WPF; font Liberation Sans 2.1.5 incorporati (OFL-1.1). Prima esecuzione: 84 test portabili superati su Windows.
2. Container: Containerfile multistadio SDK/Fedora 44, pubblicazione self-contained linux-x64, UID 10001, nessuna porta. Esecuzione reale ancora da verificare.
3. Worker: implementato con MailKit, TLS obbligatorio, controllo DMARC da authserv-id fidato, From autorizzato, blocco cicli e Reply-To ignorato. CLI figlia con timeout, limiti e pulizia; avvisi separati dalla risposta. Flusso reale IMAP → CLI figlia → SMTP superato con server TLS sintetici su loopback Windows.
4. Affidabilità: registro atomico e cursori per account/UIDVALIDITY/UID, outbox persistente, quote, recupero e invii incerti senza reinvio automatico. Test di riapertura del registro nei diversi stati, duplicati, retry limitati, pulizia, outbox mancante e limite tentativi dopo interruzione. Directory fsync implementata per Linux; arresto reale container/power loss da verificare.

## Ambiente e verifiche

Host Windows, runtime .NET 10.0.13. SDK 10.0.402 scaricato in .tools con autorizzazione sandbox, senza installazione globale; cache NuGet nel workspace. Build/test fuori sandbox approvati perché l'esecuzione limitata rimaneva bloccata. Podman assente. WSL elenca soltanto docker-desktop; CLI Docker presente ma daemon Linux non raggiungibile. Non sono state eseguite prove Linux/Fedora/Podman. Audit iniziale MailKit: Pkcs 10.0.0 vulnerabile; override 10.0.13, successivi ripristini con audit riusciti.

Verifiche concluse:

- Suite portabile finale: **120/120 superati** su Windows (.NET 10.0.13), con processo reale e server IMAP/SMTP TLS di test. Comprende recupero Received/Ready/Sending/Sent, mancato replay, retry e invii incerti, quote, limiti e configurazione senza segreti negli errori. Il ramo dei permessi Unix e fsync richiede esecuzione Linux: il passaggio della suite Windows non lo verifica.
- Suite Windows ereditata: 102/102, incluse prove startup e layout WPF; esclusivamente nella copia xFirMail.
- Pubblicazione self-contained linux-x64 di CLI e worker riuscita da Windows; host verificati ELF, runtime incluso 10.0.13, nessun appsettings nel publish. Questo non è un test di esecuzione Linux.
- PDF sintetico generato, PDF 1.7 e due pagine A4. Rasterizzazione PDFium e ispezione visiva delle due pagine; confronto Arial Windows/Liberation con identico testo estratto e numero di pagine. Nessun taglio visibile nel campione; overflow e supplementi coperti dai test di contenuto. Anteprime solo in artifacts (escluso Git).
- Configurazione reale, cache SDK/NuGet, dati e artefatti esclusi da Git. Un solo esempio fittizio. Licenze dei font e nuove dipendenze incluse; origine xFirW conservata.
- Ripristino finale di entrambe le soluzioni con `--locked-mode -p:SecurityAuditRequired=true`: riuscito, nessuna vulnerabilità segnalata dall'audit disponibile. Ricompilazione/test senza avvisi. Ripetuta pubblicazione linux-x64 di entrambi i programmi dopo le ultime modifiche: riuscita.
- `git diff --check` riuscito; unico XFIR tracciato: esempio sintetico preesistente. Hash SHA-256 dei quattro font verificati. Nessuna configurazione nei publish. Report locali: `artifacts/test-results/portable.trx` e `windows-baseline.trx` (esclusi Git).

## Decisioni

- Conservare progetti e attribuzioni Windows, aggiungere soluzione e suite portabili indipendenti.
- Credenziali soltanto nel file esterno (permessi proprietario); nessun segreto richiesto via ambiente o strumenti dedicati. Riavvio per applicare modifiche.
- Risposta al solo From autorizzato, mai a Reply-To. Richiedere autenticazione attestata da un server fidato; prevenire risposte automatiche e cicli.
- SMTP non offre invio esattamente una volta: dopo un esito ambiguo fermare il reinvio e registrare lo stato.

## Restante

Le quattro fasi hanno implementazione iniziale, test disponibili conclusi e documentazione: `ESERCIZIO.md`, `VERIFICA-FEDORA.md`, workflow Linux manuale senza pubblicazione. Nessuna esecuzione del workflow remoto. Modifiche lasciate locali, senza commit o push. Per riprendere: leggere questo stato e svolgere il collaudo Fedora; non occorre ricominciare il porting o il worker.

Da verificare nell'ambiente reale: build Containerfile e contesto di esclusione, Fedora 44, rootless Podman, mapping UID e SELinux, limiti cgroup, permessi Unix e fsync, SIGTERM/kill e reboot con volume persistente, disco pieno/OOM, confronto PDF nel container e conservazione della casella. Il processo figlio condivide utente e limiti del container: non è una sandbox OS indipendente. Richiesto un server ricevente che sanitizzi Authentication-Results e verifichi DMARC; compatibilità provider e OAuth non attestati (OAuth non implementato). Nessuna distribuzione reale, immagine pubblicata, push o email a terzi.
