using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Taskcode.Probe
{
    internal sealed class ProviderProcessJob : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        { internal long ProcessTime, JobTime; internal uint Flags; internal UIntPtr MinimumWorkingSet, MaximumWorkingSet; internal uint ActiveProcesses; internal UIntPtr Affinity; internal uint Priority, Scheduling; }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters
        { internal ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
        { internal BasicLimits Basic; internal IoCounters Io; internal UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int type, ref ExtendedLimits limits, uint size);
        [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private IntPtr handle;
        internal ProviderProcessJob(Process process)
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } }; // KILL_ON_JOB_CLOSE
            if (handle == IntPtr.Zero || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimits))) || !AssignProcessToJobObject(handle, process.Handle))
            { Dispose(); try { process.Kill(); } catch (InvalidOperationException) { } throw new InvalidOperationException("Could not bound the provider process."); }
        }
        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
    }
}
