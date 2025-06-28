﻿using Pulsar.Common.Helpers;
using Pulsar.Server.Helper;
using Pulsar.Server.Utilities;
using System;
using System.Drawing;
using System.Windows.Forms;
namespace Pulsar.Server.Controls
{
    internal class AeroListView : ListView
    {
        private const uint WM_CHANGEUISTATE = 0x127;
        private const short UIS_SET = 1;
        private const short UISF_HIDEFOCUS = 0x1;
        private readonly IntPtr _removeDots = new IntPtr(NativeMethodsHelper.MakeWin32Long(UIS_SET, UISF_HIDEFOCUS));

        private const int WM_VSCROLL = 0x115;
        private const int SB_BOTTOM = 7;
        private const int SB_TOP = 6;
        private const int WS_VSCROLL = 0x00200000;
        private const int WS_HSCROLL = 0x00100000;

        public ListViewColumnSorter LvwColumnSorter { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AeroListView"/> class.
        /// </summary>
        public AeroListView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.LvwColumnSorter = new ListViewColumnSorter();
            this.ListViewItemSorter = LvwColumnSorter;
            this.View = View.Details;
            this.FullRowSelect = true;

            Resize += AeroListView_Resize;
        }

        /// <summary>
        /// Gets the creation parameters.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WS_VSCROLL | WS_HSCROLL;  // Always show both scrollbars
                return cp;
            }
        }


        private void AeroListView_Resize(object sender, EventArgs e)
        {
            if (Columns.Count == 0) return;

            int totalWidth = 0;

            for (int i = 0; i < Columns.Count - 1; i++)
            {
                totalWidth += Columns[i].Width;
            }

            int newWidth = ClientSize.Width - totalWidth;
            if (newWidth > 0)
            {
                Columns[Columns.Count - 1].Width = newWidth;
            }
        }

        /// <summary>
        /// Refreshes the scrollbars to ensure they are properly displayed.
        /// </summary>
        public void RefreshScrollBars()
        {
            // Yes I chatGPT this I have no idea wtf is going on.
            // Force scrollbars to update by sending scroll messages
            if (IsHandleCreated && !PlatformHelper.RunningOnMono)
            {
                // Scroll to bottom and then back to top to refresh vertical scrollbar
                NativeMethods.SendMessage(this.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
                NativeMethods.SendMessage(this.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);

                // Call UpdateScrollBars to ensure proper sizing
                this.BeginUpdate();
                this.EndUpdate();
            }
        }

        /// <summary>
        /// Raises the <see cref="E:HandleCreated" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (PlatformHelper.RunningOnMono) return;
            if (PlatformHelper.VistaOrHigher)
            {
                // set window theme to explorer
                NativeMethods.SetWindowTheme(this.Handle, "explorer", null);
            }
            if (PlatformHelper.XpOrHigher)
            {
                // removes the ugly dotted line around focused item
                NativeMethods.SendMessage(this.Handle, WM_CHANGEUISTATE, _removeDots, IntPtr.Zero);
            }

            // Add this to refresh scrollbars after creation
            this.BeginInvoke(new Action(() => RefreshScrollBars()));
        }

        /// <summary>
        /// Raises the <see cref="E:Resize" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RefreshScrollBars();
        }

        /// <summary>
        /// Raises the <see cref="E:ColumnClick" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ColumnClickEventArgs"/> instance containing the event data.</param>
        protected override void OnColumnClick(ColumnClickEventArgs e)
        {
            base.OnColumnClick(e);
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == this.LvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                this.LvwColumnSorter.Order = (this.LvwColumnSorter.Order == SortOrder.Ascending)
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                this.LvwColumnSorter.SortColumn = e.Column;
                this.LvwColumnSorter.Order = SortOrder.Ascending;
            }
            // Perform the sort with these new sort options.
            if (!this.VirtualMode)
                this.Sort();
        }
    }
}