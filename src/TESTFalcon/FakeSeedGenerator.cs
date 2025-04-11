using Org.BouncyCastle.Crypto.Prng;

namespace FalconTest
{
    public sealed class FakeSeedGenerator
    : IRandomGenerator
    {
        private byte[] Seed;
        public FakeSeedGenerator(byte[] seed)
        {
            this.Seed = seed;
        }

        public void AddSeedMaterial(byte[] seed)
        {
            this.Seed = seed;
        }

        public void AddSeedMaterial(ReadOnlySpan<byte> seed)
        {
            this.Seed = seed.ToArray();
        }

        public void AddSeedMaterial(long seed)
        {
            throw new NotImplementedException();
        }

        public void NextBytes(byte[] bytes)
        {
            Buffer.BlockCopy(this.Seed, 0, bytes, 0, this.Seed.Length);
        }

        public void NextBytes(byte[] bytes, int start, int len)
        {
            Buffer.BlockCopy(this.Seed, 0, bytes, start, len);
        }

        public void NextBytes(Span<byte> bytes)
        {
            throw new NotImplementedException();
        }
    }
}
