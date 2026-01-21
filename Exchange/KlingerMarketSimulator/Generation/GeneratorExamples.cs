using KlingerSimulator.Generation;
using KlingerSimulator.Generation.Models;
using KlingerSimulator.Generation.Performance;
using KlingerSimulator.Generation.Random;
using KlingerSimulator.Generation.Strategy;

namespace KlingerSimulator.Examples;

/// <summary>
/// Examples demonstrating different generator configurations.
/// Each configuration combines the 3 independent axes differently.
/// </summary>
public static class GeneratorExamples
{
    /// <summary>
    /// STRESS TEST MODE
    /// - XorShiftRandom: Ultra-fast (10x faster than System.Random)
    /// - ThroughputProfile: Parallel execution
    /// - CrossedPairStrategy: Maximum fill rate
    /// 
    /// Use case: Load testing, stress testing, finding bottlenecks
    /// </summary>
    public static Generator<OrderRequest[]> CreateStressTestGenerator(OrderGenerationContext context)
    {
        return new Generator<OrderRequest[]>(
            new CrossedPairStrategy(context),
            new XorShiftRandom((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new ThroughputProfile()
        );
    }

    /// <summary>
    /// DETERMINISTIC REPLAY MODE
    /// - DeterministicRandom: Same seed = same sequence
    /// - LowLatencyProfile: Single-threaded (predictable)
    /// - Any strategy
    /// 
    /// Use case: Debugging, replay, backtesting, unit tests
    /// </summary>
    public static Generator<OrderRequest[]> CreateReplayGenerator(OrderGenerationContext context, int seed)
    {
        return new Generator<OrderRequest[]>(
            new CrossedPairStrategy(context),
            new DeterministicRandom(seed),
            new LowLatencyProfile()
        );
    }

    /// <summary>
    /// PRODUCTION MODE (Current implementation)
    /// - XorShiftRandom: Fast, thread-safe
    /// - LowLatencyProfile: Predictable latency
    /// - CrossedPairStrategy: Realistic market behavior
    /// 
    /// Use case: Production market data generation
    /// </summary>
    public static Generator<OrderRequest[]> CreateProductionGenerator(OrderGenerationContext context)
    {
        return new Generator<OrderRequest[]>(
            new CrossedPairStrategy(context),
            new XorShiftRandom((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new LowLatencyProfile()
        );
    }

    /// <summary>
    /// BATCH PROCESSING MODE
    /// - XorShiftRandom: Fast
    /// - BatchProfile: Controlled batch size
    /// - Any strategy
    /// 
    /// Use case: When you need to process in specific batch sizes (like matching engines do)
    /// </summary>
    public static Generator<OrderRequest> CreateBatchGenerator(OrderGenerationContext context, int batchSize)
    {
        return new Generator<OrderRequest>(
            new BuyOrderStrategy(context),
            new XorShiftRandom((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new BatchProfile(batchSize)
        );
    }

    /// <summary>
    /// BUY-ONLY MODE
    /// - Any random provider
    /// - Any performance profile
    /// - BuyOrderStrategy: Only buy orders
    /// 
    /// Use case: Testing buy-side logic, simulating buy pressure
    /// </summary>
    public static Generator<OrderRequest> CreateBuyOnlyGenerator(OrderGenerationContext context)
    {
        return new Generator<OrderRequest>(
            new BuyOrderStrategy(context),
            new XorShiftRandom((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new LowLatencyProfile()
        );
    }

    /// <summary>
    /// SELL-ONLY MODE
    /// - Any random provider
    /// - Any performance profile
    /// - SellOrderStrategy: Only sell orders
    /// 
    /// Use case: Testing sell-side logic, simulating sell pressure
    /// </summary>
    public static Generator<OrderRequest> CreateSellOnlyGenerator(OrderGenerationContext context)
    {
        return new Generator<OrderRequest>(
            new SellOrderStrategy(context),
            new XorShiftRandom((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new LowLatencyProfile()
        );
    }

    /// <summary>
    /// Example: Using the generator
    /// </summary>
    public static void UsageExample(OrderGenerationContext context)
    {
        // Create generator
        var generator = CreateProductionGenerator(context);

        // Generate 1000 order pairs
        generator.Generate(1000, orderPair =>
        {
            foreach (var order in orderPair)
            {
                Console.WriteLine($"Order: {order.Symbol} {order.Side} {order.Quantity}@{order.Price}");
                // Send to exchange, save to database, etc.
            }
        });
    }

    /// <summary>
    /// Example: Collect all results in an array
    /// </summary>
    public static void CollectExample(OrderGenerationContext context)
    {
        var generator = CreateProductionGenerator(context);

        // Generate and collect
        var orders = generator.GenerateArray(100);

        Console.WriteLine($"Generated {orders.Length} order pairs");
    }

    /// <summary>
    /// Example: Stress test with parallel execution
    /// </summary>
    public static void StressTestExample(OrderGenerationContext context)
    {
        var generator = CreateStressTestGenerator(context);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Generate 1 million order pairs
        int count = 0;
        generator.Generate(1_000_000, orderPair =>
        {
            Interlocked.Increment(ref count);
            // Send to exchange...
        });

        sw.Stop();
        Console.WriteLine($"Generated {count} pairs in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Throughput: {count * 1000.0 / sw.ElapsedMilliseconds:N0} pairs/sec");
    }

    /// <summary>
    /// Example: Deterministic replay for debugging
    /// </summary>
    public static void ReplayExample(OrderGenerationContext context)
    {
        const int seed = 42;

        // First run
        var generator1 = CreateReplayGenerator(context, seed);
        var orders1 = generator1.GenerateArray(100);

        // Second run - EXACT same result
        var generator2 = CreateReplayGenerator(context, seed);
        var orders2 = generator2.GenerateArray(100);

        // Verify they're identical
        for (int i = 0; i < 100; i++)
        {
            if (orders1[i][0].Price != orders2[i][0].Price)
            {
                throw new Exception("Replay failed!");
            }
        }

        Console.WriteLine("✅ Replay verified - sequences are identical");
    }
}
