using KlingerShared.Config;

namespace KlingerExchange.Config {
    public class BrokerConfig : ConfigBase<BrokerConfig> {
        public Dictionary<int, BrokerDto> Broker { get; set; } = new Dictionary<int, BrokerDto>();
    }


    public class BrokerDto {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string ParticipantType { get; set; }
        public string[] Segments { get; set; }
        public string Status { get; set; }
    }

}
