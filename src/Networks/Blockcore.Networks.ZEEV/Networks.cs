namespace Blockcore.Networks.ZEEV
{
    public static class Networks
    {
        public static NetworksSelector ZEEV
        {
            get
            {
                return new NetworksSelector(() => new ZEEVMain(), () => new ZEEVTest(), () => new ZEEVTest());
            }
        }
    }
}