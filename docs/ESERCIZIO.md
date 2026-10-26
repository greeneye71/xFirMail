# xFirMail: avvio e gestione

Implementazione iniziale, da collaudare su Fedora/Podman prima dell'uso reale. Il PDF è una copia di consultazione: non sostituisce XFIR e non verifica firme o vidimazione. Nessuna porta in ingresso.

## Configurazione unica

Copiare l'unico esempio `appsettings.example.json` in `appsettings.json`, modificare server, porte, account/password e mittenti autorizzati. Gli indirizzi `.invalid` e le password dell'esempio sono fittizi. SMTP Account è anche il mittente della risposta e deve essere un indirizzo completo. IMAP usa INBOX dedicata. Porte 993/465: TLS diretto; altre porte (normalmente 143/587): STARTTLS obbligatorio. Certificati validati normalmente; nessuna modalità senza TLS.

Per accettare qualsiasi mittente, usare `"AllowedSenders": ["*"]`. Il valore `*` abilita tutti gli indirizzi; la casella del servizio rimane sempre esclusa. Restano attivi i controlli contro risposte automatiche e cicli di posta e le quote per mittente. L'autenticazione del messaggio è richiesta per impostazione predefinita, come descritto sotto. Per limitare il servizio a indirizzi specifici, mantenere l'elenco di indirizzi completi.

Il file reale è escluso da Git e dal contesto immagine. Proprietario: utente del servizio; `chmod 600 appsettings.json` e directory privata `chmod 700`. Il worker rifiuta configurazioni leggibili da gruppo/altri su Linux. Non incollare configurazioni reali in issue o log. Riavviare per applicare modifiche; nessun reload o altro meccanismo di gestione richiesto.

**Autenticazione dei mittenti:** il server ricevente deve verificare DMARC e aggiungere `Authentication-Results` con `dmarc=pass header.from=dominio`. Deve eliminare i risultati ricevuti dall'esterno che dichiarano il proprio authserv-id. Senza questa garanzia l'header è falsificabile: non collegare la casella reale. Il servizio accetta esattamente un risultato fidato e un From consentito da `AllowedSenders`, anche quando è configurato `*`. L'authserv-id predefinito è `Imap.Server`; solo se diverso, aggiungere `TrustedAuthservId` nello stesso JSON. È un identificatore del server verificatore, non un indirizzo mittente. Non si verifica DMARC autonomamente e non basta il campo From.

**Casella senza risultati DMARC:** l'opzione `"RequireDmarc": false` disattiva esplicitamente il controllo `Authentication-Results` dell'applicazione. In questa modalità il campo From non è autenticato dall'applicazione: gli indirizzi di `AllowedSenders` filtrano il valore dichiarato. Restano attivi TLS, esclusione della casella del servizio, controlli sui messaggi automatici, destinatario della risposta, limiti e quote. L'impostazione predefinita è `true`; omettere l'opzione conserva il controllo DMARC.

## Podman rootless su Fedora (x86-64)

Eseguire come utente dedicato, senza sudo. Dalla directory di questo repository:

```sh
podman build -t localhost/xfirmail:dev -f Containerfile .
mkdir -p data
chmod 700 data
chmod 600 appsettings.json
podman run -d --name xfirmail \
  --userns=keep-id:uid=10001,gid=10001 --user 10001:10001 \
  --read-only --cap-drop=all --security-opt=no-new-privileges \
  --memory=512m --cpus=1 --pids-limit=64 \
  --mount type=tmpfs,destination=/work,tmpfs-size=128m,tmpfs-mode=0700,U=true,noexec,nosuid,nodev \
  --tmpfs /tmp:rw,noexec,nosuid,nodev,size=32m,mode=1777 \
  -v "$PWD/appsettings.json:/config/appsettings.json:ro,Z" \
  -v "$PWD/data:/data:rw,Z" \
  --restart=on-failure:5 localhost/xfirmail:dev
podman logs xfirmail
# Dopo una modifica alla configurazione:
podman restart xfirmail
```

Il mapping keep-id rende leggibili i bind mount al solo UID del servizio. Per `/work`, `U=true` assegna il tmpfs all'utente del container, con permessi 0700; Podman 5.8 richiede questa sintassi e rifiuta `uid=`/`gid=` passati direttamente a `--tmpfs`. Vedere le [opzioni di montaggio Podman](https://docs.podman.io/en/v5.8.0/markdown/podman-run.1.html#mount-type-type-type-specific-option).

`:Z` applica etichette SELinux private; usare directory dedicate. `/data` deve essere un filesystem locale affidabile (es. ext4/XFS), con spazio monitorato, non NFS o una cartella condivisa tra istanze. Salvare il contenuto completo per il backup, con servizio fermo. Non cancellare cursori o registro per risolvere un errore: si rischia di rielaborare posta. Per l'avvio dopo reboot, abilitare il meccanismo di avvio dei container rootless dell'utente secondo la gestione locale (il solo `--restart` non configura il login automatico).

Build multistadio, runtime .NET 10.0.13 self-contained linux-x64, Fedora 44 con CA, ICU, fusi e locale italiano. Font e licenze sono nell'immagine. Non si pubblica alcuna immagine con questi comandi. Ricostruire periodicamente per aggiornamenti Fedora e runtime; mantenere il minimo di sicurezza del progetto. Non usare un'immagine prima del collaudo descritto in `VERIFICA-FEDORA.md`.

## Comportamento e conservazione

- Una conversione alla volta, processo figlio con argomenti separati, nessuna shell e timeout di 45 secondi. Il processo condivide i limiti e l'utente del container; non costituisce una sandbox indipendente. Il motore mantiene i propri limiti XML, archivio, pagine e durata (30 secondi).
- Da 1 a 3 allegati `.xfir`; gli altri allegati (immagini, logo della firma, documenti) sono ignorati e non contano nel limite, mentre una mail senza alcun `.xfir` viene respinta. Nessun riutilizzo del nome ricevuto nei percorsi. Messaggio MIME massimo 24 MiB, singolo XFIR decodificato 16 MiB, somma PDF 16 MiB. Un solo XFIR invalido respinge l'intera richiesta; nessun PDF parziale inviato. Avvertenze in `avvertenze.txt` separato dai PDF.
- Risposta esclusivamente al From autorizzato; Reply-To, CC e BCC ignorati. Oggetto fisso; ciascun PDF conserva il nome base del relativo XFIR, sostituendo l'estensione con `.pdf` (esempio: `Formulario 2026.xfir` → `Formulario 2026.pdf`). Eventuali percorsi e caratteri di controllo sono rimossi dal nome dell'allegato; se il nome base è vuoto si usa `formulario-N.pdf`. I percorsi temporanei rimangono generati dal servizio. Si scartano risposte automatiche, liste, notifiche e messaggi del servizio. Input respinti non producono una risposta automatica di errore; consultare il registro tramite ID.
- Quota predefinita 100 richieste al giorno UTC per mittente; duplicati MIME identici dello stesso mittente rilevati durante la conservazione del registro. Il Message-ID non è usato come identità fidata. Il registro usa server/account/INBOX/UIDVALIDITY/UID, indipendentemente dal flag letto. Cambio UIDVALIDITY: nuova scansione; richieste non preparate della vecchia identità diventano fallite.
- Stato JSON atomico con flush del file e fsync della directory su Linux. Cursore avanzato dopo la presa in carico persistente, outbox pronta salvata prima dell'invio. Una sola istanza può aprire il registro. Un registro corrotto non viene ricreato silenziosamente.
- Al massimo 3 tentativi di lettura/conversione o invio esplicitamente fallito, attesa 5 e 10 minuti. SMTP 4xx: riprovabile; 5xx: fallimento definitivo; disconnessione dopo l'avvio di Send o interruzione nello stato Sending: Uncertain, **nessun reinvio automatico**. Non si promette invio esattamente una volta. Prima di un eventuale reinvio manuale verificare il Message-ID deterministico nei log del server di posta; inviare una nuova richiesta solo dopo controllo dell'operatore.
- Originali e PDF temporanei eliminati dopo ogni conversione, residui ripuliti all'avvio. Outbox eliminata dopo esito definitivo o incerto; conservata solo per tentativi riprovabili. Registro terminale 30 giorni, cursori conservati; metadata Uncertain conservati fino a gestione dell'operatore. Indirizzo e hash sono dati personali: proteggere anche backup e volume. Uncertain non conserva documenti per una successiva spedizione.
- Dopo una risposta accettata da SMTP (`Sent`), il worker imposta il flag IMAP `\Seen` sul messaggio originale. INBOX viene aperta in lettura/scrittura; nessun messaggio viene cancellato. Richieste rifiutate, fallite, pendenti o con invio incerto non vengono marcate dal servizio. Se la marcatura fallisce, viene ritentata nei cicli successivi senza ripetere conversione o invio. Anche le richieste `Sent` ancora nel registro delle versioni precedenti vengono marcate, al massimo 50 per ciclo. La marcatura riuscita viene registrata: rimettere manualmente una mail come non letta non la rielabora e non forza una nuova marcatura.
- Lettura sospesa a 10.000 registrazioni o 256 MiB di outbox (possibile superamento di un singolo messaggio); i messaggi restano in casella. Elaborazione per finestre di 500 UID, massimo 50 messaggi per ciclo. Impostare sul server di posta una quota per casella e una cancellazione dopo almeno 30 giorni, controllando prima eventuali richieste pendenti. Riavvio e sostituzione del container conservano registro e cursori nel volume `/data`: non cancellare la directory persistente.
- I log contengono soltanto ID, stato e codici statici: nessun oggetto, contenuto, indirizzo, password, output del convertitore o eccezione del provider. Gli errori di configurazione indicano il campo, senza ripeterne i valori. Stato non leggibile, disco pieno o permessi errati richiedono intervento locale.

Solo se utili, nello stesso JSON: `DailyQuota` (1–10.000), `RetentionDays` (1–365), `TrustedAuthservId`. Intervallo normale `PollSeconds=60`, valido 5–3600. Limiti e timeout restano predefiniti. L'autenticazione prevista è account/password su TLS (anche password applicativa se richiesta); OAuth non è implementato. La compatibilità con un provider deve essere verificata prima dell'uso.
