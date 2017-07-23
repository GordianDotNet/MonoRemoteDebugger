﻿using System;
using Microsoft.VisualStudio.Settings;
using MonoRemoteDebugger.SharedLib.Settings;
using NLog;

namespace MonoRemoteDebugger.VSExtension.Settings
{
    public class UserSettingsManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly UserSettingsManager manager = new UserSettingsManager();
        private WritableSettingsStore store;

        private UserSettingsManager()
        {
        }

        public static UserSettingsManager Instance
        {
            get { return manager; }
        }

        public UserSettings Load()
        {
            var result = new UserSettings();

            if (store.CollectionExists("MonoRemoteDebugger"))
            {
                try
                {
                    string content = store.GetString("MonoRemoteDebugger", "Settings");
                    result = UserSettings.DeserializeFromJson(content);
                    return result;
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }

            return result;
        }

        public void Save(UserSettings settings)
        {
            string json = settings.SerializeToJson();
            if (!store.CollectionExists("MonoRemoteDebugger"))
                store.CreateCollection("MonoRemoteDebugger");
            store.SetString("MonoRemoteDebugger", "Settings", json);
        }

        public static void Initialize(WritableSettingsStore configurationSettingsStore)
        {
            Instance.store = configurationSettingsStore;
        }
    }
}