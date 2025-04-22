using System;
using System.Linq;
using System.Threading;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;

namespace Blockcore.Networks.ZEEV.Crypto
{
    public sealed class HandShake
    {

        private readonly object hashLock;

        private static readonly Lazy<HandShake> SingletonInstance = new Lazy<HandShake>(LazyThreadSafetyMode.PublicationOnly);

        public HandShake()
        {
            this.hashLock = new object();
        }

        /// <summary>
        /// using the instance method is not thread safe.
        /// to calling the hashing method in a multi threaded environment use the create() method
        /// </summary>
        public static HandShake Instance => SingletonInstance.Value;

        public static HandShake Create()
        {
            return new HandShake();
        }

        public uint256 Hash(byte[] input)
        {
            var buffer = input;

            lock (this.hashLock)
            {
                var prevBlock = buffer.Skip(32).Take(32).ToArray();
                var data = buffer.Take(128).ToArray();
                var treeRoot = buffer.Skip(64).Take(32).ToArray();
                var pad8 = new byte[8];
                var pad32 = new byte[32];

                for (int i = 0; i < pad8.Length; i++)
                {
                    pad8[i] = (byte)(prevBlock[i % 32] ^ treeRoot[i % 32]);
                }

                for (int i = 0; i < pad32.Length; i++)
                {
                    pad32[i] = (byte)(prevBlock[i % 32] ^ treeRoot[i % 32]);
                }

                var left = Blake2B.Blake2B512().ComputeHash(data);
                var right = Sha3.Sha3256().ComputeHash(data.Concat(pad8).ToArray());
                var rightBC = Sha3.Sha3256().ComputeHash(data.Concat(pad8).ToArray());
                buffer = Blake2B.Blake2B256().ComputeHash(left.Concat(pad32).Concat(right).ToArray());
            }

            return new uint256(buffer.Take(32).Reverse().ToArray());
        }
    }
}
