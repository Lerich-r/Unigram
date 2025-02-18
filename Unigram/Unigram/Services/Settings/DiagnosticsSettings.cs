﻿using Unigram.Common;

namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _disableDatabase;
        public bool DisableDatabase
        {
            get => _disableDatabase ??= GetValueOrDefault("DisableDatabase", false);
            set => AddOrUpdateValue(ref _disableDatabase, "DisableDatabase", value);
        }

        private bool? _copyFormattedCode;
        public bool CopyFormattedCode
        {
            get => _copyFormattedCode ??= GetValueOrDefault("CopyFormattedCode", true);
            set => AddOrUpdateValue(ref _copyFormattedCode, "CopyFormattedCode", value);
        }

        private bool? _minithumbnails;
        public bool Minithumbnails
        {
            get => _minithumbnails ??= GetValueOrDefault("Minithumbnails", true);
            set => AddOrUpdateValue(ref _minithumbnails, "Minithumbnails", value);
        }

        private bool? _allowRightToLeft;
        public bool AllowRightToLeft
        {
            get => _allowRightToLeft ??= GetValueOrDefault("AllowRightToLeft", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _allowRightToLeft, "AllowRightToLeft", value);
        }

        private string _lastErrorMessage;
        public string LastErrorMessage
        {
            get => _lastErrorMessage ??= GetValueOrDefault("LastErrorMessage", string.Empty);
            set => AddOrUpdateValue(ref _lastErrorMessage, "LastErrorMessage", value);
        }

        private int? _lastErrorVersion;
        public int LastErrorVersion
        {
            get => _lastErrorVersion ??= GetValueOrDefault("LastErrorVersion", 0);
            set => AddOrUpdateValue(ref _lastErrorVersion, "LastErrorVersion", value);
        }

        public bool IsLastErrorDiskFull { get; set; }
    }
}
