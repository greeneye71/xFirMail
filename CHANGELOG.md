# Changelog

## 1.0.2 — 2026-10-20

- Corretti anche i rilievi R05-R11: limiti condivisi di elaborazione, annullamento da interfaccia e console, preparazione dei dati fuori dal thread UI e salvataggi con temporaneo e sostituzione finale. Le opzioni CLI errate terminano con codice 2 senza aprire finestre.
- Resi scorrevoli e adattabili all'area del monitor i dialoghi Informazioni e Condizioni d'uso, mantenendo separati i pulsanti. Aggiunte prove automatiche di layout, scorrimento e accettazione.
- Riutilizzati campi e QR durante la conversione e ridotta l'impaginazione ripetuta dei testi lunghi. Sul caso sintetico lungo: circa 53 ms contro 79 ms e 42 MB contro 72 MB allocati nella misura locale isolata; contenuti e numero di pagine invariati negli otto PDF di confronto.
- Generata automaticamente la versione del manifest Win32 dai metadati centrali. L'indisponibilità dei dati di audit NuGet ora blocca CI e packaging; aggiunte verifiche della generazione del manifest e della fonte di audit irraggiungibile.
- Aggiornati README e report di revisione con comportamenti, soglie e risultati: 102 test superati complessivamente. Sono compilati soltanto i componenti di test; nessun nuovo eseguibile di distribuzione, publish o MSIX. La generazione della nuova build resta in attesa della richiesta dell'utente.
- Corretti i rilievi R01-R04 del report: dati XML non rappresentati e duplicati conservati in appendice, gestione completa delle eccedenze ADR, collegamento al trasportatore risolto senza associazioni di ripiego e verifica del namespace negli eventi di annullamento. Aggiunti 24 casi di regressione; 68 test superati complessivamente. La build di distribuzione è rinviata fino alla risoluzione degli altri rilievi.
- Corretti gli avvisi CVE-2026-70162 e CVE-2026-84008 aggiornando la dipendenza Pkcs a 8.0.2 e richiedendo il runtime .NET 10.0.13, anche nei pacchetti con runtime incluso. Il packaging verifica i componenti effettivamente distribuiti; le vulnerabilità NuGet, anche transitive, bloccano il ripristino e la compilazione.
- Aggiunta la conversione senza interfaccia con `xFirW.exe --pdf <documento.xfir> [copia.pdf]`, con messaggi su console e codici di uscita per gli script. L'apertura normale e l'associazione `.xfir` mantengono l'anteprima.
- Il convertitore `Xfir.Cli` accetta anche il solo percorso XFIR: il PDF viene salvato accanto all'originale con lo stesso nome, senza sovrascrivere file esistenti. Lo stesso comportamento è disponibile nell'eseguibile grafico con `--pdf`.
- Il piè di pagina e i metadati dei PDF ricavano la versione dagli stessi metadati di compilazione usati dall'interfaccia: eliminato il numero fisso `0.1`, per mantenere automaticamente coerenti le versioni future.
- Aggiornata la versione dell'applicazione a 1.0.2 e del pacchetto Microsoft Store a 1.0.2.0.

## 1.0.1 — 2026-10-13

- Uniformata la versione a 1.0.1 nella barra inferiore, nella finestra Informazioni e nei metadati degli assembly. Il pacchetto Microsoft Store usa la versione 1.0.1.0.
- Lo script di packaging ricava la versione da quella dell'applicazione e verifica che i binari inclusi corrispondano alla versione del pacchetto, anche quando specificata esplicitamente.
- La finestra principale si apre massimizzata nell'area disponibile del monitor, lasciando visibile la barra delle applicazioni. Anche le dimensioni di ripristino e i limiti minimi vengono adattati prima dell'apertura, tenendo conto della scala DPI, per evitare che bordi e pulsanti standard finiscano fuori schermo sui display piccoli.
- Mantenuta una sola serie di controlli standard di Windows per minimizzare, ingrandire/ripristinare e chiudere la finestra.
- Corretto il controllo GitHub Actions sui documenti: l'esempio sintetico già presente nel repository è ammesso; gli altri formulari e PDF restano esclusi.

## 1.0.0

Prima versione inviata al Microsoft Store.

- Apertura locale dei formulari `.xfir` da finestra, trascinamento o associazione del tipo di file.
- Anteprima del formulario, esportazione PDF A4 e stampa.
- Riepilogo del formulario, dati originali, informazioni sulle firme ed elenco degli allegati.
- Icona dell'applicazione, finestra Informazioni, condizioni d'uso e informativa privacy.
- Pacchetto MSIX x64 con runtime .NET incluso.

La versione del pacchetto era 1.0.0.0; gli assembly e la versione mostrata nell'interfaccia riportavano ancora 0.1.3. Questa incoerenza è corretta dalla 1.0.1.
