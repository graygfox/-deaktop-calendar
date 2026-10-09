$ErrorActionPreference = 'Stop'
$base = Join-Path $PSScriptRoot 'corpus'
New-Item -ItemType Directory -Force $base | Out-Null
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class SimpleChinese {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern int LCMapStringEx(string locale, uint flags, string source, int length, StringBuilder dest, int capacity, IntPtr version, IntPtr reserved, IntPtr sort);
 public static string Convert(string s) { if(String.IsNullOrEmpty(s)) return ""; StringBuilder b=new StringBuilder(s.Length*2+10); int n=LCMapStringEx("zh-CN",0x02000000,s,-1,b,b.Capacity,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero); return n>0?b.ToString(0,n-1):s; }
}
'@
$categories = @(
 @{name='慢慢成长'; pattern='读书|学书|学问|勤|志|进|勉|行远|自强|千里|功夫|书卷|登高|直上|更上|会当'; notes=@('把一个大目标拆成小步，先完成今天这一步。','认真做完一件小事，也是在向前走。','给新技能一点耐心，熟悉总是从陌生开始。','今天学会一点点，就值得为自己高兴。','不必等到完全准备好，再开始一次小尝试。','把注意力放在能改变的事上，慢慢积累。','把困难写下来，再找一个可以着手的地方。','给努力留下一点记录，回头时会看见成长。','走得慢也可以，记得给自己留足呼吸的时间。','做事累了就休息，回来再继续也很好。','先做十分钟，往往比反复犹豫更有用。','让好奇心带你读一页书，或问一个新问题。') },
 @{name='看见美好'; pattern='春|花|芳|香|桃|柳|晴|暖|日光|朝阳|新绿|青山|碧|清风|白云|鸟|莺|燕'; notes=@('抬头看看窗外，给眼睛和心情一点空白。','认真看一朵花，平常的日子也藏着小惊喜。','有空时出去走走，让风陪你换换心情。','把今天看到的一件小美好记下来。','给忙碌留一个停顿，看看此刻身边的颜色。','拉开窗帘，让今天的光照进来。','把手机放下片刻，听听周围真实的声音。','慢一点走，也许会发现路边新开的花。','生活不必每天精彩，一点清亮也很珍贵。','找一个舒服的角落，安安静静待上几分钟。','为自己喜欢的小事留一点时间。','新的一天，可以从欣赏眼前的风景开始。') },
 @{name='温柔相伴'; pattern='友|相逢|相知|同游|欢|笑|喜|家|亲|团|共|相伴|相迎'; notes=@('向一个惦念的人问声好，温暖可以很简单。','收到善意时说声谢谢，也把善意传下去。','留点时间听朋友说话，不急着给出答案。','把想念变成一句问候，联系不必等特殊日子。','对身边的人说一句具体的赞美。','允许自己接受帮助，有人同行也是一种力量。','相处时多一点耐心，也给自己同样的温柔。','把今天的一个好消息分享给在意的人。','如果不知如何安慰，认真陪伴也很珍贵。','为一顿饭留出时间，和喜欢的人好好聊聊。','说话前停一下，让语气更柔和一点。','记得关心别人，也别忘了关心自己。') },
 @{name='安稳生活'; pattern='茶|竹|松|月|清|闲|静|安|宁|泉|溪|水|庭|窗|雨'; notes=@('整理桌面的一小块，让接下来的事情更清楚。','给自己倒一杯温水，歇一歇再继续。','事情多时先写下三件最想完成的事。','累了就停一会儿，休息也是照顾生活。','把今天做好的事记下来，不只盯着没完成的。','给晚上的自己留一点安静的时间。','把手边的一件事做完，再打开下一件。','深呼吸几次，让紧绷的肩膀慢慢放松。','今天不必事事完美，尽力之后也可以安心。','留一点空闲，生活需要可以自由呼吸的地方。','睡前放下忙碌，明天的事情明天再接着做。','为自己的小进展点个赞，平凡也值得珍惜。') }
)
# Exclude sombre, violent, romantic despair and defective passages.
$negative='死|杀|战|兵|戎|血|泪|哭|愁|忧|悲|恨|怨|病|痛|苦|衰|贫|孤|寂|坟|墓|鬼|冤|亡|凄|凶|残|断肠|肠断|离别|别离|无情|不见|无奈|酒|醉|妓|妾|奴|辱|刑|囚|祸|疫|饿|饥|剑|刀|戈|伐|尸|葬|骷|僧|佛|禅|帝|皇|君王|谗|讥|讽|贼|虏|仇|征|戍|虔|祭|焚|无望|寡|劫|哀|荒|枯|落花|花落|霜|寒|旧梦|残梦|独|悔|伤|闷|愤|怒|恶|险|折|萧|难|不得|不如|不堪|空自|徒|空悲|未归|断|黄泉|穷|怅|惆|叹|寥|冷|暮'
$negative+='|不|无|未|莫|非|空|俗|尘|神|仙|宫|殿|汉家|功名|宦|仕|祖|灵|王|官|客|谪|遁|逃|鼎|强游|蹇|衔华|归期|去国|故国|虞|废|败|衰|老|夕阳|斜阳|斜晖|夕照|晚照|白发|白头|苍颜|黄昏|零|伶|飘蓬|飘零|梦|蛮|瘴|蛊|蛟|虎|猿|豺|狼|荆|刃|蠹|鞭|妖|谀|罪|槛|骂|判|嘲|猥|谩|佞|伪|浊|迷|穹|蝉|朽|戚|亵|龊|奸|戾|谶|狱|獠|饿|痴|傻|孽|嘘|翁|骨|匈|胡儿|边塞|羁|焰|泥|役|荼|毒|屠|杖|疮|炙|嫖|贪|畏|恐|杀|阴|闰|生死|干戈'
$negative+='|避|罗网|劒|鵰|鹗|霸|至尊|銮|舆|邦|祚|侵|威|陇|别|太守|元帅|旌|麾|军|愚|识|誓|谬|荒|谐|趋|唯|爲|岂|讵|孝|寝|撩|乞|赐|讨|恭|诬|虐|五刑|六师|献|颂|朝廷|万岁|万世'
$negative+='|忌|藏辉|洁|沈牛|沉牛|束马|辽落|缅岁|争|逐|纵|缚|蔽|帐|枉|饵|誉|机心|繁华|俗流|恋'
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
$buckets = @{}; foreach($category in $categories) { $buckets[$category.name] = New-Object 'System.Collections.Generic.List[object]' }
$sourceFiles = New-Object 'System.Collections.Generic.List[string]'
for($batch=0; $batch -lt 220; $batch++) {
 $offset=$batch*1000
 $dynasty=if($batch -lt 58){'tang'}else{'song'}
 $offset=if($batch -lt 58){$offset}else{($batch-58)*1000}
 $filename="poet.$dynasty.$offset.json"
 $url="https://raw.githubusercontent.com/chinese-poetry/chinese-poetry/master/%E5%85%A8%E5%94%90%E8%AF%97/$filename"
 $path=Join-Path $base $filename
 if(!(Test-Path $path)) { Invoke-WebRequest -Uri $url -OutFile $path -TimeoutSec 30 }
 $sourceFiles.Add($url)
 $poems=[IO.File]::ReadAllText($path,[Text.Encoding]::UTF8) | ConvertFrom-Json
 foreach($poem in $poems) {
  $author=[SimpleChinese]::Convert($poem.author); $title=[SimpleChinese]::Convert($poem.title)
  if($author -match '帝|不详' -or $title -match '郊庙|祭|庙|祀|圣|诏|帝|御|奉|挽|悼|哭|兵|戎|边|塞|战|野田|招魂|贬|宫|殿|幸|禁|上人|国|剑|寺|道士|僧|咏史|谒|敕|皇|乐章|教|释|法|沐浴|畴昔|锡|臣|狩') {continue}
  foreach($paragraph in $poem.paragraphs) {
   $quote=[SimpleChinese]::Convert($paragraph).Trim()
   if($quote.Length -lt 12 -or $quote.Length -gt 26 -or $quote -match $negative -or $quote -match '[^\u4e00-\u9fff，。！？、]' -or $quote -notmatch '^[\u4e00-\u9fff]{5,7}，[\u4e00-\u9fff]{5,7}。$') { continue }
   if($seen.Contains($quote)) { continue }
   foreach($category in $categories) {
    if($quote -match $category.pattern) {
     [void]$seen.Add($quote)
     $buckets[$category.name].Add([pscustomobject]@{quote=$quote; author=$author; title=$title; source=$url; theme=$category.name})
     break
    }
   }
  }
 }
 if(($batch+1)%5 -eq 0) { Write-Output "Processed $($batch+1) files; eligible unique passages: $($seen.Count)" }
 if($seen.Count -ge 4400) { break }
}
if($seen.Count -lt 3653) { throw "Insufficient eligible unique passages: $($seen.Count)" }
# Stable shuffle and interleaved themes; independent source passages, never generated quote variants.
$random=New-Object Random(20261009)
foreach($category in $categories) {
 $bucket=$buckets[$category.name]
 for($i=$bucket.Count-1; $i -gt 0; $i--) { $j=$random.Next($i+1); $item=$bucket[$i]; $bucket[$i]=$bucket[$j]; $bucket[$j]=$item }
}
$rows=New-Object 'System.Collections.Generic.List[object]'; $positions=@{}; foreach($category in $categories){$positions[$category.name]=0}
$seedQuotes=@('长风破浪会有时，直挂云帆济沧海。','欲穷千里目，更上一层楼。','会当凌绝顶，一览众山小。','海上生明月，天涯共此时。','行到水穷处，坐看云起时。','好雨知时节，当春乃发生。','随风潜入夜，润物细无声。','迟日江山丽，春风花草香。','明月松间照，清泉石上流。','春风得意马蹄疾，一日看尽长安花。','沉舟侧畔千帆过，病树前头万木春。','千淘万漉虽辛苦，吹尽狂沙始到金。','读书破万卷，下笔如有神。','两个黄鹂鸣翠柳，一行白鹭上青天。','江碧鸟逾白，山青花欲燃。','白日依山尽，黄河入海流。')
$seedRecords=@{}
$seedAuthors=@('李白','王之涣','杜甫','张九龄','王维','杜甫','杜甫','杜甫','王维','孟郊','刘禹锡','刘禹锡','杜甫','杜甫','杜甫','王之涣')
foreach($file in Get-ChildItem $base -Filter 'poet.*.json') {
 if($seedRecords.Count -eq $seedQuotes.Count) {break}
 $seedPoems=[IO.File]::ReadAllText($file.FullName,[Text.Encoding]::UTF8) | ConvertFrom-Json
 foreach($poem in $seedPoems) { foreach($paragraph in $poem.paragraphs) {
  $line=[SimpleChinese]::Convert($paragraph)
  foreach($quote in $seedQuotes) { if(!$seedRecords.ContainsKey($quote) -and $line.Contains($quote) -and [SimpleChinese]::Convert($poem.author) -eq $seedAuthors[[Array]::IndexOf($seedQuotes,$quote)]) {
   $seedRecords[$quote]=[pscustomobject]@{quote=$quote;author=[SimpleChinese]::Convert($poem.author);title=[SimpleChinese]::Convert($poem.title);source="https://raw.githubusercontent.com/chinese-poetry/chinese-poetry/master/%E5%85%A8%E5%94%90%E8%AF%97/$($file.Name)"}
  } }
 } }
}
$outputSeen=New-Object 'System.Collections.Generic.HashSet[string]'
foreach($quote in $seedQuotes) {
 if(!$seedRecords.ContainsKey($quote)) {continue}
 $item=$seedRecords[$quote]; [void]$outputSeen.Add($quote)
 $rows.Add([pscustomobject]@{id=$rows.Count+1;note=$categories[0].notes[$rows.Count%12];quote=$quote;author=$item.author;title=$item.title;source=$item.source;theme='每日心语'})
 if(!$sourceFiles.Contains($item.source)) {$sourceFiles.Add($item.source)}
}
while($rows.Count -lt 3653) {
 foreach($category in $categories) {
  $position=$positions[$category.name]; $bucket=$buckets[$category.name]
  if($position -ge $bucket.Count) {continue}
  $item=$bucket[$position]; $positions[$category.name]++
  if(!$outputSeen.Add($item.quote)) {continue}
  $note=$category.notes[$position % $category.notes.Count]
  $rows.Add([pscustomobject]@{id=$rows.Count+1; note=$note; quote=$item.quote; author=$item.author; title=$item.title; source=$item.source; theme=$item.theme})
  if($rows.Count -eq 3653) {break}
 }
}
$tsv=($rows | ForEach-Object { @($_.note,$_.quote,$_.author,$_.title,$_.source,$_.theme) -join "`t" }) -join "`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'tips.tsv'),$tsv,(New-Object Text.UTF8Encoding($false)))
$rows | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $PSScriptRoot 'tips-with-sources.json') -Encoding utf8
$sourceFiles | Set-Content (Join-Path $base 'downloaded-sources.txt') -Encoding utf8
if(($rows.quote | Select-Object -Unique).Count -ne 3653) {throw 'Duplicate quote detected'}
Write-Output "Verified: $($rows.Count) distinct sourced passages; 48 original companion reminders."
