using System.IO.Compression;

namespace ReleaseTool.Desktop.Services;

public static class ZipVerifyService
{
    public static (bool Ok, string Message) Verify(string zipPath)
    {
        if (!File.Exists(zipPath))
            return (false, "ZIP bulunamadı");

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var count = archive.Entries.Count;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                    continue;
                using var stream = entry.Open();
                var buffer = new byte[4096];
                while (stream.Read(buffer, 0, buffer.Length) > 0)
                {
                    // read-through
                }
            }

            return (true, $"ZIP geçerli — {count} giriş");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
