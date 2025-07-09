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
using System.IO;
using System.Runtime.CompilerServices;
using Blockcore.NBitcoin.BIP39;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin.DataEncoders;

namespace Blockcore.Node
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            //var folderPath = "G:\\_ZEEV_Git\\blockcorefalcon\\src\\Node\\Blockcore.Node\\bin\\Debug\\net8.0\\nodedata\\ZEEV\\ZEEVMain";
            //try
            //{
            //    File.Delete(folderPath + "\\txdb\\default.db");
            //}
            //catch (Exception)
            //{
            //}

            //try
            //{
            //    File.Delete(folderPath + "\\default.wallet.json");
            //}
            //catch (Exception)
            //{
            //}

            try
            {
                string chain = args
                   .DefaultIfEmpty("--chain=ZEEV")
                   .Where(arg => arg.StartsWith("--chain", ignoreCase: true, CultureInfo.InvariantCulture))
                   .Select(arg => arg.Replace("--chain=", string.Empty, ignoreCase: true, CultureInfo.InvariantCulture))
                   .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(chain))
                {
                    chain = "ZEEV";
                }

                NodeSettings nodeSettings = NetworkSelector.Create(chain, args);
                IFullNodeBuilder nodeBuilder = NodeBuilder.Create(chain, nodeSettings);

                IFullNode node = nodeBuilder.Build();

                //Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith((t) => { TestFee(node); }).GetAwaiter();

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

                var password = "ThisIsATest";
                var passphase = "Extra Seed Words";

                var mnemonicSHA3 = new Mnemonic("doctor before local return visa gauge verify net unit bunker learn silk", Wordlist.English);

                wallet.CreateWallet(password, "default", passphase, mnemonicSHA3); //purpose: 84 - segwit

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

                var message = "This is a test message!";
                var result = wallet.SignMessage(password, "default", account.Name, addresses.First().Address, message);
                var verified = wallet.VerifySignedMessage(result.SignedAddress, message, result.Signature);



            }
            catch (Exception e)
            {
                //exist then nothing
            }
        }
    }
}
