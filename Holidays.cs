using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public class HolidayDay { public string name; public string date; public bool isOffDay; }
public class HolidayYear { public int year; public string[] papers; public List<HolidayDay> days; }
public class HolidayRange { public string Name; public DateTime Start, End; public bool Confirmed; }

public class ChinaCalendar {
    readonly Dictionary<int,HolidayYear> years=new Dictionary<int,HolidayYear>();
    readonly Dictionary<string,HolidayDay> dates=new Dictionary<string,HolidayDay>();
    readonly ChineseLunisolarCalendar lunar=new ChineseLunisolarCalendar();
    readonly string folder;
    static readonly string[] monthNames={"正月","二月","三月","四月","五月","六月","七月","八月","九月","十月","冬月","腊月"};
    static readonly string[] dayNames={"初一","初二","初三","初四","初五","初六","初七","初八","初九","初十","十一","十二","十三","十四","十五","十六","十七","十八","十九","二十","廿一","廿二","廿三","廿四","廿五","廿六","廿七","廿八","廿九","三十"};
    public ChinaCalendar(string dataFolder) {
        folder=Path.Combine(dataFolder,"holidays");
        using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("PaperCalendar.Holidays2026")) {
            if(resource!=null) using(StreamReader reader=new StreamReader(resource,Encoding.UTF8)) Merge(Parse(reader.ReadToEnd(),2026,DateTime.Today));
        }
        for(int y=DateTime.Today.Year-1;y<=DateTime.Today.Year+1;y++) {
            string path=Path.Combine(folder,y+".json");
            try {if(File.Exists(path)) Merge(Parse(File.ReadAllText(path,Encoding.UTF8),y,DateTime.Today));} catch(InvalidDataException) {} catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
    }
    public bool HasYear(int year) {return years.ContainsKey(year);}
    public HolidayDay Get(DateTime day) {HolidayDay result; dates.TryGetValue(day.ToString("yyyy-MM-dd"),out result); return result;}
    public string Marker(DateTime day) {HolidayDay info=Get(day); return info==null?"":info.isOffDay?"假":"班";}
    public string LunarLabel(DateTime day) {
        if(day<lunar.MinSupportedDateTime || day>lunar.MaxSupportedDateTime) return "";
        int year=lunar.GetYear(day),m=lunar.GetMonth(day),d=lunar.GetDayOfMonth(day),leap=lunar.GetLeapMonth(year);
        bool leapMonth=leap>0 && m==leap; if(leap>0 && m>=leap) m--;
        return d==1 ? (leapMonth?"闰":"")+monthNames[m-1] : dayNames[d-1];
    }
    public string LunarFull(DateTime day) {
        if(day<lunar.MinSupportedDateTime || day>lunar.MaxSupportedDateTime) return "农历超出支持范围";
        int year=lunar.GetYear(day),m=lunar.GetMonth(day),d=lunar.GetDayOfMonth(day),leap=lunar.GetLeapMonth(year);
        bool leapMonth=leap>0 && m==leap; if(leap>0 && m>=leap) m--;
        return "农历 "+(leapMonth?"闰":"")+monthNames[m-1]+dayNames[d-1];
    }
    public void Merge(HolidayYear data) {
        HolidayYear old; if(years.TryGetValue(data.year,out old)) foreach(HolidayDay d in old.days) dates.Remove(d.date);
        years[data.year]=data; foreach(HolidayDay d in data.days) dates[d.date]=d;
    }
    public static HolidayYear Parse(string json,int expectedYear,DateTime today) {
        if(json.Length>1048576) throw new InvalidDataException("节假日数据过大。");
        JavaScriptSerializer serializer=new JavaScriptSerializer {MaxJsonLength=1048576};
        HolidayYear data;
        try {
            // Check required fields explicitly; a missing Boolean must not become a false workday.
            Dictionary<string,object> raw=serializer.DeserializeObject(json) as Dictionary<string,object>;
            if(raw==null || !raw.ContainsKey("year") || !raw.ContainsKey("days") || !raw.ContainsKey("papers")) throw new InvalidDataException("节假日数据缺少字段。");
            object[] entries=raw["days"] as object[]; if(entries==null) throw new InvalidDataException("节假日日期格式无效。");
            foreach(object entry in entries) {Dictionary<string,object> row=entry as Dictionary<string,object>; if(row==null || !row.ContainsKey("name") || !row.ContainsKey("date") || !row.ContainsKey("isOffDay") || !(row["isOffDay"] is bool)) throw new InvalidDataException("放假或调休字段缺失。");}
            data=serializer.Deserialize<HolidayYear>(json);
        } catch(ArgumentException e) {throw new InvalidDataException("节假日 JSON 无效。",e);}
        if(data.year!=expectedYear || data.days==null || data.days.Count==0 || data.days.Count>100 || data.papers==null || data.papers.Length==0) throw new InvalidDataException("该年度放假安排尚未公布。");
        foreach(string paper in data.papers) {
            Uri source; if(!Uri.TryCreate(paper,UriKind.Absolute,out source) || (source.Scheme!="https" && source.Scheme!="http") || !(source.Host=="gov.cn" || source.Host.EndsWith(".gov.cn",StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("缺少官方公告来源。");
            Match date=Regex.Match(source.AbsolutePath,@"/(?<y>20\d{2})(?<m>\d{2})(?:/|-)");
            if(date.Success) {int y=Int32.Parse(date.Groups["y"].Value),m=Int32.Parse(date.Groups["m"].Value); if(m<1 || m>12 || new DateTime(y,m,1)>today.Date) throw new InvalidDataException("公告日期晚于当前日期。");}
        }
        HashSet<string> seen=new HashSet<string>();
        foreach(HolidayDay d in data.days) {
            DateTime day; if(!DateTime.TryParseExact(d.date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day) || day.Year<expectedYear-1 || day.Year>expectedYear || String.IsNullOrEmpty(d.name) || d.name.Length>12 || !seen.Add(d.date)) throw new InvalidDataException("节假日日期或名称无效。");
        }
        return data;
    }
    public HolidayYear Download(int year,DateTime today) {
        string address="https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/"+year+".json";
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        HttpWebRequest request=(HttpWebRequest)WebRequest.Create(address); request.Timeout=8000; request.ReadWriteTimeout=8000; request.UserAgent="PaperCalendar/2.0";
        string json;
        using(WebResponse response=request.GetResponse()) using(Stream stream=response.GetResponseStream()) using(MemoryStream buffer=new MemoryStream()) {
            byte[] bytes=new byte[8192]; int count; while((count=stream.Read(bytes,0,bytes.Length))>0) {buffer.Write(bytes,0,count); if(buffer.Length>1048576) throw new InvalidDataException("节假日数据过大。");} json=Encoding.UTF8.GetString(buffer.ToArray());
        }
        HolidayYear data=Parse(json,year,today);
        Directory.CreateDirectory(folder); string path=Path.Combine(folder,year+".json"),temp=path+".tmp"; File.WriteAllText(temp,json,Encoding.UTF8); if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
        return data;
    }
    public HolidayRange NearestHoliday(DateTime today) {
        List<HolidayRange> candidates=new List<HolidayRange>();
        for(int year=today.Year;year<=today.Year+1;year++) {
            HolidayYear data;
            if(years.TryGetValue(year,out data)) {
                List<HolidayDay> off=new List<HolidayDay>(); foreach(HolidayDay d in data.days) if(d.isOffDay) off.Add(d);
                off.Sort(delegate(HolidayDay a,HolidayDay b) {return String.CompareOrdinal(a.date,b.date);});
                HolidayRange range=null;
                foreach(HolidayDay d in off) {
                    DateTime date=DateTime.ParseExact(d.date,"yyyy-MM-dd",CultureInfo.InvariantCulture);
                    if(range!=null && range.Name==d.name && date==range.End.AddDays(1)) range.End=date;
                    else {range=new HolidayRange {Name=d.name,Start=date,End=date,Confirmed=true}; candidates.Add(range);}
                }
            } else {
                AddFestival(candidates,"元旦",new DateTime(year,1,1)); AddFestival(candidates,"劳动节",new DateTime(year,5,1)); AddFestival(candidates,"国庆节",new DateTime(year,10,1)); AddFestival(candidates,"清明节",Qingming(year));
                DateTime start=new DateTime(year,1,1),end=new DateTime(year,12,31);
                for(DateTime date=start;date<=end;date=date.AddDays(1)) {
                    if(date<lunar.MinSupportedDateTime || date>lunar.MaxSupportedDateTime) continue;
                    int lunarYear=lunar.GetYear(date),m=lunar.GetMonth(date),day=lunar.GetDayOfMonth(date),leap=lunar.GetLeapMonth(lunarYear);
                    if(leap>0 && m==leap) continue; if(leap>0 && m>leap) m--;
                    if(m==1 && day==1) AddFestival(candidates,"春节",date);
                    if(m==5 && day==5) AddFestival(candidates,"端午节",date);
                    if(m==8 && day==15) AddFestival(candidates,"中秋节",date);
                }
            }
        }
        candidates.Sort(delegate(HolidayRange a,HolidayRange b) {return a.Start.CompareTo(b.Start);});
        foreach(HolidayRange range in candidates) if(range.End>=today.Date) return range;
        return null;
    }
    static void AddFestival(List<HolidayRange> list,string name,DateTime date) {list.Add(new HolidayRange {Name=name,Start=date,End=date,Confirmed=false});}
    public string Countdown(DateTime today) {
        HolidayRange range=NearestHoliday(today.Date); if(range==null) return "暂未查到下一节假日";
        if(range.Start<=today.Date && range.Confirmed) return "正在"+range.Name+"假期 · 还有 "+((range.End-today.Date).Days+1)+" 天";
        int days=(range.Start-today.Date).Days;
        string text=days==0?"今天是"+range.Name:"距"+range.Name+"还有 "+days+" 天";
        return text+(range.Confirmed?"":" · 放假安排待公布");
    }
    // Approximate apparent solar longitude, using the NOAA/Meeus solar-position equations.
    // Only the festival date is computed here; unannounced holiday/workday ranges are never inferred.
    static double Sin(double degrees) {return Math.Sin(degrees*Math.PI/180.0);}
    static double Longitude(DateTime utc) {
        double jd=2451545.0+(utc-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays;
        double t=(jd-2451545.0)/36525.0;
        double mean=280.46646+t*(36000.76983+0.0003032*t),anomaly=357.52911+t*(35999.05029-0.0001537*t);
        double center=Sin(anomaly)*(1.914602-t*(0.004817+0.000014*t))+Sin(2*anomaly)*(0.019993-0.000101*t)+Sin(3*anomaly)*0.000289;
        double value=mean+center-0.00569-0.00478*Sin(125.04-1934.136*t); return ((value%360)+360)%360;
    }
    public static DateTime Qingming(int year) {
        DateTime left=new DateTime(year,4,3,0,0,0,DateTimeKind.Utc),right=left.AddDays(4);
        for(int i=0;i<40;i++) {DateTime middle=left.AddTicks((right.Ticks-left.Ticks)/2); if(Longitude(middle)<15) left=middle; else right=middle;}
        return right.AddHours(8).Date;
    }
}
