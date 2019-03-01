using Newtonsoft.Json;

namespace DemokratiskDialog.Models.Twitter
{
    public class ListMembersResponse
    {
        [JsonProperty("users")]
        public TwitterUser[] Users { get; set; }

        [JsonProperty("next_cursor")]
        public double NextCursor { get; set; }

        [JsonProperty("next_cursor_str")]
        public string NextCursorStr { get; set; }

        [JsonProperty("previous_cursor")]
        public long PreviousCursor { get; set; }

        [JsonProperty("previous_cursor_str")]
        public string PreviousCursorStr { get; set; }
    }
}
