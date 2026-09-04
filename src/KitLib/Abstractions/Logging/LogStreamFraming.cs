namespace KitLib.Logging;

/// <summary>Length-prefixed UTF-8 JSON frames for the KitLib log pipe.</summary>
public static class LogStreamFraming {
    public static byte[] Encode(LogStreamEntry entry) {
        var json = entry.ToJsonBytes();
        if (json.Length > LogStreamContract.MaxFrameBytes)
            throw new InvalidOperationException($"Log frame exceeds {LogStreamContract.MaxFrameBytes} bytes.");

        var frame = new byte[4 + json.Length];
        WriteLength(frame, json.Length);
        json.CopyTo(frame, 4);
        return frame;
    }

    public static bool TryReadFrame(Stream stream, out LogStreamEntry? entry) {
        entry = null;
        Span<byte> lenBuf = stackalloc byte[4];
        if (!ReadExact(stream, lenBuf))
            return false;

        int length = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (length <= 0 || length > LogStreamContract.MaxFrameBytes)
            throw new InvalidDataException($"Invalid log frame length: {length}.");

        var json = new byte[length];
        if (!ReadExact(stream, json))
            return false;

        entry = LogStreamEntry.FromJsonBytes(json);
        return entry != null;
    }

    public static async Task<LogStreamEntry?> ReadFrameAsync(Stream stream, CancellationToken ct) {
        var lenBuf = new byte[4];
        if (!await ReadExactAsync(stream, lenBuf, ct))
            return null;

        int length = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (length <= 0 || length > LogStreamContract.MaxFrameBytes)
            throw new InvalidDataException($"Invalid log frame length: {length}.");

        var json = new byte[length];
        if (!await ReadExactAsync(stream, json, ct))
            return null;

        return LogStreamEntry.FromJsonBytes(json);
    }

    public static async Task WriteFrameAsync(Stream stream, LogStreamEntry entry, CancellationToken ct) {
        var frame = Encode(entry);
        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }

    static void WriteLength(byte[] buffer, int length) {
        buffer[0] = (byte)(length & 0xFF);
        buffer[1] = (byte)((length >> 8) & 0xFF);
        buffer[2] = (byte)((length >> 16) & 0xFF);
        buffer[3] = (byte)((length >> 24) & 0xFF);
    }

    static bool ReadExact(Stream stream, Span<byte> buffer) {
        int offset = 0;
        while (offset < buffer.Length) {
            int read = stream.Read(buffer[offset..]);
            if (read <= 0)
                return false;
            offset += read;
        }

        return true;
    }

    static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct) {
        int offset = 0;
        while (offset < buffer.Length) {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read <= 0)
                return false;
            offset += read;
        }

        return true;
    }
}
