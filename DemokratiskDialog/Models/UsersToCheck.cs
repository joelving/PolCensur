using System.Collections.Generic;
using System.ComponentModel;

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
        [Description("Uspecificerede")]
        Unspecified,

        [Description("Politikere")]
        Politician,

        [Description("Kommunikationsfolk")]
        Spin,

        [Description("Journalister")]
        Journalist,

        [Description("Interesseorganisationer")]
        Lobby,

        [Description("Erhvervsliv")]
        Business,

        [Description("Fagforeningsfolk")]
        Union,

        [Description("Myndighedspersoner")]
        Public
    }
}
