# Deployment

Zwei Wege, je nach Server: Linux (nginx + systemd) oder Windows (IIS).
Beide setzen voraus, dass mindestens eine EF-Core-Migration existiert
(siehe Haupt-README, Abschnitt "Datenbank / Migrationen") — sonst ist
die Datenbank nach dem Start leer.

## Linux: nginx + systemd

1. Veröffentlichen (auf deiner Maschine oder direkt auf dem Server):
   ```bash
   dotnet publish -c Release -o out
   ```
2. Inhalt von `out/` auf den Server kopieren, z. B. nach `/var/www/helferapp`.
3. `helferapp.service` nach `/etc/systemd/system/helferapp.service` kopieren
   (Pfade/User bei Bedarf anpassen), dann:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now helferapp
   sudo systemctl status helferapp
   ```
4. `nginx-helferapp.conf` nach `/etc/nginx/sites-available/helferapp`
   kopieren, verlinken und TLS einrichten:
   ```bash
   sudo ln -s /etc/nginx/sites-available/helferapp /etc/nginx/sites-enabled/
   sudo nginx -t && sudo systemctl reload nginx
   sudo certbot --nginx -d helfer.example.com
   ```
5. Die App lauscht dann nur auf `127.0.0.1:5000` (siehe
   `appsettings.Production.json`) — nginx ist der einzige öffentliche
   Einstiegspunkt und terminiert TLS.

## Windows: IIS

1. ASP.NET Core Hosting Bundle auf dem Server installieren (falls noch
   nicht vorhanden).
2. `dotnet publish -c Release -o out` und den Inhalt von `out/` in ein
   IIS-Site-Verzeichnis kopieren. `web.config` liegt bereits als
   Referenz hier (wird von `dotnet publish` meist automatisch erzeugt).
3. In IIS eine neue Website/Application anlegen, die auf diesen Ordner
   zeigt, App Pool auf "No Managed Code" stellen (ASP.NET Core läuft
   eigenständig über das ANCM-Modul).
4. TLS-Zertifikat über die IIS-Bindungen einrichten.

## Allgemein

- `ASPNETCORE_ENVIRONMENT=Production` sorgt dafür, dass
  `appsettings.Production.json` greift (Kestrel bindet dann nur an
  `127.0.0.1`, HSTS ist aktiv).
- Die SQLite-Datei (`helferapp.db`) und der `Uploads/`-Ordner müssen für
  den Prozess-User beschreibbar sein.
- Für echten E-Mail-Versand `Email:Enabled=true` und die SMTP-Werte in
  `appsettings.Production.json` (oder per Umgebungsvariablen) setzen.
