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

        //�^�C�}�C�x���g
        private void Timer_Event(object obj) {
            string filename = GetChatLogFileName();

            // �Ō�ɓǂݍ��񂾉ӏ�����V�K�ǉ����ꂽ���͂�ǂݍ���
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
            public string[] Includes = new string[] { };
            public string[] Excludes = new string[] { };

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
                public string GetName() { return "TW�`���b�g�ǂݏグ�ݒ�"; }

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

                [Category("���[�h�ݒ�")]
                [DisplayName("���ʃ��[�h�̐ݒ�")]
                [Description("�K���ǂݏグ�郏�[�h��ݒ肵�܂��B")]
                public string[] Includes { get { return _Setting.Includes; } set { _Setting.Includes = value; } }

                [Category("���[�h�ݒ�")]
                [DisplayName("���O���[�h�̐ݒ�")]
                [Description("���O���郏�[�h��ݒ肵�܂��B")]
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
