using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 解析 ZIP 中央目录，读取原始文件名字节并自动探测编码（UTF-8 / Shift-JIS / GBK）
/// </summary>
public static class ZipNameDecoder
{
    public class RawEntryInfo
    {
        public byte[] RawName;   // 原始文件名字节
        public bool Utf8Flag;    // general purpose bit 11 (EFS)
        public byte[] Extra;     // extra field（可能含 Info-ZIP Unicode Path）
    }

    private static readonly Encoding Utf8Strict;
    private static readonly Encoding Utf8;
    private static readonly Encoding SjisStrict;
    private static readonly Encoding GbkStrict;
    private static readonly Encoding Sjis;

    static ZipNameDecoder()
    {
        // 必须先注册，否则 GetEncoding(932/936) 会抛异常
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Utf8Strict  = new UTF8Encoding(false, true);   // 严格模式，遇错抛异常
        Utf8        = new UTF8Encoding(false, false);
        SjisStrict  = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        GbkStrict   = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        Sjis        = Encoding.GetEncoding(932);
    }

    /// <summary>
    /// 读取中央目录，返回与 ZipArchive.Entries 顺序一一对应的原始条目信息
    /// </summary>
    public static List<RawEntryInfo> ReadCentralDirectory(string zipPath)
    {
        var entries = new List<RawEntryInfo>();
        using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

        long eocdPos = FindEocd(fs);
        fs.Position = eocdPos + 4;
        br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16();
        ushort totalEntries = br.ReadUInt16(); // 条目总数
        br.ReadUInt32();                       // CD 大小
        uint cdOffset = br.ReadUInt32();       // CD 起始偏移

        fs.Position = cdOffset;
        for (int i = 0; i < totalEntries; i++)
        {
            if (br.ReadUInt32() != 0x02014b50)
                throw new InvalidDataException("ZIP 中央目录条目签名异常");

            // 读取中央目录固定头字段
            br.ReadUInt16(); // version made by
            br.ReadUInt16(); // version needed
            ushort gpFlag = br.ReadUInt16();
            br.ReadUInt16(); // compression method
            br.ReadUInt16(); // last mod time
            br.ReadUInt16(); // last mod date
            br.ReadUInt32(); // crc32
            br.ReadUInt32(); // compressed size
            br.ReadUInt32(); // uncompressed size
            ushort nameLen = br.ReadUInt16();
            ushort extraLen = br.ReadUInt16();
            ushort commentLen = br.ReadUInt16();
            br.ReadUInt16(); // disk number start
            br.ReadUInt16(); // internal file attributes
            br.ReadUInt32(); // external file attributes
            br.ReadUInt32(); // local header offset

            byte[] rawName = br.ReadBytes(nameLen);
            byte[] extra = br.ReadBytes(extraLen);
            // comment bytes are not needed for filename decoding, but must be skipped
            if (commentLen > 0)
                br.ReadBytes(commentLen);

            entries.Add(new RawEntryInfo
            {
                RawName = rawName,
                // 0x0800 就是 bit 11。gpFlag & 0x0800 不为 0，说明 EFS 标志存在，文件名是 UTF-8
                Utf8Flag = (gpFlag & 0x0800) != 0,
                Extra = extra
            });
        }
        return entries;
    }

    private static long FindEocd(FileStream fs)
    {
        long tailLen = Math.Min(fs.Length, 65535 + 22);
        fs.Position = fs.Length - tailLen;
        byte[] tail = new byte[tailLen];
        fs.ReadExactly(tail, 0, (int)tailLen);

        for (int i = tail.Length - 22; i >= 0; i--)
        {
            // EOCD 签名 50 4B 05 06
            if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                return fs.Length - tailLen + i; // 找到后返回绝对位置
        }
        throw new InvalidDataException("找不到 ZIP 结尾目录记录 (EOCD)");
    }

    public static string DecodeEntryName(RawEntryInfo info)
    {
        // 1) Info-ZIP Unicode Path extra field（最可靠，有 CRC 校验）
        string uni = TryGetUnicodePathExtra(info.Extra, info.RawName);
        if (uni != null) return uni;

        // 2) EFS 标志位：文件名是 UTF-8
        if (info.Utf8Flag)
        {
            try { return Utf8Strict.GetString(info.RawName); }
            catch { return Utf8.GetString(info.RawName); }
        }

        // 3) 严格 UTF-8 能解 → UTF-8（纯英文也走这里）
        try { return Utf8Strict.GetString(info.RawName); } catch { }

        // 4) Shift-JIS 严格解码
        string sjisText = null;
        bool sjisOk = TryDecode(SjisStrict, info.RawName, out sjisText);
        // 半角片假名是 GBK 字节被误当 Shift-JIS 解码时的典型产物
        if (sjisOk && !HasHalfwidthKatakana(sjisText))
            return sjisText;

        // 5) 尝试 GBK
        string gbkText = null;
        if (TryDecode(GbkStrict, info.RawName, out gbkText))
            return gbkText;

        // 6) 兜底：不抛异常的宽松解码
        return sjisOk ? sjisText : Sjis.GetString(info.RawName);
    }

    private static bool TryDecode(Encoding enc, byte[] bytes, out string text)
    {
        try { text = enc.GetString(bytes); return true; }
        catch { text = null; return false; }
    }

    private static bool HasHalfwidthKatakana(string s)
    {
        foreach (char c in s)
            if (c >= '｡' && c <= 'ﾟ') return true; // U+FF61–U+FF9F
        return false;
    }

    private static string TryGetUnicodePathExtra(byte[] extra, byte[] rawName)
    {
        if (extra == null) return null;
        int pos = 0;
        while (pos + 4 <= extra.Length)
        {
            ushort id   = (ushort)(extra[pos] | (extra[pos + 1] << 8));
            ushort size = (ushort)(extra[pos + 2] | (extra[pos + 3] << 8));
            pos += 4;
            if (pos + size > extra.Length) break;

            if (id == 0x7075 && size >= 5 && extra[pos] == 1) // Info-ZIP Unicode Path v1
            {
                uint crc = BitConverter.ToUInt32(extra, pos + 1);
                if (crc == Crc32(rawName))
                    return Encoding.UTF8.GetString(extra, pos + 5, size - 5);
            }
            pos += size;
        }
        return null;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    private static uint Crc32(byte[] bytes)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in bytes) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}