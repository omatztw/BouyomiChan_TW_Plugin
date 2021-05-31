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
        #region ���t�B�[���h

        private Settings _Settings;                                                       //�ݒ�
        private SettingFormData_TW _SettingFormData;
        private string _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting"; //�ݒ�t�@�C���̕ۑ��ꏊ
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
             { "�}�b�v�̉��ɂ���{�X�����X�^�[��ގ����Ă��������B", MapType.Boss }
            ,{ "�ɂȂ�悤�ɐ����J�[�h�������Ē����̑��u�ɓ������ĉ������B", MapType.Slot}
            ,{ "��l�������̃��o�[�������Ă��J�E���g����܂�", MapType.Lever}
            ,{ "�}�b�v�����̃{�^���̏�ɏ悹�Ă��������B", MapType.Brain}
            ,{ "������ɔ�������n�_������Ē������ɂ���ړI�n�ɓ��B���Ă�������", MapType.Maze}
            ,{ "�}�b�v�����̔�΂̏�ɏ���Ă�������", MapType.Tombstone}
            ,{ "�����ɕt�^���ꂽ�����ƈ�v����o���������Ă��������B", MapType.FourExit}
            ,{ "�V�����E�o���u����������܂����B�E�o���u�͈�莞�Ԃ��o�Ə����܂��B", MapType.Exit}
            ,{ "�ψق����g�Q���[�i�T��", MapType.Toge}
            ,{ "�~�蒍���������ă����o�[�S�����e�����̒����ɏ���Ă��������B", MapType.TreeBow}
            ,{ "�~�蒍����������ă����o�[�S�����e�����̒����ɏ���Ă��������B", MapType.TreeFire}
            ,{ "�U��΂��Ă���3�C�̃{�X�������đS�đގ����Ă��������B", MapType.ThreeBoss}
            ,{ "�����Ȃ��H�������č����ɂ���ړI�n�ɓ��B���Ă��������B", MapType.Ghost}
            ,{ "[�Ñ�x���V�X]�̍U������", MapType.Belesis}
        };


        #endregion


        #region ��IPlugin�����o�̎���

        public string Name { get { return "TW�`���b�g�ǂݏグ"; } }

        public string Version { 
            get {
                String ver = "2021.03.20";
#if DEBUG
                ver += "-debug";

#endif
                return ver; 
            } 
        }

        public string Caption { get { return "TW�`���b�g�ǂݏグ"; } }

        public ISettingFormData SettingFormData { get { return _SettingFormData; } } //�v���O�C���̐ݒ��ʏ��

        //�v���O�C���J�n������
        public void Begin()
        {
            //�ݒ�t�@�C���ǂݍ���
            _Settings = new Settings(this);
            _Settings.Load(_SettingFile);
            _SettingFormData = new SettingFormData_TW(_Settings);
            // �ŏ��͑S���ǂ݃|�W�V�������Ō�ɐݒ肵�Ă���
            _CurrentPosition = GetFileLength(GetChatLogFileName());

            // �_�ǂ݂����œǂݏグ��ꂽ���ɔ��s�����C�x���g�n���h��
            // Pub.FormMain.BC.TalkTaskStarted += new EventHandler<BouyomiChan.TalkTaskStartedEventArgs>(BC_TalkTaskStarted);

            //�^�C�}�o�^
            _Timer = new System.Threading.Timer(Timer_Event, null, 0, 1000);

        }

        //�v���O�C���I��������
        public void End()
        {
            //�ݒ�t�@�C���ۑ�
            _Settings.Save(_SettingFile);

            //�^�C�}�J��
            if (_Timer != null)
            {
                _Timer.Dispose();
                _Timer = null;
            }

        }

        #endregion


        #region �����\�b�h�E�C�x���g����

        // �_�ǂ݂����œǂݏグ��ꂽ���ɔ��s�����C�x���g����
        //private void BC_TalkTaskStarted(object sender, BouyomiChan.TalkTaskStartedEventArgs eve)
        //{
        //    LogError(eve.TalkTask.SourceText.ToString());
            

        //}

        /// <summary>
        /// �f�o�b�O���O��ǉ��BDebug�Ńr���h�����ꍇ�ɕ\������郍�O�B
        /// </summary>
        /// <param name="message"></param>
        private void LogError(string message)
        {
            Log.AddDebug(Base.CallAsmName, message, LogType.Error);
        }

        /// <summary>
        /// �`���b�g���O��XML�`���ɂȂ��Ă��Ȃ��̂ŁAXML�`���ɕϊ�����
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private string ConvertLineToXml(string line)
        {
            // </br>��&nbsp�͏�������
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

        //�^�C�}�C�x���g
        private void Timer_Event(object obj)
        {
            string filename = GetChatLogFileName();

            // �Ō�ɓǂݍ��񂾉ӏ�����V�K�ǉ����ꂽ���͂�ǂݍ���
            List<string> lines = GetLinesFromLastLine(filename);

            foreach (string line in lines)
            {
                try
                {
                    Message message = DeserializeToMessage(ConvertLineToXml(line));
                    Content[] contents = message.Contents.ToArray();
                    Content date = contents[0];
                    Content content = contents[1];
                    String d = date.Text.Replace("[", "").Replace("]", "").Replace(" ", "").Replace("��", ":").Replace("��", ":").Replace("�b", "").Trim();
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
            string joinedString = "�����D�}�b�v����\n```\n";
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
        /// �Ō�ɓǂݍ��񂾉ӏ�����V�K�ǉ����ꂽ���͂�ǂݍ���
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
            if(IsSystem(content) && content.Text.Trim().Contains("�A�o���h�����[�h���D�킪�J�n���܂����B"))
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


        #region ���N���X�E�\����


        // �ݒ�N���X
        public class Settings : SettingsBase
        {
            //�ۑ��������i�ݒ��ʂ�����Q�Ƃ����j
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

            // ���D��
            public bool SdtEnabled = false;
            public string SdtDiscordWebhook = @"";

            //�쐬���v���O�C��
            internal Plugin_TW Plugin;

            //�R���X�g���N�^
            public Settings()
            {
            }

            //�R���X�g���N�^
            public Settings(Plugin_TW plubinTW)
            {
                Plugin = plubinTW;
            }

        }

        // �ݒ��ʕ\���p�N���X
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

            //�ݒ��ʂŕ\�������N���X(ISettingPropertyGrid)
            public SBase PBase;
            public class SBase : ISettingPropertyGrid
            {
                Settings _Setting;
                public SBase(Settings setting) { _Setting = setting; }
                public string GetName() { return "TW�`���b�g�ǂݏグ�ݒ�"; }

                [Category("��{�ݒ�")]
                [DisplayName("01) �N���u�`���b�g�ǂݏグ��L���ɂ���")]
                [Description("�N���u�`���b�g�̓��e��ǂݏグ�܂��B")]
                public bool ClubEnabled { get { return _Setting.ClubEnabled; } set { _Setting.ClubEnabled = value; } }


                [Category("��{�ݒ�")]
                [DisplayName("02) �`�[���`���b�g�ǂݏグ��L���ɂ���")]
                [Description("�`�[���`���b�g�̓��e��ǂݏグ�܂��B")]
                public bool TeamEnabled { get { return _Setting.TeamEnabled; } set { _Setting.TeamEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("04) ���ł��ǂݏグ��L���ɂ���")]
                [Description("���ł��̓��e��ǂݏグ�܂��B")]
                public bool DMEnabled { get { return _Setting.DMEnabled; } set { _Setting.DMEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("03) ��ʃ`���b�g�ǂݏグ��L���ɂ���")]
                [Description("��ʃ`���b�g�̓��e��ǂݏグ�܂��B")]
                public bool GeneralEnabled { get { return _Setting.GeneralEnabled; } set { _Setting.GeneralEnabled = value; } }


                [Category("��{�ݒ�")]
                [DisplayName("06) �V�X�e�����b�Z�[�W�̓ǂݏグ��L���ɂ���")]
                [Description("�V�X�e�����b�Z�[�W�̓��e��ǂݏグ�܂��B")]
                public bool SystemEnabled { get { return _Setting.SystemEnabled; } set { _Setting.SystemEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("07) �Ǘ��p���b�Z�[�W�̓ǂݏグ��L���ɂ���")]
                [Description("�Ǘ��p���b�Z�[�W�̓��e��ǂݏグ�܂��B")]
                public bool AdminEnabled { get { return _Setting.AdminEnabled; } set { _Setting.AdminEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("08) �����̔����̓ǂݏグ��L���ɂ���")]
                [Description("�����̔������e��ǂݏグ�܂��B")]
                public bool OwnChatEnabled { get { return _Setting.OwnChatEnabled; } set { _Setting.OwnChatEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("09) �����҂̖��O��ǂݏグ��B")]
                [Description("�����҂̖��O��ǂݏグ�܂��B")]
                public bool ReadSpeakerName { get { return _Setting.ReadSpeakerName; } set { _Setting.ReadSpeakerName = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("05) GM�̃`���b�g�ǂݏグ��L���ɂ���")]
                [Description("GM�̔������e��ǂݏグ�܂��B")]
                public bool GMEnabled { get { return _Setting.GMEnabled; } set { _Setting.GMEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("00) TW�t�H���_��I��")]
                [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
                public string RootDir { get { return _Setting.RootDir; } set { _Setting.RootDir = value; } }


                [Category("���[�h�ݒ�")]
                [DisplayName("���ʃ��[�h�̐ݒ�")]
                [Description("�K���ǂݏグ�郏�[�h��ݒ肵�܂��B")]
                public string[] Includes { get { return _Setting.Includes; } set { _Setting.Includes = value; } }

                [Category("���[�h�ݒ�")]
                [DisplayName("���O���[�h�̐ݒ�")]
                [Description("���O���郏�[�h��ݒ肵�܂��B")]
                public string[] Excludes { get { return _Setting.Excludes; } set { _Setting.Excludes = value; } }

                [Category("WebHook�ݒ�")]
                [DisplayName("���ۊǗ��V�[�g")]
                [Description("���ۊǗ��p�V�[�g�̃G���h�|�C���g��ݒ肵�܂��B")]
                public string EndPointTask { get { return _Setting.EndPointTask; } set { _Setting.EndPointTask = value; } }


                [Category("�ۑ�ݒ�")]
                [DisplayName("�ۑ蕶����̐ݒ�")]
                [Description("�ۑ�̕�����Ɨ�ԍ����w��")]
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


                [Category("���D��ݒ�")]
                [DisplayName("01) ���D��̓ǂݏグ��L���ɂ���")]
                [Description("���D��̓ǂݏグ��L���ɂ���")]
                public bool SdtEnabled { get { return _Setting.SdtEnabled; } set { _Setting.SdtEnabled = value; } }

                [Category("���D��ݒ�")]
                [DisplayName("02) Discord Webhook")]
                [Description("���D����𑗐M����Discord��Webhook URL")]
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
            [StringValue("�{�X")]
            Boss,
            [StringValue("�X���b�g")]
            Slot,
            [StringValue("���o�[")]
            Lever,
            [StringValue("�]�݂�")]
            Brain,
            [StringValue("���H")]
            Maze,
            [StringValue("���")]
            Tombstone,
            [StringValue("4�����o��")]
            FourExit,
            [StringValue("�E�o���u")]
            Exit,
            [StringValue("�g�Q")]
            Toge,
            [StringValue("3������")]
            TreeFire,
            [StringValue("3������")]
            TreeBow,
            [StringValue("3�{�X")]
            ThreeBoss,
            [StringValue("�H��")]
            Ghost,
            [StringValue("�x���V�X")]
            Belesis
        }

#endregion
    }
}
