import SwiftUI
import AppKit
import IOKit
import UniformTypeIdentifiers

extension Color {
    static let ink = Color(red: 0, green: 23/255, blue: 67/255)
    static let brandOrange = Color(red: 229/255, green: 84/255, blue: 17/255)
    static let paper = Color(red: 244/255, green: 246/255, blue: 250/255)
    static let peach = Color(red: 1, green: 241/255, blue: 232/255)
}
enum MacIdentity {
    static var value: String {
        let service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("IOPlatformExpertDevice"))
        defer { if service != 0 { IOObjectRelease(service) } }
        let hardware = service == 0 ? nil : IORegistryEntryCreateCFProperty(service, "IOPlatformUUID" as CFString, kCFAllocatorDefault, 0)?.takeRetainedValue() as? String
        return "mac:\(hardware ?? ProcessInfo.processInfo.hostName):\(getuid())"
    }
}
final class HandbookModel: ObservableObject {
    @Published var catalog = Catalog()
    @Published var root: URL?
    @Published var online = false
    @Published var busy = false
    @Published var message: String?
    @Published var notice = ""
    var editing = false
    let identity = MacIdentity.value
    private let work = DispatchQueue(label: "de.gruettner.prozesshandbuch.storage", qos: .userInitiated)
    private var timer: Timer?
    var canEdit: Bool { online && catalog.canEdit(identity) }
    var status: String {
        if busy { return "Daten werden verarbeitet …" }
        if root == nil { return "Kategorienübersicht · Noch kein Datenordner verbunden" }
        if !online { return "Verbindung unterbrochen · Angezeigter Stand möglicherweise veraltet" }
        return canEdit ? "Bearbeitung · \(catalog.EditorName)" : "Leseansicht · Automatische Aktualisierung alle 30 Sekunden"
    }
    init() {
        if let path = UserDefaults.standard.string(forKey: "dataFolder") { root = URL(fileURLWithPath: path); refresh() }
        timer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            guard let self = self, !self.editing else { return }; self.refresh(quiet: true)
        }
    }
    func refresh(quiet: Bool = false) {
        guard let folder = root, !busy else { return }
        busy = true
        work.async {
            let result = Result { try Storage.read(folder) }
            DispatchQueue.main.async {
                self.busy = false
                switch result {
                case .success(let c): self.catalog = c; self.online = true
                case .failure(let e): self.online = false; if !quiet { self.message = e.localizedDescription }
                }
            }
        }
    }
    func chooseFolder() {
        guard !busy else { return }
        let panel = NSOpenPanel(); panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.canCreateDirectories = true
        panel.title = "Gemeinsamen Prozessordner verbinden"; panel.prompt = "Verbinden"
        guard panel.runModal() == .OK, let folder = panel.url else { return }
        // Existence is checked on the storage queue; a sleeping share must not freeze the window.
        busy = true
        work.async {
            let exists = FileManager.default.fileExists(atPath: folder.appendingPathComponent("prozesse.json").path)
            DispatchQueue.main.async {
                if !exists {
                    let alert = NSAlert(); alert.messageText = "Neues Handbuch anlegen?"
                    alert.informativeText = "Der Ordner enthält kein Handbuch. Dein Mac-Konto wird als Bearbeiter hinterlegt. Für Mitarbeiter benötigt die Freigabe Leserechte, für dich Änderungsrechte. Die App setzt keine Serverrechte."
                    alert.addButton(withTitle: "Handbuch anlegen"); alert.addButton(withTitle: "Abbrechen")
                    guard alert.runModal() == .alertFirstButtonReturn else { self.busy = false; return }
                }
                self.connect(folder, create: !exists)
            }
        }
    }
    private func connect(_ folder: URL, create: Bool) {
        let identity = self.identity
        work.async {
            let result = Result { () -> Catalog in
                if create {
                    var c = Catalog(); c.EditorSid = identity; c.EditorMacId = identity; c.EditorName = NSFullUserName()
                    return try Storage.save(folder, catalog: c, expected: nil, identity: identity)
                }
                return try Storage.read(folder)
            }
            DispatchQueue.main.async {
                self.busy = false
                switch result {
                case .success(let c):
                    self.root = folder; self.catalog = c; self.online = true
                    UserDefaults.standard.set(folder.path, forKey: "dataFolder")
                case .failure(let e): self.message = e.localizedDescription
                }
            }
        }
    }
    func publish(_ next: Catalog, expected: String, pending: [String: URL] = [:], completion: @escaping () -> Void = {}) {
        guard let root = root, canEdit, !busy else { return }
        busy = true; let identity = self.identity
        work.async {
            let result = Result { try Storage.save(root, catalog: next, expected: expected, identity: identity, pending: pending) }
            DispatchQueue.main.async {
                self.busy = false
                switch result {
                case .success(let c): self.catalog = c; self.online = true; self.notice = "Änderungen veröffentlicht."; completion()
                case .failure(let e): self.message = e.localizedDescription
                }
            }
        }
    }
    func uploadTemplates() {
        guard canEdit, !busy else { return }
        let files = Self.pickDocuments(); guard !files.isEmpty else { return }
        var next = catalog, pending: [String: URL] = [:]
        for url in files {
            let key = UUID().uuidString + "." + url.pathExtension.lowercased()
            next.Templates.append(Attachment(Name: url.lastPathComponent, File: key)); pending[key] = url
        }
        publish(next, expected: catalog.Revision, pending: pending)
    }
    static func pickDocuments() -> [URL] {
        let panel = NSOpenPanel(); panel.allowsMultipleSelection = true; panel.canChooseDirectories = false
        panel.title = "Dokumente hinzufügen"; panel.allowedContentTypes = Storage.allowed.sorted().compactMap { UTType(filenameExtension: $0) }
        return panel.runModal() == .OK ? panel.urls : []
    }
    func canAccess(_ a: Attachment) -> Bool { Builtins.contains(a.File) || (online && root != nil) }
    func copy(_ a: Attachment, open: Bool = false) {
        guard canAccess(a), !busy else { return }
        let root = root ?? Builtins.directory
        let destination: URL
        if open {
            destination = FileManager.default.temporaryDirectory.appendingPathComponent("Prozesshandbuch-\(UUID().uuidString)").appendingPathComponent((a.Name as NSString).lastPathComponent)
        } else {
            let panel = NSSavePanel(); panel.title = "Lokale Kopie speichern"; panel.nameFieldStringValue = (a.Name as NSString).lastPathComponent
            guard panel.runModal() == .OK, let url = panel.url else { return }; destination = url
        }
        busy = true
        work.async {
            let result = Result { () -> Void in
                if open { try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true) }
                try Storage.copyDocument(root, a, to: destination)
            }
            DispatchQueue.main.async {
                self.busy = false
                switch result {
                case .success:
                    if open {
                        if !NSWorkspace.shared.open(destination) { self.message = "Keine passende App zum Öffnen gefunden. Bitte die Datei über „Kopie speichern“ herunterladen." }
                        else { self.notice = "Lokale Arbeitskopie geöffnet. Änderungen werden nicht automatisch hochgeladen." }
                    } else { self.notice = "Kopie gespeichert: \(destination.lastPathComponent)" }
                case .failure(let e): self.message = e.localizedDescription
                }
            }
        }
    }
}
struct BrandMark: View {
    var body: some View {
        GeometryReader { g in
            let w = g.size.width, h = g.size.height
            Path { p in
                p.addRect(CGRect(x: 0, y: 0, width: w * 0.42, height: h * 0.435))
                p.addRect(CGRect(x: 0, y: h * 0.565, width: w * 0.42, height: h * 0.435))
            }.fill(Color.ink)
            Path { p in
                p.move(to: CGPoint(x: w * 0.55, y: 0)); p.addLine(to: CGPoint(x: w, y: 0))
                p.addLine(to: CGPoint(x: w, y: h * 0.435)); p.addLine(to: CGPoint(x: w * 0.81, y: h * 0.435))
                p.addLine(to: CGPoint(x: w * 0.81, y: h * 0.565)); p.addLine(to: CGPoint(x: w, y: h * 0.565))
                p.addLine(to: CGPoint(x: w, y: h)); p.addLine(to: CGPoint(x: w * 0.55, y: h)); p.closeSubpath()
            }.fill(Color.brandOrange)
        }.accessibilityLabel("Bruno Grüttner Logo")
    }
}
struct Brand: View {
    var body: some View {
        HStack(spacing: 14) {
            BrandMark().frame(width: 48, height: 48)
            VStack(alignment: .leading, spacing: 3) {
                Text("Bruno Grüttner").font(.system(size: 20, weight: .semibold)).foregroundColor(.ink)
                Text("Grundstücksverwaltungen\nImmobilien e.K.").font(.system(size: 10)).foregroundColor(.brandOrange)
            }
        }
    }
}
struct SoftButton: ButtonStyle {
    var primary = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.font(.system(size: 13, weight: .semibold)).padding(.horizontal, 16).padding(.vertical, 12)
            .foregroundColor(primary ? .white : .ink)
            .background(primary ? Color.ink : Color.white)
            .clipShape(RoundedRectangle(cornerRadius: 12)).opacity(configuration.isPressed ? 0.7 : 1)
    }
}
struct Card<Content: View>: View {
    @ViewBuilder var content: Content
    var body: some View { content.padding(26).frame(maxWidth: .infinity, alignment: .leading).background(Color.white).clipShape(RoundedRectangle(cornerRadius: 20)) }
}
struct MainView: View {
    @EnvironmentObject var model: HandbookModel
    @State private var menu = CommandLine.arguments.contains("--capture-templates") ? "Vorlagen" : (CommandLine.arguments.contains("--capture-guide") || CommandLine.arguments.contains("--capture-tenant") ? "Anleitung" : "Start")
    @State private var category = CommandLine.arguments.contains("--capture-tenant") ? "Miethäuser" : "Alle Bereiche"
    @State private var query = ""
    @State private var selected: String?
    @State private var settings = false
    @State private var draft: Procedure?
    @State private var draftCatalog = Catalog()
    private var entries: [Procedure] { model.catalog.entries.filter { (category == "Alle Bereiche" || $0.Department == category) && (query.isEmpty || $0.searchable.localizedStandardContains(query)) } }
    private var current: Procedure? { entries.first(where: { $0.Id == selected }) ?? entries.first }
    var body: some View {
        VStack(spacing: 0) {
            if menu == "Start" { ScrollView { home } } else {
                HStack(spacing: 0) {
                    if menu == "Anleitung" { sidebar }
                    VStack(alignment: .leading, spacing: 22) {
                        HStack {
                            Button { menu = "Start"; query = "" } label: { Label("Hauptmenü", systemImage: "arrow.left") }.buttonStyle(SoftButton())
                            Text(menu).font(.system(size: 15)).foregroundColor(.secondary)
                            Spacer()
                            if model.busy { ProgressView().controlSize(.small) }
                            Button("Aktualisieren") { model.refresh() }.buttonStyle(SoftButton()).disabled(model.busy)
                            Button { settings = true } label: { Image(systemName: "gearshape") }.buttonStyle(SoftButton()).help("Einstellungen")
                        }
                        if menu == "Anleitung" { instructions } else { templates }
                    }.padding(30).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                }
            }
            HStack(spacing: 8) {
                Circle().fill(model.online ? Color.green : Color.brandOrange).frame(width: 6, height: 6)
                Text(model.status).font(.system(size: 11))
                Spacer()
                Text("Prozesshandbuch · 0.6.1").font(.system(size: 11))
            }.foregroundColor(.secondary).padding(.horizontal, 24).padding(.vertical, 12).background(Color.white)
        }.background(Color.paper).foregroundColor(.ink)
            .frame(minWidth: 1050, minHeight: 720)
            .preferredColorScheme(.light)
            .sheet(isPresented: $settings) { SettingsView().environmentObject(model) }
            .sheet(item: $draft, onDismiss: { model.editing = false }) { p in
                EditorView(procedure: p, snapshot: draftCatalog).environmentObject(model)
            }
            .alert("Prozesshandbuch", isPresented: Binding(get: { model.message != nil }, set: { if !$0 { model.message = nil } })) {
                Button("OK") { model.message = nil }
            } message: { Text(model.message ?? "") }
    }
    private var home: some View {
        VStack(alignment: .leading, spacing: 26) {
            HStack { Brand(); Spacer(); Button { settings = true } label: { Label("Einstellungen", systemImage: "gearshape") }.buttonStyle(SoftButton()) }
            VStack(alignment: .leading, spacing: 14) {
                Text("GUT ORGANISIERT. GEMEINSAM WEITER.").font(.system(size: 11, weight: .bold)).tracking(2).foregroundColor(.brandOrange)
                Text("Wissen, das den\nAlltag leichter macht.").font(.system(size: 40, weight: .semibold)).tracking(-1).fixedSize(horizontal: false, vertical: true)
                Text("Anleitungen und Vorlagen für unsere tägliche Zusammenarbeit.").font(.system(size: 16)).foregroundColor(.secondary)
            }
            HStack(spacing: 22) {
                homeCard("Anleitung", "book.closed", "Arbeitsabläufe verstehen", "Alle Bereiche, klare Schritte und wichtige Dokumente an einem Ort.")
                homeCard("Vorlagen", "doc.on.doc", "Direkt mit der richtigen Vorlage starten", "Gemeinsame Dokumente finden und als lokale Kopie herunterladen.")
            }
            Text("3 Bereiche · 12 Unterkategorien · Ein gemeinsames Handbuch").font(.system(size: 12)).foregroundColor(.secondary)
        }.padding(36).frame(maxWidth: 1250)
    }
    private func homeCard(_ title: String, _ icon: String, _ subtitle: String, _ text: String) -> some View {
        Button { menu = title; query = "" } label: {
            VStack(alignment: .leading, spacing: 16) {
                Image(systemName: icon).font(.system(size: 26)).foregroundColor(.brandOrange).padding(14).background(Color.peach).clipShape(RoundedRectangle(cornerRadius: 14))
                Text(title).font(.system(size: 28, weight: .semibold))
                Text(subtitle).font(.system(size: 14, weight: .semibold))
                Text(text).font(.system(size: 14)).foregroundColor(.secondary).fixedSize(horizontal: false, vertical: true).frame(minHeight: 40, alignment: .top)
                HStack { Text("Öffnen"); Spacer(); Image(systemName: "arrow.right") }.font(.system(size: 13, weight: .semibold)).padding(.top, 6)
            }.padding(28).frame(maxWidth: .infinity, alignment: .leading).background(Color.white).clipShape(RoundedRectangle(cornerRadius: 22))
        }.buttonStyle(.plain)
    }
    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 16) {
            Brand().padding(.bottom, 12)
            Text("Prozessbibliothek").font(.system(size: 20, weight: .semibold))
            VStack(alignment: .leading, spacing: 7) {
                Text("Suchen").font(.caption).foregroundColor(.secondary)
                TextField("Suchbegriff eingeben", text: $query).textFieldStyle(.roundedBorder).controlSize(.large)
            }
            VStack(alignment: .leading, spacing: 7) {
                Text("Kategorie").font(.caption).foregroundColor(.secondary)
                ForEach(["Alle Bereiche"] + model.catalog.Categories.map(\.Name), id: \.self) { name in
                    Button { category = name; selected = nil } label: {
                        Text(name).font(.system(size: 13, weight: .semibold)).frame(maxWidth: .infinity, alignment: .leading).padding(12)
                            .background(category == name ? Color.ink : Color.paper).foregroundColor(category == name ? .white : .ink).clipShape(RoundedRectangle(cornerRadius: 11))
                    }.buttonStyle(.plain)
                }
            }
            Text("\(entries.count) Einträge").font(.caption).foregroundColor(.secondary)
            ScrollView {
                LazyVStack(spacing: 5) {
                    ForEach(entries) { p in
                        Button { selected = p.Id } label: {
                            VStack(alignment: .leading, spacing: 5) {
                                Text(p.Title).font(.system(size: 13, weight: .medium)).fixedSize(horizontal: false, vertical: true)
                                if category == "Alle Bereiche" { Text(p.Department).font(.system(size: 10)).foregroundColor(.secondary) }
                            }.frame(maxWidth: .infinity, alignment: .leading).padding(12).background(current?.Id == p.Id ? Color.peach : Color.paper).clipShape(RoundedRectangle(cornerRadius: 10))
                        }.buttonStyle(.plain)
                    }
                }
            }
        }.padding(22).frame(width: 316).background(Color.white)
    }
    @ViewBuilder private var instructions: some View {
        if let p = current {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 9) {
                    Text(p.Title).font(.system(size: 29, weight: .semibold)).fixedSize(horizontal: false, vertical: true)
                    Text(p.Department).font(.system(size: 13)).foregroundColor(.secondary)
                }
                    if model.canEdit {
                    Menu {
                        Button("Bearbeiten") { edit(p) }
                        Button("Neuer Prozess") { var n = Procedure(); n.Department = p.Department; n.Topic = p.Topic; edit(n) }
                        if model.catalog.Processes.contains(where: { $0.Id == p.Id }) {
                            Button("Prozess löschen", role: .destructive) { delete(p) }
                        }
                    } label: { Label("Bearbeiten", systemImage: "pencil") }.disabled(model.busy)
                }
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    if p.Steps.isEmpty {
                        Card { VStack(alignment: .leading, spacing: 16) {
                            Text("Unterkategorie angelegt").font(.system(size: 22, weight: .semibold))
                            Text("Der Prozessablauf wird im nächsten Schritt ergänzt. Hier erscheinen später Arbeitsschritte, Zuständigkeiten und Checklisten.").foregroundColor(.secondary).fixedSize(horizontal: false, vertical: true)
                            if !p.Summary.isEmpty { Text(p.Summary).textSelection(.enabled) }
                            if model.canEdit { Button("Ablauf ergänzen") { edit(p) }.buttonStyle(SoftButton(primary: true)).disabled(model.busy) }
                        }.padding(.vertical, 16) }
                    } else {
                        VStack(alignment: .leading, spacing: 12) {
                            Text("ÜBERBLICK").font(.system(size: 11, weight: .bold)).tracking(2).foregroundColor(.orange)
                            Text(p.Summary.isEmpty ? "Für diesen Prozess ist noch kein Überblick hinterlegt." : p.Summary).foregroundColor(.white).textSelection(.enabled)
                            Text("Veröffentlicht · \(p.Updated)").font(.caption).foregroundColor(.white.opacity(0.65))
                        }.padding(26).frame(maxWidth: .infinity, alignment: .leading).background(Color.ink).clipShape(RoundedRectangle(cornerRadius: 20))
                        ForEach(p.Steps) { s in stepCard(s) }
                    }
                }.padding(.bottom, 10)
            }
        } else { Card { Text("Keine Treffer. Passe den Suchbegriff oder die Kategorie an.") }; Spacer() }
    }
    private func stepCard(_ s: Step) -> some View {
        Card {
            VStack(alignment: .leading, spacing: 16) {
                HStack(alignment: .top, spacing: 14) {
                    Text(s.Id).font(.caption.bold()).padding(12).background(Color.peach).clipShape(Capsule())
                    VStack(alignment: .leading, spacing: 6) { Text(s.Title).font(.system(size: 20, weight: .semibold)); if !s.Owner.isEmpty { Text("Zuständig · \(s.Owner)").font(.caption).foregroundColor(.secondary) } }
                }
                Text(s.Instructions).textSelection(.enabled)
                if !s.Checklist.isEmpty {
                    Text("Checkliste").font(.headline)
                    ForEach(Array(s.Checklist.split(whereSeparator: \.isNewline).enumerated()), id: \.offset) { _, line in
                        HStack(alignment: .top) { Image(systemName: "square").foregroundColor(.secondary); Text(String(line)).textSelection(.enabled) }
                    }
                }
                ForEach(s.Documents) { a in
                    Button { model.copy(a, open: true) } label: { Label(a.Name, systemImage: "doc") }.disabled(!model.canAccess(a) || model.busy)
                }
                Divider()
                Text(route(s)).font(.caption).foregroundColor(.secondary)
            }
        }
    }
    private var templates: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack {
                VStack(alignment: .leading, spacing: 8) { Text("Die richtige Vorlage.\nDirekt zur Hand.").font(.system(size: 32, weight: .semibold)); Text("Persönliche Kopie speichern und abhaken – digital oder auf Papier.").foregroundColor(.secondary) }
                Spacer()
                if model.canEdit { Button { model.uploadTemplates() } label: { Label("Vorlage hochladen", systemImage: "plus") }.buttonStyle(SoftButton(primary: true)).disabled(model.busy) }
            }
            TextField("Vorlagen durchsuchen", text: $query).textFieldStyle(.roundedBorder).controlSize(.large).frame(maxWidth: 440)
            ScrollView {
                VStack(spacing: 12) {
                    let files = Builtins.templates(for: model.catalog).filter { query.isEmpty || $0.Name.localizedStandardContains(query) }
                    if files.isEmpty { Card { VStack(alignment: .leading, spacing: 12) { Text(query.isEmpty ? "Noch keine Vorlagen hinterlegt" : "Keine passenden Vorlagen").font(.title2.bold()); Text(model.canEdit ? "Über „Vorlage hochladen“ kannst du Dokumente für alle bereitstellen." : "Veröffentlichte Vorlagen erscheinen hier, sobald ein Datenordner verbunden ist.").foregroundColor(.secondary) } } }
                    ForEach(files) { a in documentRow(a) }
                }
            }
            if !model.notice.isEmpty { Text(model.notice).font(.caption).foregroundColor(.secondary) }
        }
    }
    private func documentRow(_ a: Attachment) -> some View {
        Card { HStack(spacing: 16) {
            Image(systemName: "doc.text").font(.title2).foregroundColor(.brandOrange)
            Text(a.Name).font(.system(size: 15, weight: .medium)).textSelection(.enabled)
            Spacer()
            Button("Öffnen") { model.copy(a, open: true) }.buttonStyle(SoftButton()).disabled(!model.canAccess(a) || model.busy)
            Button("Kopie speichern") { model.copy(a) }.buttonStyle(SoftButton(primary: true)).disabled(!model.canAccess(a) || model.busy)
        } }
    }
    private func edit(_ p: Procedure) { draftCatalog = model.catalog; model.editing = true; draft = p }
    private func delete(_ p: Procedure) {
        let a = NSAlert(); a.messageText = "Prozess löschen?"; a.informativeText = "„\(p.Title)“ wird entfernt. Die vorherige Fassung bleibt im Sicherungsordner erhalten."
        a.addButton(withTitle: "Löschen"); a.addButton(withTitle: "Abbrechen")
        if a.runModal() == .alertFirstButtonReturn { var next = model.catalog; next.Processes.removeAll { $0.Id == p.Id }; model.publish(next, expected: model.catalog.Revision) }
    }
}
func route(_ s: Step) -> String {
    s.Otherwise.isEmpty ? "Weiter → \(s.Next.isEmpty ? "Ende" : s.Next)" : "Ja → \(s.Next)     Nein → \(s.Otherwise)"
}
