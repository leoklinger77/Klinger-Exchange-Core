using Serilog;
using ZeroFormatter;

namespace KlingerExchange.EventStore;

/// <summary>
/// Lê eventos do EventStore para replay/recovery
/// </summary>
public sealed class EventStoreReader : IDisposable
{
    private readonly ILogger _log = Log.ForContext<EventStoreReader>();
    private FileStream? _fileStream;

    public EventStoreReader(string filePath)
    {        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Arquivo de eventos não encontrado: {filePath}");

        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _log.Information("EventStoreReader aberto: {FilePath}", filePath);
    }

    /// <summary>
    /// Lê todos os eventos do arquivo sequencialmente
    /// </summary>
    public IEnumerable<(EventHeader Header, object Event)> ReadAll()
    {
        if (_fileStream == null)
            yield break;

        _fileStream.Position = 0;
        long eventsRead = 0;

        while (_fileStream.Position < _fileStream.Length)
        {
            EventHeader header;
            object? eventData = null;
            
            // Lê header
            var headerSize = GetHeaderSize();
            var headerBuffer = new byte[headerSize];
            var bytesRead = _fileStream.Read(headerBuffer, 0, headerSize);
            
            if (bytesRead < headerSize)
                break; // Fim do arquivo

            try
            {
                header = ZeroFormatterSerializer.Deserialize<EventHeader>(headerBuffer);

                // Lê payload
                var payloadBuffer = new byte[header.PayloadSize];
                bytesRead = _fileStream.Read(payloadBuffer, 0, header.PayloadSize);
                
                if (bytesRead < header.PayloadSize)
                {
                    _log.Warning("Payload incompleto no evento {SeqNum}", header.SequenceNumber);
                    break;
                }

                // Deserializa evento específico
                eventData = header.EventType switch
                {
                    EventType.OrderAccepted => ZeroFormatterSerializer.Deserialize<OrderAcceptedEvent>(payloadBuffer),
                    EventType.OrderFilled => ZeroFormatterSerializer.Deserialize<OrderFilledEvent>(payloadBuffer),
                    EventType.OrderPartiallyFilled => ZeroFormatterSerializer.Deserialize<OrderPartiallyFilledEvent>(payloadBuffer),
                    EventType.OrderCancelled => ZeroFormatterSerializer.Deserialize<OrderCancelledEvent>(payloadBuffer),
                    EventType.Trade => ZeroFormatterSerializer.Deserialize<TradeEvent>(payloadBuffer),
                    _ => throw new InvalidOperationException($"EventType desconhecido: {header.EventType}")
                };

                eventsRead++;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Erro lendo evento na posição {Position}", _fileStream.Position);
                yield break;
            }

            if (eventData != null)
                yield return (header, eventData);
        }

        _log.Information("Total de eventos lidos: {Count}", eventsRead);
    }

    /// <summary>
    /// Calcula tamanho do header (fixo para ZeroFormatter structs)
    /// </summary>
    private int GetHeaderSize()
    {
        // EventHeader: long(8) + byte(1) + long(8) + int(4) = ~21 bytes + overhead ZeroFormatter
        // Vamos ler um bloco maior para garantir
        return 128; // Oversized para segurança, ZeroFormatter tem overhead variável
    }

    public void Dispose()
    {
        _fileStream?.Dispose();
        _fileStream = null;
    }
}
