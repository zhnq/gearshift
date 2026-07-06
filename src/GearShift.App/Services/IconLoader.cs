using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace GearShift.App.Services;

/// <summary>Loads a program's icon from its executable via the shell thumbnail API (no extra deps).</summary>
public static class IconLoader
{
    public static async Task<ImageSource?> FromExeAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 32);
            if (thumb is null || thumb.Size == 0)
                return null;

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumb);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
