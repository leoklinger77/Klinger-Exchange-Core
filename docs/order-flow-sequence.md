# Order Flow Sequence - KlingerExchange

## Fluxo Temporal de uma Ordem

Este diagrama mostra o fluxo sequencial de uma ordem através das threads do sistema, destacando operações paralelas (não-bloqueantes) e bloqueantes.

```mermaid
sequenceDiagram
    participant Client as FIX Client
    participant FIXThread as 🧵 FIX Session Thread<br/>(QuickFIXn)
    participant Validate as OrderValidator<br/>(~500ns)
    participant Match as MatchingEngine<br/>(~50-200µs)
    participant Book as OrderBook<br/>(Lock)
    participant RingBuf as Lock-Free<br/>Ring Buffer
    participant EventWriter as 🧵 EventStore Writer<br/>(Background)
    participant MDBuffer as MD Ring Buffer
    participant MDThread as 🧵 Market Data<br/>(Background)
    participant RxThread as 🧵 Rx/Logging<br/>(ThreadPool)
    
    Client->>FIXThread: NewOrderSingle (FIX)
    activate FIXThread
    
    Note over FIXThread: Parse (~100ns)
    FIXThread->>Validate: Validate(OrderRequest)
    activate Validate
    Validate-->>FIXThread: ValidationResult
    deactivate Validate
    
    alt Order Rejected
        FIXThread->>Client: ExecutionReport (Rejected)
        Note over FIXThread: Exit hot path
    else Order Valid
        FIXThread->>Match: ProcessNewOrder()
        activate Match
        
        Match->>Book: AddOrder()
        activate Book
        Note over Book: Lock acquired<br/>FIFO matching
        Book-->>Match: Match results
        deactivate Book
        
        par Non-blocking operations (parallel)
            Match->>RingBuf: TryEnqueue(event)
            Note over RingBuf: Atomic CAS<br/>~50ns
            RingBuf-->>Match: seq#
        and
            Match->>MDBuffer: PublishTrade()
            Note over MDBuffer: Lock-free<br/>~20ns
        and
            Match->>RxThread: Emit(metrics)
            Note over RxThread: Rx.Subject<br/>~10ns
        end
        
        Match-->>FIXThread: (Order, Fills)
        deactivate Match
        
        Note over FIXThread: Build ExecutionReport<br/>~50ns
        FIXThread->>Client: ExecutionReport
        deactivate FIXThread
    end
    
    Note over EventWriter: Background loop<br/>always running
    
    loop Every 10ms OR 100 events
        EventWriter->>RingBuf: TryDequeueBatch(100)
        activate EventWriter
        RingBuf-->>EventWriter: EventContainer[]
        EventWriter->>EventWriter: WriteBatch(MMF)
        EventWriter->>EventWriter: fsync()
        Note over EventWriter: Group Commit<br/>Durability guaranteed
        deactivate EventWriter
    end
    
    loop Continuous
        MDThread->>MDBuffer: Consume()
        activate MDThread
        MDThread->>Client: WebSocket broadcast
        deactivate MDThread
    end
    
    Note over RxThread: Async logging<br/>No impact on hot path
    RxThread->>RxThread: Log metrics
```

## Hot Path vs Background Operations

### Hot Path (Bloqueante - ~140-150µs otimizado)
1. Parse FIX (~100ns)
2. Pre-Validation (~500ns, com cache)
3. Matching Engine (~50-200µs)
4. OrderBook Update (Lock)
5. Build ExecutionReport (~50ns, pooled)
6. Enqueue para ExecutionReportDispatcher (~50ns)

**Nota**: Send FIX (~80-100µs) foi movido para thread separada (ExecutionReportDispatcher)

### Background Operations (Não-bloqueantes)
- **EventStore**: ConcurrentQueue ~200-500ns → Dispatch thread → Ring buffer → Writer (fsync every 1ms)
- **Market Data**: Inline TryWrite ~20ns → UDP Multicast broadcast (~2-5µs)
- **Metrics/Logging**: Emit ~10ns → Background Rx logging
- **ExecutionReport**: Enqueue ~50ns → Dispatcher thread → Send FIX (~80-100µs)

## Pontos de Sincronização

| Ponto | Tipo | Latência | Impacto Hot Path |
|-------|------|----------|------------------|
| OrderBook Lock | Exclusive Lock | Incluído no matching | Sim (bloqueante) |
| EventStore ConcurrentQueue | Lock-based | ~200-500ns | Não (async) |
| EventStore Ring Buffer | Lock-Free CAS (SPSC) | ~50ns | Não (background) |
| MD Ring Buffer | Lock-Free unsafe | ~20ns | Não (async inline) |
| Rx Subject | Lock-Free | ~10ns | Não (async) |
| ExecutionReport Queue | ConcurrentQueue | ~50ns | Não (async) |
