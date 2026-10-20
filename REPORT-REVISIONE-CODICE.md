# Revisione del codice xFirW - proposta da approvare

Data: 20 ottobre 2026. Stato: **blocco A (R01-R04) approvato, corretto e verificato; blocchi B e C in attesa di approvazione**.

La revisione riguarda il working tree basato sul commit `5dc7d6a`, incluse le modifiche locali della versione 1.0.2. Sono stati esaminati lettore XFIR, modello, generatore PDF, applicazione WPF, CLI, test, script di packaging e workflow GitHub Actions. Non sono stati utilizzati formulari reali.

## Esito e decisione richiesta

Sono stati individuati **11 rilievi**: 3 a priorità alta, 5 media e 3 bassa. **Quattro rilievi sono ora risolti (R01-R04); sette restano aperti (R05-R11).** I principali problemi riguardavano la fedeltà del PDF ai dati originali. Non sono emerse nuove vulnerabilità con esecuzione di codice o sottrazione di dati dimostrate.

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

I rilievi dettagliati sotto descrivono la diagnosi iniziale; R01-R04 sono stati successivamente approvati e corretti. R05-R11 restano proposte da approvare. Le correzioni delle due CVE, autorizzate prima di questo report, sono state completate separatamente.

## Esito delle correzioni approvate del blocco A

- **R01 risolto:** la copertura segue le specifiche occorrenze XML lette dal modulo; dati aggiuntivi, attributi, duplicati e testo misto sono conservati in appendice. Il tipo di trasporto viene ora stampato nel riquadro del trasportatore; `NumIscrSito` resta visibile in appendice.
- **R02 risolto:** i valori ADR lunghi vengono trasferiti in appendice e `[segue]` viene mostrato soltanto quando la prosecuzione è registrata.
- **R03 risolto:** il lettore risolve una sola volta il trasportatore principale. Riferimenti mancanti, vuoti o inesistenti non attivano il ripiego su un'altra anagrafica. Il documento preliminare senza tratta mantiene l'anagrafica dichiarata.
- **R04 risolto:** lo stato di annullamento richiede il nome XML completo nel namespace riconosciuto.

Verifica: **68 test superati, 0 falliti**, di cui 24 nuovi casi di regressione in `tests/Xfir.Tests/BlockARegressionTests.cs`. Controllati anche i PDF temporanei dei test, con lettura dei contenuti e verifica visiva di modulo e appendici.

Su richiesta dell'utente, per questa fase sono stati compilati soltanto i componenti necessari ai test: **nessuna compilazione dell'applicazione WPF, nessun publish, nessun nuovo MSIX o pacchetto di distribuzione**. Il commit di invio usa `[skip ci]` per evitare la build automatica su GitHub. I pacchetti locali della precedente fase di sicurezza non includono queste correzioni e non sono stati aggiornati.

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

Nella precedente fase di sicurezza era stato generato `artifacts/store-upload/xFirW-1.0.2.0-x64-store.msix`, con runtime 10.0.13, e aggiornata la cartella `artifacts/xFirW-1.0.2`. **Non sono stati inviati allo Store e non includono le successive correzioni del blocco A.** La distribuzione rimane rinviata fino alla risoluzione degli altri problemi.

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

La suite iniziale di 44 test copriva percorsi ZIP pericolosi, nomi duplicati, DTD/entità esterne, numero FIR incoerente, eventi aggiuntivi, note lunghe, CLI e versione PDF. I 24 casi aggiunti per il blocco A coprono ora anche le anomalie R01-R04, unità e quantità duplicate, namespace estranei, testo misto, riferimenti vuoti e documenti preliminari. Ogni ulteriore correzione proposta deve includere il corrispondente test di regressione.

Sono stati verificati nel codice anche controlli già presenti: niente estrazione automatica dei percorsi ZIP sul filesystem; limiti di decompressione; DTD vietati e resolver XML disabilitato; nessuna esecuzione automatica degli allegati; protezione dalla sovrascrittura nella CLI; restrizioni di navigazione e risorse HTTP/HTTPS nell'anteprima.

Non sono stati eseguiti un penetration test completo, fuzzing esteso, prove distruttive del filesystem, intercettazione del traffico WebView2, installazione su una macchina pulita o collaudo completo dell'interfaccia con DPI multipli. L'assenza di altri rilievi non garantisce l'assenza di altri difetti.

La mancata validazione XSD e la mancata verifica crittografica completa delle firme sono limiti già dichiarati del prodotto: non sono state trasformate in richieste implicite di nuove funzionalità. Il report distingue questi limiti dai problemi di rappresentazione dei dati dimostrati sopra.

## Piano proposto per l'approvazione

| Blocco | Contenuto | Obiettivo |
| --- | --- | --- |
| A - Correttezza | R01, R02, R03, R04 | Evitare omissioni e attribuzioni errate, con test di regressione |
| B - Robustezza | R05, R06, R07, R08 | Definire i limiti, proteggere i salvataggi e completare i collaudi CLI/UI |
| C - Manutenzione | R09, R10, R11 | Ottimizzazioni misurate, versione unica e audit di rilascio più rigoroso |

**Stato:** il blocco A è completato. Il prossimo gruppo da valutare è il blocco B; il blocco C comprende interventi di manutenzione e ottimizzazioni da misurare. Per istruzione dell'utente la build di distribuzione resta sospesa fino alla risoluzione di tutti i problemi.

Decisione dell'utente, da compilare dopo la lettura:

- [x] Approvo il blocco A (correzioni e test completati).
- [ ] Approvo il blocco B.
- [ ] Approvo il blocco C.
- [ ] Richiedo una revisione del report prima di autorizzare gli interventi.

L'approvazione può anche indicare singoli ID. Non autorizza automaticamente invio allo Store, pubblicazione o push del repository.
