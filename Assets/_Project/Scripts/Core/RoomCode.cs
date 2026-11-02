using System;
using System.Text;

/// <summary>
/// Tạo, kiểm tra và trích xuất mã phòng. Mọi mã phòng đến từ Deep Link / Install Referrer
/// đều là dữ liệu không tin cậy nên phải qua TrySanitize trước khi dùng làm SessionName.
/// </summary>
public static class RoomCode
{
    public const int MinLength = 4;
    public const int MaxLength = 12;
    public const int DefaultLength = 6;

    // Bỏ các ký tự dễ nhầm (0/O, 1/I/L) để người chơi đọc mã cho nhau qua Zalo không bị sai.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private static readonly Random SharedRandom = new Random();

    public static string Generate(int length = DefaultLength, Random rng = null)
    {
        length = Math.Max(MinLength, Math.Min(MaxLength, length));
        rng ??= SharedRandom;
        var sb = new StringBuilder(length);
        lock (rng)
        {
            for (int i = 0; i < length; i++) sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
        }
        return sb.ToString();
    }

    /// <summary>Chỉ chấp nhận chữ và số ASCII, độ dài 4..12, chuẩn hoá về chữ hoa.</summary>
    public static bool TrySanitize(string raw, out string code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string trimmed = raw.Trim();
        if (trimmed.Length < MinLength || trimmed.Length > MaxLength) return false;

        var sb = new StringBuilder(trimmed.Length);
        foreach (char c in trimmed)
        {
            bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (!ok) return false;
            sb.Append(char.ToUpperInvariant(c));
        }
        code = sb.ToString();
        return true;
    }

    /// <summary>Lấy mã phòng từ link dạng https://host/join?room=ABC123&amp;x=y.</summary>
    public static bool TryParseFromUrl(string url, out string code)
    {
        code = null;
        if (string.IsNullOrEmpty(url) || url.Length > 2048) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
        return TryGetQueryValue(uri.Query, "room", out string value) && TrySanitize(value, out code);
    }

    /// <summary>
    /// Install Referrer của Google Play thường có dạng "utm_source=zalo&amp;room=ABC123"
    /// (đã URL-encode) hoặc kiểu cũ "room_ABC123".
    /// </summary>
    public static bool TryParseFromReferrer(string referrer, out string code)
    {
        code = null;
        if (string.IsNullOrEmpty(referrer) || referrer.Length > 1024) return false;

        string decoded;
        try { decoded = Uri.UnescapeDataString(referrer); }
        catch (Exception) { return false; }

        if (decoded.StartsWith("room_", StringComparison.OrdinalIgnoreCase))
            return TrySanitize(decoded.Substring(5), out code);

        return TryGetQueryValue(decoded, "room", out string value) && TrySanitize(value, out code);
    }

    public static string BuildInviteUrl(string host, string code)
    {
        return $"https://{host}/join?room={Uri.EscapeDataString(code ?? string.Empty)}";
    }

    private static bool TryGetQueryValue(string query, string key, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(query)) return false;
        if (query[0] == '?') query = query.Substring(1);

        foreach (string pair in query.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            if (!string.Equals(pair.Substring(0, eq), key, StringComparison.OrdinalIgnoreCase)) continue;
            try { value = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' ')); }
            catch (Exception) { return false; }
            return true;
        }
        return false;
    }
}
