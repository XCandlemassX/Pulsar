﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pulsar.Client.Recovery.Browsers
{
    public struct AllBrowsers
    {
        public BrowserChromium[] Chromium;
        public BrowserGecko[] Gecko;
    }
    public struct BrowserChromium
    {
        public string Name;
        public string Path;
        public string LocalState;
        public ProfileChromium[] Profiles;
    }

    public struct ProfileChromium
    {
        public string Name;
        public string LoginData;
        public string Path;
    }

    public struct BrowserGecko
    {
        public string Name;
        public string Path;
        public string Key4;
        public string Logins;
        public string ProfilesDir;
    }
}
