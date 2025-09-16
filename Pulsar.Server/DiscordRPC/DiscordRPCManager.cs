﻿using Pulsar.Server.Models;
using System.Windows.Forms;

namespace Pulsar.Server.DiscordRPC
{
    internal class DiscordRPCManager
    {
        private static DiscordRPC _rpcInstance;

        public static void Initialize(Form form)
        {
            if (_rpcInstance == null)
            {
                _rpcInstance = new DiscordRPC(form);
            }
            ApplyDiscordRPC(form);
        }

        public static void ApplyDiscordRPC(Form form)
        {
            bool isDiscordRPCChecked = Settings.DiscordRPC;
            if (_rpcInstance == null || _rpcInstance.Enabled != isDiscordRPCChecked)
            {
                if (_rpcInstance != null)
                {
                    _rpcInstance.Enabled = false; // Explicitly disable the old instance
                    _rpcInstance = null;          // Clear reference for garbage collection
                }
                _rpcInstance = new DiscordRPC(form);
                _rpcInstance.Enabled = isDiscordRPCChecked;
            }
            else
            {
                _rpcInstance.Enabled = isDiscordRPCChecked; // Enforce the state
            }
        }

        public static void Shutdown()
        {
            if (_rpcInstance != null)
            {
                _rpcInstance.Enabled = false;
                _rpcInstance = null;
            }
        }
    }
}