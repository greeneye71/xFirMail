# Piano di sviluppo xFirMail

Decisione del 20 ottobre 2026: sviluppare la nuova piattaforma in un repository autonomo, derivato da xFirW. La linea Windows originale riceverà soltanto bugfix e correzioni di sicurezza. Questo documento descrive lavoro previsto, non funzioni già implementate.

## Base di partenza

- Origine: https://github.com/greeneye71/xFirW.
- Commit di separazione: `7ed2208890656ef4df56237e3d52729e0ebaceb1`.
- Ultima versione Windows della base: 1.0.2 / MSIX 1.0.2.0.
- Tutti gli interventi R01-R11 sono presenti; 102 test superati sulla base Windows. Questi risultati non dimostrano ancora la compatibilità Linux.
- Cronologia, licenze e attribuzioni sono conservate. La copia non contiene artefatti locali, credenziali, screenshot o formulari reali non versionati.

## Fase 1: motore e CLI Linux

Separare il target .NET multipiattaforma dai progetti WPF e dalle impostazioni `win-x64`. Adattare risoluzione dei font, pacchetti e controlli del runtime a Linux; includere un font libero con gli avvisi richiesti. Separare i test portabili dai test grafici Windows.

Accettazione: conversione di documenti sintetici su Linux; verifica di quantità, date e fusi, QR, dati supplementari, eccedenze e versione PDF; controlli per input malformati, limiti, annullamento e salvataggi sicuri. Confrontare contenuti e impaginazione con la base Windows, tenendo conto delle metriche del font scelto.

## Fase 2: esecuzione Podman su Fedora

Preparare un Containerfile e un avvio documentato. Eseguire senza privilegi, predisporre font, certificati CA, dati di fuso orario e localizzazione richiesti. Separare area temporanea e stato persistente; verificare permessi e SELinux sul server Fedora.

Accettazione: conversione effettiva con Podman, limiti di memoria/CPU, timeout, pulizia dei temporanei e riavvio. Nessun documento o segreto nell'immagine. Nessuna porta in ingresso è richiesta dal solo flusso IMAP/SMTP.

## Fase 3: posta IMAP/SMTP

Aggiungere un worker dedicato. Usare connessioni cifrate e autenticazione compatibile con il provider; conservare credenziali o token fuori dall'immagine. Le impostazioni del provider saranno definite prima del collegamento a una casella reale.

Per ogni messaggio accettato: identificare gli allegati XFIR, applicare limiti, invocare la CLI in un processo isolabile e rispondere con il PDF al destinatario autorizzato. Iniziare con una conversione alla volta. Conservare separatamente eventuali avvisi del convertitore.

Accettazione: prove con server di posta di test per ricezione, conversione, risposta, credenziali errate, allegati assenti o non leggibili e indisponibilità temporanea della rete. Nessuna mail inviata a clienti durante i test.

## Fase 4: affidabilità e dati

- Registro persistente delle richieste e degli invii; identificazione robusta dei messaggi IMAP, non basata soltanto su letto/non letto.
- Tentativi limitati e gestione esplicita dell'esito incerto dopo un invio SMTP; non promettere invii esattamente una volta in presenza di arresti fra accettazione SMTP e registrazione dell'esito.
- Politica per più allegati, messaggi troppo grandi, quote per cliente e gestione degli errori.
- Controllo dei mittenti e dell'autenticazione verificata dal server di posta: il campo From da solo non prova l'identità. Regole esplicite per Reply-To e destinatari della risposta.
- Esclusione di risposte automatiche, notifiche di mancata consegna e messaggi prodotti dal servizio, per evitare cicli.
- Politica di conservazione per casella, originali e PDF; log privi di contenuti dei formulari e segreti.

Accettazione: riavvii nei diversi punti della lavorazione, invii SMTP falliti o incerti, messaggi duplicati, input oltre soglia, pulizia dei file e conservazione dello stato dopo il riavvio del container.

## Distribuzione e manutenzione

xFirMail produrrà un'immagine container Linux con versione e provenienza del codice documentate. Gli script MSIX ereditati restano riferimenti della base e non sono una distribuzione xFirMail. Non verrà inviata un'applicazione macOS ai clienti: l'accesso previsto è tramite posta.

Le correzioni di sicurezza o del PDF rilevanti per entrambe le linee saranno valutate singolarmente anche nell'altro repository, con test propri. Non eseguire merge automatici di tutte le nuove funzioni nella linea Windows.
