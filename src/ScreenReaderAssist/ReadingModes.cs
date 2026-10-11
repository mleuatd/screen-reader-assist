namespace ScreenReaderAssist;
public static class ReadingModes
{
    public const string Cursor="cursor", Manga="manga", Selection="selection", Off="off";
    public static string Normalize(string? mode) => mode switch
    {
        Cursor or Manga or Selection or Off => mode,
        _ => Cursor
    };
    public static bool IsAuto(string? mode) => mode is Cursor or Manga;
    public static string Label(string? mode)=>mode switch
    {
        Cursor => "カーソルで自動読み上げ",
        Manga => "漫画の吹き出しOCR",
        Selection => "範囲選択のみ",
        _ => "自動読み上げ停止"
    };
}
