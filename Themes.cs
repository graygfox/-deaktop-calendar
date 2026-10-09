using System.Drawing;
using System.Windows.Forms;

public class ThemePalette {
    public string Id,Name;
    public Color Paper,Ink,Muted,Panel,Entry,Border,Frame,Grain,Grid,Selected,Outside,Plan,Holiday,Work,Check,CheckBorder,Tick;
    public static ThemePalette Get(string id) {
        if(id=="ocean") return new ThemePalette {
            Id="ocean",Name="蓝色海浪",Paper=Color.FromArgb(198,220,240),Ink=Color.FromArgb(24,47,70),Muted=Color.FromArgb(66,91,115),
            Panel=Color.FromArgb(221,236,248),Entry=Color.FromArgb(239,247,253),Border=Color.FromArgb(122,155,181),Frame=Color.FromArgb(141,171,196),Grain=Color.FromArgb(188,213,233),
            Grid=Color.FromArgb(158,184,207),Selected=Color.FromArgb(176,202,224),Outside=Color.FromArgb(76,96,115),Plan=Color.FromArgb(124,58,12),
            Holiday=Color.FromArgb(151,30,42),Work=Color.FromArgb(35,35,35),Check=Color.FromArgb(124,58,12),CheckBorder=Color.FromArgb(95,127,154),Tick=Color.FromArgb(255,247,226)
        };
        return new ThemePalette {
            Id="paper",Name="米黄墨竹",Paper=Color.FromArgb(245,232,195),Ink=Color.FromArgb(79,65,45),Muted=Color.FromArgb(113,119,73),
            Panel=Color.FromArgb(250,239,207),Entry=Color.FromArgb(255,246,220),Border=Color.FromArgb(199,178,132),Frame=Color.FromArgb(215,196,153),Grain=Color.FromArgb(239,225,185),
            Grid=Color.FromArgb(211,193,151),Selected=Color.FromArgb(222,205,156),Outside=Color.FromArgb(173,155,119),Plan=Color.FromArgb(52,102,159),
            Holiday=Color.FromArgb(184,65,51),Work=Color.FromArgb(35,35,35),Check=Color.FromArgb(113,119,73),CheckBorder=Color.FromArgb(145,122,77),Tick=Color.White
        };
    }
}
public class ThemeMenuColors : ProfessionalColorTable {
    readonly ThemePalette palette;
    public ThemeMenuColors(ThemePalette theme) {palette=theme; UseSystemColors=false;}
    public override Color ToolStripDropDownBackground {get {return palette.Paper;}}
    public override Color MenuBorder {get {return palette.Border;}}
    public override Color MenuItemBorder {get {return palette.Border;}}
    public override Color MenuItemSelected {get {return palette.Selected;}}
    public override Color MenuItemSelectedGradientBegin {get {return palette.Selected;}}
    public override Color MenuItemSelectedGradientEnd {get {return palette.Selected;}}
    public override Color ImageMarginGradientBegin {get {return palette.Panel;}}
    public override Color ImageMarginGradientMiddle {get {return palette.Panel;}}
    public override Color ImageMarginGradientEnd {get {return palette.Panel;}}
    public override Color CheckBackground {get {return palette.Panel;}}
    public override Color CheckSelectedBackground {get {return palette.Selected;}}
    public override Color CheckPressedBackground {get {return palette.Selected;}}
    public override Color SeparatorDark {get {return palette.Border;}}
    public override Color SeparatorLight {get {return palette.Panel;}}
}
