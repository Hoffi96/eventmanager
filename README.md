# Helfer-Tasks — ASP.NET Core Prototyp

Web-App zur Verwaltung von Aufgaben für Helfer:innen bei Veranstaltungen.

⚠️ **Hinweis:** Dieser Code wurde ohne lokal installiertes .NET SDK
geschrieben (Sandbox-Einschränkung) und konnte daher nicht mit
`dotnet build` gegengetestet werden. Bitte nach dem Öffnen einmal
`dotnet build` laufen lassen — falls etwas rot markiert ist, gerne
melden.

## Funktionen

- Registrierung/Login, E-Mail-Pflichtfeld, Rollenverwaltung (Admin/Helfer:in)
- Passwort ändern (eingeloggt) sowie "Passwort vergessen" per E-Mail-Link
- Veranstaltungen als eigene Entität, mit Zeitraum (Datum + Uhrzeit)
- Tasks sind einer Veranstaltung zugeordnet, haben einen eigenen Zeitraum
  und ein Admin-gesteuertes Personenlimit
- **Tasks von einer anderen Veranstaltung übernehmen** (Titel,
  Beschreibung, Personenlimit werden kopiert, Zeiten relativ zum neuen
  Veranstaltungsstart übertragen; Zuordnungen/Kommentare/Anhänge nicht)
- **WYSIWYG-Editor** (Quill, per CDN) für Task- und Veranstaltungs-
  Beschreibung, inkl. einfacher serverseitiger HTML-Bereinigung
- Veranstaltungsseite zeigt zuerst Beschreibung + Anhänge, danach die
  Tasks — allgemeingültige Infos sind so auf den ersten Blick sichtbar
- Selbst-Eintragung durch Helfer:innen oder zentrale Zuordnung durch Admin
- Warteliste: ist ein Task voll, kann man sich auf die Warteliste setzen;
  wird ein Platz frei, rückt automatisch die/der am längsten Wartende nach
- E-Mail-Benachrichtigung bei Zuordnung (auch bei Warteliste-Nachrücken)
  und bei neuen Kommentaren (an alle anderen zugeordneten Personen)
- Kommentare und Datei-Anhänge pro Task
- Listen- und Zeitplan-/Agenda-Ansicht (nach Tag gruppiert)
- Produktionsreif vorbereitet: EF-Core-Migrationen, Kestrel-Config für
  Reverse-Proxy-Betrieb, nginx/systemd/IIS-Vorlagen (siehe `deploy/`)

## Voraussetzungen

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Terminal: Projekt öffnen und starten

```bash
# 1. Ins Projektverzeichnis wechseln (Pfad ggf. anpassen)
cd HelferApp

# 2. Abhängigkeiten laden
dotnet restore

# 3. Lokales dotnet-ef-Tool installieren (einmalig, für Migrationen)
dotnet tool restore

# 4. Erste Migration erzeugen (einmalig, siehe Abschnitt unten)
dotnet ef migrations add InitialCreate

# 5. App starten
dotnet run
```

Danach im Browser: **http://localhost:5000**

Beim allerersten Start wird automatisch ein Admin-Konto angelegt:

- Benutzername: `admin`
- Passwort: `admin123`
- E-Mail: `admin@example.com` (bitte danach über "Passwort ändern"
  bzw. per DB ersetzen, siehe unten)

Zum Beenden im Terminal: `Strg + C`. Für einen neuen Start reicht danach
wieder `dotnet run`.

## Datenbank / Migrationen

Diese Version nutzt **EF-Core-Migrationen** statt `EnsureCreated()`, damit
das Schema bei künftigen Änderungen sauber versioniert und aktualisiert
werden kann. Das bedeutet: **vor dem ersten Start muss einmalig eine
Migration erzeugt werden** (Schritt 4 oben) — ohne sie legt die App keine
Tabellen an.

```bash
dotnet tool restore                        # einmalig: dotnet-ef lokal installieren
dotnet ef migrations add InitialCreate     # einmalig: erste Migration erzeugen
dotnet run                                 # wendet ausstehende Migrationen automatisch an
```

Bei künftigen Modelländerungen (z. B. neues Feld):

```bash
dotnet ef migrations add <Beschreibender-Name>
dotnet run   # oder: dotnet ef database update
```

**Falls du die App schon mit einer alten `helferapp.db`
(EnsureCreated-Variante) betrieben hast:** Diese Datei bitte löschen
(`rm helferapp.db`), da sie nicht zum neuen migrationsbasierten Schema
passt.

## E-Mail-Benachrichtigungen konfigurieren

Standardmäßig ist der Mail-Versand **deaktiviert** — E-Mails werden nur
in die Konsole geloggt, damit die App ohne Setup lauffähig bleibt. Für
echten Versand in `appsettings.json` (oder `appsettings.Production.json`)
den `Email`-Abschnitt ausfüllen:

```json
"Email": {
  "Enabled": true,
  "SmtpHost": "smtp.example.com",
  "SmtpPort": 587,
  "SmtpUser": "...",
  "SmtpPassword": "...",
  "EnableSsl": true,
  "FromAddress": "no-reply@example.com",
  "FromName": "Helfer-Tasks"
}
```

## Produktions-Deployment

Siehe [`deploy/README.md`](deploy/README.md) für nginx+systemd (Linux)
bzw. IIS (Windows) inklusive Beispiel-Konfigurationsdateien.

## Was ich als Nächstes erweitern würde

1. Bestätigungs-Mail bei Registrierung (E-Mail-Verifizierung)
2. Kalender-Export (ICS) für Tasks/Veranstaltungen
3. Mehrsprachigkeit (aktuell nur Deutsch)
4. Automatisierte Tests (aktuell keine, da hier nicht kompilierbar)

## Projektstruktur

```
HelferApp/
  Controllers/       Account, Tasks, Events, Admin
  Models/             User, Event, TaskItem, TaskAssignment,
                       WaitlistEntry, Comment, Attachment
  Data/               AppDbContext (EF Core)
  Migrations/         wird per "dotnet ef migrations add" erzeugt
  Services/           PasswordHelper, EmailOptions, IEmailService,
                       SmtpEmailService
  ViewModels/         TaskListItemVm, TaskDetailVm, ScheduleDayVm
  Views/              Razor-Views je Controller + Shared/_Layout.cshtml
  Uploads/             hochgeladene Anhänge (zur Laufzeit erzeugt)
  deploy/             nginx/systemd/IIS-Vorlagen + Anleitung
  Program.cs           Hosting, DI, Auth-Setup, Migrationen, Admin-Seed
```
