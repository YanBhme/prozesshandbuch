import Foundation
import Darwin

struct HandbookError: LocalizedError {
    let message: String
    init(_ message: String) { self.message = message }
    var errorDescription: String? { message }
}
struct Attachment: Codable, Identifiable, Equatable {
    var Name: String
    var File: String
    var id: String { File }
}
struct Step: Codable, Identifiable, Equatable {
    var Id = UUID().uuidString
    var Title = ""
    var Owner = ""
    var Instructions = ""
    var Checklist = ""
    var Next = ""
    var Otherwise = ""
    var Documents: [Attachment] = []
    var id: String { Id }
}
struct Procedure: Codable, Identifiable, Equatable {
    var Id = UUID().uuidString
    var Title = ""
    var Department = ""
    var Topic = ""
    var Summary = ""
    var Updated = ""
    var Steps: [Step] = []
    var id: String { Id }
    var searchable: String { ([Title, Department, Topic, Summary] + Steps.flatMap { [$0.Title, $0.Owner, $0.Instructions, $0.Checklist] + $0.Documents.map(\.Name) }).joined(separator: " ") }
}
struct Category: Codable, Identifiable, Equatable {
    var Name: String
    var Description: String?
    var Subcategories: [String]
    var id: String { Name }
    static let defaults: [Category] = [
        Category(Name: "WEG-Verwaltung", Description: "Wohnungseigentümergemeinschaften", Subcategories: ["Checkliste Eigentümerwechsel", "Wohngeldabrechnungen / Wirtschaftspläne", "Anschreiben Jahresabrechnung", "Versammlungsnachbereitung (Eigentümerversammlung)", "Protokoll Verwalterbestellung beglaubigen lassen"]),
        Category(Name: "Miethäuser", Description: "Mietverwaltung", Subcategories: ["Checkliste Mieterwechsel"]),
        Category(Name: "Allgemeine Prozesse", Description: "Unternehmensweit", Subcategories: ["Organisation & Ablage", "Stammdaten & Formulare (SEPA, Kontaktdaten)", "Versicherungen & Schadensfälle", "Übersichtsliste laufende Abrechnungen", "Firmenpool", "Personal (Onboarding, Hauswarte → Lohnabrechnung)"])
    ]
}
struct Catalog: Codable, Equatable {
    var Schema = 3
    var Revision = UUID().uuidString
    var EditorSid = ""
    var EditorMacId: String? = nil
    var EditorName = ""
    var Processes: [Procedure] = []
    var Categories: [Category] = Category.defaults
    var Templates: [Attachment] = []
    enum CodingKeys: String, CodingKey { case Schema, Revision, EditorSid, EditorMacId, EditorName, Processes, Categories, Templates }
    init() {}
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        Schema = try c.decode(Int.self, forKey: .Schema)
        Revision = try c.decode(String.self, forKey: .Revision)
        EditorSid = try c.decode(String.self, forKey: .EditorSid)
        EditorMacId = try c.decodeIfPresent(String.self, forKey: .EditorMacId)
        EditorName = try c.decodeIfPresent(String.self, forKey: .EditorName) ?? ""
        Processes = try c.decode([Procedure].self, forKey: .Processes)
        Categories = try c.decodeIfPresent([Category].self, forKey: .Categories) ?? []
        Templates = try c.decodeIfPresent([Attachment].self, forKey: .Templates) ?? []
        for required in Category.defaults {
            if let i = Categories.firstIndex(where: { $0.Name == required.Name }) {
                for name in required.Subcategories where !Categories[i].Subcategories.contains(name) { Categories[i].Subcategories.append(name) }
            } else { Categories.append(required) }
        }
    }
    func canEdit(_ identity: String) -> Bool { Schema == 3 && (EditorMacId == identity || EditorSid == identity) }
    var entries: [Procedure] {
        var result: [Procedure] = [], seen = Set<String>()
        for (ci, category) in Categories.enumerated() {
            for (si, topic) in category.Subcategories.enumerated() {
                let matches = Processes.filter { $0.Department == category.Name && ($0.Topic == topic || $0.Title == topic) }
                if matches.isEmpty {
                    var p = Procedure(); p.Id = "section-\(ci)-\(si)"; p.Title = topic; p.Department = category.Name; p.Topic = topic
                    result.append(Builtins.procedure(category.Name, topic) ?? p)
                } else {
                    for p in matches where seen.insert(p.Id).inserted { result.append(p) }
                }
            }
        }
        result += Processes.filter { seen.insert($0.Id).inserted }
        return result
    }
}

enum Builtins {
    static var directoryOverride: URL? = nil
    static var directory: URL { directoryOverride ?? Bundle.main.resourceURL!.appendingPathComponent("Mieterwechsel") }
    static var templates: [Attachment] {
        guard let data = try? Data(contentsOf: directory.appendingPathComponent("templates.json")),
              let files = try? JSONDecoder().decode([Attachment].self, from: data) else { return [] }
        return files.filter { (try? Storage.validateAttachment($0)) != nil }
    }
    static func contains(_ file: String) -> Bool { templates.contains { $0.File == file } }
    static func procedure(_ department: String, _ topic: String) -> Procedure? {
        guard department == "Miethäuser", topic == "Checkliste Mieterwechsel",
              let data = try? Data(contentsOf: directory.appendingPathComponent("procedure.json")),
              let p = try? JSONDecoder().decode(Procedure.self, from: data) else { return nil }
        return p
    }
    static func templates(for catalog: Catalog) -> [Attachment] {
        var seen = Set<String>()
        return (templates + catalog.Templates).filter { seen.insert($0.File).inserted }
    }
}

enum Storage {
    static let allowed = Set(["pdf", "docx", "xlsx", "pptx", "txt", "csv", "png", "jpg", "jpeg"])
    static let lockName = ".veroeffentlichen.lock"
    static let fm = FileManager.default
    static func blank(_ text: String) -> Bool { text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    static func validateAttachment(_ a: Attachment) throws {
        guard !blank(a.Name), !blank(a.File), a.File != ".", a.File != "..",
              !a.File.contains(where: { "/\\:*?\"<>|".contains($0) || $0.asciiValue.map({ $0 < 32 }) == true }),
              allowed.contains((a.File as NSString).pathExtension.lowercased()) else { throw HandbookError("Ungültiger Dokumentverweis: \(a.Name)") }
    }
    static func validate(_ c: Catalog) throws {
        guard (1...3).contains(c.Schema), !blank(c.Revision), !blank(c.EditorSid) else { throw HandbookError("Nicht unterstütztes Handbuchformat. Bitte die App aktualisieren.") }
        guard c.Categories.allSatisfy({ !blank($0.Name) && $0.Subcategories.allSatisfy({ !blank($0) }) }) else { throw HandbookError("Ungültige Kategorienstruktur.") }
        var ids = Set<String>()
        for p in c.Processes {
            guard !blank(p.Id), ids.insert(p.Id).inserted, !blank(p.Title), p.Steps.count <= 60 else { throw HandbookError("Prozesse brauchen eindeutige Kennungen, einen Titel und höchstens 60 Schritte.") }
            var steps = Set<String>()
            for s in p.Steps {
                guard !blank(s.Id), steps.insert(s.Id).inserted, !blank(s.Title) else { throw HandbookError("Jeder Schritt braucht eine eindeutige Kennung und einen Titel.") }
                for a in s.Documents { try validateAttachment(a) }
            }
            for s in p.Steps {
                for target in [s.Next, s.Otherwise] where !target.isEmpty {
                    guard steps.contains(target) else { throw HandbookError("Schritt \(s.Id): Ziel \(target) existiert nicht.") }
                }
                if !s.Otherwise.isEmpty && s.Next.isEmpty { throw HandbookError("Schritt \(s.Id): Für eine Entscheidung fehlt das Ja-Ziel.") }
            }
        }
        for a in c.Templates { try validateAttachment(a) }
    }
    static func read(_ root: URL) throws -> Catalog {
        let url = root.appendingPathComponent("prozesse.json")
        let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        guard size <= 20 * 1024 * 1024 else { throw HandbookError("Die Prozessdatei überschreitet 20 MB.") }
        var bytes = try Data(contentsOf: url)
        if bytes.starts(with: [0xef, 0xbb, 0xbf]) { bytes.removeFirst(3) }
        let catalog = try JSONDecoder().decode(Catalog.self, from: bytes)
        try validate(catalog)
        return catalog
    }
    static func document(_ root: URL, _ a: Attachment) throws -> URL {
        try validateAttachment(a)
        if Builtins.contains(a.File) {
            let file = Builtins.directory.appendingPathComponent(a.File)
            guard try file.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile == true else { throw HandbookError("Mitgelieferte Vorlage fehlt. Bitte die App erneut installieren.") }
            return file
        }
        let dir = root.appendingPathComponent("Dokumente").resolvingSymlinksInPath().standardizedFileURL
        let source = dir.appendingPathComponent(a.File).resolvingSymlinksInPath().standardizedFileURL
        guard source.deletingLastPathComponent() == dir,
              try source.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile == true else { throw HandbookError("Dokument fehlt oder liegt außerhalb des Dokumentordners.") }
        return source
    }
    static func save(_ root: URL, catalog: Catalog, expected: String?, identity: String, pending: [String: URL] = [:]) throws -> Catalog {
        try validate(catalog)
        guard catalog.Schema == 3 else { throw HandbookError("Dieses Handbuch bitte zunächst mit Windows-Version 0.6 oder neuer auf das gemeinsame Format umstellen.") }
        let lock = root.appendingPathComponent(lockName)
        let fd = Darwin.open(lock.path, O_WRONLY | O_CREAT | O_EXCL, mode_t(0o600))
        guard fd >= 0 else { throw HandbookError("Veröffentlichung gesperrt: Ein anderer Schreibvorgang läuft oder Schreibrechte fehlen. Nach einem Absturz kann die IT die Datei \(lockName) entfernen, nachdem alle Apps geschlossen sind.") }
        defer { Darwin.close(fd); try? fm.removeItem(at: lock) }
        let dest = root.appendingPathComponent("prozesse.json")
        if let expected = expected {
            let actual = try read(root)
            guard actual.canEdit(identity), actual.Revision == expected else { throw HandbookError("Stand oder Bearbeiterfreigabe wurde geändert. Bitte aktualisieren und erneut bearbeiten.") }
        } else {
            guard !fm.fileExists(atPath: dest.path), catalog.canEdit(identity) else { throw HandbookError("Dieser Ordner enthält bereits ein Handbuch oder das Konto ist nicht freigegeben.") }
        }
        var next = catalog
        next.Revision = UUID().uuidString
        let encoder = JSONEncoder(); encoder.outputFormatting = [.sortedKeys, .prettyPrinted]
        let bytes = try encoder.encode(next)
        guard bytes.count <= 20 * 1024 * 1024 else { throw HandbookError("Die Prozessdatei überschreitet 20 MB.") }
        let attachments = next.Templates + next.Processes.flatMap { $0.Steps.flatMap(\.Documents) }
        let docs = root.appendingPathComponent("Dokumente")
        var copied: [URL] = []
        var committed = false
        defer { if !committed { copied.forEach { try? fm.removeItem(at: $0) } } }
        for a in attachments {
            if let source = pending[a.File] {
                try fm.createDirectory(at: docs, withIntermediateDirectories: true)
                let target = docs.appendingPathComponent(a.File)
                if !copied.contains(target) {
                    try fm.copyItem(at: source, to: target)
                    copied.append(target)
                }
            } else { _ = try document(root, a) }
        }
        if expected != nil {
            let backups = root.appendingPathComponent("Sicherungen")
            try fm.createDirectory(at: backups, withIntermediateDirectories: true)
            try fm.copyItem(at: dest, to: backups.appendingPathComponent("\(Int(Date().timeIntervalSince1970))-\(UUID().uuidString).json"))
        }
        let temp = root.appendingPathComponent(".prozesse-\(UUID().uuidString).tmp")
        defer { try? fm.removeItem(at: temp) }
        try bytes.write(to: temp, options: .withoutOverwriting)
        let file = try FileHandle(forWritingTo: temp)
        try file.synchronize(); try file.close()
        guard Darwin.rename(temp.path, dest.path) == 0 else { throw HandbookError("Die Prozessdatei konnte nicht ersetzt werden. Der bisherige Stand bleibt erhalten.") }
        committed = true
        return next
    }
    static func copyDocument(_ root: URL, _ a: Attachment, to destination: URL) throws {
        let source = try document(root, a)
        let resolved = destination.resolvingSymlinksInPath().standardizedFileURL
        let shared = root.resolvingSymlinksInPath().standardizedFileURL.path + "/"
        let bundled = Builtins.directory.resolvingSymlinksInPath().standardizedFileURL.path + "/"
        guard !resolved.path.hasPrefix(shared), !resolved.path.hasPrefix(bundled) else { throw HandbookError("Bitte außerhalb des gemeinsamen Prozessordners speichern.") }
        let temp = destination.deletingLastPathComponent().appendingPathComponent(".kopie-\(UUID().uuidString).tmp")
        defer { try? fm.removeItem(at: temp) }
        try fm.copyItem(at: source, to: temp)
        guard Darwin.rename(temp.path, destination.path) == 0 else { throw HandbookError("Die lokale Kopie konnte nicht ersetzt werden.") }
    }
}
