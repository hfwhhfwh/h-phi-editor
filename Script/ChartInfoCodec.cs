using System;
using System.Collections.Generic;

public enum ChartInfoFormat { Txt, Yml }

public static class ChartInfoCodec
{
    // ---------- Dictionary → ChartInfo ----------
    public static ChartInfo FromDict(IReadOnlyDictionary<string, string> d, ChartInfoFormat f) => f switch
    {
        ChartInfoFormat.Txt => new ChartInfo
        {
            Id             = d.GetValueOrDefault("Path"),
            Name           = d.GetValueOrDefault("Name"),
            Composer       = d.GetValueOrDefault("Composer"),
            Charter        = d.GetValueOrDefault("Charter"),
            Level          = d.GetValueOrDefault("Level", "0"),
            Bpm            = ParseF(d.GetValueOrDefault("Bpm")),
            Offset         = ParseF(d.GetValueOrDefault("Offset")),
            Duration       = ParseF(d.GetValueOrDefault("Length")),
            SongFileName   = d.GetValueOrDefault("Song"),
            PictureFileName= d.GetValueOrDefault("Picture"),
            ChartFileName  = d.GetValueOrDefault("Chart"),
        },
        ChartInfoFormat.Yml => new ChartInfo
        {
            Id             = d.GetValueOrDefault("id"),
            Name           = d.GetValueOrDefault("name"),
            Composer       = d.GetValueOrDefault("composer"),
            Charter        = d.GetValueOrDefault("charter"),
            Level          = d.GetValueOrDefault("level", "0"),
            Bpm            = ParseF(d.GetValueOrDefault("Bpm")),
            Offset         = ParseF(d.GetValueOrDefault("offset")),
            Duration       = ParseF(d.GetValueOrDefault("Length")),
            SongFileName   = d.GetValueOrDefault("music"),
            PictureFileName= d.GetValueOrDefault("illustration"),
            ChartFileName  = d.GetValueOrDefault("chart"),
            Difficulty     = ParseF(d.GetValueOrDefault("difficulty")),
            Format         = d.GetValueOrDefault("format"),
            UnlockVideo    = d.GetValueOrDefault("unlockVideo"),
            PreviewStart   = ParseF(d.GetValueOrDefault("previewStart")),
            PreviewEnd     = ParseF(d.GetValueOrDefault("previewEnd")),
            AspectRatio    = ParseF(d.GetValueOrDefault("aspectRatio")),
            BackgroundDim  = ParseF(d.GetValueOrDefault("backgroundDim")),
            LineLength     = ParseF(d.GetValueOrDefault("lineLength")),
            Tip            = d.GetValueOrDefault("tip"),
            Intro          = d.GetValueOrDefault("intro"),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(f))
    };

    // ---------- ChartInfo → Dictionary ----------
    public static Dictionary<string, string> ToDict(this ChartInfo i, ChartInfoFormat f) => f switch
    {
        ChartInfoFormat.Txt => new()
        {
            ["Name"]    = i.Name,
            ["Path"]    = i.Id,
            ["Song"]    = i.SongFileName,
            ["Picture"] = i.PictureFileName,
            ["Chart"]   = i.ChartFileName,
            ["Level"]   = i.Level,
            ["Composer"]= i.Composer,
            ["Charter"] = i.Charter,
            ["Bpm"]     = i.Bpm?.ToString(),
            ["Offset"]  = i.Offset?.ToString(),
            ["Length"]  = i.Duration?.ToString(),
        },
        ChartInfoFormat.Yml => new()
        {
            ["id"]           = i.Id,
            ["name"]         = i.Name,
            ["difficulty"]   = i.Difficulty?.ToString(),
            ["level"]        = i.Level,
            ["charter"]      = i.Charter,
            ["composer"]     = i.Composer,
            ["chart"]        = i.ChartFileName,
            ["format"]       = i.Format,
            ["music"]        = i.SongFileName,
            ["illustration"] = i.PictureFileName,
            ["unlockVideo"]  = i.UnlockVideo,
            ["previewStart"] = i.PreviewStart?.ToString(),
            ["previewEnd"]   = i.PreviewEnd?.ToString(),
            ["aspectRatio"]  = i.AspectRatio?.ToString(),
            ["backgroundDim"]= i.BackgroundDim?.ToString(),
            ["lineLength"]   = i.LineLength?.ToString(),
            ["offset"]       = i.Offset?.ToString(),
            ["tip"]          = i.Tip,
            ["intro"]        = i.Intro,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(f))
    };

    // ---------- 文件级封装 ----------
    public static ChartInfo Load(string path, ChartInfoFormat f)
        => FromDict(FileUtil.ReadInfoFile(path), f);

    public static void Save(ChartInfo info, string path, ChartInfoFormat f)
        => FileUtil.WriteInfoFile(path, ToDict(info, f), f);

    private static float? ParseF(string v)
        => string.IsNullOrWhiteSpace(v) || v == "null"
           ? null
           : (float.TryParse(v, out var r) ? r : null);
}