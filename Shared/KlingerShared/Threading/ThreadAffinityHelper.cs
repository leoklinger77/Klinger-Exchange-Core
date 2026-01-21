using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace KlingerShared.Threading;

/// <summary>
/// Cross-platform thread affinity management for ultra-low latency applications.
/// Supports both Windows and Linux CPU core pinning.
/// </summary>
public static class ThreadAffinityHelper
{
    /// <summary>
    /// Pins the current thread to a specific CPU core.
    /// </summary>
    /// <param name="coreId">Zero-based CPU core ID to pin to</param>
    /// <returns>True if successfully pinned, false otherwise</returns>
    public static bool SetThreadAffinity(int coreId)
    {
        try
        {
            // Prevent .NET from migrating this thread
            Thread.BeginThreadAffinity();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return SetWindowsThreadAffinity(coreId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return SetLinuxThreadAffinity(coreId);
            }
            else
            {
                Log.Warning("Thread affinity not supported on {OS}", RuntimeInformation.OSDescription);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set thread affinity to core {CoreId}", coreId);
            return false;
        }
    }
    
    /// <summary>
    /// Sets thread affinity on Windows using ProcessThread API.
    /// </summary>
    private static bool SetWindowsThreadAffinity(int coreId)
    {
        var nativeThreadId = GetCurrentThreadId();
        var currentProcess = Process.GetCurrentProcess();
        
        foreach (ProcessThread thread in currentProcess.Threads)
        {
            if (thread.Id == nativeThreadId)
            {
                // Set affinity mask: 1 << coreId (e.g., Core 0 = 0x01, Core 4 = 0x10)
                var affinityMask = (IntPtr)(1 << coreId);
                thread.ProcessorAffinity = affinityMask;
                thread.PriorityLevel = ThreadPriorityLevel.Highest;
                
                Log.Information("Windows: Thread pinned to Core {CoreId} with highest priority", coreId);
                return true;
            }
        }
        
        Log.Warning("Windows: Could not find native thread to pin");
        return false;
    }
    
    /// <summary>
    /// Sets thread affinity on Linux using pthread_setaffinity_np.
    /// </summary>
    private static bool SetLinuxThreadAffinity(int coreId)
    {
        // Get native thread ID for pthread calls
        var threadId = pthread_self();
        
        // Create CPU set with single core
        cpu_set_t cpuSet = default;
        CPU_ZERO(ref cpuSet);
        CPU_SET(coreId, ref cpuSet);
        
        // Set thread affinity
        int result = pthread_setaffinity_np(threadId, (UIntPtr)Marshal.SizeOf<cpu_set_t>(), ref cpuSet);
        
        if (result == 0)
        {
            // Set thread priority (requires CAP_SYS_NICE or running as root)
            TrySetLinuxThreadPriority(threadId);
            
            Log.Information("Linux: Thread pinned to Core {CoreId}", coreId);
            return true;
        }
        else
        {
            Log.Warning("Linux: pthread_setaffinity_np failed with code {Result}", result);
            return false;
        }
    }
    
    /// <summary>
    /// Attempts to set real-time priority on Linux (requires elevated permissions).
    /// </summary>
    private static void TrySetLinuxThreadPriority(IntPtr threadId)
    {
        try
        {
            // SCHED_FIFO with priority 50 (range 1-99, 99=highest)
            const int SCHED_FIFO = 1;
            sched_param schedParam = new() { sched_priority = 50 };
            
            int result = pthread_setschedparam(threadId, SCHED_FIFO, ref schedParam);
            
            if (result == 0)
            {
                Log.Information("Linux: Thread priority set to SCHED_FIFO:50");
            }
            else if (result == 1) // EPERM
            {
                Log.Debug("Linux: Could not set real-time priority (insufficient permissions). Use 'sudo setcap cap_sys_nice=+ep <executable>' or run as root.");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Linux: Could not set thread priority");
        }
    }
    
    // Windows P/Invoke
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    
    // Linux P/Invoke - pthread functions
    [DllImport("libpthread.so.0", SetLastError = true)]
    private static extern IntPtr pthread_self();
    
    [DllImport("libpthread.so.0", SetLastError = true)]
    private static extern int pthread_setaffinity_np(IntPtr thread, UIntPtr cpusetsize, ref cpu_set_t cpuset);
    
    [DllImport("libpthread.so.0", SetLastError = true)]
    private static extern int pthread_setschedparam(IntPtr thread, int policy, ref sched_param param);
    
    // Linux CPU set structures (supports up to 1024 CPUs)
    [StructLayout(LayoutKind.Sequential)]
    private struct cpu_set_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public ulong[] __bits; // 16 * 64 = 1024 bits
        
        public cpu_set_t()
        {
            __bits = new ulong[16];
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct sched_param
    {
        public int sched_priority;
    }
    
    // CPU set manipulation macros (implemented as methods)
    private static void CPU_ZERO(ref cpu_set_t cpuSet)
    {
        cpuSet.__bits ??= new ulong[16];
        Array.Clear(cpuSet.__bits, 0, cpuSet.__bits.Length);
    }
    
    private static void CPU_SET(int cpu, ref cpu_set_t cpuSet)
    {
        int index = cpu / 64;
        int bit = cpu % 64;
        cpuSet.__bits[index] |= (1UL << bit);
    }    
}
