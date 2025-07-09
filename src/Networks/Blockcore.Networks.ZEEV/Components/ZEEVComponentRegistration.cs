using Blockcore.Base;
using Blockcore.Builder;
using Blockcore.Consensus;
using Blockcore.Features.Consensus;
using Blockcore.Features.Consensus.CoinViews;
using Blockcore.Features.Consensus.Rules;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.Miner.Interfaces;
using Blockcore.Features.Miner;
using Blockcore.Features.RPC;
using Blockcore.Interfaces;
using Blockcore.Mining;
using Microsoft.Extensions.DependencyInjection;
using Blockcore.Configuration.Logging;
using Blockcore.Features.Wallet;
using Blockcore.Networks.ZEEV.Consensus;

namespace Blockcore.Networks.ZEEV.Components
{
    public static class ZEEVComponentRegistration
    {
        public static IFullNodeBuilder UseZEEVConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            return AddZEEVPowMining(UseZEEVPowConsensus(fullNodeBuilder));
        }

        static IFullNodeBuilder UseZEEVPowConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<PowConsensusFeature>("powconsensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PowConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        fullNodeBuilder.PersistenceProviderManager.RequirePersistence<PowConsensusFeature>(services);

                        services.AddSingleton<ConsensusOptions, ConsensusOptions>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<IConsensusRuleEngine, PowConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());
                    });
            });

            return fullNodeBuilder;
        }

        static IFullNodeBuilder AddZEEVPowMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<RPCFeature>()
                    .DependOn<BaseWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, ZEEVMining>();
                        services.AddSingleton<IBlockProvider, ZEEVBlockProvider>();
                        services.AddSingleton<BlockDefinition, ZEEVPowBlockDefinition>();
                        services.AddSingleton<MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
