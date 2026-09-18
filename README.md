# Prozesshandbuch · Bruno Grüttner

Desktop-App für interne Anleitungen und Vorlagen. Native Oberflächen für **Windows** und **macOS**; gemeinsamer Datenordner auf einer SMB-Freigabe.

## Download und Installation
Die Workflows unter **Actions** erzeugen nach erfolgreichem Build:
- **Prozesshandbuch-Mac**: DMG und ZIP, macOS 13+, Intel und Apple Silicon. App in „Programme“ ziehen. Testversion mit Ad-hoc-Signatur, ohne Apple-Notarisierung.
- **Prozesshandbuch-Windows**: ZIP mit App und Installationsskript. `Installieren.cmd` ausführen.

Artifacts erfordern eine GitHub-Anmeldung. Keine internen Prozessdaten oder Dokumente in dieses öffentliche Repository laden.

## Funktionen
- Hauptmenü mit Anleitung und Vorlagen, Rücknavigation, Suche und Kategorien.
- Drei Kategorien und zwölf Unterkategorien; konkrete Prozessabläufe werden später hinterlegt.
- Prozesseditor mit Schritten, Zuständigkeiten, Checklisten, Verzweigungen und Anhängen.
- Vorlagen hochladen, öffnen und lokal speichern.
- Ein Bearbeiter mit freigegebenem Windows- und Mac-Konto; alle anderen lesen.
- Hintergrundaktualisierung, Revisionsprüfung, gemeinsame Schreibsperre und Sicherung vor Veröffentlichung.

Die tatsächliche Zugriffssicherung erfolgt über Rechte am Datenordner: Bearbeiter = Ändern, Mitarbeiter = Lesen. Die App verwaltet keine Serverkonten oder Passwörter.

## Gemeinsame Nutzung
Version 0.6 verwendet Datenformat 3. Ältere Handbücher sind lesbar. Für Mac-Bearbeitung eines Windows-Handbuchs die Mac-Kennung aus den Mac-Einstellungen unter Windows → Hauptmenü → Bearbeiterkonten eintragen. Beim Speichern wird auf Format 3 umgestellt; vorher Sicherung anlegen und alle Windows-Clients aktualisieren.

Für ein am Mac angelegtes Handbuch die Windows-Kennung aus der Windows-App in den Mac-Einstellungen freigeben. Nur der bereits hinterlegte Bearbeiter kann eine zusätzliche Kennung hinterlegen. Lesezugriff benötigt keine Freigabe in der App.

Anleitung: [Mac](macOS/LIESMICH-Mac.txt), [Windows](LIESMICH.txt).

## Selbst bauen
- macOS: `bash macOS/build.sh` (Xcode Command Line Tools). Universal-App, Speicherprüfungen, DMG und ZIP.
- Windows: `powershell -ExecutionPolicy Bypass -File build.ps1` (.NET Framework 4.x). App, Speicherprüfungen und ZIP.

Die macOS-Oberfläche nutzt SwiftUI/AppKit ohne externe Bibliotheken. Windows nutzt WinForms. Prozesse liegen als JSON vor, Anhänge separat unter `Dokumente`.

## Design
[Bearbeitbarer Figma-Entwurf](https://www.figma.com/design/TudbEvSk575cotEGA9KYLl) mit Hauptmenü, drei Kategorieansichten, Vorlagen und Bearbeitung.
Farben: Marineblau `#001743`, Orange `#E55411`. Das Mac-Signet wird als Vektorgeometrie gerendert; das App-Symbol wird in allen erforderlichen Größen erzeugt.

## Prüfung und Grenzen
Build- und Teststatus stehen in GitHub Actions. Ein erfolgreicher Build ersetzt keinen Test am Firmen-SMB-Server. Checklisten sind zum Nachlesen; individuelle Erledigungsstände werden nicht gespeichert. Anhanginhalte werden nicht volltextdurchsucht. Apple Developer-ID und Notarisierung sind noch nicht eingerichtet.

## Oberfläche 0.8.0
„Hauptmenü“ steht in Anleitung und Vorlagen oben. Anleitung zeigt den Inhalt direkt; die Umschaltung zwischen Ablauf, Dokumenten und Textansicht entfällt.


Version 0.8.0 – Checkliste Mieterwechsel
Unter Anleitung > Miethäuser > Checkliste Mieterwechsel stehen neun Abschnitte
mit 96 Prüfpunkten bereit. Die ursprünglich vorgeschlagenen Abschnitte 1, 6 und 9
sind nicht enthalten. Die verbleibenden Abschnitte sind von 1 bis 9 neu nummeriert.
Direkt in der Anleitung gibt es eine Gesamtcheckliste und neun einzelne, ausfüllbare PDFs.
Sie sind fest in der App enthalten und auch ohne Datenordner verfügbar.
Kopie speichern lädt eine persönliche Datei herunter. Öffnen erstellt ebenfalls
eine lokale Arbeitskopie. Häkchen ändern niemals die gemeinsame Originalvorlage.
In einem PDF-Programm mit Formularunterstützung ausfüllen und speichern oder
ausdrucken und handschriftlich abhaken.
Bereits selbst veröffentlichte Mieterwechsel-Anleitungen haben weiterhin Vorrang.

Version 0.8.0: Windows-Oberfläche an das Mac-Design angeglichen.
Das Logo wird ohne Bildhintergrund direkt gezeichnet. Checklisten lassen sich
nur in der zugehörigen Anleitung über „Kopie speichern“ herunterladen.
„Vorlagen“ enthält ausschließlich die veröffentlichten Formulare und Schreiben.
