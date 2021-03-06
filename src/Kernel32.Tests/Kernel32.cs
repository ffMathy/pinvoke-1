﻿// Copyright (c) to owners found in https://github.com/AArnott/pinvoke/blob/master/COPYRIGHT.md. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using PInvoke;
using Xunit;
using static PInvoke.Constants;
using static PInvoke.Kernel32;

public partial class Kernel32
{
    private Random random = new Random();

    [Fact]
    public void CreateProcess_CmdListDirectories()
    {
        STARTUPINFO startupInfo = STARTUPINFO.Create();
        PROCESS_INFORMATION processInformation;
        bool result = CreateProcess(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "cmd.exe"),
            "/c dir",
            null,
            null,
            false,
            CreateProcessFlags.CREATE_NO_WINDOW,
            IntPtr.Zero,
            null,
            ref startupInfo,
            out processInformation);
        if (!result)
        {
            throw new Win32Exception();
        }

        CloseHandle(processInformation.hProcess);
        CloseHandle(processInformation.hThread);
    }

    [Fact]
    public unsafe void CreateProcess_SetUnicodeEnvironment()
    {
        string commandOutputFileName = Path.GetTempFileName();
        try
        {
            string environment = "abc=123\0def=456\0\0";
            fixed (void* environmentBlock = environment)
            {
                STARTUPINFO startupInfo = STARTUPINFO.Create();
                PROCESS_INFORMATION processInformation;
                bool result = CreateProcess(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "cmd.exe"),
                    $"/c set > \"{commandOutputFileName}\"",
                    null,
                    null,
                    false,
                    CreateProcessFlags.CREATE_NO_WINDOW | CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT,
                    new IntPtr(environmentBlock),
                    null,
                    ref startupInfo,
                    out processInformation);
                if (!result)
                {
                    throw new Win32Exception();
                }

                try
                {
                    // Wait for the process to exit.
                    Assert.Equal(
                        WaitForSingleObjectResult.WAIT_OBJECT_0,
                        WaitForSingleObject(new SafeObjectHandle(processInformation.hProcess, false), 5000));
                }
                finally
                {
                    CloseHandle(processInformation.hProcess);
                    CloseHandle(processInformation.hThread);
                }
            }

            string[] envVars = File.ReadAllLines(commandOutputFileName);
            Assert.Contains("abc=123", envVars);
            Assert.Contains("def=456", envVars);
        }
        finally
        {
            File.Delete(commandOutputFileName);
        }
    }

    [Fact]
    public void CreateFile_DeleteOnClose()
    {
        string testPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using (var tempFileHandle = CreateFile(
            testPath,
            PInvoke.Kernel32.FileAccess.GenericWrite,
            PInvoke.Kernel32.FileShare.Read,
            null,
            CreationDisposition.CreateAlways,
            CreateFileFlags.DeleteOnCloseFlag,
            new SafeObjectHandle()))
        {
            Assert.True(File.Exists(testPath));
        }

        Assert.False(File.Exists(testPath));
    }

    [Fact]
    public void FindFirstFile_NoMatches()
    {
        WIN32_FIND_DATA data;
        using (var handle = FindFirstFile("foodoesnotexist", out data))
        {
            Assert.True(handle.IsInvalid);
        }
    }

    [Fact]
    public void FindFirstFile_FindNextFile()
    {
        string testPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(testPath);
            string aTxt = Path.Combine(testPath, "a.txt");
            File.WriteAllText(aTxt, string.Empty);
            File.SetAttributes(aTxt, FileAttributes.Archive);

            var bTxt = Path.Combine(testPath, "b.txt");
            File.WriteAllText(bTxt, string.Empty);
            File.SetAttributes(bTxt, FileAttributes.Normal);

            WIN32_FIND_DATA data;
            using (var handle = FindFirstFile(Path.Combine(testPath, "*.txt"), out data))
            {
                Assert.False(handle.IsInvalid);
                Assert.Equal("a.txt", data.cFileName);
                Assert.Equal(FileAttribute.Archive, data.dwFileAttributes);

                Assert.True(FindNextFile(handle, out data));
                Assert.Equal("b.txt", data.cFileName);
                Assert.Equal(FileAttribute.Normal, data.dwFileAttributes);

                Assert.False(FindNextFile(handle, out data));
            }
        }
        finally
        {
            Directory.Delete(testPath, true);
        }
    }

    [Fact]
    public void Win32Exception_DerivesFromBCLType()
    {
        Assert.IsAssignableFrom<System.ComponentModel.Win32Exception>(new PInvoke.Win32Exception(1));
    }

    [Fact]
    public void GetCurrentThreadId_SameAsAppDomainOne()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var frameworkValue = AppDomain.GetCurrentThreadId();
#pragma warning restore CS0618 // Type or member is obsolete
        var pinvokeValue = GetCurrentThreadId();

        Assert.Equal((uint)frameworkValue, pinvokeValue);
    }

    [Fact]
    public void GetCurrentProcessId_SameAsProcessOne()
    {
        var frameworkValue = Process.GetCurrentProcess().Id;
        var pinvokeValue = GetCurrentProcessId();

        Assert.Equal((uint)frameworkValue, pinvokeValue);
    }

    [Fact]
    public void CreateToolhelp32Snapshot_CanGetCurrentProcess()
    {
        var currentProcess = GetCurrentProcessId();
        var snapshot = CreateToolhelp32Snapshot(CreateToolhelp32SnapshotFlags.TH32CS_SNAPPROCESS, 0);
        using (snapshot)
        {
            var processes = Process32Enumerate(snapshot).ToList();
            Assert.Contains(processes, p => p.th32ProcessID == currentProcess);
        }
    }

    [Fact]
    public void OpenProcess_CannotOpenSystem()
    {
        using (var system = OpenProcess(ProcessAccess.PROCESS_TERMINATE, false, 0x00000000))
        {
            var error = (Win32ErrorCode)Marshal.GetLastWin32Error();
            Assert.True(system.IsInvalid);
            Assert.Equal(Win32ErrorCode.ERROR_INVALID_PARAMETER, error);
        }
    }

    [Fact]
    public void OpenProcess_CanOpenSelf()
    {
        var currentProcessId = GetCurrentProcessId();
        var currentProcess = OpenProcess(ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION, false, currentProcessId);
        using (currentProcess)
        {
            Assert.False(currentProcess.IsInvalid);
        }
    }

    [Fact]
    public void QueryFullProcessImageName_CanGetForCurrentProcess()
    {
        var currentProcessId = GetCurrentProcessId();
        var currentProcess = OpenProcess(ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION, false, currentProcessId);
        using (currentProcess)
        {
            var actual = QueryFullProcessImageName(currentProcess);
            var expected = Process.GetCurrentProcess().MainModule.FileName;

            Assert.Equal(expected, actual, ignoreCase: true);
        }
    }

    [Fact]
    public void ReadFile_CanReadSynchronously()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var expected = new byte[testDataSize];
            this.random.NextBytes(expected);

            File.WriteAllBytes(testPath, expected);

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericRead,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.NormalAttribute,
                new SafeObjectHandle()))
            {
                var actual = ReadFile(file, testDataSize);
                var actualData = actual.Skip(actual.Offset).Take(actual.Count);
                Assert.Equal(expected, actualData);
            }
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void ReadFile_CanReadOverlappedWithWait()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var expected = new byte[testDataSize];
            this.random.NextBytes(expected);

            File.WriteAllBytes(testPath, expected);

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericRead,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                var actual = new byte[testDataSize];
                fixed (byte* pActual = actual)
                {
                    var result = ReadFile(file, pActual, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    uint bytesTransfered;
                    var overlappedResult = GetOverlappedResult(file, &overlapped, out bytesTransfered, true);
                    Assert.Equal((uint)testDataSize, bytesTransfered);
                    Assert.True(overlappedResult);
                }

                Assert.Equal(expected, actual);
            }
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void ReadFile_CanReadOverlappedWithWaitHandle()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var expected = new byte[testDataSize];
            this.random.NextBytes(expected);

            File.WriteAllBytes(testPath, expected);

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericRead,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                var actual = new byte[testDataSize];
                var evt = new ManualResetEvent(false);
                overlapped.hEvent = evt.SafeWaitHandle.DangerousGetHandle();
                fixed (byte* pActual = actual)
                {
                    var result = ReadFile(file, pActual, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    Assert.True(evt.WaitOne(TimeSpan.FromSeconds(30)));
                    uint bytesTransfered;
                    var overlappedResult = GetOverlappedResult(file, &overlapped, out bytesTransfered, false);
                    Assert.Equal((uint)testDataSize, bytesTransfered);
                    Assert.True(overlappedResult);
                }

                Assert.Equal(expected, actual);
            }
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public void WriteFile_CanWriteSynchronously()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var expected = new byte[testDataSize];
            this.random.NextBytes(expected);

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericWrite,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.NormalAttribute,
                new SafeObjectHandle()))
            {
                var bytesWritten = WriteFile(file, new ArraySegment<byte>(expected));
                Assert.Equal((uint)testDataSize, bytesWritten);
            }

            var actual = File.ReadAllBytes(testPath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void WriteFile_CanWriteOverlappedWithWait()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var expected = new byte[testDataSize];
            this.random.NextBytes(expected);

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericWrite,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                fixed (byte* pExpected = expected)
                {
                    var result = WriteFile(file, pExpected, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    uint bytesTransfered;
                    var overlappedResult = GetOverlappedResult(file, &overlapped, out bytesTransfered, true);
                    Assert.Equal((uint)testDataSize, bytesTransfered);
                    Assert.True(overlappedResult);
                }
            }

            var actual = File.ReadAllBytes(testPath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void WriteFile_CanWriteOverlappedWithWaitHandle()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var expected = new byte[testDataSize];
            this.random.NextBytes(expected);

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericWrite,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                var evt = new ManualResetEvent(false);
                overlapped.hEvent = evt.SafeWaitHandle.DangerousGetHandle();
                fixed (byte* pExpected = expected)
                {
                    var result = WriteFile(file, pExpected, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    Assert.True(evt.WaitOne(TimeSpan.FromSeconds(30)));
                    uint bytesTransfered;
                    var overlappedResult = GetOverlappedResult(file, &overlapped, out bytesTransfered, false);
                    Assert.Equal((uint)testDataSize, bytesTransfered);
                    Assert.True(overlappedResult);
                }
            }

            var actual = File.ReadAllBytes(testPath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void CancelIo_CancelWrite()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var buffer = new byte[testDataSize];

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericWrite,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                fixed (byte* pExpected = buffer)
                {
                    var result = WriteFile(file, pExpected, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    try
                    {
                        Assert.True(CancelIo(file));
                    }
                    finally
                    {
                        uint bytesTransfered;
                        GetOverlappedResult(file, &overlapped, out bytesTransfered, true);
                    }
                }
            }
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void CancelIoEx_CancelWriteAll()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var buffer = new byte[testDataSize];

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericWrite,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                fixed (byte* pExpected = buffer)
                {
                    var result = WriteFile(file, pExpected, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    try
                    {
                        var cancelled = CancelIoEx(file, null);

                        // We can't assert that it's true as if the IO finished already it'll fail with ERROR_NOT_FOUND
                        if (!cancelled)
                        {
                            Assert.Equal(Win32ErrorCode.ERROR_NOT_FOUND, GetLastError());
                        }
                    }
                    finally
                    {
                        uint bytesTransfered;
                        GetOverlappedResult(file, &overlapped, out bytesTransfered, true);
                    }
                }
            }
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public unsafe void CancelIoEx_CancelWriteSpecific()
    {
        var testPath = Path.GetTempFileName();
        try
        {
            const int testDataSize = 256;
            var buffer = new byte[testDataSize];

            using (var file = CreateFile(
                testPath,
                PInvoke.Kernel32.FileAccess.GenericWrite,
                PInvoke.Kernel32.FileShare.None,
                null,
                CreationDisposition.OpenExisting,
                CreateFileFlags.OverlappedFlag,
                new SafeObjectHandle()))
            {
                var overlapped = default(OVERLAPPED);
                fixed (byte* pExpected = buffer)
                {
                    var result = WriteFile(file, pExpected, testDataSize, null, &overlapped);
                    if (result)
                    {
                        // We can't really test anything not covered by another test here :(
                        return;
                    }

                    var lastError = GetLastError();
                    Assert.Equal(Win32ErrorCode.ERROR_IO_PENDING, lastError);
                    try
                    {
                        var cancelled = CancelIoEx(file, &overlapped);

                        // We can't assert that it's true as if the IO finished already it'll fail with ERROR_NOT_FOUND
                        if (!cancelled)
                        {
                            Assert.Equal(Win32ErrorCode.ERROR_NOT_FOUND, GetLastError());
                        }
                    }
                    finally
                    {
                        uint bytesTransfered;
                        GetOverlappedResult(file, &overlapped, out bytesTransfered, true);
                    }
                }
            }
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public void IsWow64Process_ReturnExpectedValue()
    {
        var expected = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess;
        var actual = IsWow64Process(GetCurrentProcess());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreatePipe_ReadWrite()
    {
        SafeObjectHandle readPipe, writePipe;
        Assert.True(CreatePipe(out readPipe, out writePipe, null, 0));
        using (readPipe)
        using (writePipe)
        {
            var data = new byte[] { 1, 2, 3 };
            Assert.Equal((uint)data.Length, WriteFile(writePipe, new ArraySegment<byte>(data)));
            Assert.Equal(data, ReadFile(readPipe, (uint)data.Length));
        }
    }
}
