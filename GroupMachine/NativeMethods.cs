/*
 * GroupMachine - Groups photos and videos into albums (folders) based on time & location changes.
 * Copyright (c) 2025 Richard Lawrence
 * http://github.com/mrsilver76/groupmachine/
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *  
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>
/// Provides platform-specific P/Invoke declarations for creating hard links and symbolic links.
/// </summary>
/// <remarks>This class contains methods that directly invoke native APIs for file system operations, such as
/// creating hard links and symbolic links. The methods are platform-specific and are only available on supported
/// operating systems.  Use these methods with caution, as they interact with unmanaged code and require appropriate
/// permissions.</remarks>
internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    [return: MarshalAs(UnmanagedType.Bool)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial bool CreateHardLink(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    [return: MarshalAs(UnmanagedType.U1)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial bool CreateSymbolicLink(
        string lpSymlinkFileName,
        string lpTargetFileName,
        uint dwFlags);

    [LibraryImport("libc", EntryPoint = "link",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int Link(string existing, string @new);

    [LibraryImport("libc", EntryPoint = "symlink",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int Symlink(string target, string linkpath);
}


