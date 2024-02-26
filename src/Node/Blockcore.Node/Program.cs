using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Builder;
using Blockcore.Configuration;
using Blockcore.Features.Miner;
using Blockcore.Features.Miner.Api.Controllers;
using Blockcore.Features.Miner.Interfaces;
using Blockcore.Features.RPC.Exceptions;
using Blockcore.Features.RPC;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Features.Wallet.Types;
using Blockcore.Utilities;
using Microsoft.AspNetCore.Mvc;
using Blockcore.NBitcoin;
using Blockcore.Features.RPC.Controllers;
using Blockcore.Controllers;
using System.Reflection;

namespace Blockcore.Node
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                string chain = args
                   .DefaultIfEmpty("--chain=BTC")
                   .Where(arg => arg.StartsWith("--chain", ignoreCase: true, CultureInfo.InvariantCulture))
                   .Select(arg => arg.Replace("--chain=", string.Empty, ignoreCase: true, CultureInfo.InvariantCulture))
                   .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(chain))
                {
                    chain = "BTC";
                }

                NodeSettings nodeSettings = NetworkSelector.Create(chain, args);
                IFullNodeBuilder nodeBuilder = NodeBuilder.Create(chain, nodeSettings);

                IFullNode node = nodeBuilder.Build();
     
                Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { TestFee(node); }).GetAwaiter();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex);
            }
        }

        public static void TestFee(IFullNode fullNode)
        {
            var f = new Target(new uint256("0000000000ffff00000000000000000000000000000000000000000000000000")).ToBigInteger();
            var b = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")).ToBigInteger();
            var a = new Target(new uint256("00000000ffff0000000000000000000000000000000000000000000000000000")).ToBigInteger();
            var s = new Target(new uint256("7fffff0000000000000000000000000000000000000000000000000000000000")).ToBigInteger();

            var mining = fullNode.NodeService<IPowMining>();
            var wallet = fullNode.NodeService<IWalletManager>();

            var RPC = fullNode.NodeController<FullNodeController>();
            var RPCmining = fullNode.NodeController<MiningRpcController>();

            try
            {
                string walletName = wallet.GetWalletsNames().FirstOrDefault();

                if (string.IsNullOrEmpty(walletName))
                {
                    var password = "testtest";

                    wallet.CreateWallet(password, "default", "resource"); //purpose: 84 - segwit
                }

                walletName = wallet.GetWalletsNames().FirstOrDefault();
                IHdAccount account = wallet.GetAccounts(walletName).FirstOrDefault();

                var accountReference = new WalletAccountReference(walletName, account.Name);

                HdAddress address = wallet.GetUnusedAddress(accountReference);
                var addresses = wallet.GetUnusedAddresses(accountReference, 10);
                var addresses2 = wallet.GetUnusedChangeAddress(accountReference);

                foreach (var item in addresses)
                {
                    Console.WriteLine(item.Address);
                }

                //   var ss = RPCmining.SubmitBlock("a4feb20073584e650000000000000000000000000000000000000000000000007091a1420b944c14e6e2973cbbda3cd06ab8995f57ccacd5c81327cf43d5a7010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000dfbd1089603ae909369ed623e7a05220ad5bd4be74d98ec79c1be32ecf14a6cd00000020ffff7f200101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff275a0c6d696e6564206279206873640473584e650030000001de9ff2110a4d696e696e67636f7265000000000180e66b21160000001976a914cbb39fd33f409b187a4d8bc8a5c08f631bc6740288ac00000000");
                //       var sxxx = RPC.GetBlock("00000000c698be4f084c39d49a1e4f5d01d3e78af4624e23bbfc1879ff63b3cb");

                // var res = mining.GenerateBlocks(new ReserveScript(address.Pubkey), 100, uint.MaxValue);

            }
            catch (Exception e)
            {
                //exist then nothing
            }
        }
    }
}
