using System.Diagnostics;
using System.Runtime.InteropServices;
using DiagThreadState = System.Diagnostics.ThreadState;

namespace GearShift.Core.System;

/// <summary>
/// Process freeze/thaw via the undocumented <c>NtSuspendProcess</c> / <c>NtResumeProcess</c>. Suspending
/// parks every thread of a process: it stays resident in RAM with all state and connections intact but
/// gets no CPU, so a background app can be frozen during a game and thawed afterwards with its session
/// preserved. Whether a process is currently frozen is read back from the OS (thread wait-reason), not
/// tracked in memory — so the declarative diff engine stays stateless and survives an app restart.
/// </summary>
public sealed partial class ProcessManager
{
    private const uint PROCESS_SUSPEND_RESUME = 0x0800;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);

    public bool Suspend(string match) => ForEachMatch(match, NtSuspendProcess);

    public bool Resume(string match) => ForEachMatch(match, NtResumeProcess);

    private static bool ForEachMatch(string match, Func<IntPtr, int> op)
    {
        var name = NormalizeName(match);
        var acted = false;
        foreach (var p in Process.GetProcessesByName(name))
        {
            try
            {
                var handle = OpenProcess(PROCESS_SUSPEND_RESUME, false, (uint)p.Id);
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        if (op(handle) >= 0) acted = true; // NTSTATUS >= 0 means success
                    }
                    finally
                    {
                        CloseHandle(handle);
                    }
                }
            }
            catch
            {
                // Process exited between enumeration and open, or access denied; skip it.
            }
            finally
            {
                p.Dispose();
            }
        }
        return acted;
    }

    /// <summary>
    /// Lower-cased names of every process that is fully frozen — all threads parked with wait-reason
    /// <see cref="ThreadWaitReason.Suspended"/>. Anything with at least one live thread is excluded.
    /// </summary>
    public IReadOnlySet<string> SuspendedProcessNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (IsFullySuspended(p))
                    set.Add(p.ProcessName.ToLowerInvariant() + ".exe");
            }
            catch
            {
                // Thread metadata denied for protected/system processes; treat as not frozen.
            }
            finally
            {
                p.Dispose();
            }
        }
        return set;
    }

    private static bool IsFullySuspended(Process p)
    {
        var threads = p.Threads;
        if (threads.Count == 0)
            return false;

        foreach (ProcessThread t in threads)
        {
            // Short-circuits before touching WaitReason, which is only valid in the Wait state.
            // Aliased: implicit usings pull in System.Threading.ThreadState too, and the enclosing
            // GearShift.Core.System namespace shadows a bare "System.Diagnostics".
            if (t.ThreadState != DiagThreadState.Wait || t.WaitReason != ThreadWaitReason.Suspended)
                return false;
        }
        return true;
    }
}
