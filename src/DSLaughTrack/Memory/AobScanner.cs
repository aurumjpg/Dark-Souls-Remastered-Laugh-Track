namespace DSLaughTrack.Memory;

public static class AobScanner
{
    public static int Find(byte[] haystack, string pattern)
    {
        var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needle = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            needle[i] = parts[i] is "?" or "??" ? -1 : Convert.ToInt32(parts[i], 16);

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (needle[j] != -1 && haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
