using LinqToDB.Mapping;
using System;
using System.Collections.Generic;

namespace FeuerwehrListen.Models
{
    [Table("fire_safety_watches")]
    public class FireSafetyWatch
    {
        [PrimaryKey, Identity]
        public int Id { get; set; }

        [Column("Name"), NotNull]
        public string Name { get; set; } = "";

        [Column("Location"), NotNull]
        public string Location { get; set; } = "";

        [Column("EventDateTime"), NotNull]
        public DateTime EventDateTime { get; set; }

        [Column("Status"), NotNull]
        public ListStatus Status { get; set; } = ListStatus.Open;

        [Column("ClosedAt")]
        public DateTime? ClosedAt { get; set; }

        [Column("IsArchived")]
        public bool IsArchived { get; set; } = false;
    }
}
