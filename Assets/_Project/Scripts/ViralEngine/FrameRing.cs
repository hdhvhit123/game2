using System;

/// <summary>
/// Vòng đệm khung hình cấp phát trước một lần: không tạo rác GC mỗi frame
/// (bản cũ gọi ToArray() 20 lần/giây, mỗi lần ~1.5MB).
/// </summary>
public sealed class FrameRing
{
    public readonly int Capacity;
    public readonly int FrameBytes;
    private readonly byte[][] _slots;
    private int _head;   // slot sẽ ghi tiếp theo
    private int _count;

    public int Count => _count;

    public FrameRing(int capacity, int frameBytes)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (frameBytes <= 0) throw new ArgumentOutOfRangeException(nameof(frameBytes));
        Capacity = capacity;
        FrameBytes = frameBytes;
        _slots = new byte[capacity][];
    }

    /// <summary>Trả về mảng của slot kế tiếp để ghi dữ liệu vào (tái sử dụng, cấp phát lười).</summary>
    public byte[] AcquireWriteSlot()
    {
        return _slots[_head] ??= new byte[FrameBytes];
    }

    /// <summary>Xác nhận slot vừa ghi là khung mới nhất.</summary>
    public void CommitWrite()
    {
        _head = (_head + 1) % Capacity;
        if (_count < Capacity) _count++;
    }

    /// <summary>age = 0 là khung mới nhất, age = Count-1 là cũ nhất.</summary>
    public byte[] GetByAge(int age)
    {
        if (age < 0 || age >= _count) throw new ArgumentOutOfRangeException(nameof(age));
        int index = (_head - 1 - age + Capacity * 2) % Capacity;
        return _slots[index];
    }

    /// <summary>Chọn n khung trải đều từ cũ tới mới (khung cuối luôn là khung mới nhất).</summary>
    public int[] SampleAgesOldestFirst(int n)
    {
        n = Math.Min(n, _count);
        var ages = new int[n];
        if (n == 0) return ages;
        if (n == 1) { ages[0] = 0; return ages; }
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);           // 0 = cũ nhất, 1 = mới nhất
            ages[i] = (int)Math.Round((_count - 1) * (1f - t));
        }
        return ages;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }
}

/// <summary>
/// Ghép nhiều khung RGB24 thành một ảnh lưới kiểu truyện tranh.
/// Dữ liệu texture của Unity xếp hàng từ dưới lên, nên hàng lưới đầu tiên (khung cũ nhất)
/// phải nằm ở nửa TRÊN của bộ đệm.
/// </summary>
public static class CollageBuilder
{
    public const int BytesPerPixel = 3;

    public static void Compose(byte[][] framesOldestFirst, int frameWidth, int frameHeight, int columns, int rows,
                               int border, byte[] destination)
    {
        int outW = columns * frameWidth + (columns + 1) * border;
        int outH = rows * frameHeight + (rows + 1) * border;
        if (destination.Length < outW * outH * BytesPerPixel)
            throw new ArgumentException("destination quá nhỏ", nameof(destination));

        // Viền màu kem cho ảnh trông như truyện tranh.
        for (int i = 0; i + 2 < destination.Length; i += 3)
        {
            destination[i] = 255; destination[i + 1] = 244; destination[i + 2] = 214;
        }

        int rowBytes = frameWidth * BytesPerPixel;
        for (int f = 0; f < framesOldestFirst.Length && f < columns * rows; f++)
        {
            byte[] src = framesOldestFirst[f];
            if (src == null) continue;

            int col = f % columns;
            int gridRowFromTop = f / columns;
            int gridRowFromBottom = rows - 1 - gridRowFromTop;
            int x0 = border + col * (frameWidth + border);
            int y0 = border + gridRowFromBottom * (frameHeight + border);

            for (int y = 0; y < frameHeight; y++)
            {
                int dst = ((y0 + y) * outW + x0) * BytesPerPixel;
                Buffer.BlockCopy(src, y * rowBytes, destination, dst, rowBytes);
            }
        }
    }

    public static int OutputWidth(int frameWidth, int columns, int border) => columns * frameWidth + (columns + 1) * border;
    public static int OutputHeight(int frameHeight, int rows, int border) => rows * frameHeight + (rows + 1) * border;
}
