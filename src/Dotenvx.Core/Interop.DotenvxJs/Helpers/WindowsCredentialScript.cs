namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/windowsCredentialManager.js - the PowerShell script, verbatim.</remarks>
internal static class WindowsCredentialScript
{
    /// <summary>The script upstream passes to `powershell.exe -EncodedCommand` (UTF-16LE, base64).</summary>
    public const string Script = """

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class DotenvxCredentialManager
{
    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;
    private const int ERROR_NOT_FOUND = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    public static string Read(string target)
    {
        IntPtr credentialPtr;
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out credentialPtr))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ERROR_NOT_FOUND) return null;
            throw new Win32Exception(error);
        }

        try
        {
            Credential credential = (Credential)Marshal.PtrToStructure(credentialPtr, typeof(Credential));
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return null;
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public static void Write(string target, string username, string secret)
    {
        IntPtr secretPtr = Marshal.StringToCoTaskMemUni(secret);
        try
        {
            Credential credential = new Credential();
            credential.Type = CRED_TYPE_GENERIC;
            credential.TargetName = target;
            credential.CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(secret);
            credential.CredentialBlob = secretPtr;
            credential.Persist = CRED_PERSIST_LOCAL_MACHINE;
            credential.UserName = username;

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(secretPtr);
        }
    }

    public static void Delete(string target)
    {
        if (!CredDelete(target, CRED_TYPE_GENERIC, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
'@

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

if ($payload.action -eq 'read') {
    $secret = [DotenvxCredentialManager]::Read([string]$payload.target)
    if ($null -ne $secret) {
        [Console]::Out.Write($secret)
    }
} elseif ($payload.action -eq 'write') {
    [DotenvxCredentialManager]::Write([string]$payload.target, [string]$payload.username, [string]$payload.secret)
} elseif ($payload.action -eq 'delete') {
    [DotenvxCredentialManager]::Delete([string]$payload.target)
} else {
    throw "unsupported credential action"
}

""";
}
