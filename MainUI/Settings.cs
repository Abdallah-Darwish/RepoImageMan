using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using RepoImageMan.Controls;
using SixLabors.ImageSharp;

namespace MainUI
{
    class Settings
    {
        public static readonly string SettingsFileName = "settings.json";
        public static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, SettingsFileName);
        /// <summary>
        /// If <see cref="SizeF.Width"/> or <see cref="SizeF.Height"/> is 0 then the whole scale would be ignored.
        /// </summary>
        public SizeF DesigningWindowResizingScale { get; set; } = new SizeF { Width = 1, Height = 1 };
        /// <summary>
        /// If <see cref="true"/> then the <see cref="DesigningWindow"/> would change <see cref="DesignCImage.InstanceSize"/> every time the PictureBox size changes,
        /// else the image size would be static and the value would be casted to <see cref="Size"/>.
        /// </summary>
        public bool IsDesigningWindowResizingScaleDynamic { get; set; } = true;

        public static async ValueTask<Settings> Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new Settings();
            }
            using FileStream settingsFileStream = new(SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (await JsonSerializer.DeserializeAsync<Settings>(settingsFileStream).ConfigureAwait(false))!;
        }
        public async ValueTask Save()
        {
            using var settingsFileStream = new FileStream(SettingsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

            await JsonSerializer.SerializeAsync(settingsFileStream, this).ConfigureAwait(false);
            settingsFileStream.SetLength(settingsFileStream.Position);
        }
    }
}
