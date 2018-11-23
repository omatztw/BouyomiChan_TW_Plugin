using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Serialization;
using System.ComponentModel;
using FNF.Utility;
using FNF.XmlSerializerSetting;
using FNF.BouyomiChanApp;

namespace Plugin_TW
{
    public class Plugin_TW : IPlugin {
        #region ■フィールド

        private Settings         _Settings;                                                       //設定
        private SettingFormData_TW  _SettingFormData;
        private string                 _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting"; //設定ファイルの保存場所
        private System.Threading.Timer _Timer;          
        private long _CurrentPosition = 0;

        private const string CLUB_COLOR = @"#94ddfa";
        private const string TEAM_COLOR = @"#f7b73c";


        #endregion


        #region ■IPluginメンバの実装

        public string           Name            { get { return "TWチャット読み上げ"; } }

        public string           Version         { get { return "2018.11"; } }

        public string           Caption         { get { return "TWチャット読み上げ"; } } 

        public ISettingFormData SettingFormData { get { return _SettingFormData; } } //プラグインの設定画面情報

        //プラグイン開始時処理
        public void Begin() {
            //設定ファイル読み込み
            _Settings = new Settings(this);
            _Settings.Load(_SettingFile);
            _SettingFormData = new SettingFormData_TW(_Settings);
            // 最初は全部読みポジションを最後に設定しておく
            _CurrentPosition = GetFileLength(GetChatLogFileName());

            //タイマ登録
            _Timer = new System.Threading.Timer(Timer_Event, null, 0, 1000);

        }

        //プラグイン終了時処理
        public void End() {
            //設定ファイル保存
            _Settings.Save(_SettingFile);

            //タイマ開放
            if (_Timer != null) {
                _Timer.Dispose();
                _Timer = null;
            }

        }

        #endregion


        #region ■メソッド・イベント処理

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
            if(!File.Exists(filename))
            {
                return 0;
            }
            FileInfo file = new FileInfo(filename);
            return file.Length;

        }

        private string GetChatLogFileName()
        {
            DateTime now = DateTime.Now;
            string today = string.Format(@"{0}_{1}_{2}", now.Year, now.Month, now.Day);
            string filename = string.Format(@"{0}\TWChatLog_{1}.html", _Settings.ChatLogDir, today);
            return filename;
        }

        //タイマイベント
        private void Timer_Event(object obj) {
            string filename = GetChatLogFileName();

            // 最後に読み込んだ箇所から新規追加された文章を読み込む
            List<string> lines = GetLinesFromLastLine(filename);

            foreach (string line in lines)
            {
                try
                {
                    Message message = DeserializeToMessage(ConvertLineToXml(line));
                    Content[] contents = message.Contents.ToArray();
                    Content content = contents[1];

                    if (IsDisplayed(content))
                    {
                        Pub.AddTalkTask(content.Text, -1, -1, VoiceType.Default);
                    }
                } catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }
        }

        private Boolean IsDisplayed(Content content)
        {
            if(Includes(content))
            {
                return true;
            }

            if(Excludes(content))
            {
                return false;
            }

            if(_Settings.ClubEnabled && IsClub(content))
            {
                return true;
            }

            if (_Settings.TeamEnabled && IsTeam(content))
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

        private Boolean Includes(Content content)
        {
            foreach (var include in _Settings.Includes)
            {
                if(Regex.IsMatch(content.Text, include))
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
                if(Regex.IsMatch(content.Text, exclude))
                {
                    return true;
                }
            }
            return false;
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
                Console.WriteLine(e.Message);
            }
            return lines;

        }


        #endregion


        #region ■クラス・構造体


        // 設定クラス
        public class Settings : SettingsBase
        {
            //保存される情報（設定画面からも参照される）
            public bool TeamEnabled = true;
            public bool ClubEnabled = true;
            public string ChatLogDir = @"C:\Nexon\TalesWeaver\ChatLog";
            public string[] Includes = new string[] { };
            public string[] Excludes = new string[] { };

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
        public class SettingFormData_TW : ISettingFormData {
            Settings _Setting;

            public string       Title     { get { return _Setting.Plugin.Name; } }
            public bool         ExpandAll { get { return false; } }
            public SettingsBase Setting   { get { return _Setting; } }

            public SettingFormData_TW(Settings setting) {
                _Setting = setting;
                PBase    = new SBase(_Setting);
            }

            //設定画面で表示されるクラス(ISettingPropertyGrid)
            public SBase PBase;
            public class SBase : ISettingPropertyGrid {
                Settings _Setting;
                public SBase(Settings setting) { _Setting = setting; }
                public string GetName() { return "TWチャット読み上げ設定"; }

                [Category("基本設定")]
                [DisplayName("クラブチャット読み上げを有効にする")]
                [Description("クラブチャットの内容を読み上げます。")]
                public bool ClubEnabled { get { return _Setting.ClubEnabled; } set { _Setting.ClubEnabled = value; } }


                [Category   ("基本設定")]
                [DisplayName("チームチャット読み上げを有効にする")]
                [Description("チームチャットの内容を読み上げます。")]
                public bool TeamEnabled { get { return _Setting.TeamEnabled; } set { _Setting.TeamEnabled = value; } }

                [Category("基本設定")]
                [DisplayName("チャットログのあるフォルダを選択")]
                [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
                public string ChatLogDir { get { return _Setting.ChatLogDir; } set { _Setting.ChatLogDir = value; } }

                [Category("ワード設定")]
                [DisplayName("特別ワードの設定")]
                [Description("必ず読み上げるワードを設定します。")]
                public string[] Includes { get { return _Setting.Includes; } set { _Setting.Includes = value; } }

                [Category("ワード設定")]
                [DisplayName("除外ワードの設定")]
                [Description("除外するワードを設定します。")]
                public string[] Excludes { get { return _Setting.Excludes; } set { _Setting.Excludes = value; } }

            }
        }

        public class Word
        {
            public string value { get; set; }

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

        #endregion
    }
}
