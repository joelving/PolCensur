using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DemokratiskDialog.Models
{
    public class UsersToCheck : Dictionary<UserCategory, List<TwitterUser>>
    {
        public IEnumerable<TwitterUser> All => Values.SelectMany(u => u);

        private Dictionary<string, TwitterUser> idIndex = null;
        private void RebuildIdIndex() => idIndex = All.GroupBy(u => u.IdStr).ToDictionary(g => g.Key, g => g.First());

        public TwitterUser FindById(string id)
        {
            if (idIndex == null)
                RebuildIdIndex();

            return idIndex.ContainsKey(id) ? idIndex[id] : null;
        }
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
