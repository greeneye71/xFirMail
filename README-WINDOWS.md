# xFirW

Visualizzatore di formulari digitali XFIR per Windows, con anteprima, esportazione PDF e stampa.

Versione corrente: **1.0.2** (pacchetto Microsoft Store **1.0.2.0**). Le modifiche dalla prima release Store sono riportate nel [changelog](CHANGELOG.md).

xFirW è un'applicazione C# / .NET 10 e WPF. Legge un XFIR locale, genera un PDF A4 e lo mostra nell'applicazione, con comandi per esportazione e stampa. Non è un validatore completo RENTRI.

Le funzioni implementate sono:

- Apertura di file `.xfir` da finestra, trascinamento o argomento della riga di comando.
- Lettura locale del contenitore ASiC-E e dei dati XML.
- Rappresentazione del formulario con impaginazione vicina al modello RENTRI.
- Anteprima, esportazione PDF e stampa dello stesso documento.
- Conversione PDF da riga di comando, senza aprire l'interfaccia, con nome di destinazione automatico o esplicito.
- Riepilogo del formulario, tabella dei dati originali, informazioni sulle firme ed elenco degli allegati.
- Controlli di struttura con indicazione esplicita dei limiti di validazione.
- Annullamento della conversione e limiti di elaborazione per contenitori o testi eccessivi.

Lo stack è C# con .NET 10 LTS e WPF, PDFsharp per il PDF, QRCoder per il QR e WebView2 per l'anteprima. Il pacchetto MSIX include l'associazione `.xfir`; la versione 1.0.0 è stata inviata al Microsoft Store e la 1.0.2 prepara il relativo aggiornamento.

## Avvio e compilazione

Sono necessari Windows, SDK .NET 10.0.402 o successivo della serie 10 per compilare, runtime .NET Desktop 10.0.13 o successivo e Microsoft Edge WebView2 Runtime per anteprima e stampa integrate. In assenza di WebView2 rimane possibile esportare il PDF. Il pacchetto Store include il runtime.

```powershell
dotnet restore xFirW.slnx --locked-mode -p:SecurityAuditRequired=true
dotnet build xFirW.slnx -c Release --no-restore -m:1
dotnet test tests/Xfir.Tests -c Release --no-restore -m:1
dotnet run --project src/Xfir.App -- "C:\documenti\esempio.xfir"
```

Per generare un PDF con l'eseguibile dell'applicazione, senza aprire finestre:

```powershell
.\xFirW.exe --pdf "C:\documenti\esempio.xfir"
.\xFirW.exe --pdf "C:\documenti\esempio.xfir" "C:\documenti\copia.pdf"
.\xFirW.exe --help
```

Con il solo file di origine viene creato `C:\documenti\esempio.pdf`, nella stessa cartella del file XFIR. Il secondo percorso permette di scegliere nome e cartella del PDF. I percorsi relativi sono risolti rispetto alla cartella corrente; racchiudi tra virgolette quelli che contengono spazi. I file esistenti non vengono sovrascritti: scegli un altro nome se il PDF è già presente.

L'opzione `--pdf` distingue la conversione dall'apertura nell'anteprima: `xFirW.exe "file.xfir"` continua ad aprire l'interfaccia, anche dall'associazione dei file. In modalità conversione non sono necessari WebView2 né interazioni con finestre.

Opzioni sconosciute e argomenti in eccesso producono un errore e il codice di uscita `2`, senza avviare finestre. Per un nome relativo che inizia con `-`, usa il prefisso `.\` o il percorso assoluto.

Il processo restituisce `0` in caso di successo, `1` per errori di lettura, salvataggio, superamento dei limiti o annullamento e `2` per argomenti non validi. Gli avvisi sul documento vengono scritti sul flusso di errore e non impediscono la creazione del PDF. Da una console collegata, `Ctrl+C` richiede l'annullamento; nell'interfaccia è disponibile **Annulla** durante la preparazione. Negli script PowerShell, per attendere l'eseguibile grafico e leggerne il codice di uscita:

```powershell
$processo = Start-Process -FilePath ".\xFirW.exe" -ArgumentList '--pdf "C:\documenti\esempio.xfir"' -Wait -PassThru -NoNewWindow
$processo.ExitCode
```

Dal sorgente è disponibile anche il convertitore console separato, con gli stessi comportamenti:

```powershell
dotnet run --project src/Xfir.Cli -- "C:\documenti\esempio.xfir"
dotnet run --project src/Xfir.Cli -- "C:\documenti\esempio.xfir" "C:\documenti\copia.pdf"
```

Per produrre una cartella eseguibile su PC con .NET Desktop Runtime 10.0.13 o successivo già installato:

```powershell
dotnet publish src/Xfir.App -c Release --no-restore -o artifacts/xFirW
```

La cartella deve essere distribuita per intero, inclusi licenza e avvisi di terzi. Il workflow GitHub Actions compila, esegue i test e prepara lo stesso pacchetto come artefatto.

### Verifiche e versione

La versione applicativa ha una sola origine in `Directory.Build.props`. Interfaccia, PDF e manifest Win32 generato usano questi metadati; il manifest sorgente è un modello con versione segnaposto `0.0.0.0`, non un numero da aggiornare a mano. Il packaging ricava e verifica anche la versione MSIX.

L'audit NuGet usa la fonte esplicita in `NuGet.Config`. Le vulnerabilità note bloccano sempre il ripristino. Con `SecurityAuditRequired=true`, anche l'indisponibilità o l'assenza dei dati di audit (`NU1900`, `NU1905`) sono errori: questa modalità è obbligatoria nella CI e nel packaging. Un ripristino locale offline senza tale proprietà può proseguire con un avviso e **non attesta un audit riuscito**.

Per collaudare il codice senza generare l'eseguibile dell'applicazione o un pacchetto:

```powershell
dotnet test tests/Xfir.Tests -c Release --no-restore -m:1
./scripts/Test-ManifestGeneration.ps1
./scripts/Test-SecurityAudit.ps1
```

I test compilano i componenti necessari, inclusi i sorgenti WPF modificati e un eseguibile ausiliario per verificare l'avvio CLI; non eseguono `publish` né creano MSIX. Il controllo del manifest genera soltanto XML con due versioni differenti. Il controllo dell'audit richiede dipendenze già ripristinate e simula una fonte irraggiungibile; salva i log in `tmp/`.

Le prove dei dialoghi coprono automaticamente le aree equivalenti a 1366×768 e 1920×1080 alle scale 100%, 150% e 200%, lo scorrimento e i comandi di accettazione. Rimane distinto il collaudo manuale su monitor fisici. Per ripetere la misura delle prestazioni isolata, imposta `XFIR_BENCHMARK_OUTPUT` a un percorso JSON e usa il filtro `--filter FullyQualifiedName~ProcessingBenchmarkTests`; i risultati misurano tempo e byte allocati, non il picco di memoria.

Lo stato delle correzioni e delle verifiche è nel [report di revisione](REPORT-REVISIONE-CODICE.md). Su richiesta dell'utente è stato generato il nuovo pacchetto Store **1.0.2.0 x64**, con runtime incluso e tutte le correzioni R01-R11. Il file locale per il caricamento è `artifacts/store-upload/xFirW-1.0.2.0-x64-store.msix`; hash e dettagli sono nel file `xFirW-1.0.2.0-x64-store.build-info.json` nella stessa cartella. Il pacchetto non è stato caricato in Partner Center. Le precedenti build non includono tutte le correzioni.

## Elaborazione e salvataggi

Lettura, preparazione dei dati originali e generazione PDF avvengono fuori dal thread dell'interfaccia. Un errore o un annullamento prima del completamento mantiene disponibile il documento precedentemente aperto. I dialoghi Informazioni e Condizioni d'uso hanno contenuto scorrevole e pulsanti separati dal testo; si adattano all'area utile del monitor.

Ogni operazione condivide questi limiti predefiniti tra lettura e generazione:

| Risorsa | Limite |
| --- | --- |
| File XFIR | 16 MiB |
| Voci del contenitore | 128 |
| Dimensione decompressa | 8 MiB per voce, 32 MiB complessivi |
| Caratteri XML | 4.194.304 per documento XML |
| Profondità XML | Livelli da 0 a 64, controllati durante la lettura |
| Nodi/eventi XML, inclusi attributi | 100.000 complessivi |
| Campi materializzati | 20.000 per operazione |
| Pagine PDF | 100, incluse le appendici |
| Tempo di elaborazione | 30 secondi, con controlli cooperativi |

Il tempo comprende lettura, preparazione e rendering, non l'attesa dell'anteprima WebView2 o il salvataggio finale. Il controllo cooperativo interrompe l'operazione al successivo punto di verifica; non è una terminazione forzata delle chiamate del sistema o delle librerie. Un limite superato produce un errore esplicito: non viene salvato un PDF incompleto. Le soglie proteggono da amplificazioni eccessive e possono rifiutare documenti legittimi particolarmente grandi.

L'esportazione scrive prima un temporaneo nella stessa cartella della destinazione, completa la scrittura e poi lo rinomina o sostituisce. Un errore prima della sostituzione conserva la copia precedente; la CLI continua a rifiutare le sovrascritture, anche se la destinazione viene creata contemporaneamente da un altro processo. Nell'interfaccia la sostituzione richiede la normale conferma del dialogo di salvataggio. Se il filesystem non supporta la sostituzione, il salvataggio fallisce senza usare una scrittura diretta distruttiva. Queste protezioni non costituiscono una garanzia contro guasti fisici del supporto.

## Limiti attuali

- Il modello principale copre un trasporto terrestre e la prima accettazione. Gli eventi aggiuntivi vengono riportati in un'appendice dati, con avviso di rappresentazione parziale.
- I testi troppo lunghi proseguono in appendice anziché essere eliminati.
- I codici EER sono formattati senza aggiungere l'asterisco di pericolosità: il repertorio EER completo non è ancora incluso. Le classi HP presenti restano visibili. Non vengono dedotte province o descrizioni estese delle autorizzazioni.
- Non sono eseguite validazione XSD, verifica completa XAdES, controllo delle revoche o interrogazioni RENTRI. Il QR viene riprodotto dai byte presenti, senza verificarne la firma COSE.
- Gli allegati PDF sono elencati ma non ancora apribili/esportabili dall'interfaccia, né inclusi nella stampa del formulario.
- La copia PDF non è firmata digitalmente e non sostituisce il file XFIR originale.
- xFirW non salva una cronologia dei documenti. I PDF temporanei sono eliminati alla chiusura regolare della sessione, quando non bloccati da altri processi.

L'apertura e la rappresentazione dei documenti sono progettate per funzionare localmente, senza richiedere un account RENTRI. La verifica completa delle firme e dello stato online del formulario è una funzionalità distinta dalla visualizzazione.

La proposta tecnica e le tappe di sviluppo sono descritte in [docs/PROGETTO.md](docs/PROGETTO.md).

Non pubblicare formulari reali, certificati privati, credenziali o documenti contenenti dati personali nelle issue o nelle pull request. I casi di test dovranno utilizzare dati sintetici o adeguatamente anonimizzati.

Progetto indipendente, non affiliato al RENTRI o a Microsoft.

## Licenza e uso commerciale

Copyright (C) 2026 Giovanni Bergamaschi e i contributori di xFirW.

Il codice e la documentazione originali del progetto sono distribuiti sotto la **Licenza pubblica dell'Unione europea, versione 1.2** (`EUPL-1.2`). Il testo è in [LICENSE](LICENSE) (inglese) e [LICENSE-IT.txt](LICENSE-IT.txt) (italiano); le versioni linguistiche ufficiali hanno pari valore. La licenza è regolata dalla legge italiana.

Il software è gratuito e fornito «così com'è», senza garanzie e con l'esclusione di responsabilità prevista dagli articoli 7 e 8 della licenza. xFirW è soltanto uno strumento di visualizzazione e stampa: non sostituisce il formulario XFIR originale né i sistemi ufficiali RENTRI, sui quali l'utente deve verificare i dati.

Il software è stato sviluppato con il supporto di strumenti di intelligenza artificiale generativa, sotto la supervisione dell'autore. xFirW non contiene né usa funzioni di intelligenza artificiale durante l'esecuzione.

L'uso, anche commerciale, è consentito senza royalty e senza autorizzazione. Chi distribuisce versioni modificate, o ne rende disponibili online le funzionalità essenziali, deve farlo sotto EUPL-1.2 o una licenza compatibile elencata nell'appendice e rendere disponibile il codice sorgente. La licenza non impone di pubblicare modifiche private né di inviarle a questo repository.

Se utilizzi xFirW in un progetto commerciale, ci farebbe piacere ricevere una [segnalazione volontaria](https://github.com/greeneye71/xFirW/issues/new?template=uso-commerciale.yml). È una richiesta di cortesia, non una condizione della licenza: non richiede approvazione e la mancata segnalazione non limita i diritti concessi. I dettagli sono in [COMMERCIAL_USE.md](COMMERCIAL_USE.md).

La licenza del programma non si applica automaticamente ai formulari degli utenti o ai dati e PDF generati da essi. Le dipendenze e gli eventuali materiali di terzi mantengono le rispettive licenze.

Per proporre modifiche, consulta [CONTRIBUTING.md](CONTRIBUTING.md).
