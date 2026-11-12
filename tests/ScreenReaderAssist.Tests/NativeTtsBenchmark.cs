// 旧ベンチマーク名との互換用入口。英語音声には切り替えません。
// 既定の性能テストは必ず日本語SAPI音声で実行し、未導入なら「未検証」とします。
internal static class NativeTtsBenchmark
{
    public static int Run() => JapaneseTtsBenchmark.Run();
}
