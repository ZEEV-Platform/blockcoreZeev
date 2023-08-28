using System;
using System.Collections.Generic;
using Blockcore.Base.Deployments;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Checkpoints;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin.Protocol;
using Blockcore.Networks.ZEEV.Consensus;
using Blockcore.Networks.ZEEV.Deployments;
using Blockcore.Networks.ZEEV.Policies;
using Blockcore.P2P;

namespace Blockcore.Networks.ZEEV
{
    public class ZEEVMain : ZEEVNetwork
    {
        public ZEEVMain()
        {
            this.Name = "ZEEVMain";
            this.NetworkType = NetworkType.Mainnet;

            this.CoinTicker = "ZEEV";
            this.RootFolderName = "ZEEV";
            this.DefaultConfigFilename = "zeev.conf";

            var magicMessage = new byte[4];
            magicMessage[0] = 0x7a;
            magicMessage[1] = 0x65;
            magicMessage[2] = 0x65;
            magicMessage[3] = 0x76;
            uint magic = BitConverter.ToUInt32(magicMessage, 0);

            this.Magic = magic;
            this.DefaultPort = 4927; //Book of Genesis(49:27)
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 117;
            this.DefaultRPCPort = 31350; //zv 7A76 = decimal 31350
            this.DefaultAPIPort = 30566; //wf 7766 = decimal 30566
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.MaxTipAge = 48 * 60 * 60;
            this.MinTxFee = 1000;
            this.MaxTxFee = Money.Coins(1).Satoshi;
            this.FallbackFee = 20000;
            this.MinRelayTxFee = 1000;
            this.DefaultBanTimeSeconds = 60 * 60 * 24; // 500 (MaxReorg) * 64 (TargetSpacing) / 2 = 4 hours, 26 minutes and 40 seconds

            var consensusFactory = new ZEEVConsensusFactory();

            this.GenesisTime = Utils.DateTimeToUnixTime(new DateTime(2023, 8, 18, 23, 56, 00, DateTimeKind.Utc));
            this.GenesisNonce = 4927;
            this.GenesisBits = new Target(new uint256("7fffff0000000000000000000000000000000000000000000000000000000000"));
            this.GenesisVersion = 1;

            Block genesisBlock = CreateGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion);

            this.Genesis = genesisBlock;

            var consensusOptions = new ConsensusOptions
            {
                MaxBlockBaseSize = 1 * 1000 * 1000,
                MaxBlockSerializedSize = 4 * 1000 * 1000,
                MaxStandardVersion = 2,
                MaxStandardTxWeight = (4 * 1000 * 1000) / 10,
                MaxBlockSigopsCost = 80000,
                MaxStandardTxSigopsCost = 80000 / 5,
                WitnessScaleFactor = 4,
            };

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new ZEEVBIP9Deployments
            {
                [ZEEVBIP9Deployments.CSV] = new BIP9DeploymentsParameters("CSV", 0, BIP9DeploymentsParameters.AlwaysActive, 999999999, BIP9DeploymentsParameters.AlwaysActive),
                [ZEEVBIP9Deployments.Segwit] = new BIP9DeploymentsParameters("Segwit", 1, BIP9DeploymentsParameters.AlwaysActive, 999999999, BIP9DeploymentsParameters.AlwaysActive)
            };

            consensusFactory.Protocol = new ConsensusProtocol()
            {
                ProtocolVersion = ProtocolVersion.FEEFILTER_VERSION,
                MinProtocolVersion = ProtocolVersion.FEEFILTER_VERSION,
            };

            this.Consensus = new ZEEVConsensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 0,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 26980,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: genesisBlock.GetHash(),
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 0,
                defaultAssumeValid: uint256.Zero,
                maxMoney: 1000000000 * Money.COIN,
                coinbaseMaturity: 100,
                premineHeight: 0,
                premineReward: Money.Zero,
                proofOfWorkReward: Money.Coins((decimal)950.5),
                targetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                targetSpacing: TimeSpan.FromSeconds(30),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("7fffff0000000000000000000000000000000000000000000000000000000000")),
                minimumChainWork: uint256.Zero,
                isProofOfStake: false,
                lastPowBlock: default(int),
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero,
                proofOfStakeTimestampMask: 0,
                powTimeDelay: TimeSpan.FromSeconds(25),
                subsidityDecrease: Money.Coins((decimal)9.505)
            );

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (80) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (142) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (125) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x77, 0x66 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x66, 0x77 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x77), (0x6f), (0x6c), (0x66) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x66), (0x6c), (0x6f), (0x77) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x33, 0x30, 0x35, 0x36, 0x36, 0x35, 0x33 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x33, 0x31, 0x33, 0x35, 0x30 };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 122 };
         
            var encoder = new Bech32Encoder("zv");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {

            };

            this.DNSSeeds = new List<DNSSeedData>
            {

            };

            this.SeedNodes = new List<NetworkAddress>();

            this.StandardScriptsRegistry = new ZEEVStandardScriptsRegistry();

            this.RegisterRules(this.Consensus);
            this.RegisterMempoolRules(this.Consensus);
        }
    }
}