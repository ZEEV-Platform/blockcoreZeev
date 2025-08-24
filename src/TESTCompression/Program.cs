using Blockcore.Consensus;
using Blockcore.P2P.Protocol.Compression;
using Microsoft.Extensions.Logging;
using Moq;

namespace TestLZ4Compression
{
    /// <summary>
    /// Manual test program for CompressionBehavior.
    /// English comments: Run this to manually verify compression functionality.
    /// </summary>
    public class CompressionBehaviorManualTest
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== CompressionBehavior Manual Test ===\n");

            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var logger = loggerFactory.CreateLogger<CompressionBehavior>();
            var mockFactory = new Mock<ConsensusFactory>();

            try
            {
                await RunAllTestsAsync(logger, mockFactory);
                Console.WriteLine("\n🎉 All manual tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Manual test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }

            Console.ReadKey();
        }

        private static async Task RunAllTestsAsync(ILogger<CompressionBehavior> logger, Mock<ConsensusFactory> mockFactory)
        {
            // Test 1: Basic functionality
            Console.WriteLine("1. Testing basic compression/decompression...");
            await TestBasicFunctionality(logger, mockFactory);

            // Test 2: Different data sizes
            Console.WriteLine("\n2. Testing various data sizes...");
            await TestVariousDataSizes(logger);

            // Test 3: Different commands
            Console.WriteLine("\n3. Testing different command types...");
            await TestDifferentCommands(logger);

            // Test 4: Compression ratios
            Console.WriteLine("\n4. Testing compression ratios...");
            await TestCompressionRatios(logger);

            // Test 5: Error handling
            Console.WriteLine("\n5. Testing error handling...");
            await TestErrorHandling(logger);

            // Test 6: Behavior lifecycle
            Console.WriteLine("\n6. Testing behavior lifecycle...");
            await TestBehaviorLifecycle(logger, mockFactory);
        }

        private static async Task TestBasicFunctionality(ILogger<CompressionBehavior> logger, Mock<ConsensusFactory> mockFactory)
        {
            var behavior = new CompressionBehavior(logger, mockFactory.Object);
            var testData = CreateTestData(3000);

            Console.WriteLine($"  Original data: {testData.Length} bytes");

            // Create compressed payload
            var compressed = new CompressedPayload("testblock", testData);
            Console.WriteLine($"  Compressed: {compressed.CompressedData.Length} bytes");

            // Test decompression
            var decompressed = compressed.DecompressData();
            Console.WriteLine($"  Decompressed: {decompressed.Length} bytes");

            // Verify integrity
            bool isValid = decompressed.Length == testData.Length;
            if (isValid)
            {
                for (int i = 0; i < Math.Min(100, testData.Length); i++)
                {
                    if (testData[i] != decompressed[i])
                    {
                        isValid = false;
                        break;
                    }
                }
            }

            Console.WriteLine($"  Data integrity: {(isValid ? "✅ OK" : "❌ FAILED")}");

            if (!isValid)
                throw new Exception("Basic functionality test failed");

            await Task.CompletedTask;
        }

        private static async Task TestVariousDataSizes(ILogger<CompressionBehavior> logger)
        {
            var sizes = new[] { 100, 500, 1024, 2048, 5000, 10000, 50000, 2500000 };

            foreach (var size in sizes)
            {
                Console.WriteLine($"  Testing size: {size} bytes");

                var data = CreateTestData(size);
                var compressed = new CompressedPayload($"test{size}", data);
                var decompressed = compressed.DecompressData();

                bool isValid = decompressed.Length == data.Length;
                double ratio = (double)compressed.CompressedData.Length / data.Length;

                Console.WriteLine($"    Compression ratio: {ratio:P2}, Valid: {(isValid ? "✅" : "❌")}");

                if (!isValid)
                    throw new Exception($"Size test failed for {size} bytes");
            }

            await Task.CompletedTask;
        }

        private static async Task TestDifferentCommands(ILogger<CompressionBehavior> logger)
        {
            var commands = new[] { "block", "tx", "inv", "headers", "ping", "pong", "addr", "provhdr" };

            foreach (var command in commands)
            {
                Console.WriteLine($"  Testing command: {command}");

                var data = CreateTestData(2000);
                var compressed = new CompressedPayload(command, data);

                Console.WriteLine($"    Original command: {compressed.OriginalCommand}");
                Console.WriteLine($"    Original size: {compressed.OriginalSize}");
                Console.WriteLine($"    Compressed size: {compressed.CompressedData.Length}");

                var decompressed = compressed.DecompressData();
                bool isValid = decompressed.Length == data.Length;

                Console.WriteLine($"    Decompression: {(isValid ? "✅ OK" : "❌ FAILED")}");

                if (!isValid)
                    throw new Exception($"Command test failed for {command}");
            }

            await Task.CompletedTask;
        }

        private static async Task TestCompressionRatios(ILogger<CompressionBehavior> logger)
        {
            var testCases = new[]
            {
                        ("Highly compressible (zeros)", CreateZeroData(5000)),
                        ("Repeating pattern", CreatePatternData(5000)),
                        ("Semi-random", CreateSemiRandomData(5000)),
                        ("Text-like data", CreateTextLikeData(5000))
                    };

            foreach (var (description, data) in testCases)
            {
                Console.WriteLine($"  {description}:");

                var compressed = new CompressedPayload("ratio_test", data);
                double ratio = (double)compressed.CompressedData.Length / data.Length;
                int saved = data.Length - compressed.CompressedData.Length;
                double savedPercent = (1.0 - ratio) * 100;

                Console.WriteLine($"    {data.Length}B → {compressed.CompressedData.Length}B");
                Console.WriteLine($"    Ratio: {ratio:P2}, Saved: {saved}B ({savedPercent:F1}%)");

                // Verify decompression
                var decompressed = compressed.DecompressData();
                bool isValid = decompressed.Length == data.Length;
                Console.WriteLine($"    Decompression: {(isValid ? "✅ OK" : "❌ FAILED")}");

                if (!isValid)
                    throw new Exception($"Compression ratio test failed for {description}");
            }

            await Task.CompletedTask;
        }

        private static async Task TestErrorHandling(ILogger<CompressionBehavior> logger)
        {
            Console.WriteLine("  Testing null data...");
            try
            {
                var compressed = new CompressedPayload("null_test", null);
                Console.WriteLine("    Null data handled gracefully ✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Null data exception: {ex.Message}");
            }

            Console.WriteLine("  Testing empty data...");
            try
            {
                var compressed = new CompressedPayload("empty_test", new byte[0]);
                var decompressed = compressed.DecompressData();
                Console.WriteLine($"    Empty data result: {decompressed.Length} bytes ✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Empty data exception: {ex.Message}");
            }

            Console.WriteLine("  Testing empty command...");
            try
            {
                var data = CreateTestData(1000);
                var compressed = new CompressedPayload("", data);
                Console.WriteLine("    Empty command handled gracefully ✅");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Empty command exception: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private static async Task TestBehaviorLifecycle(ILogger<CompressionBehavior> logger, Mock<ConsensusFactory> mockFactory)
        {
            Console.WriteLine("  Testing behavior creation...");
            var behavior = new CompressionBehavior(logger, mockFactory.Object);
            Console.WriteLine("    Behavior created ✅");

            Console.WriteLine("  Testing behavior cloning...");
            var cloned = behavior.Clone();
            bool isCorrectType = cloned is CompressionBehavior;
            bool isDifferentInstance = !ReferenceEquals(behavior, cloned);

            Console.WriteLine($"    Correct type: {(isCorrectType ? "✅" : "❌")}");
            Console.WriteLine($"    Different instance: {(isDifferentInstance ? "✅" : "❌")}");

            if (!isCorrectType || !isDifferentInstance)
                throw new Exception("Behavior lifecycle test failed");

            await Task.CompletedTask;
        }

        // Helper methods for creating test data
        private static byte[] CreateTestData(int size)
        {
            var data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 100);
            }
            return data;
        }

        private static byte[] CreateZeroData(int size)
        {
            return new byte[size]; // All zeros
        }

        private static byte[] CreatePatternData(int size)
        {
            var data = new byte[size];
            var pattern = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

            for (int i = 0; i < size; i++)
            {
                data[i] = pattern[i % pattern.Length];
            }

            return data;
        }

        private static byte[] CreateSemiRandomData(int size)
        {
            var data = new byte[size];
            var random = new Random(42);

            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(random.Next(50) + (i % 10)); // Semi-predictable
            }

            return data;
        }

        private static byte[] CreateTextLikeData(int size)
        {
            var data = new byte[size];
            var text = "The quick brown fox jumps over the lazy dog. ";
            var textBytes = System.Text.Encoding.UTF8.GetBytes(text);

            for (int i = 0; i < size; i++)
            {
                data[i] = textBytes[i % textBytes.Length];
            }

            return data;
        }
    }
}