# Changelog

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
