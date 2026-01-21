using KlingerExchange.MarketData.StructModels;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KlingerExchange.MarketData.Publisher;

public class UdpMulticastPublisher : IDisposable {
    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _multicastEndpoint;
    private readonly byte[] _sendBuffer;
    private readonly GCHandle _bufferHandle;
    private readonly Core.RingBuffer _ringBuffer;
    private readonly Thread _publisherThread;
    private volatile bool _isRunning;
        
    public const string MulticastAddress = "239.1.1.100";
    public const int MulticastPort = 9900;
    public const int MTU = 1500; // Maximum Transmission Unit

    public UdpMulticastPublisher(Core.RingBuffer ringBuffer) {
        _ringBuffer = ringBuffer;

        // Create UDP client for multicast - bind to ANY to allow multicast
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try {
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 256 * 1024);
        } catch (Exception ex) {
            Log.Warning(ex, "Could not set SendBuffer size");
        }

        // Enable multicast loopback so local subscribers receive packets
        try {
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
            Log.Information("Multicast loopback enabled");
        } catch (Exception ex) {
            Log.Warning(ex, "Could not set MulticastLoopback");
        }

        // Set multicast interface to any (0.0.0.0) for Docker compatibility
        try {
            // Use any interface (0.0.0.0) bytes in network order - works with Docker port mapping
            byte[] anyBytes = { 0, 0, 0, 0 };
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                BitConverter.ToInt32(anyBytes, 0));
            Log.Information("Multicast interface set to any (0.0.0.0)");
        } catch (Exception ex) {
            Log.Warning(ex, "Could not set MulticastInterface to any");
        }

        // Set TTL for multicast (1 = local network only)
        try {
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
        } catch (Exception ex) {
            Log.Warning(ex, "Could not set MulticastTimeToLive");
        }

        _multicastEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);

        Log.Information("UdpMulticastPublisher configured for {Address}:{Port}", MulticastAddress, MulticastPort);

        // Pinned buffer for zero-copy sends
        _sendBuffer = GC.AllocateArray<byte>(MTU, pinned: true);
        _bufferHandle = GCHandle.Alloc(_sendBuffer, GCHandleType.Pinned);

        // Create dedicated publisher thread
        _publisherThread = new Thread(PublisherLoop) {
            Name = "MarketDataPublisher",
            Priority = ThreadPriority.Highest,
            IsBackground = true
        };

        Log.Information("UdpMulticastPublisher initialized on {Address}:{Port}", MulticastAddress, MulticastPort);
    }

    public void Start() {
        if (_isRunning)
            return;

        _isRunning = true;
        _publisherThread.Start();
        Log.Information("UdpMulticastPublisher started");
    }

    public void Stop() {
        _isRunning = false;
        _publisherThread.Join(1000);
        Log.Information("UdpMulticastPublisher stopped");
    }

    private void PublisherLoop() {
        // Thread affinity pinning - cross-platform (Windows & Linux)
        const int PUBLISHER_CORE = 2;
        KlingerShared.Threading.ThreadAffinityHelper.SetThreadAffinity(PUBLISHER_CORE);

        var spinWait = new SpinWait();
        while (_isRunning) {
            if (_ringBuffer.TryRead(out var message)) {
                PublishMessage(in message);
                spinWait.Reset();
            } else {
                spinWait.SpinOnce();
            }
        }
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PublishMessage(in Core.TradeMessage message) {        
        try {
            // Zero-copy: direct memory copy to pinned buffer
            fixed (byte* bufferPtr = _sendBuffer) {
                var messagePtr = (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(in message));
                Buffer.MemoryCopy(messagePtr, bufferPtr, Core.TradeMessage.MessageSize, Core.TradeMessage.MessageSize);
            }
                        
            _udpClient.Send(_sendBuffer, Core.TradeMessage.MessageSize, _multicastEndpoint);
        } catch (Exception ex) {
            Log.Error(ex, "Error publishing message {Sequence}", message.SequenceNumber);
        }
    }

    public (long Available, int Capacity, bool IsFull) GetStats() {
        return (_ringBuffer.Available, _ringBuffer.Capacity, _ringBuffer.IsFull);
    }

    public void PublishInstrumentList(InstrumentInfo[] instruments, uint sequenceNumber) {
        try {
            var data = Core.InstrumentListSerializer.Serialize(instruments, sequenceNumber);

            Log.Information("Publishing instrument list: {Count} instruments, {Bytes} bytes as single packet",
                instruments.Length, data.Length);

            _udpClient.Send(data, data.Length, _multicastEndpoint);

            Log.Information("Published instrument list successfully: {Count} instruments, {Bytes} bytes",
                instruments.Length, data.Length);
        } catch (Exception ex) {
            Log.Error(ex, "Error publishing instrument list");
        }
    }

    public void Dispose() {
        Stop();

        if (_bufferHandle.IsAllocated)
            _bufferHandle.Free();

        _udpClient.Dispose();
    }
}
