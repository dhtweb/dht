namespace DhtCrawler.Service.Model
{
    public class KeyWordModel : BaseModel<string>
    {
        public override string Id { get; set; }
        public string Word { get; set; }
        public int Num { get; set; }
        public bool IsDanger { get; set; }

    }
}
