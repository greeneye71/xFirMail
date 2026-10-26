# Collaudo da eseguire su Linux/Fedora

Questa procedura non attesta che il collaudo sia stato eseguito. Risultati effettivi in `STATO-SVILUPPO.md`. Usare soltanto dati sintetici e server locali di test.

1. Con SDK di `global.json`, eseguire `dotnet restore xFirMail.slnx --locked-mode -p:SecurityAuditRequired=true` e `dotnet test tests/Xfir.Portable.Tests -c Release --no-restore`. La suite avvia server IMAP/SMTP solo su loopback, genera certificati temporanei e fa una conversione reale nel processo figlio; non invia posta in Internet. Verificare anche permessi Unix, fsync e timeout.
2. `podman build -t localhost/xfirmail:dev -f Containerfile .`. Salvare versione Fedora/Podman, ID immagine e commit sorgente. Controllare che l'immagine non contenga `appsettings.json`, XFIR o PDF di prova. Non pubblicarla.
3. Conversione senza rete e filesystem radice in sola lettura:

```sh
mkdir -p output
podman run --rm --network=none --read-only --cap-drop=all \
  --security-opt=no-new-privileges --memory=512m --cpus=1 --pids-limit=64 \
  --userns=keep-id:uid=10001,gid=10001 --user 10001:10001 \
  --mount type=tmpfs,destination=/work,tmpfs-size=128m,tmpfs-mode=0700,U=true,noexec,nosuid,nodev \
  --tmpfs /tmp:rw,nosuid,nodev,size=32m \
  -v "$PWD/esempio-sintetico.xfir:/input/esempio.xfir:ro,Z" \
  -v "$PWD/output:/output:rw,Z" \
  --entrypoint /app/cli/Xfir.Cli localhost/xfirmail:dev \
  /input/esempio.xfir /output/esempio.pdf
```

4. Aprire il PDF: verificare entrambe le pagine A4, quantità italiane, fuso Europe/Rome, QR leggibile, dati e note non tagliati, avvertenza sulle firme. Confrontare con Windows con lo stesso documento; il font cambia le metriche. Provare anche supplementi/overflow generati dalla suite. Non sovrascrivere riferimenti senza controllarli.
5. Eseguire i comandi rootless di `ESERCIZIO.md` usando soltanto un server di posta di test sulla rete privata di collaudo, con CA fidata e autenticazione sintetica verificata. Nessun bypass TLS nel worker. Provare permessi 600/700, SELinux enforcing, avvio senza privilegi, assenza di listener, SIGTERM, riavvio e ripartenza del server Fedora.
6. Interrompere il container durante conversione, dopo creazione outbox, prima e dopo accettazione SMTP. Verificare pulizia `/work`, persistenza `/data`, retry limitati e stato Uncertain senza reinvio. I test automatizzati coprono questi stati tramite registro; l'arresto reale del container resta da provare.
7. Provare password errate, certificati non fidati, server indisponibile, allegati assenti/malformati/troppo grandi, MIME profondo, mittente non autorizzato, header di autenticazione falsificato, Reply-To esterno, messaggi automatici, stesso UID e duplicati, più allegati e quote. Non deve comparire alcun segreto nei log.
8. Verificare limiti cgroup con `podman stats --no-stream`, esaurimento memoria/spazio controllato, pulizia e scadenza registro, volume conservato al riavvio e backup/ripristino con servizio fermo. Usare un filesystem locale: la resistenza alla perdita di alimentazione dipende anche da filesystem e storage.
9. Verificare il flag letto sul solo originale con risposta `Sent`, anche dopo un aggiornamento da record privi di `ReadFlagApplied`. Simulare un errore IMAP nella marcatura: nuovi messaggi devono continuare a essere elaborati; al riavvio si riprova solo il flag, senza reinvio. Rimettere manualmente una richiesta conclusa come non letta non deve riattivarla. La suite copre questi casi e il comando IMAP `UID STORE ... +FLAGS.SILENT (\Seen)` su TLS.

Solo dopo questi controlli decidere un eventuale collegamento a una casella reale. L'autorizzazione allo sviluppo non comprende distribuzione su server reali, push o pubblicazione immagini.
