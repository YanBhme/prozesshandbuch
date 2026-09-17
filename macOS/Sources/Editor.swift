import SwiftUI
import AppKit

struct EditorView: View {
    @EnvironmentObject var model: HandbookModel
    @Environment(\.dismiss) private var dismiss
    @State private var draft: Procedure
    private let original: Procedure
    private let snapshot: Catalog
    @State private var selected: String?
    @State private var pending: [String: URL] = [:]
    @State private var error: String?
    init(procedure: Procedure, snapshot: Catalog) {
        _draft = State(initialValue: procedure); original = procedure; self.snapshot = snapshot
        _selected = State(initialValue: procedure.Steps.first?.Id)
    }
    private var index: Int? { draft.Steps.firstIndex { $0.Id == selected } }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack { Text("Prozess bearbeiten").font(.title2.bold()); Spacer(); Text("Nur für den freigegebenen Bearbeiter").font(.caption).foregroundColor(.secondary) }
            VStack(spacing: 12) {
                TextField("Prozesstitel", text: $draft.Title).font(.title3)
                HStack {
                    Picker("Kategorie", selection: $draft.Department) {
                        Text("Bitte wählen").tag("")
                        ForEach(snapshot.Categories) { c in Text(c.Name).tag(c.Name) }
                    }
                    Picker("Unterkategorie", selection: $draft.Topic) {
                        Text("Ohne Zuordnung").tag("")
                        ForEach(topics, id: \.self) { Text($0).tag($0) }
                    }
                }
                TextField("Überblick über den Prozess", text: $draft.Summary, axis: .vertical).lineLimit(2...4)
            }.textFieldStyle(.roundedBorder)
            HStack(alignment: .top, spacing: 20) {
                VStack(alignment: .leading, spacing: 10) {
                    HStack { Text("Schritte").font(.headline); Spacer(); Button { addStep() } label: { Image(systemName: "plus") }.disabled(draft.Steps.count >= 60) }
                    List(selection: $selected) {
                        ForEach(draft.Steps) { s in
                            VStack(alignment: .leading, spacing: 5) { Text("\(s.Id) · \(s.Title)").lineLimit(3); Text(s.Owner).font(.caption).foregroundColor(.secondary) }.padding(.vertical, 6).tag(s.Id)
                        }
                    }.listStyle(.inset).clipShape(RoundedRectangle(cornerRadius: 10))
                    HStack {
                        Button { move(-1) } label: { Image(systemName: "arrow.up") }.disabled(index == nil || index == 0)
                        Button { move(1) } label: { Image(systemName: "arrow.down") }.disabled(index == nil || index == draft.Steps.count - 1)
                        Spacer()
                        Button("Entfernen") { removeStep() }.disabled(index == nil)
                    }
                }.frame(width: 240)
                if let i = index {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 14) {
                            field("Schritt / Entscheidungsfrage", binding: $draft.Steps[i].Title)
                            field("Zuständig", binding: $draft.Steps[i].Owner)
                            Text("Anweisung").font(.caption).foregroundColor(.secondary)
                            TextEditor(text: $draft.Steps[i].Instructions).font(.body).frame(minHeight: 120).padding(8).background(Color.white).clipShape(RoundedRectangle(cornerRadius: 10))
                            Text("Checkliste · Ein Punkt pro Zeile").font(.caption).foregroundColor(.secondary)
                            TextEditor(text: $draft.Steps[i].Checklist).font(.body).frame(minHeight: 80).padding(8).background(Color.white).clipShape(RoundedRectangle(cornerRadius: 10))
                            HStack {
                                destination("Weiter / Ja", binding: $draft.Steps[i].Next)
                                destination("Nein (optional)", binding: $draft.Steps[i].Otherwise)
                            }
                            HStack { Text("Dokumente").font(.headline); Spacer(); Button("Hinzufügen …") { attach(i) } }
                            ForEach(draft.Steps[i].Documents) { a in
                                HStack { Image(systemName: "doc"); Text(a.Name).lineLimit(2); Spacer(); Button { draft.Steps[i].Documents.removeAll { $0.File == a.File } } label: { Image(systemName: "xmark.circle") }.help("Dokumentverweis entfernen") }
                            }
                        }.padding(.trailing, 8)
                    }
                } else {
                    VStack(spacing: 16) { Image(systemName: "list.number").font(.largeTitle).foregroundColor(.brandOrange); Text("Noch keine Schritte").font(.title2); Text("Lege den ersten Arbeitsschritt über + an.").foregroundColor(.secondary); Button("Schritt hinzufügen") { addStep() }.buttonStyle(SoftButton(primary: true)) }.frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            if let error = error { Text(error).font(.caption).foregroundColor(.red).fixedSize(horizontal: false, vertical: true) }
            HStack {
                Text("Änderungen werden erst mit „Veröffentlichen“ für alle sichtbar.").font(.caption).foregroundColor(.secondary)
                Spacer()
                Button("Abbrechen") { cancel() }.buttonStyle(SoftButton())
                Button(model.busy ? "Wird veröffentlicht …" : "Veröffentlichen") { publish() }.buttonStyle(SoftButton(primary: true))
            }
        }.padding(28).frame(width: 980, height: 740).background(Color.paper).foregroundColor(.ink)
            .disabled(model.busy).interactiveDismissDisabled()
            .onChange(of: draft.Department) { _ in if !topics.contains(draft.Topic) { draft.Topic = "" } }
    }
    private var topics: [String] { snapshot.Categories.first(where: { $0.Name == draft.Department })?.Subcategories ?? [] }
    private func field(_ title: String, binding: Binding<String>) -> some View {
        VStack(alignment: .leading, spacing: 6) { Text(title).font(.caption).foregroundColor(.secondary); TextField(title, text: binding).textFieldStyle(.roundedBorder) }
    }
    private func destination(_ title: String, binding: Binding<String>) -> some View {
        Picker(title, selection: binding) {
            Text("Ende / kein Ziel").tag("")
            ForEach(draft.Steps) { s in Text("\(s.Id) · \(s.Title)").tag(s.Id) }
        }
    }
    private func addStep() {
        var n = 1; while draft.Steps.contains(where: { $0.Id == String(n) }) { n += 1 }
        var s = Step(); s.Id = String(n); s.Title = "Neuer Schritt"; draft.Steps.append(s); selected = s.Id
    }
    private func move(_ direction: Int) {
        guard let i = index, draft.Steps.indices.contains(i + direction) else { return }
        draft.Steps.swapAt(i, i + direction)
    }
    private func removeStep() {
        guard let i = index else { return }
        let a = NSAlert(); a.messageText = "Schritt entfernen?"; a.informativeText = "Verbindungen zu diesem Schritt werden ebenfalls entfernt."
        a.addButton(withTitle: "Entfernen"); a.addButton(withTitle: "Abbrechen")
        guard a.runModal() == .alertFirstButtonReturn else { return }
        let id = draft.Steps[i].Id; draft.Steps.remove(at: i)
        for j in draft.Steps.indices {
            if draft.Steps[j].Next == id { draft.Steps[j].Next = ""; draft.Steps[j].Otherwise = "" }
            if draft.Steps[j].Otherwise == id { draft.Steps[j].Otherwise = "" }
        }
        selected = draft.Steps.first?.Id
    }
    private func attach(_ i: Int) {
        for url in HandbookModel.pickDocuments() {
            let key = UUID().uuidString + "." + url.pathExtension.lowercased()
            pending[key] = url; draft.Steps[i].Documents.append(Attachment(Name: url.lastPathComponent, File: key))
        }
    }
    private func cancel() {
        if draft != original {
            let a = NSAlert(); a.messageText = "Ungespeicherte Änderungen verwerfen?"; a.addButton(withTitle: "Verwerfen"); a.addButton(withTitle: "Weiter bearbeiten")
            guard a.runModal() == .alertFirstButtonReturn else { return }
        }
        dismiss()
    }
    private func publish() {
        var next = snapshot, p = draft
        p.Title = p.Title.trimmingCharacters(in: .whitespacesAndNewlines)
        let formatter = DateFormatter(); formatter.locale = Locale(identifier: "de_DE"); formatter.dateFormat = "dd.MM.yyyy HH:mm"; p.Updated = formatter.string(from: Date())
        if let i = next.Processes.firstIndex(where: { $0.Id == p.Id }) { next.Processes[i] = p }
        else { if p.Id.hasPrefix("section-") { p.Id = UUID().uuidString }; next.Processes.append(p) }
        do { try Storage.validate(next); error = nil }
        catch { self.error = error.localizedDescription; return }
        model.publish(next, expected: snapshot.Revision, pending: pending) { dismiss() }
    }
}
struct SettingsView: View {
    @EnvironmentObject var model: HandbookModel
    @Environment(\.dismiss) private var dismiss
    @State private var windowsSid = ""
    var body: some View {
        VStack(alignment: .leading, spacing: 22) {
            HStack { Text("Einstellungen").font(.title2.bold()); Spacer(); Button("Fertig") { dismiss() }.buttonStyle(SoftButton(primary: true)) }
            Card { VStack(alignment: .leading, spacing: 12) {
                Label("Gemeinsamer Datenordner", systemImage: "externaldrive.connected.to.line.below").font(.headline)
                Text(model.root?.path ?? "Noch nicht verbunden").font(.callout).foregroundColor(.secondary).textSelection(.enabled).lineLimit(3)
                Text("Verbinde das Netzlaufwerk im Finder über „Gehe zu → Mit Server verbinden“. Wähle anschließend denselben Prozessordner wie unter Windows.").font(.callout).fixedSize(horizontal: false, vertical: true)
                Button("Datenordner verbinden …") { model.chooseFolder() }.buttonStyle(SoftButton(primary: true)).disabled(model.busy)
            } }
            Card { VStack(alignment: .leading, spacing: 12) {
                Label("Bearbeiterfreigabe", systemImage: "person.crop.circle.badge.checkmark").font(.headline)
                Text(model.canEdit ? "Dieses Mac-Konto darf bearbeiten." : "Dieses Mac-Konto liest veröffentlichte Inhalte.").font(.callout)
                Text("Deine Mac-Kennung").font(.caption).foregroundColor(.secondary)
                HStack { Text(model.identity).font(.system(size: 11, design: .monospaced)).textSelection(.enabled); Spacer(); Button("Kopieren") { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(model.identity, forType: .string) } }
                if !model.canEdit {
                    Text("Für ein bestehendes Windows-Handbuch: Kopiere diese Kennung und trage sie unter Windows-Version 0.6.0 oder neuer über „Bearbeiterkonten“ ein. Ältere Handbücher bleiben auf dem Mac lesbar.").font(.caption).foregroundColor(.secondary).fixedSize(horizontal: false, vertical: true)
                } else {
                    Divider()
                    Text("Dein Windows-Konto zusätzlich freigeben").font(.subheadline.bold())
                    Text("Die Windows-Kennung wird in der Windows-App unter „Bearbeiterkonten“ angezeigt. Trage nur dein eigenes Konto ein.").font(.caption).foregroundColor(.secondary).fixedSize(horizontal: false, vertical: true)
                    HStack { TextField("S-1-…", text: $windowsSid).textFieldStyle(.roundedBorder); Button("Freigeben") { authorize() }.disabled(model.busy || !windowsSid.hasPrefix("S-1-")) }
                }
                Text("Die tatsächlichen Zugriffsrechte setzt eure IT am Datenordner: du = Ändern, Mitarbeiter = Lesen.").font(.caption).foregroundColor(.secondary).fixedSize(horizontal: false, vertical: true)
            } }
            Text("Prozesshandbuch 0.6.0 · macOS 13 oder neuer\nAnleitungen und Vorlagen werden ausschließlich im gewählten Datenordner gespeichert.").font(.caption).foregroundColor(.secondary)
        }.padding(28).frame(width: 680).background(Color.paper).foregroundColor(.ink)
    }
    private func authorize() {
        let sid = windowsSid.trimmingCharacters(in: .whitespacesAndNewlines)
        guard sid.range(of: "^S-1-[0-9]+(-[0-9]+)+$", options: .regularExpression) != nil else { model.message = "Bitte eine gültige Windows-Kennung eingeben."; return }
        var next = model.catalog; next.EditorSid = sid; next.EditorMacId = model.identity
        model.publish(next, expected: model.catalog.Revision) { windowsSid = "" }
    }
}
final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true)
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}
@main struct ProzesshandbuchApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = HandbookModel()
    var body: some Scene {
        WindowGroup("Bruno Grüttner · Prozesshandbuch") { MainView().environmentObject(model) }
            .defaultSize(width: 1280, height: 820)
            .commands { CommandGroup(replacing: .newItem) {} }
    }
}
