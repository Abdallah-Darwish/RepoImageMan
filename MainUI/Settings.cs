﻿using RepoImageMan;
using SixLabors.Primitives;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MainUI
{
    class Settings
    {
        public static readonly string SettingsFileName = "settings.json";
        public static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, SettingsFileName);
        /// <summary>
        /// If <see cref="SizeF.Width"/> or <see cref="SizeF.Height"/> is 0 then the whole scale would be ignored.
        /// </summary>
        public SizeF DesigningWindowResizingScale { get; set; } = new SizeF { Width = 500, Height = 500 };
        /// <summary>
        /// If <see cref="true"/> then the <see cref="DesigningWindow"/> would change <see cref="DesignCImage.InstanceSize"/> every time the PictureBox size changes,
        /// else the image size would be static and the value would be casted to <see cref="Size"/>.
        /// </summary>
        public bool IsDesigningWindowResizingScaleDynamic { get; set; } = false;

        public static async ValueTask<Settings> Load()
        {
            
            if (File.Exists(SettingsFilePath) == false)
            {
                return new Settings();
            }
            using var settingsFileStream = new FileStream(SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<Settings>(settingsFileStream).ConfigureAwait(false);
        }
        public async ValueTask Save()
        {
            using var settingsFileStream = new FileStream(SettingsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

            await JsonSerializer.SerializeAsync(settingsFileStream, this).ConfigureAwait(false);
            settingsFileStream.SetLength(settingsFileStream.Position);
        }
    }
}
