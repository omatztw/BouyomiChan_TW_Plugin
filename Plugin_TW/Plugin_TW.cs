using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.ComponentModel;
using FNF.Utility;
using FNF.XmlSerializerSetting;
using FNF.BouyomiChanApp;

namespace Plugin_TW
{
    public class Plugin_TW : IPlugin {
        #region ���t�B�[���h

        private Settings         _Settings;                                                       //�ݒ�
        private SettingFormData_TW  _SettingFormData;
        private string                 _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting"; //�ݒ�t�@�C���̕ۑ��ꏊ
        private System.Threading.Timer _Timer;          
        private long _CurrentPosition = 0;

        private const string CLUB_COLOR = @"#94ddfa";
        private const string TEAM_COLOR = @"#f7b73c";


        #endregion


        #region ��IPlugin�����o�̎���

        public string           Name            { get { return "TW�`���b�g�ǂݏグ"; } }

        public string           Version         { get { return "2018.11"; } }

        public string           Caption         { get { return "TW�`���b�g�ǂݏグ"; } } 

        public ISettingFormData SettingFormData { get { return _SettingFormData; } } //�v���O�C���̐ݒ��ʏ��

        //�v���O�C���J�n������
        public void Begin() {
            //�ݒ�t�@�C���ǂݍ���
            _Settings = new Settings(this);
            _Settings.Load(_SettingFile);
            _SettingFormData = new SettingFormData_TW(_Settings);
            // �ŏ��͑S���ǂ݃|�W�V�������Ō�ɐݒ肵�Ă���
            _CurrentPosition = GetFileLength(GetChatLogFileName());

            //�^�C�}�o�^
            _Timer = new System.Threading.Timer(Timer_Event, null, 0, 1000);

        }

        //�v���O�C���I��������
        public void End() {
            //�ݒ�t�@�C���ۑ�
            _Settings.Save(_SettingFile);

            //�^�C�}�J��
            if (_Timer != null) {
                _Timer.Dispose();
                _Timer = null;
            }

        }

        #endregion


        #region �����\�b�h�E�C�x���g����

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
            string dummy = @"<message><font size=""2"" color=""white""></font> <font size=""2"" color=""#ff6464"">dummy</font></message>";

            XmlSerializer serializer = new XmlSerializer(typeof(Message));
            Message message;
            try
            {
                message = (Message)serializer.Deserialize(new StringReader(xml));

            }
            catch
            {
                message = (Message)serializer.Deserialize(new StringReader(dummy));
            }


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

        //�^�C�}�C�x���g
        private void Timer_Event(object obj) {
            string filename = GetChatLogFileName();
            List<string> lines = new List<string>();
            if (!File.Exists(filename))
            {
                // �t�@�C�������݂��Ȃ��ꍇ�́A�X�L�b�v
                return;
            }

            // �Ō�ɓǂݍ��񂾉ӏ�����V�K�ǉ����ꂽ���͂�ǂݍ���
            lines = GetLinesFromLastLine(filename);
            foreach (string line in lines)
            {
                Message message = DeserializeToMessage(ConvertLineToXml(line));
                Content content = (message.Contents.ToArray())[1];

                if(IsDisplayed(content) && content.Text != null)
                {
                    Pub.AddTalkTask(content.Text, -1, -1, VoiceType.Default);
                }
            }
        }

        private Boolean IsDisplayed(Content font)
        {
            if(_Settings.ClubEnabled && IsClub(font))
            {
                return true;
            }

            if (_Settings.TeamEnabled && IsTeam(font))
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
                            Console.WriteLine(_CurrentPosition);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
            return lines;

        }


        #endregion


        #region ���N���X�E�\����


        // �ݒ�N���X
        public class Settings : SettingsBase
        {
            //�ۑ��������i�ݒ��ʂ�����Q�Ƃ����j
            public bool TeamEnabled = true;
            public bool ClubEnabled = true;
            public string ChatLogDir = @"C:\Nexon\TalesWeaver\ChatLog";

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
        public class SettingFormData_TW : ISettingFormData {
            Settings _Setting;

            public string       Title     { get { return _Setting.Plugin.Name; } }
            public bool         ExpandAll { get { return false; } }
            public SettingsBase Setting   { get { return _Setting; } }

            public SettingFormData_TW(Settings setting) {
                _Setting = setting;
                PBase    = new SBase(_Setting);
            }

            //�ݒ��ʂŕ\�������N���X(ISettingPropertyGrid)
            public SBase PBase;
            public class SBase : ISettingPropertyGrid {
                Settings _Setting;
                public SBase(Settings setting) { _Setting = setting; }
                public string GetName() { return "TW�ǂݏグ�ݒ�"; }

                [Category("��{�ݒ�")]
                [DisplayName("�N���u�`���b�g�ǂݏグ��L���ɂ���")]
                [Description("�N���u�`���b�g�̓��e��ǂݏグ�܂��B")]
                public bool ClubEnabled { get { return _Setting.ClubEnabled; } set { _Setting.ClubEnabled = value; } }


                [Category   ("��{�ݒ�")]
                [DisplayName("�`�[���`���b�g�ǂݏグ��L���ɂ���")]
                [Description("�`�[���`���b�g�̓��e��ǂݏグ�܂��B")]
                public bool TeamEnabled { get { return _Setting.TeamEnabled; } set { _Setting.TeamEnabled = value; } }

                [Category("��{�ݒ�")]
                [DisplayName("�`���b�g���O�̂���t�H���_��I��")]
                [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
                public string ChatLogDir { get { return _Setting.ChatLogDir; } set { _Setting.ChatLogDir = value; } }

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

        #endregion
    }
}
