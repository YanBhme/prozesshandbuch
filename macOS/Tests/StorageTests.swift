import Foundation
import Darwin

@main struct StorageTests {
    static func main() throws {
        var checks = 0
        func check(_ value: Bool, _ name: String) throws {
            guard value else { throw HandbookError("Test fehlgeschlagen: \(name)") }
            checks += 1; print("OK: \(name)")
        }
        func reject(_ name: String, _ action: () throws -> Void) throws {
            var rejected = false
            do { try action() } catch { rejected = true }
            try check(rejected, name)
        }
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("Prozesshandbuch-Tests-\(UUID().uuidString)")
        let local = FileManager.default.temporaryDirectory.appendingPathComponent("Prozesshandbuch-Kopie-\(UUID().uuidString).txt")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root); try? FileManager.default.removeItem(at: local) }
        let identity = "mac:test:501"
        var c = Catalog(); c.EditorSid = identity; c.EditorMacId = identity; c.EditorName = "Testkonto"
        c = try Storage.save(root, catalog: c, expected: nil, identity: identity)
        try check(c.Categories.count == 3 && c.entries.count == 12 && c.Processes.isEmpty, "Alle Kategorien ohne erfundene Prozesse")
        try check(try Storage.read(root) == c, "JSON-Roundtrip")
        try reject("Fremdes Konto darf nicht schreiben") { _ = try Storage.save(root, catalog: c, expected: c.Revision, identity: "mac:other:501") }
        let stale = c
        var p = Procedure(); p.Title = "Entscheidung"; p.Department = "WEG-Verwaltung"; p.Topic = Category.defaults[0].Subcategories[0]
        var s1 = Step(); s1.Id = "1"; s1.Title = "Freigegeben?"; s1.Next = "2"; s1.Otherwise = "3"
        var s2 = Step(); s2.Id = "2"; s2.Title = "Ausführen"
        var s3 = Step(); s3.Id = "3"; s3.Title = "Rückfrage"; s3.Next = "1"
        p.Steps = [s1, s2, s3]; c.Processes.append(p)
        c = try Storage.save(root, catalog: c, expected: c.Revision, identity: identity)
        try check(c.entries.count == 12, "Veröffentlichter Prozess ersetzt Platzhalter")
        try check(c.Revision != stale.Revision, "Neue Versionskennung")
        try check(try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Sicherungen").path).count == 1, "Sicherung angelegt")
        try reject("Veralteten Stand abweisen") { _ = try Storage.save(root, catalog: stale, expected: stale.Revision, identity: identity) }
        var invalid = c; invalid.Processes[0].Steps[0].Next = "fehlt"
        try reject("Ungültige Ziele abweisen") { try Storage.validate(invalid) }
        invalid = c; invalid.Processes[0].Steps[1].Id = "1"
        try reject("Doppelte Schrittkennungen abweisen") { try Storage.validate(invalid) }
        invalid = c; invalid.Processes[0].Steps[0].Next = ""
        try reject("Entscheidung ohne Ja-Ziel abweisen") { try Storage.validate(invalid) }
        for path in ["../a.txt", "..\\a.txt", "/a.txt", "x:stream.txt", "programm.exe"] {
            try reject("Unsicheren Dokumentpfad abweisen: \(path)") { try Storage.validateAttachment(Attachment(Name: "Test", File: path)) }
        }
        let lock = root.appendingPathComponent(Storage.lockName)
        try Data().write(to: lock)
        try reject("Plattformübergreifende Schreibsperre") { _ = try Storage.save(root, catalog: c, expected: c.Revision, identity: identity) }
        try FileManager.default.removeItem(at: lock)
        let source = root.appendingPathComponent("upload.txt"); try Data("Vorlageninhalt".utf8).write(to: source)
        let attachment = Attachment(Name: "Vorlage.txt", File: "document.txt"); c.Templates.append(attachment)
        c = try Storage.save(root, catalog: c, expected: c.Revision, identity: identity, pending: [attachment.File: source])
        try check(try String(contentsOf: Storage.document(root, attachment), encoding: .utf8) == "Vorlageninhalt", "Vorlage hochgeladen")
        try Data("Alter Inhalt".utf8).write(to: local)
        try Storage.copyDocument(root, attachment, to: local)
        try check(try String(contentsOf: local, encoding: .utf8) == "Vorlageninhalt", "Download ersetzt vollständige lokale Kopie")
        try reject("Download in Prozessordner abweisen") { try Storage.copyDocument(root, attachment, to: root.appendingPathComponent("falsch.txt")) }
        try reject("Fehlende Quelle überschreibt Download nicht") { try Storage.copyDocument(root, Attachment(Name: "Fehlt.txt", File: "fehlt.txt"), to: local) }
        try check(try String(contentsOf: local, encoding: .utf8) == "Vorlageninhalt", "Vorhandene Kopie bleibt bei Fehler erhalten")
        let before = try Storage.read(root)
        var missing = before; missing.Templates.append(Attachment(Name: "Fehlt.txt", File: "fehlt.txt"))
        try reject("Fehlenden Anhang nicht veröffentlichen") { _ = try Storage.save(root, catalog: missing, expected: before.Revision, identity: identity) }
        try check(try Storage.read(root) == before, "Fehlversuch verändert Handbuch nicht")
        let link = root.appendingPathComponent("Dokumente/link.txt")
        try FileManager.default.createSymbolicLink(at: link, withDestinationURL: source)
        try reject("Symlink aus Dokumentordner abweisen") { _ = try Storage.document(root, Attachment(Name: "Link.txt", File: "link.txt")) }
        var future = c; future.Schema = 4
        try reject("Unbekanntes Schema nicht schreiben") { try Storage.validate(future) }
        let legacyData = Data("{\"Schema\":2,\"Revision\":\"legacy\",\"EditorSid\":\"S-1-5-21-123\",\"Processes\":[]}".utf8)
        let legacy = try JSONDecoder().decode(Catalog.self, from: legacyData)
        try check(legacy.entries.count == 12 && !legacy.canEdit(identity), "Windows-Altformat bleibt lesbar, nicht schreibbar")
        try reject("Mac verändert altes Windows-Handbuch nicht") { _ = try Storage.save(root, catalog: legacy, expected: c.Revision, identity: identity) }
        var windows = c; windows.EditorSid = "S-1-5-21-123"; windows.EditorMacId = identity
        windows = try Storage.save(root, catalog: windows, expected: c.Revision, identity: identity)
        try check(windows.EditorSid == "S-1-5-21-123" && windows.canEdit(identity), "Windows-Freigabe und Mac-Bearbeiter erhalten")
        // A checked-in fixture is generated by the Windows serializer in CI.
        if CommandLine.arguments.count > 1 {
            let fixture = URL(fileURLWithPath: CommandLine.arguments[1])
            let loaded = try Storage.read(fixture)
            try check(loaded.EditorMacId == "mac:fixture:501" && loaded.Processes.count == 1 && loaded.Templates.count == 1, "Echte Windows-JSON-Datei gelesen")
        }
        print("\(checks) Speicherprüfungen bestanden.")
    }
}
