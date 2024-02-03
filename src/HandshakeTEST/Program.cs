using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks.ZEEV.Consensus;
using Blockcore.Networks.ZEEV.Crypto;
using Blockcore.Utilities.JsonConverters;
using DBreeze.Utils;
using FASTER.core;
using NLog.Targets;

namespace HandshakeTEST
{
    public class Program
    {
        //private static BigInteger pow256 = BigInteger.ValueOf(2).Pow(256);

        public static async Task Main(string[] args)
        {

      //      var s = new Blockcore.NBitcoin.Target(new uint256("0x00000000ffff0000000000000000000000000000000000000000000000000000"));
      //      var jjjj = new uint256("0x00000000ffff0000000000000000000000000000000000000000000000000000").ToBytes();
           // var sy = new Blockcore.NBitcoin.Target(new uint256("0x00ffff0000000000000000000000000000000000000000000000000000"));
      //      BigInteger Diff1 = BigInteger.Parse("00ffff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
      //      var sss = Diff1.ToByteArray();
          //  var ddd = Diff1
            HeaderTest();
  
        }

        private static void HeaderTest()
        {
            var headerHex2 = "a6496be42fe1b065000000000000000000000005b4490d1678b73c066ed15b6cbe0e98f684612504ffa4f97e34059276d78aa47040ba328b408416b61c80ac212681e1948ac2622705af27f301fcaf2eb1143da20000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000ffd4f0144d43f2d8bb5d0da049911c392ea51e8463c5e74c2ae51b70d3016f6ae037061a67848020dee27740f93d8f9a937a1912be6068e77adb85eb43cd43d000000005a6407190000000000000000000000000000000000000000000000000000000000000000";
            var header = Enumerable.Range(0, headerHex2.Length)
                 .Where(x => x % 2 == 0)
                 .Select(x => Convert.ToByte(headerHex2.Substring(x, 2), 16))
                 .ToArray();

            MemoryStream stream = new MemoryStream(header);
            var s = new BitcoinStream(stream, false);

            var ss = new ZEEVBlockHeader(new ConsensusProtocol());
            ss.ReadWrite(s);

            var serialize = ss.ToBytes();
            var serializeHex = serialize.ToHexFromByteArray();

            var bytesss = ss.ToMiner();
            var back = bytesss.ToHexFromByteArray();

            var syy = HandShake.Instance.Hash(bytesss);
            ss.CheckProofOfWork();

            verifyPOW(syy, ss.Bits);

            var target = ss.Bits;
            var targetBytes = ss.Bits.ToBytes();
            var diff = GetDifficulty(target);

        }

        public static uint ToUInt32BigEndian(byte[] bytes, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes, startIndex, 4);
            }

            return BitConverter.ToUInt32(bytes, startIndex);
        }

        public static BigInteger Double256(byte[] target)
        {
            if (target.Length != 32)
            {
                throw new ArgumentException("Target length must be 32 bytes");
            }

            BigInteger n = 0;
            BigInteger hi, lo;


            hi = ToUInt32BigEndian(target, 0);
            lo = ToUInt32BigEndian(target, 4);
            n += (hi * 0x100000000 + 7) * BigInteger.Parse("1000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);

            hi = ToUInt32BigEndian(target, 8);
            lo = ToUInt32BigEndian(target, 12);
            n += (hi * 0x100000000 + lo) * BigInteger.Parse("100000000000000000000000000000000", NumberStyles.HexNumber);

            hi = ToUInt32BigEndian(target, 16);
            lo = ToUInt32BigEndian(target, 20);
            n += (hi * 0x100000000 + lo) * BigInteger.Parse("10000000000000000", NumberStyles.HexNumber);

            hi = ToUInt32BigEndian(target, 24);
            lo = ToUInt32BigEndian(target, 28);
            n += (hi * 0x100000000 + lo) * BigInteger.Parse("1", NumberStyles.HexNumber);

            return n;
        }

        public static BigInteger GetDifficulty(Blockcore.NBitcoin.Target target)
        {
            var d = BigInteger.Parse("00000000ffff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var targetBytes = target.ToUInt256().ToBytes(false);
            var n = Double256(targetBytes);

            if (n == 0)
                return d;

            return BigInteger.Divide(d, n);
        }

        private static bool verifyPOW(uint256 hash, Blockcore.NBitcoin.Target bits)
        {
            //exports.verifyPOW = function verifyPOW(hash, bits) {
            //    const target = exports.fromCompact(bits);

            //    if (target.isNeg() || target.isZero())
            //        return false;

            //    if (target.bitLength() > 256)
            //        return false;

            //    const num = new BN(hash, 'be');

            //    if (num.gt(target))
            //        return false;

            //    return true;
            //};

            var target = bits.ToUInt256();

            if (target.CompareTo(uint256.Zero) <= 0) return false;
            if (target.ToBytes(false).Length > 256) return false;

            if (hash > target)
                return false;

            return true;
        }

        private static void OldTest()
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

                var headerHex2 = "06812500c62d536500000000000000000000000000000000000000000000000036135c35e11fe284b2a73d96095ff9921432b1ff693d7317979b379dc93b007300000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006dec03009abe99646adc6bd0f6d41c850724a289c5ddf64615f472ef6f53be3500000020ffff7f20";
                headerHex2 = "b5a2f7923a63a05f0000000000000000000000066f04019fcd528ffb1ab82304762c2f26457edff48d12150758ffdbfaa559f997d763152ac430d6ee27738415c05f7449b0d3e26055e25d93000078830000036700000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000009e2023cf8aee75f11a1c6a00f9952af023cc2e4eb39387bd7b465e2d0d5e56b759f9a94cc8a3cce63c1b4e297f4058cd445139885ca62fce32225dbb44aa0ba00000000d78a07190000000000000000000000000000000000000000000000000000000000000000";
                headerHex2 = "000000007641385e000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001a2c60b9439206938f8d7823782abdb8b211a57431e9c9b6a6365d8d428933518e4c9756fef2ad10375f360e0560fcc7587eb5223ddf8cd7c7e06e60a1140b1500000000ffff001c0000000000000000000000000000000000000000000000000000000000000000";
                headerHex2 = "c12f05dde316ad65000000000000000000000000c7906d2c5d12012fdada9d5aee51d31f8bda925abbc93d478898ad0e1af18a11ddf379ed739e9ca3b277df1d6bb73a5b181d340ee5253fbf6aac7376750f192000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000069b823b612414612c2f93c32ab081586605f5ec654985c86765eb32ed7f23986671ceb597cb03a518610e8e59fbef873d0d2cf08500811e59b258bcbf87905aa01000000459e08190000000000000000000000000000000000000000000000000000000000000000";
                headerHex2 = "a6496be42fe1b065000000000000000000000005b4490d1678b73c066ed15b6cbe0e98f684612504ffa4f97e34059276d78aa47040ba328b408416b61c80ac212681e1948ac2622705af27f301fcaf2eb1143da20000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000ffd4f0144d43f2d8bb5d0da049911c392ea51e8463c5e74c2ae51b70d3016f6ae037061a67848020dee27740f93d8f9a937a1912be6068e77adb85eb43cd43d000000005a6407190000000000000000000000000000000000000000000000000000000000000000";
                var header = Enumerable.Range(0, headerHex2.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(headerHex2.Substring(x, 2), 16))
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

                var serialize = ss.ToBytes();
                var serializeHex = serialize.ToHexFromByteArray();

                var bytesss = ss.ToMiner();
                var back = bytesss.ToHexFromByteArray();

                var syy = HandShake.Instance.Hash(bytesss);
                ss.CheckProofOfWork();

                //test hash
                var hex1 = "c12f05dde316ad650000000000000000000000000000000000000000000000000000000000000000c7906d2c5d12012fdada9d5aee51d31f8bda925abbc93d4700000000000000000000000000000000000000000000000000000000000000008898ad0e1af18a11ddf379ed739e9ca3b277df1d6bb73a5b181d340ee5253fbf6aac7376750f192000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000069b823b612414612c2f93c32ab081586605f5ec654985c86765eb32ed7f23986671ceb597cb03a518610e8e59fbef873d0d2cf08500811e59b258bcbf87905aa01000000459e08190000000000000000000000000000000000000000000000000000000000000000";

                var Hash1Bytes = Enumerable.Range(0, hex1.Length)
     .Where(x => x % 2 == 0)
     .Select(x => Convert.ToByte(hex1.Substring(x, 2), 16))
     .ToArray();
                var Hash1 = HandShake.Instance.Hash(Hash1Bytes);


                //genesis block from hns
                var base64 = "AAAAAHZBOF4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGixguUOSBpOPjXgjeCq9uLIRpXQx6cm2pjZdjUKJM1GOTJdW/vKtEDdfNg4FYPzHWH61Ij3fjNfH4G5goRQLFQAAAAD//wAcAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP//////////AdBMV3cAAAAAABTwI3ri6Phg99eRJPxRPwEuWqqNIwAAAAAAAAQgULiTf8Xe8I+fPL2n5fCMcG7bgKuliAwAAAAAAAAAAAAgLV3lhgnUlw+1SPha0HqH20DgVONMyByVHKmVpY9nTbcgENdI7aG5xnuU0yROAhFndhiptLMp6JatkEMfn0gDS60g4sApmh5GZ3NRZlXwmmSx4WsleVMN5sSlnOVlTepFGA8=";
                var enc = new Base64Encoder();
                var bytes = enc.DecodeData(base64);
                var bytesHex = bytes.ToHexFromByteArray();

                MemoryStream streamX = new MemoryStream(header);
                MemoryStream streamX2 = streamX;

                var sX = new BitcoinStream(streamX, false);

                var ssX = new ZEEVBlockHeader(new ConsensusProtocol());
                ssX.ReadWrite(sX);

                var Hash3 = HandShake.Instance.Hash(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex);
            }
        }
    }
}
