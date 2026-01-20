using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace KlingerShared.Config {
    public static class ConfigBase<T> {

        private static readonly ConcurrentDictionary<Type, object> _cache = new();

        public static T LoadConfig() {
            var type = typeof(T);

            // Retorna do cache se já existir
            if (_cache.TryGetValue(type, out var cached) && cached is T config) {
                return config;
            }

            try {
                var path = Path.Combine(AppContext.BaseDirectory, "Config", $"{type.Name}.json");

                if (!File.Exists(path)) {
                    throw new FileNotFoundException($"Configuration file not found: {path}");
                }

                var json = File.ReadAllText(path);

                var deserialized = JsonSerializer.Deserialize<T>(json)
                    ?? throw new InvalidOperationException($"Failed to deserialize {type.Name}");

                // Add ou Update de forma thread-safe
                _cache.AddOrUpdate(
                    type,
                    deserialized,
                    (_, __) => deserialized
                );

                return deserialized;
            } catch {
                throw;
            }
        }
    }
}
