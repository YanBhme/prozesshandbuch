using System;
using System.IO;
using System.Linq;
using Prozesshandbuch;
public static class StorageTests {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("OK: "+name);}
 static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
 public static int Main(){string root=Path.Combine(Path.GetTempPath(),"Prozesshandbuch-Test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var c=Storage.New();Storage.Save(root,c,null);Check(Storage.Read(root).EditorSid==Storage.Sid(),"Erstellerkonto bleibt erhalten");
   Check(c.Categories.Count==3&&c.Categories.SelectMany(x=>x.Subcategories).Count()==12,"3 Kategorien und 12 Unterkategorien vorhanden");
   Check(Structure.Entries(c).Count()==12&&Structure.Entries(c).All(x=>x.Steps.Count==0)&&c.Processes.Count==0,"Leere Struktur ohne erfundene Abläufe oder Schreibzugriff");
   var legacy=Storage.Json().Deserialize<Catalog>("{\"Schema\":1,\"Revision\":\"legacy\",\"EditorSid\":\"S-1-0-0\",\"Processes\":[]}");
   Check(legacy.Categories.Count==3,"Bestehendes Datenformat ergänzt Kategorien");
   var sample=Storage.Clone(c);var first=Structure.Entries(sample).First();sample.Processes.Add(first);
   Check(Structure.Entries(sample).Count()==12,"Gespeicherter Ablauf ersetzt virtuellen Eintrag ohne Duplikat");
   var empty=Storage.Clone(c);empty.Categories.Clear();Storage.Validate(empty);
   Check(empty.Categories.Count==3&&Structure.Entries(empty).Count()==12,"Leere Kategorienliste wird ergänzt");
   empty.Categories[0].Subcategories.RemoveAt(0);Storage.Validate(empty);Storage.Validate(empty);
   Check(empty.Categories[0].Subcategories.Count==5&&Structure.Entries(empty).Count()==12,"Unvollständige Kategorien ergänzt ohne Duplikate");
   var stale=Storage.Clone(c);var p=new Procedure{Title="Testprozess",Department="Verwaltung",Topic="Test"};p.Steps.Add(new Step{Id="1",Title="Freigabe?",Next="2",Otherwise="3"});p.Steps.Add(new Step{Id="2",Title="Ausführen"});p.Steps.Add(new Step{Id="3",Title="Rückfrage",Next="1"});c.Processes.Add(p);
   string previous=c.Revision;Storage.Save(root,c,previous);Check(Storage.Read(root).Processes[0].Steps.Count==3,"Schritte und Verzweigungen gespeichert");Check(c.Revision!=previous,"Neue Versionskennung");Check(Directory.GetFiles(Path.Combine(root,"Sicherungen")).Length==1,"Vorherigen Stand gesichert");
   Reject(delegate{Storage.Save(root,stale,stale.Revision);},"Veraltete Veröffentlichung verhindert");
   var invalid=Storage.Clone(c);invalid.Processes[0].Steps[0].Next="unbekannt";Reject(delegate{Storage.Validate(invalid);},"Unbekanntes Verbindungsziel abgelehnt");
   invalid=Storage.Clone(c);invalid.Processes[0].Steps[1].Id="1";Reject(delegate{Storage.Validate(invalid);},"Doppelte Schritt-ID abgelehnt");
   invalid=Storage.Clone(c);invalid.EditorSid="S-1-0-0";Reject(delegate{Storage.Save(root,invalid,c.Revision);},"Fremdes Bearbeiterkonto abgelehnt");
   invalid=Storage.Clone(c);invalid.Processes[0].Steps[0].Documents.Add(new Attachment{Name="Ungültig",File="..\\fremd.txt"});Reject(delegate{Storage.Validate(invalid);},"Unsicheren Dokumentpfad abgelehnt");
   using(var locked=new FileStream(Path.Combine(root,"bearbeitung.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Reject(delegate{Storage.Save(root,c,c.Revision);},"Gleichzeitigen Schreibzugriff verhindert");}
   Check(Storage.Read(root).Revision==c.Revision,"Fehlversuche verändern Stand nicht");
   File.WriteAllText(Path.Combine(root,".veroeffentlichen.lock"),"test");
   Reject(delegate{Storage.Save(root,c,c.Revision);},"Mac/Windows-Veröffentlichungssperre beachtet");
   File.Delete(Path.Combine(root,".veroeffentlichen.lock"));
   c.EditorMacId="mac:test:501";Storage.Save(root,c,c.Revision);
   Check(Storage.Read(root).EditorMacId=="mac:test:501","Mac-Freigabe bleibt bei Windows-Veröffentlichung erhalten");
   c.Templates.Add(new Attachment{Name="Testvorlage.pdf",File="test-vorlage.pdf"});
   Storage.Save(root,c,c.Revision);
   var withTemplates=Storage.Read(root);
   Check(withTemplates.Templates.Count==1&&withTemplates.Templates[0].Name=="Testvorlage.pdf","Vorlagen werden gemeinsam gespeichert und geladen");
   Check(withTemplates.Schema==3,"Neue Dateien sind gegen Schreiben alter App-Versionen geschützt");
   var badTemplate=Storage.Clone(c);badTemplate.Templates[0].File="..\\fremd.pdf";
   Reject(delegate{Storage.Validate(badTemplate);},"Unsicheren Vorlagenpfad abgelehnt");
   Check(legacy.Templates!=null&&legacy.Templates.Count==0,"Alte Handbücher erhalten eine leere Vorlagenliste");
   string fixture=Environment.GetEnvironmentVariable("PH_FIXTURE_OUT");
   if(!String.IsNullOrWhiteSpace(fixture)){
    Directory.CreateDirectory(Path.Combine(fixture,"Dokumente"));var export=Storage.Clone(c);export.EditorMacId="mac:fixture:501";
    File.WriteAllText(Path.Combine(fixture,"Dokumente","test-vorlage.pdf"),"Fixture only");
    File.WriteAllText(Path.Combine(fixture,"prozesse.json"),Storage.Json().Serialize(export),System.Text.Encoding.UTF8);
   }
   string imported=Environment.GetEnvironmentVariable("PH_FIXTURE_IN");
   if(!String.IsNullOrWhiteSpace(imported)){
    var fromMac=Storage.Read(imported);Check(fromMac.Schema==3&&fromMac.EditorMacId=="mac:fixture:501"&&fromMac.Processes.Count==1&&fromMac.Templates.Count==1,"Von Mac geschriebene Daten gelesen");
   }
   Console.WriteLine(checks+" Speicherprüfungen bestanden.");return 0;
  }catch(Exception e){Console.Error.WriteLine("Prüfung fehlgeschlagen: "+e.Message);return 1;}finally{try{Directory.Delete(root,true);}catch{}}
 }
}
