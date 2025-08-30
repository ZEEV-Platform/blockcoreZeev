# ZEEV Network - Blockcore Implementation

## Overview

ZEEV is a blockchain network built on the Blockcore framework, implementing a proof-of-work consensus mechanism with specific parameters tailored for the ZEEV ecosystem. This repository contains the core network implementation and comprehensive testing and benchmarking tools.

## Key Features

### Network Specifications

- **Coin Ticker**: ZEEV
- **Consensus Algorithm**: Proof of Work (PoW)
- **Block Time**: 20 seconds (as implemented in tests)
- **Block Size**: 2 MB maximum
- **Block Reward**: 20 ZEEV
- **Maximum Supply**: 1,013,000,000 ZEEV
- **Subsidy Decrease**: 0.05 ZEEV per subsidy decrease (every quarter)
- **Subsidy Interval**: 262980 blocks
- **Target Difficulty Time**: 90 blocks

### Network Parameters

#### Mainnet (ZEEVMain)
- **Default Port**: 4927
- **RPC Port**: 31350
- **API Port**: 30566
- **Magic Bytes**: 0x7a656576 ("zeev")
- **Bech32 Prefix**: "zv"
- **Coinbase Maturity**: 3000
- **Confirmation Window**: 131490

#### Testnet (ZEEVTest)
- **Default Port**: 4927
- **RPC Port**: 31350
- **API Port**: 30566
- **Magic Bytes**: 0x7665657a ("veez")
- **Bech32 Prefix**: "zv"
- **Coinbase Maturity**: 1
- **Confirmation Window**: 13149

### Block and Transaction Specifications

- **Maximum Block Size**: 2 MB
- **Maximum Transaction Weight**: 1,000,000
- **Minimum Transaction Fee**: 2,000 plancks
- **Maximum Transaction Fee**: 100,000,000 plancks (1 ZEEV)
- **Fallback Fee**: 20,000 plancks
- **Minimum Relay Fee**: 1,000 plancks
- **Theoretical Max TPS**: ~62.5 TPS (based on 2MB blocks, 20s block time, ~1680 bytes per transaction)

### Address Formats

#### Base58 Prefixes
- **Public Key Address**: 80 (P prefix)
- **Script Address**: 142 (p prefix)
- **Private Key**: 125 (M prefix)
- **Extended Public Key**: 0x776f6c66
- **Extended Private Key**: 0x666c6f77

#### Bech32 Addresses
- **Prefix**: "zv"
- **Witness Public Key**: Supported
- **Witness Script**: Supported

## Architecture

### Core Components

#### 1. Network Implementation
- **Blockcore.Networks.ZEEV**: ZEEV network configuration and consensus
- **ZEEVMain**: Mainnet network configuration
- **ZEEVTest**: Testnet network configuration
- **ZEEVConsensus**: Custom consensus implementation
- **ZEEVLWMA**: Linear Weighted Moving Average difficulty adjustment

#### 2. Consensus Features
- **BIP34**: Height in coinbase (active from genesis)
- **BIP65**: CHECKLOCKTIMEVERIFY (active from genesis)
- **BIP66**: Strict DER signatures (active from genesis)
- **CSV**: CHECKSEQUENCEVERIFY (always active)
- **Segwit**: Segregated Witness (always active)

## Testing and Benchmarking Suite

The project includes comprehensive testing and benchmarking tools for performance analysis and network validation.

### TPS Testing Suite (TESTZEEVTPS)

The `TESTZEEVTPS` project provides advanced transaction throughput testing capabilities:

#### Key Components

1. **RealTransactionGenerator**
   - Generates cryptographically valid signed transactions
   - Supports batch transaction generation
   - Provides transaction validation and analysis
   - Simulates realistic blockchain constraints
   - UTXO management and tracking

2. **TPSBenchmark**
   - Professional benchmarking using BenchmarkDotNet
   - Sequential and parallel transaction processing tests
   - Memory and hardware counter diagnostics
   - Configurable batch sizes (100, 1000, 5000 transactions)

3. **Performance Test Suite**
   - `TPSPerformanceTests.cs`: Comprehensive performance testing
   - `SimpleTpsTest.cs`: Basic TPS validation
   - `RealTransactionTPSTests.cs`: Real transaction-based TPS testing

#### Transaction Generation Features

- **Multiple Transaction Types**: Simple, complex, and high-volume transaction patterns
- **Configurable Parameters**: Input/output counts, fees, complexity levels
- **Real Signatures**: Full cryptographic signature validation
- **UTXO Management**: Automatic UTXO set updates and tracking
- **Block Simulation**: Simulate block generation with real constraints

#### Usage Examples

Run TPS benchmarks:
```bash
cd src/TESTZEEVTPS
dotnet run benchmark
```

Run quick TPS tests:
```bash
dotnet run quick
```

Run real transaction tests:
```bash
dotnet run real
```

Run comparison tests:
```bash
dotnet run compare
```

### Other Testing Tools

- **TESTZEEV**: Basic ZEEV network testing
- **TESTCompression**: Compression algorithm testing
- **TESTFalcon**: Falcon signature testing
- **TESTHandshake**: Network handshake testing

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or compatible IDE
- Git for version control

### Building the Project

1. Clone the repository:
```bash
git clone <repository-url>
cd blockcore-zeev
```

2. Build the solution:
```bash
cd src
dotnet build Blockcore.sln
```

3. Run TPS tests:
```bash
cd TESTZEEVTPS
dotnet run
```

### Running a ZEEV Node

```bash
cd Node/Blockcore.Node
dotnet run --chain=ZEEV
```

For testnet:
```bash
dotnet run --chain=ZEEVTest
```

## Performance Analysis

The testing suite provides detailed performance metrics:

### Transaction Analysis
- Transaction size and fee analysis
- Script type distribution
- Space efficiency calculations
- Block constraint validation

### Block Generation Simulation
- Real-time block filling simulation
- Transaction throughput measurement
- Success rate analysis
- Performance bottleneck identification

### Benchmarking Results
The BenchmarkDotNet integration provides:
- Precise TPS measurements
- Memory allocation analysis
- Hardware counter metrics
- Performance regression detection

## Development

### Project Structure

```
src/
├── Blockcore.sln                    # Main solution file
├── Blockcore/                       # Core blockchain implementation
├── Networks/
│   └── Blockcore.Networks.ZEEV/     # ZEEV network implementation
├── TESTZEEVTPS/                     # TPS testing and benchmarking
│   ├── RealTransactionGenerator.cs  # Real transaction generation
│   ├── TPSBenchmark.cs             # BenchmarkDotNet tests
│   ├── TPSPerformanceTests.cs      # Performance test suite
│   └── Program.cs                  # Test runner
├── TESTZEEV/                       # Basic ZEEV testing
├── TESTCompression/                # Compression testing
├── TESTFalcon/                     # Falcon signature testing
└── TESTHandshake/                  # Handshake testing
```

### Key Classes

#### Network Implementation
- **ZEEVMain**: Mainnet configuration
- **ZEEVTest**: Testnet configuration
- **ZEEVConsensus**: Consensus rules and parameters

#### Testing Framework
- **RealTransactionGenerator**: Creates valid signed transactions
- **TransactionGenerationOptions**: Configures transaction parameters
- **SignedTransactionData**: Represents complete transaction data
- **TransactionAnalysis**: Provides transaction analysis metrics
- **BlockGenerationResult**: Block simulation results

### Extending the Testing Suite

To add new performance tests:

1. Create test classes in the `TESTZEEVTPS` project
2. Implement benchmark methods using BenchmarkDotNet attributes
3. Add test scenarios to the Program.cs runner
4. Use RealTransactionGenerator for realistic transaction data

## Deployment

### Production Build

1. Build in release mode:
```bash
dotnet build -c Release
```

2. Configure production settings
3. Deploy with monitoring and security measures

### Performance Monitoring

The testing suite generates comprehensive reports including:
- Transaction throughput metrics
- Memory usage analysis
- Hardware performance counters
- Block constraint compliance

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement changes with comprehensive tests
4. Run the TPS test suite to validate performance
5. Submit a pull request

### Code Standards
- Follow C# coding conventions
- Include performance tests for new features
- Document public APIs thoroughly
- Validate changes with the benchmarking suite

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Support

For support and questions:
- **GitHub Issues**: Report bugs and feature requests
- **Performance Issues**: Include TPS test results when reporting
- **Testing**: Use the comprehensive test suite for validation

---

*This documentation reflects the current ZEEV network implementation with comprehensive TPS testing and benchmarking capabilities. For detailed performance metrics and technical specifications, run the test suite and refer to the generated reports.*