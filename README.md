# ZEEV Network - Blockcore Implementation

## Overview

ZEEV is a blockchain network built on the Blockcore framework, implementing a proof-of-work consensus mechanism with specific parameters tailored for the ZEEV ecosystem. 

## Key Features

### Network Specifications

- **Coin Ticker**: ZEEV
- **Genesis Block**: August 18, 2023 23:56:00 UTC
- **Consensus Algorithm**: Proof of Work (PoW)
- **Block Time**: 30 seconds
- **Block Reward**: 950.5 ZEEV
- **Maximum Supply**: 1,000,000,000 ZEEV
- **Subsidy Decrease**: 9.505 ZEEV per halving
- **Halving Interval**: 26,980 blocks

### Network Parameters

#### Mainnet (ZEEVMain)
- **Default Port**: 4927
- **RPC Port**: 31350
- **API Port**: 30566
- **Maximum Connections**: 16 outbound, 117 inbound
- **Magic Bytes**: 0x7a656576 ("zeev")
- **Bech32 Prefix**: "zv"

#### Testnet (ZEEVTest)
- **Default Port**: 4927
- **RPC Port**: 31350
- **API Port**: 30566
- **Maximum Connections**: 16 outbound, 117 inbound
- **Magic Bytes**: 0x7665657a ("veez")
- **Bech32 Prefix**: "zv"

### Block and Transaction Specifications

- **Maximum Block Size**: 2.5 MB base size
- **Maximum Block Serialized Size**: 10 MB
- **Maximum Transaction Weight**: 1,000,000
- **Minimum Transaction Fee**: 2,000 satoshis
- **Maximum Transaction Fee**: 100,000,000 satoshis (1 ZEEV)
- **Fallback Fee**: 20,000 satoshis
- **Minimum Relay Fee**: 1,000 satoshis
- **Coinbase Maturity**: 25 blocks

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

#### 1. Consensus Layer
- **ZEEVConsensus**: Custom consensus implementation
- **ZEEVConsensusFactory**: Factory for creating consensus-specific objects
- **ZEEVLWMA**: Linear Weighted Moving Average difficulty adjustment
- **Target Time**: 14 days retarget window
- **Miner Confirmation Window**: 2016 blocks

#### 2. Network Layer
- **P2P Protocol**: Based on Blockcore's P2P implementation
- **Connection Management**: Optimized for ZEEV network characteristics
- **Peer Discovery**: DNS seeding support (currently empty)
- **Message Protocol**: Custom magic bytes for network identification

#### 3. Deployment Features
- **BIP34**: Height in coinbase (active from genesis)
- **BIP65**: CHECKLOCKTIMEVERIFY (active from genesis)
- **BIP66**: Strict DER signatures (active from genesis)
- **CSV**: CHECKSEQUENCEVERIFY (always active)
- **Segwit**: Segregated Witness (always active)

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
dotnet build Blockcore.sln
```

3. Run the ZEEV node:
```bash
cd Node/Blockcore.Node
dotnet run --chain=ZEEV
```

### Configuration

The node uses a configuration file `zeev.conf` in the data directory. Key configuration options include:

- **Network Selection**: `--chain=ZEEV` (mainnet) or `--chain=ZEEVTest` (testnet)
- **Data Directory**: `ZEEV/ZEEVMain` or `ZEEV/ZEEVTest`
- **Logging**: Configurable through standard logging frameworks

### Running a Node

#### Mainnet Node
```bash
dotnet run --chain=ZEEV
```

#### Testnet Node
```bash
dotnet run --chain=ZEEVTest
```

## API and RPC

### RESTful API
The node exposes a RESTful API on port 30566 (default) providing:
- Block information
- Transaction details
- Network statistics
- Wallet operations (when wallet feature is enabled)

### JSON-RPC Interface
Traditional JSON-RPC interface available on port 31350:
- Mining operations
- Blockchain queries
- Transaction broadcasting
- Network information

## Features

### Enabled Features

The ZEEV network supports the following Blockcore features:

1. **Consensus**: Core consensus validation and chain management
2. **Memory Pool**: Transaction pool management
3. **Miner**: Block creation and mining operations
4. **Block Store**: Persistent block storage
5. **RPC**: JSON-RPC interface
6. **API**: RESTful web API
7. **Wallet**: HD wallet functionality
8. **Notifications**: Event-driven notifications

### Mining

The network uses a custom difficulty adjustment algorithm (ZEEVLWMA) that:
- Adjusts difficulty based on recent block times
- Maintains 30-second average block time
- Prevents difficulty manipulation attacks
- Ensures stable block production

### Security Features

- **Checkpoints**: Network checkpoints for additional security
- **Peer Banning**: Automatic banning of misbehaving peers
- **Transaction Validation**: Comprehensive transaction validation
- **Script Validation**: Full script validation including witness scripts
- **Replay Protection**: Built-in replay protection mechanisms

## Development

### Project Structure

```
Networks/Blockcore.Networks.ZEEV/
├── ZEEVMain.cs                 # Mainnet configuration
├── ZEEVTest.cs                 # Testnet configuration
├── Networks.cs                 # Network selector
├── Consensus/                  # Consensus-specific implementations
├── Deployments/                # BIP deployment configurations
├── Policies/                   # Network policies
├── Rules/                      # Validation rules
└── Components/                 # Additional components
```

### Key Classes

- **ZEEVMain**: Mainnet network configuration
- **ZEEVTest**: Testnet network configuration
- **ZEEVConsensus**: Consensus parameters and rules
- **ZEEVConsensusFactory**: Factory for consensus objects
- **ZEEVLWMA**: Difficulty adjustment algorithm

### Extending the Network

To add custom functionality:

1. Create custom rules in the `Rules/` directory
2. Implement custom consensus logic in `Consensus/`
3. Add network-specific components in `Components/`
4. Register new features in the node builder

## Testing

### Unit Tests
Run the test suite:
```bash
dotnet test
```

### Integration Tests
The project includes integration tests for:
- Network consensus validation
- Block generation and validation
- Transaction processing
- P2P communication

### Testnet
Use the testnet for development and testing:
```bash
dotnet run --chain=ZEEVTest
```

## Deployment

### Production Deployment

1. Build in release mode:
```bash
dotnet build -c Release
```

2. Configure production settings in `zeev.conf`
3. Set up monitoring and logging
4. Deploy with proper security measures

### Docker Support
The project can be containerized for easy deployment:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY . /app
WORKDIR /app
ENTRYPOINT ["dotnet", "Blockcore.Node.dll", "--chain=ZEEV"]
```

## Monitoring and Maintenance

### Health Checks
- Node synchronization status
- Peer connection health
- Memory pool status
- Block validation performance

### Logging
Comprehensive logging covers:
- Network events
- Consensus validation
- Transaction processing
- Error handling

### Performance Metrics
- Block processing time
- Transaction throughput
- Network latency
- Memory usage

## Security Considerations

### Network Security
- Regular security updates
- Peer validation
- DoS protection
- Network isolation options

### Operational Security
- Secure key management
- Access control
- Monitoring and alerting
- Backup procedures

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests
4. Submit a pull request
5. Follow code review process

### Code Standards
- Follow C# coding conventions
- Include comprehensive tests
- Document public APIs
- Use meaningful commit messages

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Support

For support and questions:
- GitHub Issues: Report bugs and feature requests
- Documentation: Comprehensive API documentation
- Community: Join the ZEEV community for discussions

## Roadmap

### Upcoming Features
- Enhanced mining algorithms
- Improved peer discovery
- Performance optimizations
- Additional API endpoints

### Long-term Goals
- Layer 2 scaling solutions
- Smart contract support
- Cross-chain interoperability
- Enhanced privacy features

---

*This documentation is for the ZEEV network implementation based on the Blockcore framework. For the latest updates and detailed technical specifications, refer to the source code and API documentation.*