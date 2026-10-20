# Revisione del codice xFirW - correzioni e verifiche

Data: 20 ottobre 2026. Stato: **blocchi A, B e C approvati e corretti (R01-R11)**. Verifiche automatiche completate; rimane distinto il collaudo manuale su monitor fisici. **Nuovo pacchetto Store 1.0.2.0 generato su successiva richiesta dell'utente**, non inviato a Partner Center.

La diagnosi iniziale riguardava il working tree basato sul commit `5dc7d6a`, incluse le modifiche locali della versione 1.0.2. Il blocco A è stato corretto nel commit `c75bacd`; i blocchi B e C sono stati autorizzati con la successiva richiesta di procedere analogamente. Sono stati esaminati lettore XFIR, modello, generatore PDF, applicazione WPF, CLI, test, script di packaging e workflow GitHub Actions. Non sono stati utilizzati formulari reali.

## Esito

Sono stati individuati **11 rilievi**: 3 a priorità alta, 5 media e 3 bassa. **Tutti gli interventi R01-R11 sono ora implementati**, con 102 test automatici superati e ulteriori verifiche di audit e versione. I principali problemi riguardavano la fedeltà del PDF ai dati originali. Non sono emerse nuove vulnerabilità con esecuzione di codice o sottrazione di dati dimostrate.

Le priorità indicano l'ordine di intervento nel progetto, non punteggi CVSS:

- **P1 / alta:** correggere prima della distribuzione della nuova versione, per evitare perdita o attribuzione errata dei dati.
- **P2 / media:** robustezza, disponibilità e comportamento con input anomali; distinguere i casi riprodotti da quelli ancora da verificare.
- **P3 / bassa:** manutenzione, prestazioni e rafforzamenti della procedura di rilascio.

| ID | Priorità | Rilievo | Evidenza |
| --- | --- | --- | --- |
| R01 | P1 | Campi e attributi possono scomparire dal PDF senza avviso | Tre casi sintetici riprodotti |
| R02 | P1 | Riga ADR troncata con `[segue]`, ma senza appendice | Riprodotto sul PDF |
| R03 | P1 | Un riferimento errato può mostrare il trasportatore sbagliato | Riprodotto sul PDF |
| R04 | P2 | Un elemento di namespace estraneo segnala un annullamento | Riprodotto tramite CLI |
| R05 | P2 | Mancano limiti di lavoro e annullamento per la conversione | Amplificazione misurata; DoS non dimostrato |
| R06 | P2 | Salvataggi PDF non atomici in caso di errore | Analisi del codice; guasto del disco non simulato |
| R07 | P2 | Un'opzione CLI errata può avviare l'interfaccia | Percorso confermato dal codice; GUI non avviata per la prova |
| R08 | P2 | Dialoghi senza scorrimento possono eccedere lo schermo | Rischio da verificare con prove DPI |
| R09 | P3 | Elaborazioni ripetute di campi, testo e QR | Analisi del codice; benefici da misurare |
| R10 | P3 | Versione Win32 ancora fissata a 1.0.1.0 | Verificata nel manifest |
| R11 | P3 | L'indisponibilità dell'audit NuGet non blocca il rilascio | Verificata la configurazione; indisponibilità non simulata |

La tabella e i rilievi dettagliati sotto conservano la diagnosi iniziale e le evidenze disponibili allora; lo stato attuale e le prove successive sono nelle sezioni di esito delle correzioni. Le correzioni delle due CVE, autorizzate prima di questo report, sono state completate separatamente. I riferimenti di riga della diagnosi iniziale sono storici.

## Esito delle correzioni approvate del blocco A

- **R01 risolto:** la copertura segue le specifiche occorrenze XML lette dal modulo; dati aggiuntivi, attributi, duplicati e testo misto sono conservati in appendice. Il tipo di trasporto viene ora stampato nel riquadro del trasportatore; `NumIscrSito` resta visibile in appendice.
- **R02 risolto:** i valori ADR lunghi vengono trasferiti in appendice e `[segue]` viene mostrato soltanto quando la prosecuzione è registrata.
- **R03 risolto:** il lettore risolve una sola volta il trasportatore principale. Riferimenti mancanti, vuoti o inesistenti non attivano il ripiego su un'altra anagrafica. Il documento preliminare senza tratta mantiene l'anagrafica dichiarata.
- **R04 risolto:** lo stato di annullamento richiede il nome XML completo nel namespace riconosciuto.

Verifica: **68 test superati, 0 falliti**, di cui 24 nuovi casi di regressione in `tests/Xfir.Tests/BlockARegressionTests.cs`. Controllati anche i PDF temporanei dei test, con lettura dei contenuti e verifica visiva di modulo e appendici.

Su richiesta dell'utente, per questa fase sono stati compilati soltanto i componenti necessari ai test: **nessuna compilazione dell'applicazione WPF, nessun publish, nessun nuovo MSIX o pacchetto di distribuzione**. Il commit di invio usa `[skip ci]` per evitare la build automatica su GitHub. I pacchetti locali della precedente fase di sicurezza non includono queste correzioni e non sono stati aggiornati.

## Esito delle correzioni approvate dei blocchi B e C

| ID | Correzione | Verifica |
| --- | --- | --- |
| R05 | Un budget condiviso tra lettura, preparazione e rendering controlla profondità prima della materializzazione XML, nodi, campi, pagine e durata. Preparazione sul worker; pulsante Annulla e token da console. | `ProcessingLimitsTests`: soglie, annullamento, scadenza, conservazione dei dati originali e rifiuto di un PDF eccessivo senza file parziale. |
| R06 | `AtomicFileWriter` scrive un temporaneo esclusivo nella stessa cartella, completa il flush e rinomina/sostituisce. Nessun ripiego sulla scrittura distruttiva della destinazione. | `AtomicFileWriterTests`: errore durante la scrittura, destinazione precedente, creazione concorrente, annullamento, file bloccato, temporaneo estraneo e sostituzione riuscita. |
| R07 | Instradamento esplicito degli argomenti: opzioni errate o argomenti in eccesso terminano con codice 2; zero argomenti o un percorso mantengono l'avvio grafico. | `StartupProcessTests`: processo ausiliario che esegue i veri sorgenti di avvio, con timeout; errori e help senza richiesta GUI, conversione effettiva con spazi nei percorsi. |
| R08 | Dialoghi ridimensionabili con contenuto scorrevole, comandi separati dal testo e vincoli aggiornati su posizione/DPI. | `DialogLayoutTests`: nove casi, comprese sei combinazioni di dimensioni/scala, testo lungo, cambio di area e proprietà dei comandi di accettazione. Verifica visiva delle immagini a 1366×768 / 200%. |
| R09 | Campi preparati una volta per l'interfaccia, cache limitata alla singola operazione, QR generato una sola volta e wrapping interrotto quando il riquadro è pieno. Nessuna cache globale dei documenti. | Benchmark isolato prima/dopo; cache verificata con operazioni distinte; otto PDF confrontati con quelli del blocco A, con stessi contenuti e numero di pagine. |
| R10 | Target MSBuild condiviso genera il manifest da `AssemblyVersion`, derivato dalla versione centrale; nessuna versione manuale nel modello. | `ManifestTests` legge la risorsa Win32 incorporata nell'eseguibile di prova, anche con versione alternativa 7.8.9. Lo script genera il manifest dell'applicazione con 1.0.2 e 7.8.9 senza compilarla. |
| R11 | Fonte audit esplicita; `NU1900` e `NU1905` bloccanti con `SecurityAuditRequired=true`, imposto da CI e packaging. | `Test-SecurityAudit.ps1`: fonte loopback irraggiungibile, ripristino locale con avviso e codice 0, ripristino di rilascio respinto con codice 1. Nessun packaging eseguito. |

I limiti predefiniti sono: profondità XML fino al livello 64 (radice a livello 0), 100.000 nodi/eventi XML e attributi complessivi, 20.000 campi materializzati, 100 pagine e 30 secondi di elaborazione. Restano i limiti precedenti di dimensione e decompressione. I controlli temporali e di annullamento sono cooperativi; non terminano forzatamente le chiamate di sistema o delle librerie. Una soglia superata produce un errore e non salva una copia incompleta. Le soglie possono rifiutare anche documenti legittimi molto grandi; sono riportate nel README.

Per R08 sono stati verificati automaticamente layout WPF e stili effettivi dell'applicazione nelle aree equivalenti a 1366×768 e 1920×1080, ciascuna al 100%, 150% e 200%, riservando spazio alla barra delle applicazioni. La prova del cambio monitor usa aree logiche, incluse coordinate negative. **Non equivale a un collaudo manuale su monitor fisici**, né dimostra il comportamento di ogni tema/accessibilità: il controllo hardware rimane una verifica da eseguire prima della distribuzione. Le proprietà di tastiera sono verificate; non è stata simulata una sessione completa di input utente.

### Prestazioni misurate (R09)

Stesso PC e SDK, configurazione Release, due esecuzioni di riscaldamento e mediana di sette misure; benchmark eseguito isolatamente dai restanti test. Ogni misura comprende lettura, preparazione delle righe e rendering, con un nuovo lettore/renderer. Il documento lungo contiene 3.500 ripetizioni di un'annotazione sintetica.

| Caso | Tempo prima | Tempo dopo | Byte allocati prima | Byte allocati dopo |
| --- | --- | --- | --- | --- |
| Sintetico normale | 7,23 ms | 7,02 ms | 2.705.800 | 2.710.632 |
| Annotazioni lunghe | 79,48 ms | 53,43 ms | 72.338.760 | 41.533.512 |

Sul caso lungo: circa **−33% di tempo e −43% di allocazioni**. Il caso normale rimane sostanzialmente invariato, con un lieve incremento delle allocazioni dovuto ai controlli e alle strutture di elaborazione. Si misurano byte allocati sul thread, **non il picco di RAM**; i tempi non sono una garanzia su altri PC. Dati locali: `tmp/benchmark-before.json` e `tmp/benchmark-after.json`, esclusi dal versionamento. Il test facoltativo è ripetibile impostando `XFIR_BENCHMARK_OUTPUT` come descritto nel README.

### Verifiche complessive e distribuzione

La suite corrente comprende **102 casi superati, 0 falliti**, inclusi i 34 aggiunti dopo il blocco A. La misura delle prestazioni è facoltativa nella suite ed è stata attivata anche separatamente. Sono stati compilati soltanto Core, Rendering e i progetti di test: i sorgenti WPF modificati sono collegati al progetto di test per verificarne la compilazione. L'eseguibile ausiliario serve esclusivamente a verificare avvio e risorsa manifest; non viene distribuito.

Il confronto degli otto PDF sintetici mantiene i testi estratti, normalizzata la data di generazione, e il numero di pagine. Controllate visivamente anche pagina principale e appendice ADR. Le immagini dei dialoghi e i PDF sono temporanei sotto `tmp/`, esclusi dal commit. README, changelog e questo report documentano comportamenti, soglie e limiti delle verifiche.

Ripristino finale dell'intera soluzione con `--locked-mode -p:SecurityAuditRequired=true` superato. Audit successivo di tutti e sei i progetti, incluse le dipendenze transitive: nessun pacchetto vulnerabile segnalato dalla fonte `https://data.nuget.org/v3/index.json` al momento della verifica. Questo esito è distinto dalla simulazione della fonte indisponibile.

**Durante la fase di correzione non sono stati generati eseguibili applicativi, publish o MSIX.** Il commit `6c407ca` usa `[skip ci]` per evitare la build automatica del workflow. La successiva richiesta dell'utente ha autorizzato la build descritta di seguito; i pacchetti precedenti non contengono queste correzioni.

### Build Store successivamente autorizzata

Il nuovo pacchetto **1.0.2.0 x64** include tutte le correzioni R01-R11 del commit `6c407ca` e il runtime .NET **10.0.13**. Lo script di packaging usa l'identità Store già presente nelle build precedenti e una cartella nuova per evitare binari residui. La copia finale è `artifacts/store-upload/xFirW-1.0.2.0-x64-store.msix`; il file affiancato `.build-info.json` registra hash, commit del codice e risultati. La vecchia copia con lo stesso nome è conservata nella sottocartella `previous/`.

Verifiche completate: ripristino con lock file e audit obbligatorio, 102 test superati, publish self-contained, controllo delle DLL di runtime, validazione MakeAppx, integrità del contenuto MSIX e corrispondenza dei file con il layout di pubblicazione. Verificate anche versione **1.0.2.0** nel manifest Win32 dell'eseguibile effettivo e **1.0.2** nei PDF prodotti. Le prove CLI dell'eseguibile incluso coprono conversione, percorsi con spazi, mancata sovrascrittura, file assente, help e opzioni errate.

Il pacchetto è privo di firma locale, destinato alla firma del Microsoft Store. **Non è stato caricato in Partner Center.** Non sono stati eseguiti Windows App Certification Kit, installazione su macchina pulita o collaudo manuale su monitor fisici; le verifiche automatiche non sostituiscono la certificazione Store.

## Correzioni di sicurezza già completate

Le segnalazioni CVE-2026-70162 e CVE-2026-84008 riguardavano `System.Security.Cryptography.Pkcs`, introdotto da PDFsharp. Sono stati effettuati questi interventi:

- riferimento esplicito a Pkcs **8.0.2**, con aggiornamento dei lock file;
- runtime minimo **10.0.13**, sia per l'applicazione con runtime installato sul PC sia per quella con runtime incluso;
- SDK minimo **10.0.402** in `global.json` e installazione locale dell'SDK aggiornato;
- audit NuGet di tutte le dipendenze, anche transitive, con errori bloccanti per vulnerabilità di gravità nota;
- controllo del runtime e delle DLL effettivamente incluse nel pacchetto, oltre al controllo NuGet;
- verifica dello stesso controllo nel workflow di compilazione.

Risultati verificati:

| Verifica | Esito |
| --- | --- |
| Ripristino completo con `--locked-mode` | Superato |
| Compilazione Release della soluzione | 0 errori, 0 avvisi |
| Test automatici esistenti | 44 superati, 0 falliti |
| Audit NuGet con dipendenze transitive | Nessuna vulnerabilità nota segnalata |
| Prova CLI su entrambi gli eseguibili | Superata, inclusi percorsi con spazi ed errori |
| Controllo sul vecchio runtime 10.0.12 | Rifiutato correttamente |
| Controllo sul nuovo runtime 10.0.13 | Accettato |
| Metadati di runtime aggiornati con DLL Pkcs vecchia | Rifiutati correttamente |
| Eseguibile del nuovo pacchetto, conversione senza finestre | Superata |
| Creazione e validazione MSIX con MakeAppx | Superate |

Nella precedente fase di sicurezza era stato generato `artifacts/store-upload/xFirW-1.0.2.0-x64-store.msix`, con runtime 10.0.13, e aggiornata la cartella `artifacts/xFirW-1.0.2`. **Questi vecchi output non sono stati inviati allo Store e non includono le successive correzioni dei blocchi A, B e C.** Il file Store è ora sostituito dalla build successivamente autorizzata, con la vecchia copia conservata in `previous/`; la cartella `artifacts/xFirW-1.0.2` rimane un output precedente.

Fonti Microsoft: [CVE-2026-70162](https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7), [CVE-2026-84008](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx).

## Rilievi dettagliati

### R01 - Perdita silenziosa di campi e attributi

**Priorità P1.** Riferimenti: `src/Xfir.Core/LayoutCoverage.cs:37`, `src/Xfir.Core/LayoutCoverage.cs:44`, `src/Xfir.Core/FormDocument.cs:10`, `src/Xfir.Rendering/FormPdfRenderer.cs:117`.

La lista dei campi considerati rappresentati è mantenuta separatamente dal codice che li stampa. Per esempio, `Trasportatori/Trasportatore/NumIscrSito` è escluso dall'appendice, ma il generatore non lo stampa. Inoltre, il rilevamento dei dati aggiuntivi considera solo elementi foglia, non gli attributi; la lettura di un campo singolo seleziona soltanto la prima occorrenza.

Tre prove sul campione sintetico hanno confermato la perdita senza avvisi e senza appendice:

1. aggiunta di `NumIscrSito = AUDIT_ISCR_SITO` al trasportatore visualizzato;
2. aggiunta di `datoExtra="AUDIT_ATTR_PERSO"` a `Rifiuto`;
3. aggiunta di una seconda `Denominazione` del produttore contenente `AUDIT_SECONDO_PRODUTTORE`.

Il terzo input è ambiguo e non viene presentato come conforme allo schema ufficiale: il difetto è l'accettazione silenziosa con perdita della seconda occorrenza. Non serve introdurre un validatore XSD completo per segnalare questa ambiguità.

**Proposta:** conservare in appendice tutti gli elementi e attributi non effettivamente rappresentati o usati per relazioni. Identificare le occorrenze, non soltanto i nomi dei percorsi. Rifiutare o segnalare i duplicati dei campi singoli. Ridurre la duplicazione tra la mappa di copertura e il renderer.

**Accettazione:** i tre valori di prova devono essere conservati nel PDF oppure il contenitore ambiguo deve essere rifiutato con un errore esplicito. Nessuna perdita silenziosa.

### R02 - Rimando a un'appendice inesistente nella riga ADR

**Priorità P1.** Riferimenti: `src/Xfir.Rendering/FormPdfRenderer.cs:141`, `src/Xfir.Rendering/FormPdfRenderer.cs:268`.

La riga composta da classe ADR e numero ONU chiama `Text` senza `overflowTitle`. Quando il contenuto eccede lo spazio, `Text` aggiunge `[segue]`, ma registra il testo completo soltanto se il titolo è presente. La copertura considera questi campi già rappresentati, quindi non interviene neppure l'appendice dei campi aggiuntivi.

**Prova:** classe ADR lunga con marcatore finale `AUDIT_CODA_ADR`. Il PDF contiene `[segue]`, ha soltanto due pagine e non contiene il marcatore; nessun avviso.

**Proposta:** registrare sempre le eccedenze dei dati dinamici. Mostrare `[segue]` soltanto quando esiste una prosecuzione effettiva. Verificare anche gli altri usi di `Text` privi di titolo.

**Accettazione:** il testo ADR deve essere completo nel modulo o nell'appendice; nessun rimando senza destinazione.

### R03 - Associazione di ripiego del trasportatore

**Priorità P1.** Riferimenti: `src/Xfir.Core/XfirReader.cs:146`, `src/Xfir.Core/XfirReader.cs:152`, `src/Xfir.Rendering/FormPdfRenderer.cs:114`.

Se `idRef` non corrisponde ad alcuna anagrafica, il lettore produce un avviso, ma sia il lettore sia il renderer ripiegano sulla prima anagrafica disponibile. Il formulario può quindi mostrare un soggetto che non è collegato alla tratta.

**Prova:** anagrafica con `id="1"`, trasporto modificato a `idRef="9"`. È presente l'avviso di riferimento mancante, ma nel riquadro del trasportatore compare comunque «Trasportatore dimostrativo».

**Proposta:** risolvere la relazione una sola volta nel modello. In presenza di un riferimento errato, mostrare il collegamento mancante e conservare le anagrafiche non associate in appendice. Valutare separatamente il caso legittimo in cui non esiste ancora una tratta.

**Accettazione:** un riferimento non risolto non deve attribuire la tratta a un altro soggetto. Aggiungere prove per riferimento valido, mancante e documento preliminare.

### R04 - Namespace ignorato nello stato di annullamento

**Priorità P2.** Riferimento: `src/Xfir.Core/FormDocument.cs:57`.

Lo stato cerca qualsiasi radice con `LocalName == "Annullamento"`, senza verificare il namespace. Un evento XML estraneo può quindi cambiare il riepilogo del documento.

**Prova:** aggiunta di `<Annullamento xmlns="urn:audit:foreign">` al contenitore e al manifest. `--inspect` restituisce «Nota di annullamento presente», benché il namespace non sia quello dei formulari riconosciuti.

**Proposta:** confrontare il nome XML completo con il namespace previsto. Conservare gli eventi sconosciuti come dati aggiuntivi, senza attribuire loro significati di stato.

**Accettazione:** solo l'evento riconosciuto deve modificare lo stato; l'omonimo in un namespace estraneo deve restare un dato aggiuntivo.

### R05 - Budget di elaborazione e annullamento mancanti

**Priorità P2, rischio di disponibilità da approfondire.** Riferimenti: `src/Xfir.Core/XfirReader.cs:176`, `src/Xfir.Core/XfirReader.cs:180`, `src/Xfir.Rendering/FormPdfRenderer.cs:287`, `src/Xfir.Rendering/FormPdfRenderer.cs:309`, `src/Xfir.App/MainWindow.xaml.cs:74`.

Sono già presenti limiti di dimensione, decompressione, numero di voci e caratteri XML. Mancano però limiti su nodi/campi, pagine generate e lavoro complessivo. La profondità viene verificata dopo la costruzione dell'albero XML. La conversione non espone un meccanismo di annullamento; parte della materializzazione dei dati avviene poi sul thread dell'interfaccia.

**Misura controllata:** un XFIR sintetico compresso di 6.088 byte, contenente circa 1,54 milioni di caratteri ripetitivi nelle annotazioni, ha prodotto **167 pagine** e un PDF di 284.977 byte in circa **2,37 secondi**, inclusa l'estrazione del testo usata dalla prova. È una dimostrazione di amplificazione, non di blocco del programma. Non sono stati eseguiti test di esaurimento della memoria o carichi estremi.

**Proposta:** definire soglie motivate per nodi, campi e pagine, controllo della profondità durante la lettura e annullamento delle operazioni lunghe. Preferire un errore esplicito alla produzione silenziosa di una copia incompleta. Misurare tempi e memoria prima di scegliere i limiti.

**Accettazione:** documenti entro soglia completati; documenti oltre soglia interrotti con messaggio chiaro; annullamento verificato e interfaccia utilizzabile.

### R06 - Salvataggio non atomico

**Priorità P2; dedotto dal codice, non riprodotto con guasti del disco.** Riferimenti: `src/Shared/ConversionCommand.cs:36`, `src/Xfir.App/MainWindow.xaml.cs:187`.

La CLI scrive direttamente nel nome definitivo: un errore durante la scrittura può lasciare un PDF parziale che impedisce un nuovo tentativo con lo stesso nome. Nell'interfaccia, `File.WriteAllBytes` può troncare una copia preesistente prima che il nuovo contenuto sia stato scritto integralmente.

**Proposta:** scrivere un temporaneo nella stessa cartella e completare il salvataggio con una rinomina/sostituzione atomica. Mantenere la protezione dalla sovrascrittura nella CLI e la conferma nell'interfaccia. Eliminare soltanto temporanei creati dalla specifica operazione.

**Accettazione:** simulare un errore di scrittura; nessun PDF finale parziale e nessuna perdita della copia precedente. Verificare anche la creazione concorrente della destinazione.

### R07 - Opzioni CLI errate instradate alla GUI

**Priorità P2; confermato dal flusso del codice.** Riferimenti: `src/Xfir.App/CommandLineStartup.cs:13`, `src/Xfir.App/App.xaml.cs:32`.

Il riconoscimento della modalità console accetta solo `--pdf`, `--help` e `-h`. Un errore come `--pfd` ricade nell'avvio grafico: il programma può mostrare finestre e trattare l'opzione come nome del file. Questo comportamento è problematico negli script automatici.

**Proposta:** analizzare gli argomenti all'avvio in modo univoco. Le opzioni sconosciute devono produrre errore su stderr e codice 2. Conservare l'apertura grafica con il solo percorso XFIR e l'avvio normale senza argomenti.

**Accettazione:** prove sul processo reale per opzione errata e argomenti in eccesso, con assenza di finestre e uscita tempestiva. Non è stata aperta intenzionalmente la GUI durante questa riproduzione.

### R08 - Dialoghi potenzialmente fuori schermo con DPI elevati

**Priorità P2, da confermare con collaudo grafico.** Riferimenti: `src/Xfir.App/DisclaimerWindow.xaml:3`, `src/Xfir.App/AboutWindow.xaml:3`.

Entrambi i dialoghi usano `SizeToContent="Height"`, non sono ridimensionabili e contengono testi lunghi senza area di scorrimento. L'adattamento all'area del monitor è presente nella finestra principale, ma non in questi dialoghi. Su display piccoli o a scala elevata i pulsanti potrebbero risultare fuori dall'area utile.

**Proposta:** limitare le dimensioni all'area del monitor, rendere scorrevole il contenuto e mantenere visibili i pulsanti.

**Accettazione:** prove manuali a 1366×768 e 1920×1080 con scale 100%, 150% e 200%, incluse tastiera e cambio monitor. Il difetto visivo non è stato dichiarato riprodotto: manca questo collaudo.

### R09 - Elaborazioni ripetute e duplicazioni da ridurre

**Priorità P3; nessun guadagno prestazionale ancora misurato.** Riferimenti: `src/Xfir.Core/FormDocument.cs:23`, `src/Xfir.Core/LayoutCoverage.cs:29`, `src/Xfir.Rendering/FormPdfRenderer.cs:221`, `src/Xfir.Rendering/FormPdfRenderer.cs:287`, `src/Xfir.App/MainWindow.xaml.cs:96`.

`XmlPart.Fields` ricostruisce liste e percorsi a ogni accesso. Il QR identico viene generato per entrambi i fogli principali. Il testo lungo viene impaginato per determinare l'eccedenza e nuovamente per l'appendice. Parte di queste operazioni può essere riutilizzata per la durata della singola conversione.

Le duplicazioni con conseguenze già dimostrate sono invece quelle della copertura e della selezione del trasportatore, trattate in R01 e R03. Il sorgente CLI condiviso tramite collegamenti di progetto non è una copia mantenuta separatamente e non richiede, da solo, un nuovo progetto o un refactoring.

**Proposta:** misurare prima i costi su documento normale e lungo; poi valutare cache limitate alla conversione, QR riutilizzabile e materializzazione dei dati fuori dal thread UI. Evitare cache globali dei documenti e refactoring estesi senza beneficio misurabile.

**Accettazione:** confronto di tempi e memoria prima/dopo, contenuti e impaginazione equivalenti, nessuna conservazione aggiuntiva dei dati dopo la chiusura.

### R10 - Versione duplicata nel manifest Win32

**Priorità P3.** Riferimento: `src/Xfir.App/app.manifest:3`.

Il manifest Win32 contiene ancora `assemblyIdentity version="1.0.1.0"`, mentre assembly, PDF e configurazione MSIX indicano 1.0.2 / 1.0.2.0. Non è la causa del vecchio numero nei PDF, già corretto, ma resta una seconda fonte manuale della versione.

**Proposta:** generare anche questa versione dai metadati centrali durante la compilazione, oppure documentare e verificare un'identità volutamente stabile. Non aggiornare soltanto il numero a mano, altrimenti il disallineamento può ripetersi.

**Accettazione:** verifica del manifest incorporato nell'eseguibile dopo una compilazione con versione diversa.

### R11 - Audit indisponibile non bloccante

**Priorità P3, rafforzamento della procedura di rilascio.** Riferimento: `Directory.Build.props:15`.

Le vulnerabilità note di gravità bassa, moderata, alta e critica sono ora bloccanti (`NU1901`-`NU1904`). Gli errori di recupero delle informazioni (`NU1900`) restano avvisi. Un audit indisponibile non equivale quindi a un audit pulito, ma la procedura non impone ancora questa distinzione per il rilascio. L'audit finale di questa sessione è riuscito e non ha segnalato vulnerabilità.

**Proposta:** rendere bloccante l'indisponibilità dell'audit nella CI e nel packaging di rilascio, con fonte di audit esplicita; valutare separatamente il comportamento delle compilazioni locali offline.

**Accettazione:** simulare una fonte di audit indisponibile e verificare che non venga prodotto un pacchetto di rilascio dichiarato verificato. [Documentazione Microsoft su NU1900](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1900).

## Metodo, prove e limiti

Le sei anomalie funzionali di R01-R04 sono state riprodotte modificando soltanto `esempio-sintetico.xfir`. Sono stati controllati il riepilogo JSON, gli avvisi, il numero di pagine e il testo estratto dai PDF. La prova di carico di R05 è stata mantenuta entro i limiti di lettura del programma.

I materiali locali delle prove sono in `tmp/audit-code-2026-10-20/risultati.json` e `tmp/audit_repro.py`; sono temporanei, esclusi dal versionamento e non sono test di regressione già aggiunti alla suite. I tempi sono indicativi di questo PC, non benchmark confrontabili tra macchine.

La suite iniziale di 44 test copriva percorsi ZIP pericolosi, nomi duplicati, DTD/entità esterne, numero FIR incoerente, eventi aggiuntivi, note lunghe, CLI e versione PDF. I 24 casi aggiunti per il blocco A coprono ora anche le anomalie R01-R04, unità e quantità duplicate, namespace estranei, testo misto, riferimenti vuoti e documenti preliminari. Le successive correzioni dei blocchi B e C hanno aggiunto altri 34 casi, descritti nella sezione di esito.

Sono stati verificati nel codice anche controlli già presenti: niente estrazione automatica dei percorsi ZIP sul filesystem; limiti di decompressione; DTD vietati e resolver XML disabilitato; nessuna esecuzione automatica degli allegati; protezione dalla sovrascrittura nella CLI; restrizioni di navigazione e risorse HTTP/HTTPS nell'anteprima.

Non sono stati eseguiti un penetration test completo, fuzzing esteso, prove distruttive del filesystem, intercettazione del traffico WebView2, installazione su una macchina pulita o collaudo completo dell'interfaccia con DPI multipli. L'assenza di altri rilievi non garantisce l'assenza di altri difetti.

La mancata validazione XSD e la mancata verifica crittografica completa delle firme sono limiti già dichiarati del prodotto: non sono state trasformate in richieste implicite di nuove funzionalità. Il report distingue questi limiti dai problemi di rappresentazione dei dati dimostrati sopra.

## Piano approvato e stato finale

| Blocco | Contenuto | Obiettivo |
| --- | --- | --- |
| A - Correttezza | R01, R02, R03, R04 | Evitare omissioni e attribuzioni errate, con test di regressione |
| B - Robustezza | R05, R06, R07, R08 | Definire i limiti, proteggere i salvataggi e completare i collaudi CLI/UI |
| C - Manutenzione | R09, R10, R11 | Ottimizzazioni misurate, versione unica e audit di rilascio più rigoroso |

**Stato:** tutti e tre i blocchi sono implementati e verificati automaticamente. La build Store è stata generata dopo l'autorizzazione successiva. Prima della distribuzione resta il collaudo manuale indicato per R08.

Decisioni dell'utente:

- [x] Approvo il blocco A (correzioni e test completati).
- [x] Approvo il blocco B (richiesta di procedere analogamente).
- [x] Approvo il blocco C (richiesta di procedere analogamente).
- [ ] Richiedo una revisione del report prima di autorizzare gli interventi.

L'invio su GitHub delle correzioni testate è autorizzato dalla richiesta di procedere come per il blocco A. La nuova build è stata poi richiesta esplicitamente. L'invio allo Store rimane un'azione separata, non eseguita.
