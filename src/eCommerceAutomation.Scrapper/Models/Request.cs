namespace eCommerceAutomation.Scrapper.Models
{
    public class Request
    {
        public string RequestId
        {
            get;
            set;
        }

        public Constants.RequestType Type
        {
            get;
            set;
        }

        public string Address
        {
            get;
            set;
        }
    }
}
