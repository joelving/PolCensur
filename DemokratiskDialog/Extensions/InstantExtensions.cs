using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Extensions
{
    public static class InstantExtensions
    {
        public static DateTime ToDanishTime(this Instant instant)
            => instant.InZone(DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")).ToDateTimeUnspecified();
    }
}
