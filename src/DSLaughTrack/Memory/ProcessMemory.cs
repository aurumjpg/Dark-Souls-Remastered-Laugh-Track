using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DSLaughTrack.Memory;

/// Read-only process memory access. Opened with VM_READ|QUERY_INFORMATION only;
/// this class has no write capability by construction.
public sealed class ProcessMemory : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr handle, IntPtr address, byte[] buffer, int size, out IntPtr bytesRead);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private readonly IntPtr _handle;
    public long BaseAddress { get; }
    public int MainModuleSize { get; }

    private ProcessMemory(IntPtr handle, long baseAddress, int mainModuleSize)
    {
        _handle = handle;
        BaseAddress = baseAddress;
        MainModuleSize = mainModuleSize;
    }

    public static ProcessMemory? Attach(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var module = process.MainModule;
            if (module is null) return null;
            var handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, pid);
            if (handle == IntPtr.Zero) return null;
            return new ProcessMemory(handle, module.BaseAddress.ToInt64(), module.ModuleMemorySize);
        }
        catch
        {
            return null;
        }
    }

    public byte[]? ReadBytes(long address, int count)
    {
        var buffer = new byte[count];
        if (!ReadProcessMemory(_handle, new IntPtr(address), buffer, count, out var read) ||
            read.ToInt64() != count)
            return null;
        return buffer;
    }

    public int? ReadInt32(long address)
    {
        var b = ReadBytes(address, 4);
        return b is null ? null : BitConverter.ToInt32(b, 0);
    }

    public long? ReadInt64(long address)
    {
        var b = ReadBytes(address, 8);
        return b is null ? null : BitConverter.ToInt64(b, 0);
    }

    public byte[]? ReadMainModule() => ReadBytes(BaseAddress, MainModuleSize);

    public void Dispose()
    {
        if (_handle != IntPtr.Zero) CloseHandle(_handle);
    }
}
