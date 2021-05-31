using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Linq;
using FNF.Utility;
using FNF.XmlSerializerSetting;
using FNF.BouyomiChanApp;
using System.Net;
using System.Web.Script.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace Plugin_TW
{
    public class Plugin_TW : IPlugin
    {
        #region ■フィールド

        private Settings _Settings;                                                       //設定
        private SettingFormData_TW _SettingFormData;
        private string _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting"; //設定ファイルの保存場所
        private System.Threading.Timer _Timer;
        private long _CurrentPosition = 0;
        private List<MapType> _SdtMapList = new List<MapType>();
        private bool outputReady = false;
        private bool postAlready = false;

        private const string CLUB_COLOR = @"#94ddfa";
        private const string TEAM_COLOR = @"#f7b73c";
        private const string SYSTEM_COLOR = @"#ff64ff";
        private const string GENERAL_COLOR = @"#ffffff";
        private const string GENERAL_OWN_COLOR = @"#c8ffc8";
        private const string GM_COLOR = @"#c8ffff";
        private const string DM_COLOR = @"#64ff64";
        private const string MANAGEMENT_COLOR = @"#64ff80";

        /*
            Boss,
            Slot,
            Lever,
            Brain,
            Maze,
            Tombstone,
            FourExit,
            Exit,
            Toge,
            TreeFire,
            TreeBow,
            ThreeBoss,
            Ghost,
            Belesis
         */

        private Dictionary<string, MapType> MapDic = new Dictionary<string, MapType>()
        {
             { "マップの奥にいるボスモンスターを退治してください。", MapType.Boss }
            ,{ "になるように数字カードを見つけて中央の装置に投入して下さい。", MapType.Slot}
            ,{ "一人が複数のレバーを引いてもカウントされます", MapType.Lever}
            ,{ "マップ中央のボタンの上に乗せてください。", MapType.Brain}
            ,{ "無造作に爆発する地点を避けて中央部にある目的地に到達してください", MapType.Maze}
            ,{ "マップ中央の碑石の上に乗ってください", MapType.Tombstone}
            ,{ "自分に付与された数字と一致する出口を見つけてください。", MapType.FourExit}
            ,{ "新しい脱出装置が生成されました。脱出装置は一定時間が経つと消えます。", MapType.Exit}
            ,{ "変異したトゲリーナ探し", MapType.Toge}
            ,{ "降り注ぐ矢を避けてメンバー全員が各部屋の中央に乗ってください。", MapType.TreeBow}
            ,{ "降り注ぐ炎を避けてメンバー全員が各部屋の中央に乗ってください。", MapType.TreeFire}
            ,{ "散らばっている3匹のボスを見つけて全て退治してください。", MapType.ThreeBoss}
            ,{ "見えない幽霊を避けて左下にある目的地に到達してください。", MapType.Ghost}
            ,{ "[古代ベレシス]の攻撃から", MapType.Belesis}
        };


        #endregion


        #region ■IPluginメンバの実装

        public string Name { get { return "TWチャット読み上げ"; } }

        public string Version { 
            get {
                String ver = "2021.03.20";
#if DEBUG
                ver += "-debug";

#endif
                return ver; 
            } 
        }

        public string Caption { get { return "TWチャット読み上げ"; } }

        public ISettingFormData SettingFormData { get { return _SettingFormData; } } //プラグインの設定画面情報

        //プラグイン開始時処理
        public void Begin()
        {
            //設定ファイル読み込み
            _Settings = new Settings(this);
            _Settings.Load(_SettingFile);
            _SettingFormData = new SettingFormData_TW(_Settings);
            // 最初は全部読みポジションを最後に設定しておく
            _CurrentPosition = GetFileLength(GetChatLogFileName());

            // 棒読みちゃんで読み上げられた時に発行されるイベントハンドラ
            // Pub.FormMain.BC.TalkTaskStarted += new EventHandler<BouyomiChan.TalkTaskStartedEventArgs>(BC_TalkTaskStarted);

            //タイマ登録
            _Timer = new System.Threading.Timer(Timer_Event, null, 0, 1000);

        }

        //プラグイン終了時処理
        public void End()
        {
            //設定ファイル保存
            _Settings.Save(_SettingFile);

            //タイマ開放
            if (_Timer != null)
            {
                _Timer.Dispose();
                _Timer = null;
            }

        }

        #endregion


        #region ■メソッド・イベント処理

        // 棒読みちゃんで読み上げられた時に発行されるイベント実装
        //private void BC_TalkTaskStarted(object sender, BouyomiChan.TalkTaskStartedEventArgs eve)
        //{
        //    LogError(eve.TalkTask.SourceText.ToString());
            

        //}

        /// <summary>
        /// デバッグログを追加。Debugでビルドした場合に表示されるログ。
        /// </summary>
        /// <param name="message"></param>
        private void LogError(string message)
        {
            Log.AddDebug(Base.CallAsmName, message, LogType.Error);
        }

        /// <summary>
        /// チャットログがXML形式になっていないので、XML形式に変換する
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private string ConvertLineToXml(string line)
        {
            // </br>と&nbspは除去する
            string xml = string.Format("<message>{0}</message>", line.Replace("</br>", "").Replace(@"&nbsp", ""));
            return xml;

        }

        private Message DeserializeToMessage(string xml)
        {

            XmlSerializer serializer = new XmlSerializer(typeof(Message));
            Message message = (Message)serializer.Deserialize(new StringReader(xml));
            return message;
        }

        private long GetFileLength(string filename)
        {
            if (!File.Exists(filename))
            {
                return 0;
            }
            FileInfo file = new FileInfo(filename);
            return file.Length;

        }

        private string GetChatLogFileName()
        {
            DateTime now = DateTime.Now;
            string today = string.Format(@"{0:D4}_{1:D2}_{2:D2}", now.Year, now.Month, now.Day);
            string filename = string.Format(@"{0}\ChatLog\TWChatLog_{1}.html", _Settings.RootDir, today);
            return filename;
        }

        private CharInfo GetCurrentCharInfo() 
        {
            string file = Directory.GetFiles(_Settings.RootDir, "*.profile").OrderByDescending(f => File.GetLastWriteTime(f)).First();
            CharInfo charInfo =  new CharInfo();
            charInfo.ServerName = Path.GetFileName(file).Split('_')[0];
            charInfo.CharName = Path.GetFileName(file).Split('_')[1].Split('.')[0];
            return charInfo;
        }

        //タイマイベント
        private void Timer_Event(object obj)
        {
            string filename = GetChatLogFileName();

            // 最後に読み込んだ箇所から新規追加された文章を読み込む
            List<string> lines = GetLinesFromLastLine(filename);

            foreach (string line in lines)
            {
                try
                {
                    Message message = DeserializeToMessage(ConvertLineToXml(line));
                    Content[] contents = message.Contents.ToArray();
                    Content date = contents[0];
                    Content content = contents[1];
                    String d = date.Text.Replace("[", "").Replace("]", "").Replace(" ", "").Replace("時", ":").Replace("分", ":").Replace("秒", "").Trim();
                    String setTime = DateTime.Now.ToLongDateString() + " " + d;

                    if(_Settings.SdtEnabled)
                    {
                        AddSdt(content);
                        if(!postAlready && outputReady)
                        {
                            PostStd();
                        }
                    }

                    var task = ExecutedTask(content, setTime);
                    if (task != null) {
                        PostTask(task);
                    }

                    if (IsDisplayed(content))
                    {
                        if (!_Settings.ReadSpeakerName)
                        {
                            TrimSpeakerName(content);
                        }
                        CalcSpecialQA(content);
                        Pub.AddTalkTask(content.Text, -1, -1, VoiceType.Default);
                    }
                }
                catch (Exception e)
                {
                    LogError(e.Message);
                }

            }
        }

        private void PostTask(Task task) {
            string endpoint = _Settings.EndPointTask;
            if (endpoint == "")
            {
                return;
            }
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.ContentType = "application/json";
            req.Method = "POST";

            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {
                string jsonPayload = new JavaScriptSerializer().Serialize(new
                {
                    name = task.CharName,
                    col = task.Col,
                    sheet = task.Sheet,
                    time = task.Time,
                    matchStr = task.MatchStr
                });
                streamWriter.Write(jsonPayload);
            }

            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            RequestResult result;
            using (res)
            {
                using (var resStream = res.GetResponseStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(RequestResult));
                    result = (RequestResult)serializer.ReadObject(resStream);
                }
            }
        }

        private void PostStd()
        {
            string endpoint = _Settings.SdtDiscordWebhook;
            if (endpoint == "")
            {
                return;
            }
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.ContentType = "application/json";
            req.Method = "POST";
            _SdtMapList.RemoveAt(_SdtMapList.Count - 1);
            string joinedString = "■争奪マップ順番\n```\n";
            joinedString += string.Join("\n", _SdtMapList.Select(s => s.GetStringValue()).ToArray());
            joinedString += "\n```";
            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {


                string jsonPayload = new JavaScriptSerializer().Serialize(new
                {
                    content = joinedString
                });
                streamWriter.Write(jsonPayload);
            }

            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            RequestResult result;
            if(res.StatusCode == HttpStatusCode.NoContent)
            {
                postAlready = true;
            }
            using (res)
            {
                using (var resStream = res.GetResponseStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(RequestResult));
                    result = (RequestResult)serializer.ReadObject(resStream);
                    
                }
            }
        }

        [DataContract]
        public class RequestResult
        {
            [DataMember]
            public string message { get; set; }
        }

        private void CalcSpecialQA(Content content)
        {
            Regex re = new Regex("q(?<question>[0-9]+)a(?<answer>[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (re.IsMatch(content.Text))
            {
                Match m = re.Match(content.Text);
                int target = int.Parse(m.Groups["question"].Value);
                string answer = m.Groups["answer"].Value;
                List<int> numbers = new List<int>();
                for (int i = 0; i < answer.Length; i++)
                {
                    numbers.Add(int.Parse(answer[i].ToString()));
                }

                content.Text = SumUp(numbers, target);
            }
        }

        private void TrimSpeakerName(Content content) 
        {
            content.Text = Regex.Replace(content.Text, "^[^:]+ :", String.Empty);
        }

        private Boolean IsDisplayed(Content content)
        {
            if (Includes(content))
            {
                return true;
            }

            if (Excludes(content))
            {
                return false;
            }
            Boolean isDisplayedByChatType = CheckChatTypeIsDisplayed(content);
            if (isDisplayedByChatType) { 
                if (IsOwnChat(content)) {
                    return _Settings.OwnChatEnabled;
                }
                return true;
            }
            return false;

        }

        private Boolean CheckChatTypeIsDisplayed(Content content)
        {
            if (_Settings.ClubEnabled && IsClub(content))
            {
                return true;
            }

            if (_Settings.TeamEnabled && IsTeam(content))
            {
                return true;
            }
            if (_Settings.SystemEnabled && IsSystem(content))
            {
                return true;
            }
            if (_Settings.AdminEnabled && IsAdmin(content))
            {
                return true;
            }
            if (_Settings.DMEnabled && IsDM(content))
            {
                return true;
            }
            if (_Settings.GeneralEnabled && IsGeneral(content))
            {
                return true;
            }
            if (_Settings.GMEnabled && IsGM(content))
            {
                return true;
            }
            return false;
        }

        private Boolean IsClub(Content font)
        {
            return font.Color == CLUB_COLOR;
        }

        private Boolean IsTeam(Content font)
        {
            return font.Color == TEAM_COLOR;
        }
        private Boolean IsGeneral(Content font)
        {
            return font.Color == GENERAL_COLOR || font.Color == GENERAL_OWN_COLOR;
        }

        private Boolean IsGM(Content font)
        {
            return font.Color == GM_COLOR;
        }

        private Boolean IsSystem(Content font)
        {
            return font.Color == SYSTEM_COLOR;
        }
        private Boolean IsAdmin(Content font)
        {
            return font.Color == MANAGEMENT_COLOR;
        }
        private Boolean IsDM(Content font)
        {
            return font.Color == DM_COLOR;
        }
        private Boolean IsOwnChat(Content content)
        {
            CharInfo charInfo = GetCurrentCharInfo();
            if (content.Text.Trim().Contains(charInfo.CharName + " :"))
            {
                return true;
            }
            return false;
        }

        private Boolean Includes(Content content)
        {
            foreach (var include in _Settings.Includes)
            {
                if (include == String.Empty)
                {
                    continue;
                }
                if (Regex.IsMatch(content.Text, include))
                {
                    return true;
                }
            }
            return false;
        }

        private Boolean Excludes(Content content)
        {
            foreach (var exclude in _Settings.Excludes)
            {
                if (exclude == String.Empty) {
                    continue;
                }
                if (Regex.IsMatch(content.Text, exclude))
                {
                    return true;
                }
            }
            return false;
        }

        private Task ExecutedTask(Content content, String time)
        {
            foreach (var task in _Settings.TaskWords) {
                if (Regex.IsMatch(content.Text, task.MatchStr)) {
                    CharInfo charInfo = GetCurrentCharInfo();
                    string charName = charInfo.CharName;
                    task.CharName = charName;
                    task.Time = time;
                    return task;
                }
            }
            return null;
        }



        /// <summary>
        /// 最後に読み込んだ箇所から新規追加された文章を読み込む
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private List<string> GetLinesFromLastLine(string filename)
        {
            List<string> lines = new List<string>();
            try
            {
                using (FileStream fs = new FileStream(filename,
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(fs,
                        Encoding.GetEncoding("shift-jis")))
                    {
                        if (reader.BaseStream.Length >= _CurrentPosition)
                        {
                            reader.BaseStream.Seek(_CurrentPosition, SeekOrigin.Begin);
                        }
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lines.Add(line);
                            _CurrentPosition = fs.Position;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
            return lines;

        }

        private string SumUp(List<int> numbers, int target)
        {
            return SumUpRecursive(numbers, target, new List<int>());

        }

        private string SumUpRecursive(List<int> numbers, int target, List<int> partial)
        {
            int s = 0;
            string ans;
            foreach (int x in partial) s += x;

            if (s == target)
            {
                return string.Join(",", partial.Select(x => x.ToString()).ToArray());
            }

            if (s >= target) return string.Empty;

            for (int i = 0; i < numbers.Count; i++)
            {
                List<int> remaining = new List<int>();
                int n = numbers[i];
                for (int j = i + 1; j < numbers.Count; j++) remaining.Add(numbers[j]);
                List<int> partial_rec = new List<int>(partial);
                partial_rec.Add(n);
                ans = SumUpRecursive(remaining, target, partial_rec);
                if (!string.IsNullOrEmpty(ans))
                {
                    return ans;
                }
            }
            return string.Empty;

        }

        private void InitSdtMap()
        {
            _SdtMapList = new List<MapType>();
            outputReady = false;
            postAlready = false;
        }

        private void AddSdt(Content content)
        {
            if(IsSystem(content) && content.Text.Trim().Contains("アバンドンロード争奪戦が開始しました。"))
            {
                InitSdtMap();
            }
            if(!outputReady)
            {
                foreach(var pair in MapDic)
                {
                    if (IsSystem(content) && content.Text.Trim().Contains(pair.Key))
                    {
                        _SdtMapList.Add(pair.Value);
                        if(pair.Value == MapType.Boss)
                        {
                            var count = _SdtMapList.Count(n => n == MapType.Boss);
                            if (count >= 7)
                            {
                                outputReady = true;
                            }
                        } else
                        {
                            var count = _SdtMapList.Count(n => n == pair.Value);
                            if(count >= 2)
                            {
                                outputReady = true;
                            }
                        }
                    }

                }
            }
        }


        #endregion


        #region ■クラス・構造体


        // 設定クラス
        public class Settings : SettingsBase
        {
            //保存される情報（設定画面からも参照される）
            public bool TeamEnabled = true;
            public bool ClubEnabled = true;
            public bool GeneralEnabled = false;
            public bool DMEnabled = true;
            public bool GMEnabled = true;
            public bool SystemEnabled = false;
            public bool AdminEnabled = false;
            public bool OwnChatEnabled = true;
            public bool ReadSpeakerName = true;
            public string RootDir = @"C:\Nexon\TalesWeaver";
            public string[] Includes = new string[] { };
            public string[] Excludes = new string[] { };
            public List<Task> TaskWords = new List<Task>();
            public string EndPointTask = @"";

            // 争奪戦
            public bool SdtEnabled = false;
            public string SdtDiscordWebhook = @"";

            //作成元プラグイン
            internal Plugin_TW Plugin;

            //コンストラクタ
            public Settings()
            {
            }

            //コンストラクタ
            public Settings(Plugin_TW plubinTW)
            {
                Plugin = plubinTW;
            }

        }

        // 設定画面表示用クラス
        public class SettingFormData_TW : ISettingFormData
        {
            Settings _Setting;

            public string Title { get { return _Setting.Plugin.Name; } }
            public bool ExpandAll { get { return false; } }
            public SettingsBase Setting { get { return _Setting; } }

            public SettingFormData_TW(Settings setting)
            {
                _Setting = setting;
                PBase = new SBase(_Setting);
            }

            //設定画面で表示されるクラス(ISettingPropertyGrid)
            public SBase PBase;
            public class SBase : ISettingPropertyGrid
            {
                Settings _Setting;
                public SBase(Settings setting) { _Setting = setting; }
                public string GetName() { return "TWチャット読み上げ設定"; }

                [Category("基本設定")]
                [DisplayName("01) クラブチャット読み上げを有効にする")]
                [Description("クラブチャットの内容を読み上げます。")]
                public bool ClubEnabled { get { return _Setting.ClubEnabled; } set { _Setting.ClubEnabled = value; } }


                [Category("基本設定")]
                [DisplayName("02) チームチャット読み上げを有効にする")]
                [Description("チームチャットの内容を読み上げます。")]
                public bool TeamEnabled { get { return _Setting.TeamEnabled; } set { _Setting.TeamEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("04) 耳打ち読み上げを有効にする")]
                [Description("耳打ちの内容を読み上げます。")]
                public bool DMEnabled { get { return _Setting.DMEnabled; } set { _Setting.DMEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("03) 一般チャット読み上げを有効にする")]
                [Description("一般チャットの内容を読み上げます。")]
                public bool GeneralEnabled { get { return _Setting.GeneralEnabled; } set { _Setting.GeneralEnabled = value; } }


                [Category("基本設定")]
                [DisplayName("06) システムメッセージの読み上げを有効にする")]
                [Description("システムメッセージの内容を読み上げます。")]
                public bool SystemEnabled { get { return _Setting.SystemEnabled; } set { _Setting.SystemEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("07) 管理用メッセージの読み上げを有効にする")]
                [Description("管理用メッセージの内容を読み上げます。")]
                public bool AdminEnabled { get { return _Setting.AdminEnabled; } set { _Setting.AdminEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("08) 自分の発言の読み上げを有効にする")]
                [Description("自分の発言内容を読み上げます。")]
                public bool OwnChatEnabled { get { return _Setting.OwnChatEnabled; } set { _Setting.OwnChatEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("09) 発言者の名前を読み上げる。")]
                [Description("発言者の名前を読み上げます。")]
                public bool ReadSpeakerName { get { return _Setting.ReadSpeakerName; } set { _Setting.ReadSpeakerName = value; } }

                [Category("基本設定")]
                [DisplayName("05) GMのチャット読み上げを有効にする")]
                [Description("GMの発言内容を読み上げます。")]
                public bool GMEnabled { get { return _Setting.GMEnabled; } set { _Setting.GMEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("00) TWフォルダを選択")]
                [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
                public string RootDir { get { return _Setting.RootDir; } set { _Setting.RootDir = value; } }


                [Category("ワード設定")]
                [DisplayName("特別ワードの設定")]
                [Description("必ず読み上げるワードを設定します。")]
                public string[] Includes { get { return _Setting.Includes; } set { _Setting.Includes = value; } }

                [Category("ワード設定")]
                [DisplayName("除外ワードの設定")]
                [Description("除外するワードを設定します。")]
                public string[] Excludes { get { return _Setting.Excludes; } set { _Setting.Excludes = value; } }

                [Category("WebHook設定")]
                [DisplayName("日課管理シート")]
                [Description("日課管理用シートのエンドポイントを設定します。")]
                public string EndPointTask { get { return _Setting.EndPointTask; } set { _Setting.EndPointTask = value; } }


                [Category("課題設定")]
                [DisplayName("課題文字列の設定")]
                [Description("課題の文字列と列番号を指定")]
                public string[] TaskWords { get {
                        var list = new List<string>();
                        foreach (var task in _Setting.TaskWords)
                        {
                            list.Add(task.MatchStr + '/' + task.Sheet + '/' + task.Col.ToString());
                        }
                        return list.ToArray();
                    } set {
                        _Setting.TaskWords = ExtractTask(value);

                    } 
                }


                [Category("争奪戦設定")]
                [DisplayName("01) 争奪戦の読み上げを有効にする")]
                [Description("争奪戦の読み上げを有効にする")]
                public bool SdtEnabled { get { return _Setting.SdtEnabled; } set { _Setting.SdtEnabled = value; } }

                [Category("争奪戦設定")]
                [DisplayName("02) Discord Webhook")]
                [Description("争奪戦情報を送信するDiscordのWebhook URL")]
                public string SdtDiscordWebhook { get { return _Setting.SdtDiscordWebhook; } set { _Setting.SdtDiscordWebhook = value; } }


                private List<Task> ExtractTask(string[] tasks)
                {
                    var ret = new List<Task>();
                    foreach (var str in tasks)
                    {
                        string[] arr = str.Split('/');
                        var task = new Task();
                        task.MatchStr = arr[0];
                        task.Col = int.Parse(arr[2]);
                        task.Sheet = arr[1];
                        ret.Add(task);
                    }
                    return ret;
                }

            }
        }

        [System.Xml.Serialization.XmlRoot("message")]
        public class Message
        {
            [System.Xml.Serialization.XmlElement("font")]
            public List<Content> Contents { get; set; }
        }

        public class Content
        {
            [System.Xml.Serialization.XmlAttribute("size")]
            public string Size { get; set; }

            [System.Xml.Serialization.XmlAttribute("color")]
            public string Color { get; set; }

            [System.Xml.Serialization.XmlText()]
            public string Text { get; set; }
        }

        public class CharInfo
        {
            public string ServerName { get; set; }

            public string CharName { get; set; }
        }

        public class Task
        {
            public string MatchStr { get; set; }

            public int Col { get; set; }

            public string Sheet { get; set; }

            public string CharName { get; set; }

            public string Time { get; set; }
        }

        public enum MapType
        {
            [StringValue("ボス")]
            Boss,
            [StringValue("スロット")]
            Slot,
            [StringValue("レバー")]
            Lever,
            [StringValue("脳みそ")]
            Brain,
            [StringValue("迷路")]
            Maze,
            [StringValue("墓碑")]
            Tombstone,
            [StringValue("4か所出口")]
            FourExit,
            [StringValue("脱出装置")]
            Exit,
            [StringValue("トゲ")]
            Toge,
            [StringValue("3方向炎")]
            TreeFire,
            [StringValue("3方向矢")]
            TreeBow,
            [StringValue("3ボス")]
            ThreeBoss,
            [StringValue("幽霊")]
            Ghost,
            [StringValue("ベレシス")]
            Belesis
        }

#endregion
    }
}
