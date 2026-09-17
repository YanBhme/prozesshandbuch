using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.ComponentModel;

namespace Prozesshandbuch {
public class Attachment { public string Name {get;set;} public string File {get;set;} }
public class Step {
 public string Id {get;set;} public string Title {get;set;} public string Owner {get;set;}
 public string Instructions {get;set;} public string Checklist {get;set;}
 public string Next {get;set;} public string Otherwise {get;set;}
 public List<Attachment> Documents {get;set;}
 public Step(){Id="";Title="";Owner="";Instructions="";Checklist="";Next="";Otherwise="";Documents=new List<Attachment>();}
}
public class Procedure {
 public string Id {get;set;} public string Title {get;set;} public string Department {get;set;} public string Topic {get;set;}
 public string Summary {get;set;} public string Updated {get;set;} public List<Step> Steps {get;set;}
 public Procedure(){Id=Guid.NewGuid().ToString("N");Title="";Department="";Topic="";Summary="";Updated="";Steps=new List<Step>();}
 public override string ToString(){return (String.IsNullOrWhiteSpace(Topic)?"":Topic+" · ")+Title;}
}
public class Category {
 public string Name {get;set;} public string Description {get;set;} public List<string> Subcategories {get;set;}
}
public static class Structure {
 public static List<Category> Defaults(){return new List<Category>{
  new Category{Name="WEG-Verwaltung",Description="Wohnungseigentümergemeinschaften",Subcategories=new List<string>{
   "Checkliste Eigentümerwechsel","Wohngeldabrechnungen / Wirtschaftspläne","Anschreiben Jahresabrechnung",
   "Versammlungsnachbereitung (Eigentümerversammlung)","Protokoll Verwalterbestellung beglaubigen lassen"}},
  new Category{Name="Miethäuser",Description="Mietverwaltung",Subcategories=new List<string>{"Checkliste Mieterwechsel"}},
  new Category{Name="Allgemeine Prozesse",Description="Gelten für beide Bereiche / unternehmensweit",Subcategories=new List<string>{
   "Organisation & Ablage","Stammdaten & Formulare (SEPA, Kontaktdaten)","Versicherungen & Schadensfälle",
   "Übersichtsliste laufende Abrechnungen","Firmenpool","Personal (Onboarding, Hauswarte → Lohnabrechnung)"}}
 };}
 public static void Ensure(Catalog c){
  if(c.Categories==null)c.Categories=new List<Category>();
  foreach(var required in Defaults()){
   var found=c.Categories.FirstOrDefault(x=>x!=null&&x.Name==required.Name);
   if(found==null)c.Categories.Add(required);
   else {if(found.Subcategories==null)found.Subcategories=new List<string>();foreach(var name in required.Subcategories)if(!found.Subcategories.Contains(name))found.Subcategories.Add(name);}
  }
 }
 public static IEnumerable<Procedure> Entries(Catalog c){
  var seen=new HashSet<string>();int categoryIndex=0;
  foreach(var category in c.Categories){int subIndex=0;
   foreach(var sub in category.Subcategories){
    var existing=c.Processes.Where(p=>p.Department==category.Name&&(p.Topic==sub||p.Title==sub)).ToList();
    if(existing.Count==0)yield return new Procedure{Id="section-"+categoryIndex+"-"+subIndex,Title=sub,Department=category.Name,Topic=sub};
    else foreach(var p in existing){if(seen.Add(p.Id))yield return p;}
    subIndex++;
   }categoryIndex++;
  }
  foreach(var p in c.Processes)if(seen.Add(p.Id))yield return p;
 }
}
public class Catalog {
 public int Schema {get;set;} public string Revision {get;set;} public string EditorSid {get;set;}
 public string EditorMacId {get;set;} public string EditorName {get;set;} public List<Procedure> Processes {get;set;}
 public List<Category> Categories {get;set;}
 public List<Attachment> Templates {get;set;}
 public Catalog(){Schema=3;Processes=new List<Procedure>();Categories=Structure.Defaults();Templates=new List<Attachment>();}
}
public static class Storage {
 public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=20*1024*1024,RecursionLimit=100};}
 public static string Sid(){return WindowsIdentity.GetCurrent().User.Value;}
 public static T Clone<T>(T value){return Json().Deserialize<T>(Json().Serialize(value));}
 public static Catalog Read(string root){var c=Json().Deserialize<Catalog>(File.ReadAllText(Path.Combine(root,"prozesse.json"),Encoding.UTF8));Validate(c);return c;}
 public static void Validate(Catalog c){
  if(c==null||(c.Schema!=1&&c.Schema!=2&&c.Schema!=3)||String.IsNullOrWhiteSpace(c.EditorSid)||String.IsNullOrWhiteSpace(c.Revision)||c.Processes==null)throw new Exception("Die Prozessdatei hat kein unterstütztes Format.");
  Structure.Ensure(c);
  if(c.Templates==null)c.Templates=new List<Attachment>();
  foreach(var a in c.Templates)if(a==null||String.IsNullOrWhiteSpace(a.Name)||String.IsNullOrWhiteSpace(a.File)||Path.GetFileName(a.File)!=a.File||a.File.IndexOfAny(Path.GetInvalidFileNameChars())>=0||!EditorForm.Allowed.Contains(Path.GetExtension(a.File).ToLowerInvariant()))throw new Exception("Ungültiger Vorlagenverweis.");
  if(c.Categories.Any(x=>x==null||String.IsNullOrWhiteSpace(x.Name)||x.Subcategories==null||x.Subcategories.Any(String.IsNullOrWhiteSpace)))throw new Exception("Ungültige Kategorienstruktur.");
  var ids=new HashSet<string>();
  foreach(var p in c.Processes){
   if(p==null||String.IsNullOrWhiteSpace(p.Id)||!ids.Add(p.Id)||String.IsNullOrWhiteSpace(p.Title)||p.Steps==null)throw new Exception("Prozesse benötigen eindeutige Kennungen und einen Titel.");
   if(p.Steps.Count>60)throw new Exception("Ein Prozess darf höchstens 60 Schritte enthalten.");
   var stepIds=new HashSet<string>();
   foreach(var s in p.Steps){if(s==null||String.IsNullOrWhiteSpace(s.Id)||!stepIds.Add(s.Id)||String.IsNullOrWhiteSpace(s.Title)||s.Documents==null)throw new Exception("Jeder Schritt benötigt eine eindeutige Kennung und einen Titel.");
    foreach(var a in s.Documents){if(a==null||String.IsNullOrWhiteSpace(a.Name)||String.IsNullOrWhiteSpace(a.File)||Path.GetFileName(a.File)!=a.File||a.File.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new Exception("Ungültiger Dokumentverweis.");}
   }
   foreach(var s in p.Steps){foreach(var next in new[]{s.Next,s.Otherwise})if(!String.IsNullOrWhiteSpace(next)&&!stepIds.Contains(next))throw new Exception("Schritt "+s.Id+": Ziel '"+next+"' existiert nicht.");if(!String.IsNullOrWhiteSpace(s.Otherwise)&&String.IsNullOrWhiteSpace(s.Next))throw new Exception("Entscheidung "+s.Id+" benötigt ein Ja-Ziel.");}
  }
 }
 public static void Save(string root,Catalog c,string expected){
  Validate(c);if(c.EditorSid!=Sid())throw new Exception("Nur der hinterlegte Bearbeiter darf veröffentlichen.");
  string dest=Path.Combine(root,"prozesse.json"),lockPath=Path.Combine(root,".veroeffentlichen.lock");
  // Keep the old Windows share-mode lock during migration; all 0.6 clients also use the exclusive file lock.
  using(var gate=new FileStream(Path.Combine(root,"bearbeitung.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   FileStream cross;
   try{cross=new FileStream(lockPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);}
   catch(IOException){throw new Exception("Ein anderer Schreibvorgang läuft oder die Veröffentlichungssperre besteht noch. Nach einem Absturz darf die IT .veroeffentlichen.lock entfernen, wenn alle Apps geschlossen sind.");}
   try{using(cross){
    if(File.Exists(dest)){var actual=Read(root);if(actual.EditorSid!=Sid()||actual.Revision!=expected)throw new Exception("Der Stand wurde zwischenzeitlich geändert. Bitte schließen, aktualisieren und erneut bearbeiten.");}
    else if(expected!=null)throw new Exception("Die Prozessdatei fehlt. Veröffentlichung abgebrochen.");
    var next=Clone(c);next.Schema=3;next.Revision=Guid.NewGuid().ToString("N");
    string temp=Path.Combine(root,".prozesse-"+Guid.NewGuid().ToString("N")+".tmp");
    try{
     using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=Encoding.UTF8.GetBytes(Json().Serialize(next));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
     if(File.Exists(dest)){string backupDir=Path.Combine(root,"Sicherungen");Directory.CreateDirectory(backupDir);string backup=Path.Combine(backupDir,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");File.Copy(dest,backup,false);File.Replace(temp,dest,null);}
     else File.Move(temp,dest);
     c.Revision=next.Revision;c.Schema=3;
    }finally{if(File.Exists(temp))File.Delete(temp);}
   }}finally{File.Delete(lockPath);}
  }
 }
 public static Catalog New(){return new Catalog{Revision=Guid.NewGuid().ToString("N"),EditorSid=Sid(),EditorName=WindowsIdentity.GetCurrent().Name};}
}

public class SoftButton:Button {
 bool hover,down;
 public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?UI.Pale:Parent.BackColor);
  var rect=new Rectangle(1,1,Width-3,Height-3);Color fill=Enabled?(down?FlatAppearance.MouseDownBackColor:hover?FlatAppearance.MouseOverBackColor:BackColor):UI.Pale;
  using(var path=UI.Round(rect,11))using(var brush=new SolidBrush(fill)){g.FillPath(brush,path);}
  var flags=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;
  if(!ShowKeyboardCues)flags|=TextFormatFlags.HidePrefix;
  TextRenderer.DrawText(g,Text,Font,Rectangle.Inflate(rect,-8,-2),Enabled?ForeColor:UI.Muted,flags);
  if(Focused&&ShowFocusCues)using(var path=UI.Round(Rectangle.Inflate(rect,-3,-3),8))using(var pen=new Pen(UI.Orange,2))g.DrawPath(pen,path);
 }
}
public class Surface:Panel {
 public Color Fill=Color.White;
 public Surface(){DoubleBuffered=true;BackColor=UI.Pale;Padding=new Padding(18);ResizeRedraw=true;}
 protected override void OnPaintBackground(PaintEventArgs e){
  e.Graphics.Clear(Parent==null?UI.Pale:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=UI.Round(new Rectangle(0,0,Width-1,Height-1),16))using(var brush=new SolidBrush(Fill))e.Graphics.FillPath(brush,path);
 }
}
public class ContentPage:Panel { public ContentPage(string text){Text=text;BackColor=UI.Pale;} }
public class ModernTabs:UserControl {
 public List<ContentPage> TabPages=new List<ContentPage>();
 FlowLayoutPanel navigation=new FlowLayoutPanel();Panel body=new Panel();List<Button> buttons=new List<Button>();
 public ModernTabs(){BackColor=UI.Pale;navigation.Dock=DockStyle.Top;navigation.Height=58;navigation.WrapContents=false;navigation.AutoScroll=true;body.Dock=DockStyle.Fill;Controls.Add(body);Controls.Add(navigation);}
 public void Initialize(){
  foreach(Control c in navigation.Controls.Cast<Control>().ToArray())c.Dispose();navigation.Controls.Clear();buttons.Clear();
  for(int i=0;i<TabPages.Count;i++){int index=i;var b=UI.Button(TabPages[i].Text,delegate{SelectPage(index);});buttons.Add(b);navigation.Controls.Add(b);}
  navigation.Visible=TabPages.Count>1;
  if(TabPages.Count>0)SelectPage(0);
 }
 public void SelectPage(int index){
  body.Controls.Clear();var p=TabPages[index];p.Dock=DockStyle.Fill;body.Controls.Add(p);
  for(int i=0;i<buttons.Count;i++){var b=buttons[i];b.BackColor=i==index?UI.Ink:Color.White;b.ForeColor=i==index?Color.White:UI.Muted;
   b.FlatAppearance.MouseOverBackColor=i==index?Color.FromArgb(22,48,89):UI.Peach;b.FlatAppearance.MouseDownBackColor=UI.Peach;b.AccessibleDescription=i==index?"Ausgewählt":"";b.Invalidate();}
 }
 protected override void Dispose(bool disposing){if(disposing)foreach(var p in TabPages)if(!p.IsDisposed)p.Dispose();base.Dispose(disposing);}
}
public class ReadingCanvas:Control {
 Procedure process;string message="";readonly List<Tuple<Rectangle,Attachment>> links=new List<Tuple<Rectangle,Attachment>>();
 public event Action<Attachment> OpenAttachment;
 int preferred=460;
 public ReadingCanvas(){DoubleBuffered=true;BackColor=UI.Pale;Font=new Font("Segoe UI",12);Cursor=Cursors.Default;AccessibleName="Prozessanleitung";TabStop=true;}
 public void SetContent(Procedure p,string empty){process=p;message=empty??"";Invalidate();}
 protected override void OnMouseMove(MouseEventArgs e){Cursor=links.Any(x=>x.Item1.Contains(e.Location))?Cursors.Hand:Cursors.Default;base.OnMouseMove(e);}
 protected override void OnMouseClick(MouseEventArgs e){foreach(var link in links)if(link.Item1.Contains(e.Location)&&OpenAttachment!=null){OpenAttachment(link.Item2);break;}base.OnMouseClick(e);}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;links.Clear();
  float scale=g.DpiX/96f;g.ScaleTransform(scale,scale);int pad=28,gap=20,y=2,width=Math.Max(160,(int)(Width/scale)-4);
  using(var body=new Font("Segoe UI",16,FontStyle.Regular,GraphicsUnit.Pixel))using(var title=new Font("Segoe UI",21,FontStyle.Bold,GraphicsUnit.Pixel))using(var small=new Font("Segoe UI",14,FontStyle.Regular,GraphicsUnit.Pixel))using(var label=new Font("Segoe UI",15,FontStyle.Bold,GraphicsUnit.Pixel)){
   if(process==null){
    int h=Math.Max(270,Measure(g,message,body,width-pad*2)+pad*4+60);
    Card(g,new Rectangle(0,y,width,h),Color.White);
    Draw(g,"Alles an einem Ort.",title,UI.Ink,new Rectangle(pad,pad,width-pad*2,50));
    Draw(g,message,body,UI.Muted,new Rectangle(pad,pad+65,width-pad*2,h-pad-65));
    y+=h;
   }else{
    string overview=String.IsNullOrWhiteSpace(process.Summary)?"Für diesen Prozess ist noch kein Überblick hinterlegt.":process.Summary;
    int h=Measure(g,overview,body,width-pad*2)+pad*2+80;
    Card(g,new Rectangle(0,y,width,h),UI.Ink);
    Draw(g,"ÜBERBLICK",label,Color.FromArgb(255,168,116),new Rectangle(pad,y+pad,width-pad*2,30));
    Draw(g,overview,body,Color.White,new Rectangle(pad,y+pad+38,width-pad*2,h-pad-65));
    Draw(g,"Veröffentlicht · "+process.Updated,small,Color.FromArgb(194,210,233),new Rectangle(pad,y+h-pad-12,width-pad*2,28));y+=h+gap;
    foreach(var step in process.Steps){
     int left=pad+52,available=width-left-pad;
     int titleH=Measure(g,step.Title,title,available);
     int instructionH=Measure(g,step.Instructions??"",body,width-pad*2);
     string[] checks=(step.Checklist??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
     int checkHeight=checks.Sum(x=>Measure(g,x,body,width-pad*2-28)+10);
     string next=String.IsNullOrWhiteSpace(step.Otherwise)?"Weiter: "+(String.IsNullOrWhiteSpace(step.Next)?"Ende":step.Next):"Ja → "+step.Next+"     Nein → "+step.Otherwise;
     int cardH=pad*2+Math.Max(42,titleH)+36+instructionH+22+(checks.Length>0?46+checkHeight:0)+step.Documents.Sum(d=>Measure(g,"Dokument öffnen · "+d.Name,label,width-pad*2)+16)+46;
     Card(g,new Rectangle(0,y,width,cardH),Color.White);
     using(var brush=new SolidBrush(UI.Peach))g.FillEllipse(brush,pad,y+pad,38,38);
     Draw(g,step.Id,label,UI.OrangeText,new Rectangle(pad,y+pad+7,38,28),true);
     Draw(g,step.Title,title,UI.Ink,new Rectangle(left,y+pad,available,titleH+2));
     Draw(g,"Zuständig · "+step.Owner,small,UI.Muted,new Rectangle(left,y+pad+titleH+7,available,29));
     int cy=y+pad+Math.Max(42,titleH)+36;
     Draw(g,step.Instructions??"",body,UI.Ink,new Rectangle(pad,cy,width-pad*2,instructionH+2));cy+=instructionH+22;
     if(checks.Length>0){Draw(g,"Checkliste",label,UI.Ink,new Rectangle(pad,cy,width-pad*2,30));cy+=40;
      foreach(var check in checks){int lineH=Measure(g,check,body,width-pad*2-28);using(var pen=new Pen(UI.Line,1.5f))g.DrawRectangle(pen,pad,cy+5,15,15);Draw(g,check,body,UI.Muted,new Rectangle(pad+28,cy,width-pad*2-28,lineH+2));cy+=lineH+10;}
     }
     foreach(var doc in step.Documents){int docH=Measure(g,"Dokument öffnen · "+doc.Name,label,width-pad*2)+8;var rect=new Rectangle(pad,cy,width-pad*2,docH);Draw(g,"Dokument öffnen · "+doc.Name,label,UI.OrangeText,rect);links.Add(Tuple.Create(new Rectangle((int)(rect.X*scale),(int)(rect.Y*scale),(int)(rect.Width*scale),(int)(rect.Height*scale)),doc));cy+=docH+8;}
     using(var pen=new Pen(UI.Line))g.DrawLine(pen,pad,y+cardH-52,width-pad,y+cardH-52);
     Draw(g,next,small,UI.Muted,new Rectangle(pad,y+cardH-38,width-pad*2,28));y+=cardH+gap;
    }
   }
  }
  preferred=(int)((y+8)*scale);if(Height!=preferred&&IsHandleCreated)BeginInvoke((MethodInvoker)delegate{if(!IsDisposed&&Height!=preferred)Height=preferred;});
 }
 static void Card(Graphics g,Rectangle rect,Color color){using(var path=UI.Round(rect,18))using(var brush=new SolidBrush(color))g.FillPath(brush,path);}
 static int Measure(Graphics g,string text,Font f,int width){return (int)Math.Ceiling(g.MeasureString(String.IsNullOrEmpty(text)?" ":text,f,Math.Max(60,width)).Height);}
 static void Draw(Graphics g,string text,Font f,Color color,Rectangle rect,bool center=false){using(var brush=new SolidBrush(color))using(var format=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,Trimming=StringTrimming.None})g.DrawString(text,f,brush,rect,format);}
}

public static class UI {
 // Sampled from the supplied logo. Darker orange is reserved for readable text.
 public static Color Ink=Color.FromArgb(0,23,67),Blue=Ink,Orange=Color.FromArgb(229,84,17),
  OrangeText=Color.FromArgb(169,55,8),Pale=Color.FromArgb(244,246,250),
  Peach=Color.FromArgb(255,241,232),Muted=Color.FromArgb(88,102,124),Line=Color.FromArgb(218,225,235);
 public static Button Button(string text,EventHandler action){
  var b=new SoftButton{Text=text,AutoSize=true,MinimumSize=new Size(42,40),Padding=new Padding(12,6,12,6),
   Margin=new Padding(0,0,8,0),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Ink,Cursor=Cursors.Hand,
   Font=new Font("Segoe UI",11),UseVisualStyleBackColor=false};
  b.FlatAppearance.BorderColor=Line;b.FlatAppearance.MouseOverBackColor=Peach;b.FlatAppearance.MouseDownBackColor=Color.FromArgb(255,222,203);
  b.Click+=action;return b;
 }
 public static void Primary(Button b){b.BackColor=Ink;b.ForeColor=Color.White;b.FlatAppearance.BorderColor=Ink;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(20,49,91);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(30,65,112);}
 public static void ThemeTabs(ModernTabs t){t.Initialize();}
 public static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=Math.Max(2,Math.Min(radius*2,Math.Min(r.Width,r.Height)));p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public static Image Logo(){
  using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("BrandLogo")){
   if(stream==null)return null;
   using(var original=Image.FromStream(stream))return new Bitmap(original);
  }
 }
 public static void Error(Exception e){MessageBox.Show(e.Message,"Prozesshandbuch",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 public static bool Confirm(string message){return MessageBox.Show(message,"Prozesshandbuch",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
 public static Label Label(string text){return new Label{Text=text,AutoSize=true,ForeColor=Muted,Margin=new Padding(0,9,0,5),Font=new Font("Segoe UI",11)};}
}
public class MainForm:Form {
 string root;Catalog catalog=new Catalog();bool online;bool reading,readAgain;int dataGeneration;string config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Prozesshandbuch","ordner.txt");
 TextBox search=new TextBox();ComboBox departments=new ComboBox();ListBox list=new ListBox();Label status=new Label();Label heading=new Label();Label metadata=new Label(),resultCount=new Label(),role=new Label();
 RichTextBox instructions=new RichTextBox();ListView docs=new ListView();Panel graph=new Panel();ModernTabs tabs=new ModernTabs();ReadingCanvas reader=new ReadingCanvas();Button edit,add,delete;
 Panel screenHost=new Panel();Control instructionsScreen;Panel homeScreen,templatesScreen;
 ListView templateList=new ListView();Label templateStatus=new Label();Button uploadTemplate;TextBox templateSearch=new TextBox();
 TableLayoutPanel categoryNavigation=new TableLayoutPanel();List<Button> categoryButtons=new List<Button>();
 System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 Procedure Selected {get{return list.SelectedItem as Procedure;}}
 bool CanEdit {get{return online&&catalog!=null&&catalog.EditorSid==Storage.Sid();}}
 public MainForm(){
  Text="Bruno Grüttner | Prozesshandbuch 0.6.1";Font=new Font("Segoe UI",12);BackColor=UI.Pale;ForeColor=UI.Ink;
  Size=new Size(1380,920);MinimumSize=new Size(1120,740);StartPosition=FormStartPosition.CenterScreen;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
  var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty};
  shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  var sidebar=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.White,ColumnCount=1,RowCount=9,Padding=new Padding(24,20,24,18),Margin=Padding.Empty};
  foreach(int height in new[]{70,40,26,44,28,184,34})sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
  sidebar.RowStyles[2].SizeType=SizeType.AutoSize;sidebar.RowStyles[4].SizeType=SizeType.AutoSize;sidebar.RowStyles[5].SizeType=SizeType.AutoSize;
  sidebar.RowStyles.Add(new RowStyle(SizeType.Percent,100));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
  var logo=new PictureBox{Image=UI.Logo(),Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="Bruno Grüttner Grundstücksverwaltungen",Margin=new Padding(0,0,0,16)};
  sidebar.Controls.Add(logo,0,0);
  var section=new Label{Text="Prozessbibliothek",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",15,FontStyle.Bold)};sidebar.Controls.Add(section,0,1);
  sidebar.Controls.Add(new Label{Text="Suchen",AutoSize=true,Margin=new Padding(0,6,0,6),ForeColor=UI.Muted},0,2);
  var searchBox=new Surface{Dock=DockStyle.Fill,Fill=UI.Pale,Padding=new Padding(14,10,14,6),Margin=new Padding(0,0,0,4)};
  search.BorderStyle=BorderStyle.None;search.BackColor=UI.Pale;search.Dock=DockStyle.Fill;search.AccessibleName="Prozessinhalte durchsuchen";searchBox.Controls.Add(search);sidebar.Controls.Add(searchBox,0,3);
  sidebar.Controls.Add(new Label{Text="Kategorie",AutoSize=true,Margin=new Padding(0,8,0,6),ForeColor=UI.Muted},0,4);
  departments.DropDownStyle=ComboBoxStyle.DropDownList;
  categoryNavigation.AutoSize=true;categoryNavigation.Dock=DockStyle.Fill;categoryNavigation.ColumnCount=1;categoryNavigation.Margin=Padding.Empty;categoryNavigation.AutoScroll=true;
  sidebar.Controls.Add(categoryNavigation,0,5);
  resultCount.Dock=DockStyle.Fill;resultCount.TextAlign=ContentAlignment.MiddleLeft;resultCount.ForeColor=UI.Muted;resultCount.Font=new Font("Segoe UI",10.5f);sidebar.Controls.Add(resultCount,0,6);
  list.Dock=DockStyle.Fill;list.BackColor=Color.White;list.BorderStyle=BorderStyle.None;list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=116;list.IntegralHeight=false;list.DrawItem+=DrawProcess;sidebar.Controls.Add(list,0,7);
  var folder=UI.Button("Einstellungen · Datenordner",delegate{ChooseFolder();});folder.Dock=DockStyle.Fill;folder.BackColor=UI.Pale;sidebar.Controls.Add(folder,0,8);
  shell.Controls.Add(sidebar,0,0);
  var workspace=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(36,24,32,12),Margin=Padding.Empty};
  workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,62));workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));workspace.RowStyles.Add(new RowStyle(SizeType.Percent,100));workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
  var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
  role.Dock=DockStyle.Fill;role.TextAlign=ContentAlignment.MiddleLeft;role.Font=new Font("Segoe UI",11);role.ForeColor=UI.Muted;top.Controls.Add(UI.Button("← Hauptmenü",delegate{ShowScreen(homeScreen);}),0,0);top.Controls.Add(role,1,0);
  var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=false,FlowDirection=FlowDirection.LeftToRight,Margin=Padding.Empty};
  actions.Controls.Add(UI.Button("Aktualisieren",delegate{RefreshCatalog(false);}));
  add=UI.Button("+ Prozess",delegate{EditProcess(true);});UI.Primary(add);edit=UI.Button("Bearbeiten",delegate{EditProcess(false);});delete=UI.Button("Löschen",delegate{DeleteProcess();});
  actions.Controls.Add(edit);actions.Controls.Add(delete);actions.Controls.Add(add);top.Controls.Add(actions,2,0);workspace.Controls.Add(top,0,0);
  var titleArea=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,RowCount=2,Padding=new Padding(0,10,0,26)};
  titleArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  heading.AutoSize=true;heading.Dock=DockStyle.Fill;heading.Font=new Font("Segoe UI",26,FontStyle.Bold);heading.Text="Willkommen.";heading.Margin=new Padding(0,0,0,10);
  metadata.AutoSize=true;metadata.Dock=DockStyle.Fill;metadata.ForeColor=UI.Muted;metadata.Font=new Font("Segoe UI",11);metadata.Margin=Padding.Empty;
  titleArea.Controls.Add(heading,0,0);titleArea.Controls.Add(metadata,0,1);workspace.Controls.Add(titleArea,0,1);
  tabs.Dock=DockStyle.Fill;var first=new ContentPage("Anleitung");var second=new ContentPage("Ablauf");var third=new ContentPage("Dokumente");var fourth=new ContentPage("Textansicht");
  tabs.TabPages.Add(first);
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=UI.Pale};reader.Location=Point.Empty;reader.Width=700;reader.Height=460;reader.OpenAttachment+=OpenDocumentFile;scroll.Controls.Add(reader);
  scroll.Resize+=delegate{reader.Width=Math.Max(200,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-2);reader.Invalidate();};first.Controls.Add(scroll);
  instructions.Dock=DockStyle.Fill;instructions.ReadOnly=true;instructions.BackColor=Color.White;instructions.BorderStyle=BorderStyle.None;instructions.Font=Font;instructions.DetectUrls=false;instructions.AccessibleName="Vollständige Arbeitsanweisung als auswählbarer Text";
  var textSurface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(24)};textSurface.Controls.Add(instructions);fourth.Controls.Add(textSurface);
  graph.Dock=DockStyle.Fill;graph.AutoScroll=true;graph.BackColor=UI.Pale;second.Controls.Add(graph);
  docs.Dock=DockStyle.Fill;docs.View=View.Details;docs.FullRowSelect=true;docs.BorderStyle=BorderStyle.None;docs.ForeColor=UI.Ink;docs.HideSelection=false;docs.Columns.Add("Dokument",340);docs.Columns.Add("Prozessschritt",250);
  docs.DoubleClick+=delegate{OpenDocument();};docs.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter)OpenDocument();};
  var docSurface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(24)};var open=UI.Button("Dokument öffnen",delegate{OpenDocument();});open.Dock=DockStyle.Bottom;var copy=UI.Button("Kopie speichern …",delegate{if(docs.SelectedItems.Count>0)SaveCopy((Attachment)docs.SelectedItems[0].Tag);});copy.Dock=DockStyle.Bottom;docSurface.Controls.Add(docs);docSurface.Controls.Add(open);docSurface.Controls.Add(copy);third.Controls.Add(docSurface);
  UI.ThemeTabs(tabs);workspace.Controls.Add(tabs,0,2);
  status.Dock=DockStyle.Fill;status.ForeColor=UI.Muted;status.Font=new Font("Segoe UI",10.5f);status.AutoEllipsis=true;status.Padding=new Padding(0,12,0,0);workspace.Controls.Add(status,0,3);
  shell.Controls.Add(workspace,1,0);instructionsScreen=shell;screenHost.Dock=DockStyle.Fill;Controls.Add(screenHost);
  homeScreen=BuildHome();templatesScreen=BuildTemplates();foreach(var screen in new Control[]{instructionsScreen,templatesScreen,homeScreen}){screen.Dock=DockStyle.Fill;screenHost.Controls.Add(screen);}ShowScreen(homeScreen);
  search.TextChanged+=delegate{Filter();};departments.SelectedIndexChanged+=delegate{Filter();};list.SelectedIndexChanged+=delegate{Render();};
  Shown+=delegate{list.ItemHeight=(int)(116*DeviceScale());Populate();sidebar.RowStyles[3].Height=search.PreferredHeight+24*DeviceScale();if(File.Exists(config)){try{root=File.ReadAllText(config,Encoding.UTF8).Trim();RefreshCatalog(true);}catch{online=false;}}Render();SetStatus();UpdateButtons();};
  timer.Interval=30000;timer.Tick+=delegate{if(!String.IsNullOrWhiteSpace(root))RefreshCatalog(true);};timer.Start();FormClosed+=delegate{timer.Dispose();if(logo.Image!=null)logo.Image.Dispose();foreach(var screen in new Control[]{homeScreen,templatesScreen,instructionsScreen})if(screen!=null&&!screen.IsDisposed)screen.Dispose();};
 }
 void ShowScreen(Control screen){
  foreach(Control item in screenHost.Controls)item.Visible=item==screen;screen.BringToFront();
  if(screen==templatesScreen)UpdateTemplates();
 }
 Panel BuildHome(){
  var page=new Panel{BackColor=UI.Pale,Padding=new Padding(52),AutoScroll=true};
  var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=6};
  var brand=new PictureBox{Image=UI.Logo(),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(460,112),Margin=new Padding(0,0,0,28)};
  page.Disposed+=delegate{if(brand.Image!=null)brand.Image.Dispose();};
  layout.Controls.Add(brand);
  layout.Controls.Add(new Label{Text="Was möchtest du öffnen?",AutoSize=true,Font=new Font("Segoe UI",26,FontStyle.Bold),Margin=new Padding(0,0,0,12)});
  layout.Controls.Add(new Label{Text="Arbeitsabläufe nachlesen oder die passende Vorlage finden.",AutoSize=true,ForeColor=UI.Muted,Margin=new Padding(0,0,0,32)});
  var choices=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2,RowCount=1};
  choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
  var guide=UI.Button("Anleitung",delegate{ShowScreen(instructionsScreen);});UI.Primary(guide);
  var templates=UI.Button("Vorlagen",delegate{ShowScreen(templatesScreen);});
  foreach(var b in new[]{guide,templates}){b.Font=new Font("Segoe UI",22,FontStyle.Bold);b.MinimumSize=new Size(260,150);b.Dock=DockStyle.Fill;b.Margin=new Padding(0,0,20,24);}
  choices.Controls.Add(guide,0,0);choices.Controls.Add(templates,1,0);layout.Controls.Add(choices);
  var settings=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};
  settings.Controls.Add(UI.Button("Einstellungen · Datenordner",delegate{ChooseFolder();}));
  settings.Controls.Add(UI.Button("Bearbeiterkonten",delegate{EditorAccounts();}));
  settings.Controls.Add(UI.Button("Hilfe",delegate{ShowHelp();}));layout.Controls.Add(settings);page.Controls.Add(layout);return page;
 }
 Panel BuildTemplates(){
  var page=new Panel{BackColor=UI.Pale,Padding=new Padding(36)};
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};
  layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  var top=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
  top.Controls.Add(UI.Button("← Hauptmenü",delegate{ShowScreen(homeScreen);}));
  top.Controls.Add(UI.Button("Aktualisieren",delegate{RefreshCatalog(false);UpdateTemplates();}));
  top.Controls.Add(UI.Button("Einstellungen · Datenordner",delegate{ChooseFolder();}));
  uploadTemplate=UI.Button("+ Vorlage hochladen",delegate{UploadTemplate();});UI.Primary(uploadTemplate);top.Controls.Add(uploadTemplate);layout.Controls.Add(top,0,0);
  layout.Controls.Add(new Label{Text="Vorlagen",AutoSize=true,Font=new Font("Segoe UI",26,FontStyle.Bold),Margin=new Padding(0,26,0,20)},0,1);
  var searchArea=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(0,0,0,20)};
  searchArea.Controls.Add(new Label{Text="Vorlagen durchsuchen",AutoSize=true,Margin=new Padding(0,0,0,8)});
  templateSearch.Dock=DockStyle.Top;templateSearch.AccessibleName="Vorlagen durchsuchen";templateSearch.TextChanged+=delegate{UpdateTemplates();};searchArea.Controls.Add(templateSearch);layout.Controls.Add(searchArea,0,2);
  var card=new Surface{Dock=DockStyle.Fill,Padding=new Padding(24)};
  templateList.Dock=DockStyle.Fill;templateList.View=View.Details;templateList.FullRowSelect=true;templateList.MultiSelect=false;templateList.HideSelection=false;templateList.BorderStyle=BorderStyle.None;
  templateList.Columns.Add("Vorlage",520);templateList.Columns.Add("Dateityp",120);
  templateList.DoubleClick+=delegate{OpenSelectedTemplate();};templateList.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter)OpenSelectedTemplate();};
  card.Controls.Add(templateList);layout.Controls.Add(card,0,3);
  var bottom=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1};
  templateStatus.AutoSize=true;templateStatus.ForeColor=UI.Muted;templateStatus.Margin=new Padding(0,14,0,14);bottom.Controls.Add(templateStatus);
  var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};buttons.Controls.Add(UI.Button("Öffnen",delegate{OpenSelectedTemplate();}));
  buttons.Controls.Add(UI.Button("Kopie speichern …",delegate{if(templateList.SelectedItems.Count>0)SaveCopy((Attachment)templateList.SelectedItems[0].Tag);}));
  bottom.Controls.Add(buttons);layout.Controls.Add(bottom,0,4);page.Controls.Add(layout);return page;
 }
 void UpdateTemplates(){
  if(uploadTemplate==null)return;
  uploadTemplate.Visible=CanEdit;
  string selected=templateList.SelectedItems.Count>0?((Attachment)templateList.SelectedItems[0].Tag).File:null;
  templateList.BeginUpdate();templateList.Items.Clear();
  foreach(var a in (catalog.Templates??new List<Attachment>()).Where(x=>x.Name.IndexOf(templateSearch.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0)){
   var item=new ListViewItem(a.Name);item.SubItems.Add(Path.GetExtension(a.File).TrimStart('.').ToUpperInvariant());item.Tag=a;templateList.Items.Add(item);if(a.File==selected)item.Selected=true;
  }
  templateList.EndUpdate();
  templateStatus.Text=!online?(String.IsNullOrWhiteSpace(root)?"Über Einstellungen den gemeinsamen Datenordner verbinden, um Vorlagen zu laden.":"Verbindung unterbrochen. Angezeigter Stand möglicherweise veraltet."):
   templateList.Items.Count==0?(CanEdit?"Noch keine passenden Vorlagen. Über „Vorlage hochladen“ eine Datei bereitstellen.":"Noch keine passenden Vorlagen verfügbar."):templateList.Items.Count+" Vorlagen · Über „Kopie speichern“ lokal herunterladen.";
 }
 void OpenSelectedTemplate(){if(templateList.SelectedItems.Count>0)OpenDocumentFile((Attachment)templateList.SelectedItems[0].Tag);}
 void UploadTemplate(){
  if(!CanEdit)return;
  using(var d=new OpenFileDialog{Title="Vorlage hochladen",Multiselect=true,Filter="Dokumente|*.pdf;*.docx;*.xlsx;*.pptx;*.txt;*.csv;*.png;*.jpg;*.jpeg"}){
   if(d.ShowDialog(this)!=DialogResult.OK)return;
   timer.Stop();var copied=new List<string>();bool saved=false;
   try{
    var current=Storage.Read(root);if(current.EditorSid!=Storage.Sid())throw new Exception("Für dieses Konto ist keine Bearbeitung freigegeben.");
    foreach(var file in d.FileNames){
     string ext=Path.GetExtension(file).ToLowerInvariant();if(!EditorForm.Allowed.Contains(ext))throw new Exception("Dateityp nicht unterstützt.");
     string name=Guid.NewGuid().ToString("N")+ext;Directory.CreateDirectory(Path.Combine(root,"Dokumente"));string dest=Path.Combine(root,"Dokumente",name);copied.Add(dest);File.Copy(file,dest,false);
     current.Templates.Add(new Attachment{Name=Path.GetFileName(file),File=name});
    }
    Storage.Save(root,current,current.Revision);saved=true;dataGeneration++;catalog=current;online=true;UpdateTemplates();SetStatus();
   }catch(Exception e){UI.Error(new Exception("Vorlagen nicht veröffentlicht.\n\n"+e.Message));}
   finally{if(!saved)foreach(var file in copied){try{File.Delete(file);}catch{}}timer.Start();}
  }
 }
 void SaveCopy(Attachment a){
  try{
   string source=Path.Combine(root,"Dokumente",a.File);if(!File.Exists(source))throw new Exception("Datei nicht erreichbar. Bitte die Verbindung zum Datenordner prüfen.");
   using(var d=new SaveFileDialog{Title="Kopie speichern",FileName=Path.GetFileName(a.Name),DefaultExt=Path.GetExtension(a.File).TrimStart('.'),AddExtension=true,OverwritePrompt=true,Filter="Originaldatei|*"+Path.GetExtension(a.File)}){
    if(d.ShowDialog(this)!=DialogResult.OK)return;
    string destination=Path.GetFullPath(d.FileName);string shared=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    if(destination.StartsWith(shared,StringComparison.OrdinalIgnoreCase))throw new Exception("Bitte einen Speicherort außerhalb des gemeinsamen Prozessordners wählen.");
    string temporary=Path.Combine(Path.GetDirectoryName(destination),".prozesshandbuch-"+Guid.NewGuid().ToString("N")+".tmp");
    try{File.Copy(source,temporary,false);if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);}
    finally{if(File.Exists(temporary))File.Delete(temporary);}
   }
  }catch(Exception e){UI.Error(e);}
 }
 float DeviceScale(){using(var g=CreateGraphics())return g.DpiX/96f;}
 void DrawProcess(object sender,DrawItemEventArgs e){
  if(e.Index<0||e.Index>=list.Items.Count)return;var p=(Procedure)list.Items[e.Index];bool active=(e.State&DrawItemState.Selected)!=0;
  float scale=e.Graphics.DpiX/96f;int gap=(int)(6*scale),pad=(int)(14*scale);
  Rectangle card=new Rectangle(e.Bounds.X,e.Bounds.Y,e.Bounds.Width,e.Bounds.Height-gap);
  using(var fill=new SolidBrush(active?UI.Peach:UI.Pale))using(var path=UI.Round(Rectangle.Inflate(card,-1,-2),12))e.Graphics.FillPath(fill,path);
  
  var titleRect=new Rectangle(card.X+pad,card.Y+(int)(11*scale),card.Width-pad*2,(int)(60*scale));
  var metaRect=new Rectangle(card.X+pad,card.Y+(int)(78*scale),card.Width-pad*2,(int)(23*scale));
  using(var f=new Font("Segoe UI",12,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,p.Title,f,titleRect,UI.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.WordBreak);
  string info=p.Department+(p.Steps.Count==0?" · Ablauf folgt":" · "+p.Steps.Count+" Schritte");
  using(var small=new Font("Segoe UI",10.5f))TextRenderer.DrawText(e.Graphics,info,small,metaRect,UI.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
  if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(card,-3,-3),UI.Ink,active?UI.Peach:UI.Pale);
 }
 void UpdateButtons(){UpdateTemplates();add.Visible=edit.Visible=delete.Visible=CanEdit;add.Enabled=CanEdit;edit.Enabled=CanEdit&&Selected!=null;delete.Enabled=CanEdit&&Selected!=null&&catalog.Processes.Any(p=>p.Id==Selected.Id);}
 void EditorAccounts(){
  using(var f=new Form{Text="Bearbeiterkonten",Size=new Size(680,460),MinimumSize=new Size(680,460),StartPosition=FormStartPosition.CenterParent,Font=new Font("Segoe UI",11),BackColor=UI.Pale,AutoScaleMode=AutoScaleMode.Dpi}){
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),RowCount=7,ColumnCount=1};f.Controls.Add(layout);
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
   layout.Controls.Add(UI.Label("Deine Windows-Kennung"),0,0);
   var sid=new TextBox{Text=Storage.Sid(),ReadOnly=true,Dock=DockStyle.Fill};layout.Controls.Add(sid,0,1);
   layout.Controls.Add(UI.Label("Deine Mac-Kennung (aus den Einstellungen der Mac-App)"),0,2);
   var mac=new TextBox{Text=catalog.EditorMacId??"",ReadOnly=!CanEdit,Dock=DockStyle.Fill};layout.Controls.Add(mac,0,3);
   layout.Controls.Add(new Label{Text=CanEdit?"Du kannst dein eigenes Mac-Konto zusätzlich freigeben. Nach dem Speichern benötigen alle Windows-Nutzer Version 0.6.1 oder neuer. Die tatsächlichen Lese- und Änderungsrechte setzt eure IT am Datenordner.":"Für ein am Mac angelegtes Handbuch: Übertrage deine Windows-Kennung in den Einstellungen der Mac-App. Nur der bereits freigegebene Bearbeiter darf Konten freigeben.",Dock=DockStyle.Fill,Padding=new Padding(0,12,0,0)},0,4);
   var save=UI.Button("Mac-Freigabe speichern",delegate{
    if(!CanEdit)return;string id=mac.Text.Trim();if(id!=""&&!System.Text.RegularExpressions.Regex.IsMatch(id,@"^mac:[A-Za-z0-9.:-]+$")){UI.Error(new Exception("Bitte die vollständige Mac-Kennung übernehmen."));return;}
    try{var next=Storage.Clone(catalog);next.EditorMacId=id;Storage.Save(root,next,catalog.Revision);dataGeneration++;catalog=next;UpdateButtons();SetStatus();f.Close();}catch(Exception e){UI.Error(e);}
   });save.Enabled=CanEdit;layout.Controls.Add(save,0,5);f.ShowDialog(this);
  }
 }
 void ChooseFolder(){
  using(var d=new FolderBrowserDialog{Description="Gemeinsamen Prozessordner auf dem Netzlaufwerk auswählen",ShowNewFolderButton=true}){
   if(d.ShowDialog()!=DialogResult.OK)return;string chosen=d.SelectedPath;
   try{if(!File.Exists(Path.Combine(chosen,"prozesse.json"))){
    if(!UI.Confirm("Dieser Ordner enthält noch kein Prozesshandbuch. Möchtest du hier ein neues Handbuch anlegen und dein Windows-Konto als alleinigen Bearbeiter hinterlegen?\n\nNur beim ersten Einrichten durch den Verantwortlichen auswählen."))return;
    if(!UI.Confirm("Bitte zuvor die Ordnerrechte durch eure IT einrichten lassen: dein Konto darf ändern, Mitarbeiter dürfen nur lesen. Diese App verändert keine Windows-Berechtigungen.\n\nIst dieser Ordner für das gemeinsame Handbuch vorgesehen?"))return;
    var initial=Storage.New();Storage.Save(chosen,initial,null);
   }
   var loaded=Storage.Read(chosen);Directory.CreateDirectory(Path.GetDirectoryName(config));File.WriteAllText(config,chosen,Encoding.UTF8);dataGeneration++;root=chosen;catalog=loaded;online=true;Populate();
   }catch(Exception e){UI.Error(e);}
  }
 }
 void RefreshCatalog(bool quiet){
  if(String.IsNullOrWhiteSpace(root)||IsDisposed)return;
  if(reading){readAgain=true;return;}
  reading=true;string requestedRoot=root;int generation=dataGeneration;
  var worker=new BackgroundWorker();
  worker.DoWork+=delegate(object sender,DoWorkEventArgs e){e.Result=Storage.Read(requestedRoot);};
  worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){
   reading=false;worker.Dispose();if(IsDisposed||Disposing)return;
   if(generation==dataGeneration&&requestedRoot==root){
    if(e.Error!=null){online=false;SetStatus();if(!quiet)UI.Error(new Exception("Der Prozessordner ist nicht lesbar. Bitte Netzlaufwerk/VPN und Ordner prüfen.\n\n"+e.Error.Message));}
    else {var next=(Catalog)e.Result;bool changed=catalog.Revision!=next.Revision;catalog=next;online=true;if(changed||!quiet)Populate();else SetStatus();}
    UpdateButtons();
   }
   if(readAgain){readAgain=false;RefreshCatalog(true);}
  };
  worker.RunWorkerAsync();
 }
 void Populate(){string dep=departments.SelectedItem as string;departments.Items.Clear();departments.Items.Add("Alle Kategorien");foreach(var d in catalog.Categories.Select(x=>x.Name).Concat(catalog.Processes.Select(p=>p.Department??"").Where(x=>x!="")).Distinct())departments.Items.Add(d);departments.SelectedItem=dep!=null&&departments.Items.Contains(dep)?dep:"Alle Kategorien";BuildCategoryNavigation();Filter();SetStatus();UpdateTemplates();}
 void BuildCategoryNavigation(){
  foreach(var b in categoryButtons)b.Dispose();categoryButtons.Clear();categoryNavigation.Controls.Clear();categoryNavigation.RowStyles.Clear();
  var values=departments.Items.Cast<string>().ToList();categoryNavigation.RowCount=values.Count;
  for(int i=0;i<values.Count;i++){
   string value=values[i];string label=value=="Alle Kategorien"?"Alle Bereiche":value;
   var b=UI.Button(label,delegate{search.Clear();departments.SelectedItem=value;});
   b.Tag=value;b.Dock=DockStyle.Fill;b.Margin=new Padding(0,0,0,3);b.Padding=new Padding(8,4,8,4);
   categoryButtons.Add(b);categoryNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));categoryNavigation.Controls.Add(b,0,i);
  }
 }
 void HighlightCategory(){
  foreach(var b in categoryButtons){bool active=(string)b.Tag==(string)departments.SelectedItem;b.BackColor=active?UI.Ink:UI.Pale;b.ForeColor=active?Color.White:UI.Ink;b.FlatAppearance.MouseOverBackColor=active?UI.Ink:UI.Peach;b.Invalidate();}
 }
 void SetStatus(){
  role.Text=String.IsNullOrWhiteSpace(root)?"Kategorienübersicht":!online?"Verbindung prüfen":CanEdit?"Bearbeitung":"Leseansicht";
  role.BackColor=UI.Pale;role.ForeColor=UI.Muted;
  status.Text=String.IsNullOrWhiteSpace(root)?"Datenordner noch nicht verbunden · Einrichtung über Einstellungen.":!online?"Netzlaufwerk nicht erreichbar – angezeigter Stand möglicherweise veraltet. Bearbeitung gesperrt.\n"+root:
   (CanEdit?"Bearbeitung: "+catalog.EditorName:"Leseansicht")+"  ·  "+(catalog==null?0:catalog.Categories.Count)+" Kategorien  ·  "+(catalog==null?0:catalog.Processes.Count)+" veröffentlichte Abläufe  ·  Aktualisierung alle 30 Sekunden\n"+root;
 }
 void Filter(){
  HighlightCategory();
  string id=Selected==null?null:Selected.Id;list.BeginUpdate();list.Items.Clear();
  if(catalog!=null){string q=search.Text.Trim();string dep=departments.SelectedItem as string;foreach(var p in Structure.Entries(catalog)){
   string text=String.Join(" ",new[]{p.Title,p.Department,p.Topic,p.Summary})+" "+String.Join(" ",p.Steps.Select(s=>String.Join(" ",new[]{s.Title,s.Owner,s.Instructions,s.Checklist})+" "+String.Join(" ",s.Documents.Select(a=>a.Name))));
   if((dep==null||dep=="Alle Kategorien"||p.Department==dep)&&(q==""||text.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0))list.Items.Add(p);
  }}
  for(int i=0;i<list.Items.Count;i++)if(((Procedure)list.Items[i]).Id==id)list.SelectedIndex=i;
  if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;list.EndUpdate();resultCount.Text=list.Items.Count==1?"1 Eintrag":list.Items.Count+" Einträge";Render();
 }
 void Render(){
  docs.Items.Clear();foreach(Control c in graph.Controls.Cast<Control>().ToArray()){graph.Controls.Remove(c);c.Dispose();}instructions.Clear();var p=Selected;metadata.Text="";
  if(p==null){heading.Text=catalog!=null&&catalog.Processes.Count>0?"Keine Treffer.":"Willkommen.";metadata.Text="Euer Wissen. Klar strukturiert und schnell gefunden.";instructions.Text=catalog==null?"Wähle links unten euren gemeinsamen Prozessordner aus.":catalog.Processes.Count==0?(CanEdit?"Lege über „+ Prozess“ den ersten Arbeitsablauf an.\n\nErfasse Abteilung, Thema, Schritte, Zuständigkeiten, Anweisungen und Dokumente.":"Es sind noch keine Prozesse veröffentlicht."):"Passe den Suchbegriff oder die Abteilung an.";reader.SetContent(null,instructions.Text);UpdateButtons();return;}
  heading.Text=p.Title;metadata.Text=p.Department+(p.Steps.Count==0?"  ·  Ablauf wird ergänzt":"  ·  "+p.Steps.Count+" Schritte");
  if(p.Steps.Count==0)Append("Unterkategorie angelegt. Der Prozessablauf wird im nächsten Schritt ergänzt.\n\n",false);
  if(!String.IsNullOrWhiteSpace(p.Summary)){Append("Überblick\n",true);Append(p.Summary+"\n\n",false);}
  if(!String.IsNullOrWhiteSpace(p.Updated))Append("Zuletzt veröffentlicht: "+p.Updated+"\n\n",false);
  foreach(var s in p.Steps){Append(s.Id+"  ·  "+s.Title+"\n",true);Append("Zuständig: "+s.Owner+"\n\n"+s.Instructions+"\n",false);
   if(!String.IsNullOrWhiteSpace(s.Checklist)){Append("\nCheckliste\n",true);foreach(var line in s.Checklist.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))Append("☐  "+line+"\n",false);}
   Append("\n"+(String.IsNullOrWhiteSpace(s.Otherwise)?"Weiter: "+(String.IsNullOrWhiteSpace(s.Next)?"Ende":s.Next):"Ja → "+s.Next+"    Nein → "+s.Otherwise)+"\n\n",false);
   foreach(var a in s.Documents){var item=new ListViewItem(a.Name);item.SubItems.Add(s.Id+" · "+s.Title);item.Tag=a;docs.Items.Add(item);}
  }
  instructions.SelectionStart=0;instructions.ScrollToCaret();reader.SetContent(p.Steps.Count==0?null:p,p.Steps.Count==0?"Diese Unterkategorie ist angelegt.\n\nDer Prozessablauf wird im nächsten Schritt ergänzt.":"");graph.Controls.Add(new Diagram(p));UpdateButtons();
 }
 void Append(string text,bool bold){instructions.SelectionStart=instructions.TextLength;instructions.SelectionColor=bold?UI.Ink:UI.Muted;using(var f=new Font("Segoe UI",bold?14:12,bold?FontStyle.Bold:FontStyle.Regular)){instructions.SelectionFont=f;instructions.AppendText(text);}}
 void OpenDocument(){if(docs.SelectedItems.Count>0)OpenDocumentFile((Attachment)docs.SelectedItems[0].Tag);}
 void OpenDocumentFile(Attachment a){
  try{string file=Path.Combine(root,"Dokumente",a.File);if(!File.Exists(file))throw new Exception("Dokument nicht erreichbar. Bitte das Netzlaufwerk prüfen.");if(!EditorForm.Allowed.Contains(Path.GetExtension(file).ToLowerInvariant()))throw new Exception("Dieser Dateityp wird nicht direkt geöffnet.");System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file){UseShellExecute=true});}catch(Exception e){UI.Error(e);}
 }
 void EditProcess(bool fresh){if(!CanEdit||(!fresh&&Selected==null))return;timer.Stop();try{using(var e=new EditorForm(root,Storage.Clone(catalog),fresh?new Procedure{Department=departments.SelectedItem as string=="Alle Kategorien"?"":departments.SelectedItem as string}:Storage.Clone(Selected),fresh||!catalog.Processes.Any(p=>p.Id==Selected.Id))){if(e.ShowDialog(this)==DialogResult.OK){dataGeneration++;RefreshCatalog(false);}}}finally{timer.Start();}}
 void DeleteProcess(){if(!CanEdit||Selected==null)return;if(!UI.Confirm("Prozess „"+Selected.Title+"“ löschen? Der bisherige Stand bleibt im Sicherungsordner erhalten."))return;try{var next=Storage.Clone(catalog);next.Processes.RemoveAll(p=>p.Id==Selected.Id);Storage.Save(root,next,catalog.Revision);dataGeneration++;RefreshCatalog(false);}catch(Exception e){UI.Error(e);}}
 void ShowHelp(){MessageBox.Show("Hauptmenü: „Anleitung“ zeigt Arbeitsabläufe, „Vorlagen“ zeigt gemeinsame Dokumente. Über „Hauptmenü“ kommst du zurück.\n\nVorlagen hochladen: unter deinem Bearbeiterkonto „Vorlagen“ > „Vorlage hochladen“. Mitarbeiter nutzen „Kopie speichern“, um eine lokale Kopie zu erhalten.\n\nDer Datenordner wird einmalig über Einstellungen verbunden. Beim Start erscheint keine Ordnerabfrage. Alle Mitarbeiter wählen denselben gemeinsamen Ordner. Unterschiedliche Laufwerksbuchstaben sind möglich; empfohlen ist ein UNC-Pfad wie \\\\Server\\Freigabe\\Prozesshandbuch.\n\nNur das beim Anlegen hinterlegte Windows-Konto erhält die Bearbeitungsansicht. Die tatsächliche Absicherung erfolgt über Freigabe- und NTFS-Rechte: Bearbeiter = Ändern; Mitarbeiter = Lesen.\n\nÄnderungen erscheinen spätestens nach 30 Sekunden. Bei fehlendem Netzlaufwerk wird der letzte angezeigte Stand als veraltet markiert.\n\nChecklisten sind Lesetext. Es werden keine Bearbeitungsstände der Mitarbeiter gespeichert. Die Suche erfasst Anweisungen und Dokumentnamen, nicht den Inhalt angehängter Dateien.\n\nVollständige Einrichtung und Wiederherstellung: LIESMICH.txt im Installationspaket.","Hilfe",MessageBoxButtons.OK,MessageBoxIcon.Information);}
}
public class Diagram:Control {
 Procedure p;int boxX=38,boxWidth=390,row=145;
 public Diagram(Procedure value){p=value;DoubleBuffered=true;Font=new Font("Segoe UI",14.67f,FontStyle.Regular,GraphicsUnit.Pixel);Size=new Size(800,Math.Max(300,p.Steps.Count*row+70));BackColor=UI.Pale;AccessibleName="Ablaufdiagramm; dieselben Verbindungen stehen auch in der Arbeitsanweisung";}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);using(var g=CreateGraphics()){float scale=g.DpiX/96f;Size=new Size((int)(800*scale),(int)(Math.Max(300,p.Steps.Count*row+70)*scale));}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(g.DpiX/96f,g.DpiY/96f);
  if(p.Steps.Count==0){g.DrawString("Noch keine Schritte hinterlegt.",Font,Brushes.Gray,30,30);return;}
  for(int i=0;i<p.Steps.Count;i++){var s=p.Steps[i];DrawEdge(g,i,s.Next,String.IsNullOrWhiteSpace(s.Otherwise)?"Weiter":"Ja",0);DrawEdge(g,i,s.Otherwise,"Nein",1);}
  for(int i=0;i<p.Steps.Count;i++){var s=p.Steps[i];int y=30+i*row;bool decision=!String.IsNullOrWhiteSpace(s.Otherwise);Rectangle rect=new Rectangle(boxX,y,boxWidth,98);
   using(var brush=new SolidBrush(decision?UI.Peach:UI.Pale))using(var pen=new Pen(decision?UI.Orange:UI.Blue,1.5f)){
    if(decision){Point[] points={new Point(boxX+boxWidth/2,y),new Point(boxX+boxWidth,y+49),new Point(boxX+boxWidth/2,y+98),new Point(boxX,y+49)};g.FillPolygon(brush,points);g.DrawPolygon(pen,points);}else{using(var path=UI.Round(rect,16))using(var fill=new SolidBrush(Color.White)){g.FillPath(fill,path);}using(var stripe=new SolidBrush(UI.Orange))g.FillEllipse(stripe,boxX+14,y+14,6,6);}
   }
   Rectangle textRect=decision?new Rectangle(boxX+85,y+24,boxWidth-170,54):new Rectangle(boxX+14,y+10,boxWidth-28,78);
   using(var brush=new SolidBrush(UI.Ink))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter})g.DrawString(s.Id+" · "+s.Title+"\n"+s.Owner,Font,brush,textRect,format);
   if(String.IsNullOrWhiteSpace(s.Next)&&String.IsNullOrWhiteSpace(s.Otherwise))g.DrawString("Ende",Font,Brushes.DimGray,boxX+boxWidth+12,y+40);
  }
 }
 void DrawEdge(Graphics g,int source,string target,string label,int branch){if(String.IsNullOrWhiteSpace(target))return;int dest=p.Steps.FindIndex(s=>s.Id==target);if(dest<0)return;
  int y1=30+source*row+49+(branch==0?-10:10),y2=30+dest*row+49+(branch==0?-10:10);int lane=boxX+boxWidth+75+(source%6)*38+branch*17;
  using(var pen=new Pen(branch==0?UI.Blue:UI.OrangeText,1.6f))using(var cap=new AdjustableArrowCap(4,5)){
   pen.CustomEndCap=cap;if(source==dest){g.DrawLines(pen,new[]{new Point(boxX+boxWidth,y1),new Point(lane,y1),new Point(lane,y1-65),new Point(boxX+boxWidth/2,y1-65),new Point(boxX+boxWidth/2,30+source*row)});}
   else g.DrawLines(pen,new[]{new Point(boxX+boxWidth,y1),new Point(lane,y1),new Point(lane,y2),new Point(boxX+boxWidth,y2)});
  }
  g.DrawString(label+" → "+target,Font,Brushes.DimGray,boxX+boxWidth+7,y1-24);
 }
}
public class EditorForm:Form {
 public static readonly HashSet<string> Allowed=new HashSet<string>(new[]{".pdf",".docx",".xlsx",".pptx",".txt",".csv",".png",".jpg",".jpeg"});
 string root;Catalog catalog;Procedure procedure;bool fresh,loading,dirty;int selected=-1;
 TextBox title=new TextBox(),department=new TextBox(),topic=new TextBox(),summary=new TextBox();
 DataGridView grid=new DataGridView();TextBox instruction=new TextBox(),checklist=new TextBox();ListBox documents=new ListBox();
 Dictionary<string,string> pending=new Dictionary<string,string>();
 public EditorForm(string folder,Catalog c,Procedure p,bool isNew){root=folder;catalog=c;procedure=p;fresh=isNew;Text="Bruno Grüttner | Prozess bearbeiten";Font=new Font("Segoe UI",11);BackColor=UI.Pale;ForeColor=UI.Ink;Size=new Size(1180,920);MinimumSize=new Size(1060,780);StartPosition=FormStartPosition.CenterParent;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
  var outer=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Padding=new Padding(16)};outer.RowStyles.Add(new RowStyle(SizeType.Absolute,170));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,52));outer.RowStyles.Add(new RowStyle(SizeType.Percent,43));outer.RowStyles.Add(new RowStyle(SizeType.Percent,57));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,60));Controls.Add(outer);
  var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=3};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,40));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,40));fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  fields.Controls.Add(UI.Label("Titel"),0,0);fields.Controls.Add(title,1,0);fields.SetColumnSpan(title,3);fields.Controls.Add(UI.Label("Abteilung"),0,1);fields.Controls.Add(department,1,1);fields.Controls.Add(UI.Label("Thema"),2,1);fields.Controls.Add(topic,3,1);fields.Controls.Add(UI.Label("Überblick"),0,2);fields.Controls.Add(summary,1,2);fields.SetColumnSpan(summary,3);summary.Multiline=true;summary.ScrollBars=ScrollBars.Vertical;foreach(var t in new[]{title,department,topic,summary})t.Dock=DockStyle.Fill;
  title.Text=p.Title;department.Text=p.Department;topic.Text=p.Topic;summary.Text=p.Summary;outer.Controls.Add(fields,0,0);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true};actions.Controls.Add(UI.Button("+ Schritt",delegate{AddStep();}));actions.Controls.Add(UI.Button("Schritt entfernen",delegate{RemoveStep();}));actions.Controls.Add(UI.Button("↑",delegate{MoveStep(-1);}));actions.Controls.Add(UI.Button("↓",delegate{MoveStep(1);}));actions.Controls.Add(new Label{Text="Ziele = Schritt-ID; leer = Ende; Ja + Nein = Entscheidung",AutoSize=true,Padding=new Padding(10,10,0,0)});outer.Controls.Add(actions,0,1);
  grid.Dock=DockStyle.Fill;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AutoGenerateColumns=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.RowHeadersVisible=false;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.GridColor=UI.Line;grid.EnableHeadersVisualStyles=false;
  grid.ColumnHeadersDefaultCellStyle.BackColor=Color.White;grid.ColumnHeadersDefaultCellStyle.ForeColor=UI.Muted;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.ColumnHeadersHeight=42;
  grid.DefaultCellStyle.SelectionBackColor=UI.Peach;grid.DefaultCellStyle.SelectionForeColor=UI.Ink;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.White;grid.RowTemplate.Height=48;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  string[] names={"ID","Schritt / Entscheidungsfrage","Zuständig","Weiter / Ja","Nein"};foreach(var name in names)grid.Columns.Add(name,name);grid.Columns[0].FillWeight=35;grid.Columns[1].FillWeight=180;grid.Columns[2].FillWeight=100;grid.Columns[3].FillWeight=60;grid.Columns[4].FillWeight=60;grid.Columns[0].ReadOnly=true;
  grid.SelectionChanged+=delegate{if(!loading)SelectStep();};grid.CellValueChanged+=delegate{if(!loading)dirty=true;};grid.DataError+=delegate(object sender,DataGridViewDataErrorEventArgs e){e.ThrowException=false;};outer.Controls.Add(grid,0,2);
  var details=new ModernTabs{Dock=DockStyle.Fill};var t1=new ContentPage("Anweisung");var t2=new ContentPage("Checkliste");var t3=new ContentPage("Dokumente");details.TabPages.AddRange(new[]{t1,t2,t3});UI.ThemeTabs(details);
  foreach(var t in new[]{instruction,checklist}){t.BorderStyle=BorderStyle.None;t.BackColor=Color.White;t.Font=new Font("Segoe UI",12);t.Dock=DockStyle.Fill;t.Multiline=true;t.ScrollBars=ScrollBars.Vertical;t.AcceptsReturn=true;t.TextChanged+=delegate{if(!loading)dirty=true;};}var instructionSurface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(22)};instructionSurface.Controls.Add(instruction);t1.Controls.Add(instructionSurface);var checklistSurface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(22)};checklistSurface.Controls.Add(checklist);t2.Controls.Add(checklistSurface);
  documents.Dock=DockStyle.Fill;documents.DisplayMember="Name";var docActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45};docActions.Controls.Add(UI.Button("Datei hinzufügen …",delegate{Attach();}));docActions.Controls.Add(UI.Button("Verweis entfernen",delegate{if(selected>=0&&documents.SelectedItem!=null){procedure.Steps[selected].Documents.Remove((Attachment)documents.SelectedItem);LoadDocuments();dirty=true;}}));t3.Controls.Add(documents);t3.Controls.Add(docActions);outer.Controls.Add(details,0,3);
  var bottom=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var publish=UI.Button("Veröffentlichen",delegate{Publish();});UI.Primary(publish);bottom.Controls.Add(publish);bottom.Controls.Add(UI.Button("Abbrechen",delegate{Close();}));outer.Controls.Add(bottom,0,4);
  foreach(var t in new[]{title,department,topic,summary})t.TextChanged+=delegate{dirty=true;};FormClosing+=delegate(object sender,FormClosingEventArgs e){if(DialogResult!=DialogResult.OK&&dirty&&!UI.Confirm("Ungespeicherte Änderungen verwerfen?"))e.Cancel=true;};Rebuild(0);
 }
 void Flush(){if(selected>=0&&selected<procedure.Steps.Count){procedure.Steps[selected].Instructions=instruction.Text;procedure.Steps[selected].Checklist=checklist.Text;}grid.EndEdit();for(int i=0;i<grid.Rows.Count&&i<procedure.Steps.Count;i++){var s=procedure.Steps[i];s.Title=Cell(i,1);s.Owner=Cell(i,2);s.Next=Cell(i,3);s.Otherwise=Cell(i,4);}}
 string Cell(int row,int col){return Convert.ToString(grid.Rows[row].Cells[col].Value).Trim();}
 void SelectStep(){Flush();selected=grid.CurrentRow==null?-1:grid.CurrentRow.Index;LoadDetail();}
 void LoadDetail(){loading=true;instruction.Text=selected>=0?procedure.Steps[selected].Instructions:"";checklist.Text=selected>=0?procedure.Steps[selected].Checklist:"";instruction.Enabled=checklist.Enabled=selected>=0;LoadDocuments();loading=false;}
 void LoadDocuments(){documents.Items.Clear();if(selected>=0)foreach(var a in procedure.Steps[selected].Documents)documents.Items.Add(a);}
 void Rebuild(int index){loading=true;grid.Rows.Clear();foreach(var s in procedure.Steps)grid.Rows.Add(s.Id,s.Title,s.Owner,s.Next,s.Otherwise);selected=procedure.Steps.Count==0?-1:Math.Max(0,Math.Min(index,procedure.Steps.Count-1));if(selected>=0)grid.CurrentCell=grid.Rows[selected].Cells[1];loading=false;LoadDetail();}
 void AddStep(){Flush();if(procedure.Steps.Count>=60){UI.Error(new Exception("Maximal 60 Schritte pro Prozess."));return;}int n=1;while(procedure.Steps.Any(s=>s.Id==n.ToString()))n++;procedure.Steps.Add(new Step{Id=n.ToString(),Title="Neuer Schritt"});dirty=true;Rebuild(procedure.Steps.Count-1);}
 void RemoveStep(){if(selected<0)return;Flush();string id=procedure.Steps[selected].Id;if(!UI.Confirm("Schritt "+id+" entfernen? Verbindungen zu diesem Schritt werden ebenfalls entfernt."))return;procedure.Steps.RemoveAt(selected);foreach(var s in procedure.Steps){if(s.Next==id)s.Next="";if(s.Otherwise==id)s.Otherwise="";}dirty=true;Rebuild(selected);}
 void MoveStep(int delta){if(selected<0||selected+delta<0||selected+delta>=procedure.Steps.Count)return;Flush();int index=selected+delta;var s=procedure.Steps[selected];procedure.Steps.RemoveAt(selected);procedure.Steps.Insert(index,s);dirty=true;Rebuild(index);}
 void Attach(){if(selected<0){UI.Error(new Exception("Bitte zuerst einen Schritt auswählen."));return;}using(var d=new OpenFileDialog{Multiselect=true,Filter="Dokumente|*.pdf;*.docx;*.xlsx;*.pptx;*.txt;*.csv;*.png;*.jpg;*.jpeg"}){if(d.ShowDialog()!=DialogResult.OK)return;foreach(var file in d.FileNames){string ext=Path.GetExtension(file).ToLowerInvariant();if(!Allowed.Contains(ext))continue;string name=Guid.NewGuid().ToString("N")+ext;pending[name]=file;procedure.Steps[selected].Documents.Add(new Attachment{Name=Path.GetFileName(file),File=name});}dirty=true;LoadDocuments();}}
 void Publish(){
  Flush();procedure.Title=title.Text.Trim();procedure.Department=department.Text.Trim();procedure.Topic=topic.Text.Trim();procedure.Summary=summary.Text.Trim();procedure.Updated=DateTime.Now.ToString("dd.MM.yyyy HH:mm");var next=Storage.Clone(catalog);if(fresh)next.Processes.Add(procedure);else{int i=next.Processes.FindIndex(p=>p.Id==procedure.Id);next.Processes[i]=procedure;}
  var copied=new List<string>();bool saved=false;
  try{Storage.Validate(next);if(Storage.Read(root).Revision!=catalog.Revision)throw new Exception("Der Stand wurde geändert. Bitte den Editor schließen und aktualisieren.");
   foreach(var a in procedure.Steps.SelectMany(s=>s.Documents)){if(pending.ContainsKey(a.File)){Directory.CreateDirectory(Path.Combine(root,"Dokumente"));string dest=Path.Combine(root,"Dokumente",a.File);File.Copy(pending[a.File],dest,false);copied.Add(dest);}else if(!File.Exists(Path.Combine(root,"Dokumente",a.File)))throw new Exception("Dokument nicht gefunden: "+a.Name);}
   Storage.Save(root,next,catalog.Revision);saved=true;dirty=false;DialogResult=DialogResult.OK;Close();
  }catch(Exception e){UI.Error(new Exception("Nicht veröffentlicht.\n\n"+e.Message));}finally{if(!saved)foreach(var file in copied){try{File.Delete(file);}catch{}}}
 }
}
public static class Program {
 [STAThread] public static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){UI.Error(e.Exception);};try{Application.Run(new MainForm());}catch(Exception e){UI.Error(e);}}
}
}
