using KlingerShared.Config;

namespace KlingerExchange.Config {
    public class ExchangeConfig : ConfigBase<ExchangeConfig> {
        public bool EnableEventStore { get; set; }
        public string? EventStoreDir { get; set; }
        public string? LogDirectory { get; set; }
    }
}
