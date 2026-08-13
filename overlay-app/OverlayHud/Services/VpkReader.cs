using System.IO;
using System.Text;

namespace OverlayHud.Services;

/// <summary>
/// Reads just enough of a VPK to answer one question: does this pack contain the exporter?
///
/// Filenames cannot answer it. A manual install keeps the name it was given, but a Workshop
/// subscription is stored as <c>addons\workshop\&lt;publishedfileid&gt;.vpk</c> - a number
/// that says nothing about what is inside. Matching on the name would report a subscribed
/// addon as missing, which is the same false alarm this whole probe exists to remove.
///
/// Only the directory tree is read. It sits at the front of the file and its size is in the
/// header, so a 200 MB map pack costs the same as a small one.
/// </summary>
internal static class VpkReader
{
    private const uint Signature = 0x55AA1234;

    /// <summary>Where the exporter's script lives inside its pack.</summary>
    public const string ScriptDirectory = "scripts/vscripts";
    public const string ScriptName = "overlay_hud_export";
    public const string ScriptExtension = "nut";

    /// <summary>
    /// True when this pack carries the exporter script. False for anything unreadable or
    /// not a VPK: a pack that cannot be parsed is not evidence of an addon being absent.
    /// </summary>
    public static bool ContainsExporter(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            if (stream.Length < 12 || reader.ReadUInt32() != Signature) return false;

            uint version = reader.ReadUInt32();
            uint treeSize = reader.ReadUInt32();

            if (version is not (1 or 2)) return false;
            if (treeSize == 0 || treeSize > stream.Length) return false;

            // v2 carries four more header fields before the tree begins.
            if (version == 2) stream.Position += 16;

            return TreeContainsExporter(reader, stream.Position + treeSize);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Walks the extension / directory / filename tree. Every read is bounded by the tree
    /// end taken from the header, so a corrupt pack cannot run this off the end of the file.
    /// </summary>
    private static bool TreeContainsExporter(BinaryReader reader, long treeEnd)
    {
        while (reader.BaseStream.Position < treeEnd)
        {
            string extension = ReadString(reader, treeEnd);
            if (extension.Length == 0) return false;      // end of tree

            bool wantedExtension = extension.Equals(ScriptExtension,
                                                    StringComparison.OrdinalIgnoreCase);

            while (reader.BaseStream.Position < treeEnd)
            {
                string directory = ReadString(reader, treeEnd);
                if (directory.Length == 0) break;         // end of this extension

                bool wantedDirectory = wantedExtension
                    && directory.Equals(ScriptDirectory, StringComparison.OrdinalIgnoreCase);

                while (reader.BaseStream.Position < treeEnd)
                {
                    string name = ReadString(reader, treeEnd);
                    if (name.Length == 0) break;          // end of this directory

                    if (wantedDirectory
                        && name.Equals(ScriptName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    SkipEntry(reader, treeEnd);
                }
            }
        }

        return false;
    }

    /// <summary>crc, preload size, archive index, offset, length, terminator, preload data.</summary>
    private static void SkipEntry(BinaryReader reader, long treeEnd)
    {
        if (reader.BaseStream.Position + 18 > treeEnd)
        {
            reader.BaseStream.Position = treeEnd;
            return;
        }

        reader.ReadUInt32();                              // crc
        ushort preload = reader.ReadUInt16();
        reader.ReadUInt16();                              // archive index
        reader.ReadUInt32();                              // entry offset
        reader.ReadUInt32();                              // entry length
        reader.ReadUInt16();                              // terminator

        reader.BaseStream.Position = Math.Min(treeEnd, reader.BaseStream.Position + preload);
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
