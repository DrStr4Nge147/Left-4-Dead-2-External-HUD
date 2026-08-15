using System.IO;
using System.Text;

namespace OverlayHud.Services;

/// <summary>What one pack turned out to be. Both answers come from a single walk.</summary>
internal readonly record struct PackContents(bool HasExporter, string? AddonVersion);

/// <summary>
/// Reads just enough of a VPK to answer two questions: does this pack contain the exporter,
/// and what version does its addoninfo.txt claim?
///
/// Filenames cannot answer the first. A manual install keeps the name it was given, but a
/// Workshop subscription is stored as <c>addons\workshop\&lt;publishedfileid&gt;.vpk</c> - a
/// number that says nothing about what is inside. Matching on the name would report a
/// subscribed addon as missing, which is the same false alarm this whole probe exists to
/// remove.
///
/// Only the directory tree is read, plus the handful of bytes addoninfo.txt occupies. The
/// tree sits at the front of the file and its size is in the header, so a 200 MB map pack
/// costs the same as a small one.
/// </summary>
internal static class VpkReader
{
    private const uint Signature = 0x55AA1234;

    /// <summary>Entries with this archive index are stored in the directory file itself.</summary>
    private const ushort InlineArchive = 0x7FFF;

    /// <summary>Where the exporter's script lives inside its pack.</summary>
    public const string ScriptDirectory = "scripts/vscripts";
    public const string ScriptName = "overlay_hud_export";
    public const string ScriptExtension = "nut";

    /// <summary>The addon's own manifest, at the root of the pack. VPK spells root " ".</summary>
    public const string InfoDirectory = " ";
    public const string InfoName = "addoninfo";
    public const string InfoExtension = "txt";

    /// <summary>An addoninfo.txt is never anywhere near this big; a claim that it is, is junk.</summary>
    private const int MaxInfoBytes = 64 * 1024;

    /// <summary>
    /// What this pack carries. Everything false/null for anything unreadable or not a VPK: a
    /// pack that cannot be parsed is not evidence of an addon being absent.
    /// </summary>
    public static PackContents Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            if (stream.Length < 12 || reader.ReadUInt32() != Signature) return default;

            uint version = reader.ReadUInt32();
            uint treeSize = reader.ReadUInt32();

            if (version is not (1 or 2)) return default;
            if (treeSize == 0 || treeSize > stream.Length) return default;

            // v2 carries four more header fields before the tree begins.
            if (version == 2) stream.Position += 16;

            long treeEnd = stream.Position + treeSize;
            var scan = ScanTree(reader, treeEnd);

            return new PackContents(scan.HasExporter,
                                    ReadAddonVersion(path, reader, scan.Info, treeEnd));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>Where a file's bytes live. Zero length with preload means it is all inline.</summary>
    private readonly record struct EntryLocation(byte[] Preload, ushort Archive,
                                                 uint Offset, uint Length);

    private readonly record struct TreeScan(bool HasExporter, EntryLocation? Info);

    /// <summary>
    /// Walks the extension / directory / filename tree. Every read is bounded by the tree
    /// end taken from the header, so a corrupt pack cannot run this off the end of the file.
    /// </summary>
    private static TreeScan ScanTree(BinaryReader reader, long treeEnd)
    {
        bool exporter = false;
        EntryLocation? info = null;

        while (reader.BaseStream.Position < treeEnd)
        {
            string extension = ReadString(reader, treeEnd);
            if (extension.Length == 0) break;             // end of tree

            bool scriptExtension = extension.Equals(ScriptExtension,
                                                    StringComparison.OrdinalIgnoreCase);
            bool infoExtension = extension.Equals(InfoExtension,
                                                  StringComparison.OrdinalIgnoreCase);

            while (reader.BaseStream.Position < treeEnd)
            {
                string directory = ReadString(reader, treeEnd);
                if (directory.Length == 0) break;         // end of this extension

                bool scriptDirectory = scriptExtension
                    && directory.Equals(ScriptDirectory, StringComparison.OrdinalIgnoreCase);
                bool infoDirectory = infoExtension && directory.Trim().Length == 0;

                while (reader.BaseStream.Position < treeEnd)
                {
                    string name = ReadString(reader, treeEnd);
                    if (name.Length == 0) break;          // end of this directory

                    bool wantedInfo = infoDirectory
                        && name.Equals(InfoName, StringComparison.OrdinalIgnoreCase);

                    exporter |= scriptDirectory
                        && name.Equals(ScriptName, StringComparison.OrdinalIgnoreCase);

                    var entry = ReadEntry(reader, treeEnd);
                    if (wantedInfo) info = entry;
                }
            }
        }

        return new TreeScan(exporter, info);
    }

    /// <summary>crc, preload size, archive index, offset, length, terminator, preload data.</summary>
    private static EntryLocation ReadEntry(BinaryReader reader, long treeEnd)
    {
        if (reader.BaseStream.Position + 18 > treeEnd)
        {
            reader.BaseStream.Position = treeEnd;
            return default;
        }

        reader.ReadUInt32();                              // crc
        ushort preload = reader.ReadUInt16();
        ushort archive = reader.ReadUInt16();
        uint offset = reader.ReadUInt32();
        uint length = reader.ReadUInt32();
        reader.ReadUInt16();                              // terminator

        long start = reader.BaseStream.Position;
        long end = Math.Min(treeEnd, start + preload);
        var bytes = reader.ReadBytes((int)(end - start));

        reader.BaseStream.Position = end;

        return new EntryLocation(bytes, archive, offset, length);
    }

    /// <summary>
    /// Pulls addoninfo.txt out of wherever the pack put it and reads addonversion from it.
    /// A small file is often stored inline as preload data; otherwise it sits after the tree
    /// in this same file, or - for a <c>_dir.vpk</c> - in the numbered archive beside it.
    /// </summary>
    private static string? ReadAddonVersion(string path, BinaryReader reader,
                                            EntryLocation? location, long treeEnd)
    {
        if (location is not { } entry) return null;
        if (entry.Length > MaxInfoBytes) return null;

        try
        {
            byte[] body = entry.Length == 0
                ? Array.Empty<byte>()
                : entry.Archive == InlineArchive
                    ? ReadAt(reader.BaseStream, treeEnd + entry.Offset, entry.Length)
                    : ReadFromArchive(path, entry);

            if (entry.Preload.Length == 0 && body.Length == 0) return null;

            var text = Encoding.UTF8.GetString(entry.Preload) + Encoding.UTF8.GetString(body);

            return AddonVersionFrom(text);
        }
        catch
        {
            // A pack we cannot read the manifest out of reports no version, which reads as
            // "unknown" everywhere upstream and warns about nothing.
            return null;
        }
    }

    private static byte[] ReadAt(Stream stream, long offset, uint length)
    {
        if (offset < 0 || offset + length > stream.Length) return Array.Empty<byte>();

        stream.Position = offset;

        var bytes = new byte[length];
        int read = stream.Read(bytes, 0, bytes.Length);

        return read == bytes.Length ? bytes : bytes[..read];
    }

    /// <summary>A multi-chunk pack keeps its file data in <c>name_000.vpk</c> beside the dir.</summary>
    private static byte[] ReadFromArchive(string dirPath, EntryLocation entry)
    {
        const string suffix = "_dir.vpk";
        if (!dirPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<byte>();

        var archive = dirPath[..^suffix.Length] + $"_{entry.Archive:000}.vpk";
        if (!File.Exists(archive)) return Array.Empty<byte>();

        using var stream = new FileStream(archive, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);

        return ReadAt(stream, entry.Offset, entry.Length);
    }

    /// <summary>
    /// Valve KeyValues, one <c>addonversion "1.0.9"</c> pair among others. Quotes are
    /// optional in the format and the file is authored by hand, so both forms are accepted.
    /// </summary>
    public static string? AddonVersionFrom(string keyValues)
    {
        foreach (var line in keyValues.Split('\n'))
        {
            var fields = line.Replace('"', ' ')
                             .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 2) continue;
            if (!fields[0].Equals("addonversion", StringComparison.OrdinalIgnoreCase)) continue;

            return fields[1];
        }

        return null;
    }

    private static string ReadString(BinaryReader reader, long limit)
    {
        var builder = new StringBuilder();

        while (reader.BaseStream.Position < limit)
        {
            byte value = reader.ReadByte();
            if (value == 0) break;

            builder.Append((char)value);
        }

        return builder.ToString();
    }
}
