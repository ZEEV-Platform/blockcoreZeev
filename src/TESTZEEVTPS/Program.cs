using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;

namespace TESTZEEVTPS
{
    /// <summary>
    /// Program for running TPS tests and benchmarks - extended with real transactions
    /// Includes blockchain constraints: 30-second blocks, 2.5MB block size
    /// </summary>
    class Program
    {
        // Blockchain parameters
        public const int BLOCK_TIME_SECONDS = 30;
        public const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
        public const double THEORETICAL_MAX_TPS = BLOCK_SIZE_BYTES / (1680.0 * BLOCK_TIME_SECONDS); // Assuming ~1680 bytes per tx

        public static async Task Main(string[] args)
        {
            args = new string[] { "" };

            Console.WriteLine("=== Blockcore TPS Performance Tests ===");
            Console.WriteLine($"Blockchain Parameters: {BLOCK_TIME_SECONDS}s blocks, {BLOCK_SIZE_BYTES / 1_048_576.0:F1}MB block size");
            Console.WriteLine($"Theoretical Max TPS: ~{THEORETICAL_MAX_TPS:F2} TPS");
            Console.WriteLine();

            if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Starting BenchmarkDotNet tests...");
                Console.WriteLine("This may take several minutes...");
                Console.WriteLine();
                
                // Running benchmarks
                var summary = BenchmarkRunner.Run<TPSBenchmark>();
                
                Console.WriteLine();
                Console.WriteLine("Benchmark completed. Results are saved in the BenchmarkDotNet.Artifacts folder.");
            }
            else if (args.Length > 0 && args[0].Equals("quick", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Starting quick TPS tests...");
                await RunQuickTests();
            }
            else if (args.Length > 0 && args[0].Equals("real", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Starting tests with real transactions...");
                await RunRealTransactionTests();
            }
            else if (args.Length > 0 && args[0].Equals("compare", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Starting comparison of simulated vs real transactions...");
                await RunComparisonTests();
            }
            else if (args.Length > 0 && args[0].Equals("block", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Starting block constraint analysis...");
                await RunBlockConstraintTests();
            }
            else
            {
                Console.WriteLine("Starting complete TPS tests...");
                await RunCompleteTests();
            }

            Console.WriteLine();
            Console.WriteLine("Done! Press any key...");
            Console.ReadKey();
        }

        private static async Task RunQuickTests()
        {
            var tester = new SimpleTpsTest();
            
            Console.WriteLine("1. Basic TPS test (1000 transactions)...");
            var result1 = await tester.MeasureTPS(1000, "Quick Test");
            Console.WriteLine($"   Result: {result1.TPS:F2} TPS\n");
            
            Console.WriteLine("2. Multi-thread TPS test...");
            var result2 = await tester.MeasureParallelTPS(2000, 4, "Parallel Quick Test");
            Console.WriteLine($"   Result: {result2.TPS:F2} TPS\n");
            
            Console.WriteLine("=== Quick Tests Summary ===");
            Console.WriteLine($"Sequential TPS: {result1.TPS:F2}");
            Console.WriteLine($"Parallel TPS: {result2.TPS:F2}");
            Console.WriteLine($"Speedup: {result2.TPS / result1.TPS:F2}x");
            
            // Block analysis
            PrintBlockEfficiencyAnalysis(result1, result2);
        }

        private static async Task RunRealTransactionTests()
        {
            try
            {
                var tester = new SimpleTpsTest(useRealTransactions: true);
                
                Console.WriteLine("=== Tests with real blockchain transactions ===\n");
                
                // 1. Basic real transaction test
                Console.WriteLine("1. Basic real transaction test (500 tx)...");
                var result1 = await tester.MeasureRealTPS(500, "Real TX Basic");
                Console.WriteLine($"   {result1}");
                Console.WriteLine();
                
                // 2. Parallel real transaction test
                Console.WriteLine("2. Parallel real transaction test (1000 tx, 4 threads)...");
                var result2 = await tester.MeasureParallelRealTPS(1000, 4, "Real TX Parallel");
                Console.WriteLine($"   {result2}");
                Console.WriteLine();
                
                // 3. Block capacity test
                Console.WriteLine("3. Block capacity test:");
                var blockCapacityTest = await RunBlockCapacityTest(tester);
                Console.WriteLine($"   Max transactions per block: {blockCapacityTest.TransactionsPerBlock}");
                Console.WriteLine($"   Block utilization: {blockCapacityTest.BlockUtilization:P1}");
                Console.WriteLine();
                
                // 4. Scaling test
                Console.WriteLine("4. Real transaction scaling test:");
                var threadCounts = new[] { 1, 2, 4, 6, 8 };
                foreach (var threads in threadCounts)
                {
                    if (threads > Environment.ProcessorCount) continue;
                    
                    Console.Write($"   {threads} threads... ");
                    var result = await tester.MeasureParallelRealTPS(threads * 200, threads, $"Scale-{threads}");
                    Console.WriteLine($"{result.TPS:F2} TPS (Success: {result.SuccessRate:P1})");
                }
                
                Console.WriteLine("\n=== Real Transaction Analysis ===");
                Console.WriteLine($"• Best single-thread TPS: {result1.TPS:F2}");
                Console.WriteLine($"• Best parallel TPS: {result2.TPS:F2}");
                Console.WriteLine($"• Average transaction size: {result1.AverageTransactionSize} bytes");
                Console.WriteLine($"• Average fees: {result1.FeesPerTransaction:F0} plancks/tx");
                Console.WriteLine($"• Generation success rate: {result1.SuccessRate:P1}");
                
                // Block constraint analysis
                PrintBlockConstraintAnalysis(result1, result2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing real transactions: {ex.Message}");
                Console.WriteLine("Try running basic tests using the 'quick' parameter.");
            }
        }

        private static async Task RunBlockConstraintTests()
        {
            try
            {
                var tester = new SimpleTpsTest(useRealTransactions: true);
                
                Console.WriteLine("=== Block Constraint Analysis ===\n");
                Console.WriteLine($"Block Parameters: {BLOCK_TIME_SECONDS}s, {BLOCK_SIZE_BYTES / 1_048_576.0:F1}MB");
                Console.WriteLine($"Theoretical Max TPS: {THEORETICAL_MAX_TPS:F2}\n");
                
                // Test different transaction sizes
                var transactionSizes = new[] { 250, 500, 750, 1000, 1500 };
                
                foreach (var avgTxSize in transactionSizes)
                {
                    Console.WriteLine($"Testing with average transaction size: {avgTxSize} bytes");
                    
                    var maxTxPerBlock = BLOCK_SIZE_BYTES / avgTxSize;
                    var theoreticalTPS = maxTxPerBlock / (double)BLOCK_TIME_SECONDS;
                    
                    Console.WriteLine($"  Max transactions per block: {maxTxPerBlock}");
                    Console.WriteLine($"  Theoretical TPS: {theoreticalTPS:F2}");
                    
                    // Test actual performance
                    var testCount = Math.Min(maxTxPerBlock / 2, 1000);
                    var result = await tester.MeasureRealTPS((int)testCount, $"BlockTest-{avgTxSize}b");
                    
                    var efficiency = result.TPS / theoreticalTPS;
                    Console.WriteLine($"  Actual TPS: {result.TPS:F2} (Efficiency: {efficiency:P1})");
                    Console.WriteLine($"  Avg actual tx size: {result.AverageTransactionSize} bytes");
                    Console.WriteLine();
                }
                
                // Block fill simulation
                await RunBlockFillSimulation(tester);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in block constraint tests: {ex.Message}");
            }
        }

        private static async Task<BlockCapacityResult> RunBlockCapacityTest(SimpleTpsTest tester)
        {
            // Generate transactions until block is full
            var transactions = 0;
            var totalSize = 0;
            var maxTransactions = BLOCK_SIZE_BYTES / 250; // Conservative estimate
            
            while (totalSize < BLOCK_SIZE_BYTES && transactions < maxTransactions)
            {
                var result = await tester.MeasureRealTPS(10, $"Capacity-{transactions}");
                if (result.AverageTransactionSize > 0)
                {
                    var newSize = totalSize + (result.AverageTransactionSize * 10);
                    if (newSize > BLOCK_SIZE_BYTES) break;
                    
                    totalSize = (int)newSize;
                    transactions += 10;
                }
                else break;
            }
            
            return new BlockCapacityResult
            {
                TransactionsPerBlock = transactions,
                BlockUtilization = totalSize / (double)BLOCK_SIZE_BYTES,
                AverageTransactionSize = transactions > 0 ? totalSize / transactions : 0
            };
        }

        private static async Task RunBlockFillSimulation(SimpleTpsTest tester)
        {
            Console.WriteLine("=== Block Fill Simulation ===");
            
            var targetTPS = THEORETICAL_MAX_TPS;
            var testDuration = BLOCK_TIME_SECONDS;
            var targetTransactions = (int)(targetTPS * testDuration);
            
            Console.WriteLine($"Simulating {testDuration}s block with target {targetTransactions} transactions...");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await tester.MeasureRealTPS(targetTransactions, "BlockFill");
            stopwatch.Stop();
            
            var actualBlockTime = stopwatch.ElapsedMilliseconds / 1000.0;
            var totalBlockSize = result.AverageTransactionSize * result.SuccessfulTransactions;
            var blockUtilization = totalBlockSize / (double)BLOCK_SIZE_BYTES;
            
            Console.WriteLine($"Results:");
            Console.WriteLine($"  Actual processing time: {actualBlockTime:F1}s");
            Console.WriteLine($"  Successful transactions: {result.SuccessfulTransactions}");
            Console.WriteLine($"  Total block size: {totalBlockSize:N0} bytes ({blockUtilization:P1} utilization)");
            Console.WriteLine($"  Actual TPS: {result.TPS:F2}");
            Console.WriteLine($"  Time efficiency: {(BLOCK_TIME_SECONDS / actualBlockTime):P1}");
        }

        private static async Task RunComparisonTests()
        {
            try
            {
                var tester = new SimpleTpsTest(useRealTransactions: true);
                
                Console.WriteLine("=== Comparison of simulated vs real transactions ===\n");
                
                var sizes = new[] { 100, 300, 500, 1000 };
                
                foreach (var size in sizes)
                {
                    Console.WriteLine($"Testing {size} transactions...");
                    var comparison = await tester.CompareSimulatedVsReal(size, $"Compare-{size}");
                    Console.WriteLine($"  Simulated: {comparison.SimulatedResult.TPS:F2} TPS");
                    Console.WriteLine($"  Real: {comparison.RealResult.TPS:F2} TPS");
                    Console.WriteLine($"  Ratio: {comparison.Speedup:F2}x faster simulation");
                    Console.WriteLine($"  Overhead: {comparison.Overhead:F2} ms per transaction");
                    Console.WriteLine();
                }
                
                Console.WriteLine("=== Comparison Conclusion ===");
                Console.WriteLine("• Simulated transactions are faster for basic testing");
                Console.WriteLine("• Real transactions provide realistic results");
                Console.WriteLine("• Use simulated for development, real for production analysis");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in comparison tests: {ex.Message}");
                await RunQuickTests(); // Fallback
            }
        }

        private static async Task RunCompleteTests()
        {
            var tester = new SimpleTpsTest();
            
            Console.WriteLine("=== Complete TPS Performance Analysis ===\n");
            
            // 1. Sequential tests with different sizes
            Console.WriteLine("1. Sequential TPS tests:");
            var sequentialResults = new System.Collections.Generic.List<TPSTestResult>();
            var sizes = new[] { 500, 1000, 2000, 5000 };
            
            foreach (var size in sizes)
            {
                Console.Write($"   Testing {size} transactions... ");
                var result = await tester.MeasureTPS(size, $"Sequential-{size}");
                sequentialResults.Add(result);
                Console.WriteLine($"{result.TPS:F2} TPS");
            }
            
            Console.WriteLine();
            
            // 2. Parallel tests
            Console.WriteLine("2. Parallel TPS tests:");
            var parallelResults = new System.Collections.Generic.List<TPSTestResult>();
            var threadCounts = new[] { 2, 4, 8, Environment.ProcessorCount };
            
            foreach (var threadCount in threadCounts)
            {
                Console.Write($"   Testing {threadCount} threads (2000 tx)... ");
                var result = await tester.MeasureParallelTPS(2000, threadCount, $"Parallel-{threadCount}");
                parallelResults.Add(result);
                Console.WriteLine($"{result.TPS:F2} TPS");
            }
            
            Console.WriteLine();
            
            // 3. Stability test
            Console.WriteLine("3. Stability test (5 rounds of 1000 tx):");
            var stabilityResults = new System.Collections.Generic.List<TPSTestResult>();
            
            for (int i = 1; i <= 5; i++)
            {
                Console.Write($"   Round {i}/5... ");
                var result = await tester.MeasureTPS(1000, $"Stability-{i}");
                stabilityResults.Add(result);
                Console.WriteLine($"{result.TPS:F2} TPS");
                
                await Task.Delay(200); // Short pause between rounds
            }
            
            Console.WriteLine();
            
            // 4. Real transaction test (if possible)
            try
            {
                Console.WriteLine("4. Real transaction sample test:");
                var realTester = new SimpleTpsTest(useRealTransactions: true);
                var realResult = await realTester.MeasureRealTPS(200, "Real Sample");
                Console.WriteLine($"   Real transactions: {realResult.TPS:F2} TPS (Success: {realResult.SuccessRate:P1})");
                Console.WriteLine();
                
                // Comparison
                var avgSimulated = sequentialResults.Average(r => r.TPS);
                Console.WriteLine($"   Comparison: Simulated {avgSimulated:F2} vs Real {realResult.TPS:F2} TPS");
                Console.WriteLine($"   Factor: {avgSimulated / realResult.TPS:F2}x faster simulation");
                
                // Block analysis
                PrintBlockConstraintAnalysis(realResult);
            }
            catch
            {
                Console.WriteLine("4. Real transaction test is not available");
            }
            
            // Results analysis
            PrintDetailedAnalysis(sequentialResults, parallelResults, stabilityResults);
        }

        private static void PrintBlockEfficiencyAnalysis(TPSTestResult sequential, TPSTestResult parallel)
        {
            Console.WriteLine("\n=== Block Efficiency Analysis ===");
            
            var sequentialBlockTx = sequential.TPS * BLOCK_TIME_SECONDS;
            var parallelBlockTx = parallel.TPS * BLOCK_TIME_SECONDS;
            
            Console.WriteLine($"Transactions per {BLOCK_TIME_SECONDS}s block:");
            Console.WriteLine($"  Sequential: {sequentialBlockTx:F0} transactions");
            Console.WriteLine($"  Parallel: {parallelBlockTx:F0} transactions");
            Console.WriteLine($"  Block time efficiency: {(sequentialBlockTx / (BLOCK_SIZE_BYTES / 1500.0)):P1}");
        }

        private static void PrintBlockConstraintAnalysis(RealTPSTestResult result1, RealTPSTestResult? result2 = null)
        {
            Console.WriteLine("\n=== Block Constraint Analysis ===");
            
            var avgTxSize = result1.AverageTransactionSize > 0 ? result1.AverageTransactionSize : 1500;
            var maxTxPerBlock = BLOCK_SIZE_BYTES / avgTxSize;
            var theoreticalTPS = maxTxPerBlock / (double)BLOCK_TIME_SECONDS;
            
            Console.WriteLine($"• Average transaction size: {avgTxSize} bytes");
            Console.WriteLine($"• Max transactions per block: {maxTxPerBlock}");
            Console.WriteLine($"• Theoretical max TPS: {theoreticalTPS:F2}");
            Console.WriteLine($"• Actual TPS efficiency: {(result1.TPS / theoreticalTPS):P1}");
            
            if (result2 != null)
            {
                Console.WriteLine($"• Parallel TPS efficiency: {(result2.TPS / theoreticalTPS):P1}");
            }
            
            if (result1.TPS > theoreticalTPS)
            {
                Console.WriteLine("⚠️  Warning: TPS exceeds theoretical maximum - check transaction sizes");
            }
        }

        private static void PrintDetailedAnalysis(
            System.Collections.Generic.List<TPSTestResult> sequential,
            System.Collections.Generic.List<TPSTestResult> parallel,
            System.Collections.Generic.List<TPSTestResult> stability)
        {
            Console.WriteLine("=== Detailed Results Analysis ===\n");
            
            // Sequential analysis
            Console.WriteLine("Sequential Performance:");
            var avgSequential = sequential.Average(r => r.TPS);
            var minSequential = sequential.Min(r => r.TPS);
            var maxSequential = sequential.Max(r => r.TPS);
            
            Console.WriteLine($"  Average: {avgSequential:F2} TPS");
            Console.WriteLine($"  Range: {minSequential:F2} - {maxSequential:F2} TPS");
            Console.WriteLine($"  Variability: {((maxSequential - minSequential) / avgSequential * 100):F1}%");
            Console.WriteLine();
            
            // Parallel analysis
            Console.WriteLine("Parallel Performance:");
            var avgParallel = parallel.Average(r => r.TPS);
            var minParallel = parallel.Min(r => r.TPS);
            var maxParallel = parallel.Max(r => r.TPS);
            
            Console.WriteLine($"  Average: {avgParallel:F2} TPS");
            Console.WriteLine($"  Range: {minParallel:F2} - {maxParallel:F2} TPS");
            Console.WriteLine($"  Best speedup: {maxParallel / avgSequential:F2}x");
            Console.WriteLine();
            
            // Stability analysis
            Console.WriteLine("Stability Analysis:");
            var avgStability = stability.Average(r => r.TPS);
            var minStability = stability.Min(r => r.TPS);
            var maxStability = stability.Max(r => r.TPS);
            var stdDev = Math.Sqrt(stability.Average(r => Math.Pow(r.TPS - avgStability, 2)));
            
            Console.WriteLine($"  Average: {avgStability:F2} TPS");
            Console.WriteLine($"  Standard deviation: {stdDev:F2}");
            Console.WriteLine($"  Coefficient of variation: {(stdDev / avgStability * 100):F1}%");
            Console.WriteLine($"  Range: {minStability:F2} - {maxStability:F2} TPS");
            Console.WriteLine();
            
            // Block performance analysis
            Console.WriteLine("Block Performance Analysis:");
            var avgBlockCapacity = (avgSequential * BLOCK_TIME_SECONDS);
            var maxBlockCapacity = (maxParallel * BLOCK_TIME_SECONDS);
            var theoreticalMax = BLOCK_SIZE_BYTES / 1500.0; // Assuming 1500 byte transactions
            
            Console.WriteLine($"  Average transactions per block: {avgBlockCapacity:F0}");
            Console.WriteLine($"  Max transactions per block: {maxBlockCapacity:F0}");
            Console.WriteLine($"  Theoretical maximum: {theoreticalMax:F0}");
            Console.WriteLine($"  Block utilization: {(maxBlockCapacity / theoreticalMax):P1}");
            Console.WriteLine();
            
            // Recommendations
            Console.WriteLine("=== Recommendations ===");
            Console.WriteLine($"• Optimal TPS for sequential processing: ~{avgSequential:F0} TPS");
            Console.WriteLine($"• Optimal TPS for parallel processing: ~{maxParallel:F0} TPS");
            Console.WriteLine($"• Recommended thread count: {parallel.OrderByDescending(r => r.TPS).First().TestName.Split('-')[1]}");
            Console.WriteLine($"• Block constraint compliance: {(maxParallel < THEORETICAL_MAX_TPS ? "✅ Within limits" : "⚠️ Exceeds theoretical maximum")}");
            
            if (stdDev / avgStability < 0.1)
                Console.WriteLine("• Performance is stable (low variability)");
            else if (stdDev / avgStability < 0.2)
                Console.WriteLine("• Performance is slightly variable");
            else
                Console.WriteLine("• Performance is highly variable - optimization may be needed");
                
            Console.WriteLine();
            Console.WriteLine("=== Execution Methods ===");
            Console.WriteLine("dotnet run         - Complete tests");
            Console.WriteLine("dotnet run quick   - Quick tests");
            Console.WriteLine("dotnet run real    - Real transactions");
            Console.WriteLine("dotnet run compare - Compare simulated vs real");
            Console.WriteLine("dotnet run block   - Block constraint analysis");
            Console.WriteLine("dotnet run benchmark - BenchmarkDotNet analysis");
        }
    }

    /// <summary>
    /// Result of block capacity testing
    /// </summary>
    public class BlockCapacityResult
    {
        public int TransactionsPerBlock { get; set; }
        public double BlockUtilization { get; set; }
        public int AverageTransactionSize { get; set; }
    }
}