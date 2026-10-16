/// <summary>User-mode hardware counters and CPU nanoseconds for a Linux thread.</summary>
module CounterBench.LinuxPmc

open System
open System.IO
open System.Globalization
open System.Runtime.InteropServices

/// <summary>Counts over a measured region; unavailable measurements have value -1.</summary>
type Counts =
    {
        Instructions: int64
        Cycles: int64
        BranchMisses: int64
        CpuNs: int64
    }

    /// <summary>All measurements set to -1.</summary>
    static member Unmeasured =
        {
            Instructions = -1L
            Cycles = -1L
            BranchMisses = -1L
            CpuNs = -1L
        }

module private Native =
    [<DllImport("libc", EntryPoint = "syscall", SetLastError = true)>]
    extern int64 perfEventOpen(int64 number, byte[] attr, int pid, int cpu, int groupFd, unativeint flags)

    [<DllImport("libc", EntryPoint = "gettid", SetLastError = true)>]
    extern int gettid()

    [<DllImport("libc", SetLastError = true)>]
    extern int ioctl(int fd, unativeint request, nativeint arg)

    [<DllImport("libc", SetLastError = true)>]
    extern nativeint read(int fd, byte[] buffer, unativeint count)

    [<DllImport("libc")>]
    extern int close(int fd)

let private perfEventOpen =
    match RuntimeInformation.ProcessArchitecture with
    | Architecture.X64 -> Some 298L
    | Architecture.Arm64 -> Some 241L
    | _ -> None

/// <summary>Encodes a disabled, user-mode hardware event as a 112-byte perf_event_attr.</summary>
let attributes (config: uint64) : byte[] =
    let bytes = Array.zeroCreate<byte> 112
    BitConverter.GetBytes(112u).CopyTo(bytes, 4)
    BitConverter.GetBytes(config).CopyTo(bytes, 8)
    BitConverter.GetBytes(0x61UL).CopyTo(bytes, 40)
    bytes

/// <summary>Parses CPU nanoseconds from the first whitespace-separated schedstat field.</summary>
let parseSchedstat (text: string) =
    Int64.Parse (text.Split((null: char[]), StringSplitOptions.RemoveEmptyEntries).[0], CultureInfo.InvariantCulture)

/// <summary>Returns the calling Linux thread's native ID.</summary>
let currentThreadId () =
    if not (OperatingSystem.IsLinux ()) then
        raise (PlatformNotSupportedException ("Linux thread IDs require Linux."))

    let tid = Native.gettid ()

    if tid <= 0 then
        raise (IOException ("gettid failed."))

    tid

/// <summary>Owns hardware counters and reads CPU time for a Linux thread.</summary>
/// <remarks>Call Start and Stop on the measuring thread. Dispose releases native descriptors and is idempotent.</remarks>
[<Sealed>]
type ThreadCounters private (pid: int, tid: int, opened: int[]) =
    let schedstat = $"/proc/{pid}/task/{tid}/schedstat"
    let mutable fds = opened
    let mutable disposed = false
    let mutable running = false
    let mutable cpuStart = 0L

    let release () =
        for fd in fds do
            Native.close fd |> ignore

        fds <- [||]

    let ensureAlive () =
        if disposed then
            raise (ObjectDisposedException (nameof ThreadCounters))

    let command request =
        let mutable succeeded = true

        for fd in fds do
            if Native.ioctl (fd, request, 0n) <> 0 then
                succeeded <- false

        if not succeeded then
            release ()

    /// <summary>Opens counters for positive process and thread IDs; unavailable hardware falls back to CPU time.</summary>
    static member Open(pid: int, tid: int) =
        if not (OperatingSystem.IsLinux ()) then
            raise (PlatformNotSupportedException ("Thread counters require Linux."))

        if pid <= 0 then
            invalidArg (nameof pid) "A positive process ID is required."

        if tid <= 0 then
            invalidArg (nameof tid) "A positive thread ID is required."

        let opened =
            match perfEventOpen with
            | Some syscall ->
                [| 1UL; 0UL; 5UL |]
                |> Array.map (fun config -> int (Native.perfEventOpen (syscall, attributes config, tid, -1, -1, 8un)))
            | None -> [||]

        if opened |> Array.exists (fun fd -> fd < 0) then
            for fd in opened do
                if fd >= 0 then
                    Native.close fd |> ignore

            new ThreadCounters (pid, tid, [||])
        else
            new ThreadCounters (pid, tid, opened)

    /// <summary>Whether all three hardware events remain available.</summary>
    member _.Hardware = not disposed && fds.Length = 3

    /// <summary>Starts a fresh region; a failed hardware reset or enable switches to CPU time.</summary>
    member _.Start() =
        ensureAlive ()

        if running then
            invalidOp "A counter region is already running."

        cpuStart <- parseSchedstat (File.ReadAllText schedstat)
        command 0x2403un
        command 0x2400un
        running <- true

    /// <summary>Stops the active region; failed hardware reads produce -1 for all hardware measurements.</summary>
    member _.Stop() : Counts =
        ensureAlive ()

        if not running then
            invalidOp "A counter region has not been started."

        running <- false
        command 0x2401un
        let cpuEnd = parseSchedstat (File.ReadAllText schedstat)
        let values = Array.create 3 -1L

        if fds.Length = 3 then
            let buffer = Array.zeroCreate<byte> 8
            let mutable valid = true

            for index in 0..2 do
                if Native.read (fds[index], buffer, 8un) = 8n then
                    values[index] <- BitConverter.ToInt64 (buffer, 0)

                    if values[index] < 0L then
                        valid <- false
                else
                    valid <- false

            if not valid then
                release ()
                Array.fill values 0 3 -1L

        {
            Instructions = values[0]
            Cycles = values[1]
            BranchMisses = values[2]
            CpuNs = cpuEnd - cpuStart
        }

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true
                release ()

/// <summary>Whether this process can open all three Linux hardware counters.</summary>
let hardwareAvailable () =
    OperatingSystem.IsLinux ()
    && (use counters = ThreadCounters.Open (Environment.ProcessId, currentThreadId ())
        counters.Hardware)
