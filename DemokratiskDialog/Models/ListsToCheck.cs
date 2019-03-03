using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Models
{
    public class ListsToCheck : List<(string Owner, string Slug, UserCategory Category, string InternalSlug)>
    {
        public ListsToCheck() : base() { }
        public ListsToCheck(IEnumerable<(string Owner, string Slug, UserCategory Category, string InternalSlug)> source) : base(source) { }
    }
}
