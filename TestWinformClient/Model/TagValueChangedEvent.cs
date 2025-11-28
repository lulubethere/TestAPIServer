using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TestWinformClient.Model
{
    internal class TagValueChangedEvent
    {
        [JsonPropertyName("clientHandle")]
        public string ClientHandle { get; set; } = string.Empty;
        
        [JsonPropertyName("value")]
        public object? Value { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
