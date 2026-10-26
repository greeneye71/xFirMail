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

Le quattro fasi hanno implementazione iniziale, test disponibili conclusi e documentazione: `ESERCIZIO.md`, `VERIFICA-FEDORA.md`, workflow Linux manuale senza pubblicazione. Nessuna esecuzione del workflow remoto. Alla chiusura della sessione del 20 ottobre le modifiche erano locali; successivamente pubblicate su richiesta dell'utente nel commit `b374ceabbe16c34ea51d71600b1c7b26204f4f05`. Il collaudo Fedora è iniziato il 21 ottobre, come registrato sotto; non occorre ricominciare il porting o il worker.

Alla chiusura del 20 ottobre erano ancora da verificare nell'ambiente reale: build Containerfile e contesto di esclusione, Fedora 44, rootless Podman, mapping UID e SELinux, limiti cgroup, permessi Unix e fsync, SIGTERM/kill e reboot con volume persistente, disco pieno/OOM, confronto PDF nel container e conservazione della casella. Il processo figlio condivide utente e limiti del container: non è una sandbox OS indipendente. Richiesto un server ricevente che sanitizzi Authentication-Results e verifichi DMARC; compatibilità provider e OAuth non attestati (OAuth non implementato).

## Installazione e collaudo del 21 ottobre 2026

Installazione autorizzata dall'utente su un server Fedora 44 x86-64, Podman 5.8.9 rootless, cgroup v2, SELinux enforcing. Sorgenti del commit `b374cea`, immagine costruita sul server senza pubblicazione, installazione privata in `~/xfirmail` dell'utente di servizio.

- La prima suite Linux ha evidenziato un solo test non portabile: presumeva che un lettore aperto con FileShare.Read impedisse la sostituzione del file anche su Unix. Corretto il test per verificare il blocco della sostituzione su Windows e, su Unix, la sostituzione atomica con il lettore ancora riferito al contenuto precedente. Nessuna modifica al motore o al comportamento applicativo.
- Dopo la correzione: **120/120 test superati su Linux** nel container SDK .NET 10.0.402 sul server; **6/6 test AtomicFileWriter superati su Windows**. Il test corretto è incluso nei sorgenti di collaudo sul server.
- Build effettiva del Containerfile riuscita. Immagine `localhost/xfirmail:b374cea`, ID `d9d3777e725b4f6264ca8d7620bdec23b6de014ec50598305242828622ea5aee`.
- Conversione del documento sintetico riuscita nel container Fedora con rete disabilitata, filesystem di radice in sola lettura, mapping UID, bind mount SELinux privati e limiti di risorse previsti. PDF 1.7 da 42.637 byte in `~/xfirmail/collaudo/esempio.pdf`; ispezione visiva di questo PDF remoto ancora da eseguire.
- Servizio utente systemd/Quadlet `xfirmail.service` registrato, con stato persistente separato e configurazione esterna. Nessuna porta esposta. La generazione del servizio è stata verificata; l'avvio è subordinato alla presenza di `~/xfirmail/appsettings.json`. Abilitato e verificato `Linger=yes` per l'utente del servizio, così l'avvio non dipende dal login interattivo; reboot effettivo ancora da collaudare.
- Casella reale non configurata: presente il solo esempio. Il worker non è stato avviato e non sono state inviate email reali. Resta da compilare il JSON sul server e verificare provider, autenticazione dei mittenti e flusso mail. Arresti forzati, reboot, disco pieno/OOM e ripristino operativo restano da collaudare.

I log dell'installazione sono conservati localmente in `tmp/server-install/`, escluso da Git. Le credenziali SSH e sudo sono state inserite dall'utente nel terminale e non salvate dall'installazione. La sessione di abilitazione del linger si è conclusa con codice 0 dopo la verifica del valore `yes`.

### Primo avvio con configurazione utente

L'utente ha compilato il JSON sul server. Il primo avvio ha evidenziato un errore nella definizione del servizio: Podman 5.8.9 rifiuta `uid=10001,gid=10001` nelle opzioni `--tmpfs`, terminando con codice 125. Sostituito il montaggio di `/work` con `--mount type=tmpfs,destination=/work,tmpfs-size=128m,tmpfs-mode=0700,U=true,noexec,nosuid,nodev`; verificati nel container UID/GID 10001, permessi 0700, scrittura e flag del mount. L'unità originale è stata salvata in un backup; JSON e credenziali non sono stati letti o modificati. Allineati anche il comando documentato e il collaudo container della CI.

Dopo la correzione il container parte, ma la validazione dell'applicazione termina con codice 2: `AllowedSenders` contiene la stessa casella di `Smtp.Account`. Occorre correggere l'elenco con un mittente distinto, indicato dall'utente, quindi azzerare il limite di riavvio con `systemctl --user reset-failed xfirmail.service` e avviare nuovamente il servizio. Il flusso con la posta reale non è ancora verificato.

### Supporto a tutti i mittenti e avvio del servizio

Su richiesta esplicita dell'utente, aggiunto `"AllowedSenders": ["*"]`. La stessa regola viene applicata alla ricezione e alle risposte nell'outbox, comprese quelle riprese dopo un riavvio. La casella `Smtp.Account` rimane sempre esclusa; autenticazione DMARC, verificatore fidato, blocco dei messaggi automatici, destinatario della risposta e quote restano verificati. L'elenco di indirizzi espliciti continua a essere supportato.

- **138/138 test superati su Windows e 138/138 su Linux**, con nuove prove per wildcard nel JSON, mittenti di altri domini, esclusione della casella del servizio, autenticazione fallita o ambigua, cicli di posta, outbox e flusso reale IMAP/SMTP TLS sintetico.
- Costruita e installata sul server l'immagine `localhost/xfirmail:b374cea-all-senders-20261021`, ID `5999bd4159b09e7827a15ef5e81ecf321b25d9d209db0f88bd4face9774fbad2`, da sorgenti locali derivati dal commit base con la modifica wildcard e le correzioni di collaudo. La label di revisione distingue questa build con il suffisso `+all-senders`; queste modifiche non sono ancora state committate o pubblicate su GitHub.
- Aggiornato sul server il solo campo `AllowedSenders` a `["*"]`, conservando tutte le altre impostazioni. Backup completo del JSON nella cartella privata del servizio, con permessi 0600; nessuna credenziale trasferita nei log o nella copia locale.
- Verificati montaggio temporaneo, accesso a stato/configurazione e avvio: **ActiveState=active, SubState=running, Result=success, NRestarts=0**, dopo 35 secondi. Il log conferma configurazione valida. Un messaggio presente in casella è stato scartato dal controllo `sender-or-loop`; il motivo specifico e un ciclo completo XFIR/PDF con la casella reale restano da verificare. Nessun invio di collaudo a terzi avviato dall'operatore.

Log locali in `tmp/server-install/all-senders.log`; la vecchia immagine e i backup dell'unità restano sul server per un eventuale ripristino.

### Diagnosi della mancata risposta alle prove reali

Il servizio riceve e legge le due prove reali (UID 1 e 2), ma le rifiuta prima della conversione: mittenti distinti dalla casella del servizio, consentiti dalla wildcard, nessun indicatore di ciclo nelle intestazioni esaminate e nessuna intestazione `Authentication-Results` su entrambi i messaggi. Il controllo DMARC obbligatorio è quindi la causa del rifiuto; non sono stati avviati tentativi SMTP per queste richieste. Diagnosi in sola lettura, senza scaricare allegati o mostrare indirizzi, oggetti e password; nessun registro o cursore modificato.

Implementata l'opzione `RequireDmarc` (predefinita `true`), con modalità `false` che conserva filtro mittenti, esclusione della casella del servizio, TLS, limiti e blocchi dei cicli, ma non autentica il campo From. **148/148 test superati su Windows e 148/148 su Linux**, compreso il flusso IMAP/SMTP sintetico senza `Authentication-Results`.

### Modalità semplice applicata e prova reale conclusa

Dopo l'approvazione esplicita dell'utente, installata l'immagine `localhost/xfirmail:b374cea-mail-compat-20261021`, ID `186bea0fbc8978636106239b064c4caf886101b0ecb8ee75d619fd5f8a7fd061`. Applicato sul server il solo campo `RequireDmarc=false`, conservando l'elenco wildcard già autorizzato e tutte le altre impostazioni. Backup privati di JSON, unità e due record precedenti in `~/xfirmail/backups/mail-compat-<data-ora>/`.

Con il servizio fermo, riprese soltanto le due richieste note precedentemente rifiutate con `sender-or-loop`, entrambe con zero tentativi SMTP. Conservati cursori, identità delle richieste e conteggi dei tentativi; nessuna cancellazione o nuova scansione indiscriminata della casella.

- **UID 2:** conversione riuscita, outbox preparata e risposta **Sent**, al primo tentativo SMTP. Il server SMTP ha accettato la risposta con PDF; il recapito nella posta del destinatario non è stato verificato dall'operatore.
- **UID 1:** rifiutato come `invalid-input`, prima di qualsiasi invio. Non è stato riaperto ulteriormente né sono stati esaminati i contenuti del messaggio per determinarne la causa specifica.
- Servizio **active/running**, `Result=success`, `NRestarts=0`. Montaggi di lavoro, stato e configurazione verificati. I controlli su messaggi automatici, mittente del servizio, quote e limiti rimangono attivi; la scelta `RequireDmarc=false` è specifica della configurazione di questa casella.

Log in `tmp/server-install/mail-compat.log`. Nessuna credenziale copiata localmente o stampata. Le modifiche sorgenti e la documentazione rimangono locali, ancora senza nuovi commit o push dopo il commit base.

### Nome originale degli allegati PDF

Su richiesta dell'utente, i PDF nella risposta conservano il nome base dell'allegato XFIR con estensione `.pdf`: `Formulario 2026.xfir` diventa `Formulario 2026.pdf`. Ogni allegato mantiene il proprio nome, inclusi spazi, accenti e punti; l'estensione XFIR è riconosciuta anche in maiuscolo. Sono rimossi eventuali componenti di percorso Windows/Unix e caratteri di controllo; per il solo nome vuoto si mantiene il ripiego `formulario-N.pdf`. Questo nome è usato soltanto nei metadati MIME: i percorsi temporanei del convertitore restano generati.

**154/154 test superati su Windows e 154/154 su Linux**, comprese la persistenza e rilettura della risposta MIME e la verifica del nome ricevuto dal server SMTP sintetico nelle tre modalità di configurazione già coperte.

Pubblicato nuovamente il worker self-contained e installata sul server l'immagine `localhost/xfirmail:b374cea-pdf-names-20261021`, ID `f11d6a3eae0444a95f3477baf0d35259d07d3f9cb506398e0e35403dc5ce7edc`. L'immagine deriva dalla precedente `mail-compat`, con la sola nuova pubblicazione del worker sovrapposta; base Fedora, motore CLI e dipendenze restano quelli già collaudati. Il normale Containerfile del repository rimane utilizzabile per una ricostruzione completa.

Riavvio verificato: **active/running, Result=success, NRestarts=0**, configurazione valida e montaggi accessibili. Nessuna modifica al JSON o alle richieste già elaborate, nessun reinvio delle prove precedenti. Il nuovo nome si applica ai PDF preparati dalle prossime richieste. Backup dell'unità in `~/xfirmail/backups/pdf-names-<data-ora>/`, log locale `tmp/server-install/pdf-names.log`. Modifiche locali ancora senza nuovi commit o push.

### Marcatura delle richieste concluse come lette

Confermato dall'utente il nome corretto dei PDF. Su sua richiesta, aggiunta la marcatura IMAP `\Seen` dell'originale dopo una risposta accettata da SMTP. Il registro persistente rimane la fonte per evitare rielaborazioni: il flag letto non decide quali richieste convertire o inviare. Nessuna cancellazione della posta e nessuna nuova impostazione nel JSON.

Lo stato `Sent` viene salvato prima della marcatura; un errore del flag non cambia l'esito SMTP e non causa reinvii. La marcatura viene ritentata nei cicli successivi e dopo riavvio; il campo persistente `ReadFlagApplied` evita di ripeterla dopo il successo. Record preesistenti privi del campo sono compatibili e vengono marcati solo se `Sent` e riferiti alla stessa identità IMAP, al massimo 50 per ciclo. Richieste rifiutate, fallite, pendenti o con esito incerto non vengono marcate dal servizio.

- **160/160 test superati su Windows e 160/160 su Linux**, comprese marcatura dopo SMTP, errori del flag senza bloccare nuove richieste, ripresa dopo riavvio senza riconversione/reinvio, compatibilità con il registro precedente e comando effettivo `UID STORE ... +FLAGS.SILENT (\Seen)` su IMAP TLS sintetico.
- Installata l'immagine `localhost/xfirmail:b374cea-mark-read-20261021`, ID `55bfc70ea2a40fda80122d192e61f1c14af05c97f2f0513a8d0648b893a51e83`, con worker aggiornato sopra la precedente immagine `pdf-names`. JSON e credenziali conservati; backup dell'unità in `~/xfirmail/backups/mark-read-<data-ora>/`.
- Servizio **active/running, Result=success, NRestarts=0**. Verificati sul provider, tramite lettura dei soli flag, gli originali UID 2 e 3: entrambi presenti, `ReadFlagApplied=true` e `\Seen` effettivo. Confrontato il registro prima/dopo aggiornamento: entrambe le richieste restano `Sent` con il conteggio dei tentativi SMTP invariato, quindi nessun reinvio delle richieste precedenti.

Log locale `tmp/server-install/mark-read.log`, aggiornamento concluso con codice 0. Nessun contenuto, oggetto, indirizzo o password mostrato dalla verifica dei flag. Modifiche sorgenti ancora locali, senza nuovi commit o push dopo il commit base.
