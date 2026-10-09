using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
using System.Threading.Tasks;

public class PaperPlan { public string Date; public string Text; public bool Done; }
public class PaperSettings { public string FirstDate; public int X; public int Y; public bool HasPosition; public bool StartupInitialized; public int ZoomPercent=100; public string Theme="paper"; }
public class PaperTip { public string Note, Quote, Author, Title, Source, Theme; }
public class PaperCheckBox : CheckBox {
    public Color FillColor=Color.FromArgb(250,239,207),MarkColor=Color.FromArgb(113,119,73),StrokeColor=Color.FromArgb(145,122,77),TickColor=Color.White;
    public PaperCheckBox() {SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float scale=Math.Min(Width/22f,Height/24f); RectangleF box=new RectangleF(2*scale,4*scale,12*scale,12*scale);
        using(SolidBrush fill=new SolidBrush(Checked?MarkColor:FillColor)) e.Graphics.FillRectangle(fill,box);
        using(Pen border=new Pen(StrokeColor,Math.Max(0.5f,scale))) e.Graphics.DrawRectangle(border,box.X,box.Y,box.Width,box.Height);
        if(Checked) using(Pen tick=new Pen(TickColor,Math.Max(1,1.6f*scale))) e.Graphics.DrawLines(tick,new PointF[]{new PointF(4*scale,10*scale),new PointF(7*scale,13*scale),new PointF(12*scale,7*scale)});
        if(Focused) using(Pen focus=new Pen(StrokeColor,0.6f)) {focus.DashStyle=System.Drawing.Drawing2D.DashStyle.Dot; e.Graphics.DrawRectangle(focus,0,0,Width-1,Height-1);}
    }
}
public class PaperCalendar : Form {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref Margins margins);
    struct Margins { public int Left, Right, Top, Bottom; }
    ThemePalette palette=ThemePalette.Get("paper");
    Color paper {get {return palette.Paper;}} Color ink {get {return palette.Ink;}} Color olive {get {return palette.Muted;}}
    DateTime month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), selected = DateTime.Today;
    List<PaperPlan> plans = new List<PaperPlan>();
    string folder = Environment.GetEnvironmentVariable("PAPER_CALENDAR_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaperCalendar");
    Panel calendar = new Panel(), notes = new Panel(); Label monthTitle = new Label(), dayTitle = new Label(), footer = new Label();
    FlowLayoutPanel list = new FlowLayoutPanel(); NotifyIcon tray; bool exiting = false; bool hotkey;
    PaperSettings settings; List<PaperTip> tips = new List<PaperTip>();
    Label tipLabel=new Label(), reminderLabel=new Label(), holidayLabel=new Label(); ToolTip tipDetails=new ToolTip();
    PictureBox bamboo=new PictureBox(); ChinaCalendar china;
    Color planColor {get {return palette.Plan;}} Color holidayRed {get {return palette.Holiday;}} Color workBlack {get {return palette.Work;}}
    Color pendingText {get {return palette.Id=="ocean"?planColor:ink;}}
    Image bambooArt,oceanArt; ToolStripMenuItem themeMenu;
    Dictionary<int,DateTime> holidayRequests=new Dictionary<int,DateTime>(); HashSet<int> holidayInFlight=new HashSet<int>();
    ContextMenuStrip menu; ToolStripMenuItem startupItem; Timer dailyTimer=new Timer();
    DateTime displayedDay=DateTime.Today; bool restoringPosition, dragging; Point dragMouse,dragWindow; DateTime lastDrag=DateTime.MinValue;
    static bool testing;
    float zoom=1f;
    class DesignControl { public Rectangle Bounds; public Padding Margin,Padding; public string Family; public float FontSize; public FontStyle Style; }
    Dictionary<Control,DesignControl> designControls=new Dictionary<Control,DesignControl>();
    Dictionary<string,Font> scaledFonts=new Dictionary<string,Font>();
    ToolStripMenuItem zoomMenu;
    Image LoadArt(string resourceName) {
        using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
            if(resource==null) throw new InvalidDataException("主题图片未打包："+resourceName);
            using(Image image=Image.FromStream(resource)) return new Bitmap(image);
        }
    }
    void ThemeControls(Control control,bool inNotes) {
        inNotes=inNotes || control==notes;
        control.BackColor=control==bamboo?Color.Transparent:inNotes?palette.Panel:paper;
        control.ForeColor=ink;
        if(control.Tag as string=="muted") control.ForeColor=olive;
        if(control.Tag as string=="task") control.ForeColor=pendingText;
        Button button=control as Button;
        if(button!=null) {button.BackColor=paper; button.FlatAppearance.BorderColor=palette.Border; button.FlatAppearance.MouseOverBackColor=palette.Selected; button.FlatAppearance.MouseDownBackColor=palette.Selected;}
        PaperCheckBox check=control as PaperCheckBox;
        if(check!=null) {check.FillColor=palette.Entry; check.MarkColor=palette.Check; check.StrokeColor=palette.CheckBorder; check.TickColor=palette.Tick; check.Invalidate();}
        foreach(Control child in control.Controls) ThemeControls(child,inNotes);
    }
    void ThemeMenu(ToolStripDropDown dropdown) {
        dropdown.BackColor=paper; dropdown.ForeColor=ink;
        foreach(ToolStripItem item in dropdown.Items) {item.BackColor=paper; item.ForeColor=ink; ToolStripDropDownItem child=item as ToolStripDropDownItem; if(child!=null && child.HasDropDownItems) ThemeMenu(child.DropDown);}
    }
    void ApplyTheme(string id,bool persist) {
        palette=ThemePalette.Get(id); settings.Theme=palette.Id;
        SuspendLayout(); ThemeControls(this,false); reminderLabel.ForeColor=planColor; holidayLabel.ForeColor=olive; footer.ForeColor=olive;
        bamboo.Image=palette.Id=="ocean"?oceanArt:bambooArt;
        menu.Renderer=new ToolStripProfessionalRenderer(new ThemeMenuColors(palette)); ThemeMenu(menu);
        foreach(ToolStripItem item in themeMenu.DropDownItems) {ToolStripMenuItem option=item as ToolStripMenuItem; if(option!=null) option.Checked=(string)option.Tag==palette.Id;}
        tray.Text="纸间日历 · "+palette.Name+" · 点击打开";
        tipDetails.BackColor=palette.Panel; tipDetails.ForeColor=ink; ResumeLayout(true);
        RefreshAll(); Invalidate(true); if(persist) SaveSettings();
    }
    Font CachedFont(string family,float size,FontStyle style) {
        string key=family+"|"+size.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture)+"|"+(int)style; Font result;
        if(!scaledFonts.TryGetValue(key,out result)) {result=new Font(family,size,style); scaledFonts[key]=result;} return result;
    }
    int Px(int value) {return (int)Math.Round(value*zoom);}
    Padding ScalePadding(Padding p) {return new Padding(Px(p.Left),Px(p.Top),Px(p.Right),Px(p.Bottom));}
    void CaptureDesign(Control control) {
        if(control!=this && !designControls.ContainsKey(control)) designControls[control]=new DesignControl {Bounds=control.Bounds,Margin=control.Margin,Padding=control.Padding,Family=control.Font.Name,FontSize=control.Font.SizeInPoints,Style=control.Font.Style};
        foreach(Control child in control.Controls) CaptureDesign(child);
    }
    void ForgetDesign(Control control) {foreach(Control child in control.Controls) ForgetDesign(child); designControls.Remove(control);}
    void ScaleControlFromDesign(Control control) {
        DesignControl spec=designControls[control]; Rectangle b=spec.Bounds;
        control.Font=CachedFont(spec.Family,spec.FontSize*zoom,spec.Style);
        control.Margin=ScalePadding(spec.Margin); control.Padding=ScalePadding(spec.Padding);
        control.SetBounds(Px(b.X),Px(b.Y),Px(b.Width),Px(b.Height));
    }
    void ScaleNewRow(Control row) {
        CaptureDesign(row); ScaleRowTree(row);
    }
    void ScaleRowTree(Control control) {ScaleControlFromDesign(control); foreach(Control child in control.Controls) ScaleRowTree(child);}
    void ApplyZoom(int percent,bool persist) {
        percent=Math.Max(50,Math.Min(100,percent)); zoom=percent/100f;
        SuspendLayout(); notes.SuspendLayout(); list.SuspendLayout();
        MinimumSize=Size.Empty; MaximumSize=Size.Empty; Font=CachedFont("Microsoft YaHei UI",10*zoom,FontStyle.Regular);
        foreach(KeyValuePair<Control,DesignControl> pair in designControls) if(!pair.Key.IsDisposed) ScaleControlFromDesign(pair.Key);
        ClientSize=new Size(Px(830),Px(700)); MinimumSize=MaximumSize=Size;
        list.ResumeLayout(true); notes.ResumeLayout(true); ResumeLayout(true); calendar.Invalidate(); Invalidate();
        if(zoomMenu!=null) foreach(ToolStripItem item in zoomMenu.DropDownItems) {ToolStripMenuItem option=item as ToolStripMenuItem; if(option!=null && option.Tag is int) option.Checked=(int)option.Tag==percent;}
        settings.ZoomPercent=percent;
        if(persist) {
            Rectangle work=Screen.FromControl(this).WorkingArea; Location=new Point(Math.Max(work.Left,Math.Min(Left,work.Right-Width)),Math.Max(work.Top,Math.Min(Top,work.Bottom-Height)));
            settings.HasPosition=true; settings.X=Left; settings.Y=Top; SaveSettings();
        }
    }
    protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams; p.ClassStyle |= 0x20000; return p; } }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try { int enabled=2; DwmSetWindowAttribute(Handle,2,ref enabled,4); Margins m=new Margins {Left=1,Right=1,Top=1,Bottom=1}; DwmExtendFrameIntoClientArea(Handle,ref m); } catch(DllNotFoundException) { }
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        using(Pen border=new Pen(palette.Frame)) e.Graphics.DrawRectangle(border,0,0,ClientSize.Width-1,ClientSize.Height-1);
        using(Pen grain=new Pen(palette.Grain)) for(int y=Px(8);y<ClientSize.Height;y+=Math.Max(1,Px(7))) e.Graphics.DrawLine(grain,1,y,ClientSize.Width-2,y);
    }
    string Key(DateTime d) { return d.ToString("yyyy-MM-dd"); }
    Button Button(string text, int x, int y, int w, Control parent, EventHandler action) {
        Button b = new Button { Text=text, Left=x, Top=y, Width=w, Height=34, FlatStyle=FlatStyle.Flat, BackColor=paper, ForeColor=ink, Cursor=Cursors.Hand, Font=CachedFont("Microsoft YaHei UI",10,FontStyle.Regular) };
        b.FlatAppearance.BorderColor=palette.Border; b.FlatAppearance.MouseOverBackColor=palette.Selected; b.FlatAppearance.MouseDownBackColor=palette.Selected; b.Click+=action; parent.Controls.Add(b); return b;
    }
    public PaperCalendar() {
        Text="纸间日历"; AutoScaleMode=AutoScaleMode.None; FormBorderStyle=FormBorderStyle.None; ClientSize=new Size(830,700); MinimumSize=MaximumSize=Size; MaximizeBox=false; ShowInTaskbar=false; DoubleBuffered=true;
        BackColor=paper; ForeColor=ink; Font=new Font("Microsoft YaHei UI",10); StartPosition=FormStartPosition.Manual;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Controls.Add(new Label { Text="纸 间", Left=28, Top=22, AutoSize=true, Font=new Font("Microsoft YaHei UI",21,FontStyle.Bold), ForeColor=ink });
        Controls.Add(new Label { Text="把日子写在纸上", Left=135, Top=39, AutoSize=true, ForeColor=olive,Tag="muted" });
        Button("今天", 410,29,65,this,delegate { selected=DateTime.Today; month=new DateTime(selected.Year,selected.Month,1); RefreshAll(); });
        Button("‹",28,88,38,this,delegate { month=month.AddMonths(-1); RefreshAll(); });
        monthTitle.SetBounds(82,89,310,35); monthTitle.Font=new Font("Microsoft YaHei UI",17,FontStyle.Bold); Controls.Add(monthTitle);
        Button("›",434,88,38,this,delegate { month=month.AddMonths(1); RefreshAll(); });
        calendar.SetBounds(25,139,452,344); calendar.BackColor=paper; calendar.Paint+=PaintCalendar; calendar.MouseClick+=PickDay; calendar.MouseMove+=CalendarHover; Controls.Add(calendar);
        notes.SetBounds(504,88,300,386); notes.BackColor=palette.Panel; notes.BorderStyle=BorderStyle.None; notes.Paint+=delegate(object sender,PaintEventArgs e) {using(Pen border=new Pen(palette.Border)) e.Graphics.DrawRectangle(border,0,0,notes.Width-1,notes.Height-1);}; Controls.Add(notes);
        dayTitle.SetBounds(16,17,270,45); dayTitle.Font=new Font("Microsoft YaHei UI",13,FontStyle.Bold); notes.Controls.Add(dayTitle);
        list.SetBounds(12,75,273,250); list.FlowDirection=FlowDirection.TopDown; list.WrapContents=false; list.AutoScroll=true; notes.Controls.Add(list);
        Button("＋ 写下计划",15,336,268,notes,delegate { Edit(null); });
        reminderLabel.SetBounds(28,509,530,32); reminderLabel.ForeColor=planColor; reminderLabel.Font=new Font("Microsoft YaHei UI",11); reminderLabel.AutoEllipsis=true; Controls.Add(reminderLabel);
        holidayLabel.SetBounds(28,551,530,32); holidayLabel.ForeColor=olive; holidayLabel.Font=new Font("Microsoft YaHei UI",11); holidayLabel.AutoEllipsis=true; Controls.Add(holidayLabel);
        tipLabel.SetBounds(28,598,535,49); tipLabel.Font=new Font("KaiTi",15); tipLabel.ForeColor=ink; Controls.Add(tipLabel);
        footer.SetBounds(28,664,770,23); footer.ForeColor=olive; footer.Font=new Font("Microsoft YaHei UI",8.5f); Controls.Add(footer);
        bamboo.SetBounds(574,501,228,160); bamboo.SizeMode=PictureBoxSizeMode.Zoom; bamboo.BackColor=Color.Transparent; Controls.Add(bamboo);
        bambooArt=LoadArt("PaperCalendar.Bamboo"); oceanArt=LoadArt("PaperCalendar.Ocean"); bamboo.Image=bambooArt;
        LoadPlans(); LoadSettings(); LoadTips(); china=new ChinaCalendar(folder);
        tray=new NotifyIcon { Icon=Icon, Text="纸间日历 · 点击打开", Visible=true };
        menu=new ContextMenuStrip(); menu.BackColor=paper; menu.ForeColor=ink; menu.Items.Add("显示日历",null,delegate { ShowCalendar(); });
        menu.Items.Add("隐藏",null,delegate { Hide(); });
        themeMenu=new ToolStripMenuItem("切换主题");
        foreach(string id in new string[]{"paper","ocean"}) {string value=id; ToolStripMenuItem option=new ToolStripMenuItem(ThemePalette.Get(id).Name) {Tag=id}; option.Click+=delegate {ApplyTheme(value,true);}; themeMenu.DropDownItems.Add(option);}
        menu.Items.Add(themeMenu);
        zoomMenu=new ToolStripMenuItem("等比例缩放");
        foreach(int percent in new int[]{50,60,70,80,90,100}) {
            int value=percent; ToolStripMenuItem option=new ToolStripMenuItem(percent+"%"+(percent==100?"（原始大小）":"")) {Tag=percent};
            option.Click+=delegate {ApplyZoom(value,true);}; zoomMenu.DropDownItems.Add(option);
        }
        menu.Items.Add(zoomMenu);
        startupItem=new ToolStripMenuItem("开机自启动"); startupItem.Click+=delegate { SetStartup(!IsStartupEnabled()); }; menu.Items.Add(startupItem);
        menu.Items.Add("更新节假日",null,delegate { RefreshHolidayYear(month.Year,true); });
        menu.Items.Add("短句来源与许可",null,delegate { ShowSources(); });
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("关闭程序",null,delegate { exiting=true; Close(); }); tray.ContextMenuStrip=menu;
        menu.Opening+=delegate { startupItem.Checked=IsStartupEnabled(); };
        AttachInteractions(this);
        CaptureDesign(this); ApplyZoom(settings.ZoomPercent,false);
        tray.MouseClick+=delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Left) { if(Visible && WindowState!=FormWindowState.Minimized) Hide(); else ShowCalendar(); } };
        FormClosing+=delegate(object s,FormClosingEventArgs e) { if(!exiting) { e.Cancel=true; Hide(); } };
        FormClosed+=delegate { UnregisterHotKey(Handle,1); tray.Visible=false; tray.Dispose(); dailyTimer.Dispose(); tipDetails.Dispose(); menu.Dispose(); bamboo.Image=null; bambooArt.Dispose(); oceanArt.Dispose(); foreach(Font font in scaledFonts.Values) font.Dispose(); };
        Shown+=delegate { hotkey=!testing && RegisterHotKey(Handle,1,0x4003,0x44); PositionWindow(); RefreshAll(); };
        dailyTimer.Interval=30000; dailyTimer.Tick+=delegate { CheckDate(DateTime.Today); }; dailyTimer.Start();
        ApplyTheme(settings.Theme,false);
    }
    void PositionWindow() {
        restoringPosition=true;
        Rectangle a=Screen.PrimaryScreen.WorkingArea;
        Point saved=new Point(settings.X,settings.Y); bool reachable=false;
        foreach(Screen screen in Screen.AllScreens) if(screen.WorkingArea.IntersectsWith(new Rectangle(saved,new Size(Width,65)))) {reachable=true; a=screen.WorkingArea; break;}
        Location=settings.HasPosition && reachable ? new Point(Math.Max(a.Left,Math.Min(saved.X,a.Right-Width)),Math.Max(a.Top,Math.Min(saved.Y,a.Bottom-Height))) : new Point(Math.Max(a.Left,a.Right-Width-16),Math.Max(a.Top,a.Bottom-Height-16));
        restoringPosition=false;
    }
    void ShowCalendar() { CheckDate(DateTime.Today); Show(); WindowState=FormWindowState.Normal; Activate(); }
    void AttachInteractions(Control control) {
        control.ContextMenuStrip=menu;
        if(!(control is Button) && !(control is CheckBox) && !(control is TextBox)) {
            control.MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Left) { dragging=false; dragMouse=Control.MousePosition; dragWindow=Location; ((Control)s).Capture=true; } };
            control.MouseMove+=delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Left && ((Control)s).Capture) {
                Point now=Control.MousePosition; int dx=now.X-dragMouse.X,dy=now.Y-dragMouse.Y;
                if(Math.Abs(dx)+Math.Abs(dy)>6) dragging=true;
                if(dragging) { Location=new Point(dragWindow.X+dx,dragWindow.Y+dy); lastDrag=DateTime.UtcNow; }
            } };
            control.MouseUp+=delegate(object s,MouseEventArgs e) { ((Control)s).Capture=false; if(dragging) { dragging=false; if(!restoringPosition) {settings.HasPosition=true; settings.X=Left; settings.Y=Top; SaveSettings();} } };
        }
        foreach(Control child in control.Controls) AttachInteractions(child);
    }
    void LoadSettings() {
        string p=Path.Combine(folder,"settings.xml");
        try { if(File.Exists(p)) using(FileStream f=File.OpenRead(p)) settings=(PaperSettings)new XmlSerializer(typeof(PaperSettings)).Deserialize(f); }
        catch(Exception e) { throw new InvalidDataException("设置文件无法读取，原文件已保留。",e); }
        if(settings==null) settings=new PaperSettings {FirstDate=DateTime.Today.ToString("yyyy-MM-dd")};
        if(settings.ZoomPercent<50 || settings.ZoomPercent>100) settings.ZoomPercent=100;
        if(settings.Theme!="paper" && settings.Theme!="ocean") settings.Theme="paper";
        DateTime first; if(!DateTime.TryParseExact(settings.FirstDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out first)) throw new InvalidDataException("每日短句的起始日期无效。");
    }
    bool SaveSettings() {
        try { Directory.CreateDirectory(folder); string p=Path.Combine(folder,"settings.xml"),temp=p+".tmp";
            using(FileStream f=File.Create(temp)) new XmlSerializer(typeof(PaperSettings)).Serialize(f,settings);
            if(File.Exists(p)) File.Replace(temp,p,p+".bak"); else File.Move(temp,p); return true;
        } catch(Exception e) { MessageBox.Show("设置保存失败："+e.Message,"纸间日历"); return false; }
    }
    const string StartupKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    static string InstalledPath { get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PaperCalendar","PaperCalendar.exe");} }
    bool IsStartupEnabled() { using(RegistryKey key=Registry.CurrentUser.OpenSubKey(StartupKey)) {return key!=null && key.GetValue("PaperCalendar") is string;} }
    void SetStartup(bool enable) {
        try {
            if(enable) {
                Directory.CreateDirectory(Path.GetDirectoryName(InstalledPath));
                if(!String.Equals(Path.GetFullPath(Application.ExecutablePath),Path.GetFullPath(InstalledPath),StringComparison.OrdinalIgnoreCase)) File.Copy(Application.ExecutablePath,InstalledPath,true);
            }
            using(RegistryKey key=Registry.CurrentUser.CreateSubKey(StartupKey)) { if(enable) key.SetValue("PaperCalendar","\""+InstalledPath+"\" --startup",RegistryValueKind.String); else key.DeleteValue("PaperCalendar",false); }
            startupItem.Checked=enable; settings.StartupInitialized=true; SaveSettings();
        } catch(Exception e) { MessageBox.Show("自启动设置失败："+e.Message,"纸间日历"); }
    }
    void LoadTips() {
        Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("PaperCalendar.Tips");
        if(resource!=null) using(StreamReader reader=new StreamReader(resource,Encoding.UTF8)) ReadTips(reader);
        else using(StreamReader reader=new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tips.tsv"),Encoding.UTF8)) ReadTips(reader);
        if(tips.Count<3653) throw new InvalidDataException("每日短句不足 3653 条。");
    }
    void ReadTips(StreamReader reader) { string line; HashSet<string> unique=new HashSet<string>(); while((line=reader.ReadLine())!=null) {string[] parts=line.Split('\t'); if(parts.Length!=6 || !unique.Add(parts[1])) throw new InvalidDataException("短句格式错误或重复。"); tips.Add(new PaperTip {Note=parts[0],Quote=parts[1],Author=parts[2],Title=parts[3],Source=parts[4],Theme=parts[5]});} }
    void ShowSources() {
        string license=""; Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("PaperCalendar.License"); if(resource!=null) using(StreamReader r=new StreamReader(resource)) license=r.ReadToEnd();
        using(Form dialog=new Form {Text="短句来源与许可",Size=new Size(620,450),StartPosition=FormStartPosition.CenterParent,BackColor=paper}) {
            using(Stream holidayLicense=Assembly.GetExecutingAssembly().GetManifestResourceStream("PaperCalendar.HolidayLicense")) if(holidayLicense!=null) using(StreamReader reader=new StreamReader(holidayLicense)) license+="\r\n\r\nholiday-cn 许可：\r\n"+reader.ReadToEnd();
            TextBox text=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=paper,Font=new Font("Microsoft YaHei UI",10),Text="每日一句：3653 条互不重复的古诗文短句，温暖与励志内容混排，来自网上公开 chinese-poetry 数据库。\r\nhttps://github.com/chinese-poetry/chinese-poetry\r\n\r\n悬停在短句上查看作者、作品和来源。每天只显示一句，3653 天后从头循环。古诗文版本差异按来源保留。\r\n\r\n中国大陆假期数据：holiday-cn，根据国务院公告整理。内置 2026 年安排，联网可自动更新当年和下一年。尚未公布的年份只提示节日日期，不推测放假和调休。农历支持 1901—2100 年；清明日期用太阳黄经近似算法计算，未公布安排以正式公告为准。\r\nhttps://github.com/NateScarlet/holiday-cn\r\n\r\n起始日："+settings.FirstDate+"\r\n\r\n"+license}; dialog.Controls.Add(text); dialog.ShowDialog(this);
        }
    }
    int TipIndex(DateTime day) { DateTime start=DateTime.ParseExact(settings.FirstDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture); int n=(int)(day.Date-start.Date).TotalDays; return ((n%tips.Count)+tips.Count)%tips.Count; }
    void UpdateTip(DateTime day) {
        int index=TipIndex(day); PaperTip tip=tips[index];
        tipLabel.Text="「"+tip.Quote+"」";
        tipDetails.SetToolTip(tipLabel,tip.Author+"《"+tip.Title+"》\n"+tip.Source);
    }
    string Reminder(DateTime today) {
        DateTime? nearest=null; int count=0; bool anyUndone=false;
        foreach(PaperPlan plan in plans) {
            if(plan.Done) continue; anyUndone=true; DateTime date;
            if(!DateTime.TryParseExact(plan.Date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out date) || date<=today.Date) continue;
            if(!nearest.HasValue || date<nearest.Value) {nearest=date; count=1;} else if(date==nearest.Value) count++;
        }
        if(nearest.HasValue) return "距 "+nearest.Value.ToString("M 月 d 日")+"待办还有 "+(nearest.Value-today.Date).Days+" 天 · "+count+" 项";
        if(plans.Count==0) return "暂无待办计划";
        if(!anyUndone) return "已完成所有待办";
        return "暂无未来待办";
    }
    void RefreshHolidayYear(int year,bool manual) {
        if(testing || !IsHandleCreated || year<2007 || year>DateTime.Today.Year+1 || holidayInFlight.Contains(year)) return;
        DateTime tried; if(!manual && holidayRequests.TryGetValue(year,out tried) && tried.Date==DateTime.Today) return;
        holidayRequests[year]=DateTime.Today; holidayInFlight.Add(year); DateTime today=DateTime.Today;
        Task.Factory.StartNew(delegate {
            HolidayYear data=null; Exception error=null; try {data=china.Download(year,today);} catch(Exception e) {error=e;}
            if(IsDisposed || !IsHandleCreated) return;
            try {BeginInvoke((Action)delegate {
                if(IsDisposed) return; holidayInFlight.Remove(year);
                if(data!=null) {china.Merge(data); RefreshAll();}
                if(manual) MessageBox.Show(data!=null?year+" 年节假日数据已更新。":"暂未能更新："+error.Message+"\n继续使用本机已有数据；未公布的安排不标记假 / 班。","纸间日历");
            });} catch(InvalidOperationException) {}
        });
    }
    bool HasPlan(DateTime date) {foreach(PaperPlan plan in plans) if(plan.Date==Key(date)) return true; return false;}
    Color DayColor(DateTime date) {
        if(HasPlan(date)) return planColor;
        HolidayDay holiday=china.Get(date); if(holiday!=null) return holiday.isOffDay?holidayRed:workBlack;
        return date.Month==month.Month?ink:palette.Outside;
    }
    void CalendarHover(object sender,MouseEventArgs e) {
        DateTime date; if(e.Button!=MouseButtons.None || !DateAtPoint(e.Location,out date)) return;
        HolidayDay holiday=china.Get(date); string text=date.ToString("yyyy 年 M 月 d 日")+" · "+china.LunarFull(date);
        if(holiday!=null) text+="\n"+holiday.name+(holiday.isOffDay?" · 放假":" · 调休上班");
        if(HasPlan(date)) text+="\n当天已记录计划";
        if(tipDetails.GetToolTip(calendar)!=text) tipDetails.SetToolTip(calendar,text);
    }
    void CheckDate(DateTime today) { if(today.Date==displayedDay) return; DateTime previous=displayedDay; displayedDay=today.Date; if(selected==previous) { selected=today.Date; month=new DateTime(today.Year,today.Month,1); } RefreshAll(); }
    protected override void WndProc(ref Message m) { if(m.Msg==0x0312 && m.WParam.ToInt32()==1) { if(Visible && WindowState!=FormWindowState.Minimized) Hide(); else ShowCalendar(); } base.WndProc(ref m); }
    void LoadPlans() {
        try { string p=Path.Combine(folder,"plans.xml"); if(File.Exists(p)) using(FileStream f=File.OpenRead(p)) plans=(List<PaperPlan>)new XmlSerializer(typeof(List<PaperPlan>)).Deserialize(f); }
        catch(Exception e) { MessageBox.Show("备忘文件暂时无法读取，原文件会保留。\n"+e.Message,"纸间日历"); throw; }
    }
    bool SavePlans() {
        try {
            Directory.CreateDirectory(folder); string p=Path.Combine(folder,"plans.xml"), temp=p+".tmp";
            using(FileStream f=File.Create(temp)) new XmlSerializer(typeof(List<PaperPlan>)).Serialize(f,plans);
            if(File.Exists(p)) File.Replace(temp,p,p+".bak"); else File.Move(temp,p); return true;
        } catch(Exception e) { MessageBox.Show("保存失败，请重试。\n"+e.Message,"纸间日历"); return false; }
    }
    void RefreshAll() {
        monthTitle.Text=month.ToString("yyyy 年  M 月"); dayTitle.Text=selected.ToString("M 月 d 日")+"  "+new string[]{"星期日","星期一","星期二","星期三","星期四","星期五","星期六"}[(int)selected.DayOfWeek];
        while(list.Controls.Count>0) {Control old=list.Controls[0]; ForgetDesign(old); old.Dispose();} int total=0, completed=0;
        foreach(PaperPlan plan in plans) if(plan.Date==Key(selected)) {
            total++; if(plan.Done) completed++;
            Panel row=new Panel { Width=250, Height=68, Margin=new Padding(0,0,0,8), Font=CachedFont("Microsoft YaHei UI",10,FontStyle.Regular) };
            CheckBox check=new PaperCheckBox { Left=3, Top=12, Width=22, Height=24, Checked=plan.Done }; row.Controls.Add(check);
            Label text=new Label { Left=30, Top=4, Width=165, Height=58, Text=plan.Text, ForeColor=plan.Done?olive:pendingText, Tag=plan.Done?"muted":"task", AutoEllipsis=true, Cursor=Cursors.Hand, Font=CachedFont("Microsoft YaHei UI",10,plan.Done?FontStyle.Strikeout:FontStyle.Regular) };
            row.Controls.Add(text); text.Click+=delegate { Edit(plan); };
            Button remove=Button("×",204,8,32,row,delegate {
                if(MessageBox.Show("删除这条计划？","纸间日历",MessageBoxButtons.YesNo)==DialogResult.Yes) {
                    int index=plans.IndexOf(plan); plans.Remove(plan); if(!SavePlans()) plans.Insert(index,plan); RefreshAll();
                }
            }); remove.AccessibleName="删除计划";
            check.CheckedChanged+=delegate { bool old=plan.Done; plan.Done=check.Checked; if(!SavePlans()) plan.Done=old; RefreshAll(); };
            ThemeControls(row,true); ScaleNewRow(row); list.Controls.Add(row);
            row.ContextMenuStrip=menu; foreach(Control child in row.Controls) child.ContextMenuStrip=menu;
        }
        if(total==0) { Label empty=new Label { Text="这一天还留着空白。\n\n写下一个小计划，\n或者留一点时间给自己。", Width=240, Height=160, Padding=new Padding(8,20,0,0), ForeColor=olive,Tag="muted",Font=CachedFont("Microsoft YaHei UI",10,FontStyle.Regular) }; ThemeControls(empty,true); ScaleNewRow(empty); list.Controls.Add(empty); AttachInteractions(empty); }
        reminderLabel.Text=Reminder(displayedDay); holidayLabel.Text=china.Countdown(displayedDay);
        footer.Text="左键拖动    ·    右键隐藏 / 关闭    ·    "+(hotkey || testing?"Ctrl + Alt + D 呼出 / 收起":"Ctrl + Alt + D 被占用，请从托盘呼出"); calendar.Invalidate(); UpdateTip(displayedDay);
        RefreshHolidayYear(DateTime.Today.Year,false); RefreshHolidayYear(DateTime.Today.Year+1,false);
        if(month.Year!=DateTime.Today.Year && month.Year!=DateTime.Today.Year+1) RefreshHolidayYear(month.Year,false);
    }
    void PaintCalendar(object sender,PaintEventArgs e) {
        Graphics g=e.Graphics; g.ScaleTransform(zoom,zoom); g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        string[] weekdays={"一","二","三","四","五","六","日"}; int w=64,h=50;
        using(Font dateFont=new Font("Microsoft YaHei UI",10)) using(Font lunarFont=new Font("Microsoft YaHei UI",7.5f)) using(Font badgeFont=new Font("Microsoft YaHei UI",7.5f)) using(Pen line=new Pen(palette.Grid)) using(SolidBrush muted=new SolidBrush(olive)) {
            for(int c=0;c<7;c++) g.DrawString(weekdays[c],dateFont,muted,c*w+23,6);
            DateTime start=month.AddDays(-(((int)month.DayOfWeek+6)%7));
            for(int i=0;i<42;i++) {
                int x=(i%7)*w,y=38+(i/7)*h; DateTime d=start.AddDays(i);
                if(d==selected.Date) using(SolidBrush bg=new SolidBrush(palette.Selected)) g.FillRectangle(bg,x+3,y+2,w-6,h-5);
                g.DrawLine(line,x+5,y+h-2,x+w-5,y+h-2);
                Color dateColor=DayColor(d); using(SolidBrush color=new SolidBrush(dateColor)) {
                    string number=d.Day.ToString(),lunar=china.LunarLabel(d);
                    g.DrawString(number,dateFont,color,x+(w-g.MeasureString(number,dateFont).Width)/2,y+4);
                    g.DrawString(lunar,lunarFont,color,x+(w-g.MeasureString(lunar,lunarFont).Width)/2,y+28);
                }
                if(d==DateTime.Today) using(Pen todayPen=new Pen(olive,1.2f)) g.DrawEllipse(todayPen,x+18,y+1,27,25);
                HolidayDay info=china.Get(d); if(info!=null) using(SolidBrush color=new SolidBrush(info.isOffDay?holidayRed:workBlack)) g.DrawString(info.isOffDay?"假":"班",badgeFont,color,x+49,y+1);
            }
        }
    }
    void PickDay(object sender,MouseEventArgs e) {
        if(e.Button!=MouseButtons.Left || (DateTime.UtcNow-lastDrag).TotalMilliseconds<250) return;
        DateTime date; if(!DateAtPoint(e.Location,out date)) return;
        selected=date; RefreshAll(); if(e.Clicks==2) Edit(null);
    }
    bool DateAtPoint(Point point,out DateTime date) {
        date=DateTime.MinValue; float x=point.X/zoom,y=point.Y/zoom;
        if(y<38 || x<0 || x>=448 || y>=338) return false;
        int index=(int)((y-38)/50)*7+(int)(x/64); date=month.AddDays(-(((int)month.DayOfWeek+6)%7)+index); return true;
    }
    void Edit(PaperPlan plan) {
        using(Form dialog=new Form { Text=plan==null?"写下计划":"编辑计划", ClientSize=new Size(370,275), FormBorderStyle=FormBorderStyle.FixedDialog, MaximizeBox=false, MinimizeBox=false, StartPosition=FormStartPosition.CenterParent, BackColor=paper, ForeColor=ink, Font=CachedFont("Microsoft YaHei UI",10,FontStyle.Regular) }) {
            dialog.Controls.Add(new Label { Text=selected.ToString("yyyy 年 M 月 d 日"), Left=20, Top=18, AutoSize=true });
            TextBox input=new TextBox { Multiline=true, Left=20, Top=54, Width=330, Height=150, MaxLength=2000, ScrollBars=ScrollBars.Vertical, BackColor=palette.Entry, ForeColor=ink, Text=plan==null?"":plan.Text }; dialog.Controls.Add(input);
            Button save=Button("保存",240,223,110,dialog,delegate {
                string value=input.Text.Trim(); if(value.Length==0) { MessageBox.Show("先写一点内容吧。","纸间日历"); return; }
                if(plan==null) { PaperPlan added=new PaperPlan { Date=Key(selected),Text=value }; plans.Add(added); if(!SavePlans()) { plans.Remove(added); return; } }
                else { string old=plan.Text; plan.Text=value; if(!SavePlans()) { plan.Text=old; return; } }
                dialog.DialogResult=DialogResult.OK; dialog.Close();
            });
            Button cancel=Button("取消",118,223,110,dialog,delegate { dialog.Close(); }); cancel.DialogResult=DialogResult.Cancel; dialog.CancelButton=cancel;
            dialog.Shown+=delegate { input.Focus(); }; dialog.ShowDialog(this); RefreshAll();
        }
    }
    [STAThread] public static void Main(string[] args) {
        try {
            if(args.Length>0 && args[0]=="--self-test") { SelfTest(args[1]); return; }
            string requestedTheme=null; foreach(string arg in args) {if(arg=="--theme=ocean") requestedTheme="ocean"; else if(arg=="--theme=paper") requestedTheme="paper";} Run(requestedTheme);
        } catch(Exception e) { if(args.Length>1 && args[0]=="--self-test") File.WriteAllText(Path.Combine(args[1],"verification-error.txt"),e.ToString()); else MessageBox.Show("启动失败："+e.Message,"纸间日历"); Environment.ExitCode=1; }
    }
    [STAThread] public static void Run() {
        Run(null);
    }
    static void Run(string requestedTheme) {
        bool created; using(System.Threading.Mutex mutex=new System.Threading.Mutex(true,"Local\\PaperCalendarDesktop",out created)) {
            if(!created) { MessageBox.Show("纸间日历已经在运行，请点击右下角托盘图标打开。","纸间日历"); return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); PaperCalendar app=new PaperCalendar();
            if(requestedTheme!=null) app.ApplyTheme(requestedTheme,true);
            if(!app.settings.StartupInitialized) app.SetStartup(true);
            else if(app.IsStartupEnabled() && !String.Equals(Path.GetFullPath(Application.ExecutablePath),Path.GetFullPath(InstalledPath),StringComparison.OrdinalIgnoreCase)) app.SetStartup(true);
            app.SaveSettings(); Application.Run(app);
        }
    }
    static void Assert(bool condition,string message) {if(!condition) throw new Exception(message);}
    static void SelfTest(string output) {
        testing=true; Directory.CreateDirectory(output); Environment.SetEnvironmentVariable("PAPER_CALENDAR_DATA",Path.Combine(output,"test-data"));
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using(PaperCalendar app=new PaperCalendar()) {
            app.ApplyTheme("paper",false);
            app.ApplyZoom(100,false);
            Assert(app.tips.Count==3653,"Tip count"); HashSet<string> all=new HashSet<string>(); foreach(PaperTip t in app.tips) all.Add(t.Quote); Assert(all.Count==3653,"Unique quotes");
            DateTime start=DateTime.ParseExact(app.settings.FirstDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture); HashSet<int> daily=new HashSet<int>(); for(DateTime d=start;d<start.AddYears(10);d=d.AddDays(1)) Assert(daily.Add(app.TipIndex(d)),"Repeated daily tip within ten years");
            Assert(app.TipIndex(start.AddDays(3653))==app.TipIndex(start),"Tip cycling after exhaustion");
            Assert(app.china.LunarLabel(new DateTime(2026,2,17))=="正月","Lunar New Year label");
            Assert(app.china.LunarLabel(new DateTime(2023,3,22))=="闰二月","Leap lunar month label");
            Assert(app.china.Marker(new DateTime(2026,10,1))=="假","Official holiday badge");
            Assert(app.china.Marker(new DateTime(2026,10,10))=="班","Adjusted workday badge");
            Assert(app.china.Marker(new DateTime(2027,1,1))=="","Unannounced year has no invented holiday badge");
            Assert(app.DayColor(new DateTime(2026,10,1)).Equals(app.holidayRed),"Holiday red");
            Assert(app.DayColor(new DateTime(2026,10,10)).Equals(app.workBlack),"Workday black");
            Assert(app.china.Countdown(new DateTime(2026,10,9)).Contains("84 天"),"Cross-year holiday countdown");
            Assert(app.china.Countdown(new DateTime(2026,10,1)).Contains("还有 7 天"),"Current holiday countdown");
            Assert(ChinaCalendar.Qingming(2026)==new DateTime(2026,4,5),"Qingming 2026");
            Assert(ChinaCalendar.Qingming(2027)==new DateTime(2027,4,5),"Qingming 2027");
            bool badRejected=false; try {ChinaCalendar.Parse("{\"year\":2026,\"papers\":[\"https://www.gov.cn/202511/example.htm\"],\"days\":[{\"name\":\"元旦\",\"date\":\"2026-01-01\"}]}",2026,new DateTime(2026,10,9));} catch(InvalidDataException) {badRejected=true;} Assert(badRejected,"Missing Boolean must not become a workday");
            Assert(app.bamboo.Image!=null && ((Bitmap)app.bamboo.Image).GetPixel(0,0).A==0,"Embedded bamboo transparency");
            DateTime testDay=new DateTime(2026,10,9); app.plans.Clear();
            app.plans.Add(new PaperPlan {Date=app.Key(testDay.AddDays(-1)),Text="过去的计划"}); app.plans.Add(new PaperPlan {Date=app.Key(testDay),Text="今天的计划"});
            app.plans.Add(new PaperPlan {Date=app.Key(testDay.AddDays(1)),Text="已完成的未来计划",Done=true}); app.plans.Add(new PaperPlan {Date=app.Key(testDay.AddDays(7)),Text="更远的计划"});
            app.plans.Add(new PaperPlan {Date=app.Key(testDay.AddDays(3)),Text="最近的计划"}); app.plans.Add(new PaperPlan {Date=app.Key(testDay.AddDays(3)),Text="同日第二项"});
            Assert(app.Reminder(testDay).Contains("3 天 · 2 项"),"Only nearest future undone date");
            app.plans.Add(new PaperPlan {Date="2026-10-01",Text="假日计划",Done=true}); Assert(app.DayColor(new DateTime(2026,10,1)).Equals(app.planColor) && app.china.Marker(new DateTime(2026,10,1))=="假","Plan blue takes precedence and holiday badge remains");
            foreach(PaperPlan plan in app.plans) plan.Done=true; Assert(app.Reminder(testDay)=="已完成所有待办","All completed reminder");
            app.plans.Clear(); app.plans.Add(new PaperPlan {Date=app.Key(start),Text="测试备忘：保存后仍可读取",Done=true}); Assert(app.SavePlans(),"Save plans"); app.plans.Clear(); app.LoadPlans(); Assert(app.plans.Count==1 && app.plans[0].Done,"Reload plans");
            app.settings.HasPosition=true; app.settings.X=123; app.settings.Y=145; Assert(app.SaveSettings(),"Save settings"); app.settings=null; app.LoadSettings(); Assert(app.settings.X==123 && app.settings.Y==145,"Position persistence");
            app.plans.Clear(); app.Show(); Application.DoEvents(); Assert(app.FormBorderStyle==FormBorderStyle.None,"Borderless");
            app.CheckDate(start.AddDays(1)); Assert(app.selected==start.AddDays(1),"Midnight rollover"); app.CheckDate(start); app.RefreshAll();
            using(Bitmap bitmap=new Bitmap(app.Width,app.Height)) {app.DrawToBitmap(bitmap,new Rectangle(0,0,app.Width,app.Height)); bitmap.Save(Path.Combine(output,"preview.png"));}
            app.plans.Add(new PaperPlan {Date=app.Key(start.AddDays(3)),Text="留半小时读一本喜欢的书"}); app.plans.Add(new PaperPlan {Date=app.Key(start.AddDays(5)),Text="整理下周的小计划"}); app.selected=start.AddDays(3); app.RefreshAll(); Application.DoEvents();
            using(Bitmap bitmap=new Bitmap(app.Width,app.Height)) {app.DrawToBitmap(bitmap,new Rectangle(0,0,app.Width,app.Height)); bitmap.Save(Path.Combine(output,"preview-example.png"));}
            Rectangle baseCalendar=app.calendar.Bounds,baseBamboo=app.bamboo.Bounds; int designCount=app.designControls.Count; DateTime testMonth=app.month;
            foreach(int percent in new int[]{50,60,70,80,90,100}) {
                app.ApplyZoom(percent,false); app.RefreshAll(); Application.DoEvents();
                Assert(app.ClientSize==new Size(app.Px(830),app.Px(700)),"Proportional window size");
                Assert(app.calendar.Bounds==new Rectangle(app.Px(baseCalendar.X),app.Px(baseCalendar.Y),app.Px(baseCalendar.Width),app.Px(baseCalendar.Height)),"Proportional calendar layout");
                Assert(app.bamboo.Bounds==new Rectangle(app.Px(baseBamboo.X),app.Px(baseBamboo.Y),app.Px(baseBamboo.Width),app.Px(baseBamboo.Height)),"Proportional bamboo layout");
                Assert(Math.Abs(app.tipLabel.Font.SizeInPoints-15*percent/100f)<0.02,"Proportional tip font");
                DateTime hit; Assert(app.DateAtPoint(new Point(app.Px(160),app.Px(153)),out hit) && hit==testMonth.AddDays(-(((int)testMonth.DayOfWeek+6)%7)+16),"Calendar hit test at each zoom");
                Assert(app.list.Controls.Count==1 && app.list.Controls[0].Width==app.Px(250),"Rebuilt todo rows respect zoom");
                Assert(app.designControls.Count==designCount,"No stale row layout records");
            }
            Assert(app.calendar.Bounds==baseCalendar && app.bamboo.Bounds==baseBamboo,"No cumulative layout drift");
            app.ApplyZoom(70,true); app.settings=null; app.LoadSettings(); Assert(app.settings.ZoomPercent==70,"Zoom persistence");
            using(Bitmap bitmap=new Bitmap(app.Width,app.Height)) {app.DrawToBitmap(bitmap,new Rectangle(0,0,app.Width,app.Height)); bitmap.Save(Path.Combine(output,"preview-70.png"));}
            app.ApplyZoom(50,false);
            using(Bitmap bitmap=new Bitmap(app.Width,app.Height)) {app.DrawToBitmap(bitmap,new Rectangle(0,0,app.Width,app.Height)); bitmap.Save(Path.Combine(output,"preview-50.png"));}
            app.ApplyZoom(100,false);
            DateTime themeSelected=app.selected; string themeTip=app.tipLabel.Text; int themePlans=app.plans.Count;
            app.ApplyTheme("ocean",true);
            Assert(app.palette.Id=="ocean" && app.BackColor==app.palette.Paper && app.bamboo.Image==app.oceanArt,"Ocean artwork and background");
            Assert(((Bitmap)app.oceanArt).GetPixel(0,0).A==0,"Wave transparency");
            Assert(app.planColor.R>app.planColor.G && app.planColor.G>app.planColor.B && app.DayColor(themeSelected)==app.planColor && app.reminderLabel.ForeColor==app.planColor,"Warm ocean task colors");
            Assert(app.list.Controls[0].Controls[1].ForeColor==app.planColor,"Ocean todo text color");
            Assert(app.selected==themeSelected && app.tipLabel.Text==themeTip && app.plans.Count==themePlans,"Theme preserves dates quotes and plans");
            Assert(((ToolStripMenuItem)app.themeMenu.DropDownItems[1]).Checked && !((ToolStripMenuItem)app.themeMenu.DropDownItems[0]).Checked,"Theme menu checked state");
            using(Bitmap bitmap=new Bitmap(app.Width,app.Height)) {app.DrawToBitmap(bitmap,new Rectangle(0,0,app.Width,app.Height)); bitmap.Save(Path.Combine(output,"preview-ocean.png"));}
            app.ApplyZoom(70,true); app.ApplyTheme("paper",false); app.ApplyTheme("ocean",true); app.settings=null; app.LoadSettings();
            Assert(app.settings.Theme=="ocean" && app.settings.ZoomPercent==70 && app.Width==581,"Theme and zoom persistence");
            using(Bitmap bitmap=new Bitmap(app.Width,app.Height)) {app.DrawToBitmap(bitmap,new Rectangle(0,0,app.Width,app.Height)); bitmap.Save(Path.Combine(output,"preview-ocean-70.png"));}
            app.ApplyTheme("paper",false); Assert(app.bamboo.Image==app.bambooArt && app.DayColor(themeSelected)==Color.FromArgb(52,102,159),"Paper theme restoration"); app.ApplyZoom(100,false);
            Point shownLocation=app.Location; app.Hide(); Assert(!app.Visible && app.tray.Visible,"Hide retains tray"); app.ShowCalendar(); Assert(app.Visible && app.Location==shownLocation,"Show retains position");
            string tipFontName=app.tipLabel.Font.Name;
            app.exiting=true; app.Close();
            File.WriteAllText(Path.Combine(output,"verification.txt"),"PASS: all six proportional zoom levels; zoom persistence; scaled calendar hit tests; resized dynamic todo controls; no cumulative layout drift or stale row records; 3653 unique daily quotes; ten-year uniqueness and cycling; lunar dates and leap month; holiday/workday badges and colors; blue plan precedence; nearest future unfinished-day reminder; all-completed reminder; cross-year holiday countdown; unknown years never guess holiday/workday badges; embedded transparent bamboo; XML save/reload; position memory; date rollover; tray hide/show.\r\nTip font: "+tipFontName+".\r\nStartup registration and online updates are excluded from isolated self-test.",Encoding.UTF8);
        }
    }
}
