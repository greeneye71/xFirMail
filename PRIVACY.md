# Informativa sulla privacy di xFirW

Ultimo aggiornamento: 12 ottobre 2026

xFirW è un visualizzatore di formulari digitali XFIR che funziona interamente sul computer dell'utente. Questa informativa descrive quali dati l'applicazione tratta e dove li conserva.

## In sintesi

- xFirW **non raccoglie, non invia e non condivide** dati personali.
- Non richiede account, registrazione o accesso a Internet.
- Non contiene telemetria, statistiche d'uso, pubblicità o sistemi di tracciamento.

## Dati trattati

I file `.xfir` aperti dall'utente possono contenere dati personali (per esempio nominativi, codici fiscali, indirizzi, targhe e firme digitali). xFirW li legge esclusivamente in locale per mostrarli, esportarli in PDF e stamparli. Il file originale non viene mai modificato.

## Dati conservati sul computer

| Dato | Posizione | Durata |
|---|---|---|
| PDF temporanei dell'anteprima | `%LOCALAPPDATA%\xFirW\Preview` | Eliminati alla chiusura della finestra; eventuali residui di un arresto anomalo vengono rimossi al successivo avvio |
| Profilo del componente di anteprima (WebView2) | `%LOCALAPPDATA%\xFirW\WebView2` | Fino alla disinstallazione |
| Revisione accettata delle condizioni d'uso | Registro di sistema, `HKEY_CURRENT_USER\Software\xFirW` | Fino alla disinstallazione |
| PDF esportati | Cartella scelta dall'utente | Sotto il controllo dell'utente |

Nella versione distribuita tramite Microsoft Store, i dati dell'applicazione sono conservati nell'area riservata al pacchetto e vengono rimossi da Windows con la disinstallazione. I PDF esportati dall'utente non vengono rimossi.

La stampa invia il documento alla stampante selezionata dall'utente tramite il sistema di stampa di Windows.

## Connessioni di rete

xFirW non effettua connessioni di rete. L'anteprima integrata blocca il caricamento di qualsiasi risorsa esterna (`http` e `https`).

L'anteprima usa Microsoft Edge WebView2 Runtime, un componente di Windows fornito e aggiornato da Microsoft, che può raccogliere dati diagnostici secondo le impostazioni di Windows e l'[informativa sulla privacy di Microsoft](https://privacy.microsoft.com/). Lo stesso vale per il download e l'aggiornamento dell'applicazione tramite Microsoft Store. L'autore di xFirW non riceve questi dati.

## Diritti dell'utente

Poiché l'autore non riceve né conserva dati personali degli utenti, non vi sono dati da consultare, correggere o cancellare presso l'autore. L'utente può eliminare in qualsiasi momento i dati locali indicati sopra o disinstallare l'applicazione.

## Modifiche

Eventuali modifiche a questa informativa saranno pubblicate in questo file, nel repository <https://github.com/greeneye71/xFirW>.

## Contatti

Studio ing. Giovanni Bergamaschi — Parma, Italia
E-mail: <info@studiobergamaschi.net>
Sito: <https://www.studiobergamaschi.net>
