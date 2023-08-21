using System;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.NBitcoin;
using Blockcore.Networks.ZEEV.Consensus;
using Blockcore.Networks.ZEEV.Crypto;

namespace HandshakeTEST
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {

                var headerHex =
  // nonce
  "00000000"
  // time
  + "3f42a45c00000000"
  // padding
  + "0000000000000000000000000000000000000000"
  // prev block
  + "8e4c9756fef2ad10375f360e0560fcc7587eb5223ddf8cd7c7e06e60a1140b15"
  // tree root
  + "7c7c2818c605a97178460aad4890df2afcca962cbcb639b812db0af839949798"
  // commit hash
  + "3a62731564743864425daac90ec4045f40a33379a3ed786ad8ef6ab8992802bb"
  // extra nonce
  + "000000000000000000000000000000000000000000000000"
  // reserved root
  + "0000000000000000000000000000000000000000000000000000000000000000"
  // witness root
  + "7c7c2818c605a97178460aad4890df2afcca962cbcb639b812db0af839949798"
  // merkle root
  + "8e4c9756fef2ad10375f360e0560fcc7587eb5223ddf8cd7c7e06e60a1140b15"
  // version
  + "00000000"
  // bits
  + "ffff001d";

                var header = Enumerable.Range(0, headerHex.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(headerHex.Substring(x, 2), 16))
                     .ToArray();

                MemoryStream stream = new MemoryStream(header);
                MemoryStream stream2 = stream;

                if (stream2 == stream)
                {
                    var isTrue = true;
                } 

                var s = new BitcoinStream(stream, false);

                var ss = new ZEEVBlockHeader(new ConsensusProtocol());
                ss.ReadWrite(s);

                var bytesss = ss.ToBytes();


                //var s = HandShake.Instance.Hash(header);
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex);
            }
        }
    }
}
