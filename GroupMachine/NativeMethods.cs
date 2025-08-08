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

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

    [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static partial int Link(string existing, string @new);

    [LibraryImport("libc", EntryPoint = "symlink", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static partial int Symlink(string target, string linkpath);
}

