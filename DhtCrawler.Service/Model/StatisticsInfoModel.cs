namespace DhtCrawler.Service.Model
{
    public class StatisticsInfoModel : BaseModel<string>
    {
        public string DataKey { get; set; }
        public override string Id
        {
            get
            {
                return DataKey;
            }
            set
            {
                DataKey = value;
            }
        }
        public long Num { get; set; }
    }
}
