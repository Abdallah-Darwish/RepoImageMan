using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SixLabors.Primitives;
using RepoImageMan;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;
using System.Text.Unicode;

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
                ic.Location = new PointF((float) c.IDX, (float) c.IDY);
                ic.Font = SystemFonts.CreateFont("Arial", c.IDFontSize);
                ic.LabelColor = new Argb32((uint) c.IDForeColorArgb);
                ic.Cost = c.Cost ?? 0m;
                ic.WholePrice = c.WholePrice;
                ic.PartialPrice = c.PartialPrice;
                ic.Name = c.Name;
                await ic.Save();
            }
        }

        private static async Task Fix(CImage nimg, OCImage oimg, OCommodity[] coms, string imgFolder)
        {
            nimg.Brightness = 1.0f;
            nimg.Contrast = oimg.Contrast;
            using (var ns = nimg.OpenStream())
            {
                await using var os = new FileStream(Path.Combine(imgFolder, oimg.Path.Split('\\').Last()),
                    FileMode.Open,
                    FileAccess.Read);
                await os.CopyToAsync(ns);
            }

            nimg.Refresh();
            await Fix(nimg, coms);
        }

        public static async Task Convert(string comsJson, string imgsJson, string imgsFolder,
            string newDbPath,
            string newPkgPath, int imgCount)
        {
            var ops = new JsonSerializerOptions {Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)};
            await CommodityPackage.Create(newDbPath, newPkgPath);
            using var res = await CommodityPackage.Open(newDbPath, newPkgPath);
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