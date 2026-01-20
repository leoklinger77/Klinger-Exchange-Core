using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Serilog;

namespace KlingerExchange.MarketData.Publisher;

/// <summary>
/// UDP publisher for ultra-low latency market data distribution.
/// Uses pinned memory and unsafe operations for zero-copy sends.
/// Target: ~2-5µs per send, zero allocation.
/// Uses UDP multicast for multi-subscriber support.
/// </summary>
public class UdpMulticastPublisher : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _multicastEndpoint;
    private readonly byte[] _sendBuffer;
    private readonly GCHandle _bufferHandle;
    private readonly Core.RingBuffer _ringBuffer;
    private readonly Thread _publisherThread;
    private volatile bool _isRunning;
    
    // Multicast configuration
    public const string MulticastAddress = "239.1.1.100";
    public const int MulticastPort = 9900;
    public const int MTU = 1500; // Maximum Transmission Unit
    
    public UdpMulticastPublisher(Core.RingBuffer ringBuffer)
    {
        _ringBuffer = ringBuffer;
        
        // Create UDP client for multicast - bind to ANY to allow multicast
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        
        try
        {
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 256 * 1024);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set SendBuffer size");
        }
        
        // Enable multicast loopback so local subscribers receive packets
        try
        {
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
            Log.Information("Multicast loopback enabled");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set MulticastLoopback");
        }
        
        // Set multicast interface to loopback for local development
        try
        {
            // Use the loopback interface (127.0.0.1) bytes in network order
            byte[] loopbackBytes = { 127, 0, 0, 1 };
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, 
                BitConverter.ToInt32(loopbackBytes, 0));
            Log.Information("Multicast interface set to loopback");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set MulticastInterface to loopback");
        }
        
        // Set TTL for multicast (1 = local network only)
        try
        {
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set MulticastTimeToLive");
        }
        
        _multicastEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
        
        Log.Information("UdpMulticastPublisher configured for {Address}:{Port}", MulticastAddress, MulticastPort);
        
        // Pinned buffer for zero-copy sends
        _sendBuffer = GC.AllocateArray<byte>(MTU, pinned: true);
        _bufferHandle = GCHandle.Alloc(_sendBuffer, GCHandleType.Pinned);
        
        // Create dedicated publisher thread
        _publisherThread = new Thread(PublisherLoop)
        {
            Name = "MarketDataPublisher",
            Priority = ThreadPriority.Highest,
            IsBackground = true
        };
        
        Log.Information("UdpMulticastPublisher initialized on {Address}:{Port}", MulticastAddress, MulticastPort);
    }
    
    /// <summary>Start the publisher thread</summary>
    public void Start()
    {
        if (_isRunning)
            return;
        
        _isRunning = true;
        _publisherThread.Start();
        Log.Information("UdpMulticastPublisher started");
    }
    
    /// <summary>Stop the publisher thread</summary>
    public void Stop()
    {
        _isRunning = false;
        _publisherThread.Join(1000);
        Log.Information("UdpMulticastPublisher stopped");
    }
    
    /// <summary>Publisher loop - runs in dedicated thread with busy-wait</summary>
    private void PublisherLoop()
    {
        // Thread affinity pinning only on Windows (Linux uses taskset/numactl externally)
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var currentThread = GetCurrentThread();
                SetThreadAffinityMask(currentThread, new UIntPtr(0x4)); // Binary 100 = Core 2
                Log.Information("Publisher thread pinned to CPU core 2");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not pin publisher thread to CPU core. Performance may be suboptimal.");
            }
        }
        
        var spinWait = new SpinWait();
        while (_isRunning)
        {
            // Try to read from ring buffer
            if (_ringBuffer.TryRead(out var message))
            {
                PublishMessage(in message);
                spinWait.Reset(); // Reset on successful read
            }
            else
            {
                // Adaptive wait: SpinOnce does short spins then yields
                spinWait.SpinOnce();
            }
        }
    }
    
    /// <summary>Publish a single message via UDP multicast</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PublishMessage(in Core.TradeMessage message)
    {
        // Log.Debug removed - hot path optimization
        
        try
        {
            // Zero-copy: direct memory copy to pinned buffer
            fixed (byte* bufferPtr = _sendBuffer)
            {
                var messagePtr = (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(in message));
                Buffer.MemoryCopy(messagePtr, bufferPtr, Core.TradeMessage.MessageSize, Core.TradeMessage.MessageSize);
            }
            
            // Send to multicast group (2-5µs typical)
            _udpClient.Send(_sendBuffer, Core.TradeMessage.MessageSize, _multicastEndpoint);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error publishing message {Sequence}", message.SequenceNumber);
        }
    }
    
    /// <summary>Get statistics about the publisher</summary>
    public (long Available, int Capacity, bool IsFull) GetStats()
    {
        return (_ringBuffer.Available, _ringBuffer.Capacity, _ringBuffer.IsFull);
    }

    /// <summary>
    /// Send instrument list at market open (one-time, not performance critical)
    /// Send as single packet - UDP can handle up to ~64KB, our 2624 bytes is fine
    /// </summary>
    public void PublishInstrumentList(Core.InstrumentInfo[] instruments, uint sequenceNumber)
    {
        try
        {
            var data = Core.InstrumentListSerializer.Serialize(instruments, sequenceNumber);
            
            Log.Information("Publishing instrument list: {Count} instruments, {Bytes} bytes as single packet", 
                instruments.Length, data.Length);
            
            // Send as single UDP packet - no fragmentation needed for this size
            _udpClient.Send(data, data.Length, _multicastEndpoint);
            
            Log.Information("Published instrument list successfully: {Count} instruments, {Bytes} bytes", 
                instruments.Length, data.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error publishing instrument list");
        }
    }
    
    public void Dispose()
    {
        Stop();
        
        if (_bufferHandle.IsAllocated)
            _bufferHandle.Free();
        
        _udpClient.Dispose();
    }
    
    // P/Invoke for thread affinity (Windows)
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();
    
    [DllImport("kernel32.dll")]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);
}
