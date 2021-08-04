namespace eCommerceAutomation.Scrapper
{
    public class ApplicationOptions
    {
        public string ExchangeName
        {
            get;
            set;
        }

        public string RequestQueueName
        {
            get;
            set;
        }

        public string ResponseQueueName
        {
            get;
            set;
        }

        public int TelegramCacheInMinutes
        {
            get;
            set;
        }

        public Models.ProxyOptions ProxyOptions
        {
            get;
            set;
        }
    }
}
