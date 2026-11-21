using System.Runtime.InteropServices;

namespace OnlyYours.Core.Security.Memory;

/// <summary>
/// Raw Win32 entry points used by the secure-memory layer. Everything here is
/// source-generated <see cref="LibraryImportAttribute"/> interop: no marshalling
/// stubs are created at run time and no managed copies of the data are made.
/// </summary>
internal static unsafe partial class Win32
{
    internal const uint MEM_COMMIT = 0x1000;
    internal const uint MEM_RESERVE = 0x2000;
    internal const uint MEM_RELEASE = 0x8000;

    internal const uint PAGE_NOACCESS = 0x01;
    internal const uint PAGE_READWRITE = 0x04;

    /// <summary>Data can only be decrypted by this very process.</summary>
    internal const uint CRYPTPROTECTMEMORY_SAME_PROCESS = 0x00;
    internal const int CRYPTPROTECTMEMORY_BLOCK_SIZE = 16;

    internal const int ERROR_WORKING_SET_QUOTA = 1453;

    internal const uint QUOTA_LIMITS_HARDWS_MIN_DISABLE = 0x2;
    internal const uint QUOTA_LIMITS_HARDWS_MAX_DISABLE = 0x8;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial void* VirtualAlloc(void* address, nuint size, uint allocationType, uint protect);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool VirtualFree(void* address, nuint size, uint freeType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool VirtualLock(void* address, nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool VirtualUnlock(void* address, nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool VirtualProtect(void* address, nuint size, uint newProtect, out uint oldProtect);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptProtectMemory(void* data, uint size, uint flags);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptUnprotectMemory(void* data, uint size, uint flags);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetProcessWorkingSetSizeEx(nint process, out nuint minimum, out nuint maximum, out uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessWorkingSetSizeEx(nint process, nuint minimum, nuint maximum, uint flags);
}
