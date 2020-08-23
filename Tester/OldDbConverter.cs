using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using RepoImageMan;
using System.Text.Json;
using System.Text.Unicode;
using Avalonia;
using Avalonia.Media;

namespace Tester
{
    public static class OldDbConverter
    {
        private static async Task Fix(CImage img, OCommodity[] coms)
        {
            //Call in-order to set position correctly
            foreach (var c in coms)
            {
                var ic = await img.AddCommodity();
                ic.Location = new Point(Math.Min(c.IDX, img.Size.Width), Math.Min(c.IDY, img.Size.Height));
                ic.Font = new Font("Arial", (int)(c.IDFontSize - c.IDFontSize * 0.07f), RepoImageMan.FontStyle.Regular);
                ic.LabelColor = Color.FromUInt32((uint)c.IDForeColorArgb);
                ic.Cost = c.Cost ?? 0m;
                ic.WholePrice = c.WholePrice;
                ic.PartialPrice = c.PartialPrice;
                ic.Name = c.Name;
                ic.Location = new Point(ic.Location.X + ic.Font.Size / 30, ic.Location.Y - ic.Font.Size / 30);
                await ic.Save();
            }
        }

        private static async Task Fix(CImage nimg, OCImage oimg, OCommodity[] coms, string imgFolder)
        {
            nimg.Brightness = 1.0f;
            nimg.Contrast = oimg.Contrast;

            await using var os = new FileStream(Path.Combine(imgFolder, oimg.Path.Split('\\').Last()),
                FileMode.Open,
                FileAccess.Read);

            await nimg.ReplaceFile(os);
            await Fix(nimg, coms);
        }

        public static async Task Convert(string comsJson, string imgsJson, string imgsFolder, string newPkgPath, int imgCount)
        {
            var ops = new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
            await CommodityPackage.Create(newPkgPath);
            using var res = await CommodityPackage.TryOpen(newPkgPath);
            var imgs = (await JsonSerializer.DeserializeAsync<OCImage[]>(File.OpenRead(imgsJson), ops))
                .OrderBy(a => a.Position)
                .Take(imgCount)
                .ToArray();
            var coms = (await JsonSerializer.DeserializeAsync<OCommodity[]>(File.OpenRead(comsJson), ops))
                .OrderBy(a => a.Position)
                .ToLookup(a => a.ImageID);
            foreach (var img in imgs)
            {
                await Fix(await res.AddImage(), img, coms[img.ID].ToArray(), imgsFolder);
            }
        }
    }
}