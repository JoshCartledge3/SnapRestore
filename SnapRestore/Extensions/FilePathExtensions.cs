using System;
using System.IO;

namespace SnapRestore.Extensions;

public static class FilePathExtensions
{
    public static bool IsUploadFile(this string path)
    {
        return Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)
               || Directory.Exists(path);
    }
    
    public static bool IsImageFile(this string path)
    {
        var extension = Path.GetExtension(path);

        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVideoFile(this string path)
    {
        return Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }
}
