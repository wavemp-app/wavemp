using System.Runtime.InteropServices;

namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// The calls of the C library the moves need on Unix. Error numbers are those of Linux; <c>EEXIST</c> and
/// <c>ENOENT</c> are the same on every Unix.
/// </summary>
internal static partial class UnixFileCalls
{
    public const int Success = 0;
    public const int Eperm = 1;
    public const int Enoent = 2;
    public const int Eexist = 17;
    public const int Exdev = 18;
    public const int Einval = 22;
    public const int Enosys = 38;
    public const int Eopnotsupp = 95;

    private const int AtFdCwd = -100;
    private const uint RenameNoReplaceFlag = 1;
    private const string Library = "libc";

    /// <summary>
    /// Renames without replacing; returns 0 or the error number. Throws <see cref="EntryPointNotFoundException"/>
    /// where the C library has no <c>renameat2</c> (older glibc, other systems).
    /// </summary>
    public static int RenameNoReplace(string sourcePath, string targetPath)
    {
        int result = RenameAt2(AtFdCwd, sourcePath, AtFdCwd, targetPath, RenameNoReplaceFlag);
        return result == 0 ? Success : Marshal.GetLastPInvokeError();
    }

    /// <summary>
    /// Creates a hard link; fails with <c>EEXIST</c> if the target exists. Returns 0 or the error number.
    /// </summary>
    public static int Link(string sourcePath, string targetPath)
    {
        int result = LinkFile(sourcePath, targetPath);
        return result == 0 ? Success : Marshal.GetLastPInvokeError();
    }

    [LibraryImport(Library, EntryPoint = "renameat2", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int RenameAt2(
        int oldDirectory,
        string oldPath,
        int newDirectory,
        string newPath,
        uint flags);

    [LibraryImport(Library, EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int LinkFile(string oldPath, string newPath);
}
