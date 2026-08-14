using System.IO.Compression;

namespace AssignmentSystem.Api.Services;

public static class UploadRules
{
    public const long MaximumFileBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".zip", ".jpg", ".jpeg", ".png"
    };

    public static bool TryValidateMetadata(string fileName, long length, out string extension, out string error)
    {
        extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            error = "Allowed file types: PDF, DOCX, ZIP, JPG, and PNG.";
            return false;
        }

        if (length <= 0 || length > MaximumFileBytes)
        {
            error = "File must be between 1 byte and 10 MB.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool HasValidContent(string extension, Stream stream)
    {
        if (!stream.CanSeek)
            return false;

        var initialPosition = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[8];
            var bytesRead = stream.Read(header);
            stream.Position = initialPosition;

            var validSignature = extension.ToLowerInvariant() switch
            {
                ".pdf" => StartsWith(header[..bytesRead], "%PDF-"u8),
                ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
                ".png" => StartsWith(header[..bytesRead], new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
                ".zip" or ".docx" => HasZipSignature(header[..bytesRead]),
                _ => false
            };

            if (!validSignature)
                return false;

            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                return IsReadableArchive(stream, requireWordDocument: false);

            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                return IsReadableArchive(stream, requireWordDocument: true);

            return true;
        }
        finally
        {
            stream.Position = initialPosition;
        }
    }

    public static bool CanDisplayInline(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> expected) =>
        value.Length >= expected.Length && value[..expected.Length].SequenceEqual(expected);

    private static bool HasZipSignature(ReadOnlySpan<byte> value) =>
        value.Length >= 4 && value[0] == 0x50 && value[1] == 0x4b && value[2] is 0x03 or 0x05 or 0x07 && value[3] is 0x04 or 0x06 or 0x08;

    private static bool IsReadableArchive(Stream stream, bool requireWordDocument)
    {
        try
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            _ = archive.Entries.Count;
            if (!requireWordDocument)
                return true;

            return archive.GetEntry("[Content_Types].xml") is not null && archive.GetEntry("word/document.xml") is not null;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return false;
        }
    }
}
