﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using Pulsar.Server.Networking;

namespace Pulsar.Server.Forms
{
    public partial class FrmNickname : Form
    {
        private Label lblNickname;
        private TextBox txtNickname;
        private Button btnOk;
        private Button btnCancel;
        private readonly Client _client;
        private static readonly string PulsarStuffDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PulsarStuff");

        public event EventHandler NicknameSaved;

        public FrmNickname(Client client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            // Initialize UI Components
            this.lblNickname = new Label
            {
                AutoSize = true,
                Location = new Point(12, 15),
                Name = "lblNickname",
                Size = new Size(58, 13),
                TabIndex = 0,
                Text = "Nickname:"
            };

            this.txtNickname = new TextBox
            {
                Location = new Point(76, 12),
                Name = "txtNickname",
                Size = new Size(196, 20),
                TabIndex = 1
            };

            this.btnOk = new Button
            {
                Location = new Point(116, 38),
                Name = "btnOk",
                Size = new Size(75, 23),
                TabIndex = 2,
                Text = "OK",
                UseVisualStyleBackColor = true
            };
            this.btnOk.Click += BtnOk_Click;

            this.btnCancel = new Button
            {
                Location = new Point(197, 38),
                Name = "btnCancel",
                Size = new Size(75, 23),
                TabIndex = 3,
                Text = "Cancel",
                UseVisualStyleBackColor = true
            };
            this.btnCancel.Click += BtnCancel_Click;

            // Form Settings
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(284, 71);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.txtNickname);
            this.Controls.Add(this.lblNickname);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmNickname";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Nickname";
            this.Load += FrmNickname_Load;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNickname.Text))
            {
                ShowErrorMessage("Please enter a valid nickname.");
                return;
            }

            if (_client?.Value == null)
            {
                ShowErrorMessage("Client information is not available.");
                return;
            }

            try
            {
                string filePath = Path.Combine(PulsarStuffDir, "client_info.json");
                
                if (!Directory.Exists(PulsarStuffDir))
                {
                    Directory.CreateDirectory(PulsarStuffDir);
                }

                SaveOrUpdateClientInfo(filePath, txtNickname.Text);

                OnNicknameSaved(EventArgs.Empty); // Trigger event  

                ShowSuccessMessage("Nickname saved successfully!");
                this.Close(); // Close the form after saving the nickname  
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"An error occurred while saving the nickname: {ex.Message}");
            }
        }

        protected virtual void OnNicknameSaved(EventArgs e)
        {
            NicknameSaved?.Invoke(this, e);
        }

        public Client GetClient()
        {
            return _client;
        }

        private void SaveOrUpdateClientInfo(string filePath, string nickname)
        {
            Dictionary<string, ClientInfo> clientInfos = new Dictionary<string, ClientInfo>();

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        clientInfos = JsonConvert.DeserializeObject<Dictionary<string, ClientInfo>>(json) ?? new Dictionary<string, ClientInfo>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading existing client info: {ex.Message}");
                }
            }

            string clientId = _client.Value.Id;
            clientInfos[clientId] = new ClientInfo
            {
                ClientId = clientId,
                Nickname = nickname
            };

            try
            {
                string updatedJson = JsonConvert.SerializeObject(clientInfos, Formatting.Indented);
                File.WriteAllText(filePath, updatedJson);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to save or update client info: {ex.Message}");
                throw;
            }
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowSuccessMessage(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnCancel_Click(object sender, EventArgs e) => this.Close();
        private void FrmNickname_Load(object sender, EventArgs e) { }
    }
}
