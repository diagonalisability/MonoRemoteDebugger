﻿namespace MonoRemoteDebugger.VSExtension.Settings
{
    public class UserSettings
    {
        public UserSettings()
        {
            LastIp = "127.0.0.1";
            LastTimeout = 10000;
            ShouldUploadBinariesToDebuggingServer = true;
        }

        public string LastIp { get; set; }
        public int LastTimeout { get; set; }
        public bool ShouldUploadBinariesToDebuggingServer { get; set; }
    }
}