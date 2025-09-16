﻿using System;
using System.Windows.Forms;

namespace Pulsar.Server.Controls
{
    public class NoButtonTabControl : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            // Message 0x1328 is related to tab header drawing, we suppress it here.
            if (m.Msg == 0x1328 && !DesignMode)
            {
                // Suppress the header (tab) drawing
                m.Result = (IntPtr)1;
            }
            else
            {
                // Process other messages as usual
                base.WndProc(ref m);
            }
        }
    }
}