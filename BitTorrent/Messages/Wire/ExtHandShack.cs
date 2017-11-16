using BitTorrent.MonoTorrent.BEncoding;

namespace BitTorrent.Messages.Wire
{
    public class ExtHandShack : ExtendMessage
    {
        static readonly byte ExtHandShackID = 0;
        static readonly string UtMetadataKey = "ut_metadata";
        static readonly string MethodKey = "m";
        static readonly string MetadataSizeKey = "metadata_size";

        public bool SupportUtMetadata
        {
            get => ((BEncodedDictionary)Parameters[MethodKey]).Keys.Contains(UtMetadataKey);
            set
            {
                if (value)
                    ((BEncodedDictionary)Parameters[MethodKey])[UtMetadataKey] = new BEncodedNumber(1);
                else
                    ((BEncodedDictionary)Parameters[MethodKey]).Remove(UtMetadataKey);
            }
        }
        public byte UtMetadata
        {
            get => (byte)((BEncodedNumber)((BEncodedDictionary)Parameters[MethodKey])[UtMetadataKey]).Number;
            set => ((BEncodedDictionary)Parameters[MethodKey])[UtMetadataKey] = new BEncodedNumber(value);
        }
        public long MetadataSize
        {
            get => ((BEncodedNumber)Parameters[MetadataSizeKey]).Number;
            set => Parameters[MetadataSizeKey] = new BEncodedNumber(value);
        }

        public bool CanGetMetadate => Parameters.Keys.Contains(MetadataSizeKey) && SupportUtMetadata;

        public ExtHandShack()
            : base()
        {
            ExtTypeID = ExtHandShackID;
            Parameters[MethodKey] = new BEncodedDictionary();
        }
    }
}
