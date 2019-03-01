using System.Collections.Generic;

namespace DemokratiskDialog.Models
{
    public class UsersToCheck : List<Influencer>
    {
        public UsersToCheck() : base() { }
        public UsersToCheck(IEnumerable<Influencer> source) : base(source) { }
    }

    public class Influencer
    {
        public UserCategory Category { get; set; }
        public TwitterUser Profile { get; set; }
    }

    public enum UserCategory
    {
        Unspecified,
        Politician,
        Spin,
        Journalist,
        Lobby,
        Business,
        Union,
        Public
    }
}
