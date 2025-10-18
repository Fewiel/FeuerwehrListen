using System;

namespace FeuerwehrListen.DTOs
{
    public class FireSafetyWatchDto : Models.FireSafetyWatch
    {
        public int TotalRequired { get; set; }
        public int TotalAssigned { get; set; }
    }
}
