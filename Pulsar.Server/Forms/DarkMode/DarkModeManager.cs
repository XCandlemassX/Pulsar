﻿using DarkModeForms;
using Pulsar.Server.Models;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pulsar.Server.Forms.DarkMode
{
    public class DarkModeManager
    {
        private static readonly DarkModeCS.DisplayMode lightMode = DarkModeCS.DisplayMode.ClearMode;
        private static readonly DarkModeCS.DisplayMode darkMode = DarkModeCS.DisplayMode.DarkMode;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_BORDER_COLOR = 34; // DWM attribute for border color

        public static void ApplyDarkMode(Form form)
        {
            bool isDarkModeChecked = Settings.DarkMode;

            DarkModeCS _ = new DarkModeCS(form)
            {
                ColorMode = isDarkModeChecked ? darkMode : lightMode,
                ColorizeIcons = false,
            };

            Color borderColor = isDarkModeChecked ? Color.DimGray : Color.DimGray;
            SetBorderColor(form, borderColor);
        }

        private static void SetBorderColor(Form form, Color color)
        {
            int colorValue = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(form.Handle, DWMWA_BORDER_COLOR, ref colorValue, sizeof(int));
        }
    }
}
